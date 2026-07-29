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

        // 1. 일반 생산 시설
        if (TryGetComponent<ProductionFacilityRuntime>(out var facilityRuntime))
        {
            if (facilityRuntime.DeployedMemEntries.Contains(entry))
            {
                Debug.Log("<color=cyan>[BuildingRuntime]</color> ProductionFacilityRuntime 슬롯 해제.");
                facilityRuntime.RemoveMem(data);
                entry.IsActive = false;
                if (ProductionPanelUI.Instance != null && ProductionPanelUI.Instance.gameObject.activeSelf)
                {
                    ProductionPanelUI.Instance.RefreshStaticUI();
                }
                return true;
            }
        }

        // 2. 제작대 시설
        if (TryGetComponent<ProductionCraftRuntime>(out var craftRuntime))
        {
            if (craftRuntime.DeployedMemEntries.Contains(entry))
            {
                Debug.Log("<color=cyan>[BuildingRuntime]</color> ProductionCraftRuntime 슬롯 해제.");
                craftRuntime.RemoveMem(data);
                entry.IsActive = false;
                if (CraftingPanelUI.Instance != null && CraftingPanelUI.Instance.gameObject.activeSelf)
                {
                    CraftingPanelUI.Instance.RefreshStaticUI();
                }
                return true;
            }
        }

        // 3. 목장 시설
        if (TryGetComponent<RanchFacilityRuntime>(out var ranchRuntime))
        {
            var targetSlot = ranchRuntime.Slots.FirstOrDefault(s => s.deployedMemEntry == entry);
            if (targetSlot != null && targetSlot.deployedMem != null)
            {
                Debug.Log("<color=cyan>[BuildingRuntime]</color> RanchFacilityRuntime 슬롯 해제.");
                ranchRuntime.RemoveMem(data);
                entry.IsActive = false;
                if (RanchPanelUI.Instance != null && RanchPanelUI.Instance.gameObject.activeSelf)
                {
                    RanchPanelUI.Instance.RefreshStaticUI();
                }
                return true;
            }
        }

        // 4. 발전기 시설
        if (TryGetComponent<GeneratorRuntime>(out var genRuntime))
        {
            if (genRuntime.DeployedMemEntries.Contains(entry))
            {
                Debug.Log("<color=cyan>[BuildingRuntime]</color> GeneratorRuntime 슬롯 해제.");
                genRuntime.RemoveMem(data);
                entry.IsActive = false;
                if (GeneratorPanelUI.Instance != null && GeneratorPanelUI.Instance.gameObject.activeSelf)
                {
                    GeneratorPanelUI.Instance.RefreshStaticUI();
                }
                return true;
            }
        }

        // 5. 운송 시설
        if (TryGetComponent<TransportRuntime>(out var transportRuntime))
        {
            if (transportRuntime.DeployedMemEntries.Contains(entry))
            {
                Debug.Log("<color=cyan>[BuildingRuntime]</color> TransportRuntime에서 멤 해제.");
                transportRuntime.RemoveMem(data);
                entry.IsActive = false;
                if (TransportPanelUI.Instance != null && TransportPanelUI.Instance.gameObject.activeSelf)
                {
                    TransportPanelUI.Instance.RefreshStaticUI();
                }
                return true;
            }
        }

        return false;
    }
}