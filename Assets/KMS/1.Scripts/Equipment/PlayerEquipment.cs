using System;
using HDY.Item;
using KMS.InventoryDuped;
using UnityEngine;

namespace KMS.Equipment
{
    /// <summary>
    /// [멤] 캐릭터의 장착창(방어구 4칸 + 장신구 8칸 = 12칸)을 보관하고, 장착 중인 장비의 보너스를 합산해
    /// PlayerCombatStats에 반영하는 컴포넌트. 칸 배치는 EquipmentSlotLayout 한 곳에서만 정의된다.
    ///
    /// [멤] 인벤토리와의 연동(드래그로 장착/해제)은 장비 UI 단계에서 붙인다 - 이 컴포넌트는 "칸에 무엇이
    /// 들어있는가"와 "그래서 스탯이 얼마나 오르는가"만 책임진다. 그래서 API도 슬롯 단위(TryEquip/TryUnequip)로
    /// 두었고, UI가 인벤토리에서 아이템을 빼서 넘겨주기만 하면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerEquipment : MonoBehaviour
    {
        public const int SlotCount = EquipmentSlotLayout.TotalSlotCount;

        [Header("References")]
        [SerializeField] private ItemCatalogManager catalogManager;
        [SerializeField] private PlayerCombatStats combatStats;
        [SerializeField] private EquipmentInstanceRegistry instanceRegistry;

        [Header("장착 슬롯 (0~3 방어구: 머리/갑옷/다리/신발, 4~11 장신구: 귀걸이x2/반지x2/목걸이/벨트/팔찌/머리핀)")]
        [SerializeField] private ItemStack[] slots = new ItemStack[SlotCount];

        /// <summary>장착/해제 등으로 12칸 내용이 바뀔 때마다 발행된다(UI 갱신 및 저장 트리거용).</summary>
        public event Action OnEquipmentChanged;

        private void Reset()
        {
            combatStats = GetComponent<PlayerCombatStats>();
            EnsureSlots();
        }

        private void Awake()
        {
            EnsureSlots();
            ResolveReferences();
        }

        private void Start()
        {
            // [멤] Awake 시점에는 카탈로그/스탯 쪽 초기화가 끝나지 않았을 수 있어, 한 프레임 뒤 시점에 한 번 더 반영한다.
            RecalculateAndApplyBonus();
        }

        private void ResolveReferences()
        {
            catalogManager = ItemCatalogManager.Resolve(catalogManager);
            if (combatStats == null) combatStats = GetComponent<PlayerCombatStats>();
            instanceRegistry = EquipmentInstanceRegistry.Resolve(instanceRegistry);
        }

        private void EnsureSlots()
        {
            if (slots == null || slots.Length != SlotCount)
            {
                var resized = new ItemStack[SlotCount];
                for (int i = 0; i < SlotCount; i++)
                {
                    resized[i] = slots != null && i < slots.Length && slots[i] != null ? slots[i] : new ItemStack();
                }

                slots = resized;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) slots[i] = new ItemStack();
            }
        }

        // ---- 조회 ----

        public ItemStack GetSlot(int slotIndex)
        {
            EnsureSlots();
            return EquipmentSlotLayout.IsValidIndex(slotIndex) ? slots[slotIndex] : null;
        }

        /// <summary>이 칸에 장착된 장비 데이터. 비어있거나 장비가 아니면 null.</summary>
        public EquipmentItemData GetEquippedData(int slotIndex)
        {
            ItemStack slot = GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty) return null;

            catalogManager = ItemCatalogManager.Resolve(catalogManager);
            return catalogManager != null ? catalogManager.FindItemData(slot.itemId) as EquipmentItemData : null;
        }

        /// <summary>이 itemId가 해당 칸에 들어갈 수 있는지(부위 일치 + 장비 카테고리) 검사한다.</summary>
        public bool CanEquip(int slotIndex, string itemId)
        {
            if (!EquipmentSlotLayout.IsValidIndex(slotIndex) || string.IsNullOrEmpty(itemId)) return false;

            catalogManager = ItemCatalogManager.Resolve(catalogManager);
            var data = catalogManager != null ? catalogManager.FindItemData(itemId) as EquipmentItemData : null;
            if (data == null) return false;

            return EquipmentSlotLayout.Accepts(slotIndex, data.EquipSlot);
        }

        /// <summary>이 부위가 들어갈 수 있는 칸 중 비어있는 첫 칸을 찾는다(귀걸이/반지는 2칸). 없으면 -1.</summary>
        public int FindEmptySlotFor(EquipSlotType slotType)
        {
            EnsureSlots();
            var candidates = EquipmentSlotLayout.GetSlotIndices(slotType);
            for (int i = 0; i < candidates.Count; i++)
            {
                int index = candidates[i];
                if (slots[index] == null || slots[index].IsEmpty) return index;
            }

            return -1;
        }

        // ---- 장착 / 해제 ----

        /// <summary>
        /// 지정한 칸에 장비를 장착한다. 부위가 맞지 않으면 실패한다("맞는 칸에만 장착 가능").
        /// 이미 다른 장비가 들어있으면 실패한다 - 교체는 UI가 먼저 TryUnequip을 호출하는 방식으로 처리한다
        /// (해제된 아이템을 인벤토리 어디에 돌려줄지는 UI가 결정해야 하므로, 여기서 임의로 버리지 않는다).
        /// </summary>
        public bool TryEquip(int slotIndex, string itemId, int durability = -1)
        {
            EnsureSlots();
            if (!CanEquip(slotIndex, itemId)) return false;
            if (slots[slotIndex] != null && !slots[slotIndex].IsEmpty) return false;

            slots[slotIndex].Set(itemId, 1, durability);
            HandleEquipmentChanged();
            return true;
        }

        /// <summary>지정한 칸을 비우고 무엇이 들어있었는지 돌려준다. 비어있으면 false.</summary>
        public bool TryUnequip(int slotIndex, out string itemId, out int durability)
        {
            itemId = null;
            durability = -1;

            EnsureSlots();
            if (!EquipmentSlotLayout.IsValidIndex(slotIndex)) return false;
            if (slots[slotIndex] == null || slots[slotIndex].IsEmpty) return false;

            itemId = slots[slotIndex].itemId;
            durability = slots[slotIndex].durability;
            slots[slotIndex].Clear();
            HandleEquipmentChanged();
            return true;
        }

        /// <summary>이 itemId가 장착된 칸 인덱스를 찾는다. 없으면 -1(전승으로 재료가 소멸할 때 확인용).</summary>
        public int FindSlotByItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return -1;

            EnsureSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && !slots[i].IsEmpty && slots[i].itemId == itemId) return i;
            }

            return -1;
        }

        // ---- 보너스 합산 ----

        /// <summary>지금 장착 중인 12칸의 보너스를 전부 합산한다.</summary>
        public EquipmentBonusSnapshot BuildBonusSnapshot()
        {
            EnsureSlots();
            ResolveReferences();

            var snapshot = new EquipmentBonusSnapshot();

            for (int i = 0; i < slots.Length; i++)
            {
                ItemStack slot = slots[i];
                if (slot == null || slot.IsEmpty) continue;

                var data = catalogManager != null ? catalogManager.FindItemData(slot.itemId) as EquipmentItemData : null;
                if (data == null) continue;

                if (data.IsArmor)
                {
                    // [멤] 방어구: 체력 + 주스탯 + 부스탯. 주/부 스탯 종류는 DamageType이 자동으로 결정한다.
                    snapshot.Health += data.HealthBonus;
                    snapshot.AddStat(data.PrimaryStatType, data.PrimaryStatValue);
                    snapshot.AddStat(data.SecondaryStatType, data.SecondaryStatValue);
                }
                else
                {
                    // [멤] 장신구: 기본옵션(아이템 종류가 결정) + 특수옵션(개체별, 아래에서 합산).
                    snapshot.AddStat(data.BaseOptionStatType, data.BaseOptionValue);
                }

                AddInstanceOptions(slot.itemId, ref snapshot);
            }

            return snapshot;
        }

        // [멤] 개체별 데이터(연마 옵션 / 장신구 특수옵션)를 합산한다. 순수 Item_ID(개체가 아직 없는 장비)면
        // 아무것도 더하지 않는다 - 지연 생성 원칙상 이게 정상 상태다.
        private void AddInstanceOptions(string itemId, ref EquipmentBonusSnapshot snapshot)
        {
            instanceRegistry = EquipmentInstanceRegistry.Resolve(instanceRegistry);
            if (instanceRegistry == null) return;

            var instance = instanceRegistry.GetInstanceByCompositeId(itemId);
            if (instance == null) return;

            if (instance.RefinementOptions != null)
            {
                foreach (var option in instance.RefinementOptions)
                {
                    if (option != null) snapshot.AddStat(option.StatType, option.Value);
                }
            }

            if (instance.SpecialOptions != null)
            {
                foreach (var option in instance.SpecialOptions)
                {
                    if (option != null) snapshot.AddStat(option.StatType, option.Value);
                }
            }
        }

        /// <summary>장비 보너스를 다시 계산해 캐릭터 스탯에 반영한다. 장착 상태가 바뀔 때마다 호출된다.</summary>
        public void RecalculateAndApplyBonus()
        {
            ResolveReferences();
            if (combatStats == null) return;

            combatStats.SetEquipmentBonus(BuildBonusSnapshot());
        }

        private void HandleEquipmentChanged()
        {
            RecalculateAndApplyBonus();
            OnEquipmentChanged?.Invoke();
        }

        // ---- 저장/불러오기 ----

        /// <summary>세이브용 12칸 스냅샷(참조 공유를 피하기 위해 새 ItemStack으로 복사한다).</summary>
        public ItemStack[] CaptureSaveData()
        {
            EnsureSlots();

            var captured = new ItemStack[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                ItemStack source = slots[i];
                captured[i] = source == null
                    ? new ItemStack()
                    : new ItemStack { itemId = source.itemId, amount = source.amount, durability = source.durability };
            }

            return captured;
        }

        /// <summary>세이브에서 12칸을 복원한다. 칸 수가 다른 구버전 세이브는 앞에서부터 채우고 나머지는 빈 칸으로 둔다.</summary>
        public void RestoreSaveData(ItemStack[] saved)
        {
            EnsureSlots();

            for (int i = 0; i < SlotCount; i++)
            {
                ItemStack source = saved != null && i < saved.Length ? saved[i] : null;
                if (source == null || string.IsNullOrEmpty(source.itemId) || source.amount <= 0)
                {
                    slots[i].Clear();
                }
                else
                {
                    slots[i].Set(source.itemId, source.amount, source.durability);
                }
            }

            RecalculateAndApplyBonus();
            OnEquipmentChanged?.Invoke();
        }

        private void OnValidate()
        {
            EnsureSlots();

            // [멤] 장비 UI가 만들어지기 전까지는 인스펙터에서 슬롯을 직접 채워 테스트하게 되므로,
            // 플레이 중 인스펙터 편집도 즉시 스탯에 반영되도록 한다.
            if (Application.isPlaying)
            {
                RecalculateAndApplyBonus();
            }
        }
    }
}
