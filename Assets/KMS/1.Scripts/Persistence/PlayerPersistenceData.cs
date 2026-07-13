using System;
using KMS.InventoryDuped;

namespace KMS.Persistence
{
    [Serializable]
    public class InventoryContainerSaveData
    {
        public int width;
        public int height;
        public ItemStack[] slots;

        public static InventoryContainerSaveData Capture(InventoryContainer source)
        {
            if (source == null) return null;

            var data = new InventoryContainerSaveData
            {
                width = source.width,
                height = source.height,
                slots = new ItemStack[source.slots != null ? source.slots.Length : 0]
            };

            for (int i = 0; i < data.slots.Length; i++)
            {
                ItemStack sourceSlot = source.slots[i];
                data.slots[i] = sourceSlot == null
                    ? new ItemStack()
                    : new ItemStack { itemId = sourceSlot.itemId, amount = sourceSlot.amount };
            }

            return data;
        }
    }

    [Serializable]
    public class PlayerInventorySaveData
    {
        public InventoryContainerSaveData inventory;
        public InventoryContainerSaveData quickSlots;
        public int selectedQuickSlotIndex;
    }

    [Serializable]
    public class PlayerStatsSaveData
    {
        public float currentHealth;
        public float currentHunger;
    }

    [Serializable]
    public class PlayerSaveData
    {
        public int version = 1;
        public PlayerInventorySaveData inventory;
        public PlayerStatsSaveData stats;
    }
}
