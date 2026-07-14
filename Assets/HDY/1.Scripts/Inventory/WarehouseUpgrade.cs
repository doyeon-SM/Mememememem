using System;
using System.Collections.Generic;
using UnityEngine;
using HDY.Upgrade;
using HDY.Recipe;

namespace HDY.Inventory
{
    /// <summary>
    /// 창고 한 줄(가로 폭만큼, 10칸) 확장을 공용 업그레이드 팝업(UpgradePopupUI)에 연결하는 어댑터.
    /// 실제 행 추가는 WarehouseInventory가 담당하고(이미 슬롯/크기 개념을 갖고 있음), 이 클래스는
    /// IUpgradable을 구현해서 "이번 업그레이드에 얼마가 드는지 / 더 업그레이드할 수 있는지"만 계산한다.
    ///
    /// [비용 표] upgradeSteps의 각 원소는 "현재 행 수 - 시작 행 수" 번째 업그레이드에 필요한 골드+재료다.
    /// 재료 요구치는 RecipeRequsetItemData(HDY.Recipe.Recipe_Requset_Item_Data)를 그대로 재사용해서
    /// Item_ID/Amount 쌍을 새로 정의하지 않았다 - 공용 팝업이 쓰는 UpgradeMaterialCost로 변환만 해서 넘긴다.
    /// </summary>
    public class WarehouseUpgrade : MonoBehaviour, IUpgradable
    {
        /// <summary>업그레이드 한 단계에 필요한 골드 + 재료(RecipeRequsetItemData 재사용).</summary>
        [Serializable]
        public class WarehouseUpgradeStep
        {
            public int GoldCost;
            public List<Recipe_Requset_Item_Data> MaterialCosts = new List<Recipe_Requset_Item_Data>();
        }

        [Header("데이터 참조")]
        [SerializeField] private WarehouseInventory warehouseInventory;

        [Header("단계별 필요 골드+재료 (시작 행 -> 다음 행, 순서대로 입력)")]
        [SerializeField] private List<WarehouseUpgradeStep> upgradeSteps = new List<WarehouseUpgradeStep>();

        private void Awake()
        {
            if (warehouseInventory == null) warehouseInventory = FindFirstObjectByType<WarehouseInventory>();

            if (warehouseInventory == null) Debug.LogWarning("[WarehouseUpgrade] warehouseInventory가 비어있습니다.", this);
        }

        /// <summary>현재 행 수 기준으로 몇 번째 업그레이드 단계인지(0부터 시작). 이미 최대치면 upgradeSteps 범위를 벗어난다.</summary>
        private int GetCurrentStepIndex()
        {
            if (warehouseInventory == null) return -1;
            return warehouseInventory.RowCount - warehouseInventory.StartingRows;
        }

        public bool CanUpgrade()
        {
            int stepIndex = GetCurrentStepIndex();
            return stepIndex >= 0 && stepIndex < upgradeSteps.Count;
        }

        public UpgradeCost GetUpgradeCost()
        {
            int stepIndex = GetCurrentStepIndex();

            if (stepIndex < 0 || stepIndex >= upgradeSteps.Count)
            {
                Debug.LogWarning($"[WarehouseUpgrade] 단계({stepIndex})에 해당하는 비용 데이터가 없습니다. upgradeSteps 크기를 확인하세요.", this);
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
            return "창고 확장";
        }

        /// <summary>확인 버튼 라벨에 그대로 들어가는 짧은 문구. 최대치면 "MAX", 아니면 "현재행 → 다음행".</summary>
        public string GetUpgradeDescription()
        {
            if (warehouseInventory == null) return string.Empty;

            if (!CanUpgrade())
            {
                return "MAX";
            }

            return $"{warehouseInventory.RowCount} → {warehouseInventory.RowCount + 1}";
        }

        /// <summary>UpgradePopupUI가 비용 지불을 마친 뒤 호출한다. 여기서는 순수하게 행 추가만 담당한다.</summary>
        public void ApplyUpgrade()
        {
            if (warehouseInventory == null) return;
            warehouseInventory.AddRow();
        }
    }
}
