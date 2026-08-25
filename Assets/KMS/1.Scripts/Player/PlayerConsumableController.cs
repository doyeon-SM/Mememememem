using HDY.Item;
using KMS.InventoryDuped;
using UnityEngine;

namespace KMS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput), typeof(PlayerStats), typeof(PlayerInventory))]
    public sealed class PlayerConsumableController : MonoBehaviour
    {
        private const float MinimumEffectiveSatiety = 0.001f;

        [Header("References")]
        [SerializeField] private PlayerInput input;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerStats stats;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private PlayerActionSlotCoordinator actionCoordinator;
        [SerializeField] private Animator animator;
        [SerializeField] private ItemCatalogManager catalogManager;

        [Header("Animation")]
        [SerializeField, Min(0.1f)] private float stateEntryTimeout = 0.75f;

        public bool IsConsuming => actionRequested || actionStateActive || HasPendingConsume;

        private static readonly int LocomotionStateHash = Animator.StringToHash("Locomotion");
        private static readonly int EatHash = Animator.StringToHash("Eat");

        private bool actionRequested;
        private bool actionStateActive;
        private bool consumeCommitted;
        private float requestTime;
        private string pendingItemId;
        private float pendingSatietyAmount;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (input != null)
            {
                input.PrimaryActionPressed += TryConsumeSelectedFood;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.PrimaryActionPressed -= TryConsumeSelectedFood;
            }

            CancelPendingConsume();
        }

        private void Update()
        {
            if (!actionRequested || actionStateActive) return;
            if (Time.unscaledTime - requestTime <= stateEntryTimeout) return;

            CancelPendingConsume();
        }

private void ResolveReferences()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (actionCoordinator == null) actionCoordinator = GetComponent<PlayerActionSlotCoordinator>();
            if (movement != null && movement.Animator != null) animator = movement.Animator;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);

            catalogManager = ItemCatalogManager.Resolve(catalogManager);
        }

        private void TryConsumeSelectedFood()
        {
            if (IsConsuming || inventory == null || stats == null || animator == null)
            {
                return;
            }

            if (catalogManager == null)
            {
                catalogManager = ItemCatalogManager.Resolve(catalogManager);
            }

            ItemStack selectedStack = inventory.GetSelectedQuickSlot();
            if (selectedStack == null || selectedStack.IsEmpty || catalogManager == null)
            {
                return;
            }

            ItemData selectedItem = catalogManager.FindItemData(selectedStack.itemId);
            if (selectedItem == null
                || selectedItem.Category != ItemCategory.Food
                || selectedItem.UseAction != UseAction.Eat)
            {
                return;
            }

            float satietyAmount = GetTotalSatiety(selectedItem);
            if (satietyAmount <= MinimumEffectiveSatiety
                || !stats.IsAlive
                || !stats.CanApplyFood(selectedItem, satietyAmount))
            {
                return;
            }

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0) || currentState.shortNameHash != LocomotionStateHash)
            {
                return;
            }

            if (actionCoordinator != null && !actionCoordinator.CanBeginAction(this, ActionInputSlot.Primary))
            {
                return;
            }

            if (!inventory.BeginQuickSlotUse())
            {
                return;
            }

            pendingItemId = selectedStack.itemId;
            pendingSatietyAmount = satietyAmount;
            consumeCommitted = false;
            actionRequested = true;
            actionStateActive = false;
            requestTime = Time.unscaledTime;

            if (actionCoordinator != null)
            {
                actionCoordinator.TryBeginAction(this, ActionInputSlot.Primary, ActionSpeedTier.Heavy);
            }
            else
            {
                movement?.SetMoveSpeedOverride(this, 0.4f);
            }

            animator.ResetTrigger(EatHash);
            animator.SetTrigger(EatHash);
        }

        public void NotifyConsumeActionEntered()
        {
            if (!HasPendingConsume) return;

            actionRequested = false;
            actionStateActive = true;
        }

        public void NotifyConsumeActionCompleted()
        {
            if (!HasPendingConsume || consumeCommitted) return;

            if (catalogManager == null)
            {
                catalogManager = ItemCatalogManager.Resolve(catalogManager);
            }

            ItemData consumedItem = catalogManager != null
                ? catalogManager.FindItemData(pendingItemId)
                : null;

            if (inventory == null
                || stats == null
                || consumedItem == null
                || !stats.IsAlive
                || pendingSatietyAmount <= MinimumEffectiveSatiety
                || !stats.CanApplyFood(consumedItem, pendingSatietyAmount)
                || inventory.GetQuickSlotUseItemId() != pendingItemId)
            {
                CancelPendingConsume();
                return;
            }

            if (!inventory.TryReserveQuickSlotItem(1)
                || !inventory.CommitQuickSlotUse())
            {
                CancelPendingConsume();
                return;
            }

            consumeCommitted = true;
            stats.ApplyFood(consumedItem, pendingSatietyAmount);
            FinishPendingConsume();
        }

        public void NotifyConsumeActionExited()
        {
            if (!HasPendingConsume) return;

            if (!consumeCommitted)
            {
                CancelPendingConsume();
            }
        }

        public void CancelPendingConsume()
        {
            if (animator != null)
            {
                animator.ResetTrigger(EatHash);
            }

            if (inventory != null && HasPendingConsume)
            {
                inventory.EndQuickSlotUse();
            }

            ClearPendingState();
        }

        private void FinishPendingConsume()
        {
            if (inventory != null)
            {
                inventory.EndQuickSlotUse();
            }

            ClearPendingState();
        }

        private void ClearPendingState()
        {
            if (actionCoordinator != null)
            {
                actionCoordinator.EndAction(this);
            }
            else
            {
                movement?.SetMoveSpeedOverride(this, null);
            }
            actionRequested = false;
            actionStateActive = false;
            consumeCommitted = false;
            pendingItemId = null;
            pendingSatietyAmount = 0f;
        }

        private bool HasPendingConsume => !string.IsNullOrEmpty(pendingItemId);

        private static float GetTotalSatiety(ItemData item)
        {
            if (item == null || item.EatEffects == null)
            {
                return 0f;
            }

            float total = 0f;

            for (int i = 0; i < item.EatEffects.Count; i++)
            {
                ItemEffect effect = item.EatEffects[i];
                if (effect == null || effect.Effect != EffectType.Satiety || effect.Value <= 0f)
                {
                    continue;
                }

                total += effect.Value;
            }

            return total;
        }

        private void OnValidate()
        {
            stateEntryTimeout = Mathf.Max(0.1f, stateEntryTimeout);
        }
    }
}
