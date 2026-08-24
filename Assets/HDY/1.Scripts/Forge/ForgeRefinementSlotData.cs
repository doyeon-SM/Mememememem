using System;

namespace HDY.Forge
{
    /// <summary>
    /// 연마칸 1칸의 현재 상태.
    /// 등급은 최초 생성 시 Rare로 시작하며, 연마를 통해서만 올라가고 절대 내려가지 않는다.
    /// 종류(OptionType)는 txt의 "종류" 컬럼(영문 키) 값을 그대로 저장한다 - 실제 스탯 적용 로직은
    /// 이후 진행 예정이라 지금은 값만 보관. 표시명(DisplayName)은 화면(툴팁 등)에 그대로 보여줄
    /// 한글 라벨로, 뽑힐 당시 txt의 "표시명" 컬럼 값을 그대로 복사해둔다(재조회 없이 바로 표시 가능).
    /// </summary>
    [Serializable]
    public class ForgeRefinementSlotData
    {
        /// <summary>이 칸의 현재 등급.</summary>
        public CommonClass Grade;

        /// <summary>이 칸에 붙은 옵션 종류 (txt "종류" 컬럼 값, 예: DamageIncrease, GatherIncrease).</summary>
        public string OptionType;

        /// <summary>이 칸에 붙은 옵션 수치 (txt "수치" 컬럼 값).</summary>
        public float Value;

        /// <summary>화면 표시용 한글 라벨 (txt "표시명" 컬럼 값, 예: 데미지, 채집량).</summary>
        public string DisplayName;

        public ForgeRefinementSlotData()
        {
        }

        public ForgeRefinementSlotData(CommonClass grade, string optionType, string displayName, float value)
        {
            Grade = grade;
            OptionType = optionType;
            DisplayName = displayName;
            Value = value;
        }

        /// <summary>같은 값을 가진 새 인스턴스를 만든다(전승 시 참조 공유를 피하기 위한 깊은 복사용).</summary>
        public ForgeRefinementSlotData Clone()
        {
            return new ForgeRefinementSlotData(Grade, OptionType, DisplayName, Value);
        }

        /// <summary>
        /// [HDY 요청] 툴팁 등 UI에 보여줄 수치 문자열. GatherIncrease는 이제 %(비율) 값이라 "%"를 붙이고,
        /// 그 외(DamageIncrease 등 고정 수치)는 기존처럼 숫자만 표시한다.
        /// </summary>
        public string FormatValue()
        {
            return string.Equals(OptionType, "GatherIncrease", StringComparison.Ordinal)
                ? $"{Value:0.#}%"
                : $"{Value:0.#}";
        }
    }
}
