using UnityEngine;
using UnityEngine.UI;
using HDY.UI;
using UnityEngine.Rendering.Universal;

public class SortButtonManagement : MonoBehaviour
{
    public static SortButtonManagement Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 클릭한 시설물에 등록된 컴포넌트(Production**Runtime)의 BuildingData.BuildingType을 찾아오고
    /// MemId, Tier, 시설 전용 스탯 버튼 총 3개만 남기고 나머지를 전부 SetActive(false)처리하기
    /// </summary>
    public void UpdateSortFiltersByFacility(GameObject facilityObject)
    {
        if (facilityObject == null) return;

        BuildingType type = BuildingType.Workshop;
        bool hasValidRuntime = false;

        if (facilityObject.TryGetComponent<ProductionCraftRuntime>(out var craftRuntime))
        {
            if (craftRuntime.buildingData != null)
            {
                type = craftRuntime.buildingData.buildingType;
                hasValidRuntime = true;
            }
        }
        else if (facilityObject.TryGetComponent<ProductionFacilityRuntime>(out var facilityRuntime))
        {
            if (facilityRuntime.buildingData != null)
            {
                type = facilityRuntime.buildingData.buildingType;
                hasValidRuntime = true;
            }
        }

        if (!hasValidRuntime) return;

        string targetKeyword = "";
        switch (type)
        {
            case BuildingType.Workshop: targetKeyword = "craft"; break; 
            case BuildingType.LoggingCamp: targetKeyword = "log"; break; 
            case BuildingType.MiningCamp: targetKeyword = "mining"; break; 
            case BuildingType.TransportFacility: targetKeyword = "trans"; break; 
            case BuildingType.Farm: targetKeyword = "farm"; break; 
        }
        Debug.Log($"type: {type}, targetKeyword: {targetKeyword}");

        MemStorageUI_Sort sortComponent = UnityEngine.Object.FindAnyObjectByType<MemStorageUI_Sort>();
        if (sortComponent == null) return;

        Transform pSortTransform = sortComponent.transform;

        for (int i = 0; i < pSortTransform.childCount; i++)
        {
            Transform child = pSortTransform.GetChild(i);
            string childNameLower = child.name.ToLower();

            if (childNameLower.Contains("id") || childNameLower.Contains("tier"))
            {
                child.gameObject.SetActive(true);
            }
            else
            {
                if (!string.IsNullOrEmpty(targetKeyword) && childNameLower.Contains(targetKeyword))
                {
                    child.gameObject.SetActive(true);
                }
                else
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
    }
}