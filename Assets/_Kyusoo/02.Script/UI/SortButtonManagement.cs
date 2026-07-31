using UnityEngine;
using UnityEngine.UI;
using HDY.UI;

public class SortButtonManagement : MonoBehaviour
{
    public static SortButtonManagement Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void UpdateSortFilters(GameObject facilityObject)
    {
        if (facilityObject == null)
        {
            Debug.LogWarning("[SortButtonManagement] facilityObject가 null입니다.");
            return;
        }

        MemStorageUI_Sort[] activeSortComponents = Object.FindObjectsByType<MemStorageUI_Sort>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (activeSortComponents == null || activeSortComponents.Length == 0)
        {
            Debug.LogWarning("[SortButtonManagement] 활성화된 P_Sort(MemStorageUI_Sort)를 찾을 수 없습니다.");
            return;
        }

        string targetKeyword = GetKeywordFromFacility(facilityObject);

        foreach (var sortComp in activeSortComponents)
        {
            if (sortComp == null || !sortComp.gameObject.activeInHierarchy) continue;

            Transform pSortTransform = sortComp.transform;
            int activeCount = 0;

            for (int i = 0; i < pSortTransform.childCount; i++)
            {
                Transform child = pSortTransform.GetChild(i);
                string childNameLower = child.name.ToLower();

                bool shouldActive = false;
                if (childNameLower.Contains("id") || childNameLower.Contains("tier"))
                {
                    shouldActive = true;
                }
                else if (!string.IsNullOrEmpty(targetKeyword) && childNameLower.Contains(targetKeyword))
                {
                    shouldActive = true;
                }

                child.gameObject.SetActive(shouldActive);
                if (shouldActive) activeCount++;
            }

            if (pSortTransform is RectTransform rectTransform)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            }
        }
    }

    private string GetKeywordFromFacility(GameObject facilityObject)
    {
        var kitchenRuntime = facilityObject.GetComponentInParent<KitchenRuntime>();
        if (kitchenRuntime == null) kitchenRuntime = facilityObject.GetComponentInChildren<KitchenRuntime>();
        if (kitchenRuntime != null && kitchenRuntime.buildingData != null)
        {
            return GetKeywordByBuildingType(kitchenRuntime.buildingData.buildingType);
        }

        var kitchenUI = facilityObject.GetComponentInParent<KitchenPanelUI>();
        if (kitchenUI == null) kitchenUI = facilityObject.GetComponentInChildren<KitchenPanelUI>();
        if (kitchenUI != null && kitchenUI.TargetFacility != null && kitchenUI.TargetFacility.buildingData != null)
        {
            return GetKeywordByBuildingType(kitchenUI.TargetFacility.buildingData.buildingType);
        }

        var campFireRuntime = facilityObject.GetComponentInParent<CampFireRuntime>();
        if (campFireRuntime == null) campFireRuntime = facilityObject.GetComponentInChildren<CampFireRuntime>();
        if (campFireRuntime != null && campFireRuntime.buildingData != null)
        {
            return GetKeywordByBuildingType(campFireRuntime.buildingData.buildingType);
        }

        var campFireUI = facilityObject.GetComponentInParent<CampFirePanelUI>();
        if (campFireUI == null) campFireUI = facilityObject.GetComponentInChildren<CampFirePanelUI>();
        if (campFireUI != null && campFireUI.TargetFacility != null && campFireUI.TargetFacility.buildingData != null)
        {
            return GetKeywordByBuildingType(campFireUI.TargetFacility.buildingData.buildingType);
        }

        var transportRuntime = facilityObject.GetComponentInParent<TransportRuntime>();
        if (transportRuntime == null) transportRuntime = facilityObject.GetComponentInChildren<TransportRuntime>();
        if (transportRuntime != null && transportRuntime.buildingData != null)
        {
            return GetKeywordByBuildingType(transportRuntime.buildingData.buildingType);
        }

        var transportUI = facilityObject.GetComponentInParent<TransportPanelUI>();
        if (transportUI == null) transportUI = facilityObject.GetComponentInChildren<TransportPanelUI>();
        if (transportUI != null && transportUI.TargetFacility != null && transportUI.TargetFacility.buildingData != null)
        {
            return GetKeywordByBuildingType(transportUI.TargetFacility.buildingData.buildingType);
        }

        var genRuntime = facilityObject.GetComponentInParent<GeneratorRuntime>();
        if (genRuntime == null) genRuntime = facilityObject.GetComponentInChildren<GeneratorRuntime>();
        if (genRuntime != null && genRuntime.buildingData != null)
        {
            return GetKeywordByBuildingType(genRuntime.buildingData.buildingType);
        }

        var genUI = facilityObject.GetComponentInParent<GeneratorPanelUI>();
        if (genUI == null) genUI = facilityObject.GetComponentInChildren<GeneratorPanelUI>();
        if (genUI != null && genUI.TargetFacility != null && genUI.TargetFacility.buildingData != null)
        {
            return GetKeywordByBuildingType(genUI.TargetFacility.buildingData.buildingType);
        }

        var ranchRuntime = facilityObject.GetComponentInParent<RanchFacilityRuntime>();
        if (ranchRuntime == null) ranchRuntime = facilityObject.GetComponentInChildren<RanchFacilityRuntime>();
        if (ranchRuntime != null && ranchRuntime.buildingData != null)
        {
            return GetKeywordByBuildingType(ranchRuntime.buildingData.buildingType);
        }

        var ranchUI = facilityObject.GetComponentInParent<RanchPanelUI>();
        if (ranchUI == null) ranchUI = facilityObject.GetComponentInChildren<RanchPanelUI>();
        if (ranchUI != null && ranchUI.TargetFacility != null && ranchUI.TargetFacility.buildingData != null)
        {
            return GetKeywordByBuildingType(ranchUI.TargetFacility.buildingData.buildingType);
        }

        var craftRuntime = facilityObject.GetComponentInParent<ProductionCraftRuntime>();
        if (craftRuntime == null) craftRuntime = facilityObject.GetComponentInChildren<ProductionCraftRuntime>();
        if (craftRuntime != null && craftRuntime.buildingData != null)
        {
            return GetKeywordByBuildingType(craftRuntime.buildingData.buildingType);
        }

        var facilityRuntime = facilityObject.GetComponentInParent<ProductionFacilityRuntime>();
        if (facilityRuntime == null) facilityRuntime = facilityObject.GetComponentInChildren<ProductionFacilityRuntime>();
        if (facilityRuntime != null && facilityRuntime.buildingData != null)
        {
            return GetKeywordByBuildingType(facilityRuntime.buildingData.buildingType);
        }

        var expUI = facilityObject.GetComponentInParent<ExplorationPanelUI>();
        if (expUI == null) expUI = facilityObject.GetComponentInChildren<ExplorationPanelUI>();
        if (expUI != null)
        {
            return "exp";
        }

        return "";
    }

    private string GetKeywordByBuildingType(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.Workshop: return "craft";
            case BuildingType.LoggingCamp: return "log";
            case BuildingType.MiningCamp: return "mining";
            case BuildingType.TransportFacility: return "trans";
            case BuildingType.Generator: return "trans";
            case BuildingType.Farm: return "farm";
            case BuildingType.Ranch: return "farm";
            default: return "farm";
        }
    }
}