using HDY.Capture;
using HDY.Item;
using HDY.Mem;
using KMS.Audio;
using MemSystem.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TransportRuntime : MonoBehaviour
{
    [Header("기본 정보")]
    public BuildingData buildingData;
    public int currentLevel = 1;

    [Header("운송 정보")]
    public bool isWorking = false;
    public float baseIntervalTime = 60f;
    public int autoCollectThreshold = 10;
    public float totalRequiredTime;
    public float currentProgressTime = 0f;

    [Header("수거 진행")]
    public bool isCollecting = false;
    private Coroutine collectCoroutine;
    private ProductionFacilityRuntime currentTargetFacility;

    [Header("배치된 멤 목록")]
    [SerializeField] private List<MemData> addMems = new List<MemData>();
    [SerializeField] private List<CapturedMemEntry> addMemEntries = new List<CapturedMemEntry>();

    private Coroutine soundRoutine;
    public List<MemData> DeployedMems => addMems;
    public List<CapturedMemEntry> DeployedMemEntries => addMemEntries;

    [SerializeField] private List<Transform> memPositions = new List<Transform>();
    public List<Transform> MemPositions
    {
        get
        {
            if (memPositions == null || memPositions.Count == 0) CacheMemPositions();
            return memPositions;
        }
    }

    public static event Action OnMemDeploymentChanged;
    public static event Action<BuildingType, MemData, bool, List<Transform>> MemAdded;
    public static event Action<BuildingType, List<MemData>, List<Transform>> FacilityStarted;
    public static event Action<BuildingType, List<MemData>, FacilityStopReason, List<Transform>> FacilityStopped;

    private void Start()
    {
        EnsureBuildingData();
        CacheMemPositions();
        CheckProductionCondition();
        if (FacilityCollectManager.Instance != null)
            FacilityCollectManager.Instance.RegisterFacility(this);
    }

    private void OnDestroy()
    {
        StopCollectRoutine();
        if (FacilityCollectManager.Instance != null)
            FacilityCollectManager.Instance.UnregisterFacility(this);
    }

    private void EnsureBuildingData()
    {
        if (buildingData == null && TryGetComponent<BuildingRuntime>(out var br))
        {
            buildingData = br.buildingData;
        }
    }

    private void CacheMemPositions()
    {
        memPositions.Clear();
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name.StartsWith("MemPos"))
            {
                memPositions.Add(child);
            }
        }
    }

    /// <summary>
    /// 운송 시설 1레벨 추가 해금 (최대 3레벨)
    /// </summary>
    public void LevelUp()
    {
        if (currentLevel < 3)
        {
            currentLevel++;
            CheckProductionCondition();
            OnMemDeploymentChanged?.Invoke();

            if (TransportPanelUI.Instance != null && TransportPanelUI.Instance.TargetFacility == this)
            {
                TransportPanelUI.Instance.RefreshStaticUI();
            }
        }
    }

    private void Update()
    {
        if (!isWorking || isCollecting)
        {
            SetWorkingActive(false);
            return;
        }
        currentProgressTime += Time.deltaTime;

        if (currentProgressTime >= totalRequiredTime)
        {
            currentProgressTime = totalRequiredTime;
            ProductionFacilityRuntime targetFacility = FindTargetProductionFacility();
            if (targetFacility != null)
            {
                StartCollectRoutine(targetFacility);
            }
        }
    }

    private void StartCollectRoutine(ProductionFacilityRuntime targetFacility)
    {
        StopCollectRoutine();
        currentTargetFacility = targetFacility;
        collectCoroutine = StartCoroutine(CollectRoutine());
    }

    private void StopCollectRoutine()
    {
        if (collectCoroutine != null)
        {
            StopCoroutine(collectCoroutine);
            collectCoroutine = null;
        }
        isCollecting = false;
        currentTargetFacility = null;
    }

    private IEnumerator CollectRoutine()
    {
        isCollecting = true;
        yield return new WaitForSeconds(5f);

        if (currentTargetFacility != null && currentTargetFacility.gameObject.activeInHierarchy)
        {
            currentTargetFacility.StoredItems();
        }

        currentProgressTime = 0f;
        isCollecting = false;
        currentTargetFacility = null;
        collectCoroutine = null;
        totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseIntervalTime, addMems);
    }

    public string GetTargetItemName()
    {
        ProductionFacilityRuntime target = isCollecting ? currentTargetFacility : FindTargetProductionFacility();
        if (target == null || string.IsNullOrEmpty(target.craftingItem)) return string.Empty;

        var catalog = ItemCatalogManager.Resolve(null);
        if (catalog == null) return string.Empty;

        ItemData itemData = catalog.FindItemData(target.craftingItem);
        return itemData != null ? itemData.ItemName : string.Empty;
    }

    public ProductionFacilityRuntime FindTargetProductionFacility()
    {
        var facilities = FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        Array.Sort(facilities, (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

        foreach (var facility in facilities)
        {
            if (facility == null || !facility.gameObject.activeInHierarchy) continue;
            if (facility.currentStorageCount >= autoCollectThreshold && !string.IsNullOrEmpty(facility.craftingItem))
            {
                return facility;
            }
        }
        return null;
    }

    public void CheckProductionCondition()
    {
        if (addMems.Count == 0)
        {
            StopCollectRoutine();
            isWorking = false;
            currentProgressTime = 0f;
            return;
        }

        float currentProgressPercent = (totalRequiredTime > 0f) ? (currentProgressTime / totalRequiredTime) : 0f;
        totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseIntervalTime, addMems);
        currentProgressTime = totalRequiredTime * currentProgressPercent;

        bool isAnyMemStarving = DeployedMemEntries.Any(e => e != null && (e.IsStarving || e.CurrentHunger <= 0));
        SetWorkingActive(!isAnyMemStarving);

        if (isAnyMemStarving)
        {
            StopCollectRoutine();
        }
        
    }

    public bool TryAddMem(MemData targetMem, CapturedMemEntry targetEntry)
    {
        EnsureBuildingData();
        if (targetEntry == null || buildingData == null) return false;

        MemData realMemData = targetMem;
        if ((realMemData == null || string.IsNullOrEmpty(realMemData.memId)) && MemCatalogManager.Instance != null && !string.IsNullOrEmpty(targetEntry.MemId))
        {
            realMemData = MemCatalogManager.Instance.FindMemData(targetEntry.MemId);
        }
        if (realMemData == null) return false;

        int maxCapacity = ProductionCalculator.GetTransportMaxMemCount(currentLevel);
        if (addMemEntries.Exists(e => e != null && e.KeyId == targetEntry.KeyId)) return false;
        if (targetEntry.IsActive) return false;

        if (!ProductionCalculator.CanDeployToFacility(realMemData, buildingData.buildingType)) return false;

        if (addMems.Count >= maxCapacity && addMemEntries.Count > 0)
        {
            RemoveMem(addMemEntries[0]);
        }

        addMems.Add(realMemData);
        addMemEntries.Add(targetEntry);
        targetEntry.IsActive = true;
        CheckProductionCondition();

        if (TotalHungerManager.Instance != null) TotalHungerManager.Instance.RecalculateTotalHunger();
        OnMemDeploymentChanged?.Invoke();

        if (buildingData != null)
        {
            MemAdded?.Invoke(buildingData.buildingType, realMemData, true, MemPositions);
        }
        return true;
    }

    public void RemoveMem(CapturedMemEntry targetEntry)
    {
        if (targetEntry == null) return;
        int index = addMemEntries.FindIndex(e => e != null && e.KeyId == targetEntry.KeyId);
        if (index >= 0)
        {
            MemData removedMem = (index < addMems.Count) ? addMems[index] : null;
            addMemEntries[index].IsActive = false;
            addMemEntries.RemoveAt(index);
            if (index < addMems.Count) addMems.RemoveAt(index);
            CheckProductionCondition();

            if (TotalHungerManager.Instance != null) TotalHungerManager.Instance.RecalculateTotalHunger();
            OnMemDeploymentChanged?.Invoke();

            if (buildingData != null && removedMem != null)
            {
                MemAdded?.Invoke(buildingData.buildingType, removedMem, false, MemPositions);
            }
        }
        if(addMems.Count == 0)
        {
            SetWorkingActive(false);
        }   
    } 

    public void RemoveMem(MemData targetMem)
    {
        if (targetMem == null) return;
        int index = addMems.IndexOf(targetMem);
        if (index >= 0 && index < addMemEntries.Count)
        {
            RemoveMem(addMemEntries[index]);
        }
    }

    private void SetWorkingActive(bool value)
    {
        if (isWorking == value && (value && soundRoutine != null)) return;
        isWorking = value;
        if (isWorking)
        {
            if (soundRoutine != null) StopCoroutine(soundRoutine);
            soundRoutine = StartCoroutine(FacilitySoundRoutine());

            if (buildingData != null)
                FacilityStarted?.Invoke(buildingData.buildingType, addMems, MemPositions);
        }
        else
        {
            if (soundRoutine != null)
            {
                StopCoroutine(soundRoutine);
                soundRoutine = null;
            }

            KMSAudioService.StopSfx(GameSfxId.Transport);

            if (buildingData != null)
                FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.CancelCrafting, MemPositions);
        }
    }

    private IEnumerator FacilitySoundRoutine()
    {
        while (isWorking)
        {
            KMSAudioService.PlayAt(GameSfxId.Transport, transform.position);
            yield return new WaitForSeconds(2.0f);
        }
    }

    public void StopWorkDueToStarvation()
    {
        if (!isWorking) return;
        StopCollectRoutine();
        SetWorkingActive(false);
        if (buildingData != null)
        {
            FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.Starvation, MemPositions);
        }
    }
}