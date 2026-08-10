using HDY.Item;
using KGH.Data;
using KMS.Audio;
using KMS.Effects;
using UnityEngine;
using UnityEngine.Serialization;

using HdyItemCategory = HDY.Item.ItemCategory;
using KmsItemStack = KMS.InventoryDuped.ItemStack;
using KmsPlayerInventory = KMS.InventoryDuped.PlayerInventory;

namespace KMS.Harvesting
{
    [DisallowMultipleComponent]
    public class PlayerHarvestController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private KMS.PlayerInput input;
        [SerializeField] private KMS.PlayerMovement movement;
        [SerializeField] private KmsPlayerInventory inventory;
        [SerializeField] private KMS.PlayerStats playerStats;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Animator animator;
        [SerializeField] private KMS.PlayerToolAnimationController toolAnimationController;

        // [HDY 요청] 선택된 퀵슬롯(ItemStack)에는 itemId만 있으므로, 실제 ItemData(Category/Value/ObjectType 등)를
        // 조회하기 위한 참조.
        [Header("아이템 카탈로그 (Item_ID로 조회할 때 사용)")]
        [SerializeField] private ItemCatalogManager catalogManager;

        [Header("Harvest")]
        [SerializeField] private LayerMask harvestLayer = ~0;
        [SerializeField] private float harvestDistance = 3f;
        [Tooltip("도구 타격 SphereCast의 반경입니다. 값이 클수록 조준 판정이 여유로워집니다.")]
        [SerializeField, Min(0.01f)] private float harvestRadius = 0.45f;
        [SerializeField] private int fallbackToolDamage = 1;

        [Header("Tool Timing")]
        [FormerlySerializedAs("toolUseCooldown")]
        [Tooltip("기본 도구 사용 간격입니다. 도구 애니메이션 한 사이클 길이로도 사용됩니다.")]
        [SerializeField, Min(0.1f)] private float baseToolUseCooldown = 1f;
        [Tooltip("효과 적용 후 허용할 최소 도구 사용 간격입니다.")]
        [SerializeField, Min(0.05f)] private float minimumToolUseCooldown = 0.1f;

        [Header("Mem Melee")]
        [SerializeField] private string[] memMeleeItemIds =
        {
            "tool_shabby_club",
            "tool_club",
            "tool_decent_club"
        };
        [SerializeField] private LayerMask memMeleeLayer = 1 << 10;
        [SerializeField] private LayerMask memMeleeObstructionLayer = ~0;
        [SerializeField, Min(0.1f)] private float memMeleeDistance = 5f;
        [SerializeField, Range(1f, 90f)] private float memMeleeHalfAngle = 55f;
        [SerializeField, Min(0f)] private float memMeleeOriginHeight = 1f;
        [SerializeField, Min(0f)] private float memMeleeVerticalTolerance = 1.5f;
        [SerializeField, Min(0f)] private float memMeleeHungerCost = 1f;
        [SerializeField] private KMSMemHitDustPool memHitDustPool;

        [Header("Debug")]
        [Tooltip("플레이 중 SphereCast 중심선을 Scene 뷰에 표시합니다.")]
        [SerializeField] private bool drawDebugRay = true;
        [Tooltip("플레이어를 선택했을 때 SphereCast의 시작·끝 구체와 판정 폭을 표시합니다.")]
        [SerializeField] private bool drawSphereCastGizmo = true;
        [Tooltip("플레이어를 선택했을 때 멤 근접 공격의 전방 부채꼴을 표시합니다.")]
        [SerializeField] private bool drawMemMeleeGizmo = true;
        [SerializeField] private Color debugMissColor = Color.red;
        [SerializeField] private Color debugHitColor = Color.green;
        [SerializeField] private Color sphereCastGizmoColor = new Color(1f, 0.72f, 0.1f, 0.85f);
        [SerializeField] private Color memMeleeGizmoColor = new Color(0.2f, 0.85f, 1f, 0.85f);
        [SerializeField] private bool logHitTarget = true;

        private const int MaxHarvestHits = 32;
        private const int MaxMemMeleeHits = 32;
        private const int MeleeGizmoSegments = 24;

        private float cooldownTimer;
        private bool isPrimaryActionHeld;
        private ItemData pendingToolItem;
        private bool hasPendingToolImpact;
        // [HDY 요청 - KMS 크로스 승인 - 내구도] UseTool() 시점에 사용 중이던 퀵슬롯 인덱스를 함께 기억해둔다.
        // ResolvePendingToolImpact()는 Animation Event로 한 박자 늦게 호출되므로, 그 사이 선택된 퀵슬롯이
        // 바뀌었을 가능성을 대비해 durability 감소 시 실제로 그 인덱스의 아이템인지(itemId 일치) 다시 확인한다.
        private int pendingToolSlotIndex = -1;
        private readonly RaycastHit[] harvestHits = new RaycastHit[MaxHarvestHits];
        private readonly Collider[] memMeleeHits = new Collider[MaxMemMeleeHits];
        private readonly RaycastHit[] memMeleeObstructionHits = new RaycastHit[MaxHarvestHits];
        private static readonly int SlashHash = Animator.StringToHash("Slash");

        public float BaseToolUseCooldown => baseToolUseCooldown;

        /// <summary>현재 쿨다운 배율까지 반영한 실제 도구 사용 간격. 별도 private 헬퍼로 분리하지 않고
        /// 이 프로퍼티 안에서 직접 계산한다(중복 메서드처럼 보이는 것을 피하기 위함).</summary>
        public float EffectiveToolUseCooldown
        {
            get
            {
                float cooldownMultiplier = Mathf.Max(0f, ResolveToolCooldownMultiplier());
                return Mathf.Max(minimumToolUseCooldown, baseToolUseCooldown * cooldownMultiplier);
            }
        }

        private void Reset()
        {
            input = GetComponent<KMS.PlayerInput>();
            movement = GetComponent<KMS.PlayerMovement>();
            inventory = GetComponent<KmsPlayerInventory>();
            playerStats = GetComponent<KMS.PlayerStats>();
            memHitDustPool = GetComponent<KMSMemHitDustPool>();
            toolAnimationController = GetComponent<KMS.PlayerToolAnimationController>();

            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Awake()
        {
            if (input == null) input = GetComponent<KMS.PlayerInput>();
            if (movement == null) movement = GetComponent<KMS.PlayerMovement>();
            if (inventory == null) inventory = GetComponent<KmsPlayerInventory>();
            if (playerStats == null) playerStats = GetComponent<KMS.PlayerStats>();
            if (memHitDustPool == null) memHitDustPool = GetComponent<KMSMemHitDustPool>();
            if (toolAnimationController == null)
            {
                toolAnimationController = GetComponent<KMS.PlayerToolAnimationController>();
            }
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (movement != null && movement.Animator != null) animator = movement.Animator;

            catalogManager = ItemCatalogManager.Resolve(catalogManager);
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.PrimaryActionPressed += HandlePrimaryActionPressed;
                input.PrimaryActionReleased += HandlePrimaryActionReleased;
            }

            if (toolAnimationController != null)
            {
                toolAnimationController.ToolActionEnded += HandleToolActionEnded;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.PrimaryActionPressed -= HandlePrimaryActionPressed;
                input.PrimaryActionReleased -= HandlePrimaryActionReleased;
            }

            if (toolAnimationController != null)
            {
                toolAnimationController.ToolActionEnded -= HandleToolActionEnded;
            }

            isPrimaryActionHeld = false;
            ClearPendingToolImpact();
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.deltaTime);
            }

            if (!isPrimaryActionHeld) return;

            if (!CanContinueHeldToolUse())
            {
                isPrimaryActionHeld = false;
                return;
            }

            if (cooldownTimer <= 0f && TryGetSelectedTool(out ItemData selectedTool))
            {
                UseTool(selectedTool);
            }
        }

        private void HandlePrimaryActionPressed()
        {
            if (!TryGetSelectedTool(out ItemData selectedTool)) return;

            isPrimaryActionHeld = true;

            if (cooldownTimer <= 0f)
            {
                UseTool(selectedTool);
            }
        }

        private void HandlePrimaryActionReleased()
        {
            isPrimaryActionHeld = false;
        }

        private bool CanContinueHeldToolUse()
        {
            if (input == null || !input.isActiveAndEnabled) return false;
            if (input.IsGameplayInputBlocked || input.IsCursorReleased) return false;

            return TryGetSelectedTool(out _);
        }

        private bool TryGetSelectedTool(out ItemData selectedItem)
        {
            selectedItem = null;

            if (inventory == null) return false;

            if (catalogManager == null)
            {
                catalogManager = ItemCatalogManager.Resolve(catalogManager);
            }

            KmsItemStack selectedSlot = inventory.GetSelectedQuickSlot();
            if (selectedSlot == null || selectedSlot.IsEmpty || catalogManager == null) return false;

            // [HDY 요청] 슬롯에는 itemId(string)만 있으므로 카탈로그에서 실제 ItemData를 조회한다.
            selectedItem = catalogManager.FindItemData(selectedSlot.itemId);
            return selectedItem != null && selectedItem.Category == HdyItemCategory.Tool;
        }

        private void UseTool(ItemData selectedItem)
        {
            if (selectedItem == null || cameraTransform == null || cooldownTimer > 0f) return;

            float effectiveToolUseCooldown = EffectiveToolUseCooldown;

            if (toolAnimationController != null)
            {
                if (!toolAnimationController.TryPlay(selectedItem, effectiveToolUseCooldown)) return;
            }
            else if (animator != null)
            {
                // Older KMS player prefabs keep the previous motion until the migration tool is applied.
                animator.SetTrigger(SlashHash);
            }

            pendingToolItem = selectedItem;
            // [HDY 요청 - KMS 크로스 승인 - 내구도] 지금 사용한 도구가 어느 퀵슬롯에서 나온 것인지 기억해둔다.
            pendingToolSlotIndex = inventory != null ? inventory.selectedQuickSlotIndex : -1;
            hasPendingToolImpact = true;
            cooldownTimer = effectiveToolUseCooldown;
        }

        public void ResolvePendingToolImpact()
        {
            if (!hasPendingToolImpact || pendingToolItem == null) return;

            if (playerStats != null && !playerStats.IsAlive)
            {
                ClearPendingToolImpact();
                return;
            }

            // Consume first so duplicate Animation Events can never apply damage twice.
            ItemData selectedItem = pendingToolItem;
            int toolSlotIndex = pendingToolSlotIndex;
            ClearPendingToolImpact();

            if (IsMemMeleeItem(selectedItem.Item_ID))
            {
                ResolveMemMeleeImpact(selectedItem, toolSlotIndex);
                return;
            }

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            bool hasHit = TryGetHarvestHit(ray, selectedItem, out RaycastHit hit);

            if (drawDebugRay)
            {
                Debug.DrawRay(
                    ray.origin,
                    ray.direction * harvestDistance,
                    hasHit ? debugHitColor : debugMissColor,
                    0.5f);
            }

            if (!hasHit)
            {
                KMSAudioService.PlayAt(GameSfxId.ToolSwing, transform.position);
                return;
            }

            if (logHitTarget)
            {
                Debug.Log($"[Harvest] Hit: {hit.collider.name}", hit.collider);
            }

            if (WorldObjectHarvest(hit, selectedItem))
            {
                return;
            }

            KMSAudioService.PlayAt(GameSfxId.ToolSwing, transform.position);

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.IsDead) return;

            int damage = Mathf.Max(1, selectedItem.Value);
            if (damage <= 0) damage = fallbackToolDamage;

            damageable.TakeDamage(damage);
            if (!damageable.IsDead) return;

            HarvestableResource resource = hit.collider.GetComponentInParent<HarvestableResource>();
            if (resource != null)
            {
                resource.TryCollectReward(inventory);
            }
        }

        public void CancelActiveToolUse()
        {
            isPrimaryActionHeld = false;
            ClearPendingToolImpact();
            toolAnimationController?.CancelToolAction();
        }

        private void HandleToolActionEnded()
        {
            // If the state exited before its impact event (hit reaction, death, disable, etc.),
            // the queued action is cancelled without applying late damage or audio.
            ClearPendingToolImpact();
        }

        private void ClearPendingToolImpact()
        {
            pendingToolItem = null;
            pendingToolSlotIndex = -1;
            hasPendingToolImpact = false;
        }

        /// <summary>
        /// 카메라 정면으로 SphereCast를 수행하고 현재 도구로 처리할 수 있는 가장 가까운 대상을 반환합니다.
        /// 비대상 Trigger는 건너뛰지만 고체 장애물과 사용할 수 없는 전방 자원은 뒤쪽 대상을 차단합니다.
        /// </summary>
        private bool TryGetHarvestHit(Ray ray, ItemData selectedItem, out RaycastHit selectedHit)
        {
            float distance = Mathf.Max(0f, harvestDistance);
            float radius = Mathf.Max(0.01f, harvestRadius);
            int hitCount = Physics.SphereCastNonAlloc(
                ray,
                radius,
                harvestHits,
                distance,
                harvestLayer,
                QueryTriggerInteraction.Collide);

            SortHitsByDistance(harvestHits, hitCount);
            selectedHit = default;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = harvestHits[i];

                if (candidate.collider == null || IsPlayerCollider(candidate.collider))
                {
                    continue;
                }

                WorldObject worldObject = candidate.collider.GetComponentInParent<WorldObject>();
                if (worldObject != null)
                {
                    if (worldObject.EvaluateInteraction(selectedItem) == WorldObjectInteractionState.Available)
                    {
                        selectedHit = candidate;
                        ClearHits(harvestHits, hitCount);
                        return true;
                    }

                    ClearHits(harvestHits, hitCount);
                    return false;
                }

                // 멤은 몽둥이 전용 판정에서만 피해를 받으며 앞에 서 있으면 뒤쪽 대상을 가립니다.
                if (candidate.collider.GetComponentInParent<KMS.Combat.KMSMemDamageableAdapter>() != null)
                {
                    ClearHits(harvestHits, hitCount);
                    return false;
                }

                IDamageable damageable = candidate.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    selectedHit = candidate;
                    ClearHits(harvestHits, hitCount);
                    return true;
                }

                if (candidate.collider.isTrigger)
                {
                    continue;
                }

                // 벽, 지형, 장식용 고체 Collider는 기존처럼 뒤쪽 타격을 차단합니다.
                ClearHits(harvestHits, hitCount);
                return false;
            }

            ClearHits(harvestHits, hitCount);
            return false;
        }

        private void ResolveMemMeleeImpact(ItemData selectedItem, int toolSlotIndex)
        {
            if (!TryGetMemMeleeTarget(
                    out KMS.Combat.KMSMemDamageableAdapter memTarget,
                    out Collider targetCollider,
                    out Vector3 hitPoint,
                    out Vector3 hitNormal))
            {
                KMSAudioService.PlayAt(GameSfxId.ToolSwing, transform.position);
                return;
            }

            if (logHitTarget)
            {
                Debug.Log($"[MemMelee] Hit: {targetCollider.name}", targetCollider);
            }

            if (playerStats != null)
            {
                playerStats.ConsumeHunger(memMeleeHungerCost);
            }

            memTarget.TakeDamage(Mathf.Max(1, selectedItem.Value));

            if (selectedItem.MaxDurability > 0 && inventory != null)
            {
                inventory.DamageQuickSlotToolDurability(
                    toolSlotIndex,
                    selectedItem.Item_ID,
                    selectedItem.MaxDurability);
            }

            if (memHitDustPool != null)
            {
                memHitDustPool.Play(hitPoint, hitNormal);
            }

            KMSAudioService.PlayAt(GameSfxId.ClubHitMem, hitPoint);
        }

        private bool TryGetMemMeleeTarget(
            out KMS.Combat.KMSMemDamageableAdapter selectedTarget,
            out Collider selectedCollider,
            out Vector3 selectedPoint,
            out Vector3 selectedNormal)
        {
            Vector3 origin = GetMemMeleeOrigin();
            Vector3 forward = GetMemMeleeForward();
            float distanceLimit = Mathf.Max(0.1f, memMeleeDistance);
            float halfAngle = Mathf.Clamp(memMeleeHalfAngle, 1f, 90f);
            int hitCount = Physics.OverlapSphereNonAlloc(
                origin,
                distanceLimit,
                memMeleeHits,
                memMeleeLayer,
                QueryTriggerInteraction.Collide);

            selectedTarget = null;
            selectedCollider = null;
            selectedPoint = default;
            selectedNormal = default;
            float bestAngle = float.MaxValue;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider candidateCollider = memMeleeHits[i];
                memMeleeHits[i] = null;
                if (candidateCollider == null || IsPlayerCollider(candidateCollider)) continue;

                KMS.Combat.KMSMemDamageableAdapter candidateTarget =
                    candidateCollider.GetComponentInParent<KMS.Combat.KMSMemDamageableAdapter>();
                if (candidateTarget == null || candidateTarget.IsDead) continue;

                Vector3 candidatePoint = candidateCollider.ClosestPoint(origin);
                Vector3 toTarget = candidatePoint - origin;
                if (Mathf.Abs(toTarget.y) > memMeleeVerticalTolerance) continue;

                Vector3 planarToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
                float candidateDistance = toTarget.magnitude;
                float candidateAngle = planarToTarget.sqrMagnitude > 0.0001f
                    ? Vector3.Angle(forward, planarToTarget)
                    : 0f;

                if (candidateDistance > distanceLimit || candidateAngle > halfAngle) continue;
                if (!HasClearMemMeleePath(origin, candidatePoint, candidateTarget)) continue;

                bool isBetterAim = candidateAngle < bestAngle - 0.01f;
                bool isSameAimButCloser = Mathf.Abs(candidateAngle - bestAngle) <= 0.01f
                                          && candidateDistance < bestDistance;
                if (!isBetterAim && !isSameAimButCloser) continue;

                bestAngle = candidateAngle;
                bestDistance = candidateDistance;
                selectedTarget = candidateTarget;
                selectedCollider = candidateCollider;
                selectedPoint = candidatePoint;
                selectedNormal = toTarget.sqrMagnitude > 0.0001f
                    ? -toTarget.normalized
                    : -forward;
            }

            // NonAlloc 버퍼가 가득 찬 경우에도 다음 공격에 이전 Collider가 남지 않도록 정리합니다.
            for (int i = hitCount; i < memMeleeHits.Length; i++)
            {
                memMeleeHits[i] = null;
            }

            return selectedTarget != null;
        }

        private bool HasClearMemMeleePath(
            Vector3 origin,
            Vector3 targetPoint,
            KMS.Combat.KMSMemDamageableAdapter intendedTarget)
        {
            Vector3 toTarget = targetPoint - origin;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f) return true;

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                toTarget / distance,
                memMeleeObstructionHits,
                distance + 0.05f,
                memMeleeObstructionLayer,
                QueryTriggerInteraction.Ignore);

            SortHitsByDistance(memMeleeObstructionHits, hitCount);
            bool hasClearPath = true;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = memMeleeObstructionHits[i].collider;
                if (hitCollider == null || IsPlayerCollider(hitCollider)) continue;

                KMS.Combat.KMSMemDamageableAdapter hitTarget =
                    hitCollider.GetComponentInParent<KMS.Combat.KMSMemDamageableAdapter>();
                hasClearPath = hitTarget == intendedTarget;
                break;
            }

            ClearHits(memMeleeObstructionHits, hitCount);
            return hasClearPath;
        }

        private bool IsMemMeleeItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || memMeleeItemIds == null) return false;

            for (int i = 0; i < memMeleeItemIds.Length; i++)
            {
                if (string.Equals(memMeleeItemIds[i], itemId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private Vector3 GetMemMeleeOrigin()
        {
            return transform.position + Vector3.up * Mathf.Max(0f, memMeleeOriginHeight);
        }

        private Vector3 GetMemMeleeForward()
        {
            Transform facingTransform = animator != null ? animator.transform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(facingTransform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }

            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private static void SortHitsByDistance(RaycastHit[] hits, int hitCount)
        {
            for (int i = 1; i < hitCount; i++)
            {
                RaycastHit value = hits[i];
                int j = i - 1;
                while (j >= 0 && hits[j].distance > value.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }

                hits[j + 1] = value;
            }
        }

        private static void ClearHits(RaycastHit[] hits, int hitCount)
        {
            for (int i = 0; i < hitCount; i++)
            {
                hits[i] = default;
            }
        }

        private bool IsPlayerCollider(Collider candidate)
        {
            return candidate != null
                   && candidate.GetComponentInParent<PlayerHarvestController>() == this;
        }

        private bool WorldObjectHarvest(RaycastHit hitObj, ItemData selectedItem)
        {
            if (hitObj.collider == null) return false;
            WorldObject harvestable = hitObj.collider.GetComponentInParent<WorldObject>();
            if (harvestable == null) return false;

            // Spawn-fading resources keep collision enabled to prevent player overlap,
            // but consume the tool trace without damage, impact audio, or hit feedback.
            if (!harvestable.CanReceiveToolHit)
            {
                return true;
            }

            bool applied = harvestable.ObjectInteract(
                inventory,
                selectedItem,
                hitObj.point,
                transform.position);
            if (applied)
            {
                GameSfxId? impactId = GetHarvestImpactId(harvestable.RequiredToolType);
                if (impactId.HasValue)
                {
                    KMSAudioService.PlayAt(impactId.Value, hitObj.point);
                }
                else
                {
                    KMSAudioService.PlayAt(GameSfxId.ToolSwing, transform.position);
                }
            }
            else
            {
                KMSAudioService.PlayAt(GameSfxId.ToolSwing, transform.position);
            }

            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (drawSphereCastGizmo)
            {
                DrawHarvestGizmo();
            }

            if (drawMemMeleeGizmo)
            {
                DrawMemMeleeGizmo();
            }
        }

        private void DrawHarvestGizmo()
        {
            Transform cam = cameraTransform != null
                ? cameraTransform
                : Camera.main != null
                    ? Camera.main.transform
                    : null;

            if (cam == null)
            {
                return;
            }

            float distance = Mathf.Max(0f, harvestDistance);
            float radius = Mathf.Max(0.01f, harvestRadius);
            Vector3 start = cam.position;
            Vector3 end = start + cam.forward * distance;
            Vector3 rightOffset = cam.right * radius;
            Vector3 upOffset = cam.up * radius;

            Gizmos.color = sphereCastGizmoColor;
            Gizmos.DrawWireSphere(start, radius);
            Gizmos.DrawWireSphere(end, radius);
            Gizmos.DrawLine(start + rightOffset, end + rightOffset);
            Gizmos.DrawLine(start - rightOffset, end - rightOffset);
            Gizmos.DrawLine(start + upOffset, end + upOffset);
            Gizmos.DrawLine(start - upOffset, end - upOffset);
        }

        private void DrawMemMeleeGizmo()
        {
            Vector3 origin = GetMemMeleeOrigin();
            Vector3 forward = GetMemMeleeForward();
            float distance = Mathf.Max(0.1f, memMeleeDistance);
            float halfAngle = Mathf.Clamp(memMeleeHalfAngle, 1f, 90f);

            Gizmos.color = memMeleeGizmoColor;
            Vector3 previousDirection = Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward;
            Gizmos.DrawLine(origin, origin + previousDirection * distance);

            for (int i = 1; i <= MeleeGizmoSegments; i++)
            {
                float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)MeleeGizmoSegments);
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                Gizmos.DrawLine(
                    origin + previousDirection * distance,
                    origin + direction * distance);
                previousDirection = direction;
            }

            Gizmos.DrawLine(origin, origin + previousDirection * distance);
            Gizmos.DrawLine(
                origin + Vector3.down * memMeleeVerticalTolerance,
                origin + Vector3.up * memMeleeVerticalTolerance);
        }

        private void OnValidate()
        {
            harvestDistance = Mathf.Max(0f, harvestDistance);
            harvestRadius = Mathf.Max(0.01f, harvestRadius);
            memMeleeDistance = Mathf.Max(0.1f, memMeleeDistance);
            memMeleeHalfAngle = Mathf.Clamp(memMeleeHalfAngle, 1f, 90f);
            memMeleeOriginHeight = Mathf.Max(0f, memMeleeOriginHeight);
            memMeleeVerticalTolerance = Mathf.Max(0f, memMeleeVerticalTolerance);
            memMeleeHungerCost = Mathf.Max(0f, memMeleeHungerCost);
            minimumToolUseCooldown = Mathf.Max(0.05f, minimumToolUseCooldown);
            baseToolUseCooldown = Mathf.Max(minimumToolUseCooldown, baseToolUseCooldown);
            fallbackToolDamage = Mathf.Max(1, fallbackToolDamage);
        }

        private float ResolveToolCooldownMultiplier()
        {
            // Future KMS food, equipment, or status effects can contribute here.
            return 1f;
        }

        private static GameSfxId? GetHarvestImpactId(ObjectType objectType)
        {
            switch (objectType)
            {
                case ObjectType.Tree:
                    return GameSfxId.AxeHitTree;
                case ObjectType.Stone:
                    return GameSfxId.PickaxeHitStone;
                case ObjectType.Bush:
                    return GameSfxId.HoeHitBush;
                default:
                    return null;
            }
        }
    }
}
