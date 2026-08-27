using UnityEngine;

namespace KMS
{
    /// <summary>
    /// [멤] 무기/스킬의 데미지 계열이 힘·민첩(물리) 조합을 쓰는지 지능·행운(마법) 조합을 쓰는지 구분한다.
    /// 무기(WeaponItemData/WeaponCatalog.csv)와 스킬(SkillData/SkillCatalog.csv) 양쪽에 동일하게 쓰이며,
    /// 스킬은 자신과 다른 타입의 무기를 들고 있으면 발동이 제한된다(PlayerWeaponSkillController 참고).
    /// </summary>
    public enum WeaponDamageType
    {
        Physical = 0,
        Magic = 1,
    }

    /// <summary>
    /// [멤] 캐릭터 스탯(힘/지능/민첩/행운/의지) 기획 공식을 순수 계산 함수로 모아둔 static 클래스.
    /// PlayerCombatStats(상태 보관 + 기존 시스템 연동)와 분리해서, 공식 자체는 MonoBehaviour 없이도
    /// 테스트/재사용할 수 있게 했다. 전부 claude/character-stat-system-plan.md에 정리된 확정 공식을
    /// 그대로 코드로 옮긴 것이며, 각 상수(계수/상한)는 그 문서에서 확인된 값이다.
    /// </summary>
    public static class CharacterStatFormulas
    {
        /// <summary>스탯 값 자체의 상한(임시, 넓게 잡음 - 장비 등으로 늘어날 수 있어 여유있게 설정).</summary>
        public const int StatValueCap = 2000;

        /// <summary>레벨업(영지 레벨업) 1회당 지급되는 포인트.</summary>
        public const int PointsPerLevel = 5;

        public const float DefenseCapPercent = 50f;
        public const float ResistanceCapPercent = 50f;
        public const float CritChanceCapPercent = 100f;
        public const float MoveSpeedBonusCapPercent = 100f;
        public const float BaseCritDamagePercent = 50f;
        public const float CritDamageCapPercent = 200f;

        /// <summary>체력 = 영지레벨 기반 체력 × (100+힘/20)%.</summary>
        public static float HealthMultiplier(int strength)
        {
            return 1f + Mathf.Max(0, strength) / 2000f;
        }

        /// <summary>배고픔최대치 = 100 × (100+의지/10)%.</summary>
        public static float HungerMultiplier(int willpower)
        {
            return 1f + Mathf.Max(0, willpower) / 1000f;
        }

        /// <summary>이동속도 보너스 = (민첩/20)%, 최대 100%. 배율(1.0~2.0)로 반환한다.</summary>
        public static float MoveSpeedMultiplier(int agility)
        {
            float bonusPercent = Mathf.Clamp(Mathf.Max(0, agility) / 20f, 0f, MoveSpeedBonusCapPercent);
            return 1f + bonusPercent / 100f;
        }

        /// <summary>채집량 = 기존 채집량 × (100+행운/20)%. 배율로 반환한다.</summary>
        public static float GatherAmountMultiplier(int luck)
        {
            return 1f + Mathf.Max(0, luck) / 2000f;
        }

        /// <summary>크리티컬 확률(%) = 지능/20, 최대 100%.</summary>
        public static float CritChancePercent(int intelligence)
        {
            return Mathf.Clamp(Mathf.Max(0, intelligence) / 20f, 0f, CritChanceCapPercent);
        }

        /// <summary>방어력(%, 받는 데미지 감소) = 힘/20 + 의지/5, 최대 50%.</summary>
        public static float DefensePercent(int strength, int willpower)
        {
            float value = Mathf.Max(0, strength) / 20f + Mathf.Max(0, willpower) / 5f;
            return Mathf.Clamp(value, 0f, DefenseCapPercent);
        }

        /// <summary>저항력(%, 받는 디버프 지속시간 감소) = 지능/20 + 의지/5, 최대 50%.</summary>
        public static float ResistancePercent(int intelligence, int willpower)
        {
            float value = Mathf.Max(0, intelligence) / 20f + Mathf.Max(0, willpower) / 5f;
            return Mathf.Clamp(value, 0f, ResistanceCapPercent);
        }

        /// <summary>크리티컬 데미지(%) = 기본 50% + 보너스(현재는 장비 등으로만 늘어날 예정, 기본 0), 최대 200%.</summary>
        public static float CritDamagePercent(float bonusPercent)
        {
            return Mathf.Clamp(BaseCritDamagePercent + Mathf.Max(0f, bonusPercent), 0f, CritDamageCapPercent);
        }

        /// <summary>
        /// 공격력 or 마력 = 무기공격력 × (1 + (주력 + 부/5) / 100).
        /// 물리 무기면 주력=힘/부=민첩, 마법 무기면 주력=지능/부=행운을 넘겨서 호출한다(PlayerCombatStats 참고).
        /// </summary>
        public static float AttackOrMagicPower(float weaponPower, int primaryStat, int secondaryStat)
        {
            float bonus = (Mathf.Max(0, primaryStat) + Mathf.Max(0, secondaryStat) / 5f) / 100f;
            return Mathf.Max(0f, weaponPower) * (1f + bonus);
        }
    }
}
