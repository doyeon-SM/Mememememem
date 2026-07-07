using UnityEngine;

[CreateAssetMenu(fileName = "New_Product_Item", menuName = "Territory/ProductItemData")]
public class ProductItemData : ScriptableObject
{
    [Header("아이템 기본 정보")]
    public string itemId;
    public string itemName;
    public ProductItemType itemType;
    public Sprite itemIcon; 

    [Header("생산 및 밸런스 정보")]
    public BuildingType matchBuildingType;
    [Range(1, 5)] public int itemStage = 1;
    public float baseProductionTime = 30f;

    /// <summary>
    /// 인스펙터에서 아이템 단계가 올라갈때마다 10초씩 증가되도록 처리
    /// </summary>
    private void OnValidate()
    {
        baseProductionTime = 30f + ((itemStage - 1) * 10f);
    }
}
