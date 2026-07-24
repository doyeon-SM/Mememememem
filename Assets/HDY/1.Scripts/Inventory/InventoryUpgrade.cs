using System;
using System.Collections.Generic;
using UnityEngine;
using HDY.Upgrade;
using HDY.Recipe;
using KMS.InventoryDuped;

namespace HDY.Inventory
{
    /// <summary>
    /// 인벤토리 칸(5칸씩) 확장을 공용 업그레이드 팝업(UpgradePopupUI)에 연결하는 어댑터.
    /// 실제 칸 언락은 PlayerInventory가 담당하고(이미 잠금/언락 칸수 개념을 갖고 있음), 이 클래스는
    /// IUpgradable을 구현해서 "이번 업그레이드에 얼마가 드는지 / 더 업그레이드할 수 있는지"만 계산한다.
    /// WarehouseUpgrade와 완전히 동일한 패턴이다.
    ///
    /// [비용 표] upgradeSteps의 각 원소는 "(현재 언락 칸수 - 시작 칸수) / 칸당 언락 수" 번째 업그레이드에
    /// 필요한 골드+재료다. 재료 요구치는 RecipeRequsetItemData(HDY.Recipe.Recipe_Requset_Item_Data)를
    /// 그대로 재사용해서 Item_ID/Amount 쌍을 새로 정의하지 않았다 - 공용 팝업이 쓰는 UpgradeMaterialCost로
    /// 변환만 해서 넘긴다. 값은 기획 확정 전까지 인스펙터에서 직접 입력한다(창고/멤창고 업그레이드와 동일).
    /// </summary>
    public class InventoryUpgrade : MonoBehaviour, IUpgradable
    {
        /// <summary>업그레이드 한 단계에 필요한 골드 + 재료(RecipeRequsetItemData 재사용).</summary>
        [Serializable]
        public class InventoryUpgradeStep
        {
            public int GoldCost;
            public List<Recipe_Requset_Item_Data> MaterialCosts = new List<Recipe_Requset_Item_Data>();
        }

        [Header("데이터 참조")]
        [SerializeField] private PlayerInventory playerInventory;

        [Header("단계별 필요 골드+재료 (시작 칸수 -> 최대 칸수까지, 순서대로 입력)")]
        [SerializeField] private List<InventoryUpgradeStep> upgradeSteps = new List<InventoryUpgradeStep>();

        private void Awake()
        {
            if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();

            if (playerInventory == null) Debug.LogWarning("[InventoryUpgrade] playerInventory가 비어있습니다.", this);
        }

        /// <summary>현재 언락 칸수 기준으로 몇 번째 업그레이드 단계인지(0부터 시작). 이미 최대치면 upgradeSteps 범위를 벗어난다.</summary>
        private int GetCurrentStepIndex()
        {
            if (playerInventory == null) return -1;

            int step = playerInventory.SlotsPerInventoryUpgrade;
            if (step <= 0) return -1;

            int progressed = playerInventory.UnlockedInventorySlotCount - playerInventory.StartingInventorySlotCount;
            return progressed / step;
        }

        public bool CanUpgrade()
        {
            if (playerInventory == null) return false;
            if (playerInventory.UnlockedInventorySlotCount >= playerInventory.MaxInventorySlotCount) return false;

            int stepIndex = GetCurrentStepIndex();
            return stepIndex >= 0 && stepIndex < upgradeSteps.Count;
        }

        public UpgradeCost GetUpgradeCost()
        {
            int stepIndex = GetCurrentStepIndex();

            if (stepIndex < 0 || stepIndex >= upgradeSteps.Count)
            {
                Debug.LogWarning($"[InventoryUpgrade] 단계({stepIndex})에 해당하는 비용 데이터가 없습니다. upgradeSteps 크기를 확인하세요.", this);
                return UpgradeCost.GoldOnly(0);
            }

            var step = upgradeSteps[stepIndex];
            var cost = new UpgradeCost { GoldCost = step.GoldCost };

            foreach (var material in step.MaterialCosts)
            {
                cost.MaterialCosts.Add(new UpgradeMaterialCost { Item_ID = material.Item_ID, Amount = material.Amount });
            }

            return cost;
        }

        public string GetUpgradeTitle()
        {
            return "인벤토리 확장";
        }

        /// <summary>확인 버튼 라벨에 그대로 들어가는 짧은 문구. 최대치면 "MAX", 아니면 "현재칸수 → 다음칸수".</summary>
        public string GetUpgradeDescription()
        {
            if (playerInventory == null) return string.Empty;

            if (!CanUpgrade())
            {
                return "MAX";
            }

            int next = Mathf.Min(playerInventory.MaxInventorySlotCount, playerInventory.UnlockedInventorySlotCount + playerInventory.SlotsPerInventoryUpgrade);
            return $"{playerInventory.UnlockedInventorySlotCount}칸 → {next}칸";
        }

        /// <summary>UpgradePopupUI가 비용 지불을 마친 뒤 호출한다. 여기서는 순수하게 칸 언락만 담당한다.</summary>
        public void ApplyUpgrade()
        {
            if (playerInventory == null) return;
            playerInventory.UnlockNextInventorySlots();
        }
    }
}
