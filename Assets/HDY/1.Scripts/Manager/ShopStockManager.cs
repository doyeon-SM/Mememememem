using System;
using System.Collections.Generic;
using UnityEngine;

namespace HDY.Shop
{
    /// <summary>
    /// 모든 상점의 "현재 재고"와 "다음 재입고 시각"을 관리하는 런타임 매니저.
    /// ShopData(SO)는 정적 설정(품목 목록, 재입고 주기)만 갖고, 실제로 몇 개 남았는지 같은
    /// 가변 상태는 이 컴포넌트가 메모리에 들고 있는다.
    ///
    /// [구매 재고 vs 판매 재고 - 별도 관리] 상점이 플레이어에게 파는 재고(구매 탭)와 플레이어에게서
    /// 사들이는 재고(판매 탭)는 서로 다른 풀(currentPurchaseStock / currentSellStock)로 관리한다.
    /// ShopItemData의 Purchase_MaxAmount와 Selling_MaxAmount도 각각 별개의 상한이다.
    ///
    /// [씬 배치 싱글톤] ItemCatalogManager와 동일한 패턴 - 씬에 하나 배치, Resolve()로 폴백 탐색.
    ///
    /// [리얼타임 재입고] DateTime.UtcNow 기준으로 상점별 "다음 재입고 시각"을 Update에서 매 프레임
    /// 비교한다. 도달하면 그 상점에 속한 모든 품목의 구매 재고와 판매 재고를 각자의 최대치로 동시에
    /// 리셋하고(같은 재입고 타이머 하나를 공유) 다음 시각을 다시 계산한다.
    ///
    /// [TODO: 세이브/로드] 지금은 저장 시스템이 없어 앱을 껐다 켜면 재고/다음 재입고 시각이 초기화된다.
    /// 나중에 세이브 시스템이 생기면 상점별 nextRestockTimeUtc(및 두 재고 풀)를 저장/복원하도록
    /// 이어붙이면 된다.
    /// </summary>
    public class ShopStockManager : MonoBehaviour
    {
        public static ShopStockManager Instance { get; private set; }

        [Header("상점 목록 (인스펙터에서 등록)")]
        [SerializeField] private List<ShopData> allShops = new List<ShopData>();

        private readonly Dictionary<ShopItemData, int> currentPurchaseStock = new Dictionary<ShopItemData, int>();
        private readonly Dictionary<ShopItemData, int> currentSellStock = new Dictionary<ShopItemData, int>();
        private readonly Dictionary<ShopData, DateTime> nextRestockTimeUtc = new Dictionary<ShopData, DateTime>();

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

            InitializeAllShops();
        }

        private void Update()
        {
            var now = DateTime.UtcNow;

            foreach (var shop in allShops)
            {
                if (shop == null) continue;
                if (!nextRestockTimeUtc.TryGetValue(shop, out var nextTime)) continue;

                if (now >= nextTime)
                {
                    RestockShop(shop, now);
                }
            }
        }

        /// <summary>시작 시 모든 상점의 품목 구매/판매 재고를 각자의 최대치로 채우고, 다음 재입고 시각을 지금부터 계산한다.</summary>
        private void InitializeAllShops()
        {
            var now = DateTime.UtcNow;

            foreach (var shop in allShops)
            {
                if (shop == null) continue;

                foreach (var item in shop.Items)
                {
                    if (item == null) continue;
                    currentPurchaseStock[item] = item.Purchase_MaxAmount;
                    currentSellStock[item] = item.Selling_MaxAmount;
                }

                nextRestockTimeUtc[shop] = now.AddMinutes(Mathf.Max(1, shop.RestockIntervalMinutes));
            }
        }

        private void RestockShop(ShopData shop, DateTime now)
        {
            foreach (var item in shop.Items)
            {
                if (item == null) continue;

                currentPurchaseStock[item] = item.Purchase_MaxAmount;
                currentSellStock[item] = item.Selling_MaxAmount;
                OnStockChanged?.Invoke(item);
            }

            nextRestockTimeUtc[shop] = now.AddMinutes(Mathf.Max(1, shop.RestockIntervalMinutes));

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

        /// <summary>해당 상점이 다음 재입고까지 남은 시간을 반환한다. 등록되지 않았으면 TimeSpan.Zero.</summary>
        public TimeSpan GetTimeUntilRestock(ShopData shop)
        {
            if (shop == null || !nextRestockTimeUtc.TryGetValue(shop, out var nextTime)) return TimeSpan.Zero;

            var remaining = nextTime - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
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
