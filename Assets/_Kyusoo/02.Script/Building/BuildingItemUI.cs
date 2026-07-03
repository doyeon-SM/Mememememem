using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingItemUI : MonoBehaviour
{
    [Header("Item 정보: 시설 이미지, 시설 이름")]
    [SerializeField] private Image itemImage;
    [SerializeField] private TextMeshProUGUI itemName;

    private PlacementUI placementUI;
    private int dataIndex;

    public void Setup(BuildingData buildingData, int index, PlacementUI placementUI)
    {
        dataIndex = index;
        this.placementUI = placementUI;
        itemName.text = buildingData.buildingName;

        if(buildingData.buildingImage != null)
        {
            itemImage.sprite = buildingData.buildingImage;
        }
    }

    public void OnClickItem()
    {
        if(placementUI != null)
        {
            placementUI.SelectBuilding(dataIndex);
        }
    }
}
