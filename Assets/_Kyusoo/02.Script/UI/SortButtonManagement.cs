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

    /// <summary>
    /// 현재 씬에서 활성화된 P_Sort(MemStorageUI_Sort)를 전부 찾아, 
    /// 대상 시설(facilityObject)에 맞는 정렬 버튼만 활성화합니다.
    /// </summary>
    public void UpdateSortFilters(GameObject facilityObject)
    {
        if (facilityObject == null)
        {
            Debug.LogWarning("[SortButtonManagement] facilityObject가 null입니다.");
            return;
        }

        // 🌟 [핵심 변경]: 부모 위치(Center/Left)와 상관없이 씬 내 활성화된 모든 MemStorageUI_Sort(P_Sort) 탐색
        MemStorageUI_Sort[] activeSortComponents = Object.FindObjectsByType<MemStorageUI_Sort>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (activeSortComponents == null || activeSortComponents.Length == 0)
        {
            Debug.LogWarning("[SortButtonManagement] 씬에서 현재 활성화된 P_Sort(MemStorageUI_Sort)를 찾지 못했습니다.");
            return;
        }

        // 1. 시설 종류에 따른 키워드 추출
        string targetKeyword = GetKeywordFromFacility(facilityObject);
        Debug.Log($"<color=cyan>[SortButtonManagement]</color> 활성화된 P_Sort {activeSortComponents.Length}개 발견! (필터 키워드: '<b>{targetKeyword}</b>')");

        // 2. 발견된 활성 P_Sort 들의 자식 버튼 SetActive 제어
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

                // MemId와 Tier 정렬 버튼은 상시 노출
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

            // UI Layout 즉시 강제 재계산
            if (pSortTransform is RectTransform rectTransform)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            }

            Debug.Log($"<color=lime>[SortButtonManagement]</color> '{pSortTransform.name}' (위치: {pSortTransform.parent?.name}) 정렬 필터 적용 완료! (활성 버튼 {activeCount}개)");
        }
    }

    /// <summary>
    /// 시설/패널 오브젝트(또는 그 부모/자식)에서 정렬 키워드 추출
    /// </summary>
    private string GetKeywordFromFacility(GameObject facilityObject)
    {
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
            case BuildingType.Farm: return "farm";
            default: return "";
        }
    }
}