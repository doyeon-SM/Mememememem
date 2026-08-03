using System;
using System.Collections.Generic;
using UnityEngine;
using HDY.Item;

public class FacilityCollectManager : MonoBehaviour
{
    public static FacilityCollectManager Instance { get; private set; }

    [Header("수령 알림 임계값 설정")]
    [SerializeField] private int productionCollectThreshold = 10;
    [SerializeField] private int craftingCollectThreshold = 1;

    public int ProductionCollectThreshold
    {
        get => productionCollectThreshold;
        set { productionCollectThreshold = value; RefreshAllFacilitiesStatus(); }
    }

    public class FacilityStatusData
    {
        public MonoBehaviour facilityRuntime;
        public string currentItemId;
        public int currentCount;
        public bool isProducing;
        public bool isCraftingFacility;
        public Vector3 overheadWorldPosition;
    }

    private Dictionary<MonoBehaviour, FacilityStatusData> activeFacilities = new Dictionary<MonoBehaviour, FacilityStatusData>();

    // 🌟 [추가] 시설의 상태 변동 및 생산 단위 완료 감지 이벤트
    public static event Action<MonoBehaviour> OnFacilityChangedEvent;
    public static event Action OnCollectAllTriggered;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

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

    public void NotifyFacilityChanged(MonoBehaviour facility)
    {
        if (facility == null) return;
        if (!activeFacilities.ContainsKey(facility))
        {
            RegisterFacility(facility);
        }
        UpdateFacilityState(facility);

        // 🌟 생산/제작 수량 및 상태 변동 시 레코드 저장용 이벤트 발행
        OnFacilityChangedEvent?.Invoke(facility);
    }

    private void UpdateFacilityState(MonoBehaviour facility)
    {
        if (!activeFacilities.TryGetValue(facility, out FacilityStatusData data)) return;

        string itemId = string.Empty;
        int count = 0;
        bool isProducing = false;
        Vector3 worldPos = facility.transform.position;

        if (facility is ProductionFacilityRuntime prod)
        {
            itemId = prod.craftingItem;
            count = prod.currentStorageCount;
            isProducing = prod.isProducing;
        }
        else if (facility is ProductionCraftRuntime craft)
        {
            itemId = craft.currentCraftingItem;
            count = craft.currentStorageCount;
            isProducing = craft.isProducing;
        }
        else if (facility is RanchFacilityRuntime ranch)
        {
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
        data.overheadWorldPosition = worldPos + new Vector3(0f, 1.0f, 0f);

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

    private bool CheckBubbleCondition(FacilityStatusData data)
    {
        if (data.currentCount <= 0 || string.IsNullOrEmpty(data.currentItemId)) return false;
        if (!data.isProducing) return true;
        return data.isCraftingFacility ? data.currentCount >= craftingCollectThreshold : data.currentCount >= productionCollectThreshold;
    }

    public void CollectSingleFacility(MonoBehaviour facility)
    {
        if (facility == null || !activeFacilities.ContainsKey(facility)) return;

        if (FacilityCollectUI.Instance != null)
        {
            FacilityCollectUI.Instance.AnimateCollectSingleBubble(facility);
        }

        if (facility is ProductionFacilityRuntime prod) prod.StoredItems();
        else if (facility is ProductionCraftRuntime craft) craft.CollectCraftedItems();
        else if (facility is RanchFacilityRuntime ranch) ranch.CollectAllItems();

        UpdateFacilityState(facility);
        OnFacilityChangedEvent?.Invoke(facility);
    }

    public void RefreshAllFacilitiesStatus()
    {
        foreach (var facility in activeFacilities.Keys)
        {
            UpdateFacilityState(facility);
        }
    }
}