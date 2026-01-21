using System.Collections.Generic;
using UnityEngine;
using FishNet.Managing;

namespace NightHunt.Networking.Prediction.Core
{
    /// <summary>
    /// Singleton manager quản lý tất cả predicted objects trong game.
    /// Cung cấp tick rate management, batch processing, và LOD system.
    /// </summary>
    public class PredictionManager : MonoBehaviour
    {
        private static PredictionManager _instance;
        public static PredictionManager Instance => _instance;

        [Header("Prediction Settings")]
        [SerializeField] private int tickRate = 60;
        [SerializeField] private bool enablePrediction = true;
        [SerializeField] private bool enableLOD = true;

        [Header("LOD Settings")]
        [SerializeField] private float lodDistance1 = 50f;  // Full prediction
        [SerializeField] private float lodDistance2 = 100f; // Half rate
        [SerializeField] private float lodDistance3 = 200f; // Quarter rate

        [Header("Performance")]
        [SerializeField] private bool enableBatchProcessing = true;
        [SerializeField] private int batchSize = 10;

        private readonly List<IPredictedObject> _predictedObjects = new List<IPredictedObject>();
        private float _tickInterval;
        private float _lastTickTime;
        private Camera _mainCamera;

        /// <summary>
        /// Tick rate hiện tại (ticks per second).
        /// </summary>
        public int TickRate => tickRate;

        /// <summary>
        /// Tick interval (seconds per tick).
        /// </summary>
        public float TickInterval => _tickInterval;

        /// <summary>
        /// Tick hiện tại.
        /// </summary>
        public uint CurrentTick { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _tickInterval = 1f / tickRate;
            _lastTickTime = Time.time;
            CurrentTick = 0;
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                _mainCamera = FindFirstObjectByType<Camera>();
            }
        }

        private void Update()
        {
            if (!enablePrediction)
                return;

            // Tick-based update
            float currentTime = Time.time;
            if (currentTime - _lastTickTime >= _tickInterval)
            {
                ProcessTick();
                _lastTickTime = currentTime;
            }
        }

        /// <summary>
        /// Xử lý một tick - update tất cả predicted objects.
        /// </summary>
        private void ProcessTick()
        {
            CurrentTick++;

            if (enableBatchProcessing)
            {
                ProcessBatch();
            }
            else
            {
                ProcessAll();
            }
        }

        /// <summary>
        /// Xử lý tất cả predicted objects.
        /// </summary>
        private void ProcessAll()
        {
            foreach (var obj in _predictedObjects)
            {
                if (obj != null && obj.IsPredictionEnabled)
                {
                    obj.OnPredictionTick(CurrentTick);
                }
            }
        }

        /// <summary>
        /// Xử lý predicted objects theo batch để tránh spike.
        /// </summary>
        private void ProcessBatch()
        {
            int processed = 0;
            foreach (var obj in _predictedObjects)
            {
                if (obj != null && obj.IsPredictionEnabled)
                {
                    // LOD: Skip một số objects dựa trên distance
                    if (enableLOD && ShouldSkipObject(obj))
                    {
                        continue;
                    }

                    obj.OnPredictionTick(CurrentTick);
                    processed++;

                    if (processed >= batchSize)
                    {
                        break; // Process batch trong frame tiếp theo
                    }
                }
            }
        }

        /// <summary>
        /// Kiểm tra xem có nên skip object này không dựa trên LOD.
        /// </summary>
        private bool ShouldSkipObject(IPredictedObject obj)
        {
            if (_mainCamera == null)
                return false;

            float distance = Vector3.Distance(_mainCamera.transform.position, obj.Position);
            uint tickModulo = CurrentTick % 4; // 4 levels: full, half, quarter, skip

            if (distance > lodDistance3)
            {
                // Xa nhất: Skip tất cả ticks
                return true;
            }
            else if (distance > lodDistance2)
            {
                // Quarter rate: Chỉ process mỗi tick thứ 4
                return tickModulo != 0;
            }
            else if (distance > lodDistance1)
            {
                // Half rate: Chỉ process mỗi tick thứ 2
                return tickModulo % 2 != 0;
            }

            // Gần: Full rate
            return false;
        }

        /// <summary>
        /// Đăng ký predicted object vào manager.
        /// </summary>
        /// <param name="obj">Predicted object cần đăng ký</param>
        public void RegisterPredictedObject(IPredictedObject obj)
        {
            if (obj != null && !_predictedObjects.Contains(obj))
            {
                _predictedObjects.Add(obj);
            }
        }

        /// <summary>
        /// Hủy đăng ký predicted object khỏi manager.
        /// </summary>
        /// <param name="obj">Predicted object cần hủy đăng ký</param>
        public void UnregisterPredictedObject(IPredictedObject obj)
        {
            _predictedObjects.Remove(obj);
        }

        /// <summary>
        /// Set tick rate mới.
        /// </summary>
        /// <param name="newTickRate">Tick rate mới</param>
        public void SetTickRate(int newTickRate)
        {
            tickRate = Mathf.Max(1, newTickRate);
            _tickInterval = 1f / tickRate;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }

    /// <summary>
    /// Interface cho predicted objects để manager có thể quản lý.
    /// </summary>
    public interface IPredictedObject
    {
        /// <summary>
        /// Kiểm tra xem prediction có được enable không.
        /// </summary>
        bool IsPredictionEnabled { get; }

        /// <summary>
        /// Vị trí của object (dùng cho LOD).
        /// </summary>
        Vector3 Position { get; }

        /// <summary>
        /// Được gọi mỗi tick để update prediction.
        /// </summary>
        /// <param name="tick">Tick hiện tại</param>
        void OnPredictionTick(uint tick);
    }
}

