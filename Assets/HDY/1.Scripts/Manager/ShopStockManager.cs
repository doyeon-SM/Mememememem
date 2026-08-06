using System;
using System.Collections.Generic;
using UnityEngine;
using HDY.Territory;
using HDY.Item;

namespace HDY.Shop
{
    /// <summary>
    /// 모든 상점의 "현재 재고"와 "다음 재입고까지 남은 인게임 하루 수"를 관리하는 런타임 매니저.
    /// ShopData(SO)는 정적 설정(품목 ID 목록, 재입고 주기)만 갖고, 실제로 몇 개 남았는지 같은
    /// 가변 상태는 이 컴포넌트가 메모리에 들고 있는다.
    ///
    /// [구매 재고 vs 판매 재고 - 별도 관리] 상점이 플레이어에게 파는 재고(구매 탭)와 플레이어에게서
    /// 사들이는 재고(판매 탭)는 서로 다른 풀(currentPurchaseStock / currentSellStock)로 관리한다.
    /// ShopItemData의 Purchase_MaxAmount와 Selling_MaxAmount도 각각 별개의 상한이다.
    ///
    /// [씬 배치 싱글톤] ItemCatalogManager와 동일한 패턴 - 씬에 하나 배치, Resolve()로 폴백 탐색.
    ///
    /// [HDY 요청 - 시트 마이그레이션] ShopData.Items(ShopItemData SO 리스트)가 ShopData.ItemIds
    /// (문자열 목록)로 바뀌면서, 이 매니저가 Start 시점에 한 번 ItemCatalogManager.FindShopItemData(id)로
    /// 실제 ShopItemData 인스턴스를 resolve해서 상점별로 캐싱해둔다(resolvedShopItems). 아래
    /// currentPurchaseStock/currentSellStock 딕셔너리는 여전히 ShopItemData 객체 자체를 키로 쓰기 때문에,
    /// 매번 새로 resolve하지 않고 이 캐싱된 같은 인스턴스를 계속 재사용하는 게 중요하다(그렇지 않으면
    /// 딕셔너리 조회가 항상 실패한다). ShopUI 등 외부에서 "이 상점의 품목 목록"이 필요하면
    /// shop.ItemIds를 직접 순회하지 말고 반드시 GetShopItems(shop)을 사용해야 한다.
    ///
    /// [인게임 시간 기반 재입고] 예전에는 DateTime.UtcNow(리얼타임)로 다음 재입고 시각을 직접 계산해서
    /// 매 프레임 비교했지만, 이제는 GameTimeManager의 인게임 하루(기본 20분 = TerritoryData.ElapsedTime
    /// 1200초)를 기준으로 삼는다. 상점별 ShopData.RestockIntervalMinutes를 GameTimeManager.DayLengthSeconds로
    /// 나눠 "며칠(인게임 하루 수)마다 재입고할지"로 환산해두고(restockIntervalDays, 최소 1일), 인게임 하루가
    /// 넘어갈 때마다(GameTimeManager.OnInGameDayChanged) 각 상점이 그만큼의 하루가 지났는지 확인해서
    /// 재입고한다. 리얼타임 폴링(Update)이 사라지고 이벤트 구독으로 바뀌었다 - 게임이 실제로 진행되는
    /// 동안(TerritoryData.ElapsedTime이 쌓이는 동안)만 재입고 타이머가 흐른다(예전처럼 앱을 꺼놨다 켜도
    /// 그 사이 지난 리얼타임만큼 즉시 재입고되는 일은 이제 없다).
    ///
    /// [GetTimeUntilRestock도 인게임 시간 기준] ShopUI의 재입고 카운트다운(mm:ss) 표시가 이 메서드를 쓰는데,
    /// 이제 리얼타임이 아니라 "인게임 시간으로 앞으로 몇 초 후 재입고되는지"를 TimeSpan으로 환산해서
    /// 반환한다 - 표시 형식(mm:ss)은 그대로라 ShopUI 쪽은 손댈 필요가 없다.
    ///
    /// [TODO: 세이브/로드] 지금은 저장 시스템이 없어 앱을 껐다 켜면 재고/lastRestockedDay가 초기화된다.
    /// 나중에 세이브 시스템이 생기면 상점별 lastRestockedDay(및 두 재고 풀)를 저장/복원하도록 이어붙이면 된다.
    /// </summary>
    public class ShopStockManager : MonoBehaviour
    {
        public static ShopStockManager Instance { get; private set; }

        [Header("데이터 참조 (비어있으면 자동 탐색)")]
        [SerializeField] private GameTimeManager gameTimeManager;
        [SerializeField] private ItemCatalogManager itemCatalogManager;

        [Header("상점 목록 (인스펙터에서 등록)")]
        [SerializeField] private List<ShopData> allShops = new List<ShopData>();

        /// <summary>상점별로 ItemIds를 카탈로그에서 resolve해 캐싱해둔 실제 ShopItemData 목록.</summary>
        private readonly Dictionary<ShopData, List<ShopItemData>> resolvedShopItems = new Dictionary<ShopData, List<ShopItemData>>();
        private static readonly List<ShopItemData> EmptyShopItems = new List<ShopItemData>();

        private readonly Dictionary<ShopItemData, int> currentPurchaseStock = new Dictionary<ShopItemData, int>();
        private readonly Dictionary<ShopItemData, int> currentSellStock = new Dictionary<ShopItemData, int>();

        /// <summary>상점이 마지막으로 재입고된 인게임 날짜(GameTimeManager.CurrentInGameDay 기준).</summary>
        private readonly Dictionary<ShopData, int> lastRestockedDay = new Dictionary<ShopData, int>();

        /// <summary>ShopData.RestockIntervalMinutes를 인게임 하루 수로 환산한 값(최소 1일).</summary>
        private readonly Dictionary<ShopData, int> restockIntervalDays = new Dictionary<ShopData, int>();

        /// <summary>구매 재고 또는 판매 재고가 바뀔 때마다(소비되거나 재입고로 리셋될 때) 발행. UI가 구독해서 해당 슬롯만 갱신할 수 있다.</summary>
        public event Action<ShopItemData> OnStockChanged;

        /// <summary>어떤 상점이 재입고되었을 때 발행(그 상점을 보고 있는 UI가 전체를 새로고침하는 데 사용).</summary>
        public event Action<ShopData> OnShopRestocked;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ShopStockManager] 씬에 ShopStockManager가 이미 있어 중복 오브젝트를 파괴합니다.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            gameTimeManager = GameTimeManager.Resolve(gameTimeManager);
            if (gameTimeManager == null) Debug.LogWarning("[ShopStockManager] gameTimeManager를 찾을 수 없습니다. 인게임 시간 기반 재입고가 동작하지 않습니다.", this);

            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);
            if (itemCatalogManager == null) Debug.LogWarning("[ShopStockManager] itemCatalogManager를 찾을 수 없습니다. 상점 품목을 resolve할 수 없습니다.", this);
        }

        private void Start()
        {
            // GameTimeManager.CurrentInGameDay/DayLengthSeconds는 GameTimeManager.Awake에서 이미
            // 동기화되어 있으므로(Unity가 모든 Awake가 끝난 뒤 Start를 호출함을 보장), 여기서 안전하게
            // 참조할 수 있다.
            InitializeAllShops();
        }

        private void OnEnable()
        {
            if (gameTimeManager != null) gameTimeManager.OnInGameDayChanged += HandleInGameDayChanged;
        }

        private void OnDisable()
        {
            if (gameTimeManager != null) gameTimeManager.OnInGameDayChanged -= HandleInGameDayChanged;
        }

        /// <summary>
        /// 상점의 ItemIds를 카탈로그에서 resolve해 실제 ShopItemData 목록을 만들고 캐싱한다.
        /// 이미 resolve된 상점이면 캐싱된 목록을 그대로 반환한다.
        /// </summary>
        private List<ShopItemData> ResolveShopItems(ShopData shop)
        {
            if (resolvedShopItems.TryGetValue(shop, out var cached)) return cached;

            var resolved = new List<ShopItemData>();
            if (itemCatalogManager != null && shop.ItemIds != null)
            {
                foreach (var itemId in shop.ItemIds)
                {
                    var shopItem = itemCatalogManager.FindShopItemData(itemId);
                    if (shopItem == null)
                    {
                        Debug.LogWarning($"[ShopStockManager] 상점 '{shop.ShopName}'의 Item_ID '{itemId}'를 카탈로그에서 찾을 수 없습니다.");
                        continue;
                    }

                    resolved.Add(shopItem);
                }
            }

            resolvedShopItems[shop] = resolved;
            return resolved;
        }

        /// <summary>
        /// 상점(ShopData)이 실제로 취급하는 ShopItemData 목록을 반환한다(ItemIds가 카탈로그에서 resolve된 결과).
        /// ShopUI 등 외부 코드는 shop.ItemIds를 직접 순회하지 말고 이 메서드를 사용해야 한다 - 여기서 반환하는
        /// 객체가 currentPurchaseStock/currentSellStock 딕셔너리의 키와 동일한 인스턴스이기 때문이다.
        /// </summary>
        public IReadOnlyList<ShopItemData> GetShopItems(ShopData shop)
        {
            if (shop == null) return EmptyShopItems;

            return resolvedShopItems.TryGetValue(shop, out var resolved) ? resolved : ResolveShopItems(shop);
        }

        /// <summary>시작 시 모든 상점의 품목 구매/판매 재고를 각자의 최대치로 채우고, RestockIntervalMinutes를
        /// 인게임 하루 수로 환산해두고, "지금 막 재입고한 것"으로 기준일을 맞춰둔다.</summary>
        private void InitializeAllShops()
        {
            int currentDay = gameTimeManager != null ? gameTimeManager.CurrentInGameDay : 0;
            float dayLengthMinutes = gameTimeManager != null ? gameTimeManager.DayLengthSeconds / 60f : 20f;

            foreach (var shop in allShops)
            {
                if (shop == null) continue;

                var items = ResolveShopItems(shop);
                foreach (var item in items)
                {
                    if (item == null) continue;
                    currentPurchaseStock[item] = item.Purchase_MaxAmount;
                    currentSellStock[item] = item.Selling_MaxAmount;
                }

                int intervalDays = Mathf.Max(1, Mathf.RoundToInt(shop.RestockIntervalMinutes / Mathf.Max(0.0001f, dayLengthMinutes)));
                restockIntervalDays[shop] = intervalDays;
                lastRestockedDay[shop] = currentDay;
            }
        }

        /// <summary>인게임 하루가 넘어갈 때마다(GameTimeManager.OnInGameDayChanged) 호출된다. 각 상점이
        /// 자신의 재입고 주기(하루 수)만큼 지났는지 확인해서, 지났으면 재입고한다.</summary>
        private void HandleInGameDayChanged()
        {
            if (gameTimeManager == null) return;

            int currentDay = gameTimeManager.CurrentInGameDay;

            foreach (var shop in allShops)
            {
                if (shop == null) continue;
                if (!lastRestockedDay.TryGetValue(shop, out int lastDay)) continue;
                if (!restockIntervalDays.TryGetValue(shop, out int intervalDays)) continue;

                if (currentDay - lastDay >= intervalDays)
                {
                    RestockShop(shop, currentDay);
                }
            }
        }

        private void RestockShop(ShopData shop, int currentDay)
        {
            foreach (var item in GetShopItems(shop))
            {
                if (item == null) continue;

                currentPurchaseStock[item] = item.Purchase_MaxAmount;
                currentSellStock[item] = item.Selling_MaxAmount;
                OnStockChanged?.Invoke(item);
            }

            lastRestockedDay[shop] = currentDay;

            Debug.Log($"[ShopStockManager] 상점 재입고 완료: {shop.ShopName}");

            OnShopRestocked?.Invoke(shop);
        }

        /// <summary>item의 현재 구매 재고(상점이 플레이어에게 팔 수 있는 양)를 반환한다. 등록되지 않은 아이템이면 0.</summary>
        public int GetPurchaseStock(ShopItemData item)
        {
            if (item == null) return 0;
            return currentPurchaseStock.TryGetValue(item, out int amount) ? amount : 0;
        }

        /// <summary>item의 현재 판매 재고(상점이 플레이어에게서 사들일 수 있는 양)를 반환한다. 등록되지 않은 아이템이면 0.</summary>
        public int GetSellStock(ShopItemData item)
        {
            if (item == null) return 0;
            return currentSellStock.TryGetValue(item, out int amount) ? amount : 0;
        }

        /// <summary>구매 재고를 amount만큼 차감한다(재고 확인은 호출부 책임). 0 미만으로는 내려가지 않는다.</summary>
        public void ConsumePurchaseStock(ShopItemData item, int amount)
        {
            if (item == null || amount <= 0) return;

            int current = GetPurchaseStock(item);
            currentPurchaseStock[item] = Mathf.Max(0, current - amount);

            OnStockChanged?.Invoke(item);
        }

        /// <summary>판매 재고를 amount만큼 차감한다(재고 확인은 호출부 책임). 0 미만으로는 내려가지 않는다.</summary>
        public void ConsumeSellStock(ShopItemData item, int amount)
        {
            if (item == null || amount <= 0) return;

            int current = GetSellStock(item);
            currentSellStock[item] = Mathf.Max(0, current - amount);

            OnStockChanged?.Invoke(item);
        }

        /// <summary>
        /// 해당 상점이 다음 재입고까지 남은 시간을 인게임 시간 기준으로 계산해서 반환한다. 등록되지
        /// 않았거나 gameTimeManager가 없으면 TimeSpan.Zero. ShopUI의 재입고 카운트다운(mm:ss) 표시에 쓰인다.
        /// </summary>
        public TimeSpan GetTimeUntilRestock(ShopData shop)
        {
            if (shop == null || gameTimeManager == null) return TimeSpan.Zero;
            if (!lastRestockedDay.TryGetValue(shop, out int lastDay)) return TimeSpan.Zero;
            if (!restockIntervalDays.TryGetValue(shop, out int intervalDays)) return TimeSpan.Zero;

            int nextRestockDay = lastDay + intervalDays;
            int daysRemaining = nextRestockDay - gameTimeManager.CurrentInGameDay;

            float remainingSeconds = daysRemaining * gameTimeManager.DayLengthSeconds - gameTimeManager.InGameTimeOfDaySeconds;
            remainingSeconds = Mathf.Max(0f, remainingSeconds);

            return TimeSpan.FromSeconds(remainingSeconds);
        }

        /// <summary>
        /// 다른 스크립트가 들고 있는 ShopStockManager 참조가 비어있을 때 쓰는 공용 폴백 탐색.
        /// ItemCatalogManager.Resolve와 동일한 패턴(1. 기존 참조 2. 싱글톤 3. 씬 전체 검색).
        /// </summary>
        public static ShopStockManager Resolve(ShopStockManager existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<ShopStockManager>();
            if (found == null)
            {
                Debug.LogWarning("[ShopStockManager] 씬에서 ShopStockManager를 찾을 수 없습니다.");
            }

            return found;
        }
    }
}
