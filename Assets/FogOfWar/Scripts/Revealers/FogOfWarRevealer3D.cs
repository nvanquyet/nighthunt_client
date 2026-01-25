 using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using System.Runtime.CompilerServices;
#if UNITY_EDITOR
using UnityEngine.Profiling;
using UnityEditor;
#endif

namespace FOW
{
	public class FogOfWarRevealer3D : FogOfWarRevealer
	{
		//public int rev3dProp;
		private NativeArray<RaycastCommand> RaycastCommandsNative;
		private NativeArray<RaycastHit> RaycastHits;
		private NativeArray<float3> Vector3Directions;
		private JobHandle IterationOneJobHandle;
		private Phase1SetupJob SetupJob;
		private JobHandle SetupJobJobHandle;
		private GetVector2Data DataJob;
		private JobHandle Vector2NormalJobHandle;
		private PhysicsScene physicsScene;
#if UNITY_2022_2_OR_NEWER
		public QueryParameters RayQueryParameters;
#endif
		protected override void _InitRevealer(int StepCount)
		{
            physicsScene = gameObject.scene.GetPhysicsScene();

            //if (RaycastCommands != null)
            if (RaycastCommandsNative.IsCreated)
				CleanupRevealer();

			//RaycastCommands = new RaycastCommand[StepCount];
			RaycastCommandsNative = new NativeArray<RaycastCommand>(StepCount, Allocator.Persistent);
			RaycastHits = new NativeArray<RaycastHit>(StepCount, Allocator.Persistent);
			Vector3Directions = new NativeArray<float3>(StepCount, Allocator.Persistent);

#if UNITY_2022_2_OR_NEWER
			RayQueryParameters = new QueryParameters(ObstacleMask, false, QueryTriggerInteraction.UseGlobal, false);
#endif
            SetupJob = new Phase1SetupJob()
			{
				GamePlane = (int)FogOfWarWorld.instance.gamePlane,
				RayAngles = FirstIteration.RayAngles,
				Vector3Directions = Vector3Directions,
				RaycastCommandsNative = RaycastCommandsNative,
			};

			DataJob = new GetVector2Data()
			{
				GamePlane = (int)FogOfWarWorld.instance.gamePlane,
				RaycastHits = RaycastHits,
				Hits = FirstIteration.Hits,
				Distances = FirstIteration.Distances,

				InDirections = Vector3Directions,
				OutPoints = FirstIteration.Points,
				OutDirections = FirstIteration.Directions,
				OutNormals = FirstIteration.Normals
			};
			for (int i = 0; i < StepCount; i++)
            {
				//RaycastCommands[i] = new RaycastCommand(Vector3.zero, Vector3.up, layerMask: ObstacleMask);
				//RaycastCommands[i].layerMask = ObstacleMask;
			}
		}

		protected override void CleanupRevealer()
        {
			if (!RaycastCommandsNative.IsCreated)
				return;
			RaycastCommandsNative.Dispose();
			RaycastHits.Dispose();
			Vector3Directions.Dispose();
		}
		
		protected override void IterationOne(int NumSteps, float firstAngle, float angleStep)
        {
#if UNITY_EDITOR
            if (ProfileFOW) Profiler.BeginSample("pt1");	//if this is taking a super long time on some frames only, update unity!
#endif
			SetupJob.FirstAngle = firstAngle;
			SetupJob.AngleStep = angleStep;
			SetupJob.RayDistance = RayDistance;
			SetupJob.EyePosition = EyePosition;
#if UNITY_2022_2_OR_NEWER
			RayQueryParameters.layerMask = ObstacleMask;
			SetupJob.Parameters = RayQueryParameters;
#else
			SetupJob.LayerMask = ObstacleMask;
#endif
			SetupJobJobHandle = SetupJob.Schedule(NumSteps, CommandsPerJob, default(JobHandle));

#if UNITY_EDITOR
			if (DebugMode && DrawInitialRays)
			{
				SetupJobJobHandle.Complete();
				for (int i = 0; i < NumSteps; i++)
				{
					Debug.DrawRay(EyePosition, Vector3Directions[i] * RayDistance, Color.white);
				}
			}
#endif

			//IterationOneJobHandle = RaycastCommand.ScheduleBatch(RaycastCommandsNative, RaycastHits, 64);
			//Debug.Log(commandsPerJob);
			IterationOneJobHandle = RaycastCommand.ScheduleBatch(RaycastCommandsNative, RaycastHits, CommandsPerJob, SetupJobJobHandle);
            //JobHandle.ScheduleBatchedJobs();

            //IterationOneJobHandle.Complete();
#if UNITY_EDITOR
            if (ProfileFOW) Profiler.EndSample();
            if (ProfileFOW) Profiler.BeginSample("pt2");
#endif
			//DataJob.RayDistance = ViewRadius;
			DataJob.RayDistance = RayDistance;
			DataJob.EyePosition = EyePosition;
            //Vector2NormalJobHandle = DataJob.Schedule(NumSteps, 32, IterationOneJobHandle);

            PointsJob.SStep = SinStep;
            PointsJob.CStep = CosStep;
            Vector2NormalJobHandle = DataJob.Schedule(NumSteps, CommandsPerJob, IterationOneJobHandle);
            //Vector2NormalJobHandle.Complete();
#if UNITY_EDITOR
            if (ProfileFOW) Profiler.EndSample();
#endif


			//PointsJobHandle = PointsJob.Schedule(NumSteps, 32);
			PointsJobHandle = PointsJob.Schedule(NumSteps, CommandsPerJob, Vector2NormalJobHandle);
			//PointsJobHandle.Complete();	//now called in phase 2
		}

		[BurstCompile]
		struct Phase1SetupJob : IJobParallelFor
        {
			public int GamePlane;
			public float FirstAngle;
			public float AngleStep;
			public float RayDistance;
			public Vector3 EyePosition;
#if UNITY_2022_2_OR_NEWER
			public QueryParameters Parameters;
#else
			public int LayerMask;
#endif
			[WriteOnly] public NativeArray<float> RayAngles;
			[WriteOnly] public NativeArray<float3> Vector3Directions;
			[WriteOnly] public NativeArray<RaycastCommand> RaycastCommandsNative;
			public PhysicsScene PhysicsScene;
			public void Execute(int id)
            {
				float angle = FirstAngle + (AngleStep * id);
				RayAngles[id] = angle;
                //float3 dir = DirFromAngle(angle);
                float s, c; math.sincos(math.radians(angle), out s, out c);
                float3 dir;
                switch (GamePlane)
                {
                    case 0: dir = new float3(c, 0f, s); break;
                    case 1: dir = new float3(c, s, 0f); break;
                    default: dir = new float3(0f, s, c); break;
                }
                Vector3Directions[id] = dir;

#if UNITY_2022_2_OR_NEWER
				RaycastCommandsNative[id] = new RaycastCommand(EyePosition, dir, Parameters, RayDistance);
#else
				RaycastCommandsNative[id] = new RaycastCommand(EyePosition, dir, RayDistance, layerMask: LayerMask);
#endif
			}
			float3 DirFromAngle(float angleInDegrees)
			{
				float3 direction = new float3();
				switch (GamePlane)
				{
					case 0:
						direction.x = math.cos(angleInDegrees * Mathf.Deg2Rad);
						direction.z = math.sin(angleInDegrees * Mathf.Deg2Rad);
						return direction;
					case 1:
						direction.x = math.cos(angleInDegrees * Mathf.Deg2Rad);
						direction.y = math.sin(angleInDegrees * Mathf.Deg2Rad);
						return direction;
					case 2: break;
				}
				direction.z = math.cos(angleInDegrees * Mathf.Deg2Rad);
				direction.y = math.sin(angleInDegrees * Mathf.Deg2Rad);
				return direction;
			}
		}
		[BurstCompile]
		struct GetVector2Data : IJobParallelFor
		{
			public int GamePlane;
			public float RayDistance;
			public float3 EyePosition;
			[ReadOnly] public NativeArray<RaycastHit> RaycastHits;
			[WriteOnly] public NativeArray<bool> Hits;
			[WriteOnly] public NativeArray<float> Distances;

			[ReadOnly] public NativeArray<float3> InDirections;
			[WriteOnly] public NativeArray<float2> OutPoints;
			[WriteOnly] public NativeArray<float2> OutDirections;
			[WriteOnly] public NativeArray<float2> OutNormals;
			public void Execute(int id)
			{
				//if (RaycastHits[id].distance)
				float3 point3d;
				float3 normal3d;
				//if (!approximately(RaycastHits[id].distance, RayDistance))
				if (!FogMath2D.Approximately(RaycastHits[id].distance, 0))
				{
					Hits[id] = true;
					Distances[id] = RaycastHits[id].distance;
					point3d = RaycastHits[id].point;
					normal3d = RaycastHits[id].normal;
				}
				else
				{
					Hits[id] = false;
					Distances[id] = RayDistance;
					point3d = EyePosition + (InDirections[id] * RayDistance);
					normal3d = -InDirections[id];
				}

                float2 point, direction, norm;
                switch (GamePlane)
                {
                    case 0:
                        point = new float2(point3d.x, point3d.z);
                        direction = new float2(InDirections[id].x, InDirections[id].z);
                        norm = new float2(normal3d.x, normal3d.z);
                        break;
                    case 1:
                        point = new float2(point3d.x, point3d.y);
                        direction = new float2(InDirections[id].x, InDirections[id].y);
                        norm = new float2(normal3d.x, normal3d.y);
                        break;
                    default:
                        point = new float2(point3d.z, point3d.y);
                        direction = new float2(InDirections[id].z, InDirections[id].y);
                        norm = new float2(normal3d.z, normal3d.y);
                        break;
                }
                OutPoints[id] = point;
				OutDirections[id] = direction;
				OutNormals[id] = FogMath2D.NormalizeSafe(norm);
			}
		}

        #region Find Edge (c# jobs)

        //we currently dont use this cause it was actually slower

        private NativeArray<RaycastCommand> edgeRaycastCmds;
        private NativeArray<float3> edgeRayDirs;
        private NativeArray<SightRay> edgeSightRays;
        private NativeArray<SightSegment> edgeSegments;
        private NativeArray<EdgeResolveData> edgeResolve;
        private NativeArray<float2> edgeNormalsNA;
        private int edgeCapacity;
        void EnsureEdgeBuffersCapacity(int capacity)
        {
            if (edgeCapacity >= capacity) return;
            DisposeEdgeBuffers();

            edgeRaycastCmds = new NativeArray<RaycastCommand>(capacity, Allocator.Persistent);
            edgeRayDirs = new NativeArray<float3>(capacity, Allocator.Persistent);
            edgeSightRays = new NativeArray<SightRay>(capacity, Allocator.Persistent);
            edgeSegments = new NativeArray<SightSegment>(capacity, Allocator.Persistent);
            edgeResolve = new NativeArray<EdgeResolveData>(capacity, Allocator.Persistent);
            edgeNormalsNA = new NativeArray<float2>(capacity, Allocator.Persistent);
            edgeCapacity = capacity;
        }

        void DisposeEdgeBuffers()
        {
            if (edgeCapacity == 0) return;
            if (edgeRaycastCmds.IsCreated) edgeRaycastCmds.Dispose();
            if (edgeRayDirs.IsCreated) edgeRayDirs.Dispose();
            if (edgeSightRays.IsCreated) edgeSightRays.Dispose();
            if (edgeSegments.IsCreated) edgeSegments.Dispose();
            if (edgeResolve.IsCreated) edgeResolve.Dispose();
            if (edgeNormalsNA.IsCreated) edgeNormalsNA.Dispose();
            edgeCapacity = 0;
        }
        protected override void _FindEdgeUsingJobs()
        {
            EnsureEdgeBuffersCapacity(NumberOfPoints);

            for (int i = 0; i < NumberOfPoints; i++)
            {
                edgeSegments[i] = ViewPoints[i];
                edgeResolve[i] = new EdgeResolveData
                {
                    CurrentAngle = ViewPoints[i].Angle + EdgeAngles[i] * 0.5f,
                    AngleAdd = EdgeAngles[i] * 0.5f,
                    Sign = 1f,
                    Break = false
                };
                edgeNormalsNA[i] = EdgeNormals[i];
            }

            EdgeJob.SightRays = edgeSightRays;
            EdgeJob.SightSegments = edgeSegments;
            EdgeJob.EdgeData = edgeResolve;
            EdgeJob.EdgeNormals = edgeNormalsNA;
            EdgeJob.MaxAcceptableEdgeAngleDifference = MaxAcceptableEdgeAngleDifference;
            EdgeJob.DoubleHitMaxAngleDelta = DoubleHitMaxAngleDelta;
            EdgeJob.EdgeDstThresholdSq = edgeDstThresholdSq;

			for (int r = 0; r < MaxEdgeResolveIterations; r++)
			{
                for (int i = 0; i < NumberOfPoints; i++)
                {
                    float ang = edgeResolve[i].CurrentAngle;
                    Vector3 dir = DirFromAngle(ang, true);
                    edgeRayDirs[i] = new float3(dir.x, dir.y, dir.z);
#if UNITY_2022_2_OR_NEWER
					edgeRaycastCmds[i] = new RaycastCommand(EyePosition, edgeRayDirs[i], RayQueryParameters, RayDistance);
#else
                    edgeRaycastCmds[i] = new RaycastCommand(EyePosition, edgeRayDirs[i], RayDistance, layerMask: ObstacleMask);
#endif
				}
                JobHandle rayCastJobHandle = RaycastCommand.ScheduleBatch(edgeRaycastCmds, RaycastHits, CommandsPerJob);

				var SightRayJob = new SightRayFromRaycastHit()
				{
                    GamePlane = (int)FogOfWarWorld.instance.gamePlane,
                    RayDistance = RayDistance,
                    EyePosition = EyePosition,
                    RaycastHits = RaycastHits,
                    InDirections = edgeRayDirs,
                    SightRays = edgeSightRays
                }.Schedule(NumberOfPoints, CommandsPerJob, rayCastJobHandle);
				EdgeJobHandle = EdgeJob.Schedule(NumberOfPoints, CommandsPerJob, SightRayJob);
                EdgeJobHandle.Complete();

                bool allDone = true;
                for (int i = 0; i < NumberOfPoints; i++) { if (!edgeResolve[i].Break) { allDone = false; break; } }
                if (allDone) break;
            }

            for (int i = 0; i < NumberOfPoints; i++)
                ViewPoints[i] = edgeSegments[i];
        }

        [BurstCompile]
        struct SightRayFromRaycastHit : IJobParallelFor
        {
            public int GamePlane;
            public float RayDistance;
            public float3 EyePosition;
            [ReadOnly] public NativeArray<RaycastHit> RaycastHits;
            [ReadOnly] public NativeArray<float3> InDirections;
            [WriteOnly] public NativeArray<SightRay> SightRays;
            public void Execute(int id)
            {
                SightRay ray = new SightRay();
                float3 point3d, normal3d;
                //if (!approximately(RaycastHits[id].distance, 0))
                if (RaycastHits[id].distance > 0f)
                {
                    ray.hit = true;
                    ray.distance = RaycastHits[id].distance;
                    point3d = RaycastHits[id].point;
                    normal3d = RaycastHits[id].normal;
                }
                else
                {
                    ray.hit = false;
                    ray.distance = RayDistance;
                    point3d = EyePosition + (InDirections[id] * RayDistance);
                    normal3d = -InDirections[id];
                }

                switch (GamePlane)
                {
                    case 0:
                        ray.point = new float2(point3d.x, point3d.z);
                        ray.direction = new float2(InDirections[id].x, InDirections[id].z);
                        ray.normal = FogMath2D.NormalizeSafe(new float2(normal3d.x, normal3d.z));
                        break;
                    case 1:
                        ray.point = new float2(point3d.x, point3d.y);
                        ray.direction = new float2(InDirections[id].x, InDirections[id].y);
                        ray.normal = FogMath2D.NormalizeSafe(new float2(normal3d.x, normal3d.y));
                        break;
                    default:
                        ray.point = new float2(point3d.z, point3d.y);
                        ray.direction = new float2(InDirections[id].z, InDirections[id].y);
                        ray.normal = FogMath2D.NormalizeSafe(new float2(normal3d.z, normal3d.y));
                        break;
                }
                SightRays[id] = ray;
            }
        }
        #endregion

        RaycastHit RayHit;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected override void RayCast(float angle, ref SightRay ray)
        {
			Vector3 direction = DirFromAngle(angle, true);
			ray.angle = angle;
			ray.direction = GetVector2D(direction);
			if (physicsScene.Raycast(EyePosition, direction, out RayHit, RayDistance, ObstacleMask))
            {
				ray.hit = true;
				ray.normal = FogMath2D.NormalizeSafe(GetVector2D(RayHit.normal));
				ray.distance = RayHit.distance;
				ray.point = GetVector2D(RayHit.point);
			}
			else
            {
				ray.hit = false;
				ray.normal = -ray.direction;
				ray.distance = RayDistance;
				//ray.point = GetVector2D(CachedTransform.position) + (ray.direction * RayDistance);
                ray.point = GetVector2D(EyePosition) + ray.direction * RayDistance;
            }
		}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 GetVector2D(Vector3 v)
        {
            switch (FogOfWarWorld.instance.gamePlane)
            {
                case FogOfWarWorld.GamePlane.XZ:
                    return new float2(v.x, v.z);
                case FogOfWarWorld.GamePlane.XY:
                    return new float2(v.x, v.y);
                default: // ZY
                    return new float2(v.z, v.y);
            }
        }

        protected override float GetEuler()
        {
            switch (FogOfWarWorld.instance.gamePlane)
            {
                case FogOfWarWorld.GamePlane.XZ: return CachedTransform.eulerAngles.y;
                case FogOfWarWorld.GamePlane.XY:
					Vector3 up = CachedTransform.up;
					up.z = 0;
					up.Normalize();
					float ang = Vector3.SignedAngle(up, Vector3.up, FogOfWarWorld.UpVector);
					return -ang;
					//return -CachedTransform.rotation.eulerAngles.z;
                case FogOfWarWorld.GamePlane.ZY:
					Vector3 upz = CachedTransform.up;
					upz.x = 0;
					upz.Normalize();
					float angz = Vector3.SignedAngle(upz, Vector3.up, FogOfWarWorld.UpVector);
					return -angz;
					//return CachedTransform.eulerAngles.x;
			}
			return CachedTransform.eulerAngles.y;
        }

		public override Vector3 GetEyePosition()
        {
			Vector3 eyePos = CachedTransform.position + FogOfWarWorld.UpVector * EyeOffset;
			if (FogOfWarWorld.instance.PixelateFog && FogOfWarWorld.instance.RoundRevealerPosition)
            {
				eyePos *= FogOfWarWorld.instance.PixelDensity;
				Vector3 PixelGridOffset = new Vector3(FogOfWarWorld.instance.PixelGridOffset.x, 0, FogOfWarWorld.instance.PixelGridOffset.y);
				eyePos -= PixelGridOffset;
				eyePos = (Vector3)(Vector3Int.RoundToInt(eyePos));
				eyePos += PixelGridOffset;
				eyePos /= FogOfWarWorld.instance.PixelDensity;
			}
			return eyePos;
		}

		Vector3 hiderPosition;
		Vector3 revealerOrigin;
		private float unobscuredSightDist;
		protected override void _RevealHiders()
		{
#if UNITY_EDITOR
			Profiler.BeginSample("Revealing Hiders");
#endif
			FogOfWarHider hiderInQuestion;
			//float distToHider;
			//float heightDist = 0;
			EyePosition = GetEyePosition();
			ForwardVectorCached = GetForward();
			float sightDist = ViewRadius;
			if (FogOfWarWorld.instance.UsingSoftening)
				sightDist += RevealHiderInFadeOutZonePercentage * SoftenDistance;

			unobscuredSightDist = UnobscuredRadius;
			if (FogOfWarWorld.instance.UsingSoftening)
				unobscuredSightDist += RevealHiderInFadeOutZonePercentage * FogOfWarWorld.instance.UnobscuredSoftenDistance;

			sightDist = Mathf.Max(sightDist, UnobscuredRadius);
			//foreach (FogOfWarHider hiderInQuestion in FogOfWarWorld.HidersList)
			for (int i = 0; i < Mathf.Min(MaxHidersSampledPerFrame, FogOfWarWorld.NumHiders); i++)
			{
				_lastHiderIndex = (_lastHiderIndex + 1) % FogOfWarWorld.NumHiders;
				hiderInQuestion = FogOfWarWorld.HidersList[_lastHiderIndex];
				float minDistToHider = DistBetweenVectors(hiderInQuestion.CachedTransform.position, EyePosition) - hiderInQuestion.MaxDistBetweenSamplePoints;
				bool seen = CanSeeHider(hiderInQuestion, sightDist, minDistToHider);

                if (UnobscuredRadius < 0 && (minDistToHider + 1.5f * hiderInQuestion.MaxDistBetweenSamplePoints) < -UnobscuredRadius)
					seen = false;

				if (seen)
                {
					if (!HidersSeen.Contains(hiderInQuestion))
					{
						HidersSeen.Add(hiderInQuestion);
						hiderInQuestion.AddObserver(this);
					}
				}
				else
                {
					if (HidersSeen.Contains(hiderInQuestion))
					{
						HidersSeen.Remove(hiderInQuestion);
						hiderInQuestion.RemoveObserver(this);
					}
				}
			}
#if UNITY_EDITOR
			Profiler.EndSample();
#endif
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool CanSeeHider(FogOfWarHider hiderInQuestion, float sightDist, float minDistToHider)
        {
			//float minDistToHider = DistBetweenVectors(hiderInQuestion.CachedTransform.position, EyePosition) - hiderInQuestion.MaxDistBetweenSamplePoints;

			if (minDistToHider > sightDist)
				return false;

			for (int j = 0; j < hiderInQuestion.SamplePoints.Length; j++)
            {
				if (CanSeeHiderSamplePoint(hiderInQuestion.SamplePoints[j], sightDist))
					return true;
            }

			return false;
		}
		
		bool CanSeeHiderSamplePoint(Transform samplePoint, float sightDist)
        {
			float distToHider = DistBetweenVectors(samplePoint.position, EyePosition);

			if (distToHider < unobscuredSightDist)
				return true;

            float heightDist = 0;
            switch (FogOfWarWorld.instance.gamePlane)
			{
				case FogOfWarWorld.GamePlane.XZ: heightDist = Mathf.Abs(EyePosition.y - samplePoint.position.y); break;
				case FogOfWarWorld.GamePlane.XY: heightDist = Mathf.Abs(EyePosition.z - samplePoint.position.z); break;
				case FogOfWarWorld.GamePlane.ZY: heightDist = Mathf.Abs(EyePosition.x - samplePoint.position.x); break;
			}

			if (heightDist > VisionHeight)
				return false;

			//if ((distToHider < sightDist && Mathf.Abs(AngleBetweenVector2(samplePoint.position - EyePosition, ForwardVectorCached)) <= ViewAngle / 2))
			if (Mathf.Abs(AngleBetweenVector2(samplePoint.position - EyePosition, ForwardVectorCached)) <= ViewAngle / 2)
			{
				revealerOrigin = EyePosition;
				if (CalculateHidersAtHiderHeight)
					SetRevealerOrigin(EyePosition, samplePoint.position);

                hiderPosition = samplePoint.position;
                if (SampleHidersAtRevealerHeight)
					SetHiderPosition(samplePoint.position, EyePosition);
				//else
				//	hiderPosition = samplePoint.position;


				if (!physicsScene.Raycast(revealerOrigin, hiderPosition - revealerOrigin, out RayHit, distToHider, ObstacleMask))
				{
#if UNITY_EDITOR
                    if (DrawHiderSamples)
                        Debug.DrawLine(revealerOrigin, hiderPosition, Color.green);
#endif
                    return true;
				}
#if UNITY_EDITOR
                else
				{
					if (DebugLogHiderBlockerName)
						Debug.Log(RayHit.collider.gameObject.name);
                    if (DrawHiderSamples)
					{
                        Debug.DrawLine(revealerOrigin, RayHit.point, Color.green);
                        Debug.DrawLine(RayHit.point, hiderPosition, Color.red);
                    }
                }
#endif
            }

            return false;
		}

		void SetHiderPosition(Vector3 point, Vector3 eyePosition)
        {
			switch (FogOfWarWorld.instance.gamePlane)
			{
				case FogOfWarWorld.GamePlane.XZ:
					//hiderPosition.x = point.x;
					hiderPosition.y = eyePosition.y;
					//hiderPosition.z = point.z;
					break;
				case FogOfWarWorld.GamePlane.XY:
					//hiderPosition.x = point.x;
					//hiderPosition.y = point.y;
					hiderPosition.z = eyePosition.z;
					break;
				case FogOfWarWorld.GamePlane.ZY:
					hiderPosition.x = eyePosition.x;
					//hiderPosition.y = point.y;
					//hiderPosition.z = point.z;
					break;
			}
		}

        void SetRevealerOrigin(Vector3 point, Vector3 _hiderPosition)
        {
            switch (FogOfWarWorld.instance.gamePlane)
            {
                case FogOfWarWorld.GamePlane.XZ:
                    revealerOrigin.y = _hiderPosition.y;
                    break;
                case FogOfWarWorld.GamePlane.XY:
                    revealerOrigin.z = _hiderPosition.z;
                    break;
                case FogOfWarWorld.GamePlane.ZY:
                    revealerOrigin.x = _hiderPosition.x;
                    break;
            }
        }

        protected override bool _TestPoint(Vector3 point)
        {
			float sightDist = ViewRadius;
			if (FogOfWarWorld.instance.UsingSoftening)
				sightDist += RevealHiderInFadeOutZonePercentage * SoftenDistance;

			EyePosition = GetEyePosition();
			ForwardVectorCached = GetForward();
			float distToPoint = DistBetweenVectors(point, EyePosition);
            bool inFov = Mathf.Abs(AngleBetweenVector2(point - EyePosition, ForwardVectorCached)) < (ViewAngle * 0.5f);
            if (distToPoint < UnobscuredRadius || (distToPoint < sightDist && inFov))
			{
				SetHiderPosition(point, EyePosition);
				if (!physicsScene.Raycast(EyePosition, hiderPosition - EyePosition, distToPoint, ObstacleMask))
					return true;
			}
			return false;
		}

		protected override void SetCenterAndHeight()
        {
			switch (FogOfWarWorld.instance.gamePlane)
			{
				case FogOfWarWorld.GamePlane.XZ:
					center.x = EyePosition.x;
					center.y = EyePosition.z;
					heightPos = EyePosition.y;
					break;
				case FogOfWarWorld.GamePlane.XY:
					center.x = EyePosition.x;
					center.y = EyePosition.y;
					heightPos = EyePosition.z;
					break;
				case FogOfWarWorld.GamePlane.ZY:
					center.x = EyePosition.z;
					center.y = EyePosition.y;
					heightPos = EyePosition.x;
					break;
			}
		}

		protected override float AngleBetweenVector2(Vector3 _vec1, Vector3 _vec2)
		{
            var plane = FogOfWarWorld.instance.gamePlane;
            float2 a = FogMath2D.ProjectTo2D(_vec1, plane);
            float2 b = FogMath2D.ProjectTo2D(_vec2, plane);
            return FogMath2D.SignedAngleDeg(a, b);
		}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float DistBetweenVectors(Vector3 a, Vector3 b)
        {
            var plane = FogOfWarWorld.instance.gamePlane;

            float dx, dy;
            switch (plane)
            {
                case FogOfWarWorld.GamePlane.XZ:
                    dx = a.x - b.x; dy = a.z - b.z;
                    break;
                case FogOfWarWorld.GamePlane.XY:
                    dx = a.x - b.x; dy = a.y - b.y;
                    break;
                default: // ZY
                    dx = a.z - b.z; dy = a.y - b.y;
                    break;
            }
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        Vector3 ForwardVectorCached;
        Vector3 GetForward()
        {
            switch (FogOfWarWorld.instance.gamePlane)
            {
                case FogOfWarWorld.GamePlane.XZ: return CachedTransform.forward;
				case FogOfWarWorld.GamePlane.XY: return CachedTransform.up;
				//case FogOfWarWorld.GamePlane.XY: return new Vector3(-CachedTransform.up.x, CachedTransform.up.y, 0).normalized;
				//case FogOfWarWorld.GamePlane.XY: return -CachedTransform.right;
				case FogOfWarWorld.GamePlane.ZY: return CachedTransform.up;
            }
            return CachedTransform.forward;
        }

		Vector3 direction = Vector3.zero;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
		{
            switch (FogOfWarWorld.instance.gamePlane)
            {
                case FogOfWarWorld.GamePlane.XZ:
                    if (!angleIsGlobal)
                    {
                        angleInDegrees += CachedTransform.eulerAngles.y;
                    }
                    direction.x = Mathf.Cos(angleInDegrees * Mathf.Deg2Rad);
                    direction.z = Mathf.Sin(angleInDegrees * Mathf.Deg2Rad);
                    return direction;
                case FogOfWarWorld.GamePlane.XY:
                    if (!angleIsGlobal)
                    {
                        angleInDegrees += CachedTransform.eulerAngles.z;
                    }
                    direction.x = Mathf.Cos(angleInDegrees * Mathf.Deg2Rad);
                    direction.y = Mathf.Sin(angleInDegrees * Mathf.Deg2Rad);
                    return direction;
                case FogOfWarWorld.GamePlane.ZY: break;
            }
            if (!angleIsGlobal)
            {
                angleInDegrees += CachedTransform.eulerAngles.x;
            }
            direction.z = Mathf.Cos(angleInDegrees * Mathf.Deg2Rad);
            direction.y = Mathf.Sin(angleInDegrees * Mathf.Deg2Rad);
            return direction;
        }

        protected override Vector3 _Get3Dfrom2D(Vector2 pos)
        {
			switch (FogOfWarWorld.instance.gamePlane)
            {
				case FogOfWarWorld.GamePlane.XZ: return new Vector3(pos.x, CachedTransform.position.y, pos.y);
				case FogOfWarWorld.GamePlane.XY: return new Vector3(pos.x, pos.y, CachedTransform.position.z);
				case FogOfWarWorld.GamePlane.ZY: return new Vector3(CachedTransform.position.x, pos.y, pos.x);
			}
			return new Vector3(pos.x, CachedTransform.position.y, pos.y);
		}
    }
}