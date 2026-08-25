using KMS.Audio;
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
        [SerializeField] private PlayerActionSlotCoordinator actionCoordinator;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerHUD hud;
        [SerializeField] private PlayerCameraController cameraController;
        [SerializeField] private CapsuleTrajectoryPreview trajectoryPreview;
        [SerializeField] private PlayerHeldItemModelController heldItemModel;
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
        [SerializeField] private float aimRotationSpeed = 720f;

        // [HDY 요청 - 이동 잠금 영구 고착 방지] OnCapsuleThrowFinished 애니메이션 이벤트는
        // Throw_Go 클립에 정상적으로 심어져 있지만(KMS_DodoAnimator.controller 확인 완료), 프레임
        // 드랍으로 이벤트 타이밍이 씹히거나, exitTime 기반 전환으로 애니메이터는 이미 Locomotion으로
        // 돌아갔는데 이 C# state만 Throwing에 멈춰있는 경우 등, 이벤트가 어떤 이유로든 오지 않으면
        // RestoreMovement()가 영원히 호출되지 않아 플레이어가 그 자리에서 굳어버리는 문제가 있었다.
        // 캡슐 발사(OnCapsuleRelease) 쪽은 Update()에 애니메이터 상태 기반 폴백이 있었지만, 종료
        // 쪽에는 아무 안전장치가 없었다. throwStateTimer로 Throwing 상태 지속 시간을 재서, 클립
        // 길이보다 충분히 긴 maxThrowDuration을 넘기면 애니메이터 상태와 무관하게 강제로
        // FinishThrowFromAnimationEvent()를 호출해 이동 잠금이 영구히 풀리지 않는 사고를 막는다.
        [SerializeField, Min(0.5f)] private float maxThrowDuration = 2.5f;
        private float throwStateTimer;

        private static readonly int ThrowPrepareHash = Animator.StringToHash("ThrowPrepare");
        private static readonly int ThrowReadyHash = Animator.StringToHash("ThrowReady");
        private static readonly int ThrowGoHash = Animator.StringToHash("ThrowGo");

        private ThrowState state;
        private float holdTime;
        private bool capsuleReleased;
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
            if (actionCoordinator == null) actionCoordinator = GetComponent<PlayerActionSlotCoordinator>();
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (hud == null) hud = GetComponent<PlayerHUD>();
            if (cameraController == null) cameraController = GetComponent<PlayerCameraController>();
            if (trajectoryPreview == null) trajectoryPreview = GetComponent<CapsuleTrajectoryPreview>();
            if (heldItemModel == null) heldItemModel = GetComponent<PlayerHeldItemModelController>();
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
            else if (state == ThrowState.Throwing)
            {
                throwStateTimer += Time.deltaTime;

                if (!capsuleReleased && animator != null)
                {
                    AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                    if (info.IsName("Throw_Go") && info.normalizedTime >= fallbackReleaseNormalizedTime)
                    {
                        Debug.LogWarning("[CapsuleThrow] Throw_Go Animation Event가 호출되지 않아 fallback 시점에 캡슐을 발사합니다.");
                        ReleaseCapsuleFromAnimationEvent();
                    }
                }

                // [HDY 요청 - 이동 잠금 영구 고착 방지] 종료 이벤트가 끝내 오지 않아도 일정 시간 후
                // 강제로 던지기를 종료시켜 RestoreMovement()가 반드시 호출되도록 한다.
                if (throwStateTimer >= maxThrowDuration)
                {
                    Debug.LogWarning("[CapsuleThrow] OnCapsuleThrowFinished Animation Event가 호출되지 않아 안전장치로 던지기를 강제 종료(이동 잠금 해제)합니다.");
                    FinishThrowFromAnimationEvent();
                }
            }
        }

private void BeginAim()
        {
            if (state != ThrowState.Idle || !HasSelectedCapsule()) return;
            // [멤] 공격/채집/취식이 이미 진행 중이면 캡슐 조준을 시작하지 않는다(서로 배타적).
            if (actionCoordinator != null && !actionCoordinator.CanBeginAction(this, ActionInputSlot.Secondary)) return;
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
            throwStateTimer = 0f;
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

            KMSAudioService.PlayAt(GameSfxId.ToolSwing, transform.position);
            body.isKinematic = false;
            body.linearVelocity = CalculateInitialVelocity(origin, lockedThrowTarget);
            IgnorePlayerCollisions(capsule);
            capsuleReleased = true;
            inventory.CommitQuickSlotUse();
            if (heldItemModel != null) heldItemModel.SetThrowVisualSuppressed(true);
        }

        /// <summary>사망 등 외부 상태 전환에서 진행 중인 투척을 안전하게 취소한다.</summary>
        public void CancelActiveThrow()
        {
            CancelThrow(false);
        }

        public void FinishThrowFromAnimationEvent()
        {
            if (state != ThrowState.Throwing) return;
            if (!capsuleReleased) ReleaseCapsuleFromAnimationEvent();

            inventory.EndQuickSlotUse();
            if (heldItemModel != null) heldItemModel.SetThrowVisualSuppressed(false);
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
            if (actionCoordinator != null)
            {
                actionCoordinator.TryBeginAction(this, ActionInputSlot.Secondary, ActionSpeedTier.Heavy);
                return;
            }

            if (movement != null) movement.SetMoveSpeedOverride(this, 0.4f);
        }

        private void RestoreMovement()
        {
            if (actionCoordinator != null)
            {
                actionCoordinator.EndAction(this);
                return;
            }

            if (movement != null) movement.SetMoveSpeedOverride(this, null);
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
            if (heldItemModel != null) heldItemModel.SetThrowVisualSuppressed(false);
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
            throwStateTimer = 0f;
            capsuleReleased = false;
            hasLockedThrowTarget = false;
        }
    }
}
