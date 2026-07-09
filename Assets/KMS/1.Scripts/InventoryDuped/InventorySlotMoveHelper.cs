using UnityEngine;
using HDY.Item;

namespace KMS.InventoryDuped
{
    /// <summary>
    /// [HDY 요청] 두 InventoryContainer 사이에서 슬롯 하나를 옮기거나(자리 교환) 합치는(스택 병합) 공용 로직.
    /// PlayerInventory(플레이어 인벤토리+퀵슬롯 내부 이동)와 WarehouseUI(창고↔인벤토리 간 이동)가 완전히
    /// 동일한 "옮기기/합치기" 규칙이 필요해서 공유한다. 잠긴 퀵슬롯 여부 같은 컨테이너별 특수 규칙은
    /// 호출하는 쪽에서 미리 걸러낸 뒤 이 헬퍼를 호출해야 한다(이 헬퍼는 그런 규칙을 모른다).
    /// </summary>
    public static class InventorySlotMoveHelper
    {
        /// <summary>
        /// fromContainer[fromIndex]의 아이템을 toContainer[toIndex]로 옮긴다. 대상이 비어있으면 그대로
        /// 이동, 같은 아이템이면 병합(MergeStack), 다른 아이템이면 서로 자리를 바꾼다.
        /// </summary>
        public static bool MoveSlot(InventoryContainer fromContainer, int fromIndex, InventoryContainer toContainer, int toIndex, ItemCatalogManager catalogManager)
        {
            if (fromContainer == null || toContainer == null) return false;
            if (!fromContainer.IsValidIndex(fromIndex) || !toContainer.IsValidIndex(toIndex)) return false;
            if (fromContainer == toContainer && fromIndex == toIndex) return false;

            ItemStack fromSlot = fromContainer.slots[fromIndex];
            ItemStack toSlot = toContainer.slots[toIndex];

            if (fromSlot.IsEmpty) return false;

            if (toSlot.IsEmpty)
            {
                toSlot.Set(fromSlot.itemId, fromSlot.amount);
                fromSlot.Clear();
                return true;
            }

            if (fromSlot.itemId == toSlot.itemId)
            {
                return MergeStack(fromSlot, toSlot, catalogManager);
            }

            string tempItemId = fromSlot.itemId;
            int tempAmount = fromSlot.amount;
            fromSlot.Set(toSlot.itemId, toSlot.amount);
            toSlot.Set(tempItemId, tempAmount);

            return true;
        }

        /// <summary>
        /// 같은 아이템 스택을 병합한다. 슬롯에는 itemId(string)만 있어 MaxStack을 알 수 없으므로,
        /// 카탈로그에서 ItemData를 다시 조회해서 MaxStack을 가져온다. 카탈로그에서 못 찾으면 안전하게
        /// 1개(스택 불가)로 취급한다.
        /// </summary>
        public static bool MergeStack(ItemStack fromSlot, ItemStack toSlot, ItemCatalogManager catalogManager)
        {
            var toItemData = catalogManager != null ? catalogManager.FindItemData(toSlot.itemId) : null;

            if (toItemData == null)
            {
                Debug.LogWarning($"[InventorySlotMoveHelper] MergeStack: Item_ID '{toSlot.itemId}'에 해당하는 ItemData를 찾을 수 없어 MaxStack을 1로 취급합니다.");
            }

            int maxStack = toItemData != null ? Mathf.Max(1, toItemData.MaxStack) : 1;
            int space = maxStack - toSlot.amount;

            if (space <= 0) return false;

            int moved = Mathf.Min(space, fromSlot.amount);

            toSlot.amount += moved;
            fromSlot.amount -= moved;

            if (fromSlot.amount <= 0) fromSlot.Clear();

            return true;
        }
    }
}
