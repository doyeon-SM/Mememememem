using UnityEngine;
using KMS.InventoryDuped;

namespace HDY.Inventory
{
    /// <summary>
    /// [HDY 요청] 병합 없이 무조건 덮어쓰는 트래시(휴지통) 슬롯 1칸의 데이터+동작을 담당하는 공용 헬퍼.
    /// WarehouseUI와 InventoryUI 양쪽에서 완전히 동일한 트래시 규칙(덮어쓰기, 강제 수납)이 필요해서
    /// 이 클래스로 추출했다. 트래시 내용물(trashStack)은 패널마다 독립적인 로컬 상태이므로, 각 패널이
    /// 이 클래스의 인스턴스를 하나씩 따로 소유한다(데이터 공유가 아니라 로직만 공유).
    /// </summary>
    public class TrashSlotController
    {
        private readonly ItemStack trashStack = new ItemStack();
        private InventorySlotUI slotUI;

        /// <summary>현재 트래시에 들어있는 수량(0이면 비어있음).</summary>
        public int CurrentAmount => trashStack.IsEmpty ? 0 : trashStack.amount;

        public void Initialize(IInventorySlotOwner owner, InventorySlotUI trashSlotUI)
        {
            slotUI = trashSlotUI;
            slotUI?.Initialize(owner, SlotGroup.Trash, 0);
        }

        public bool TryTakeAmount(int amount, out ItemStack taken)
        {
            taken = null;
            if (trashStack.IsEmpty || amount <= 0) return false;

            int takenAmount = Mathf.Min(amount, trashStack.amount);
            taken = new ItemStack { itemId = trashStack.itemId, amount = takenAmount };

            trashStack.amount -= takenAmount;
            if (trashStack.amount <= 0) trashStack.Clear();

            Refresh();
            return true;
        }

        /// <summary>트래시는 병합하지 않고 무조건 덮어쓴다 - 기존에 있던 아이템은 그대로 삭제되며 복구되지 않는다.</summary>
        public bool Place(ItemStack held, int amount)
        {
            if (held == null || held.IsEmpty || amount <= 0) return false;

            int placeAmount = Mathf.Min(amount, held.amount);

            trashStack.Set(held.itemId, placeAmount);
            held.amount -= placeAmount;
            if (held.amount <= 0) held.Clear();

            Refresh();
            return true;
        }

        /// <summary>[안전장치] 손에 든 아이템을 놓을 자리가 전혀 없을 때 최종적으로 강제 수납한다(실패 케이스 없음).</summary>
        public void ForcePlace(ItemStack held)
        {
            if (held == null || held.IsEmpty) return;

            string itemId = held.itemId;
            int amount = held.amount;

            Place(held, held.amount);

            Debug.LogWarning($"[TrashSlotController] 반환할 공간이 없어 '{itemId}' x{amount}을(를) 트래시 슬롯에 강제로 넣었습니다.");
        }

        /// <summary>수량 팝업 표시용으로 트래시 내용물의 복사본을 반환한다. 비어있으면 null.</summary>
        public ItemStack Snapshot()
        {
            return trashStack.IsEmpty ? null : new ItemStack { itemId = trashStack.itemId, amount = trashStack.amount };
        }

        public void Refresh()
        {
            slotUI?.SetStack(trashStack);
        }
    }
}
