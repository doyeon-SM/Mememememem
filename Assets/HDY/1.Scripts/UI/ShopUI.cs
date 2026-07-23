using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HDY.Item;
using HDY.Shop;
using HDY.Upgrade;
using HDY.Territory;
using HDY.Inventory;
using KMS.InventoryDuped;

namespace HDY.UI
{
    /// <summary>
    /// 공용 상점 팝업 UI. 마트/식당/철물점 등 상점 종류에 상관없이 이 하나로 전부 처리한다.
    /// 팝업은 ShopData(어떤 품목을 파는지)만 받아서 열리고, 구매/판매 탭을 전환해서 보여준다.
    ///
    /// [생명주기 = UIManager가 관리] 이 컴포넌트는 더 이상 씬에 상시 배치된 싱글톤이 아니라, HUD의
    /// "상점" 버튼을 누를 때마다 UIManager가 P_ShopRoot 프리팹을 Instantiate해서 만들고, 닫힐 때 통째로
    /// Destroy한다. 그래서 인스펙터에서 다른 씬 오브젝트(TerritoryData 등)를 직접 참조하던 필드들은
    /// 프리팹 에셋 자체에는 값이 저장될 수 없어(프리팹은 그 씬의 특정 오브젝트를 가리킬 수 없음) 전부
    /// Awake에서 씬을 훑어 자동으로 채운다(Resolve/FindFirstObjectByType류 폴백). 반면 이 프리팹
    /// 내부의 자식(버튼, 텍스트, transactionPopup 등)에 대한 참조는 프리팹 자체에 정상적으로 저장되어
    /// 있으므로 그대로 유지된다.
    ///
    /// [사용법] UIManager가 Instantiate 직후 Open(defaultShop)을 한 번 호출해서 처음 보여줄 상점을
    /// 정해준다. 그 이후 상점 내부 이동(마트/식당/철물점 전환)은 아래 "상점 이동 탭" 설명대로 이
    /// 컴포넌트가 스스로 처리하고, UIManager는 관여하지 않는다.
    ///
    /// [popupRoot와 transactionPopup은 서로 다른 창] popupRoot는 상점 창 전체(탭+목록)를 켜고 끄는
    /// 대상이고, transactionPopup(ShopTransactionPopupUI)은 슬롯을 클릭했을 때 그 위에 따로 뜨는
    /// 수량 선택 창이다 - 상점 창은 계속 열려있는데 수량 선택 창만 열렸다 닫혔다 해야 하므로 반드시
    /// 서로 다른 오브젝트다.
    ///
    /// [상점 이동 탭] shopEntries에 등록된 (버튼, ShopData) 쌍마다 그 버튼을 누르면 Open(그 ShopData)이
    /// 호출되어 상점 창 내용이 그 상점으로 바뀐다(상점 창 자체는 닫히지 않는다 - UIManager를 거치지
    /// 않는 내부 전환). 지금 보고 있는 상점의 버튼은 interactable=false로 회색 표시한다(구매/판매
    /// 탭과 동일한 방식).
    ///
    /// [탭 전환 = 같은 컨테이너 재사용] 구매/판매 탭은 같은 슬롯 컨테이너(slotContainer)와 같은 슬롯 풀
    /// (spawnedSlots)을 공유한다 - 탭을 누르면 그 컨테이너 안 내용물이 구매 목록 또는 판매 목록으로
    /// 다시 채워진다. 선택된 탭 버튼은 interactable=false로 만들어 Button의 Disabled Color(기본 회색
    /// 계열)로 "선택됨"을 표시한다. 상점을 열면(또는 다른 상점으로 이동하면) 기본으로 판매 탭이 먼저 보인다.
    ///
    /// [탭별 품목 필터링] 구매 탭에는 구매 가능한 품목만(<see cref="IsPurchasable"/>), 판매 탭에는
    /// 판매 가능한 품목만(<see cref="IsSellable"/>) 표시한다. 조건을 만족하지 못하는 아이템은 비활성화된
    /// 채로 보이는 게 아니라 목록 자체에서 빠진다.
    ///
    /// [슬롯에 표시하는 수량 = 상점 재고 그 자체] 슬롯(ShopSlotUI)에 넘겨주는 수량은 "지금 결제할 수
    /// 있는 수량"이 아니라 ShopStockManager가 관리하는 순수 재고(구매는 GetPurchaseStock, 판매는
    /// GetSellStock - 둘 다 ShopItemData의 Purchase_MaxAmount/Selling_MaxAmount에서 그동안 소모된
    /// 만큼을 뺀 값)다. 예전에는 판매 탭에 "플레이어가 지금 보유한 수량"을 보여줬는데, 그러면 그
    /// 아이템을 하나도 안 갖고 있을 때 상점 재고가 아무리 남아있어도 항상 0으로 보이는 문제가 있었다 -
    /// 이제는 상점 재고를 그대로 보여주므로 그런 문제가 없다. 골드/재료가 부족해서 실제로는 그만큼
    /// 못 사거나(구매) 그 아이템을 갖고 있지 않아서 못 파는(판매) 경우는, 슬롯을 클릭해서 거래 팝업을
    /// 열 때 GetBuyMaxQuantity/GetSellMaxQuantity(재고 + 결제 가능 여부/보유량을 모두 고려한 진짜 최대
    /// 거래 가능 수량)로 다시 계산해서 팝업 쪽에서 걸러진다(팝업의 수량 슬라이더가 그 값을 최대치로
    /// 쓰고, 0이면 확인 버튼이 비활성화됨).
    ///
    /// [슬롯 클릭 -> 팝업] 슬롯에는 수량 스테퍼나 구매/판매 버튼이 없다. 슬롯 전체가 버튼이라 클릭하면
    /// <see cref="ShopTransactionPopupUI"/>가 열려서 실제 수량 선택과 결제를 진행한다. 실제 트랜잭션
    /// 로직(재고/골드/재료 확인 및 차감, 인벤토리 지급)은 여전히 이 클래스가 담당하고, 팝업에는
    /// "결제를 실행하는 함수"만 넘겨준다.
    ///
    /// [구매 아이템 지급 순서 = 창고 먼저, 인벤토리 나중] 구매에 성공하면 지급할 아이템을 창고
    /// (warehouseInventory)에 먼저 넣고, 창고 공간이 모자라 못 들어간 나머지만 플레이어 인벤토리
    /// (playerInventory)에 넣는다(반대 순서 아님). 창고와 인벤토리 모두 공간이 부족하면 남은 수량은
    /// 지급되지 않고 경고 로그만 남긴다.
    ///
    /// [구매 가격 = 골드 또는 재료 "하나" 중 하나] ShopItemData.Purchase_Price_Material이 채워져 있으면
    /// 그 재료 하나만 소비하고 골드는 요구하지 않는다(Purchase_Price_Golds는 이 경우 무시). 재료가
    /// 없으면 골드로만 구매한다.
    ///
    /// [구매 재고 vs 판매 재고 - 별도 관리] ShopStockManager가 구매 재고(상점→플레이어)와 판매 재고
    /// (플레이어→상점)를 서로 다른 풀로 관리한다(ShopItemData.Purchase_MaxAmount / Selling_MaxAmount).
    /// 구매 최대 수량은 구매 재고와 "지금 가진 재화(골드 또는 재료)로 실제 결제 가능한 수량" 중 작은
    /// 값이고, 판매 최대 수량은 판매 재고와 "지금 보유한 수량" 중 작은 값이다.
    ///
    /// [HDY 요청 - 시트 마이그레이션] ShopData.Items(SO 리스트)가 ShopData.ItemIds(문자열 목록)로
    /// 바뀌면서, 이 화면에서 "지금 상점이 취급하는 ShopItemData 목록"이 필요할 때는 더 이상
    /// currentShop.Items를 직접 순회하지 않고 stockManager.GetShopItems(currentShop)을 사용한다 -
    /// ShopStockManager가 ItemIds를 카탈로그에서 resolve해 캐싱해둔 같은 인스턴스를 반환해줘야
    /// 재고 딕셔너리 조회(Dictionary&lt;ShopItemData,int&gt;)가 정상 동작하기 때문이다.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        /// <summary>상점 이동 탭 버튼 하나와 그 버튼이 여는 상점을 짝짓는 항목.</summary>
        [Serializable]
        private class ShopEntry
        {
            public Button button;
            public ShopData shop;
        }

        public static ShopUI Instance { get; private set; }

        [Header("데이터 참조")]
        [SerializeField] private TerritoryData territoryData;
        [Tooltip("IMaterialInventory를 구현한 컴포넌트. 비워두면 Awake에서 씬을 훑어 자동으로 찾는다(UpgradePopupUI와 동일한 방식).")]
        [SerializeField] private MonoBehaviour materialInventorySource;
        [SerializeField] private ItemCatalogManager itemCatalogManager;
        [SerializeField] private ShopStockManager stockManager;
        [Tooltip("구매한 아이템을 먼저 채워 넣을 창고. 공간이 부족한 만큼만 playerInventory로 넘어간다.")]
        [SerializeField] private WarehouseInventory warehouseInventory;
        [Tooltip("구매한 아이템 중 창고에 다 들어가지 못한 나머지가 들어갈 플레이어 인벤토리.")]
        [SerializeField] private PlayerInventory playerInventory;
        [Tooltip("골드 가격/판매 대가를 표시할 때 쓰는 공용 골드 아이콘.")]
        [SerializeField] private Sprite goldIcon;

        [Header("팝업 루트 (상점 창 전체 - 평소에는 꺼져 있다가 Open()에서 켜짐)")]
        [SerializeField] private GameObject popupRoot;

        [Header("상단 정보")]
        [SerializeField] private TMP_Text shopNameText;
        [Tooltip("다음 재입고까지 남은 시간(mm:ss)을 표시.")]
        [SerializeField] private TMP_Text restockCountdownText;

        [Header("구매 / 판매 탭 (선택된 탭 버튼은 interactable=false로 회색 표시)")]
        [SerializeField] private Button buyTabButton;
        [SerializeField] private Button sellTabButton;

        [Header("상점 이동 탭 (마트/식당/철물점 등 - 상점 창을 닫지 않고 다른 상점으로 전환)")]
        [SerializeField] private List<ShopEntry> shopEntries = new List<ShopEntry>();

        [Header("슬롯 목록 (구매/판매 탭이 같은 컨테이너를 공유)")]
        [SerializeField] private Transform slotContainer;
        [SerializeField] private ShopSlotUI slotPrefab;

        [Header("수량 선택 팝업 (popupRoot와는 다른 오브젝트 - 슬롯 클릭 시 그 위에 별도로 뜸)")]
        [SerializeField] private ShopTransactionPopupUI transactionPopup;

        [Header("닫기 버튼")]
        [SerializeField] private Button closeButton;

        private readonly List<ShopSlotUI> spawnedSlots = new List<ShopSlotUI>();

        // 매 Refresh마다 새로 만들면 GC 부담이라 재사용하는 필터링용 임시 리스트.
        private readonly List<ShopItemData> filteredItemsBuffer = new List<ShopItemData>();

        private ShopData currentShop;
        private bool showingSellTab;

        private IMaterialInventory MaterialInventory => materialInventorySource as IMaterialInventory;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ShopUI] 씬에 ShopUI가 이미 있어 중복 오브젝트를 파괴합니다.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // territoryData는 다른 씬 오브젝트(TerritoryData)를 가리키는 참조라 프리팹 에셋 자체에는
            // 저장될 수 없다(UIManager가 매번 새로 Instantiate하기 때문에 인스펙터 값이 항상 비어있다) -
            // 그래서 다른 매니저 참조들과 동일하게 씬에서 자동으로 찾도록 폴백을 둔다.
            if (territoryData == null) territoryData = FindFirstObjectByType<TerritoryData>();
            if (territoryData == null) Debug.LogWarning("[ShopUI] territoryData가 비어있습니다. 골드 확인/차감이 불가능합니다.", this);

            if (materialInventorySource == null)
            {
                materialInventorySource = FindMaterialInventorySource();

                if (materialInventorySource == null)
                {
                    Debug.LogWarning("[ShopUI] 씬에서 IMaterialInventory 구현체를 찾지 못했습니다. 재료 조건 검사를 건너뜁니다.", this);
                }
            }

            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);
            stockManager = ShopStockManager.Resolve(stockManager);

            if (warehouseInventory == null) warehouseInventory = FindFirstObjectByType<WarehouseInventory>();
            if (warehouseInventory == null) Debug.LogWarning("[ShopUI] warehouseInventory가 비어있습니다. 구매한 아이템이 창고를 거치지 않고 바로 인벤토리로 들어갑니다.", this);

            if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (playerInventory == null) Debug.LogWarning("[ShopUI] playerInventory가 비어있습니다. 구매한 아이템을 지급할 수 없습니다.", this);

            if (transactionPopup == null) Debug.LogWarning("[ShopUI] transactionPopup이 비어있습니다. 슬롯을 클릭해도 팝업이 열리지 않습니다.", this);

            if (stockManager != null)
            {
                stockManager.OnStockChanged += HandleStockChanged;
                stockManager.OnShopRestocked += HandleShopRestocked;
            }

            if (buyTabButton != null) buyTabButton.onClick.AddListener(ShowBuyTab);
            if (sellTabButton != null) sellTabButton.onClick.AddListener(ShowSellTab);
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            foreach (var entry in shopEntries)
            {
                if (entry == null || entry.button == null || entry.shop == null) continue;

                var targetShop = entry.shop; // 람다 클로저 캡처용 로컬 변수(foreach 변수를 그대로 캡처하면 마지막 값으로 덮인다)
                entry.button.onClick.AddListener(() => Open(targetShop));
            }

            if (popupRoot != null) popupRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (stockManager != null)
            {
                stockManager.OnStockChanged -= HandleStockChanged;
                stockManager.OnShopRestocked -= HandleShopRestocked;
            }
        }

        /// <summary>materialInventorySource가 비어있을 때 씬 전체에서 IMaterialInventory 구현체를 찾는다(UpgradePopupUI.FindMaterialInventorySource와 동일).</summary>
        private MonoBehaviour FindMaterialInventorySource()
        {
            var candidates = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            foreach (var candidate in candidates)
            {
                if (candidate is IMaterialInventory) return candidate;
            }

            return null;
        }

        private void Update()
        {
            if (currentShop == null || popupRoot == null || !popupRoot.activeSelf) return;
            if (restockCountdownText == null || stockManager == null) return;

            var remaining = stockManager.GetTimeUntilRestock(currentShop);
            restockCountdownText.text = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
        }

        /// <summary>
        /// 상점을 연다. 이미 다른 상점이 열려있는 상태에서 호출해도(= 상점 이동 탭 클릭) 창을 닫았다 열지
        /// 않고 내용만 바뀐다. 기본으로 판매 탭이 먼저 보인다.
        /// </summary>
        public void Open(ShopData shop)
        {
            if (shop == null)
            {
                Debug.LogWarning("[ShopUI] Open 호출: shop이 null입니다.", this);
                return;
            }

            currentShop = shop;

            if (popupRoot != null) popupRoot.SetActive(true);
            if (shopNameText != null) shopNameText.text = shop.ShopName;

            RefreshShopEntryButtons();
            ShowSellTab();
        }

        public void Close()
        {
            currentShop = null;
            transactionPopup?.Close();
            if (popupRoot != null) popupRoot.SetActive(false);
        }

        /// <summary>지금 보고 있는 상점의 이동 탭 버튼만 interactable=false로 회색 표시한다.</summary>
        private void RefreshShopEntryButtons()
        {
            foreach (var entry in shopEntries)
            {
                if (entry == null || entry.button == null) continue;
                entry.button.interactable = entry.shop != currentShop;
            }
        }

        private void ShowBuyTab()
        {
            showingSellTab = false;

            if (buyTabButton != null) buyTabButton.interactable = false;
            if (sellTabButton != null) sellTabButton.interactable = true;

            RefreshBuyList();
        }

        private void ShowSellTab()
        {
            showingSellTab = true;

            if (buyTabButton != null) buyTabButton.interactable = true;
            if (sellTabButton != null) sellTabButton.interactable = false;

            RefreshSellList();
        }

        /// <summary>재료(Item_ID+Amount)가 실제로 설정되어 있는지. 재료 필드가 비어있으면(Item_ID 없음 또는 Amount 0) 골드 구매로 취급한다.</summary>
        private static bool HasMaterialCost(ShopItemData itemData)
        {
            var material = itemData.Purchase_Price_Material;
            return material != null && !string.IsNullOrEmpty(material.Item_ID) && material.Amount > 0;
        }

        /// <summary>골드 가격이 있거나 재료 비용이 있으면 구매 가능한 품목으로 취급한다.</summary>
        private static bool IsPurchasable(ShopItemData itemData)
        {
            return HasMaterialCost(itemData) || itemData.Purchase_Price_Golds > 0;
        }

        /// <summary>판매 가격이 0보다 크면 판매 가능한 품목으로 취급한다.</summary>
        private static bool IsSellable(ShopItemData itemData)
        {
            return itemData.Selling_Price > 0;
        }

        /// <summary>구매 가격 표시에 쓸 아이콘. 재료로 구매하면 그 재료의 아이콘, 아니면 골드 아이콘.</summary>
        private Sprite GetBuyCostIcon(ShopItemData itemData)
        {
            if (HasMaterialCost(itemData))
            {
                var materialItem = itemCatalogManager != null ? itemCatalogManager.FindItemData(itemData.Purchase_Price_Material.Item_ID) : null;
                return materialItem != null ? materialItem.ItemIcon : null;
            }

            return goldIcon;
        }

        /// <summary>1개당 구매 가격(재료로 구매하면 그 재료 필요 수량, 아니면 골드 가격).</summary>
        private static int GetBuyUnitPrice(ShopItemData itemData)
        {
            return HasMaterialCost(itemData) ? itemData.Purchase_Price_Material.Amount : itemData.Purchase_Price_Golds;
        }

        /// <summary>
        /// 구매 재고와 "지금 가진 재화로 실제 결제 가능한 수량" 중 작은 값을 구매 최대 수량으로 계산한다.
        /// 슬롯 표시용이 아니라(슬롯은 재고 그 자체를 보여줌) 거래 팝업을 열 때 진짜 최대 거래 가능
        /// 수량을 넘겨주기 위해 쓰인다.
        /// </summary>
        private int GetBuyMaxQuantity(ShopItemData itemData)
        {
            int stock = stockManager != null ? stockManager.GetPurchaseStock(itemData) : 0;
            if (stock <= 0) return 0;

            if (HasMaterialCost(itemData))
            {
                var material = itemData.Purchase_Price_Material;
                var materialInventory = MaterialInventory;
                int owned = materialInventory != null ? materialInventory.GetAmount(material.Item_ID) : 0;
                int affordable = material.Amount > 0 ? owned / material.Amount : 0;

                return Mathf.Min(stock, affordable);
            }
            else
            {
                int goldPrice = itemData.Purchase_Price_Golds;
                if (goldPrice <= 0) return 0; // 구매 불가능한 품목(IsPurchasable에서 이미 걸러지지만 방어적으로 처리)

                int gold = territoryData != null ? territoryData.Gold : 0;
                int affordable = gold / goldPrice;

                return Mathf.Min(stock, affordable);
            }
        }

        /// <summary>
        /// 판매 재고(상점이 사들일 수 있는 양)와 "지금 보유한 수량" 중 작은 값을 판매 최대 수량으로 계산한다.
        /// 슬롯 표시용이 아니라(슬롯은 재고 그 자체를 보여줌) 거래 팝업을 열 때 진짜 최대 거래 가능
        /// 수량을 넘겨주기 위해 쓰인다.
        /// </summary>
        private int GetSellMaxQuantity(ShopItemData itemData)
        {
            var materialInventory = MaterialInventory;
            int owned = materialInventory != null ? materialInventory.GetAmount(itemData.Item_ID) : 0;
            int sellStock = stockManager != null ? stockManager.GetSellStock(itemData) : 0;

            return Mathf.Min(owned, sellStock);
        }

        /// <summary>stockManager.GetShopItems(currentShop)에서 predicate를 만족하는 것만 골라 filteredItemsBuffer에 채운다.</summary>
        private List<ShopItemData> FilterCurrentShopItems(Func<ShopItemData, bool> predicate)
        {
            filteredItemsBuffer.Clear();

            if (stockManager == null) return filteredItemsBuffer;

            foreach (var itemData in stockManager.GetShopItems(currentShop))
            {
                if (itemData == null) continue;
                if (predicate(itemData)) filteredItemsBuffer.Add(itemData);
            }

            return filteredItemsBuffer;
        }

        /// <summary>기존에 만들어둔 슬롯을 재사용하고, 모자라면 새로 만든다(UpgradePopupUI.PopulateMaterialRows와 동일한 풀링 방식).</summary>
        private ShopSlotUI GetOrCreateSlot(int index)
        {
            if (index < spawnedSlots.Count) return spawnedSlots[index];

            var slot = Instantiate(slotPrefab, slotContainer);
            spawnedSlots.Add(slot);
            return slot;
        }

        private void HideExtraSlots(int usedCount)
        {
            for (int i = usedCount; i < spawnedSlots.Count; i++)
            {
                spawnedSlots[i].gameObject.SetActive(false);
            }
        }

        /// <summary>구매 가능한(IsPurchasable) 품목만 걸러서 공용 슬롯 컨테이너를 채운다. 슬롯에는 구매 재고 자체(GetPurchaseStock)를 표시한다.</summary>
        private void RefreshBuyList()
        {
            if (currentShop == null || slotPrefab == null || slotContainer == null) return;

            var items = FilterCurrentShopItems(IsPurchasable);

            for (int i = 0; i < items.Count; i++)
            {
                var itemData = items[i];

                var slot = GetOrCreateSlot(i);
                slot.gameObject.SetActive(true);

                var catalogItem = itemCatalogManager != null ? itemCatalogManager.FindItemData(itemData.Item_ID) : null;
                var costIcon = GetBuyCostIcon(itemData);
                int unitPrice = GetBuyUnitPrice(itemData);
                int stock = stockManager != null ? stockManager.GetPurchaseStock(itemData) : 0;

                slot.SetBuyData(itemData, catalogItem, costIcon, unitPrice, stock);
                slot.SetClickHandler(HandleBuySlotClicked);
            }

            HideExtraSlots(items.Count);
        }

        /// <summary>판매 가능한(IsSellable) 품목만 걸러서 공용 슬롯 컨테이너를 채운다. 슬롯에는 판매 재고 자체(GetSellStock)를 표시한다.</summary>
        private void RefreshSellList()
        {
            if (currentShop == null || slotPrefab == null || slotContainer == null) return;

            var items = FilterCurrentShopItems(IsSellable);

            for (int i = 0; i < items.Count; i++)
            {
                var itemData = items[i];

                var slot = GetOrCreateSlot(i);
                slot.gameObject.SetActive(true);

                var catalogItem = itemCatalogManager != null ? itemCatalogManager.FindItemData(itemData.Item_ID) : null;
                int stock = stockManager != null ? stockManager.GetSellStock(itemData) : 0;

                slot.SetSellData(itemData, catalogItem, goldIcon, itemData.Selling_Price, stock);
                slot.SetClickHandler(HandleSellSlotClicked);
            }

            HideExtraSlots(items.Count);
        }

        /// <summary>구매 탭 슬롯 클릭 -> 팝업을 구매 모드로 연다(여기서는 진짜 거래 가능 수량인 GetBuyMaxQuantity를 다시 계산해서 넘긴다).</summary>
        private void HandleBuySlotClicked(ShopItemData itemData)
        {
            if (transactionPopup == null) return;

            var catalogItem = itemCatalogManager != null ? itemCatalogManager.FindItemData(itemData.Item_ID) : null;
            var costIcon = GetBuyCostIcon(itemData);
            int unitPrice = GetBuyUnitPrice(itemData);
            int maxQuantity = GetBuyMaxQuantity(itemData);

            transactionPopup.Open(ShopSlotUI.Mode.Buy, catalogItem, itemData, costIcon, unitPrice, maxQuantity,
                quantity => ExecuteBuy(itemData, quantity));
        }

        /// <summary>판매 탭 슬롯 클릭 -> 팝업을 판매 모드로 연다(여기서는 진짜 거래 가능 수량인 GetSellMaxQuantity를 다시 계산해서 넘긴다).</summary>
        private void HandleSellSlotClicked(ShopItemData itemData)
        {
            if (transactionPopup == null) return;

            var catalogItem = itemCatalogManager != null ? itemCatalogManager.FindItemData(itemData.Item_ID) : null;
            int maxQuantity = GetSellMaxQuantity(itemData);

            transactionPopup.Open(ShopSlotUI.Mode.Sell, catalogItem, itemData, goldIcon, itemData.Selling_Price, maxQuantity,
                quantity => ExecuteSell(itemData, quantity));
        }

        /// <summary>
        /// 실제 구매를 실행한다(팝업 확인 버튼의 콜백으로 호출됨). 구매 재고 확인 -> 결제 -> 재고 차감 ->
        /// 인벤토리 지급 순서로 진행하고, 성공 여부를 반환해서 팝업이 닫힐지 결정하게 한다.
        /// </summary>
        private bool ExecuteBuy(ShopItemData itemData, int quantity)
        {
            int stock = stockManager != null ? stockManager.GetPurchaseStock(itemData) : 0;

            if (quantity <= 0 || quantity > stock)
            {
                Debug.LogWarning($"[ShopUI] 구매 재고보다 많은 수량을 구매하려 했습니다: {itemData.Item_ID} x{quantity} (재고 {stock})", this);
                return false;
            }

            if (!TryPayPurchaseCost(itemData, quantity))
            {
                Debug.LogWarning($"[ShopUI] 비용이 부족해 구매하지 못했습니다: {itemData.Item_ID} x{quantity}", this);
                return false;
            }

            stockManager?.ConsumePurchaseStock(itemData, quantity);
            GrantPurchasedItem(itemData, quantity);

            Debug.Log($"[ShopUI] 구매 완료: {itemData.Item_ID} x{quantity}");

            RefreshBuyList();
            return true;
        }

        /// <summary>
        /// 구매한 아이템을 지급한다. 창고에 먼저 채워 넣고, 창고 공간이 부족해 못 들어간 나머지만
        /// 플레이어 인벤토리로 넘어간다(반대 순서 아님). 둘 다 공간이 부족하면 남은 수량은 사라지지 않고
        /// 그냥 지급되지 못한 채 경고만 남긴다 - 이미 결제는 끝난 뒤라 되돌리지는 않는다.
        /// </summary>
        private void GrantPurchasedItem(ShopItemData itemData, int quantity)
        {
            int remaining = quantity;

            if (warehouseInventory != null) remaining = warehouseInventory.AddItem(itemData.Item_ID, remaining);
            if (remaining > 0 && playerInventory != null) remaining = playerInventory.AddItem(itemData.Item_ID, remaining);

            if (remaining > 0)
            {
                Debug.LogWarning($"[ShopUI] 창고와 인벤토리 공간이 부족해 {itemData.Item_ID} {remaining}개를 지급하지 못했습니다.", this);
            }
        }

        /// <summary>
        /// 실제 판매를 실행한다(팝업 확인 버튼의 콜백으로 호출됨). 판매 재고(상점이 사들일 수 있는 양)와
        /// 보유 수량을 모두 확인한 뒤 재료를 차감하고 골드를 지급하며, 판매 재고도 그만큼 줄인다.
        /// </summary>
        private bool ExecuteSell(ShopItemData itemData, int quantity)
        {
            var materialInventory = MaterialInventory;

            if (materialInventory == null)
            {
                Debug.LogWarning("[ShopUI] 재료 인벤토리가 연결되지 않아 판매할 수 없습니다.", this);
                return false;
            }

            int sellStock = stockManager != null ? stockManager.GetSellStock(itemData) : 0;

            if (quantity <= 0 || quantity > sellStock)
            {
                Debug.LogWarning($"[ShopUI] 판매 재고보다 많은 수량을 판매하려 했습니다: {itemData.Item_ID} x{quantity} (판매 재고 {sellStock})", this);
                return false;
            }

            if (!materialInventory.HasEnough(itemData.Item_ID, quantity))
            {
                Debug.LogWarning($"[ShopUI] 보유 수량보다 많은 수량을 판매하려 했습니다: {itemData.Item_ID} x{quantity}", this);
                return false;
            }

            materialInventory.Consume(itemData.Item_ID, quantity);
            territoryData?.AddGold(itemData.Selling_Price * quantity);
            stockManager?.ConsumeSellStock(itemData, quantity);

            Debug.Log($"[ShopUI] 판매 완료: {itemData.Item_ID} x{quantity}");

            RefreshSellList();
            return true;
        }

        /// <summary>
        /// 구매 비용을 확인하고 차감한다. 재료 비용이 있으면 그 재료 하나만 소비하고 골드는 요구하지
        /// 않는다 - 재료와 골드를 동시에 확인/차감하는 경우는 없다(기획 확정 사항).
        /// </summary>
        private bool TryPayPurchaseCost(ShopItemData itemData, int quantity)
        {
            if (HasMaterialCost(itemData))
            {
                var material = itemData.Purchase_Price_Material;
                var materialInventory = MaterialInventory;
                int needed = material.Amount * quantity;

                if (materialInventory == null)
                {
                    Debug.LogWarning("[ShopUI] 재료 인벤토리가 연결되지 않아 재료 결제를 할 수 없습니다.", this);
                    return false;
                }

                if (!materialInventory.HasEnough(material.Item_ID, needed)) return false;

                materialInventory.Consume(material.Item_ID, needed);
                return true;
            }
            else
            {
                int goldCost = itemData.Purchase_Price_Golds * quantity;

                if (territoryData == null)
                {
                    Debug.LogWarning("[ShopUI] territoryData가 비어있어 골드를 확인/차감할 수 없습니다.", this);
                    return false;
                }

                if (territoryData.Gold < goldCost) return false;

                territoryData.TrySpendGold(goldCost);
                return true;
            }
        }

        /// <summary>구매 재고 또는 판매 재고가 바뀌면 지금 보고 있는 탭(구매/판매)에 해당하는 목록만 다시 그린다.</summary>
        private void HandleStockChanged(ShopItemData itemData)
        {
            if (currentShop == null || stockManager == null) return;
            if (!stockManager.GetShopItems(currentShop).Contains(itemData)) return;

            if (showingSellTab) RefreshSellList();
            else RefreshBuyList();
        }

        /// <summary>상점이 재입고되면(구매/판매 재고 모두 리셋됨) 지금 보고 있는 탭에 해당하는 목록을 다시 그린다.</summary>
        private void HandleShopRestocked(ShopData shop)
        {
            if (currentShop != shop) return;

            if (showingSellTab) RefreshSellList();
            else RefreshBuyList();
        }
    }
}
