using System.Collections.Generic;

namespace KMS.Equipment
{
    /// <summary>
    /// [멤] 장비 부위. 방어구 4종(Head~Boots)과 장신구 6종(Earring~Hairpin)이 하나의 enum에 들어있고,
    /// 앞의 4개가 방어구라는 순서 규약을 EquipmentSlotLayout.IsArmorType이 사용한다(값 순서를 바꾸면 안 됨).
    /// 아이템의 대분류(ItemCategory.Armor / Accessory)와는 별개로, "어느 칸에 들어가는가"만 나타낸다.
    /// </summary>
    public enum EquipSlotType
    {
        Head = 0,
        Chest = 1,
        Legs = 2,
        Boots = 3,
        Earring = 4,
        Ring = 5,
        Necklace = 6,
        Belt = 7,
        Bracelet = 8,
        Hairpin = 9,
    }

    /// <summary>
    /// [멤] 장착창 12칸의 고정 배치표. 방어구 4칸 + 장신구 8칸이며, 귀걸이와 반지만 2칸씩 있다.
    /// 인덱스 -> 부위 매핑을 여기 한 곳에서만 정의해서, PlayerEquipment / 저장 데이터 / UI가 전부
    /// 같은 순서를 공유하게 한다(칸 순서를 바꾸면 기존 세이브의 칸이 밀리므로 끝에만 추가할 것).
    /// </summary>
    public static class EquipmentSlotLayout
    {
        public const int ArmorSlotCount = 4;
        public const int AccessorySlotCount = 8;
        public const int TotalSlotCount = ArmorSlotCount + AccessorySlotCount;

        private static readonly EquipSlotType[] SlotTypes =
        {
            EquipSlotType.Head,     // 0
            EquipSlotType.Chest,    // 1
            EquipSlotType.Legs,     // 2
            EquipSlotType.Boots,    // 3
            EquipSlotType.Earring,  // 4
            EquipSlotType.Earring,  // 5 (귀걸이 2칸)
            EquipSlotType.Ring,     // 6
            EquipSlotType.Ring,     // 7 (반지 2칸)
            EquipSlotType.Necklace, // 8
            EquipSlotType.Belt,     // 9
            EquipSlotType.Bracelet, // 10
            EquipSlotType.Hairpin,  // 11
        };

        public static bool IsValidIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < TotalSlotCount;
        }

        /// <summary>이 칸이 받아들이는 부위. 잘못된 인덱스면 Head를 반환하므로 항상 IsValidIndex로 먼저 검사할 것.</summary>
        public static EquipSlotType GetSlotType(int slotIndex)
        {
            return IsValidIndex(slotIndex) ? SlotTypes[slotIndex] : EquipSlotType.Head;
        }

        /// <summary>해당 칸에 이 부위의 장비를 넣을 수 있는지. "맞는 칸에만 장착 가능"의 유일한 판정 지점이다.</summary>
        public static bool Accepts(int slotIndex, EquipSlotType slotType)
        {
            return IsValidIndex(slotIndex) && SlotTypes[slotIndex] == slotType;
        }

        /// <summary>방어구 부위(Head~Boots)인지. 강화/연마 가능 여부와 1:1로 대응한다.</summary>
        public static bool IsArmorType(EquipSlotType slotType)
        {
            return slotType <= EquipSlotType.Boots;
        }

        public static bool IsArmorSlot(int slotIndex)
        {
            return IsValidIndex(slotIndex) && slotIndex < ArmorSlotCount;
        }

        /// <summary>이 부위가 들어갈 수 있는 칸 인덱스들(귀걸이/반지는 2개). 빈 칸 자동 탐색에 쓴다.</summary>
        public static IReadOnlyList<int> GetSlotIndices(EquipSlotType slotType)
        {
            var result = new List<int>(2);
            for (int i = 0; i < SlotTypes.Length; i++)
            {
                if (SlotTypes[i] == slotType) result.Add(i);
            }

            return result;
        }

        /// <summary>UI/툴팁 표시용 한글 부위 이름.</summary>
        public static string GetDisplayName(EquipSlotType slotType)
        {
            switch (slotType)
            {
                case EquipSlotType.Head: return "머리";
                case EquipSlotType.Chest: return "갑옷";
                case EquipSlotType.Legs: return "다리";
                case EquipSlotType.Boots: return "신발";
                case EquipSlotType.Earring: return "귀걸이";
                case EquipSlotType.Ring: return "반지";
                case EquipSlotType.Necklace: return "목걸이";
                case EquipSlotType.Belt: return "벨트";
                case EquipSlotType.Bracelet: return "팔찌";
                case EquipSlotType.Hairpin: return "머리핀";
                default: return slotType.ToString();
            }
        }
    }
}
