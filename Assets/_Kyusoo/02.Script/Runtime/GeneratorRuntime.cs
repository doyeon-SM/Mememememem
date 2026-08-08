using HDY.Capture;
using HDY.Item;
using HDY.Mem;
using KMS.Audio;
using KMS.InventoryDuped;
using MemSystem.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GeneratorRuntime : MonoBehaviour
{
    [Header("시설 기본 정보")]
    public BuildingData buildingData;
    public int currentLevel = 1;

    [Header("발전 상태")]
    public bool isPowerGenerating = false;
    public float basePowerGenerationTime = 30f;
    public int powerPerUnit = 10;
    public float totalPowerRequiredTime;
    public float currentPowerProgressTime = 0f;

    [Header("전력 보관 정보")]
    public int currentPowerStorage = 0;
    public int maxPowerStorage = 300;

    [Header("배치된 멤 정보")]
    [SerializeField] private List<MemData> addMems = new List<MemData>();
    [SerializeField] private List<CapturedMemEntry> addMemEntries = new List<CapturedMemEntry>();

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
        UpdateMaxPowerStorage();
        CheckPowerCondition();
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

    public void LevelUp()
    {
        currentLevel++;
        UpdateMaxPowerStorage();
        CheckPowerCondition();
        OnMemDeploymentChanged?.Invoke();
    }

    public void UpdateMaxPowerStorage()
    {
        maxPowerStorage = currentLevel * 300;
    }

    private void Update()
    {
        if (!isPowerGenerating) return;

        if (currentPowerStorage >= maxPowerStorage)
        {
            isPowerGenerating = false;
            return;
        }

        currentPowerProgressTime += Time.deltaTime;

        if (currentPowerProgressTime >= totalPowerRequiredTime)
        {
            CompletePowerUnit();
        }
    }

    private void CompletePowerUnit()
    {
        currentPowerStorage = Mathf.Min(maxPowerStorage, currentPowerStorage + powerPerUnit);
        currentPowerProgressTime = 0f;

        if (addMems.Count > 0)
        {
            totalPowerRequiredTime = ProductionCalculator.CalculatePowerGenerationTime(basePowerGenerationTime, addMems[0]);
        }
    }

    public void CheckPowerCondition()
    {
        if (addMems.Count == 0)
        {
            isPowerGenerating = false;
            currentPowerProgressTime = 0f;
            return;
        }

        float currentProgressPercent = (totalPowerRequiredTime > 0f) ? (currentPowerProgressTime / totalPowerRequiredTime) : 0f;

        totalPowerRequiredTime = ProductionCalculator.CalculatePowerGenerationTime(basePowerGenerationTime, addMems[0]);
        currentPowerProgressTime = totalPowerRequiredTime * currentProgressPercent;

        // 🌟 추천 수정 방식: 배치된 멤 중 한 마리라도 IsStarving 상태인지 직접 확인
        bool isAnyMemStarving = DeployedMemEntries.Any(e => e != null && (e.IsStarving || e.CurrentHunger <= 0));

        if (!isAnyMemStarving)
        {
            SetPowerGeneratingActive(true);
        }
        else
        {
            isPowerGenerating = false;
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

        if (addMemEntries.Exists(e => e != null && e.KeyId == targetEntry.KeyId)) return false;
        if (targetEntry.IsActive) return false;

        ProductionStatType requiredStat = ProductionCalculator.GetRequiredStatType(buildingData.buildingType);

        if (!ProductionCalculator.CanDeployToFacility(realMemData, buildingData.buildingType)) return false;

        if (addMems.Count >= 1 && addMemEntries.Count > 0)
        {
            RemoveMem(addMemEntries[0]);
        }

        addMems.Add(realMemData);
        addMemEntries.Add(targetEntry);
        targetEntry.IsActive = true;

        CheckPowerCondition();

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

            CheckPowerCondition();

            if (TotalHungerManager.Instance != null) TotalHungerManager.Instance.RecalculateTotalHunger();

            OnMemDeploymentChanged?.Invoke();

            if (buildingData != null && removedMem != null)
            {
                MemAdded?.Invoke(buildingData.buildingType, removedMem, false, MemPositions);
            }
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

    public int ConsumePower(int amount)
    {
        if (currentPowerStorage <= 0) return 0;

        int consumed = Mathf.Min(currentPowerStorage, amount);
        currentPowerStorage -= consumed;

        CheckPowerCondition();

        return consumed;
    }

    private void SetPowerGeneratingActive(bool value)
    {
        if (isPowerGenerating == value) return;
        isPowerGenerating = value;

        if (isPowerGenerating && buildingData != null)
        {
            KMS.Audio.KMSAudioService.Play2D(GameSfxId.Generator);

            FacilityStarted?.Invoke(buildingData.buildingType, addMems, MemPositions);
        }
    }

    public void StopWorkDueToStarvation()
    {
        if (!isPowerGenerating) return;
        isPowerGenerating = false;

        if (buildingData != null)
        {
            FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.Starvation, MemPositions);
        }
    }
}