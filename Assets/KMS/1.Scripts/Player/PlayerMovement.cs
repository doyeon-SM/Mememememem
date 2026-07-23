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
        [SerializeField] private float ladderSlideDownSpeed = 1.2f;
        [SerializeField] private float ladderSnapSpeed = 12f;
        [SerializeField] private float ladderInputThreshold = 0.1f;

        public bool IsMovementEnabled { get; set; } = true;
        public Animator Animator => animator;
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsOnLadder => activeLadder != null;
        public float CurrentSpeed { get; private set; }
        public float VerticalVelocity => verticalVelocity;
        public Vector3 LastMoveDirection { get; private set; } = Vector3.forward;
        public bool IsDead => isDead;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
        private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");

        private float verticalVelocity;
        private float rotationVelocity;
        private float coyoteTimer;
        private float jumpBufferTimer;
        private Vector3 externalVelocity;
        private LadderVolume candidateLadder;
        private LadderVolume activeLadder;
        private bool isDead;

        private void Reset()
        {
            characterController = GetComponent<CharacterController>();
            input = GetComponent<PlayerInput>();
            stats = GetComponent<PlayerStats>();
            rotationTransform = transform;
            animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (input == null) input = GetComponent<PlayerInput>();
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (rotationTransform == null) rotationTransform = transform;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
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

        public void SetPosition(Vector3 position)
        {
            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;
            transform.position = position;
            characterController.enabled = wasEnabled;
        }

        public void SetVerticalVelocity(float velocity)
        {
            verticalVelocity = velocity;
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
        }

        public void SetDead(bool dead)
        {
            isDead = dead;
            if (!dead) return;

            activeLadder = null;
            candidateLadder = null;
            IsSprinting = false;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            ResetMovementForces();
        }

        private void TryEnterLadder()
        {
            if (!IsMovementEnabled || candidateLadder == null || input == null) return;
            if (input.Move.y <= ladderInputThreshold) return;

            activeLadder = candidateLadder;
            verticalVelocity = 0f;
            externalVelocity = Vector3.zero;
            CurrentSpeed = 0f;
            IsSprinting = false;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;

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
            verticalVelocity = groundedStickForce;
            externalVelocity = Vector3.zero;
            CurrentSpeed = 0f;
            IsSprinting = false;
            SetPosition(exitPosition);
        }

        private void QueueJump()
        {
            jumpBufferTimer = jumpBufferTime;
        }

        private void UpdateGroundedState()
        {
            Vector3 spherePosition = transform.position + Vector3.up * groundedOffset;
            IsGrounded = Physics.CheckSphere(
                spherePosition,
                groundedRadius,
                groundLayers,
                QueryTriggerInteraction.Ignore);

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
        }

        private void HandleMovement()
        {
            Vector2 moveInput = IsMovementEnabled && input != null ? input.Move : Vector2.zero;
            float inputMagnitude = Mathf.Clamp01(moveInput.magnitude);
            bool hasMoveInput = inputMagnitude > 0.1f;

            IsSprinting = hasMoveInput && input != null && input.IsSprinting;

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

            float targetSpeed = hasMoveInput ? (IsSprinting ? sprintSpeed : moveSpeed) * inputMagnitude : 0f;
            float speedRate = targetSpeed > CurrentSpeed ? acceleration : deceleration;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, speedRate * Time.deltaTime);

            Vector3 moveDirection = hasMoveInput ? ResolveMoveDirection(moveInput) : LastMoveDirection;

            if (hasMoveInput)
            {
                LastMoveDirection = moveDirection;

                if (rotateTowardsMovement)
                {
                    RotateTowards(moveDirection);
                }
            }

            verticalVelocity += gravity * Time.deltaTime;
            externalVelocity = Vector3.Lerp(externalVelocity, Vector3.zero, externalForceDecay * Time.deltaTime);

            Vector3 horizontalVelocity = hasMoveInput ? moveDirection * CurrentSpeed : Vector3.zero;
            Vector3 velocity = horizontalVelocity + externalVelocity + Vector3.up * verticalVelocity;

            characterController.Move(velocity * Time.deltaTime);
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

            float normalizedSpeed = Mathf.Approximately(sprintSpeed, 0f) ? 0f : CurrentSpeed / sprintSpeed;
            animator.SetFloat(SpeedHash, CurrentSpeed);
            animator.SetFloat(MotionSpeedHash, Mathf.Clamp01(normalizedSpeed));
            animator.SetBool(GroundedHash, IsGrounded);
            animator.SetBool(JumpHash, !IsGrounded && verticalVelocity > 0f);
            animator.SetBool(FreeFallHash, !IsGrounded && verticalVelocity < 0f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsGrounded ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * groundedOffset, groundedRadius);
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
