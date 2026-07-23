using System;

namespace HDY.Forge
{
    /// <summary>
    /// 연마칸 1칸의 현재 상태.
    /// 등급은 최초 생성 시 Rare로 시작하며, 연마를 통해서만 올라가고 절대 내려가지 않는다.
    /// 종류(OptionType)는 지금은 문자열 키로만 관리한다 - 실제 스탯 적용 로직은 이후 진행 예정이라
    /// txt(RefinementOptionRow)의 "종류" 컬럼 값을 그대로 저장해두는 용도.
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

        public ForgeRefinementSlotData()
        {
        }

        public ForgeRefinementSlotData(CommonClass grade, string optionType, float value)
        {
            Grade = grade;
            OptionType = optionType;
            Value = value;
        }

        /// <summary>같은 값을 가진 새 인스턴스를 만든다(전승 시 참조 공유를 피하기 위한 깊은 복사용).</summary>
        public ForgeRefinementSlotData Clone()
        {
            return new ForgeRefinementSlotData(Grade, OptionType, Value);
        }
    }
}
