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
                // [HDY 요청 - KMS 크로스 승인 - 내구도] durability도 함께 저장한다.
                data.slots[i] = sourceSlot == null
                    ? new ItemStack()
                    : new ItemStack { itemId = sourceSlot.itemId, amount = sourceSlot.amount, durability = sourceSlot.durability };
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
    public class KMSFoodEffectValueSaveData
    {
        public int effectType;
        public float value;
    }

    [Serializable]
    public class KMSFoodEffectSegmentSaveData
    {
        public string itemId;
        public float remainingSatiety;
        public KMSFoodEffectValueSaveData[] effects;
    }

    [Serializable]
    public class KMSFoodEffectStateSaveData
    {
        public int layoutVersion;
        public float normalSatiety;
        public KMSFoodEffectSegmentSaveData[] segments;
    }

    [Serializable]
    public class PlayerStatsSaveData
    {
        public float currentHealth;
        public float currentHunger;
        public KMSFoodEffectStateSaveData foodEffects;
    }

    /// <summary>
    /// [멤] 캐릭터 스탯(힘/지능/민첩/행운/의지) + 투자 포인트 저장 데이터.
    /// claude/character-stat-system-plan.md 확정 공식 기준 - PlayerCombatStats.CaptureSaveData/RestoreSaveData 참고.
    /// </summary>
    [Serializable]
    public class PlayerCombatStatsSaveData
    {
        public int strength;
        public int intelligence;
        public int agility;
        public int luck;
        public int willpower;
        public int unspentPoints;
        public int lastKnownTerritoryLevel = 1;
    }


    [Serializable]
    public class PlayerSaveData
    {
        public int version = 4;
        public PlayerInventorySaveData inventory;
        public PlayerStatsSaveData stats;
        public PlayerCombatStatsSaveData combatStats;
    }
}
