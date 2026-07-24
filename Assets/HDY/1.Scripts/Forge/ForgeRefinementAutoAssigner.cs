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
    /// </summary>
    public class ForgeRefinementAutoAssigner : MonoBehaviour
    {
        [Header("참조 (비워두면 씬에서 자동으로 찾음)")]
        [SerializeField] private ForgeManager forgeManager;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private WarehouseInventory warehouseInventory;

        private void Awake()
        {
            if (forgeManager == null) forgeManager = ForgeManager.Instance;
            if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (warehouseInventory == null) warehouseInventory = FindFirstObjectByType<WarehouseInventory>();
        }

        private void OnEnable()
        {
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
            if (playerInventory != null)
            {
                if (subscribe) playerInventory.OnInventoryChanged += HandleInventoryChanged;
                else playerInventory.OnInventoryChanged -= HandleInventoryChanged;
            }

            if (warehouseInventory != null)
            {
                if (subscribe) warehouseInventory.OnStorageChanged += HandleInventoryChanged;
                else warehouseInventory.OnStorageChanged -= HandleInventoryChanged;
            }
        }

        private void HandleInventoryChanged()
        {
            ScanAllContainers();
        }

        private void ScanAllContainers()
        {
            if (forgeManager == null) return;

            if (playerInventory != null)
            {
                ScanContainer(playerInventory.inventory);
                ScanContainer(playerInventory.quickSlots);
            }

            if (warehouseInventory != null)
            {
                ScanContainer(warehouseInventory.storage);
            }
        }

        private void ScanContainer(InventoryContainer container)
        {
            if (container?.slots == null) return;

            foreach (var slot in container.slots)
            {
                if (slot == null || slot.IsEmpty) continue;

                // 이미 합성 ID(=이미 처리됨)면 TryEnsureRefinementInstance 내부에서 슬롯 누락만 방어적으로 채우고 조용히 지나간다.
                forgeManager.TryEnsureRefinementInstance(slot);
            }
        }
    }
}
