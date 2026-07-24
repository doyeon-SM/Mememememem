using System;
using System.Collections.Generic;
using UnityEngine;
using HDY.Item;

public class FacilityCollectManager : MonoBehaviour
{
    public static FacilityCollectManager Instance { get; private set; }

    [Header("말풍선 생성 조건 설정")]
    [Tooltip("생산 시설(벌목장/채석장/농장/목장 등) 말풍선 생성 최소 수량")]
    [SerializeField] private int productionCollectThreshold = 10; // 🌟 쉽게 변경 가능 (10 -> 1 등)

    [Tooltip("제작 시설(공방/제작대) 말풍선 생성 최소 수량")]
    [SerializeField] private int craftingCollectThreshold = 1;

    public int ProductionCollectThreshold
    {
        get => productionCollectThreshold;
        set { productionCollectThreshold = value; RefreshAllFacilitiesStatus(); }
    }

    // 개별 시설 인스턴스 기반 추적 데이터 구조체
    public class FacilityStatusData
    {
        public MonoBehaviour facilityRuntime;
        public string currentItemId;
        public int currentCount;
        public bool isProducing;
        public bool isCraftingFacility;
        public Vector3 overheadWorldPosition;
    }

    // 다중 건물을 완벽히 구별하기 위해 런타임 컴포넌트 인스턴스를 Key로 사용
    private Dictionary<MonoBehaviour, FacilityStatusData> activeFacilities = new Dictionary<MonoBehaviour, FacilityStatusData>();

    public static event Action OnCollectAllTriggered;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 시설 런타임이 Start/OnEnable 시 호출하여 시스템에 등록
    /// </summary>
    public void RegisterFacility(MonoBehaviour facility)
    {
        if (facility == null || activeFacilities.ContainsKey(facility)) return;

        FacilityStatusData data = new FacilityStatusData
        {
            facilityRuntime = facility,
            currentItemId = string.Empty,
            currentCount = 0,
            isProducing = false,
            isCraftingFacility = facility is ProductionCraftRuntime
        };

        activeFacilities.Add(facility, data);
        UpdateFacilityState(facility);
    }

    /// <summary>
    /// 시설 파괴/철거 시 해제
    /// </summary>
    public void UnregisterFacility(MonoBehaviour facility)
    {
        if (facility == null) return;

        if (activeFacilities.ContainsKey(facility))
        {
            activeFacilities.Remove(facility);
            if (FacilityCollectUI.Instance != null)
            {
                FacilityCollectUI.Instance.RemoveBubble(facility);
            }
        }
    }

    /// <summary>
    /// 각 시설에서 수량 변경, 제작 개시, 가동 중단 이벤트 발생 시 호출
    /// </summary>
    public void NotifyFacilityChanged(MonoBehaviour facility)
    {
        if (facility == null) return;
        if (!activeFacilities.ContainsKey(facility))
        {
            RegisterFacility(facility);
        }

        UpdateFacilityState(facility);
    }

    /// <summary>
    /// 시설 상태 파악 및 말풍선 노출 조건 검사
    /// </summary>
    private void UpdateFacilityState(MonoBehaviour facility)
    {
        if (!activeFacilities.TryGetValue(facility, out FacilityStatusData data)) return;

        string itemId = string.Empty;
        int count = 0;
        bool isProducing = false;
        Vector3 worldPos = facility.transform.position;

        // 1. 일반 생산 시설 (벌목장, 채석장 등)
        if (facility is ProductionFacilityRuntime prod)
        {
            itemId = prod.craftingItem;
            count = prod.currentStorageCount;
            isProducing = prod.isProducing;
        }
        // 2. 제작 시설 (공방, 제작대)
        else if (facility is ProductionCraftRuntime craft)
        {
            itemId = craft.currentCraftingItem;
            count = craft.currentStorageCount;
            isProducing = craft.isProducing;
        }
        // 3. 목장 시설
        else if (facility is RanchFacilityRuntime ranch)
        {
            // 목장은 슬롯 중 수령 가능한 자원 총합 수량 계산
            count = 0;
            foreach (var slot in ranch.Slots)
            {
                if (slot.currentStorageCount > 0)
                {
                    count += slot.currentStorageCount;
                    if (string.IsNullOrEmpty(itemId)) itemId = slot.craftingItemId;
                }
            }
            isProducing = ranch.isProducing;
        }

        data.currentItemId = itemId;
        data.currentCount = count;
        data.isProducing = isProducing;
        data.overheadWorldPosition = worldPos + new Vector3(0f, 2.2f, 0f); // 머리 위 Y축 오프셋

        // 🌟 말풍선 생성 조건 판단
        bool shouldShowBubble = CheckBubbleCondition(data);

        if (FacilityCollectUI.Instance != null)
        {
            if (shouldShowBubble && !string.IsNullOrEmpty(data.currentItemId))
            {
                ItemData itemData = ItemCatalogManager.Instance != null ? ItemCatalogManager.Instance.FindItemData(data.currentItemId) : null;
                Sprite icon = itemData != null ? itemData.ItemIcon : null;

                FacilityCollectUI.Instance.ShowBubble(facility, icon, data.overheadWorldPosition);
            }
            else
            {
                FacilityCollectUI.Instance.HideBubble(facility);
            }
        }
    }

    /// <summary>
    /// 말풍선 노출 조건 검사 헬퍼 메서드
    /// </summary>
    private bool CheckBubbleCondition(FacilityStatusData data)
    {
        if (data.currentCount <= 0 || string.IsNullOrEmpty(data.currentItemId)) return false;

        // 1. 가동 중지 상태(식량 부족으로 멈춤, 제작 완료 등)일 때 자원이 1개라도 있다면 무조건 노출
        if (!data.isProducing) return true;

        // 2. 정상 가동 중일 때는 생산/제작 종류별 설정된 임계값 이상일 때 노출
        if (data.isCraftingFacility)
        {
            return data.currentCount >= craftingCollectThreshold; // 제작: 1개 이상
        }
        else
        {
            return data.currentCount >= productionCollectThreshold; // 생산: 10개 이상 (설정 가능)
        }
    }

    /// <summary>
    /// 🌟 아무 말풍선이나 클릭 시 실행되는 전체 일괄 수령 (Collect All)
    /// </summary>
    public void CollectAll()
    {
        List<MonoBehaviour> facilitiesToRefresh = new List<MonoBehaviour>(activeFacilities.Keys);

        // 1. 모든 말풍선에 DOTween 수획(pop) 애니메이션 연출 실행
        if (FacilityCollectUI.Instance != null)
        {
            FacilityCollectUI.Instance.AnimateCollectAllBubbles();
        }

        // 2. 등록된 모든 시설의 자체 수령 로직 실행
        foreach (var facility in facilitiesToRefresh)
        {
            if (facility is ProductionFacilityRuntime prod)
            {
                prod.StoredItems();
            }
            else if (facility is ProductionCraftRuntime craft)
            {
                craft.CollectCraftedItems();
            }
            else if (facility is RanchFacilityRuntime ranch)
            {
                ranch.CollectAllItems();
            }

            // 각 시설 수령 완료 후 데이터 0 정산 및 상태 재갱신
            UpdateFacilityState(facility);
        }

        OnCollectAllTriggered?.Invoke();
        Debug.Log("<color=lime>[FacilityCollectManager]</color> 영지 내 모든 시설 생산품 일괄 수령 완료!");
    }

    public void RefreshAllFacilitiesStatus()
    {
        foreach (var facility in activeFacilities.Keys)
        {
            UpdateFacilityState(facility);
        }
    }
}