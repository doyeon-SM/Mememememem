using HDY.Inventory;
using UnityEngine;

namespace KMS.InventoryDuped
{
    /// <summary>TestScene_KMS-only supply injector for exercising all inventory upgrades.</summary>
    [DisallowMultipleComponent]
    public sealed class KMSInventoryUpgradeTestSupplies : MonoBehaviour
    {
        [SerializeField] private string itemId = "item_iron";
        [SerializeField, Min(0)] private int targetAmount = 100;

        private void Start()
        {
            PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
            if (inventory == null)
            {
                Debug.LogWarning("[KMSInventoryUpgradeTestSupplies] PlayerInventory is missing.", this);
                return;
            }

            int inventoryAmount = 0;
            for (int i = 0; i < inventory.UnlockedInventorySlotCount; i++)
            {
                if (inventory.TryGetSlotSnapshot(SlotGroup.Inventory, i, out ItemStack snapshot)
                    && snapshot != null
                    && snapshot.itemId == itemId)
                {
                    inventoryAmount += snapshot.amount;
                }
            }

            int missing = Mathf.Max(0, targetAmount - inventoryAmount);
            if (missing <= 0) return;

            int remaining = missing;
            for (int i = 0; i < inventory.UnlockedInventorySlotCount && remaining > 0; i++)
            {
                inventory.TryGetSlotSnapshot(SlotGroup.Inventory, i, out ItemStack snapshot);
                if (snapshot != null && !snapshot.IsEmpty && snapshot.itemId != itemId) continue;

                ItemStack supply = new ItemStack { itemId = itemId, amount = Mathf.Min(20, remaining) };
                int before = supply.amount;
                if (inventory.TryPlaceHeldStack(SlotGroup.Inventory, i, supply))
                    remaining -= before - supply.amount;
            }

            Debug.Log($"[KMSInventoryUpgradeTestSupplies] Added {missing - remaining} {itemId} to inventory for upgrade testing.", this);
        }
    }
}
