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
        [SerializeField] private ItemCatalogManager catalogManager;

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
        }

        private void ResolveReferences()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (stats == null) stats = GetComponent<PlayerStats>();

            catalogManager = ItemCatalogManager.Resolve(catalogManager);
        }

        private void TryConsumeSelectedFood()
        {
            if (inventory == null || stats == null)
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
                || stats.CurrentHunger >= stats.MaxHunger - MinimumEffectiveSatiety)
            {
                return;
            }

            if (!inventory.BeginQuickSlotUse())
            {
                return;
            }

            try
            {
                if (!inventory.TryReserveQuickSlotItem(1))
                {
                    return;
                }

                /*
                 * Future eating-animation integration point:
                 *
                 * 1. Cache selectedItem and satietyAmount as the pending food use.
                 * 2. Trigger the eating animation instead of applying the effect immediately.
                 * 3. Call ApplySatietyAndCommit(...) from an Animation Event at the bite frame.
                 * 4. If the animation is interrupted, call RollbackQuickSlotUse() and EndQuickSlotUse().
                 *
                 * animator.SetTrigger(EatHash);
                 * return;
                 */

                if (!inventory.CommitQuickSlotUse())
                {
                    return;
                }

                stats.RestoreHunger(satietyAmount);
            }
            finally
            {
                inventory.EndQuickSlotUse();
            }
        }

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
    }
}
