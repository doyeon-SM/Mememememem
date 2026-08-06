using System;
using UnityEngine;
using UnityEngine.UI;
using KMS.InventoryDuped;
using HDY.Upgrade;

namespace HDY.Inventory
{
    /// <summary>
    /// [HDY 요청] 인벤토리+퀵슬롯 그리드 관리(바인딩/갱신/잠금 표시/정렬·업그레이드 처리)를 전담하는 공용
    /// 헬퍼. WarehouseUI(창고+인벤토리+퀵슬롯+트래시를 하나의 커서로 통합 조작)와 InventoryUI(인벤토리+
    /// 퀵슬롯+트래시만 단독 조작)가 서로 다른 커서(heldStack) 상태를 각자 유지하면서도 이 클래스의 로직은
    /// 완전히 동일하게 재사용하도록, 일부러 MonoBehaviour가 아닌 일반 C# 클래스로 만들었다(각 패널이
    /// new PlayerInventoryGridController(playerInventory)로 인스턴스 하나씩을 직접 소유한다).
    ///
    /// [책임 범위] 이 클래스는 "인벤토리/퀵슬롯 그룹"에 대한 처리만 안다 - 창고(Storage)나 트래시(Trash)
    /// 그룹, 그리고 여러 그룹을 넘나드는 커서 상태 자체는 각 패널(WarehouseUI/InventoryUI)이 직접 관리한다.
    /// 이 클래스는 커서를 갖지 않으므로 TryTake/TryPlace 계열 메서드는 항상 PlayerInventory에 있는 로직을
    /// 그대로 위임만 하고, 실제로 든 아이템이 어디서 왔는지/어디로 가는지는 호출부(각 패널)가 판단한다.
    /// </summary>
    public class PlayerInventoryGridController
    {
        public PlayerInventory PlayerInventory { get; }
        public InventorySlotUI[] InventorySlots { get; private set; }
        public InventorySlotUI[] QuickSlots { get; private set; }

        /// <summary>아직 언락되지 않은 인벤토리 칸의 표시 투명도(0~1). 낮을수록 더 흐리게(회색처럼) 보인다.</summary>
        public float LockedSlotAlpha = 0.35f;

        public PlayerInventoryGridController(PlayerInventory playerInventory)
        {
            PlayerInventory = playerInventory;
        }

        // ===================== 슬롯 바인딩 =====================

        /// <summary>인벤토리/퀵슬롯은 씬에 미리 배치된 슬롯을 그대로 수집해서 owner/그룹/인덱스를 초기화한다.</summary>
        public void BindSlots(IInventorySlotOwner owner, Transform inventoryGrid, Transform quickSlotRoot)
        {
            InventorySlots = BindSlotGroup(owner, inventoryGrid, PlayerInventory.inventory.slots.Length, SlotGroup.Inventory);
            QuickSlots = BindSlotGroup(owner, quickSlotRoot, PlayerInventory.quickSlots.slots.Length, SlotGroup.QuickSlot);
        }

        private static InventorySlotUI[] BindSlotGroup(IInventorySlotOwner owner, Transform root, int count, SlotGroup group)
        {
            InventorySlotUI[] result = new InventorySlotUI[count];
            if (root == null) return result;

            for (int i = 0; i < count && i < root.childCount; i++)
            {
                InventorySlotUI slotUI = root.GetChild(i).GetComponent<InventorySlotUI>();
                result[i] = slotUI;

                if (slotUI != null) slotUI.Initialize(owner, group, i);
            }

            return result;
        }

        // ===================== 갱신 =====================

        public void RefreshInventorySlots()
        {
            if (InventorySlots == null) return;

            for (int i = 0; i < InventorySlots.Length; i++)
            {
                if (InventorySlots[i] != null) InventorySlots[i].SetStack(PlayerInventory.inventory.slots[i]);
            }
        }

        public void RefreshQuickSlots()
        {
            if (QuickSlots == null) return;

            for (int i = 0; i < QuickSlots.Length; i++)
            {
                RefreshQuickSlot(i);
            }
        }

        public void RefreshQuickSlot(int index)
        {
            if (QuickSlots == null || index < 0 || index >= QuickSlots.Length || QuickSlots[index] == null) return;

            QuickSlots[index].SetStack(PlayerInventory.quickSlots.slots[index]);
            QuickSlots[index].SetSelected(index == PlayerInventory.selectedQuickSlotIndex);
        }

        public void RefreshSelectedQuickSlot(int index)
        {
            if (QuickSlots == null) return;

            for (int i = 0; i < QuickSlots.Length; i++)
            {
                if (QuickSlots[i] != null) QuickSlots[i].SetSelected(i == index);
            }
        }

        /// <summary>
        /// [HDY 요청 - 인벤토리 업그레이드] 언락된 칸은 정상 상태로 두고, 아직 언락되지 않은 칸은 슬롯에
        /// 이미 붙어있는 CanvasGroup으로 회색 처리(alpha 낮춤) + 상호작용을 막는다(interactable=false,
        /// blocksRaycasts=false). InventorySlotUI 자체는 다른 팀 소유라 건드리지 않고, 슬롯 프리팹에 이미
        /// 있는 CanvasGroup 컴포넌트를 여기서 직접 제어한다.
        /// </summary>
        public void RefreshInventorySlotLocks()
        {
            if (InventorySlots == null) return;

            int unlockedCount = PlayerInventory.UnlockedInventorySlotCount;

            for (int i = 0; i < InventorySlots.Length; i++)
            {
                if (InventorySlots[i] == null) continue;

                bool unlocked = i < unlockedCount;
                CanvasGroup canvasGroup = InventorySlots[i].GetComponent<CanvasGroup>();
                if (canvasGroup == null) continue;

                canvasGroup.interactable = unlocked;
                canvasGroup.blocksRaycasts = unlocked;
                canvasGroup.alpha = unlocked ? 1f : LockedSlotAlpha;
            }
        }

        public void RefreshAll()
        {
            RefreshInventorySlots();
            RefreshQuickSlots();
            RefreshSelectedQuickSlot(PlayerInventory.selectedQuickSlotIndex);
            RefreshInventorySlotLocks();
        }

        // ===================== 잠금 판정 / 집기·놓기 위임 =====================

        /// <summary>퀵슬롯 사용중 잠금 + 인벤토리 미언락 잠금을 함께 판정한다. Inventory/QuickSlot 외 그룹은 항상 false(창고/트래시는 호출부가 별도 처리).</summary>
        public bool IsLocked(InventorySlotUI slot)
        {
            if (slot == null) return false;
            if (slot.group == SlotGroup.QuickSlot) return PlayerInventory.IsQuickSlotLocked(slot.slotIndex);
            if (slot.group == SlotGroup.Inventory) return PlayerInventory.IsInventorySlotLocked(slot.slotIndex);
            return false;
        }

        public bool TryTakeFull(SlotGroup group, int index, out ItemStack taken)
        {
            return PlayerInventory.TryTakeSlot(group, index, int.MaxValue, out taken);
        }

        public bool TryTakeHalf(SlotGroup group, int index, out ItemStack taken)
        {
            return PlayerInventory.TryTakeHalfSlot(group, index, out taken);
        }

        public bool TryTakeAmount(SlotGroup group, int index, int amount, out ItemStack taken)
        {
            return PlayerInventory.TryTakeSlot(group, index, amount, out taken);
        }

        public bool TryPlaceFull(SlotGroup group, int index, ItemStack held)
        {
            return PlayerInventory.TryPlaceHeldStack(group, index, held);
        }

        public bool TryPlaceOne(SlotGroup group, int index, ItemStack held)
        {
            return PlayerInventory.TryPlaceHeldAmount(group, index, held, 1, false);
        }

        public bool TryGetSnapshot(SlotGroup group, int index, out ItemStack snapshot)
        {
            return PlayerInventory.TryGetSlotSnapshot(group, index, out snapshot);
        }

        // ===================== 정렬 / 업그레이드 처리 =====================

        /// <summary>정렬 버튼 클릭 시 실제 정렬을 수행한다. 로그는 각 패널이 자기 이름으로 남기고 이 메서드는 조용히 처리만 한다.</summary>
        public void HandleSortRequested(InventorySortCriteria criteria)
        {
            PlayerInventory.ApplyInventorySort(criteria);
        }

        /// <summary>업그레이드 버튼 클릭 시 공용 업그레이드 팝업(UpgradePopupUI)을 연다.</summary>
        public void HandleUpgradeButtonClicked(InventoryUpgrade upgrade)
        {
            if (upgrade == null)
            {
                Debug.LogWarning("[PlayerInventoryGridController] inventoryUpgrade가 비어있어 업그레이드 팝업을 열 수 없습니다.");
                return;
            }

            if (UpgradePopupUI.Instance == null)
            {
                Debug.LogWarning("[PlayerInventoryGridController] 씬에서 UpgradePopupUI를 찾을 수 없습니다.");
                return;
            }

            UpgradePopupUI.Instance.Show(upgrade);
        }
    }
}
