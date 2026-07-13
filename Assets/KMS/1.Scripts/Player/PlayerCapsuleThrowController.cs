using KMS.InventoryDuped;
using UnityEngine;

namespace KMS
{
    public class PlayerCapsuleThrowController : MonoBehaviour
    {
        private enum ThrowState { Idle, Preparing, Ready, Throwing }

        [Header("References")]
        [SerializeField] private PlayerInput input;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerHUD hud;
        [SerializeField] private PlayerCameraController cameraController;
        [SerializeField] private CapsuleTrajectoryPreview trajectoryPreview;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform throwOrigin;
        [SerializeField] private GameObject capsulePrefab;

        [Header("Capsule")]
        [SerializeField] private string capsuleItemId = "test_capsule";
        [SerializeField, Min(0.1f)] private float requiredHoldTime = 0.5f;
        [SerializeField, Min(0.1f)] private float throwSpeed = 12f;
        [SerializeField, Min(0f)] private float upwardThrowSpeed = 2.5f;
        [SerializeField, Min(1f)] private float aimDistance = 30f;
        [SerializeField] private LayerMask aimLayers = ~0;
        [SerializeField] private float fallbackReleaseNormalizedTime = 0.2f;
        [SerializeField, Min(0f)] private float aimRotationSpeed = 720f;

        private static readonly int ThrowPrepareHash = Animator.StringToHash("ThrowPrepare");
        private static readonly int ThrowReadyHash = Animator.StringToHash("ThrowReady");
        private static readonly int ThrowGoHash = Animator.StringToHash("ThrowGo");

        private ThrowState state;
        private float holdTime;
        private bool capsuleReleased;
        private bool previousMovementEnabled = true;
        private Vector3 lockedThrowTarget;
        private bool hasLockedThrowTarget;

        private void Reset()
        {
            input = GetComponent<PlayerInput>();
            movement = GetComponent<PlayerMovement>();
            inventory = GetComponent<PlayerInventory>();
            hud = GetComponent<PlayerHUD>();
            animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (hud == null) hud = GetComponent<PlayerHUD>();
            if (cameraController == null) cameraController = GetComponent<PlayerCameraController>();
            if (trajectoryPreview == null) trajectoryPreview = GetComponent<CapsuleTrajectoryPreview>();
            if (movement != null && movement.Animator != null) animator = movement.Animator;
            else if (animator == null) animator = GetComponentInChildren<Animator>();

            if (throwOrigin == null && animator != null && animator.isHuman)
            {
                throwOrigin = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.SecondaryActionPressed += BeginAim;
                input.SecondaryActionReleased += ReleaseAim;
            }

            if (inventory != null)
            {
                inventory.OnQuickSlotSelectionRequested += HandleQuickSlotSelectionRequested;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.SecondaryActionPressed -= BeginAim;
                input.SecondaryActionReleased -= ReleaseAim;
            }

            if (inventory != null)
            {
                inventory.OnQuickSlotSelectionRequested -= HandleQuickSlotSelectionRequested;
            }

            CancelThrow(false);
        }

        private void Update()
        {
            if (state == ThrowState.Preparing || state == ThrowState.Ready)
            {
                RotateTowardsCamera(false);
            }

            if (state == ThrowState.Ready && trajectoryPreview != null)
            {
                Vector3 origin = GetThrowOriginPosition();
                trajectoryPreview.Show(origin, CalculateInitialVelocity(origin, ResolveAimTarget()));
            }

            if (state == ThrowState.Preparing)
            {
                holdTime += Time.deltaTime;
                if (holdTime >= requiredHoldTime)
                {
                    state = ThrowState.Ready;
                    if (animator != null) animator.SetBool(ThrowReadyHash, true);
                    if (hud != null) hud.SetThrowGuideVisible(true);
                }
            }
            else if (state == ThrowState.Throwing && !capsuleReleased && animator != null)
            {
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.IsName("Throw_Go") && info.normalizedTime >= fallbackReleaseNormalizedTime)
                {
                    Debug.LogWarning("[CapsuleThrow] Throw_Go Animation Event가 호출되지 않아 fallback 시점에 캡슐을 발사합니다.");
                    ReleaseCapsuleFromAnimationEvent();
                }
            }
        }

        private void BeginAim()
        {
            if (state != ThrowState.Idle || !HasSelectedCapsule()) return;
            if (inventory == null || !inventory.BeginQuickSlotUse()) return;
            if (!inventory.TryReserveQuickSlotItem(1))
            {
                inventory.EndQuickSlotUse();
                return;
            }

            state = ThrowState.Preparing;
            holdTime = 0f;
            capsuleReleased = false;
            hasLockedThrowTarget = false;
            LockMovement();
            if (cameraController != null) cameraController.SetAimZoom(true);

            if (animator != null)
            {
                animator.ResetTrigger(ThrowGoHash);
                animator.SetBool(ThrowReadyHash, false);
                animator.SetTrigger(ThrowPrepareHash);
            }
        }

        private void ReleaseAim()
        {
            if (state == ThrowState.Preparing)
            {
                CancelThrow(true);
                return;
            }

            if (state != ThrowState.Ready) return;

            state = ThrowState.Throwing;
            RotateTowardsCamera(true);
            lockedThrowTarget = ResolveAimTarget();
            hasLockedThrowTarget = true;
            if (trajectoryPreview != null) trajectoryPreview.Hide();
            if (cameraController != null) cameraController.SetAimZoom(false);
            if (hud != null) hud.SetThrowGuideVisible(false);
            if (animator != null)
            {
                animator.SetBool(ThrowReadyHash, false);
                animator.SetTrigger(ThrowGoHash);
            }
        }

        private void HandleQuickSlotSelectionRequested(int _)
        {
            if (state == ThrowState.Preparing || state == ThrowState.Ready) CancelThrow(true);
        }

        public void ReleaseCapsuleFromAnimationEvent()
        {
            if (state != ThrowState.Throwing || capsuleReleased) return;

            if (capsulePrefab == null)
            {
                Debug.LogError("[CapsuleThrow] 투척할 capsulePrefab이 연결되지 않았습니다.", this);
                CancelThrow(true);
                return;
            }

            Vector3 origin = GetThrowOriginPosition();
            Vector3 direction = ResolveThrowDirection(origin);
            GameObject capsule = Instantiate(capsulePrefab, origin, Quaternion.LookRotation(direction));
            Rigidbody body = capsule.GetComponent<Rigidbody>();

            if (body == null)
            {
                Debug.LogError("[CapsuleThrow] HDY TestCapsule에 Rigidbody가 없습니다.", capsule);
                Destroy(capsule);
                CancelThrow(true);
                return;
            }

            body.isKinematic = false;
            body.linearVelocity = CalculateInitialVelocity(origin, lockedThrowTarget);
            IgnorePlayerCollisions(capsule);
            capsuleReleased = true;
            inventory.CommitQuickSlotUse();
        }

        public void FinishThrowFromAnimationEvent()
        {
            if (state != ThrowState.Throwing) return;
            if (!capsuleReleased) ReleaseCapsuleFromAnimationEvent();

            inventory.EndQuickSlotUse();
            RestoreMovement();
            state = ThrowState.Idle;
            hasLockedThrowTarget = false;
        }

        private bool HasSelectedCapsule()
        {
            ItemStack selected = inventory != null ? inventory.GetSelectedQuickSlot() : null;
            return selected != null && !selected.IsEmpty && selected.itemId == capsuleItemId;
        }

        private Vector3 ResolveThrowDirection(Vector3 origin)
        {
            Vector3 target = hasLockedThrowTarget ? lockedThrowTarget : ResolveAimTarget();
            Vector3 direction = target - origin;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        }

        private Vector3 CalculateInitialVelocity(Vector3 origin, Vector3 target)
        {
            Vector3 direction = target - origin;
            if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
            return direction.normalized * throwSpeed + Vector3.up * upwardThrowSpeed;
        }

        private Vector3 GetThrowOriginPosition()
        {
            return throwOrigin != null
                ? throwOrigin.position
                : transform.position + Vector3.up * 1.25f + transform.forward * 0.45f;
        }

        private Vector3 ResolveAimTarget()
        {
            Camera aimCamera = Camera.main;
            if (aimCamera == null) return transform.position + transform.forward * aimDistance;

            Ray ray = new Ray(aimCamera.transform.position, aimCamera.transform.forward);
            Vector3 target = ray.GetPoint(aimDistance);
            RaycastHit[] hits = Physics.RaycastAll(ray, aimDistance, aimLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;

                target = hits[i].point;
                break;
            }

            return target;
        }

        private void RotateTowardsCamera(bool immediate)
        {
            Camera aimCamera = Camera.main;
            if (aimCamera == null) return;

            Vector3 forward = aimCamera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            transform.rotation = immediate
                ? targetRotation
                : Quaternion.RotateTowards(transform.rotation, targetRotation, aimRotationSpeed * Time.deltaTime);
        }

        private void IgnorePlayerCollisions(GameObject capsule)
        {
            Collider[] capsuleColliders = capsule.GetComponentsInChildren<Collider>(true);
            Collider[] playerColliders = GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < capsuleColliders.Length; i++)
            {
                for (int j = 0; j < playerColliders.Length; j++)
                {
                    Physics.IgnoreCollision(capsuleColliders[i], playerColliders[j], true);
                }
            }
        }

        private void LockMovement()
        {
            if (movement == null) return;
            previousMovementEnabled = movement.IsMovementEnabled;
            movement.IsMovementEnabled = false;
        }

        private void RestoreMovement()
        {
            if (movement != null) movement.IsMovementEnabled = previousMovementEnabled;
        }

        private void CancelThrow(bool blendToLocomotion)
        {
            if (state == ThrowState.Idle) return;

            if (inventory != null)
            {
                inventory.RollbackQuickSlotUse();
                inventory.EndQuickSlotUse();
            }

            if (hud != null) hud.SetThrowGuideVisible(false);
            if (trajectoryPreview != null) trajectoryPreview.Hide();
            if (cameraController != null) cameraController.SetAimZoom(false);
            if (animator != null)
            {
                animator.SetBool(ThrowReadyHash, false);
                animator.ResetTrigger(ThrowPrepareHash);
                animator.ResetTrigger(ThrowGoHash);
                if (blendToLocomotion) animator.CrossFade("Locomotion", 0.15f, 0);
            }

            RestoreMovement();
            state = ThrowState.Idle;
            holdTime = 0f;
            capsuleReleased = false;
            hasLockedThrowTarget = false;
        }
    }
}
