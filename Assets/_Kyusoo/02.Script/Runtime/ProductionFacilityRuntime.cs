using HDY.Capture;
using MemSystem.Data;
using System.Collections.Generic;
using UnityEngine;

public class ProductionFacilityRuntime : MonoBehaviour
{
    [Header("시설 기반 정보 (배치 시 데이터 주입됨)")]
    public BuildingData buildingData;
    public int currentLevel = 1;

    [Header("실시간 생산 상태 변수")]
    public bool isProducing = false;
    public ProductItemData craftingItem;
    public float totalRequiredTime;           
    public float currentProgressTime = 0f;

    [Header("시설 내 자원 축적 현황")]
    public int currentStorageCount = 0; 
    public int maxStorageCount = 100;   

    [Header("현재 시설에 배치된 멤 리스트")]
    [SerializeField] private List<MemData> addMems = new List<MemData>();

    [SerializeField] private List<CapturedMemEntry> addMemEntries = new List<CapturedMemEntry>();

    public List<MemData> DeployedMems => addMems;
    public List<CapturedMemEntry> DeployedMemEntries => addMemEntries;

    private void Start()
    {
        UpdateMaxStorage();
        CheckProductionCondition();
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
    /// 최소 1마리의 멤이 배치되면 아이템 생산되도록 처리
    /// </summary>
    public void CheckProductionCondition()
    {
        if (craftingItem == null || addMems.Count == 0)
        {
            isProducing = false;
            currentProgressTime = 0f; 
            return;
        }

        if (isProducing && totalRequiredTime > 0f)
        {
            float currentProgressPercent = currentProgressTime / totalRequiredTime;

            totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(craftingItem.baseProductionTime, addMems);

            currentProgressTime = totalRequiredTime * currentProgressPercent;

            Debug.Log($"멤 배치 변동. 새 소요시간: {totalRequiredTime}초");
        }
        else
        {
            currentProgressTime = 0f;
            totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(craftingItem.baseProductionTime, addMems);
            isProducing = true;

            Debug.Log($"{craftingItem.itemName} 제작 시작. 최종 소요시간: {totalRequiredTime}초");
        }
    }


    /// <summary>
    /// UI에서 특정 멤을 클릭하여 배치할때 호출
    /// </summary>
    public bool TryAddMem(MemData targetMem, CapturedMemEntry targetEntry)
    {
        if (targetMem == null || buildingData == null) return false;

        int maxCapacity = ProductionCalculator.GetMaxMemCount(currentLevel);
        if (addMems.Contains(targetMem))
        {
            Debug.LogWarning($"{targetMem.memName}은 이미 이 시설에 투입되어 있습니다.");
            return false;
        }
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
        addMemEntries.Add(targetEntry);
        targetEntry.IsActive = true;
        Debug.Log($"[생산] {targetMem.memName} 배치 성공!");

        CheckProductionCondition();
        return true;
    }

    /// <summary>
    /// 시설에 배치된 멤을 제거할때 처리할 함수
    /// </summary>
    public void RemoveMem(MemData targetMem)
    {
        if (addMems.Contains(targetMem))
        {
            int index = addMems.IndexOf(targetMem);
            if (index >= 0 && index < addMemEntries.Count)
            {
                addMemEntries[index].IsActive = false;
                addMemEntries.RemoveAt(index);
            }
            addMems.RemoveAt(index);

            Debug.Log($"[생산 해제] {targetMem.memName} 시설에서 제외 완료.");

            CheckProductionCondition();
        }
    }


    /// <summary>
    /// 아이템 1개 생성이 완료되었을 때, 시설 내부에 저장되도록 처리
    /// </summary>
    private void CompleteProductionUnit()
    {
        currentStorageCount++;

        // 아이템 수량 텍스트 수정처리(Event발행, currentStorageCount)

        currentProgressTime = 0f;
        if (craftingItem != null)
        {
            totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(craftingItem.baseProductionTime, addMems);
        }
    }

    /// <summary>
    /// 시설물에서 생산된 아이템 전체를 수령할때 호출될 함수
    /// </summary>
    public void StoredItems()
    {
        if (currentStorageCount <= 0) return;
        if (craftingItem == null) return;

        int amountToCollect = currentStorageCount;

        // 창고에 아이템 추가하는 함수 작성
        
        currentStorageCount = 0;

        // 아이템 수량 텍스트 수정처리(Event발행, currentStorageCount)

    }
}