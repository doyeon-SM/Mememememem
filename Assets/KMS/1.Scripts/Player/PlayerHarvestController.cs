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
        [SerializeField] private string memMeleeItemId = "tool_shabby_club";
        [SerializeField, Min(0.1f)] private float memMeleeDistance = 5f;
        [SerializeField, Min(0f)] private float memMeleeHungerCost = 1f;
        [SerializeField] private KMSMemHitDustPool memHitDustPool;

        [Header("Debug")]
        [Tooltip("플레이 중 SphereCast 중심선을 Scene 뷰에 표시합니다.")]
        [SerializeField] private bool drawDebugRay = true;
        [Tooltip("플레이어를 선택했을 때 SphereCast의 시작·끝 구체와 판정 폭을 표시합니다.")]
        [SerializeField] private bool drawSphereCastGizmo = true;
        [SerializeField] private Color debugMissColor = Color.red;
        [SerializeField] private Color debugHitColor = Color.green;
        [SerializeField] private Color sphereCastGizmoColor = new Color(1f, 0.72f, 0.1f, 0.85f);
        [SerializeField] private bool logHitTarget = true;

        private const int MaxHarvestHits = 32;

        private float cooldownTimer;
        private bool isPrimaryActionHeld;
        private ItemData pendingToolItem;
        private bool hasPendingToolImpact;
        private readonly RaycastHit[] harvestHits = new RaycastHit[MaxHarvestHits];
        private static readonly int SlashHash = Animator.StringToHash("Slash");

        public float BaseToolUseCooldown => baseToolUseCooldown;
        public float EffectiveToolUseCooldown => ResolveEffectiveToolUseCooldown();

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

            float effectiveToolUseCooldown = ResolveEffectiveToolUseCooldown();

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
            ClearPendingToolImpact();
            bool isMemMeleeAttempt = selectedItem.Item_ID == memMeleeItemId;

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            bool hasHit = TryGetClosestSphereCastHit(ray, out RaycastHit hit);

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

            KMS.Combat.KMSMemDamageableAdapter memTarget =
                hit.collider.GetComponentInParent<KMS.Combat.KMSMemDamageableAdapter>();

            if (memTarget != null)
            {
                if (!isMemMeleeAttempt
                    || hit.distance > memMeleeDistance
                    || memTarget.IsDead)
                {
                    KMSAudioService.PlayAt(GameSfxId.ToolSwing, transform.position);
                    return;
                }

                if (playerStats != null)
                {
                    playerStats.ConsumeHunger(memMeleeHungerCost);
                }

                memTarget.TakeDamage(Mathf.Max(1, selectedItem.Value));
                if (memHitDustPool != null)
                {
                    memHitDustPool.Play(hit.point, hit.normal);
                }
                KMSAudioService.PlayAt(GameSfxId.ClubHitMem, hit.point);
                return;
            }

            if(WorldObjectHarvest(hit, selectedItem))
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
            hasPendingToolImpact = false;
        }

        /// <summary>
        /// 카메라 정면으로 SphereCast를 수행하고 플레이어 자신의 콜라이더를 제외한 가장 가까운 충돌을 반환합니다.
        /// 가장 가까운 외부 장애물도 결과에 포함되므로 기존 Raycast와 동일하게 벽 너머 대상을 타격하지 않습니다.
        /// </summary>
        private bool TryGetClosestSphereCastHit(Ray ray, out RaycastHit closestHit)
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

            closestHit = default;
            float closestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = harvestHits[i];
                harvestHits[i] = default;

                if (candidate.collider == null || IsPlayerCollider(candidate.collider))
                {
                    continue;
                }

                if (!found || candidate.distance < closestDistance)
                {
                    closestHit = candidate;
                    closestDistance = candidate.distance;
                    found = true;
                }
            }

            return found;
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
            if (!drawSphereCastGizmo)
            {
                return;
            }

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

        private void OnValidate()
        {
            harvestDistance = Mathf.Max(0f, harvestDistance);
            harvestRadius = Mathf.Max(0.01f, harvestRadius);
            minimumToolUseCooldown = Mathf.Max(0.05f, minimumToolUseCooldown);
            baseToolUseCooldown = Mathf.Max(minimumToolUseCooldown, baseToolUseCooldown);
            fallbackToolDamage = Mathf.Max(1, fallbackToolDamage);
        }

        private float ResolveEffectiveToolUseCooldown()
        {
            float cooldownMultiplier = Mathf.Max(0f, ResolveToolCooldownMultiplier());
            return Mathf.Max(
                minimumToolUseCooldown,
                baseToolUseCooldown * cooldownMultiplier);
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
