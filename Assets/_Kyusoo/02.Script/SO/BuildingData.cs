using UnityEngine;

[CreateAssetMenu(fileName = "New_Building_Data", menuName = "Territory/BuildingData")]
public class BuildingData : ScriptableObject
{
    [Header("시설 정보")]
    public string buildingId;
    public string buildingName;
    public GameObject buildingPrefab;
    public Sprite buildingImage;
    public BuildingType buildingType;
    public int satisfaction;

    [Header("건물 사이즈")]
    public int width;
    public int height;
}
