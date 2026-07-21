using System;
using System.Collections.Generic;
using UnityEngine;

namespace HDY.Forge
{
    [Serializable]
    public class ForgeEnhanceBracket
    {
        [Tooltip("이 구간이 적용되는 목표 강화레벨 시작값(도달하려는 레벨 기준, inclusive)")]
        public int FromLevelInclusive;

        [Tooltip("이 구간이 적용되는 목표 강화레벨 끝값(도달하려는 레벨 기준, inclusive)")]
        public int ToLevelInclusive;

        [Tooltip("강화석 소모 개수")]
        public int MaterialCost;

        [Tooltip("골드 소모량. 기획 확정 전이라 0으로 두면 골드를 소모하지 않는다 (기획 확정 후 값만 채우면 됨)")]
        public int GoldCost;

        [Range(0f, 1f)]
        [Tooltip("성공 확률 (0~1)")]
        public float SuccessRate;

        [Range(0f, 1f)]
        [Tooltip("실패 시 충전되는 모루 과열 수치 비율 (0~1). 예: 0.5 = 50% 충전")]
        public float FailureOverheatCharge;
    }

    /// <summary>
    /// 강화(0강~10강) 구간별 소모/확률/과열 충전율과, 승급 시도의 소모/확률/과열 충전율을 정의하는
    /// 단일 설정 자산. 도구 티어와 무관하게 강화 레벨 전체에 공통 적용된다.
    /// </summary>
    [CreateAssetMenu(fileName = "ForgeEnhancementTable", menuName = "HDY/Forge/Forge Enhancement Table", order = 2)]
    public class ForgeEnhancementTable : ScriptableObject
    {
        [Header("강화 구간 목록 (1~5강, 6~8강, 9~10강)")]
        public List<ForgeEnhanceBracket> Brackets = new List<ForgeEnhanceBracket>();

        [Header("승급")]
        public int PromotionMaterialCost = 10;

        [Tooltip("골드 소모량. 기획 확정 전이라 0으로 두면 골드를 소모하지 않는다")]
        public int PromotionGoldCost;

        [Range(0f, 1f)] public float PromotionSuccessRate = 0.15f;
        [Range(0f, 1f)] public float PromotionOverheatCharge = 0.10f;

        [Header("모루 과열")]
        [Tooltip("과열 수치가 이 값(기본 1.0 = 100%) 이상이면 다음 시도는 무조건 성공한다")]
        public float OverheatGuaranteedThreshold = 1f;

        /// <summary>목표 강화레벨(1~10)이 속한 구간을 찾는다. 없으면 null.</summary>
        public ForgeEnhanceBracket GetBracket(int targetLevel)
        {
            foreach (var bracket in Brackets)
            {
                if (bracket == null) continue;

                if (targetLevel >= bracket.FromLevelInclusive && targetLevel <= bracket.ToLevelInclusive)
                {
                    return bracket;
                }
            }

            return null;
        }
    }
}
