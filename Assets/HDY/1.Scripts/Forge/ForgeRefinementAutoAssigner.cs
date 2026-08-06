using HDY.Inventory;
using KMS.InventoryDuped;
using UnityEngine;

namespace HDY.Forge
{
    /// <summary>
    /// 도구가 "제작 즉시" 연마 슬롯을 가진 것처럼 만들어주는 훅.
    ///
    /// 실제 도구 제작은 Kyusoo의 제작대(ProductionCraftRuntime.CollectCraftedItems)가
    /// PlayerInventory.AddItem(KMS 소유)을 호출해서 이루어진다. 이 두 파일은 크로스팀 소유라 직접
    /// 수정하지 않고, 대신 기존에 이미 있는 PlayerInventory.OnInventoryChanged / WarehouseInventory.OnStorageChanged
    /// 이벤트(ForgeUI.cs에서도 동일하게 구독 중)를 감시해서, 아직 강화 개체로 등록되지 않은(합성 ID가
    /// 아닌) 연마 가능 도구(도끼/곡괭이/괭이)를 발견하는 즉시 ForgeManager.TryEnsureRefinementInstance를
    /// 호출해 인스턴스 생성 + 연마 슬롯 채움을 처리한다. 제작대에서 수령하는 순간 이 이벤트가 바로
    /// 발생하므로 체감상 "제작 즉시"와 동일하다.
    ///
    /// [버그 수정] 기존에는 forgeManager/playerInventory/warehouseInventory를 Awake()에서 딱 한 번만
    /// 찾아 캐싱했다. 스크립트 실행 순서는 보장되지 않으므로, 이 컴포넌트의 초기화 시점에 ForgeManager나
    /// PlayerInventory가 아직 준비되기 전이면 참조가 계속 null로 남아 이 훅 자체가 조용히 아무 일도
    /// 하지 않는 문제가 있었다(재시도 없음). ForgeManager.MaterialInventory에서 이미 쓰고 있는
    /// lazy-resolve 패턴과 동일하게, 참조가 비어있거나 파괴됐으면(Unity Object의 == null 비교가 파괴
    /// 여부도 함께 판정해줌) 그때그때 다시 찾도록 바꿔서 초기화 순서나 씬 전환과 무관하게 항상 안전하게
    /// 동작하도록 했다.
    /// </summary>
    public class ForgeRefinementAutoAssigner : MonoBehaviour
    {
        [Header("참조 (비워두면 씬에서 자동으로 찾음 - 필요할 때마다 재확인함)")]
        [SerializeField] private ForgeManager forgeManager;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private WarehouseInventory warehouseInventory;

        /// <summary>비어있거나 파괴됐으면 ForgeManager.Instance로 다시 찾는다.</summary>
        private ForgeManager ForgeManagerRef
        {
            get
            {
                if (forgeManager == null) forgeManager = ForgeManager.Instance;
                return forgeManager;
            }
        }

        /// <summary>비어있거나 파괴됐으면 씬에서 다시 찾는다.</summary>
        private PlayerInventory PlayerInventoryRef
        {
            get
            {
                if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
                return playerInventory;
            }
        }

        /// <summary>비어있거나 파괴됐으면 씬에서 다시 찾는다.</summary>
        private WarehouseInventory WarehouseInventoryRef
        {
            get
            {
                if (warehouseInventory == null) warehouseInventory = FindFirstObjectByType<WarehouseInventory>();
                return warehouseInventory;
            }
        }

        private void OnEnable()
        {
            // [수정] Awake 1회성 탐색 대신, 활성화될 때마다 프로퍼티로 재확인한 참조로 구독한다.
            SubscribeEvents(true);

            // 씬 재진입 등으로 이미 들어와 있던 도구도 놓치지 않도록 최초 1회 스캔한다.
            ScanAllContainers();
        }

        private void OnDisable()
        {
            SubscribeEvents(false);
        }

        private void SubscribeEvents(bool subscribe)
        {
            var inventory = PlayerInventoryRef;
            if (inventory != null)
            {
                if (subscribe) inventory.OnInventoryChanged += HandleInventoryChanged;
                else inventory.OnInventoryChanged -= HandleInventoryChanged;
            }

            var warehouse = WarehouseInventoryRef;
            if (warehouse != null)
            {
                if (subscribe) warehouse.OnStorageChanged += HandleInventoryChanged;
                else warehouse.OnStorageChanged -= HandleInventoryChanged;
            }
        }

        private void HandleInventoryChanged()
        {
            ScanAllContainers();
        }

        private void ScanAllContainers()
        {
            var manager = ForgeManagerRef;
            if (manager == null) return;

            var inventory = PlayerInventoryRef;
            if (inventory != null)
            {
                ScanContainer(manager, inventory.inventory);
                ScanContainer(manager, inventory.quickSlots);
            }

            var warehouse = WarehouseInventoryRef;
            if (warehouse != null)
            {
                ScanContainer(manager, warehouse.storage);
            }
        }

        private void ScanContainer(ForgeManager manager, InventoryContainer container)
        {
            if (container?.slots == null) return;

            foreach (var slot in container.slots)
            {
                if (slot == null || slot.IsEmpty) continue;

                // 이미 합성 ID(=이미 처리됨)면 TryEnsureRefinementInstance 내부에서 슬롯 누락만 방어적으로 채우고 조용히 지나간다.
                manager.TryEnsureRefinementInstance(slot);
            }
        }
    }
}
