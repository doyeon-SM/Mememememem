using UnityEngine;
using HDY.Upgrade;

namespace HDY.Inventory
{
    /// <summary>
    /// 창고 한 줄(가로 폭만큼, 10칸) 확장을 공용 업그레이드 팝업(UpgradePopupUI)에 연결하는 어댑터.
    /// 실제 행 추가는 WarehouseInventory가 담당하고(이미 슬롯/크기 개념을 갖고 있음), 이 클래스는
    /// IUpgradable을 구현해서 "이번 업그레이드에 얼마가 드는지 / 더 업그레이드할 수 있는지"만 계산한다.
    ///
    /// [HDY 요청 - 비용표 SO 분리] 예전에는 upgradeSteps 리스트를 이 컴포넌트가 직접 인스펙터에 들고
    /// 있었지만, InventoryUpgrade와 동일한 패턴으로 WarehouseUpgradeCostTable(ScriptableObject) 에셋에
    /// 비용표를 분리했다. 씬을 열지 않고도 에셋 파일만 열어 비용을 조정할 수 있고, 나중에 창고가 여러
    /// 씬/인스턴스에 배치되어도 같은 에셋 하나를 그대로 공유할 수 있다.
    /// </summary>
    public class WarehouseUpgrade : MonoBehaviour, IUpgradable
    {
        [Header("데이터 참조")]
        [SerializeField] private WarehouseInventory warehouseInventory;

        [Header("단계별 필요 골드+재료 (공용 에셋 - 시작 행 -> 다음 행, 순서대로 입력)")]
        [SerializeField] private WarehouseUpgradeCostTable costTable;

        private void Awake()
        {
            if (warehouseInventory == null) warehouseInventory = FindFirstObjectByType<WarehouseInventory>();

            if (warehouseInventory == null) Debug.LogWarning("[WarehouseUpgrade] warehouseInventory가 비어있습니다.", this);
            if (costTable == null) Debug.LogWarning("[WarehouseUpgrade] costTable이 비어있습니다. WarehouseUpgradeCostTable 에셋을 연결하세요.", this);
        }

        /// <summary>현재 행 수 기준으로 몇 번째 업그레이드 단계인지(0부터 시작). 이미 최대치면 costTable.Steps 범위를 벗어난다.</summary>
        private int GetCurrentStepIndex()
        {
            if (warehouseInventory == null) return -1;
            return warehouseInventory.RowCount - warehouseInventory.StartingRows;
        }

        public bool CanUpgrade()
        {
            if (costTable == null) return false;

            int stepIndex = GetCurrentStepIndex();
            return stepIndex >= 0 && stepIndex < costTable.Steps.Count;
        }

        public UpgradeCost GetUpgradeCost()
        {
            if (costTable == null)
            {
                Debug.LogWarning("[WarehouseUpgrade] costTable이 비어있어 비용을 계산할 수 없습니다.", this);
                return UpgradeCost.GoldOnly(0);
            }

            int stepIndex = GetCurrentStepIndex();

            if (stepIndex < 0 || stepIndex >= costTable.Steps.Count)
            {
                Debug.LogWarning($"[WarehouseUpgrade] 단계({stepIndex})에 해당하는 비용 데이터가 없습니다. costTable의 Steps 크기를 확인하세요.", this);
                return UpgradeCost.GoldOnly(0);
            }

            var step = costTable.Steps[stepIndex];
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

            return $"{warehouseInventory.RowCount}줄 → {warehouseInventory.RowCount + 1}줄";
        }

        /// <summary>UpgradePopupUI가 비용 지불을 마친 뒤 호출한다. 여기서는 순수하게 행 추가만 담당한다.</summary>
        public void ApplyUpgrade()
        {
            if (warehouseInventory == null) return;
            warehouseInventory.AddRow();
        }
    }
}
