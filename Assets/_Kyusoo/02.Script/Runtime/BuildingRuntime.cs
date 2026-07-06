using UnityEngine;

public class BuildingRuntime : MonoBehaviour
{
    public BuildingData buildingData { get; private set; }

    public int currentLevel = 1;
    public int currentStorageCount;
    public int maxStorageCount;

    public int gridX;
    public int gridZ;


    public void Initialize(BuildingData buildingData, int x, int z)
    {
        this.buildingData = buildingData;
        gridX = x;
        gridZ = z;
    }
}
