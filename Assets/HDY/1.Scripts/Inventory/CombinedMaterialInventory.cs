using UnityEngine;
using HDY.Upgrade;
using KMS.InventoryDuped;

namespace HDY.Inventory
{
    /// <summary>
    /// IMaterialInventory의 실제 구현체. UpgradePopupUI가 재료 비용을 확인/차감할 때 이걸 사용한다
    /// (지금까지는 인터페이스만 있고 구현체가 없어서 재료 조건이 항상 통과되고 있었다).
    ///
    /// [확인 범위] 인벤토리(PlayerInventory)와 창고(WarehouseInventory)에 있는 수량을 합산해서 확인한다.
    /// [차감 순서] 인벤토리에서 먼저 차감하고, 부족한 만큼만 창고에서 차감한다.
    /// </summary>
    public class CombinedMaterialInventory : MonoBehaviour, IMaterialInventory
    {
        [Header("데이터 참조")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private WarehouseInventory warehouseInventory;

        private void Awake()
        {
            if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (warehouseInventory == null) warehouseInventory = FindFirstObjectByType<WarehouseInventory>();

            if (playerInventory == null) Debug.LogWarning("[CombinedMaterialInventory] playerInventory가 비어있습니다.", this);
            if (warehouseInventory == null) Debug.LogWarning("[CombinedMaterialInventory] warehouseInventory가 비어있습니다.", this);
        }

        /// <summary>인벤토리 + 창고에 있는 수량의 합이 amount 이상인지 확인한다.</summary>
        public bool HasEnough(string itemId, int amount)
        {
            int inventoryAmount = playerInventory != null ? playerInventory.GetItemAmount(itemId) : 0;
            int warehouseAmount = warehouseInventory != null ? warehouseInventory.GetItemAmount(itemId) : 0;

            return inventoryAmount + warehouseAmount >= amount;
        }

        /// <summary>인벤토리에서 먼저 차감하고, 부족한 만큼만 창고에서 차감한다. HasEnough로 이미 확인된 뒤 호출된다고 가정한다.</summary>
        public void Consume(string itemId, int amount)
        {
            if (playerInventory == null && warehouseInventory == null) return;

            int fromInventory = 0;

            if (playerInventory != null)
            {
                fromInventory = Mathf.Min(amount, playerInventory.GetItemAmount(itemId));
                if (fromInventory > 0) playerInventory.RemoveItem(itemId, fromInventory);
            }

            int remaining = amount - fromInventory;

            if (remaining > 0 && warehouseInventory != null)
            {
                warehouseInventory.RemoveItem(itemId, remaining);
            }
        }
    }
}
