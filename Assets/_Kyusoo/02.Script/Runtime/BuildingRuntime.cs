using UnityEngine;
using MemSystem.Data;
using HDY.Capture;
using System.Linq;

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

    public bool TryReleaseDeployedMem(CapturedMemEntry entry, MemData data)
    {
        if (entry == null) return false;

        // 1. 일반 생산 시설 스캔
        if (TryGetComponent<ProductionFacilityRuntime>(out var facilityRuntime))
        {
            if (facilityRuntime.DeployedMemEntries.Contains(entry))
            {
                Debug.Log("<color=cyan>[BuildingRuntime]</color> ProductionFacilityRuntime에서 배치된 멤을 발견하여 해제합니다.");

                facilityRuntime.RemoveMem(data);
                entry.IsActive = false;

                if (ProductionPanelUI.Instance != null && ProductionPanelUI.Instance.gameObject.activeSelf)
                {
                    ProductionPanelUI.Instance.RefreshStaticUI();
                }
                return true;
            }
        }

        // 2. 공방 제작 시설 스캔
        if (TryGetComponent<ProductionCraftRuntime>(out var craftRuntime))
        {
            if (craftRuntime.DeployedMemEntries.Contains(entry))
            {
                Debug.Log("<color=cyan>[BuildingRuntime]</color> ProductionCraftRuntime에서 배치된 멤을 발견하여 해제합니다.");

                craftRuntime.RemoveMem(data);
                entry.IsActive = false;

                if (CraftingPanelUI.Instance != null && CraftingPanelUI.Instance.gameObject.activeSelf)
                {
                    CraftingPanelUI.Instance.RefreshStaticUI();
                }
                return true;
            }
        }

        // 3. 목장 시설 스캔 추가
        if (TryGetComponent<RanchFacilityRuntime>(out var ranchRuntime))
        {
            var targetSlot = ranchRuntime.Slots.FirstOrDefault(s => s.deployedMemEntry == entry);
            if (targetSlot != null && targetSlot.deployedMem != null)
            {
                Debug.Log("<color=cyan>[BuildingRuntime]</color> RanchFacilityRuntime에서 배치된 멤을 발견하여 해제합니다.");

                ranchRuntime.RemoveMem(data);
                entry.IsActive = false;

                if (RanchPanelUI.Instance != null && RanchPanelUI.Instance.gameObject.activeSelf)
                {
                    RanchPanelUI.Instance.RefreshStaticUI();
                }
                return true;
            }
        }

        return false;
    }
}