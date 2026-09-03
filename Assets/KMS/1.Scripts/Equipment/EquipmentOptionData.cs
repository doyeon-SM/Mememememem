using System;

namespace KMS.Equipment
{
    /// <summary>
    /// [멤] 장신구 옵션 1개(기본옵션 또는 특수옵션)의 값. HDY.Forge.ForgeRefinementSlotData와 같은 모양이지만
    /// 장비 시스템이 도구 대장간 코드에 의존하지 않도록 별도 타입으로 둔다(Grade 개념도 아직 없음).
    ///
    /// 기본옵션은 아이템 종류가 결정하므로 EquipmentCatalog.csv에서 오고, 특수옵션은 개체마다 다르므로
    /// EquipmentInstanceData.SpecialOptions(개체별 데이터)에 저장된다 - 전승으로 옮겨지는 것은 후자뿐이다.
    /// </summary>
    [Serializable]
    public class EquipmentOptionData
    {
        /// <summary>이 옵션이 올려주는 캐릭터 스탯 종류.</summary>
        public CharacterStatType StatType;

        /// <summary>올려주는 수치(1 = 스탯 +1).</summary>
        public int Value;

        public EquipmentOptionData()
        {
        }

        public EquipmentOptionData(CharacterStatType statType, int value)
        {
            StatType = statType;
            Value = value;
        }

        /// <summary>전승 시 참조 공유를 피하기 위한 깊은 복사(ForgeRefinementSlotData.Clone과 동일한 목적).</summary>
        public EquipmentOptionData Clone()
        {
            return new EquipmentOptionData(StatType, Value);
        }

        /// <summary>툴팁 등에 그대로 보여줄 문자열(예: "지능 +8").</summary>
        public string Format()
        {
            return $"{GetStatDisplayName(StatType)} +{Value}";
        }

        public static string GetStatDisplayName(CharacterStatType statType)
        {
            switch (statType)
            {
                case CharacterStatType.Strength: return "힘";
                case CharacterStatType.Intelligence: return "지능";
                case CharacterStatType.Agility: return "민첩";
                case CharacterStatType.Luck: return "행운";
                case CharacterStatType.Willpower: return "의지";
                default: return statType.ToString();
            }
        }
    }
}
