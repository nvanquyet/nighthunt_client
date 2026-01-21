using UnityEngine;
using NightHunt.Networking.Prediction.Core;
using NightHunt.Networking.Prediction.Input;
using NightHunt.Networking.Prediction.Utils;
using System;

namespace NightHunt.Networking.Prediction.Components
{
    /// <summary>
    /// State structure cho Transform prediction.
    /// </summary>
    [Serializable]
    public struct TransformState : IEquatable<TransformState>
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public TransformState(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }

        public bool Equals(TransformState other)
        {
            return position.Equals(other.position) && 
                   rotation.Equals(other.rotation) && 
                   scale.Equals(other.scale);
        }

        public override bool Equals(object obj)
        {
            return obj is TransformState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(position, rotation, scale);
        }
    }

    /// <summary>
    /// Input structure cho Transform prediction.
    /// </summary>
    [Serializable]
    public struct TransformInput : IInputData
    {
        public Vector2 moveInput;
        public Quaternion rotationInput;
        public bool hasMovementChange;
        public bool hasRotationChange;

        public TransformInput(Vector2 moveInput, Quaternion rotationInput, bool hasMovementChange, bool hasRotationChange)
        {
            this.moveInput = moveInput;
            this.rotationInput = rotationInput;
            this.hasMovementChange = hasMovementChange;
            this.hasRotationChange = hasRotationChange;
        }

        public bool HasChanged(IInputData other)
        {
            if (other is TransformInput otherInput)
            {
                return hasMovementChange || hasRotationChange ||
                       !moveInput.Equals(otherInput.moveInput) ||
                       !rotationInput.Equals(otherInput.rotationInput);
            }
            return true;
        }

        public void Reset()
        {
            moveInput = Vector2.zero;
            rotationInput = Quaternion.identity;
            hasMovementChange = false;
            hasRotationChange = false;
        }
    }

    /// <summary>
    /// Predicted Transform component cho position, rotation, scale prediction.
    /// Hỗ trợ weight-based speed modifier system và smooth interpolation.
    /// </summary>
    [RequireComponent(typeof(Transform))]
    public class PredictedTransform : PredictedObject<TransformState, TransformInput>
    {
        [Header("Movement Settings")]
        [SerializeField] private float baseSpeed = 5f;
        [SerializeField] private bool useWeightModifier = false;

        [Header("Interpolation Settings")]
        [SerializeField] private bool enableInterpolation = true;
        [SerializeField] private float interpolationDelay = 0.1f;
        [SerializeField] private bool useExtrapolation = true;
        [SerializeField] private float extrapolationLimit = 0.5f;

        [Header("Reconciliation Settings")]
        [SerializeField] private float positionThreshold = 0.1f;
        [SerializeField] private float rotationThreshold = 5f; // degrees

        // Weight modifier callback (set từ external code)
        private Func<float> _weightModifierCallback;

        // Interpolation state
        private TransformState _targetState;
        private TransformState _previousState;
        private float _targetTime;
        private bool _hasTargetState;

        /// <summary>
        /// Set weight modifier callback function.
        /// Callback trả về speed multiplier dựa trên weight (0-1).
        /// </summary>
        /// <param name="callback">Callback function trả về speed multiplier</param>
        public void SetSpeedModifier(Func<float> callback)
        {
            _weightModifierCallback = callback;
        }

        protected override TransformState GetInitialState()
        {
            return new TransformState(transform.position, transform.rotation, transform.localScale);
        }

        protected override bool TryGetInput(out TransformInput input)
        {
            // Input sẽ được set từ external code (ví dụ: PlayerInputHandler)
            // Override method này trong derived class hoặc set input từ external
            input = default;
            return false;
        }

        /// <summary>
        /// Set input từ external code (ví dụ: PlayerInputHandler).
        /// </summary>
        /// <param name="moveInput">Movement input (WASD)</param>
        /// <param name="rotationInput">Rotation input</param>
        public void SetInput(Vector2 moveInput, Quaternion rotationInput)
        {
            bool hasMovement = moveInput.magnitude > 0.01f;
            bool hasRotation = !rotationInput.Equals(transform.rotation);

            var input = new TransformInput(moveInput, rotationInput, hasMovement, hasRotation);
            
            // Process input ngay lập tức
            if (IsOwner && IsPredictionEnabled)
            {
                ProcessInput(input);
            }
        }

        /// <summary>
        /// Process input và predict state.
        /// </summary>
        private void ProcessInput(TransformInput input)
        {
            if (input.hasMovementChange || input.hasRotationChange)
            {
                _currentState = PredictState(input, _currentState);
                ApplyState(_currentState);
            }
        }

        protected override TransformState PredictState(TransformInput input, TransformState currentState)
        {
            TransformState newState = currentState;

            // Apply movement
            if (input.hasMovementChange)
            {
                float speed = baseSpeed;

                // Apply weight modifier nếu có
                if (useWeightModifier && _weightModifierCallback != null)
                {
                    float modifier = _weightModifierCallback();
                    speed *= modifier;
                }

                // Calculate movement direction từ input
                Vector3 moveDirection = new Vector3(input.moveInput.x, 0f, input.moveInput.y).normalized;
                
                // Rotate movement direction theo rotation hiện tại
                moveDirection = currentState.rotation * moveDirection;

                // Apply movement
                newState.position = currentState.position + moveDirection * speed * Time.fixedDeltaTime;
            }

            // Apply rotation
            if (input.hasRotationChange)
            {
                newState.rotation = input.rotationInput;
            }

            return newState;
        }

        protected override void ApplyState(TransformState state)
        {
            if (IsOwner)
            {
                // Owner: Apply trực tiếp (client-side prediction)
                transform.position = state.position;
                transform.rotation = state.rotation;
                transform.localScale = state.scale;
            }
            else
            {
                // Non-owner: Interpolate
                if (enableInterpolation)
                {
                    _previousState = _currentState;
                    _targetState = state;
                    _targetTime = Time.time + interpolationDelay;
                    _hasTargetState = true;
                }
                else
                {
                    transform.position = state.position;
                    transform.rotation = state.rotation;
                    transform.localScale = state.scale;
                }
            }

            _currentState = state;
        }

        protected override void Update()
        {
            base.Update();

            // Interpolation cho non-owner
            if (!IsOwner && enableInterpolation && _hasTargetState)
            {
                InterpolateState();
            }
        }

        /// <summary>
        /// Interpolate state cho non-owner clients.
        /// </summary>
        private void InterpolateState()
        {
            float currentTime = Time.time;
            float targetTime = _targetTime;

            if (currentTime >= targetTime)
            {
                // Interpolate
                float t = InterpolationHelper.CalculateInterpolationFactor(
                    targetTime - interpolationDelay,
                    targetTime,
                    currentTime
                );

                transform.position = InterpolationHelper.Lerp(_previousState.position, _targetState.position, t);
                transform.rotation = InterpolationHelper.Lerp(_previousState.rotation, _targetState.rotation, t);
                transform.localScale = InterpolationHelper.Lerp(_previousState.scale, _targetState.scale, t);
            }
            else if (useExtrapolation)
            {
                // Extrapolate
                float deltaTime = currentTime - targetTime;
                if (deltaTime <= extrapolationLimit)
                {
                    // Estimate velocity từ previous states
                    Vector3 velocity = (_targetState.position - _previousState.position) / interpolationDelay;
                    transform.position = InterpolationHelper.Extrapolate(_targetState.position, velocity, deltaTime);
                    transform.rotation = _targetState.rotation; // Rotation không extrapolate
                    transform.localScale = _targetState.scale;
                }
            }
        }

        protected override bool ShouldReconcile(TransformState clientState, TransformState serverState)
        {
            float positionError = Vector3.Distance(clientState.position, serverState.position);
            float rotationError = Quaternion.Angle(clientState.rotation, serverState.rotation);

            return positionError > positionThreshold || rotationError > rotationThreshold;
        }

    }
}

