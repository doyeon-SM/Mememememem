using UnityEngine;
using HDY.Upgrade;

/// 음식 창고 슬롯 1개 확장을 공용 업그레이드 팝업(UpgradePopupUI)에 연결
/// </summary>
public class FoodWarehouseUpgrade : MonoBehaviour, IUpgradable
{
    [Header("데이터 참조")]
    [SerializeField] private FoodWarehouseUI foodWarehouseUI;

    [Header("단계별 필요 골드+재료 (1슬롯 확장 단위 데이터)")]
    [SerializeField] private FoodWarehouseUpgradeCostTable costTable;

    private void Awake()
    {
        if (foodWarehouseUI == null) foodWarehouseUI = FindFirstObjectByType<FoodWarehouseUI>();

        if (foodWarehouseUI == null) Debug.LogWarning("[FoodWarehouseUpgrade] foodWarehouseUI가 연결되지 않았습니다.", this);
        if (costTable == null) Debug.LogWarning("[FoodWarehouseUpgrade] costTable이 연결되지 않았습니다. FoodWarehouseUpgradeCostTable 에셋을 부착하세요.", this);
    }

    /// <summary>
    /// 현재 초기 슬롯 수 대비 확장된 슬롯 개수로 현재 업그레이드 단계 인덱스를 계산
    /// </summary>
    private int GetCurrentStepIndex()
    {
        if (foodWarehouseUI == null) return -1;
        return foodWarehouseUI.GetCurrentUpgradedSlotCount();
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
            Debug.LogWarning("[FoodWarehouseUpgrade] costTable이 비어있어 비용을 계산할 수 없습니다.", this);
            return UpgradeCost.GoldOnly(0);
        }

        int stepIndex = GetCurrentStepIndex();

        if (stepIndex < 0 || stepIndex >= costTable.Steps.Count)
        {
            Debug.LogWarning($"[FoodWarehouseUpgrade] 단계({stepIndex})에 해당하는 비용 데이터가 없습니다.", this);
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
        return "음식 창고 확장";
    }

    public string GetUpgradeDescription()
    {
        if (foodWarehouseUI == null) return string.Empty;

        if (!CanUpgrade())
        {
            return "MAX";
        }

        int currentSlots = foodWarehouseUI.GetTotalFoodStorageSlotCount();
        return $"{currentSlots}칸 → {currentSlots + 1}칸";
    }

    /// <summary>
    /// UpgradePopupUI에서 지불이 정상적으로 처리된 후 호출되며 실제 1개 슬롯을 확장합니다.
    /// </summary>
    public void ApplyUpgrade()
    {
        if (foodWarehouseUI == null) return;
        foodWarehouseUI.AddSingleFoodStorageSlot();
    }
}
