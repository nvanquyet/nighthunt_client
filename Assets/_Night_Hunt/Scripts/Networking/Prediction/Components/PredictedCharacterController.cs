// using UnityEngine;
// using FishNet.Object;
// using NightHunt.Networking.Prediction.Core;
// using NightHunt.Networking.Prediction.Input;
// using NightHunt.Networking.Prediction.Utils;
// using NightHunt.Gameplay.Character.Movement;
// using NightHunt.Gameplay.Character;
// using System;
//
// namespace NightHunt.Networking.Prediction.Components
// {
//     /// <summary>
//     /// State structure cho CharacterController prediction.
//     /// </summary>
//     [System.Serializable]
//     public struct CharacterControllerState : System.IEquatable<CharacterControllerState>
//     {
//         public Vector3 position;
//         public Quaternion rotation;
//         public Vector3 velocity;
//
//         public CharacterControllerState(Vector3 position, Quaternion rotation, Vector3 velocity)
//         {
//             this.position = position;
//             this.rotation = rotation;
//             this.velocity = velocity;
//         }
//
//         public bool Equals(CharacterControllerState other)
//         {
//             return position.Equals(other.position) && 
//                    rotation.Equals(other.rotation) && 
//                    velocity.Equals(other.velocity);
//         }
//
//         public override bool Equals(object obj)
//         {
//             return obj is CharacterControllerState other && Equals(other);
//         }
//
//         public override int GetHashCode()
//         {
//             return System.HashCode.Combine(position, rotation, velocity);
//         }
//     }
//
//     /// <summary>
//     /// Input structure cho CharacterController prediction.
//     /// </summary>
//     [System.Serializable]
//     public struct CharacterControllerInput : IInputData
//     {
//         public Vector2 moveInput;
//         public Quaternion rotationInput;
//         public bool isSprinting;
//         public bool isCrouching;
//         public bool hasMovementChange;
//         public bool hasRotationChange;
//
//         public CharacterControllerInput(Vector2 moveInput, Quaternion rotationInput, bool isSprinting, bool isCrouching, bool hasMovementChange, bool hasRotationChange)
//         {
//             this.moveInput = moveInput;
//             this.rotationInput = rotationInput;
//             this.isSprinting = isSprinting;
//             this.isCrouching = isCrouching;
//             this.hasMovementChange = hasMovementChange;
//             this.hasRotationChange = hasRotationChange;
//         }
//
//         public bool HasChanged(IInputData other)
//         {
//             if (other is CharacterControllerInput otherInput)
//             {
//                 return hasMovementChange || hasRotationChange ||
//                        isSprinting != otherInput.isSprinting ||
//                        isCrouching != otherInput.isCrouching ||
//                        !moveInput.Equals(otherInput.moveInput) ||
//                        !rotationInput.Equals(otherInput.rotationInput);
//             }
//             return true;
//         }
//
//         public void Reset()
//         {
//             moveInput = Vector2.zero;
//             rotationInput = Quaternion.identity;
//             isSprinting = false;
//             isCrouching = false;
//             hasMovementChange = false;
//             hasRotationChange = false;
//         }
//     }
//
//     /// <summary>
//     /// Predicted CharacterController component.
//     /// Wrapper cho CharacterController với prediction support.
//     /// Tích hợp với CharacterMovement hiện có.
//     /// </summary>
//     [RequireComponent(typeof(CharacterController))]
//     public class PredictedCharacterController : PredictedObject<CharacterControllerState, CharacterControllerInput>
//     {
//         private CharacterController _characterController;
//         
//         // Cached config values (loaded from MovementConfig)
//         private float baseSpeed;
//         private float sprintMultiplier;
//         private float crouchMultiplier;
//         private float gravity;
//         private bool useWeightModifier;
//         private float maxWeight;
//         private float positionThreshold;
//         private float rotationThreshold;
//         private float smoothLerpSpeed;
//         private float movingVelocityThreshold;
//         private float movingPositionErrorThreshold;
//         private float stoppedLerpSpeed;
//         private float collisionCheckDistance;
//         private LayerMask wallLayers;
//         private float minWallHeight;
//         private float kneeHeight;
//         private float headHeight;
//         
//         // Weight modifier callback (set từ external code)
//         private Func<float> _weightModifierCallback;
//         
//         // Server input callbacks (set từ external code để package độc lập)
//         private Action<Vector2, bool, bool> _onServerMovementInput;
//         private Action<Quaternion> _onServerRotationInput;
//
//         protected override void Awake()
//         {
//             base.Awake();
//             _characterController = GetComponent<CharacterController>();
//             if (_characterController == null)
//             {
//                 Debug.LogError("[PredictedCharacterController] CharacterController component not found!");
//             }
//             
//             // Load config values
//             LoadConfig();
//         }
//         
//         /// <summary>
//         /// Load MovementConfig từ CharacterConfig hoặc default.
//         /// </summary>
//         private MovementConfig LoadMovementConfig()
//         {
//             // Tìm CharacterConfig từ CharacterConfigManager
//             var configManager = CharacterConfigManager.Instance;
//             if (configManager != null && configManager.CharacterConfig != null)
//             {
//                 var movementConfig = configManager.CharacterConfig.MovementConfig;
//                 if (movementConfig != null)
//                 {
//                     return movementConfig;
//                 }
//             }
//
//             // Fallback: Load từ CharacterConfigData (JSON) nếu có
//             var characterConfigData = GameConfigLoader.Instance?.GetCharacterConfig("CHAR_DEFAULT");
//             if (characterConfigData != null && !string.IsNullOrEmpty(characterConfigData.MovementConfigName))
//             {
//                 // Có thể load từ Resources nếu cần (tạm thời return null để dùng default)
//             }
//
//             // Return null để dùng default values
//             return null;
//         }
//         
//         /// <summary>
//         /// Load movement config values. Nếu không có config, dùng default values.
//         /// </summary>
//         private void LoadConfig()
//         {
//             var movementConfig = LoadMovementConfig();
//             if (movementConfig != null)
//             {
//                 baseSpeed = movementConfig.baseMoveSpeed;
//                 sprintMultiplier = movementConfig.sprintMultiplier;
//                 crouchMultiplier = movementConfig.crouchMultiplier;
//                 gravity = movementConfig.gravity;
//                 useWeightModifier = movementConfig.useWeightModifier;
//                 maxWeight = movementConfig.maxWeight;
//                 positionThreshold = movementConfig.positionThreshold;
//                 rotationThreshold = movementConfig.rotationThreshold;
//                 smoothLerpSpeed = movementConfig.smoothLerpSpeed;
//                 movingVelocityThreshold = movementConfig.movingVelocityThreshold;
//                 movingPositionErrorThreshold = movementConfig.movingPositionErrorThreshold;
//                 stoppedLerpSpeed = movementConfig.stoppedLerpSpeed;
//                 collisionCheckDistance = movementConfig.collisionCheckDistance;
//                 wallLayers = movementConfig.wallLayers;
//                 minWallHeight = movementConfig.minWallHeight;
//                 kneeHeight = movementConfig.kneeHeight;
//                 headHeight = movementConfig.headHeight;
//             }
//             else
//             {
//                 // Default values nếu không có config
//                 baseSpeed = 5f;
//                 sprintMultiplier = 1.5f;
//                 crouchMultiplier = 0.6f;
//                 gravity = -9.81f;
//                 useWeightModifier = true;
//                 maxWeight = 20f;
//                 positionThreshold = 0.01f;
//                 rotationThreshold = 5f;
//                 smoothLerpSpeed = 20f;
//                 movingVelocityThreshold = 0.15f;
//                 movingPositionErrorThreshold = 0.8f;
//                 stoppedLerpSpeed = 25f;
//                 collisionCheckDistance = 0.1f;
//                 wallLayers = -1;
//                 minWallHeight = 0.5f;
//                 kneeHeight = 0.5f;
//                 headHeight = 1.6f;
//                 
//                 Debug.LogWarning("[PredictedCharacterController] MovementConfig not assigned, using default values!");
//             }
//         }
//
//         protected override CharacterControllerState GetInitialState()
//         {
//             return new CharacterControllerState(
//                 transform.position,
//                 transform.rotation,
//                 Vector3.zero
//             );
//         }
//
//         protected override bool TryGetInput(out CharacterControllerInput input)
//         {
//             // Input sẽ được set từ external code (ví dụ: PlayerInputHandler)
//             input = default;
//             return false;
//         }
//
//         /// <summary>
//         /// Set input từ external code. Triggers prediction flow: buffer input, predict state, apply, send to server.
//         /// </summary>
//         public void SetInput(Vector2 moveInput, Quaternion rotationInput, bool isSprinting, bool isCrouching)
//         {
//             if (!IsOwner || !IsPredictionEnabled) return;
//
//             // ✅ CRITICAL: Luôn check movement change, kể cả khi input = (0,0)
//             // Điều này đảm bảo khi release input, velocity được clear
//             bool hasMovement = true; // Luôn true để đảm bảo input được xử lý
//             bool hasRotation = !rotationInput.Equals(transform.rotation);
//
//             var input = new CharacterControllerInput(
//                 moveInput,
//                 rotationInput,
//                 isSprinting,
//                 isCrouching,
//                 hasMovement,
//                 hasRotation
//             );
//
//             uint currentTick = CurrentTick;
//             _inputBuffer.AddInput(input, currentTick);
//             _currentState = PredictState(input, _currentState);
//             _stateHistory.AddState(_currentState, currentTick);
//             _stateHistory.IncrementTick();
//             ApplyState(_currentState);
//             SendInputToServer(input, currentTick);
//         }
//
//         /// <summary>
//         /// Set weight modifier callback function.
//         /// </summary>
//         public void SetWeightModifier(Func<float> callback)
//         {
//             _weightModifierCallback = callback;
//         }
//
//         /// <summary>
//         /// Set server input callbacks. Package độc lập, external code register callbacks.
//         /// </summary>
//         public void SetServerInputCallbacks(Action<Vector2, bool, bool> onMovementInput, Action<Quaternion> onRotationInput)
//         {
//             _onServerMovementInput = onMovementInput;
//             _onServerRotationInput = onRotationInput;
//         }
//
//         /// <summary>
//         /// Sends input to server via ServerRpc. Package handles this independently.
//         /// </summary>
//         protected override void SendInputToServer(CharacterControllerInput input, uint tick)
//         {
//             if (input.hasMovementChange || input.hasRotationChange)
//             {
//                 ServerReceiveInput(input, tick);
//             }
//         }
//
//         /// <summary>
//         /// Server receives input from client, predicts state, applies it, and sends reconciliation data back.
//         /// Also updates NetworkCharacterMovement to sync with TopDownMovement.
//         /// </summary>
//         [ServerRpc(RequireOwnership = true)]
//         private void ServerReceiveInput(CharacterControllerInput input, uint tick)
//         {
//             if (!IsServer) return;
//
//             // ✅ CRITICAL: Server predict và apply state
//             // ApplyState() sẽ check collision và update position thực tế
//             CharacterControllerState serverState = PredictState(input, _currentState);
//             ApplyState(serverState);
//             
//             // ✅ CRITICAL: Server state position đã được update bởi ApplyState() với actual position
//             // Nếu có collision, position sẽ không thay đổi (CharacterController tự động block)
//             // Server state.position giờ là actual position sau khi Move(), không phải predicted position
//             
//             // Package độc lập: Gọi callbacks thay vì reference trực tiếp
//             if (input.hasMovementChange && _onServerMovementInput != null)
//             {
//                 _onServerMovementInput(input.moveInput, input.isSprinting, input.isCrouching);
//             }
//             if (input.hasRotationChange && _onServerRotationInput != null)
//             {
//                 _onServerRotationInput(input.rotationInput);
//             }
//             
//             // ✅ CRITICAL: Gửi server state với actual position (đã được validate bởi collision)
//             SendReconciliationDataToClient(serverState, tick);
//         }
//         
//         /// <summary>
//         /// Sends reconciliation data to client owner for rollback/replay if state differs.
//         /// </summary>
//         [ObserversRpc(ExcludeOwner = false, ExcludeServer = true)]
//         private void SendReconciliationDataToClient(CharacterControllerState serverState, uint serverTick)
//         {
//             if (!IsOwner) return;
//             
//             var reconciliationData = new ReconciliationData<CharacterControllerState>(
//                 serverTick,
//                 serverState,
//                 Time.time
//             );
//             
//             Reconcile(reconciliationData);
//         }
//
//
//         protected override CharacterControllerState PredictState(CharacterControllerInput input, CharacterControllerState currentState)
//         {
//             CharacterControllerState newState = currentState;
//
//             // Initialize CharacterController nếu chưa có
//             if (_characterController == null)
//             {
//                 _characterController = GetComponent<CharacterController>();
//             }
//
//             // Calculate horizontal movement
//             Vector3 horizontalVelocity = Vector3.zero;
//             if (input.hasMovementChange && input.moveInput.magnitude > 0.01f)
//             {
//                 float speed = baseSpeed;
//
//                 // Apply sprint/crouch multiplier
//                 if (input.isSprinting)
//                 {
//                     speed *= sprintMultiplier;
//                 }
//                 else if (input.isCrouching)
//                 {
//                     speed *= crouchMultiplier;
//                 }
//
//                 // Apply weight modifier nếu có
//                 if (useWeightModifier && _weightModifierCallback != null)
//                 {
//                     float modifier = _weightModifierCallback();
//                     speed *= modifier;
//                 }
//
//                 // Calculate movement direction
//                 Vector3 moveDirection = new Vector3(input.moveInput.x, 0f, input.moveInput.y).normalized;
//                 
//                 // Rotate movement direction theo rotation hiện tại
//                 moveDirection = currentState.rotation * moveDirection;
//
//                 // ✅ CRITICAL: Check collision với tường ở client TRƯỚC khi tính velocity
//                 // Chỉ check horizontal collision (tường), không check ground
//                 if (_characterController != null)
//                 {
//                     Vector3 checkPosition = currentState.position;
//                     Vector3 checkDirection = moveDirection;
//                     float checkDistance = speed * Time.fixedDeltaTime + collisionCheckDistance;
//                     
//                     // Get CharacterController bounds
//                     Vector3 center = checkPosition + _characterController.center;
//                     float radius = _characterController.radius;
//                     float halfHeight = _characterController.height * 0.5f;
//                     
//                     // ✅ CRITICAL: Dùng raycast để check tường
//                     // Check 3 vị trí ngang: center, left, right
//                     // Check 3 độ cao: đầu gối, center, đầu
//                     bool hasWallCollision = false;
//                     
//                     Vector3 rightDir = Vector3.Cross(checkDirection, Vector3.up).normalized;
//                     
//                     // Check từ 3 vị trí ngang: center, left, right
//                     Vector3[] horizontalOffsets = new Vector3[]
//                     {
//                         Vector3.zero, // Center
//                         rightDir * radius, // Right
//                         -rightDir * radius // Left
//                     };
//                     
//                     // Check ở 3 độ cao: đầu gối, center, đầu
//                     float[] heightOffsets = new float[]
//                     {
//                         kneeHeight, // Đầu gối
//                         _characterController.height * 0.5f, // Center
//                         headHeight // Đầu
//                     };
//                     
//                     foreach (Vector3 horizontalOffset in horizontalOffsets)
//                     {
//                         foreach (float heightOffset in heightOffsets)
//                         {
//                             Vector3 rayOrigin = new Vector3(checkPosition.x, checkPosition.y + heightOffset, checkPosition.z) + horizontalOffset;
//                             
//                             // Raycast horizontal về phía trước
//                             RaycastHit hit;
//                             if (Physics.Raycast(rayOrigin, checkDirection, out hit, checkDistance, wallLayers))
//                             {
//                                 // Check xem có phải là tường không (normal không phải là ground)
//                                 Vector3 hitNormal = hit.normal;
//                                 float verticalDot = Mathf.Abs(Vector3.Dot(hitNormal, Vector3.up));
//                                 
//                                 // Nếu normal không phải là ground (verticalDot < 0.7), thì là tường
//                                 if (verticalDot < 0.7f)
//                                 {
//                                     // Tường đủ cao (từ minWallHeight trở lên) → block movement
//                                     hasWallCollision = true;
//                                     break;
//                                 }
//                             }
//                         }
//                         
//                         if (hasWallCollision) break;
//                     }
//                     
//                     // Nếu có collision với tường, không di chuyển (velocity = 0)
//                     if (hasWallCollision)
//                     {
//                         horizontalVelocity = Vector3.zero;
//                     }
//                     else
//                     {
//                         // Không có collision - tính velocity bình thường
//                         horizontalVelocity = moveDirection * speed;
//                     }
//                 }
//                 else
//                 {
//                     // Không có CharacterController - tính velocity bình thường
//                     horizontalVelocity = moveDirection * speed;
//                 }
//             }
//             else
//             {
//                 // No input - clear horizontal velocity
//                 horizontalVelocity = Vector3.zero;
//             }
//
//             // Apply gravity to vertical velocity
//             float verticalVelocity = currentState.velocity.y;
//             
//             // Apply gravity
//             if (_characterController != null && !_characterController.isGrounded)
//             {
//                 // Not grounded - apply gravity
//                 verticalVelocity += gravity * Time.fixedDeltaTime;
//             }
//             else if (_characterController != null && _characterController.isGrounded)
//             {
//                 // Grounded - reset vertical velocity to small downward force
//                 verticalVelocity = gravity * Time.fixedDeltaTime;
//             }
//             else
//             {
//                 // CharacterController not available - apply gravity anyway
//                 verticalVelocity += gravity * Time.fixedDeltaTime;
//             }
//
//             // Combine horizontal and vertical velocity
//             newState.velocity = new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z);
//
//             // Calculate movement vector
//             Vector3 movement = newState.velocity * Time.fixedDeltaTime;
//             
//             // Calculate new position
//             newState.position = currentState.position + movement;
//
//             // Apply rotation
//             if (input.hasRotationChange)
//             {
//                 newState.rotation = input.rotationInput;
//             }
//
//             return newState;
//         }
//
//         protected override void ApplyState(CharacterControllerState state)
//         {
//             // Initialize CharacterController nếu chưa có
//             if (_characterController == null)
//             {
//                 _characterController = GetComponent<CharacterController>();
//             }
//             
//             if (_characterController != null)
//             {
//                 // ✅ CRITICAL: Luôn dùng velocity để apply movement
//                 // Velocity đã được tính đúng trong PredictState(), bao gồm cả gravity
//                 Vector3 movement = state.velocity * Time.fixedDeltaTime;
//                 
//                 // Apply movement với CharacterController.Move để respect collisions và gravity
//                 // CharacterController.Move() trả về CollisionFlags để check collision
//                 CollisionFlags collisionFlags = _characterController.Move(movement);
//                 
//                 // ✅ CRITICAL: Check collision - nếu có collision với tường, không update position
//                 // Điều này ngăn player teleport xuyên tường (server-side validation)
//                 bool hasCollision = (collisionFlags & CollisionFlags.Sides) != 0;
//                 
//                 // Update state position với actual position sau khi Move()
//                 // Nếu có collision, position sẽ không thay đổi (CharacterController tự động block)
//                 Vector3 actualPosition = transform.position;
//                 
//                 // ✅ CRITICAL: Reconciliation logic - smooth khi di chuyển, chính xác khi dừng
//                 Vector3 currentPosition = actualPosition;
//                 Vector3 targetPosition = state.position;
//                 float positionDistance = Vector3.Distance(currentPosition, targetPosition);
//                 
//                 // ✅ CRITICAL: Check horizontal velocity để xác định đang di chuyển (không tính gravity)
//                 Vector3 horizontalVelocity = new Vector3(state.velocity.x, 0f, state.velocity.z);
//                 bool isMoving = horizontalVelocity.magnitude > movingVelocityThreshold;
//                 
//                 // Nếu position khác nhau và không có collision
//                 if (positionDistance > 0.0001f && !hasCollision)
//                 {
//                     bool shouldReconcile = false;
//                     float lerpSpeed = smoothLerpSpeed;
//                     
//                     if (isMoving)
//                     {
//                         // ✅ Đang di chuyển: Chỉ reconcile nếu sai lệch QUÁ LỚN
//                         // Tăng threshold cao để tránh reconcile khi đang di chuyển (giữ smooth)
//                         if (positionDistance > movingPositionErrorThreshold)
//                         {
//                             shouldReconcile = true;
//                             // Reconcile rất chậm khi đang di chuyển để không giật
//                             lerpSpeed = smoothLerpSpeed * 0.3f; // Rất chậm khi đang di chuyển
//                         }
//                         // Nếu sai lệch nhỏ khi đang di chuyển → KHÔNG reconcile (giữ smooth hoàn toàn)
//                     }
//                     else
//                     {
//                         // ✅ Dừng lại: Reconcile chính xác về server position
//                         shouldReconcile = true;
//                         lerpSpeed = stoppedLerpSpeed; // Nhanh hơn khi dừng để chính xác
//                     }
//                     
//                     if (shouldReconcile)
//                     {
//                         // Smooth lerp về server position với damping để tránh overshoot
//                         float t = Mathf.Clamp01(lerpSpeed * Time.fixedDeltaTime);
//                         Vector3 lerpedPosition = Vector3.Lerp(currentPosition, targetPosition, t);
//                         Vector3 correctionDelta = lerpedPosition - currentPosition;
//                         
//                         // Apply correction với CharacterController để respect collisions
//                         if (correctionDelta.magnitude > 0.0001f)
//                         {
//                             CollisionFlags correctionFlags = _characterController.Move(correctionDelta);
//                             // Nếu correction có collision, không apply thêm
//                             if ((correctionFlags & CollisionFlags.Sides) != 0)
//                             {
//                                 // Có collision khi correction - giữ nguyên position hiện tại
//                             }
//                         }
//                     }
//                 }
//                 
//                 // ✅ CRITICAL: Update state với actual position (sau khi Move())
//                 // Server position giờ là actual position đã được validate bởi collision
//                 state.position = transform.position;
//                 
//                 // Apply rotation (chỉ cho owner, non-owners nhận từ NetworkTransform)
//                 if (IsOwner && state.rotation != Quaternion.identity)
//                 {
//                     // Rotation được handle bởi TopDownMovement cho smooth lerping
//                     // Chỉ set rotation nếu cần (không override lerped rotation)
//                 }
//             }
//             else
//             {
//                 // Fallback: Direct position set nếu không có CharacterController
//                 transform.position = state.position;
//                 if (IsOwner && state.rotation != Quaternion.identity)
//                 {
//                     transform.rotation = state.rotation;
//                 }
//             }
//
//             _currentState = state;
//         }
//
//         protected override bool ShouldReconcile(CharacterControllerState clientState, CharacterControllerState serverState)
//         {
//             float positionError = Vector3.Distance(clientState.position, serverState.position);
//             float rotationError = Quaternion.Angle(clientState.rotation, serverState.rotation);
//
//             return positionError > positionThreshold || rotationError > rotationThreshold;
//         }
//
//         /// <summary>
//         /// Calculate weight penalty modifier.
//         /// </summary>
//         /// <param name="currentWeight">Current weight</param>
//         /// <returns>Speed modifier (0-1)</returns>
//         public float CalculateWeightPenalty(float currentWeight)
//         {
//             float weightPercent = currentWeight / maxWeight;
//
//             if (weightPercent < 0.8f)
//                 return 1f;      // 100% speed
//             else if (weightPercent < 1f)
//                 return 0.9f;   // 90% speed
//             else if (weightPercent < 1.4f)
//                 return 0.8f;   // 80% speed
//             else
//                 return 0.5f;   // 50% speed
//         }
//
//     }
// }
//
//
//
