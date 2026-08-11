using UnityEngine;
using HDY.Upgrade;

public class FacilityUpgrade : MonoBehaviour, IUpgradable
{
    [Header("공통 비용표 데이터 에셋")]
    [SerializeField] private FacilityUpgradeCostTable costTable;

    // 연결된 시설 Runtime 컴포넌트 참조
    private ProductionFacilityRuntime productionRuntime;
    private RanchFacilityRuntime ranchRuntime;
    private TransportRuntime transportRuntime;
    private GeneratorRuntime generatorRuntime;

    private void Awake()
    {
        productionRuntime = GetComponent<ProductionFacilityRuntime>();
        ranchRuntime = GetComponent<RanchFacilityRuntime>();
        transportRuntime = GetComponent<TransportRuntime>();
        generatorRuntime = GetComponent<GeneratorRuntime>();
    }

    public int GetCurrentLevel()
    {
        if (productionRuntime != null) return productionRuntime.currentLevel;
        if (ranchRuntime != null) return ranchRuntime.currentLevel;
        if (transportRuntime != null) return transportRuntime.currentLevel;
        if (generatorRuntime != null) return generatorRuntime.currentLevel;
        return 1;
    }

    public int GetMaxLevel()
    {
        return (transportRuntime != null) ? 3 : 5;
    }

    public bool CanUpgrade()
    {
        int currentLevel = GetCurrentLevel();
        int maxLevel = GetMaxLevel();

        if (currentLevel >= maxLevel) return false;
        if (costTable == null) return false;

        int stepIndex = currentLevel - 1; 
        return stepIndex >= 0 && stepIndex < costTable.Steps.Count;
    }

    public UpgradeCost GetUpgradeCost()
    {
        if (costTable == null || !CanUpgrade()) return UpgradeCost.GoldOnly(0);

        int stepIndex = GetCurrentLevel() - 1;
        var step = costTable.Steps[stepIndex];

        var cost = new UpgradeCost { GoldCost = step.GoldCost };
        if (step.MaterialCosts != null)
        {
            foreach (var mat in step.MaterialCosts)
            {
                cost.MaterialCosts.Add(new UpgradeMaterialCost
                {
                    Item_ID = mat.Item_ID,
                    Amount = mat.Amount
                });
            }
        }
        return cost;
    }

    public string GetUpgradeTitle()
    {
        string buildingName = "시설";
        BuildingData data = GetBuildingData();
        if (data != null && !string.IsNullOrEmpty(data.buildingName))
        {
            buildingName = data.buildingName;
        }
        return $"{buildingName} 강화";
    }

    // [HDY 요청] 팝업 중간에 표시할 문구. 최대 레벨이면 "Lv.Max", 아니면 "강화 비용".
    public string GetUpgradeMiddleText()
    {
        int currentLevel = GetCurrentLevel();
        int maxLevel = GetMaxLevel();

        return currentLevel >= maxLevel ? "Lv.Max" : "강화 비용";
    }

    // [HDY 요청] 확인 버튼에 표시할 고정 문구.
    public string GetUpgradeButtonText()
    {
        return "강화";
    }

    public void ApplyUpgrade()
    {
        if (!CanUpgrade()) return;

        if (productionRuntime != null) productionRuntime.LevelUp();
        else if (ranchRuntime != null) ranchRuntime.LevelUp();
        else if (transportRuntime != null) transportRuntime.LevelUp();
        else if (generatorRuntime != null) generatorRuntime.LevelUp();

        Debug.Log($"[FacilityUpgrade] {gameObject.name} 시설 레벨업 완료! (현재 레벨: {GetCurrentLevel()})");
    }

    private BuildingData GetBuildingData()
    {
        if (productionRuntime != null) return productionRuntime.buildingData;
        if (ranchRuntime != null) return ranchRuntime.buildingData;
        if (transportRuntime != null) return transportRuntime.buildingData;
        if (generatorRuntime != null) return generatorRuntime.buildingData;
        return null;
    }
}
