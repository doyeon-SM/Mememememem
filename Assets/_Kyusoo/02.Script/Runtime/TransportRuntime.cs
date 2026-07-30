using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HDY.Capture;
using HDY.Item;
using HDY.Mem;
using MemSystem.Data;

/// <summary>
/// 운반 시설의 런타임 로직을 담당합니다.
/// </summary>
public class TransportRuntime : MonoBehaviour
{
    [Header("기본 시설 정보")]
    public BuildingData buildingData;
    public int currentLevel = 1;

    [Header("운반 작업 설정")]
    public bool isWorking = false;
    [Tooltip("기본 운반 주기 (60초)")]
    public float baseIntervalTime = 60f;
    [Tooltip("자동 수령을 진행할 최소 생산 축적 수량")]
    public int autoCollectThreshold = 10;

    public float totalRequiredTime;
    public float currentProgressTime = 0f;

    [Header("수거 지연 상태")]
    [Tooltip("현재 5초간 수거 작업을 진행 중인지 여부")]
    public bool isCollecting = false;
    private Coroutine collectCoroutine;
    private ProductionFacilityRuntime currentTargetFacility;

    [Header("배치된 멤 데이터 (최대 3마리)")]
    [SerializeField] private List<MemData> addMems = new List<MemData>();
    [SerializeField] private List<CapturedMemEntry> addMemEntries = new List<CapturedMemEntry>();

    public List<MemData> DeployedMems => addMems;
    public List<CapturedMemEntry> DeployedMemEntries => addMemEntries;

    public static event Action OnMemDeploymentChanged;
    public static event Action<BuildingType, MemData, bool> MemAdded;
    public static event Action<BuildingType, List<MemData>> FacilityStarted;
    public static event Action<BuildingType, List<MemData>, FacilityStopReason> FacilityStopped;

    private void Start()
    {
        EnsureBuildingData();
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

    public void LevelUp()
    {
        int maxCapacity = ProductionCalculator.GetTransportMaxMemCount(currentLevel + 1);
        if (currentLevel < maxCapacity)
        {
            currentLevel++;
            CheckProductionCondition();
            OnMemDeploymentChanged?.Invoke();
            Debug.Log($"<color=lime>[운반시설 레벨업]</color> {buildingData?.buildingName} 레벨이 Lv.{currentLevel}로 상승했습니다.");
        }
    }

    private void Update()
    {
        // 작동 중이 아니거나 이미 5초 수거 동작 중이면 타이머 멈춤
        if (!isWorking || isCollecting) return;

        currentProgressTime += Time.deltaTime;

        if (currentProgressTime >= totalRequiredTime)
        {
            // 진행도를 100% 상태로 고정
            currentProgressTime = totalRequiredTime;

            // 수거 조건(10개 이상) 충족 시설 탐색
            ProductionFacilityRuntime targetFacility = FindTargetProductionFacility();

            if (targetFacility != null)
            {
                StartCollectRoutine(targetFacility);
            }
        }
    }

    /// <summary>
    /// 5초 지연 수거 코루틴 시작
    /// </summary>
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

    /// <summary>
    /// 5초 대기 후 수령 처리 코루틴
    /// </summary>
    private IEnumerator CollectRoutine()
    {
        isCollecting = true;

        // 5초간 수거 지연
        yield return new WaitForSeconds(5f);

        if (currentTargetFacility != null && currentTargetFacility.gameObject.activeInHierarchy)
        {
            string facilityName = currentTargetFacility.buildingData != null ? currentTargetFacility.buildingData.buildingName : currentTargetFacility.name;
            int countBefore = currentTargetFacility.currentStorageCount;

            // 실제 창고 수령 실행
            currentTargetFacility.StoredItems();
            Debug.Log($"<color=cyan>[운반 완료]</color> '{facilityName}' 시설에서 자원 {countBefore}개 수령 완료!");
        }

        // 수거 완료 후 타이머 리셋
        currentProgressTime = 0f;
        isCollecting = false;
        currentTargetFacility = null;
        collectCoroutine = null;

        totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseIntervalTime, addMems);
    }

    /// <summary>
    /// 현재 수령 대상이 되는 생산 시설의 아이템 이름을 반환합니다.
    /// </summary>
    public string GetTargetItemName()
    {
        ProductionFacilityRuntime target = isCollecting ? currentTargetFacility : FindTargetProductionFacility();
        if (target == null || string.IsNullOrEmpty(target.craftingItem)) return string.Empty;

        var catalog = HDY.Item.ItemCatalogManager.Resolve(null);
        if (catalog == null) return string.Empty;

        HDY.Item.ItemData itemData = catalog.FindItemData(target.craftingItem);
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

        if (ConsumeFoodSystem.Instance == null || !ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation)
        {
            SetWorkingActive(true);
        }
        else
        {
            StopCollectRoutine();
            isWorking = false;
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

        ProductionStatType requiredStat = ProductionCalculator.GetRequiredStatType(buildingData.buildingType);
        if (!ProductionCalculator.CanDeployToFacility(realMemData, buildingData.buildingType))
        {
            Debug.LogWarning($"[운반시설] {realMemData.memName}의 {requiredStat} 스탯이 부족하여 배치 불가.");
            return false;
        }

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

            CheckProductionCondition();

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

    private void SetWorkingActive(bool value)
    {
        if (isWorking == value) return;
        isWorking = value;

        if (isWorking && buildingData != null)
        {
            FacilityStarted?.Invoke(buildingData.buildingType, addMems);
        }
    }

    public void StopWorkDueToStarvation()
    {
        if (!isWorking) return;
        StopCollectRoutine();
        isWorking = false;

        if (buildingData != null)
        {
            FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.Starvation);
        }
    }
}