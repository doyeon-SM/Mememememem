using System.Collections;
using HDY;
using HDY.Forge;
using KMS.InventoryDuped;
using UnityEngine;

namespace KMS.Testing
{
    [DisallowMultipleComponent]
    public sealed class KMSForgeTestInventorySeeder : MonoBehaviour
    {
        [SerializeField] private string baseItemId = "tool_shabby_axe";
        [SerializeField, Range(1, 10)] private int enhanceLevel = 5;
        [SerializeField, Min(0f)] private float damageIncrease = 2f;
        [SerializeField, Min(0f)] private float gatherIncrease = 2f;

        private IEnumerator Start()
        {
            // 저장 데이터와 플레이어 인벤토리의 Start 초기화가 끝난 다음 테스트 아이템을 추가한다.
            yield return null;
            GrantEnhancedAxeIfMissing();
        }

        public void Configure(string itemId, int level)
        {
            baseItemId = itemId;
            enhanceLevel = Mathf.Clamp(level, 1, 10);
        }

        private void GrantEnhancedAxeIfMissing()
        {
            PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
            ForgeInstanceRegistry registry = ForgeInstanceRegistry.Resolve(null);
            if (inventory == null || registry == null || ForgeInstanceItemDataProvider.Instance == null)
            {
                Debug.LogWarning("[KMSForgeTestInventorySeeder] 인벤토리 또는 Forge 런타임을 찾지 못했습니다.", this);
                return;
            }

            if (ContainsMatchingAxe(inventory.inventory, registry)
                || ContainsMatchingAxe(inventory.quickSlots, registry))
            {
                return;
            }

            ForgeInstanceData instance = registry.CreateInstance(baseItemId, ForgeToolType.Axe, 1);
            instance.EnhanceLevel = enhanceLevel;
            instance.RefinementSlots = new[]
            {
                new ForgeRefinementSlotData(
                    CommonClass.Rare,
                    "DamageIncrease",
                    "데미지",
                    damageIncrease),
                new ForgeRefinementSlotData(
                    CommonClass.Rare,
                    "GatherIncrease",
                    "채집량",
                    gatherIncrease)
            };
            string compositeId = instance.BuildCompositeId();

            int remaining = inventory.AddItem(compositeId, 1);
            if (remaining > 0)
            {
                registry.RemoveInstance(instance.InstanceId);
                Debug.LogWarning("[KMSForgeTestInventorySeeder] 인벤토리 공간 부족으로 강화 도끼를 지급하지 못했습니다.", this);
                return;
            }

            ForgeManager.Instance?.NotifyForgeDataChanged();
            Debug.Log(
                $"[KMSForgeTestInventorySeeder] 테스트 아이템 지급: {baseItemId} +{enhanceLevel}, " +
                $"DamageIncrease +{damageIncrease:0.#}, GatherIncrease +{gatherIncrease:0.#}",
                this);
        }

        private bool ContainsMatchingAxe(InventoryContainer container, ForgeInstanceRegistry registry)
        {
            if (container?.slots == null) return false;

            foreach (ItemStack stack in container.slots)
            {
                if (stack == null || stack.IsEmpty
                    || !ForgeInstanceRegistry.TryParseCompositeId(stack.itemId, out _, out string instanceId))
                {
                    continue;
                }

                ForgeInstanceData instance = registry.GetInstance(instanceId);
                if (instance != null
                    && instance.BaseItemId == baseItemId
                    && instance.EnhanceLevel == enhanceLevel
                    && HasExpectedRefinement(instance))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasExpectedRefinement(ForgeInstanceData instance)
        {
            if (instance?.RefinementSlots == null) return false;

            float actualDamageIncrease = 0f;
            float actualGatherIncrease = 0f;
            foreach (ForgeRefinementSlotData slot in instance.RefinementSlots)
            {
                if (slot == null) continue;

                if (slot.OptionType == "DamageIncrease") actualDamageIncrease += slot.Value;
                if (slot.OptionType == "GatherIncrease") actualGatherIncrease += slot.Value;
            }

            return Mathf.Approximately(actualDamageIncrease, damageIncrease)
                && Mathf.Approximately(actualGatherIncrease, gatherIncrease);
        }
    }
}
