using System.Collections.Generic;
using UnityEngine;
using MemSystem.Data;

public class ProductionFacilityRuntime : MonoBehaviour
{
    [Header("시설 기반 정보 (배치 시 데이터 주입됨)")]
    public BuildingData buildingData;
    public int currentLevel = 1;

    [Header("실시간 생산 상태 변수")]
    public bool isProducing = false;
    public ProductItemData currentProductItem; 
    public float totalRequiredTime;           
    public float currentProgressTime = 0f;

    [Header("시설 내 자원 축적 현황")]
    public int currentStorageCount = 0; 
    public int maxStorageCount = 100;   

    [Header("현재 시설에 배치된 멤 리스트")]
    [SerializeField] private List<MemData> addMems = new List<MemData>();

    public List<MemData> DeployedMems => addMems;

    private void Start()
    {
        UpdateMaxStorage();
    }

    public void UpdateMaxStorage()
    {
        maxStorageCount = currentLevel * 100;
    }

    private void Update()
    {
        if (!isProducing) return;

        if (currentStorageCount >= maxStorageCount)
        {
            return;
        }

        currentProgressTime += Time.deltaTime;

        if (currentProgressTime >= totalRequiredTime)
        {
            CompleteProductionUnit();
        }
    }

    /// <summary>
    /// UI에서 특정 멤을 클릭하여 배치할때 호출
    /// </summary>
    public bool TryAddMem(MemData targetMem)
    {
        if (targetMem == null || buildingData == null) return false;

        int maxCapacity = ProductionCalculator.GetMaxMemCount(currentLevel);
        if (addMems.Count >= maxCapacity)
        {
            Debug.LogWarning($"배치 인원이 가득 찼습니다.");
            return false;
        }

        if (!ProductionCalculator.CanDeployToFacility(targetMem, buildingData.buildingType))
        {
            ProductionStatType requiredStat = ProductionCalculator.GetRequiredStatType(buildingData.buildingType);
            Debug.LogWarning($"{targetMem.memName}이 {requiredStat} 스탯이 없어 시설에 배치할 수 없습니다.");
            return false;
        }

        // 3. 배치 성공 및 실시간 소요 시간 재반영
        addMems.Add(targetMem);
        Debug.Log($"[생산] {targetMem.memName} 배치 성공!");

        if (isProducing) RecalculateTimerMidProduction();
        return true;
    }

    /// <summary>
    /// 시설에 배치된 멤을 제거할때 처리할 함수
    /// </summary>
    public void RemoveMem(MemData targetMem)
    {
        if (addMems.Contains(targetMem))
        {
            addMems.Remove(targetMem);

            if (isProducing) RecalculateTimerMidProduction();
        }
    }

    /// <summary>
    /// 생산시 시작될 때 동작할 함수
    /// </summary>
    public void SelectAndStartProduction(ProductItemData selectedItem)
    {
        if (selectedItem == null) return;

        if (addMems.Count == 0)
        {
            return;
        }

        if (selectedItem.matchBuildingType != buildingData.buildingType)
        {
            return;
        }

        currentProductItem = selectedItem;
        currentProgressTime = 0f;

        totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(currentProductItem.baseProductionTime, addMems);
        isProducing = true;

        Debug.Log($"생산 시작. 최종 완료 소요시간: {totalRequiredTime}초");
    }

    /// <summary>
    /// 생산 도중 멤 배치 변경 발생 시 즉시 감소 공식을 재호출하여 소요 시간을 재반영
    /// </summary>
    private void RecalculateTimerMidProduction()
    {
        // 도중 변동으로 인해 멤이 0명이 되어버렸다면 가동 중단 처리
        if (addMems.Count == 0)
        {
            isProducing = false;
            currentProgressTime = 0f;
            currentProductItem = null;
            Debug.LogWarning($"멤을 배치하지않아 생산 작업이 중단되었습니다.");
            return;
        }

        float currentProgressPercent = currentProgressTime / totalRequiredTime;

        totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(currentProductItem.baseProductionTime, addMems);

        currentProgressTime = totalRequiredTime * currentProgressPercent;

        Debug.Log($"멤 추가로 인한 소요시간 업데이트. 새 소요시간: {totalRequiredTime}초");
    }

    /// <summary>
    /// 아이템 1개 생성이 완료되었을 때, 시설 내부에 저장되도록 처리
    /// </summary>
    private void CompleteProductionUnit()
    {
        currentStorageCount++;

        // 아이템 수량 텍스트 수정처리(Event발행, currentStorageCount)

        currentProgressTime = 0f;
        totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(currentProductItem.baseProductionTime, addMems);
    }

    /// <summary>
    /// 시설물에서 생산된 아이템 전체를 수령할때 호출될 함수
    /// </summary>
    public void StoredItems()
    {
        if (currentStorageCount <= 0)
        {
            return;
        }

        if (currentProductItem == null) return;

        int amountToCollect = currentStorageCount;

        // 창고에 아이템 추가하는 함수 작성
        
        currentStorageCount = 0;

        // 아이템 수량 텍스트 수정처리(Event발행, currentStorageCount)

    }
}