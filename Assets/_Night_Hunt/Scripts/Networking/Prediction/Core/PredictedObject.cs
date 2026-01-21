using UnityEngine;
using System.Collections;
using FishNet.Object;
using FishNet.Object.Prediction;
using NightHunt.Networking.Prediction.Core;
using NightHunt.Networking.Prediction.Input;
using NightHunt.Networking.Prediction.Reconciliation;

namespace NightHunt.Networking.Prediction.Core
{
    /// <summary>
    /// Base class cho tất cả predicted objects trong game.
    /// Cung cấp client-side prediction với server reconciliation.
    /// 
    /// Usage:
    /// public class PlayerMovement : PredictedObject&lt;MovementState, MovementInput&gt;
    /// {
    ///     protected override MovementState PredictState(MovementInput input, MovementState currentState)
    ///     {
    ///         // Implement prediction logic
    ///         return newState;
    ///     }
    ///     
    ///     protected override void ApplyState(MovementState state)
    ///     {
    ///         // Apply state to transform/component
    ///         transform.position = state.position;
    ///     }
    /// }
    /// </summary>
    /// <typeparam name="TState">Type của state (phải là struct để zero-allocation)</typeparam>
    /// <typeparam name="TInput">Type của input (phải implement IInputData)</typeparam>
    [RequireComponent(typeof(NetworkObject))]
    public abstract class PredictedObject<TState, TInput> : NetworkBehaviour, IPredictedObject
        where TState : struct
        where TInput : struct, IInputData
    {
        [Header("Prediction Settings")]
        [SerializeField] private int stateHistorySize = 32;
        [SerializeField] private bool enablePrediction = true;
        [SerializeField] private bool enableReconciliation = true;
        [SerializeField] private float reconciliationThreshold = 0.1f;
        [SerializeField] private ReconciliationStrategy reconciliationStrategy = ReconciliationStrategy.Hybrid;

        protected StateHistory<TState> _stateHistory;
        protected InputBuffer<TInput> _inputBuffer;
        private IReconciliationStrategy<TState> _reconciliationStrategy;
        protected TState _currentState;
        private uint _lastProcessedTick;
        private bool _isInitialized;

        /// <summary>
        /// State hiện tại của object.
        /// </summary>
        public TState CurrentState => _currentState;

        /// <summary>
        /// Tick hiện tại của object.
        /// </summary>
        public uint CurrentTick => _stateHistory?.CurrentTick ?? 0;

        /// <summary>
        /// Kiểm tra xem prediction có được enable không.
        /// </summary>
        public bool IsPredictionEnabled => enablePrediction && _isInitialized;

        protected virtual void Awake()
        {
            _stateHistory = new StateHistory<TState>(stateHistorySize);
            _inputBuffer = new InputBuffer<TInput>();
            _lastProcessedTick = 0;
            _isInitialized = false;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Initialize reconciliation strategy
            _reconciliationStrategy = CreateReconciliationStrategy(reconciliationStrategy);

            // Initialize current state
            _currentState = GetInitialState();

            // Add initial state to history
            if (base.Owner != null && base.Owner.IsLocalClient)
            {
                _stateHistory.AddState(_currentState, 0);
            }

            // Register với PredictionManager (delay để đảm bảo Instance đã được khởi tạo)
            StartCoroutine(RegisterWithPredictionManagerCoroutine());

            _isInitialized = true;
        }

        protected virtual void Update()
        {
            if (!IsSpawned || !_isInitialized)
                return;

            if (IsOwner && enablePrediction)
            {
                // Client owner: Process input và predict state
                ProcessClientPrediction();
            }
            else if (!IsOwner)
            {
                // Non-owner: Interpolate state từ server
                ProcessInterpolation();
            }
        }

        /// <summary>
        /// Xử lý client-side prediction cho owner.
        /// </summary>
        private void ProcessClientPrediction()
        {
            // Lấy input từ Unity Input System hoặc input handler
            if (TryGetInput(out TInput input))
            {
                // Thêm input vào buffer với tick hiện tại
                uint currentTick = _stateHistory.CurrentTick;
                _inputBuffer.AddInput(input, currentTick);

                // Predict state từ input
                _currentState = PredictState(input, _currentState);

                // Lưu state vào history
                _stateHistory.AddState(_currentState, currentTick);
                _stateHistory.IncrementTick();

                // Apply state ngay lập tức (client-side prediction)
                ApplyState(_currentState);

                // Gửi input lên server qua RPC
                SendInputToServer(input, currentTick);
            }
        }

        /// <summary>
        /// Xử lý interpolation cho non-owner clients.
        /// </summary>
        private void ProcessInterpolation()
        {
            // Non-owner clients sẽ nhận state từ server qua NetworkTransform hoặc SyncVar
            // Interpolation được xử lý bởi NetworkTransform hoặc custom interpolation logic
            // Override method này nếu cần custom interpolation
        }

        /// <summary>
        /// Lấy input từ Unity Input System hoặc input handler.
        /// Override method này để cung cấp input cho prediction.
        /// </summary>
        /// <param name="input">Output input nếu có</param>
        /// <returns>True nếu có input mới</returns>
        protected abstract bool TryGetInput(out TInput input);

        /// <summary>
        /// Predict state từ input và current state.
        /// Đây là method chính cần implement trong derived class.
        /// </summary>
        /// <param name="input">Input từ player</param>
        /// <param name="currentState">State hiện tại</param>
        /// <returns>State mới sau khi predict</returns>
        protected abstract TState PredictState(TInput input, TState currentState);

        /// <summary>
        /// Apply state vào transform/component.
        /// Đây là method chính cần implement trong derived class.
        /// </summary>
        /// <param name="state">State cần apply</param>
        protected abstract void ApplyState(TState state);

        /// <summary>
        /// Lấy initial state khi object được spawn.
        /// Override method này nếu cần custom initial state.
        /// </summary>
        /// <returns>Initial state</returns>
        protected virtual TState GetInitialState()
        {
            return default;
        }

        /// <summary>
        /// Gửi input lên server qua RPC.
        /// Override method này để customize cách gửi input.
        /// </summary>
        /// <param name="input">Input cần gửi</param>
        /// <param name="tick">Tick của input</param>
        protected virtual void SendInputToServer(TInput input, uint tick)
        {
            // Default: Gửi qua ServerRpc
            // Derived classes có thể override để dùng [Replicate] hoặc custom RPC
        }

        /// <summary>
        /// Reconcile state từ server.
        /// Được gọi khi nhận reconciliation data từ server.
        /// </summary>
        /// <param name="reconciliationData">Data từ server</param>
        public void Reconcile(ReconciliationData<TState> reconciliationData)
        {
            if (!enableReconciliation || !IsOwner)
                return;

            // Kiểm tra xem có cần reconcile không
            if (_stateHistory.TryGetState(reconciliationData.ServerTick, out TState clientState))
            {
                // So sánh client state với server state
                if (ShouldReconcile(clientState, reconciliationData.ServerState))
                {
                    // Rollback về server tick
                    RollbackToTick(reconciliationData.ServerTick);

                    // Apply server state
                    _currentState = reconciliationData.ServerState;
                    ApplyState(_currentState);

                    // Replay inputs từ server tick đến hiện tại
                    ReplayInputsFromTick(reconciliationData.ServerTick);
                }
            }
            else
            {
                // Không có state tại tick đó, apply server state trực tiếp
                _currentState = reconciliationData.ServerState;
                ApplyState(_currentState);
            }
        }

        /// <summary>
        /// Kiểm tra xem có cần reconcile không dựa trên threshold.
        /// </summary>
        /// <param name="clientState">State trên client</param>
        /// <param name="serverState">State trên server</param>
        /// <returns>True nếu cần reconcile</returns>
        protected virtual bool ShouldReconcile(TState clientState, TState serverState)
        {
            // Default: Dùng reconciliation strategy để check
            return _reconciliationStrategy.ShouldReconcile(clientState, serverState, reconciliationThreshold);
        }

        /// <summary>
        /// Rollback về tick cụ thể.
        /// </summary>
        /// <param name="tick">Tick cần rollback về</param>
        private void RollbackToTick(uint tick)
        {
            if (_stateHistory.TryGetState(tick, out TState state))
            {
                _currentState = state;
                ApplyState(_currentState);

                // Xóa các state sau tick này
                _stateHistory.RemoveStatesBefore(tick + 1);
            }
        }

        /// <summary>
        /// Replay inputs từ tick cụ thể đến hiện tại.
        /// </summary>
        /// <param name="fromTick">Tick bắt đầu replay</param>
        private void ReplayInputsFromTick(uint fromTick)
        {
            // Lấy tất cả inputs từ tick đó đến hiện tại
            var inputs = _inputBuffer.GetInputsFromTick(fromTick);

            // Replay từng input
            foreach (var (tick, input, timestamp) in inputs)
            {
                _currentState = PredictState(input, _currentState);
                _stateHistory.AddState(_currentState, tick);
            }

            // Apply state cuối cùng
            if (_stateHistory.TryGetLatestState(out TState latestState))
            {
                ApplyState(latestState);
            }
        }

        /// <summary>
        /// Tạo reconciliation strategy dựa trên config.
        /// </summary>
        /// <param name="strategy">Strategy type</param>
        /// <returns>Reconciliation strategy instance</returns>
        private IReconciliationStrategy<TState> CreateReconciliationStrategy(ReconciliationStrategy strategy)
        {
            switch (strategy)
            {
                case ReconciliationStrategy.Snap:
                    return new SnapReconciliation<TState>();
                case ReconciliationStrategy.Smooth:
                    return new SmoothReconciliation<TState>();
                case ReconciliationStrategy.Hybrid:
                    return new HybridReconciliation<TState>();
                default:
                    return new HybridReconciliation<TState>();
            }
        }

        /// <summary>
        /// So sánh 2 states để check equality.
        /// Override method này nếu cần custom comparison logic.
        /// </summary>
        /// <param name="state1">State 1</param>
        /// <param name="state2">State 2</param>
        /// <returns>True nếu states bằng nhau</returns>
        protected virtual bool CompareStates(TState state1, TState state2)
        {
            // Default: Dùng Equals (nếu TState implement IEquatable)
            return state1.Equals(state2);
        }

        /// <summary>
        /// IPredictedObject implementation.
        /// </summary>
        bool IPredictedObject.IsPredictionEnabled => IsPredictionEnabled;

        /// <summary>
        /// IPredictedObject implementation.
        /// </summary>
        Vector3 IPredictedObject.Position => transform.position;

        /// <summary>
        /// IPredictedObject implementation.
        /// </summary>
        void IPredictedObject.OnPredictionTick(uint tick)
        {
            // Tick-based update được xử lý trong Update()
        }

        /// <summary>
        /// Coroutine để register với PredictionManager sau khi Instance đã được khởi tạo.
        /// </summary>
        private IEnumerator RegisterWithPredictionManagerCoroutine()
        {
            // Wait until PredictionManager is available
            NightHunt.Networking.Prediction.Core.PredictionManager manager = null;
            while (manager == null)
            {
#if UNITY_2023_1_OR_NEWER
                manager = FindFirstObjectByType<NightHunt.Networking.Prediction.Core.PredictionManager>();
#else
                manager = FindObjectOfType<NightHunt.Networking.Prediction.Core.PredictionManager>();
#endif
                if (manager == null)
                {
                    yield return null;
                }
            }

            manager.RegisterPredictedObject(this);
        }

        protected virtual void OnDestroy()
        {
            // Unregister từ PredictionManager
#if UNITY_2023_1_OR_NEWER
            var manager = FindFirstObjectByType<NightHunt.Networking.Prediction.Core.PredictionManager>();
#else
            var manager = FindObjectOfType<NightHunt.Networking.Prediction.Core.PredictionManager>();
#endif
            if (manager != null)
            {
                manager.UnregisterPredictedObject(this);
            }

            _stateHistory?.Clear();
            _inputBuffer?.Clear();
        }
    }

    /// <summary>
    /// Enum cho reconciliation strategy.
    /// </summary>
    public enum ReconciliationStrategy
    {
        Snap,    // Instant correction
        Smooth,  // Lerp correction
        Hybrid   // Kết hợp snap + smooth dựa trên threshold
    }
}

