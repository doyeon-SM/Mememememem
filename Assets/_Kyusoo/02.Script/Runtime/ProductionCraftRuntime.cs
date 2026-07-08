using System.Collections.Generic;
using UnityEngine;
using MemSystem.Data;
using HDY.Capture;
using HDY.Item; // 🌟 팀원 확정 아이템 네임스페이스 연결 완료

public class ProductionCraftRuntime : MonoBehaviour
{
    [Header("제작 시설 기반 정보")]
    public BuildingData buildingData;

    [Header("제작 상태 여부")]
    public bool isProducing = false;

    public ItemData currentCraftingItem;

    public float totalRequiredTime;
    public float currentProgressTime = 0f;

    [Header("제작 수량 데이터: 목표 설정 수량, 남은 수량")]
    public int targetQuantity = 1;
    public int remainingQuantity = 0;

    [Header("제작 완료 데이터: 제작 완료된 수량, 최대 수량")]
    public int currentStorageCount = 0;
    public int maxStorageCount;

    [Header("제작대에 배치된 멤 정보")]
    [SerializeField] private List<MemData> addMems = new List<MemData>();
    [SerializeField] private List<CapturedMemEntry> addMemEntries = new List<CapturedMemEntry>();

    public List<MemData> DeployedMems => addMems;
    public List<CapturedMemEntry> DeployedMemEntries => addMemEntries;

    // 임시. 생산 소요 시간
    private float craftingDuration = 20f;

    private void Start()
    {
        maxStorageCount = 10;
    }

    private void Update()
    {
        if (!isProducing) return;

        if (currentStorageCount >= maxStorageCount)
        {
            isProducing = false;
            return;
        }

        currentProgressTime += Time.deltaTime;

        if (currentProgressTime >= totalRequiredTime)
        {
            CompleteCraftingUnit();
        }
    }

    /// <summary>
    /// 제작 버튼 클릭시 동작되는 함수
    /// 총 소요시간(1개당 제작 시간 - 멤 등급으로인해 감속되는 시간)을 기반으로 제작을 진행
    /// 작업상태를 True로 변경
    /// </summary>
    public void SelectAndStartCrafting(ItemData targetItem, int quantity)
    {
        if (targetItem == null || addMems.Count == 0) return;

        currentCraftingItem = targetItem;
        targetQuantity = quantity;
        remainingQuantity = quantity; 
        currentProgressTime = 0f;

        totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(craftingDuration, addMems);
        isProducing = true;
    }

    /// <summary>
    /// 제작 도중 멤 슬롯 상태 변경(교체, 제거)시 진행 비율을 보존 or 제거 + 시간 재 계산처리
    /// </summary>
    private void RecalculateCraftingTimer()
    {
        // 강제 보상 Transction이 동작되도록 처리되어있는지 확인
        if (addMems.Count == 0)
        {
            isProducing = false;
            currentProgressTime = 0f;
            currentCraftingItem = null;
            remainingQuantity = 0;
            targetQuantity = 1;
            Debug.LogWarning("가동 중이던 제작 공정이 취소되었습니다.");
            return;
        }

        if (isProducing && currentCraftingItem != null && totalRequiredTime > 0f)
        {
            float currentProgressPercent = currentProgressTime / totalRequiredTime;

            totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(craftingDuration, addMems);
            currentProgressTime = totalRequiredTime * currentProgressPercent;

            Debug.Log($"제작대 멤 배치 변경으로 인한 시간 재조정 완료.");
        }
    }

    /// <summary>
    /// Drop으로 멤배치하였을 때 동작하는 함수  
    /// </summary>
    public bool TryAddMem(MemData targetMem, CapturedMemEntry targetEntry)
    {
        if (targetMem == null || buildingData == null) return false;

        if (addMems.Contains(targetMem)) return false;

        if (!ProductionCalculator.CanDeployToFacility(targetMem, buildingData.buildingType))
        {
            ProductionStatType requiredStat = ProductionCalculator.GetRequiredStatType(buildingData.buildingType);
            return false;
        }

        addMems.Add(targetMem);
        addMemEntries.Add(targetEntry);
        targetEntry.IsActive = true;

        RecalculateCraftingTimer();
        return true;
    }

    /// <summary>
    /// 멤 제거 + 시간 재조정처리
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

            RecalculateCraftingTimer();
        }
    }

    /// <summary>
    /// 제작 물품이 100%되어 완료되었을 때 생산 물품 수량 증가 + 다음 생산 진행처리
    /// </summary>
    private void CompleteCraftingUnit()
    {
        currentStorageCount++;
        remainingQuantity--; 

        currentProgressTime = 0f;

        if (remainingQuantity > 0)
        {
            totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(craftingDuration, addMems);
        }
        else
        {
            isProducing = false;
        }
    }

    /// <summary>
    /// 제작 도중 취소 버튼 클릭 시 누적 완성 개수를 판별하여 환불 처리
    /// </summary>
    public void CancelCrafting()
    {
        if (!isProducing && currentCraftingItem == null) return;

        if (currentStorageCount == 0)
        {
             // TODO:: 제작 완료가 0개일 때 소모재료 전체 인벤토리 or 창고로 보내기
        }
        else
        {
            // TODO:: 제작 완료가 1개 이상일 때 완성된 수량 -> 인벤토리 수령, 잔여 미완성 재료 -> 인벤토리 or 창고
        }

        isProducing = false;
        currentStorageCount = 0;
        remainingQuantity = 0;
        targetQuantity = 1;
        currentProgressTime = 0f;
        currentCraftingItem = null;
    }

    /// <summary>
    /// 수령 보상 처리
    /// 0개일 때, return;
    /// 전체 제작 예정 수량보다 적은상태에서 수령할 때, 수령 + 화면유지
    /// 전체 제작 수량을 다 제작한 상태에서 수령하면 수령 + 초기화면 전환
    /// </summary>
    public bool CollectCraftedItems()
    {
        if (currentStorageCount <= 0) return false;

        //  TODO :: 인벤토리로 수령하기

        currentStorageCount = 0;

        if (remainingQuantity <= 0 && !isProducing)
        {
            currentCraftingItem = null;
            targetQuantity = 1;
            return true;
        }

        return false; 
    }
}