using System;
using System.Collections.Generic;
using UnityEngine;
using HDY.Capture;
using HDY.Item;
using HDY.Mem;
using MemSystem.Data;
using KMS.InventoryDuped;

/// <summary>
/// 발전기 시설의 런타임 로직을 담당합니다.
/// 멤 1마리를 배치하여 전력을 자동 생산 및 축적합니다.
/// </summary>
public class GeneratorRuntime : MonoBehaviour
{
    [Header("기본 시설 정보")]
    public BuildingData buildingData;
    public int currentLevel = 1;

    [Header("전력 발전 설정")]
    public bool isPowerGenerating = false;
    [Tooltip("기본 발전 소요 시간 (30초)")]
    public float basePowerGenerationTime = 30f;
    [Tooltip("1회 발전 시 생성되는 전력량 (10Watt)")]
    public int powerPerUnit = 10;

    public float totalPowerRequiredTime;
    public float currentPowerProgressTime = 0f;

    [Header("전력 저장 용량")]
    [Tooltip("현재 축적된 전력량 (Watt)")]
    public int currentPowerStorage = 0;
    [Tooltip("최대 저장 가능 전력량 (Watt)")]
    public int maxPowerStorage = 300;

    [Header("배치된 멤 데이터 (최대 1마리)")]
    [SerializeField] private List<MemData> addMems = new List<MemData>();
    [SerializeField] private List<CapturedMemEntry> addMemEntries = new List<CapturedMemEntry>();

    public List<MemData> DeployedMems => addMems;
    public List<CapturedMemEntry> DeployedMemEntries => addMemEntries;

    // 대장간/시설 저장소 및 외부 연동용 공용 이벤트
    public static event Action OnMemDeploymentChanged;
    public static event Action<BuildingType, MemData, bool> MemAdded;
    public static event Action<BuildingType, List<MemData>> FacilityStarted;
    public static event Action<BuildingType, List<MemData>, FacilityStopReason> FacilityStopped;

    private void Start()
    {
        EnsureBuildingData();
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

    public void LevelUp()
    {
        currentLevel++;
        UpdateMaxPowerStorage();
        CheckPowerCondition();
        OnMemDeploymentChanged?.Invoke();
        Debug.Log($"<color=lime>[발전기 레벨업]</color> {buildingData?.buildingName} 레벨이 Lv.{currentLevel}로 상승했습니다.");
    }

    public void UpdateMaxPowerStorage()
    {
        // 1. 요구사항 반영: 레벨당 300Watt씩 최대 저장 용량 설정
        maxPowerStorage = currentLevel * 300;
    }

    private void Update()
    {
        if (!isPowerGenerating) return;

        // 전력 저장 용량이 가득 차면 발전 자동 정지
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

    /// <summary>
    /// 3. 요구사항 반영: 수령 버튼 클릭 필요 없이 발전 즉시 내부에 전력이 자동으로 수급/저장됩니다.
    /// </summary>
    private void CompletePowerUnit()
    {
        currentPowerStorage = Mathf.Min(maxPowerStorage, currentPowerStorage + powerPerUnit);
        currentPowerProgressTime = 0f;

        if (addMems.Count > 0)
        {
            // 2. 요구사항 반영: ProductionCalculator 정적 클래스 메서드 호출
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

        // 2. 요구사항 반영: ProductionCalculator 호출
        totalPowerRequiredTime = ProductionCalculator.CalculatePowerGenerationTime(basePowerGenerationTime, addMems[0]);
        currentPowerProgressTime = totalPowerRequiredTime * currentProgressPercent;

        if (ConsumeFoodSystem.Instance == null || !ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation)
        {
            SetPowerGeneratingActive(true);
        }
        else
        {
            isPowerGenerating = false;
        }
    }

    /// <summary>
    /// 발전기에 멤 배치를 시도합니다 (최대 1마리 제한).
    /// </summary>
    public bool TryAddMem(MemData targetMem, CapturedMemEntry targetEntry)
    {
        EnsureBuildingData();

        if (targetEntry == null)
        {
            Debug.LogWarning("[발전기] CapturedMemEntry가 null입니다.");
            return false;
        }

        if (buildingData == null)
        {
            Debug.LogError("[발전기] BuildingData가 연결되지 않았습니다.");
            return false;
        }

        MemData realMemData = targetMem;
        if ((realMemData == null || string.IsNullOrEmpty(realMemData.memId)) && MemCatalogManager.Instance != null && !string.IsNullOrEmpty(targetEntry.MemId))
        {
            realMemData = MemCatalogManager.Instance.FindMemData(targetEntry.MemId);
        }

        if (realMemData == null)
        {
            Debug.LogError($"[발전기] targetEntry의 MemId('{targetEntry.MemId}')에 해당하는 MemData SO를 찾을 수 없습니다.");
            return false;
        }

        if (addMemEntries.Exists(e => e != null && e.KeyId == targetEntry.KeyId))
        {
            Debug.LogWarning($"[발전기] 이미 배치된 멤(KeyID: {targetEntry.KeyId})입니다.");
            return false;
        }

        if (targetEntry.IsActive)
        {
            Debug.LogWarning($"[발전기] {realMemData.memName}(KeyID: {targetEntry.KeyId})는 이미 다른 곳에서 IsActive == true 상태입니다.");
            return false;
        }

        ProductionStatType requiredStat = ProductionCalculator.GetRequiredStatType(buildingData.buildingType);
        int currentStatVal = realMemData.productionStats.GetStat(requiredStat);

        if (!ProductionCalculator.CanDeployToFacility(realMemData, buildingData.buildingType))
        {
            Debug.LogWarning($"[발전기] {realMemData.memName}의 {requiredStat} 스탯이 1 미만({currentStatVal})이므로 배치할 수 없습니다.");
            return false;
        }

        // 1마리 제한: 기존 멤이 있다면 교체
        if (addMems.Count >= 1 && addMemEntries.Count > 0)
        {
            Debug.Log($"[발전기] 기존 배치된 멤({addMems[0].memName})을 새 멤({realMemData.memName})으로 교체합니다.");
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
            MemAdded?.Invoke(buildingData.buildingType, realMemData, true);
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
                MemAdded?.Invoke(buildingData.buildingType, removedMem, false);
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

    /// <summary>
    /// 축적된 전력을 사용/소비합니다.
    /// </summary>
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
            FacilityStarted?.Invoke(buildingData.buildingType, addMems);
        }
    }

    public void StopWorkDueToStarvation()
    {
        if (!isPowerGenerating) return;
        isPowerGenerating = false;

        if (buildingData != null)
        {
            FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.Starvation);
        }
    }
}