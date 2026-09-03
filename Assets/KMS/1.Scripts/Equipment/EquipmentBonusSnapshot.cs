namespace KMS.Equipment
{
    /// <summary>
    /// [멤] 지금 장착 중인 장비 12칸이 주는 보너스의 합계. PlayerEquipment가 계산해서
    /// PlayerCombatStats.SetEquipmentBonus로 넘긴다.
    ///
    /// 스탯 보너스는 캐릭터 스탯(힘/지능/민첩/행운/의지)에 그대로 더해져 "총합 스탯 = 투자 포인트 + 장비"가
    /// 된다(character-stat-system-plan.md에 이미 그렇게 설계되어 있었다). 체력만은 스탯이 아니라
    /// 최대 체력에 직접 더해지는 고정 수치라 별도 필드다.
    /// </summary>
    public struct EquipmentBonusSnapshot
    {
        /// <summary>최대 체력 가산치(고정 수치). 이 값이 늘어도 현재 체력은 회복되지 않는다.</summary>
        public int Health;

        public int Strength;
        public int Intelligence;
        public int Agility;
        public int Luck;
        public int Willpower;

        public int GetStat(CharacterStatType statType)
        {
            switch (statType)
            {
                case CharacterStatType.Strength: return Strength;
                case CharacterStatType.Intelligence: return Intelligence;
                case CharacterStatType.Agility: return Agility;
                case CharacterStatType.Luck: return Luck;
                case CharacterStatType.Willpower: return Willpower;
                default: return 0;
            }
        }

        public void AddStat(CharacterStatType statType, int value)
        {
            if (value == 0) return;

            switch (statType)
            {
                case CharacterStatType.Strength: Strength += value; break;
                case CharacterStatType.Intelligence: Intelligence += value; break;
                case CharacterStatType.Agility: Agility += value; break;
                case CharacterStatType.Luck: Luck += value; break;
                case CharacterStatType.Willpower: Willpower += value; break;
            }
        }

        public bool Equals(EquipmentBonusSnapshot other)
        {
            return Health == other.Health
                && Strength == other.Strength
                && Intelligence == other.Intelligence
                && Agility == other.Agility
                && Luck == other.Luck
                && Willpower == other.Willpower;
        }
    }
}
