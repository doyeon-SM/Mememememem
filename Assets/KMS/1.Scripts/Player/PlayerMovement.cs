using System;
using System.Collections.Generic;
using KMS.Audio;
using UnityEngine;

namespace KMS
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        public enum MovementDirectionMode
        {
            World,
            CharacterRelative,
            CameraRelative
        }

        [Header("References")]
        [SerializeField] private PlayerInput input;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerStats stats;
        [SerializeField] private KMSFoodEffectController foodEffects;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform rotationTransform;
        [SerializeField] private Animator animator;

        [Header("Movement")]
        [SerializeField] private MovementDirectionMode directionMode = MovementDirectionMode.CameraRelative;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float deceleration = 22f;
        [SerializeField] private float rotationSmoothTime = 0.08f;
        [SerializeField] private bool rotateTowardsMovement = true;

        [Header("Step Traversal")]
        [Tooltip("점프 없이 자동으로 넘어갈 수 있는 최대 단차 높이(m)입니다. CharacterController Step Offset과 동기화됩니다.")]
        [SerializeField, Min(0f)] private float maxStepHeight = 0.25f;
        [Tooltip("단차를 올라갈 때 캐릭터 모델이 새 높이에 자연스럽게 따라가는 데 걸리는 시간(초)입니다.")]
        [SerializeField, Min(0.01f)] private float stepSmoothTime = 0.08f;
        [Tooltip("단차 보간을 적용할 시각 모델 루트입니다. 비어 있으면 Animator의 최상위 시각 루트를 자동으로 찾습니다.")]
        [SerializeField] private Transform stepVisualRoot;

        [Header("Jump And Gravity")]
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedStickForce = -2f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("Ground Check")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private float groundedOffset = -0.12f;
        [SerializeField] private float groundedRadius = 0.28f;

        [Header("Hunger Costs")]
        [SerializeField] private float moveHungerCostPerSecond = 1f;
        [SerializeField] private float jumpHungerCost = 8f;
        [SerializeField] private float sprintHungerCostPerSecond = 6f;

        [Header("External Forces")]
        [SerializeField] private float externalForceDecay = 8f;

        [Header("Ladder")]
        [SerializeField] private float ladderClimbSpeed = 3f;
        [SerializeField] private float ladderSlideDownSpeed = 2.4f;
        [SerializeField] private float ladderSnapSpeed = 12f;
        [SerializeField] private float ladderInputThreshold = 0.1f;
        [SerializeField, Min(0.01f)] private float ladderAnimationCycleDuration = 0.7666667f;

        public bool IsMovementEnabled
        {
            get => legacyMovementEnabled && movementBlockOwners.Count == 0;
            set
            {
                legacyMovementEnabled = value;
                if (!value) StopControlledMovement();
            }
        }
        public Animator Animator => animator;
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsOnLadder => activeLadder != null;
        public float CurrentSpeed { get; private set; }
        public float VerticalVelocity => verticalVelocity;
        public Vector3 LastMoveDirection { get; private set; } = Vector3.forward;
        public bool IsDead => isDead;
        public float MaxStepHeight => maxStepHeight;
        public float CurrentStepVisualOffset => stepVisualOffset;

        public event Action<float> Landed;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
        private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
        private static readonly int IsClimbingHash = Animator.StringToHash("IsClimbing");
        private static readonly int ClimbCycleHash = Animator.StringToHash("ClimbCycle");

        private float verticalVelocity;
        private float maximumDownwardSpeed;
        private bool suppressNextLanding = true;
        private bool wasGroundedByController;
        private float rotationVelocity;
        private float coyoteTimer;
        private float jumpBufferTimer;
        private Vector3 externalVelocity;
        private LadderVolume candidateLadder;
        private LadderVolume activeLadder;
        private float climbAnimationCycle;
        private bool isDead;
        private Vector3 stepVisualBaseLocalPosition;
        private float stepVisualOffset;
        private float stepVisualOffsetVelocity;
        private bool isStepVisualInitialized;
        private bool legacyMovementEnabled = true;
        private readonly HashSet<object> movementBlockOwners = new HashSet<object>();
        private readonly Dictionary<object, float> moveSpeedOverrideOwners = new Dictionary<object, float>();
        // [멤] 민첩 스탯 기반 기본 이동속도 배율(PlayerCombatStats.ApplyMoveSpeed에서 설정) - 액션 감속/음식효과와
        // 달리 "덮어쓰기"가 아니라 moveSpeed/sprintSpeed 자체에 곱해지는 기본값 수정에 가깝기 때문에(사용자 확인됨),
        // 액션 감속 오버라이드와 자연스럽게 곱연산된다(액션 감속 중에도 민첩 보너스가 그대로 적용됨).
        private float statSpeedMultiplier = 1f;

        private void Reset()
        {
            characterController = GetComponent<CharacterController>();
            input = GetComponent<PlayerInput>();
            stats = GetComponent<PlayerStats>();
            foodEffects = GetComponent<KMSFoodEffectController>();
            rotationTransform = transform;
            animator = GetComponentInChildren<Animator>();
            stepVisualRoot = FindStepVisualRoot();
            SyncStepHeight();
        }

        private void Awake()
        {
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (input == null) input = GetComponent<PlayerInput>();
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (foodEffects == null) foodEffects = GetComponent<KMSFoodEffectController>();
            if (rotationTransform == null) rotationTransform = transform;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (stepVisualRoot == null) stepVisualRoot = FindStepVisualRoot();

            SyncStepHeight();
            InitializeStepVisual();
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.JumpPressed += QueueJump;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.JumpPressed -= QueueJump;
            }

            ResetStepVisual();
            ResetFallTracking(true);
            wasGroundedByController = false;
            IsGrounded = false;
        }

        private void OnValidate()
        {
            maxStepHeight = Mathf.Max(0f, maxStepHeight);
            stepSmoothTime = Mathf.Max(0.01f, stepSmoothTime);
            ladderAnimationCycleDuration = Mathf.Max(0.01f, ladderAnimationCycleDuration);
            if (characterController == null) characterController = GetComponent<CharacterController>();
            SyncStepHeight();
        }

        private void Update()
        {
            if (isDead)
            {
                UpdateAnimator();
                return;
            }

            if (activeLadder != null)
            {
                HandleLadderMovement();
            }
            else
            {
                UpdateGroundedState();
                UpdateTimers();
                HandleJump();
                HandleMovement();
                TryEnterLadder();
            }

            UpdateAnimator();
        }

        private void LateUpdate()
        {
            if (!isStepVisualInitialized || stepVisualRoot == null) return;

            stepVisualOffset = Mathf.SmoothDamp(
                stepVisualOffset,
                0f,
                ref stepVisualOffsetVelocity,
                stepSmoothTime);

            if (Mathf.Abs(stepVisualOffset) < 0.0001f)
            {
                stepVisualOffset = 0f;
            }

            ApplyStepVisualOffset();
        }

        public void SetPosition(Vector3 position)
        {
            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;
            transform.position = position;
            characterController.enabled = wasEnabled;

            ResetFallTracking(true);
            wasGroundedByController = false;
            IsGrounded = false;
        }

        public void SetVerticalVelocity(float velocity)
        {
            verticalVelocity = velocity;

            if (velocity >= 0f)
            {
                maximumDownwardSpeed = 0f;
            }
        }

        public void ApplyExternalForce(Vector3 force)
        {
            externalVelocity += force;
        }

        public void ResetMovementForces()
        {
            verticalVelocity = 0f;
            externalVelocity = Vector3.zero;
            CurrentSpeed = 0f;

            if (!IsGrounded)
            {
                ResetFallTracking(true);
            }
        }

        /// <summary>
        /// Adds or removes a movement block owned by the caller. Movement stays blocked
        /// until every owner has released its own block.
        /// </summary>
        public void SetMovementBlocked(object owner, bool blocked)
        {
            if (owner == null)
            {
                Debug.LogWarning("[PlayerMovement] A movement block owner cannot be null.", this);
                return;
            }

            if (blocked)
            {
                if (movementBlockOwners.Add(owner)) StopControlledMovement();
            }
            else
            {
                movementBlockOwners.Remove(owner);
            }
        }

        /// <summary>
        /// [멤] 특정 오너가 이동속도 배율을 강제로 지정한다(음식효과 배율을 무시하고 덮어씀).
        /// multiplier가 null이면 해당 오너의 오버라이드를 해제한다. 여러 오너가 동시에 걸려있는
        /// 경우(정상적으로는 PlayerActionSlotCoordinator가 한 번에 하나만 걸리도록 보장하지만,
        /// 안전을 위한 방어 로직) 가장 낮은(가장 느린) 배율을 적용한다.
        /// </summary>
        public void SetMoveSpeedOverride(object owner, float? multiplier)
        {
            if (owner == null)
            {
                Debug.LogWarning("[PlayerMovement] A move speed override owner cannot be null.", this);
                return;
            }

            if (multiplier.HasValue)
            {
                moveSpeedOverrideOwners[owner] = Mathf.Clamp01(multiplier.Value);
            }
            else
            {
                moveSpeedOverrideOwners.Remove(owner);
            }
        }

        public bool HasMoveSpeedOverride => moveSpeedOverrideOwners.Count > 0;

        /// <summary>
        /// [멤] 민첩 스탯 보너스 배율(1.0~2.0)을 설정한다 - PlayerCombatStats가 스탯이 바뀜 때마다 호출한다.
        /// 기존 액션 오버라이드(Clamp01, 덮어쓰기)와 달리 기본 moveSpeed/sprintSpeed 자체를 바꾸는 방식이라
        /// 서로 충돌하지 않고 그대로 곱연산된다.
        /// </summary>
        public void SetStatSpeedMultiplier(float multiplier)
        {
            statSpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        private void StopControlledMovement()
        {
            CurrentSpeed = 0f;
            IsSprinting = false;
            jumpBufferTimer = 0f;
        }

        public void SetDead(bool dead)
        {
            isDead = dead;
            if (!dead) return;

            activeLadder = null;
            candidateLadder = null;
            climbAnimationCycle = 0f;
            IsSprinting = false;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            ResetMovementForces();
            ResetFallTracking(true);
            wasGroundedByController = false;
            IsGrounded = false;
        }

        private void TryEnterLadder()
        {
            if (!IsMovementEnabled || candidateLadder == null || input == null) return;
            if (input.Move.y <= ladderInputThreshold) return;

            activeLadder = candidateLadder;
            climbAnimationCycle = 0f;
            verticalVelocity = 0f;
            externalVelocity = Vector3.zero;
            CurrentSpeed = 0f;
            IsSprinting = false;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            ResetFallTracking(true);
            wasGroundedByController = false;
            IsGrounded = false;

            Vector3 ladderPoint = activeLadder.GetClosestPointOnPath(transform.position);
            //Vector3 facing = -activeLadder.Forward;
            //facing.y = 0f;

            //if (facing.sqrMagnitude > 0.001f)
            //{
            //    rotationTransform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            //}

            characterController.Move(ladderPoint - transform.position);
        }

        private void ExitLadder(Vector3 exitPosition)
        {
            activeLadder = null;
            climbAnimationCycle = 0f;
            verticalVelocity = groundedStickForce;
            externalVelocity = Vector3.zero;
            CurrentSpeed = 0f;
            IsSprinting = false;
            SetPosition(exitPosition);
        }

        private void QueueJump()
        {
            if (!IsMovementEnabled) return;
            jumpBufferTimer = jumpBufferTime;
        }

        private void UpdateGroundedState()
        {
            Vector3 spherePosition = transform.position + Vector3.up * groundedOffset;
            bool wasGrounded = IsGrounded;
            bool groundedNow = Physics.CheckSphere(
                spherePosition,
                groundedRadius,
                groundLayers,
                QueryTriggerInteraction.Ignore)
                || wasGroundedByController;

            if (!wasGrounded && groundedNow)
            {
                float impactSpeed = maximumDownwardSpeed;
                bool shouldNotify = !suppressNextLanding;

                ResetFallTracking(false);

                if (shouldNotify && impactSpeed > 0f)
                {
                    Landed?.Invoke(impactSpeed);
                }
            }

            IsGrounded = groundedNow;

            if (IsGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = groundedStickForce;
            }
        }

        private void UpdateTimers()
        {
            coyoteTimer = IsGrounded ? coyoteTime : Mathf.Max(0f, coyoteTimer - Time.deltaTime);
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
        }

        private void HandleJump()
        {
            if (!IsMovementEnabled) return;
            if (jumpBufferTimer <= 0f || coyoteTimer <= 0f) return;

            if (stats != null && !stats.ConsumeHunger(jumpHungerCost))
            {
                return;
            }

            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            KMSAudioService.PlayAt(GameSfxId.Jump, transform.position);
        }

private void HandleMovement()
        {
            Vector2 moveInput = IsMovementEnabled && input != null ? input.Move : Vector2.zero;
            float inputMagnitude = Mathf.Clamp01(moveInput.magnitude);
            bool hasMoveInput = inputMagnitude > 0.1f;

            // [멤] 공격/채집/캡슐던지기/취식 등 행동으로 인한 감속 중에는 스프린트를 금지하고,
            // 이동 방향으로의 자동 회전도 하지 않는다(조준·타격 방향이 이동으로 인해 흐트러지지
            // 않도록 각 행동 컨트롤러가 회전을 필요로 하면 스스로 담당한다).
            bool hasSpeedOverride = HasMoveSpeedOverride;

            IsSprinting = !hasSpeedOverride && hasMoveInput && input != null && input.IsSprinting;

            if (hasMoveInput && stats != null)
            {
                float cost = moveHungerCostPerSecond * Time.deltaTime;
                if (!stats.ConsumeHunger(cost))
                {
                    IsSprinting = false;
                }
            }

            if (IsSprinting && stats != null)
            {
                float cost = sprintHungerCostPerSecond * Time.deltaTime;
                IsSprinting = stats.ConsumeHunger(cost);
            }

            float movementMultiplier = ResolveMoveSpeedMultiplier();
            float targetSpeed = hasMoveInput
                ? (IsSprinting ? sprintSpeed : moveSpeed) * statSpeedMultiplier * movementMultiplier * inputMagnitude
                : 0f;
            float speedRate = targetSpeed > CurrentSpeed ? acceleration : deceleration;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, speedRate * Time.deltaTime);

            Vector3 moveDirection = hasMoveInput ? ResolveMoveDirection(moveInput) : LastMoveDirection;

            if (hasMoveInput)
            {
                LastMoveDirection = moveDirection;

                if (rotateTowardsMovement && !hasSpeedOverride)
                {
                    RotateTowards(moveDirection);
                }
            }

            verticalVelocity += gravity * Time.deltaTime;
            externalVelocity = Vector3.Lerp(externalVelocity, Vector3.zero, externalForceDecay * Time.deltaTime);

            Vector3 horizontalVelocity = hasMoveInput ? moveDirection * CurrentSpeed : Vector3.zero;
            Vector3 velocity = horizontalVelocity + externalVelocity + Vector3.up * verticalVelocity;

            float previousHeight = transform.position.y;
            CollisionFlags collisionFlags = characterController.Move(velocity * Time.deltaTime);
            wasGroundedByController = (collisionFlags & CollisionFlags.Below) != 0;

            float actualVerticalSpeed = Time.deltaTime > 0f
                ? (transform.position.y - previousHeight) / Time.deltaTime
                : 0f;

            // Track actual downward movement instead of the requested gravity velocity.
            // A CharacterController can remain caught on a ledge while its requested
            // vertical velocity keeps growing; treating that value as impact speed
            // causes severe damage after stepping down from a very small height.
            if (!IsGrounded && !wasGroundedByController && actualVerticalSpeed < 0f)
            {
                maximumDownwardSpeed = Mathf.Max(maximumDownwardSpeed, -actualVerticalSpeed);
            }

            float upwardStep = transform.position.y - previousHeight;
            if (IsGrounded
                && hasMoveInput
                && verticalVelocity <= 0f
                && upwardStep > 0.01f
                && upwardStep <= maxStepHeight + characterController.skinWidth + 0.01f)
            {
                AddStepVisualOffset(-upwardStep);
            }
        }

        private void HandleLadderMovement()
        {
            if (activeLadder == null) return;
            if (!IsMovementEnabled)
            {
                // Keep the current ladder while UI such as the inventory temporarily blocks movement.
                // activeLadder = null;
                return;
            }

            float verticalInput = input != null ? input.Move.y : 0f;
            bool climbingUp = verticalInput > ladderInputThreshold;
            float verticalSpeed = climbingUp ? ladderClimbSpeed : -ladderSlideDownSpeed;
            float normalizedClimbSpeed = Mathf.Approximately(ladderClimbSpeed, 0f)
                ? 0f
                : verticalSpeed / ladderClimbSpeed;
            climbAnimationCycle = Mathf.Repeat(
                climbAnimationCycle
                    + normalizedClimbSpeed * Time.deltaTime / ladderAnimationCycleDuration,
                1f);

            Vector3 snappedPoint = activeLadder.GetClosestPointOnPath(transform.position);
            Vector3 snapDelta = snappedPoint - transform.position;
            Vector3 snapVelocity = snapDelta * ladderSnapSpeed;
            Vector3 ladderVelocity = activeLadder.Up * verticalSpeed + snapVelocity;

            verticalVelocity = 0f;
            externalVelocity = Vector3.zero;
            IsGrounded = false;
            IsSprinting = false;
            CurrentSpeed = Mathf.Abs(verticalSpeed);

            characterController.Move(ladderVelocity * Time.deltaTime);

            float height = activeLadder.GetNormalizedHeight(transform.position);
            if (climbingUp && height >= 1f)
            {
                ExitLadder(activeLadder.GetTopExitPoint());
            }
            else if (!climbingUp && height <= 0f)
            {
                ExitLadder(activeLadder.GetBottomExitPoint());
            }
        }

        private void ResetFallTracking(bool suppressLanding)
        {
            maximumDownwardSpeed = 0f;
            suppressNextLanding = suppressLanding;
        }

        private Vector3 ResolveMoveDirection(Vector2 moveInput)
        {
            Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

            switch (directionMode)
            {
                case MovementDirectionMode.CharacterRelative:
                    return rotationTransform.TransformDirection(inputDirection).normalized;

                case MovementDirectionMode.CameraRelative:
                    Transform cam = cameraTransform != null ? cameraTransform : Camera.main != null ? Camera.main.transform : null;
                    if (cam == null) return inputDirection;

                    Vector3 forward = cam.forward;
                    Vector3 right = cam.right;
                    forward.y = 0f;
                    right.y = 0f;
                    forward.Normalize();
                    right.Normalize();

                    return (forward * moveInput.y + right * moveInput.x).normalized;

                case MovementDirectionMode.World:
                default:
                    return inputDirection;
            }
        }

        private void RotateTowards(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;

            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float smoothedAngle = Mathf.SmoothDampAngle(
                rotationTransform.eulerAngles.y,
                targetAngle,
                ref rotationVelocity,
                rotationSmoothTime);

            rotationTransform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            float effectiveSprintSpeed = sprintSpeed * statSpeedMultiplier * ResolveMoveSpeedMultiplier();
            float normalizedSpeed = Mathf.Approximately(effectiveSprintSpeed, 0f)
                ? 0f
                : CurrentSpeed / effectiveSprintSpeed;
            animator.SetFloat(SpeedHash, CurrentSpeed);
            animator.SetFloat(MotionSpeedHash, Mathf.Clamp01(normalizedSpeed));
            animator.SetBool(GroundedHash, IsGrounded);
            animator.SetBool(JumpHash, !IsGrounded && verticalVelocity > 0f);
            animator.SetBool(FreeFallHash, !IsGrounded && verticalVelocity < 0f);
            animator.SetBool(IsClimbingHash, IsOnLadder);
            animator.SetFloat(ClimbCycleHash, climbAnimationCycle);
        }

private float ResolveMoveSpeedMultiplier()
        {
            // [멤] 공격/채집, 캡슐 던지기, 취식 등 행동 중 감속(PlayerActionSlotCoordinator)이
            // 걸려 있으면 음식효과 배율은 무시하고 그 감속 값을 그대로 사용한다(곱연산이 아닌
            // 덮어쓰기). 행동이 끝나 오버라이드가 사라지면 다시 음식효과 배율을 사용한다.
            if (moveSpeedOverrideOwners.Count > 0)
            {
                float lowest = 1f;
                foreach (float value in moveSpeedOverrideOwners.Values)
                {
                    if (value < lowest) lowest = value;
                }

                return lowest;
            }

            if (foodEffects == null)
            {
                foodEffects = stats != null ? stats.FoodEffects : GetComponent<KMSFoodEffectController>();
            }

            return foodEffects != null ? foodEffects.MoveSpeedMultiplier : 1f;
        }

        private void SyncStepHeight()
        {
            if (characterController == null) return;

            float controllerHeight = Mathf.Max(0f, characterController.height);
            characterController.stepOffset = Mathf.Clamp(maxStepHeight, 0f, controllerHeight);
        }

        private Transform FindStepVisualRoot()
        {
            Transform namedVisual = transform.Find("PlayerVisual_Dodo");
            if (namedVisual != null) return namedVisual;

            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null) return null;

            Transform candidate = animator.transform;
            while (candidate.parent != null && candidate.parent != transform)
            {
                candidate = candidate.parent;
            }

            return candidate != transform ? candidate : animator.transform;
        }

        private void InitializeStepVisual()
        {
            if (stepVisualRoot == null || stepVisualRoot == transform) return;

            stepVisualBaseLocalPosition = stepVisualRoot.localPosition;
            stepVisualOffset = 0f;
            stepVisualOffsetVelocity = 0f;
            isStepVisualInitialized = true;
        }

        private void AddStepVisualOffset(float offset)
        {
            if (!isStepVisualInitialized || stepVisualRoot == null) return;

            stepVisualOffset = Mathf.Clamp(
                stepVisualOffset + offset,
                -maxStepHeight,
                maxStepHeight);
            ApplyStepVisualOffset();
        }

        private void ApplyStepVisualOffset()
        {
            if (!isStepVisualInitialized || stepVisualRoot == null) return;

            stepVisualRoot.localPosition =
                stepVisualBaseLocalPosition + Vector3.up * stepVisualOffset;
        }

        private void ResetStepVisual()
        {
            if (isStepVisualInitialized && stepVisualRoot != null)
            {
                stepVisualRoot.localPosition = stepVisualBaseLocalPosition;
            }

            stepVisualOffset = 0f;
            stepVisualOffsetVelocity = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsGrounded ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * groundedOffset, groundedRadius);

            if (characterController != null)
            {
                Vector3 center = transform.TransformPoint(characterController.center);
                float bottom = center.y - characterController.height * 0.5f;
                Gizmos.color = new Color(0.1f, 0.75f, 1f, 1f);
                Gizmos.DrawWireCube(
                    new Vector3(center.x, bottom + maxStepHeight * 0.5f, center.z),
                    new Vector3(
                        characterController.radius * 2f,
                        Mathf.Max(0.01f, maxStepHeight),
                        characterController.radius * 2f));
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            LadderVolume ladder = other.GetComponentInParent<LadderVolume>();
            if (ladder != null)
            {
                candidateLadder = ladder;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            LadderVolume ladder = other.GetComponentInParent<LadderVolume>();
            if (ladder != null)
            {
                candidateLadder = ladder;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            LadderVolume ladder = other.GetComponentInParent<LadderVolume>();
            if (ladder == null || ladder != candidateLadder) return;

            candidateLadder = null;
        }
    }
}
