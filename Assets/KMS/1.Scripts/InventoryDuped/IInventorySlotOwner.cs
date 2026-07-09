using UnityEngine;

namespace KMS.InventoryDuped
{
    /// <summary>
    /// 인벤토리 슬롯이 어느 컨테이너 그룹에 속하는지 구분한다.
    /// [HDY 요청] 기존에는 bool(isQuickSlot) 하나로 인벤토리/퀵슬롯 2그룹만 구분했는데,
    /// 창고(Storage)가 추가되며 3그룹이 되어 enum으로 일반화했다.
    /// </summary>
    public enum SlotGroup
    {
        Inventory,
        QuickSlot,
        Storage
    }

    /// <summary>
    /// [HDY 요청] InventorySlotUI가 드래그/툴팁 상호작용을 위임하는 대상의 계약.
    /// 기존에는 InventorySlotUI.owner가 InventoryUI 타입으로 고정되어 있었는데, 창고 UI(WarehouseUI)도
    /// 같은 InventorySlotUI 컴포넌트를 재사용해서 창고↔인벤토리↔퀵슬롯 통합 드래그를 지원해야 해서
    /// 인터페이스로 일반화했다. InventoryUI(플레이어 전용 화면)와 WarehouseUI(창고+인벤토리 통합 화면)
    /// 둘 다 이 인터페이스를 구현한다.
    /// </summary>
    public interface IInventorySlotOwner
    {
        void BeginSlotDrag(InventorySlotUI source, ItemStack stack, Vector2 position);
        void MoveSlotDrag(Vector2 position);
        void EndSlotDrag(InventorySlotUI target);
        void ShowItemTooltip(ItemStack stack, Vector2 position);
        void MoveItemTooltip(Vector2 position);
        void HideItemTooltip();
    }
}
