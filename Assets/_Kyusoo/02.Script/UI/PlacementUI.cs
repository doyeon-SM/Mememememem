using UnityEngine;
using System;
using System.Collections.Generic;

public class PlacementUI : MonoBehaviour
{
    [Header("배치모드 UI 정보: 하단 패널, 생성될 부모, 건물 프리팹")]
    [SerializeField] private GameObject placementPanel;
    [SerializeField] private Transform uiContentParent;
    [SerializeField] private GameObject buildingPrefab;

    private List<GameObject> activeUISlots = new List<GameObject>();

    public static event Action<int> OnBuildingSelected;

    private void OnEnable()
    {
        GridManager.OnPlacementModeChanged += HandlePlacementModeChanged;
    }

    private void OnDisable()
    {
        GridManager.OnPlacementModeChanged -= HandlePlacementModeChanged;
    }

    /// <summary>
    /// GridManager를 통해 배치모드로 전환되면 Panel하위에 건물 UI를 생성.
    /// </summary>
    private void HandlePlacementModeChanged(bool isPlacementMode, List<BuildingData> buildings)
    {
        if(placementPanel != null)
        {
            placementPanel.SetActive(isPlacementMode);
        }

        ClearSlots();

        if(isPlacementMode && buildings != null)
        {
            for(int i = 0; i < buildings.Count; i++)
            {
                GameObject slot = Instantiate(buildingPrefab, uiContentParent);

                if(slot.TryGetComponent<BuildingItemUI>(out BuildingItemUI buildingItemUI))
                {
                    buildingItemUI.Setup(buildings[i], i, this);
                }
                activeUISlots.Add(slot);
            }
        }
    }

    /// <summary>
    /// 하단 영역에 배치된 시설중 하나 클릭시 동작.
    /// </summary>
    public void SelectBuilding(int index)
    {
        OnBuildingSelected?.Invoke(index);
    }  
    
    private void ClearSlots()
    {
        foreach(var slot in activeUISlots)
        {
            Destroy(slot);
        }
        activeUISlots.Clear();
    }
}