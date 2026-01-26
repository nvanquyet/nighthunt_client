using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.Profiling;
#endif

namespace FOW
{
    public abstract class FogOfWarRevealer : MonoBehaviour
    {
        [Header("Customization Variables")]
        [SerializeField] public float ViewRadius = 15;
        [SerializeField] public float SoftenDistance = 3;

        [Range(1f, 360)]
        [SerializeField] public float ViewAngle = 360;

        [SerializeField] public float UnobscuredRadius = 1f;

        [Range(0, 1)]
        [SerializeField] public float Opacity = 1;

        [SerializeField] protected bool AddCorners = true;

        //[SerializeField] public bool RevealHidersInFadeOutZone = true;
        [Range(0,1)]
        [SerializeField] public float RevealHiderInFadeOutZonePercentage = .5f;

        [Tooltip("how high above this object should the sight be calculated from")]
        [SerializeField] public float EyeOffset = 0;
        [Tooltip("An offset used only in the shader, to determine how high above the revealer vision height should be calculated at")]
        [SerializeField] public float ShaderEyeOffset = 0;
        [SerializeField] public float VisionHeight = 3;
        [SerializeField] public float VisionHeightSoftenDistance = 1.5f;
        [SerializeField] public bool SampleHidersAtRevealerHeight = true;
        [SerializeField] public bool CalculateHidersAtHiderHeight = false;
        [SerializeField] protected LayerMask ObstacleMask;

        [Tooltip("Static revealers are revealers that need the sight function to be called manually, similar to the 'Called Elsewhere' option on FOW world. To change this at runtime, use the SetRevealerAsStatic(bool IsStatic) Method.")]
        [SerializeField] public bool StartRevealerAsStatic = false;
        [HideInInspector] public bool StaticRevealer = false;

        [Header("Quality Settings")]
        [SerializeField] protected int MaxHidersSampledPerFrame = 50;

        [SerializeField] public RevealerQualityPreset QualityPreset = RevealerQualityPreset.HighResolution;

        [SerializeField] public float RaycastResolution = .5f;

        [Range(0, 10)]
        [SerializeField] public int NumExtraIterations = 4;

        [Range(1, 5)]
        [SerializeField] public int NumExtraRaysOnIteration = 3;
        protected int IterationRayCount;

        [Tooltip("Should this revealer find the edges of objects?.")]
        [SerializeField] public bool ResolveEdge = true;
        [Range(1, 30)]
        [Tooltip("Higher values will lead to more accurate edge detection, especially at higher distances. however, this will also result in more raycasts.")]
        [SerializeField] public int MaxEdgeResolveIterations = 10;

        [Header("Technical Variables")]
        [Range(.001f, 1)]
        [Tooltip("Lower values will lead to more accurate edge detection, especially at higher distances. however, this will also result in more raycasts.")]
        [SerializeField] protected float MaxAcceptableEdgeAngleDifference = .005f;
        [Range(.001f, 1)]
        [SerializeField] public float EdgeDstThreshold = 0.1f;
        //[SerializeField] protected float DoubleHitMaxDelta = 0.1f;
        [SerializeField] public float DoubleHitMaxAngleDelta = 15;

        [HideInInspector]
        public int FogOfWarID;
        [HideInInspector]
        public int IndexID;

        //local variables
        protected FogOfWarWorld.RevealerStruct CircleStruct;
        protected bool IsRegistered = false;
        public SightSegment[] ViewPoints;
        protected float[] EdgeAngles;
        protected float2[] EdgeNormals;
        [HideInInspector] public int NumberOfPoints;
        [HideInInspector] public float[] Angles;
        [HideInInspector] public float[] Radii;
        [HideInInspector] public bool[] AreHits;

        [Header("Debugging")]
#if UNITY_EDITOR
        [SerializeField] public bool DebugMode = false;
        [SerializeField] public bool DrawInitialRays = false;
        [SerializeField] protected int SegmentTest = -1;
        [SerializeField] public bool DrawExpectedNextPoints = false;
        [SerializeField] protected bool DrawIteritiveRays;
        [SerializeField] protected bool DrawEdgeResolveRays;
        
        [SerializeField] protected bool DrawExtraCastLines;
        [SerializeField] protected bool DrawHiderSamples;
        [SerializeField] protected bool DebugLogHiderBlockerName;
#endif
        public HashSet<FogOfWarHider> HidersSeen = new HashSet<FogOfWarHider>();
        protected Transform CachedTransform;

        public enum RevealerQualityPreset
        {
            Custom,
            ExtraLargeScaleRTS,
            LargeScaleRTS,
            MediumScaleRTS,
            SmallScaleRTS,
            HighResolution,
            OverkillResolution,
        };

        public enum RevealerMode
        {
            ConstantDensity,
            EdgeDetect,
        };

        public struct SightRay
        {
            public bool hit;
            public float2 point;
            public float distance;
            public float angle;
            public float2 normal;
            public float2 direction;

            public void SetData(bool _hit, Vector2 _point, float _distance, Vector2 _normal, Vector2 _direction)
            {
                hit = _hit;
                point = _point;
                distance = _distance;
                normal = _normal;
                direction = _direction;
            }
        }
        public struct SightSegment
        {
            public float Radius;
            public float Angle;
            public bool DidHit;

            public float2 Point;
            public float2 Direction;
            public SightSegment(float rad, float ang, bool hit, float2 point, float2 dir)
            {
                Radius = rad;
                Angle = ang;
                DidHit = hit;
                Point = point;
                Direction = dir;
            }
        }

        private void OnEnable()
        {
            RegisterRevealer();
        }

        private void OnDisable()
        {
            DeregisterRevealer();
            Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        public void RegisterRevealer()
        {
            CachedTransform = transform;
            if (StartRevealerAsStatic)
                SetRevealerAsStatic(true);
            else
                SetRevealerAsStatic(false);     //fail-safe in case someone changes the value in debug mode

            NumberOfPoints = 0;
            if (FogOfWarWorld.instance == null)
            {
                if (!FogOfWarWorld.RevealersToRegister.Contains(this))
                {
                    FogOfWarWorld.RevealersToRegister.Add(this);
                }
                return;
            }
            if (IsRegistered)
            {
                Debug.Log("Tried to double register revealer");
                return;
            }
            ViewPoints = new SightSegment[FogOfWarWorld.instance.MaxPossibleSegmentsPerRevealer];
            EdgeAngles = new float[FogOfWarWorld.instance.MaxPossibleSegmentsPerRevealer];
            EdgeNormals = new float2[FogOfWarWorld.instance.MaxPossibleSegmentsPerRevealer];

            Angles = new float[ViewPoints.Length];
            Radii = new float[ViewPoints.Length];
            AreHits = new bool[ViewPoints.Length];

            IsRegistered = true;
            FogOfWarID = FogOfWarWorld.instance.RegisterRevealer(this);
            CircleStruct = new FogOfWarWorld.RevealerStruct();
            LineOfSightPhase1();
            LineOfSightPhase2();
            //_RegisterRevealer();
        }

        public void DeregisterRevealer()
        {
            if (FogOfWarWorld.instance == null)
            {
                if (FogOfWarWorld.RevealersToRegister.Contains(this))
                {
                    FogOfWarWorld.RevealersToRegister.Remove(this);
                }
                return;
            }
            if (!IsRegistered)
            {
                //Debug.Log("Tried to de-register revealer thats not registered");
                return;
            }
            foreach (FogOfWarHider hider in HidersSeen)
            {
                hider?.RemoveObserver(this);
            }
            HidersSeen.Clear();
            IsRegistered = false;
            FogOfWarWorld.instance.DeRegisterRevealer(this);
        }

        /// <summary>
        /// Marks this revealer as static. prevents automatic recalculation of Line Of Sight.
        /// </summary>
        public void SetRevealerAsStatic(bool IsStatic)
        {
            if (IsRegistered)
            {
                if (StaticRevealer && !IsStatic)
                    FogOfWarWorld.numDynamicRevealers++;
                else if (!StaticRevealer && IsStatic)
                    FogOfWarWorld.numDynamicRevealers--;
            }
            
            StaticRevealer = IsStatic;
        }

        /// <summary>
        /// Manually calculate line of sight for this revealer.
        /// </summary>
        public void ManualCalculateLineOfSight()
        {
            LineOfSightPhase1();    //if possible, call phase 1 early in the frame, and phase 2 later in the frame!
            LineOfSightPhase2();
        }

        protected int _lastHiderIndex;
        protected abstract void _RevealHiders();
        public void RevealHiders()
        {
            _RevealHiders();
        }

        protected abstract bool _TestPoint(Vector3 point);
        public bool TestPoint(Vector3 point)
        {
            return _TestPoint(point);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void AddViewPoint(bool hit, float distance, float angle, float step, float2 normal, float2 point, float2 dir)
        {
//#if UNITY_EDITOR
//            Profiler.BeginSample("Add View Point");
//#endif
            if (NumberOfPoints == ViewPoints.Length)
            {
                Debug.LogError("Sight Segment buffer is full! Increase Maximum Segments per Revealer on Fog Of War World!");
                return;
            }
                
            ViewPoints[NumberOfPoints].DidHit = hit;
            ViewPoints[NumberOfPoints].Radius = distance;
            ViewPoints[NumberOfPoints].Angle = angle;

            ViewPoints[NumberOfPoints].Point = point;
            ViewPoints[NumberOfPoints].Direction = dir;

            EdgeAngles[NumberOfPoints] = -step;
            EdgeNormals[NumberOfPoints] = normal;
            NumberOfPoints++;
//#if UNITY_EDITOR
//            Profiler.EndSample();
//#endif
        }

        protected float heightPos;
        protected Vector2 center = new Vector2();
        protected abstract void SetCenterAndHeight();
        private void ApplyData()
        {
#if UNITY_EDITOR
            if (DebugMode)
                UnityEngine.Random.InitState(1);
#endif

            for (int i = 0; i < NumberOfPoints; i++)
            {
                Angles[i] = ViewPoints[i].Angle;
                AreHits[i] = ViewPoints[i].DidHit;
                if (!AreHits[i])
                    ViewPoints[i].Radius = Mathf.Min(ViewPoints[i].Radius, ViewRadius);
                Radii[i] = ViewPoints[i].Radius;
                if (i == NumberOfPoints - 1 && CircleIsComplete)
                {
                    Angles[i] += 360;
                }
            }

            SetCenterAndHeight();
            
            CircleStruct.CircleOrigin = center;
            CircleStruct.NumSegments = NumberOfPoints;
            CircleStruct.UnobscuredRadius = UnobscuredRadius;
            CircleStruct.CircleHeight = heightPos + ShaderEyeOffset;
            CircleStruct.CircleRadius = ViewRadius;
            CircleStruct.CircleFade = SoftenDistance;
            CircleStruct.VisionHeight = VisionHeight;
            CircleStruct.HeightFade = VisionHeightSoftenDistance;
            CircleStruct.Opacity = Opacity;

            FogOfWarWorld.instance.UpdateRevealerData(FogOfWarID, CircleStruct, NumberOfPoints, Angles, Radii, AreHits);
        }

        protected abstract float GetEuler();
        public abstract Vector3 GetEyePosition();
        public abstract Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal);
        protected abstract float AngleBetweenVector2(Vector3 _vec1, Vector3 _vec2);
        protected bool CircleIsComplete;
        
        protected bool Initialized;
        protected Vector3 EyePosition;
        protected int FirstIterationStepCount;
        protected SightIteration FirstIteration;

        protected int CommandsPerJob;
        protected CalculateNextPoints PointsJob;
        protected JobHandle PointsJobHandle;
        public NativeArray<bool> FirstIterationConditions;
        public ConditionCalculations FirstIterationConditionsJob;
        public JobHandle FirstIterationConditionsJobHandle;

        protected float RayDistance;
        public float GetRayDistance() { return RayDistance; }
        protected abstract void _InitRevealer(int StepCount);
        void InitRevealer(int StepCount, float AngleStep)
        {
            //if (FirstIteration.Distances.IsCreated)
            if (FirstIteration != null && FirstIteration.Distances.IsCreated)
                Cleanup();
            for (int i = 0; i < ViewPoints.Length; i++)
                ViewPoints[i] = new SightSegment();
            //InitialPoints = new SightRay[StepCount];
            FirstIterationStepCount = StepCount;
            FirstIteration = new SightIteration();
            FirstIteration.InitializeStruct(StepCount);
            IterationRayCount = NumExtraRaysOnIteration + 2;

            //CommandsPerJob = Mathf.Max(StepCount / Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount, 1);
            PointsJob = new CalculateNextPoints()
            {
                AngleStep = AngleStep,
                Distances = FirstIteration.Distances,
                Points = FirstIteration.Points,
                Directions = FirstIteration.Directions,
                Normals = FirstIteration.Normals,
                ExpectedNextPoints = FirstIteration.NextPoints,
            };
            FirstIterationConditions = new NativeArray<bool>(StepCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            //FirstIterationConditions = new NativeArray<bool>(NumSteps, Allocator.Persistent);
            FirstIterationConditionsJob = new ConditionCalculations()
            {
                Points = FirstIteration.Points,
                NextPoints = FirstIteration.NextPoints,
                Normals = FirstIteration.Normals,
                Hits = FirstIteration.Hits,
                IterateConditions = FirstIterationConditions
            };
            EdgeJob = new FindEdgeJob()
            {

            };
            Initialized = true;
            _InitRevealer(StepCount);
        }

        protected abstract void CleanupRevealer();
        void Cleanup()
        {
            Initialized = false;
            if (FirstIteration == null || !FirstIteration.Distances.IsCreated)
                return;

            FirstIteration.DisposeStruct();
            FirstIterationConditions.Dispose();
            foreach (SightIteration s in SubIterations)
                s.DisposeStruct();
            SubIterations.Clear();
            CleanupRevealer();
        }

        static int ComputeBatchSize(int count)
        {
            int workers = math.max(1, Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount);
            int targetJobs = workers * 5;
            if (count <= 256) return count;
            int batch = count / math.max(1, targetJobs);
            batch = math.max(32, (batch + 31) & ~31);
            return math.min(256, batch);
        }

        protected abstract void IterationOne(int NumSteps, float firstAngle, float angleStep);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void RayCast(float angle, ref SightRay ray);

        protected SightRay currentRay;
        private int NumSteps;
        private float AngleStep;
        private float cosDouble;
        protected float edgeDstThresholdSq;
        protected float angleStepRadians, SinStep, CosStep;
#if UNITY_EDITOR
        protected bool ProfileFOW = false;
#endif
        public void LineOfSightPhase1()
        {
            EdgeDstThreshold = Mathf.Max(.001f, EdgeDstThreshold);
            edgeDstThresholdSq = EdgeDstThreshold * EdgeDstThreshold;
            //Debug.Log("PHASE 1");
            CircleIsComplete = Mathf.Approximately(ViewAngle, 360);

            EyePosition = GetEyePosition();
            RayDistance = ViewRadius;
            if (FogOfWarWorld.instance.UsingSoftening)
                RayDistance += SoftenDistance;
            NumberOfPoints = 0;
#if UNITY_EDITOR
            if (ProfileFOW) Profiler.BeginSample("Line Of Sight");
#endif
            NumSteps = Mathf.Max(2, Mathf.CeilToInt(ViewAngle * RaycastResolution));
            AngleStep = ViewAngle / (NumSteps - 1);

            //CommandsPerJob = Mathf.Max(Mathf.CeilToInt(FirstIterationStepCount / Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount), 1);
            //CommandsPerJob = 32;
            //int workers = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount;
            //CommandsPerJob = math.max(1, NumSteps / math.max(1, workers * 2));
            //CommandsPerJob = math.clamp(CommandsPerJob, 16, 256);
            CommandsPerJob = ComputeBatchSize(NumSteps);

            if (!Initialized || FirstIteration == null || FirstIteration.RayAngles == null || FirstIteration.RayAngles.Length != NumSteps)
            {
                InitRevealer(NumSteps, AngleStep);
            }

#if UNITY_EDITOR
            if (ProfileFOW) Profiler.BeginSample("Iteration One");
#endif
            angleStepRadians = math.radians(AngleStep);
            math.sincos(angleStepRadians, out SinStep, out CosStep);
            cosDouble = math.cos(math.radians(DoubleHitMaxAngleDelta));

            float firstAng = ((-GetEuler() + 360 + 90) % 360) - (ViewAngle / 2);
            IterationOne(NumSteps, firstAng, AngleStep);
            ////PointsJobHandle = PointsJob.Schedule(NumSteps, 32);
            //PointsJobHandle = PointsJob.Schedule(NumSteps, CommandsPerJob);
            //PointsJobHandle.Complete();

            FirstIterationConditionsJob.DoubleHitMaxAngleDelta = DoubleHitMaxAngleDelta;
            FirstIterationConditionsJob.EdgeDstThresholdSq = edgeDstThresholdSq;
            FirstIterationConditionsJob.AddCorners = AddCorners;
            FirstIterationConditionsJob.CosDoubleHit = cosDouble;
            FirstIterationConditionsJob.SignEps = 1e-8f;
            FirstIterationConditionsJobHandle = FirstIterationConditionsJob.Schedule(NumSteps, CommandsPerJob, PointsJobHandle);
            JobHandle.ScheduleBatchedJobs();

#if UNITY_EDITOR
            if (ProfileFOW) Profiler.EndSample();
            if (ProfileFOW) Profiler.EndSample();
#endif
        }

        public void LineOfSightPhase2()
        {
            //Debug.Log("PHASE 2");
#if UNITY_EDITOR
            if (ProfileFOW) Profiler.BeginSample("Line Of Sight");
            if (ProfileFOW) Profiler.BeginSample("Complete Phase 1 Work");
#endif

            //PointsJobHandle.Complete();
            FirstIterationConditionsJobHandle.Complete();

#if UNITY_EDITOR
            if (ProfileFOW) Profiler.EndSample();
            if (ProfileFOW) Profiler.BeginSample("Sorting");
#endif
            AddViewPoint(FirstIteration.Hits[0], FirstIteration.Distances[0], FirstIteration.RayAngles[0], 0, FirstIteration.Normals[0], FirstIteration.Points[0], FirstIteration.Directions[0]);
            //AddViewPoint(new ViewCastInfo(InitialPoints[0].hit, InitialPoints[0].point, InitialPoints[0].distance, InitialPoints[0].angle, Normals[0], InitialPoints[0].direction));
            //Debug.Log(Points[0]);
            //Debug.Log(NextPoints[0]);
            //SortData(ref InitialAngles, ref FirstIteration.Hits, ref FirstIteration.Distances, ref FirstIteration.Points, ref FirstIteration.NextPoints, ref FirstIteration.Normals, AngleStep, NumSteps);
            SortData(ref FirstIteration, AngleStep, NumSteps, 0, true);


#if UNITY_EDITOR
            if (ProfileFOW) Profiler.EndSample();
            if (ProfileFOW) Profiler.BeginSample("Extra Iterations");
#endif
            while (InUseIterations.Count > 0)
                SubIterations.Push(InUseIterations.Pop());
            //CAST EXTRA ITERATIONS

#if UNITY_EDITOR
            if (ProfileFOW) Profiler.EndSample();
            if (ProfileFOW) Profiler.BeginSample("Add Data");
#endif
            //for (int i = 0; i < FirstIteration.NextIterations.Length; i++)
            //{

            //}

            if (NumberOfPoints == 1)
            {
                if (!ViewPoints[0].DidHit && !ViewPoints[1].DidHit)
                    AddViewPoint(false, ViewPoints[0].Radius, ViewPoints[0].Angle + (ViewAngle / 2), -EdgeAngles[0], new float2(0,0), new float2(0,0), new float2(0,0));
            }
            if (CircleIsComplete)
            {
                //if ((FirstIteration.Hits[NumSteps - 1] || FirstIteration.Hits[0]) && (Vector2.Distance(FirstIteration.NextPoints[NumSteps - 1], FirstIteration.Points[0]) > .05f))    //not sure why i hard coded .05 here.
                if ((FirstIteration.Hits[NumSteps - 1] || FirstIteration.Hits[0]) && (!PointsCloseEnough(FirstIteration.NextPoints[NumSteps - 1], FirstIteration.Points[0])))
                    AddViewPoint(FirstIteration.Hits[NumSteps - 1], FirstIteration.Distances[NumSteps - 1], FirstIteration.RayAngles[NumSteps - 1], 0, FirstIteration.Normals[NumSteps - 1], FirstIteration.Points[NumSteps - 1], FirstIteration.Directions[NumSteps - 1]);
                AddViewPoint(FirstIteration.Hits[0], FirstIteration.Distances[0], FirstIteration.RayAngles[0], 0, FirstIteration.Normals[0], FirstIteration.Points[0], FirstIteration.Directions[0]);
            }
            else
            {
                int n = NumSteps - 1;
                AddViewPoint(FirstIteration.Hits[n], FirstIteration.Distances[n], FirstIteration.RayAngles[n], 0, FirstIteration.Normals[n], FirstIteration.Points[n], FirstIteration.Directions[n]);
            }

#if UNITY_EDITOR
            if (ProfileFOW) Profiler.EndSample();
            if (ProfileFOW) Profiler.BeginSample("Edge Detection");
#endif

            if (ResolveEdge)
            {
                FindEdges();
                //FindEdgesJobs();  //the jobs version is much slower
            }

#if UNITY_EDITOR
            if (ProfileFOW) Profiler.EndSample();
            if (ProfileFOW) Profiler.EndSample();
#endif
            ApplyData();
        }
        
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SortData(ref SightIteration iteration, float angleStep, int iterationSteps, int iterationNumber, bool isFirstIteration = false)
        {
            //Profiler.BeginSample($"-iteration {iterationNumber}");
            const float signEps = 1e-8f;
            float newAngleStep = angleStep / (IterationRayCount - 1);

            var hits = iteration.Hits;
            var points = iteration.Points;
            var nextPoints = iteration.NextPoints;
            var normals = iteration.Normals;
            var rayAngles = iteration.RayAngles;
            var dirs = iteration.Directions;

            for (int i = 1; i < iterationSteps; i++)
            {
#if UNITY_EDITOR
                if (DebugMode && DrawExpectedNextPoints && !isFirstIteration)
                    Debug.DrawLine(Get3Dfrom2D(points[i]), Get3Dfrom2D(iteration.NextPoints[i]) + FogOfWarWorld.UpVector * (.03f / (iterationNumber+1)), UnityEngine.Random.ColorHSV());
#endif


                #region calculate if we need to fire extra rays or not.

                bool iterateAgain;
                if (!isFirstIteration)
                {
                    bool hitPrev = hits[i - 1];
                    bool hitCurr = hits[i];

                    if (!(hitPrev | hitCurr))
                        continue;

                    //we always need to sample if the hit state changed
                    if (hitPrev != hitCurr)
                    {
                        iterateAgain = true;
                    }
                    else
                    {
                        bool distanceCondition = !PointsCloseEnough(points[i], nextPoints[i - 1]);
                        if (distanceCondition)
                        {
                            iterateAgain = true;
                        }
                        else
                        {
                            float2 n0 = normals[i - 1];
                            float2 n1 = normals[i];
                            bool angleCondition = math.dot(n1, n0) < cosDouble;

                            if (!AddCorners && angleCondition)
                            {
                                float crossZ = n1.x * n0.y - n1.y * n0.x;
                                bool positiveAngle = crossZ > signEps;

                                iterateAgain = !positiveAngle;
                            }
                            else
                                iterateAgain = angleCondition;
                        }
                    }
                }
                else
                    iterateAgain = FirstIterationConditions[i];

                #endregion


                //TODO: MOVE DISTANCE CALC INSIDE JOB
                if (!iterateAgain)
                    continue;

                if (iterationNumber == NumExtraIterations)
                {
                    AddViewPoint(iteration.Hits[i - 1], iteration.Distances[i - 1], iteration.RayAngles[i - 1], -angleStep, iteration.Normals[i - 1], iteration.Points[i - 1], iteration.Directions[i - 1]);
                    AddViewPoint(iteration.Hits[i], iteration.Distances[i], iteration.RayAngles[i], angleStep, iteration.Normals[i], iteration.Points[i], iteration.Directions[i]);
                }
                else
                {
                    float initalAngle = iteration.RayAngles[i - 1];

                    //Profiler.BeginSample("gather iteration");
                    SightIteration newIter = Iterate(iterationNumber + 1, initalAngle, newAngleStep, ref iteration, i - 1);
                    //Profiler.EndSample();

                    SortData(ref newIter, newAngleStep, IterationRayCount, iterationNumber + 1);
                }
            }
            //Profiler.EndSample();
        }

        Stack<SightIteration> SubIterations = new Stack<SightIteration>();
        Stack<SightIteration> InUseIterations = new Stack<SightIteration>();
        SightIteration GetSubIteration()
        {
            if (SubIterations.Count > 0)
            {
                return SubIterations.Pop();
            }
            SightIteration newInstance = new SightIteration();
            newInstance.InitializeStruct(IterationRayCount);
            return newInstance;
        }

        bool ProfileExtraIterations = false;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        SightIteration Iterate(int iterNumber, float initialAngle, float angleStep, ref SightIteration PreviousIteration, int PrevIterStartIndex)   //TODO: JOBIFY
        {
#if UNITY_EDITOR
            if (ProfileExtraIterations && ProfileFOW)
                Profiler.BeginSample($"Iteration {iterNumber + 1}");
#endif
            SightIteration iter = GetSubIteration();
            InUseIterations.Push(iter);
            //float step = angleStep / (IterationRayCount + 1);
            //step = angleStep;
            
            iter.RayAngles[0] = PreviousIteration.RayAngles[PrevIterStartIndex];
            iter.Hits[0] = PreviousIteration.Hits[PrevIterStartIndex];
            iter.Distances[0] = PreviousIteration.Distances[PrevIterStartIndex];
            iter.Points[0] = PreviousIteration.Points[PrevIterStartIndex];
            iter.Directions[0] = PreviousIteration.Directions[PrevIterStartIndex];
            iter.Normals[0] = PreviousIteration.Normals[PrevIterStartIndex];

            float stepRad = math.radians(angleStep);
            float sStep, cStep; math.sincos(stepRad, out sStep, out cStep);
            iter.NextPoints[0] = FogMath2D.PredictNextPoint(iter.Points[0], iter.Normals[0], iter.Directions[0], iter.Distances[0], sStep, cStep);
            //iter.NextPoints[0] = PreviousIteration.NextPoints[PrevIterStartIndex];

            //for (int i = 1; i <= IterationRayCount; i++)
            for (int i = 1; i < IterationRayCount - 1; i++)
            {
                RayCast(initialAngle + angleStep * i, ref currentRay);
#if UNITY_EDITOR
                if (DebugMode && DrawIteritiveRays)
                {
                    Debug.DrawRay(EyePosition, DirFromAngle(initialAngle + angleStep * i, true) * 10, Color.red);
                    //Debug.DrawRay(EyePosition, DirFromAngle(currentRay.angle, true) * 10, Color.red);
                }

#endif
                iter.RayAngles[i] = currentRay.angle;
                iter.Hits[i] = currentRay.hit;
                iter.Distances[i] = currentRay.distance;
                iter.Points[i] = currentRay.point;
                iter.Directions[i] = currentRay.direction;
                iter.Normals[i] = currentRay.normal;

                iter.NextPoints[i] = FogMath2D.PredictNextPoint(iter.Points[i], iter.Normals[i], iter.Directions[i], iter.Distances[i], sStep, cStep);
            }

            iter.RayAngles[IterationRayCount - 1] = PreviousIteration.RayAngles[PrevIterStartIndex + 1];
            iter.Hits[IterationRayCount - 1] = PreviousIteration.Hits[PrevIterStartIndex + 1];
            iter.Distances[IterationRayCount - 1] = PreviousIteration.Distances[PrevIterStartIndex + 1];
            iter.Points[IterationRayCount - 1] = PreviousIteration.Points[PrevIterStartIndex + 1];
            iter.Directions[IterationRayCount - 1] = PreviousIteration.Directions[PrevIterStartIndex + 1];
            iter.Normals[IterationRayCount - 1] = PreviousIteration.Normals[PrevIterStartIndex + 1];
            iter.NextPoints[IterationRayCount - 1] = PreviousIteration.NextPoints[PrevIterStartIndex + 1];

#if UNITY_EDITOR
            if (DebugMode && DrawIteritiveRays)
            {
                Debug.DrawRay(EyePosition, DirFromAngle(initialAngle + angleStep * 0, true) * 10, Color.red);
                Debug.DrawRay(EyePosition, DirFromAngle(initialAngle + angleStep * (IterationRayCount - 1), true) * 10, Color.red);
                //Debug.DrawRay(EyePosition, DirFromAngle(currentRay.angle, true) * 10, Color.red);
            }

            if (ProfileExtraIterations && ProfileFOW)
                Profiler.EndSample();
#endif
            return iter;
        }

        [BurstCompile]
        public struct CalculateNextPoints : IJobParallelFor
        {
            public float AngleStep;
            public float SStep;
            public float CStep;

            //[ReadOnly] public NativeArray<SightRay> rays;
            [ReadOnly] public NativeArray<float> Distances;
            [ReadOnly] public NativeArray<float2> Points;
            [ReadOnly] public NativeArray<float2> Normals;      //unit
            [ReadOnly] public NativeArray<float2> Directions;   //unit

            [WriteOnly] public NativeArray<float2> ExpectedNextPoints;
            public void Execute(int id)
            {
                //float2 normal = Normals[id];
                //float2 rotatedNormal = FogMath2D.NormalRotate90(normal);
                //float2 md = -Directions[id];

                //float cosPhi = rotatedNormal.x * md.x + rotatedNormal.y * md.y;
                //float sinPhi = rotatedNormal.x * md.y - rotatedNormal.y * md.x;
                //float sinAngleC = sinPhi * CStep + cosPhi * SStep;
                //float s = sinAngleC;
                ////if (math.abs(s) < 1e-6f) s = s >= 0f ? 1e-6f : -1e-6f;

                //float nextDist = Distances[id] * SStep / s;

                //ExpectedNextPoints[id] = Points[id] + (rotatedNormal * nextDist);

                float2 next = FogMath2D.PredictNextPoint(Points[id], Normals[id], Directions[id], Distances[id], SStep, CStep);
                ExpectedNextPoints[id] = next;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        public struct ConditionCalculations : IJobParallelFor
        {
            public float DoubleHitMaxAngleDelta;
            public float CosDoubleHit;
            public float SignEps;
            public float EdgeDstThresholdSq;
            public bool AddCorners;

            [ReadOnly][NativeDisableParallelForRestriction] public NativeArray<float2> Points;
            [ReadOnly][NativeDisableParallelForRestriction] public NativeArray<float2> NextPoints;
            [ReadOnly][NativeDisableParallelForRestriction] public NativeArray<float2> Normals;
            [ReadOnly][NativeDisableParallelForRestriction] public NativeArray<bool> Hits;

            [WriteOnly][NativeDisableParallelForRestriction] public NativeArray<bool> IterateConditions;
            public void Execute(int id)
            {
                if (id == 0)
                    return;
                //float AngleDelta = FogMath2D.SignedAngleDeg(Normals[id], Normals[id - 1]);
                //bool AngleCondition = math.abs(AngleDelta) > DoubleHitMaxAngleDelta;
                //bool PositiveAngle = AngleDelta > 0;

                float2 n0 = Normals[id - 1];
                float2 n1 = Normals[id];
                bool AngleCondition = math.dot(n1, n0) < CosDoubleHit;
                float crossZ = n1.x * n0.y - n1.y * n0.x;
                bool PositiveAngle = crossZ > SignEps;


                bool DistanceCondition = math.distancesq(Points[id], NextPoints[id - 1]) >= EdgeDstThresholdSq;

                bool SampleCondition = (Hits[id - 1] || Hits[id]) &&
                    (DistanceCondition || AngleCondition)
                    || Hits[id - 1] != Hits[id];

                if (!AddCorners && AngleCondition && PositiveAngle && !DistanceCondition)
                    SampleCondition = false;

                IterateConditions[id] = SampleCondition;
            }
        }

        //TODO: merge the CalculateNextPoints and ConditionCalculations jobs into one!
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        public struct CalculateNextPointsAndAngleConditions : IJobParallelFor
        {
            //used in next point calculations
            public float AngleStep;
            public float SStep, CStep;

            //used in condition calculations
            public float CosDoubleHit;
            public float SignEps;
            public float EdgeDstThresholdSq;
            public bool AddCorners;

            //[ReadOnly] public NativeArray<SightRay> rays;
            [ReadOnly] public NativeArray<float> Distances;
            [ReadOnly] public NativeArray<float2> Points;
            [ReadOnly] public NativeArray<float2> Normals;      //unit
            [ReadOnly] public NativeArray<float2> Directions;   //unit
            [ReadOnly][NativeDisableParallelForRestriction] public NativeArray<bool> Hits;

            [WriteOnly] public NativeArray<float2> ExpectedNextPoints;
            [WriteOnly][NativeDisableParallelForRestriction] public NativeArray<bool> IterateConditions;
            public void Execute(int id)
            {
                float2 next = FogMath2D.PredictNextPoint(Points[id], Normals[id], Directions[id], Distances[id], SStep, CStep);
                ExpectedNextPoints[id] = next;

                if (id == 0) { IterateConditions[0] = false; return; }

                // recompute previous NextPoints locally (no read-after-write)
                float2 prevNext = FogMath2D.PredictNextPoint(
                    Points[id - 1], Normals[id - 1], Directions[id - 1], Distances[id - 1], SStep, CStep);

                bool distanceCondition = math.distancesq(Points[id], prevNext) >= EdgeDstThresholdSq;

                float2 n0 = Normals[id - 1];
                float2 n1 = Normals[id];
                bool angleCondition = math.dot(n1, n0) < CosDoubleHit;
                float crossZ = n1.x * n0.y - n1.y * n0.x;
                bool positiveAngle = crossZ > SignEps;

                bool sample = (Hits[id - 1] || Hits[id]) &&
                              (distanceCondition || angleCondition) ||
                              Hits[id - 1] != Hits[id];

                if (!AddCorners && angleCondition && positiveAngle && !distanceCondition)
                    sample = false;

                IterateConditions[id] = sample;
            }
        }

        public struct EdgeResolveData
        {
            public float CurrentAngle;
            public float AngleAdd;
            public float Sign;
            public bool Break;
        }

        private void FindEdges()
        {
            //EDGE FIND. TODO: USE JOBS SYSTEM
            for (int i = 0; i < NumberOfPoints; i++)
            {
                float currentAngle = ViewPoints[i].Angle;
                float angleAdd = EdgeAngles[i] * 0.5f;

                currentAngle += angleAdd;
                for (int r = 0; r < MaxEdgeResolveIterations; r++)
                {
                    if (math.abs(angleAdd) < MaxAcceptableEdgeAngleDifference)
                        break;

                    RayCast(currentAngle, ref currentRay);

                    float delta = currentAngle - ViewPoints[i].Angle;
                    float sDelta, cDelta; math.sincos(math.radians(delta), out sDelta, out cDelta);
                    float2 nextPoint = FogMath2D.PredictNextPoint(ViewPoints[i].Point, EdgeNormals[i], ViewPoints[i].Direction, ViewPoints[i].Radius, sDelta, cDelta);

#if UNITY_EDITOR
                    //if (DebugMode && i == DEBUGEDGESLICE)
                    if (DebugMode && DrawEdgeResolveRays)
                    {
                        if (SegmentTest != -1 && SegmentTest == i)
                        {
                            Debug.DrawLine(Get3Dfrom2D(ViewPoints[i].Point), Get3Dfrom2D(nextPoint) + Vector3.up * .05f, UnityEngine.Random.ColorHSV());
                            Debug.DrawRay(EyePosition, DirFromAngle(currentAngle, true) * currentRay.distance, angleAdd >= 0 ? Color.green : Color.cyan);
                        }
                    }

#endif
                    //bool angleBad = Vector2.Angle(EdgeNormals[i], currentRay.normal) > DoubleHitMaxAngleDelta;
                    bool angleBad = math.dot(EdgeNormals[i], currentRay.normal) < cosDouble;

                    bool mismatch = ViewPoints[i].DidHit != currentRay.hit ||
                        angleBad ||
                        !PointsCloseEnough(nextPoint, currentRay.point);

                    float sign = mismatch ? -1f : 1f;

                    if (!mismatch)
                    {
                        sign = 1;
                        ViewPoints[i].Direction = currentRay.direction;
                        ViewPoints[i].Angle = currentAngle;
                        ViewPoints[i].Radius = currentRay.distance;
                        EdgeNormals[i] = currentRay.normal;
                        ViewPoints[i].Point = currentRay.point;
                    }

                    angleAdd *= 0.5f;
                    currentAngle += angleAdd * sign;
                }
            }
        }

        #region finding edges using the jobs system

        //this is slow so we dont use it. keeping it here in case i ever revisit it
        protected FindEdgeJob EdgeJob;
        protected JobHandle EdgeJobHandle;
        protected abstract void _FindEdgeUsingJobs();
        private void FindEdgesJobs()
        {
            _FindEdgeUsingJobs();
        }

        [BurstCompile]
        protected struct FindEdgeJob : IJobParallelFor
        {
            public float MaxAcceptableEdgeAngleDifference;
            public float DoubleHitMaxAngleDelta;
            public float EdgeDstThresholdSq;
            [ReadOnly] public NativeArray<SightRay> SightRays;
            public NativeArray<SightSegment> SightSegments;
            public NativeArray<float2> EdgeNormals;
            public NativeArray<EdgeResolveData> EdgeData;
            public void Execute(int index)
            {
                EdgeResolveData data = EdgeData[index];

                if (data.Break)
                    return;

                SightSegment segment = SightSegments[index];
                SightRay currentRay = SightRays[index];

                float2 normal = EdgeNormals[index];
                float delta = data.CurrentAngle - segment.Angle;
                float sDelta, cDelta; math.sincos(math.radians(delta), out sDelta, out cDelta);
                float2 nextPoint = FogMath2D.PredictNextPoint(segment.Point, normal, segment.Direction, segment.Radius, sDelta, cDelta);

                float CosDoubleHit = math.cos(math.radians(DoubleHitMaxAngleDelta));
                bool angleBad = math.dot(normal, currentRay.normal) < CosDoubleHit;
                bool mismatch =
                    segment.DidHit != currentRay.hit ||
                    angleBad ||
                    !PointsCloseEnough(nextPoint, currentRay.point);

                float sign = mismatch ? -1f : 1f;

                if (!mismatch)
                {
                    data.Sign = 1;
                    segment.Angle = data.CurrentAngle;
                    segment.Radius = currentRay.distance;
                    segment.Point = currentRay.point;
                    segment.Direction = currentRay.direction;
                    EdgeNormals[index] = currentRay.normal;
                }

                SightSegments[index] = segment;

                data.AngleAdd /= 2;
                if (math.abs(data.AngleAdd) < MaxAcceptableEdgeAngleDifference)
                    data.Break = true;
                data.CurrentAngle += data.AngleAdd * data.Sign;

                EdgeData[index] = data;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool PointsCloseEnough(float2 v1, float2 v2)
            {
                return math.distancesq(v1, v2) < EdgeDstThresholdSq;
            }
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PointsCloseEnough(float2 v1, float2 v2)
        {
            //return math.distance(v1, v2) < EdgeDstThreshold;
            return math.distancesq(v1, v2) < edgeDstThresholdSq;
            //return math.abs(math.distancesq(v1, v2)) < EdgeDstThreshold;
            //return Mathf.Abs((v1 - v2).sqrMagnitude) < EdgeDstThreshold;
            //return Mathf.Approximately(0, (v1 - v2).sqrMagnitude);
        }

        //used only for debug line drawing
        protected abstract Vector3 _Get3Dfrom2D(Vector2 twoD);
        Vector3 Get3Dfrom2D(Vector2 twoD)
        {
            return _Get3Dfrom2D(twoD);
        }
    }

    public class SightIteration
    {
        //public float[] RayAngles;
        public NativeArray<float> RayAngles;
        public NativeArray<bool> Hits;
        public NativeArray<float> Distances;
        public NativeArray<float2> Points;
        public NativeArray<float2> Directions;
        public NativeArray<float2> Normals;

        public NativeArray<float2> NextPoints;

        public SightIteration[] NextIterations;

        public void InitializeStruct(int NumSteps)
        {
            //RayAngles = new float[NumSteps];
            RayAngles = new NativeArray<float>(NumSteps, Allocator.Persistent);
            Hits = new NativeArray<bool>(NumSteps, Allocator.Persistent);
            Distances = new NativeArray<float>(NumSteps, Allocator.Persistent);
            Points = new NativeArray<float2>(NumSteps, Allocator.Persistent);
            Directions = new NativeArray<float2>(NumSteps, Allocator.Persistent);
            Normals = new NativeArray<float2>(NumSteps, Allocator.Persistent);
            NextPoints = new NativeArray<float2>(NumSteps, Allocator.Persistent);
        }
        public void DisposeStruct()
        {
            //RayAngles = null;
            RayAngles.Dispose();
            Distances.Dispose();
            Hits.Dispose();
            Points.Dispose();
            Directions.Dispose();
            Normals.Dispose();
            NextPoints.Dispose();
        }
    }

    internal static class FogMath2D
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 ProjectTo2D(float3 v, FogOfWarWorld.GamePlane plane)
        {
            switch (plane)
            {
                case FogOfWarWorld.GamePlane.XZ: return new float2(v.x, v.z);
                case FogOfWarWorld.GamePlane.XY: return new float2(v.x, v.y);
                case FogOfWarWorld.GamePlane.ZY: return new float2(v.z, v.y);
            }
            return new float2(v.x, v.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 DirFromAngle2D(float angleDeg, FogOfWarWorld.GamePlane plane)
        {
            float s, c;
            math.sincos(math.radians(angleDeg), out s, out c);
            switch (plane)
            {
                case FogOfWarWorld.GamePlane.XZ: return new float2(c, s);
                case FogOfWarWorld.GamePlane.XY: return new float2(c, s);
                case FogOfWarWorld.GamePlane.ZY: return new float2(c, s);
            }
            return new float2(c, s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SignedAngleDeg(float2 a, float2 b)
        {
            a = math.normalize(a);
            b = math.normalize(b);
            float s = a.x * b.y - a.y * b.x;
            float c = math.dot(a, b);
            return math.degrees(math.atan2(s, c));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AngleGreaterThan(float2 a, float2 b, float degThreshold)
        {
            float cosT = math.cos(math.radians(degThreshold));
            float d = math.dot(math.normalize(a), math.normalize(b));
            return d < cosT;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 NormalRotate90(float2 v)
        {
            return new float2(-v.y, v.x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 NormalizeSafe(float2 v, float eps = 1e-8f)
        {
            return math.normalize(v);
            //float ls = v.x * v.x + v.y * v.y;
            //if (ls <= eps) return new float2(0f, 0f);
            //float inv = math.rsqrt(ls);
            //return v * inv;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Approximately(float a, float b)
        {
            return math.abs(b - a) < math.max(0.000001f * math.max(math.abs(a), math.abs(b)), math.EPSILON * 8f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Distance2D(float3 a, float3 b, FogOfWarWorld.GamePlane plane)
        {
            float2 pa = ProjectTo2D(a, plane);
            float2 pb = ProjectTo2D(b, plane);
            return math.length(pa - pb);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosHalfFov(float viewAngleDeg)
        {
            return math.cos(math.radians(viewAngleDeg * 0.5f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InFov(float2 fwd, float2 to, float cosHalfFov)
        {
            return math.dot(NormalizeSafe(fwd), NormalizeSafe(to)) >= cosHalfFov;
        }

        //next point prediction

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SafeNonZero(float x, float eps = 1e-6f)
        {
            return math.select(x, math.select(-eps, eps, x >= 0f), math.abs(x) < eps);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ComputePhi(float2 normalUnit, float2 dirUnit, out float sinPhi, out float cosPhi)
        {
            // rotatedNormal = (-n.y, n.x), md = -dir
            float2 rotated = new float2(-normalUnit.y, normalUnit.x);
            float2 md = -dirUnit;

            cosPhi = rotated.x * md.x + rotated.y * md.y;
            sinPhi = rotated.x * md.y - rotated.y * md.x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float PredictNextDist(float distance, float sinAngleStep, float sinAngleC)
        {
            // nextDist = distance * sin(step) / sin(angleC), clamped for numeric safety
            float s = SafeNonZero(sinAngleC);
            return distance * sinAngleStep / s;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SinAngleC(float2 normalUnit, float2 dirUnit, float sStep, float cStep)
        {
            float sinPhi, cosPhi;
            ComputePhi(normalUnit, dirUnit, out sinPhi, out cosPhi);
            return sinPhi * cStep + cosPhi * sStep;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 PredictNextPoint(float2 point, float2 normalUnit, float2 dirUnit, float distance, float sStep, float cStep)
        {
            float sinAC = SinAngleC(normalUnit, dirUnit, sStep, cStep);
            float nextDist = PredictNextDist(distance, sStep, sinAC);
            float2 rotated = NormalRotate90(normalUnit);
            return point + rotated * nextDist;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 PredictNextPointOldMethod(float2 point, float2 normalUnit, float2 dirUnit, float distance, float AngleStep)
        {
            //rotated normal is parallel to the surface we hit
            float2 RotatedNormal = FogMath2D.NormalRotate90(normalUnit);
            float AngleA = FogMath2D.SignedAngleDeg(RotatedNormal, -dirUnit);
            float angleC = 180 - (AngleA + AngleStep);
            float nextDist = (distance * math.sin(math.radians(AngleStep))) / Mathf.Sin(math.radians(angleC));
            return point + (RotatedNormal * nextDist);
        }
    }
}
