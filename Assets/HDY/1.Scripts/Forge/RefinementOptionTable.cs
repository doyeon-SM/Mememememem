using System.Collections.Generic;
using UnityEngine;

namespace HDY.Forge
{
    /// <summary>
    /// RefinementOptionRow 목록을 등급별로 묶어, 등급이 이미 결정된 상태에서
    /// "종류+수치+표시명" 조합을 확률(가중치) 기반으로 랜덤 추첨하는 테이블.
    /// RefinementConfig가 txt를 파싱한 결과를 넘겨받아 내부에서 구성한다.
    /// </summary>
    public class RefinementOptionTable
    {
        private readonly Dictionary<CommonClass, List<RefinementOptionRow>> rowsByGrade
            = new Dictionary<CommonClass, List<RefinementOptionRow>>();

        private readonly Dictionary<CommonClass, float> totalWeightByGrade
            = new Dictionary<CommonClass, float>();

        public RefinementOptionTable(IReadOnlyList<RefinementOptionRow> rows)
        {
            if (rows == null) return;

            foreach (var row in rows)
            {
                if (!rowsByGrade.TryGetValue(row.Grade, out var list))
                {
                    list = new List<RefinementOptionRow>();
                    rowsByGrade[row.Grade] = list;
                }

                list.Add(row);

                totalWeightByGrade.TryGetValue(row.Grade, out var currentTotal);
                totalWeightByGrade[row.Grade] = currentTotal + Mathf.Max(0f, row.Probability);
            }
        }

        /// <summary>이 등급에 대해 정의된 옵션 행이 하나라도 있는지.</summary>
        public bool HasOptionsForGrade(CommonClass grade)
        {
            return rowsByGrade.TryGetValue(grade, out var list) && list.Count > 0;
        }

        /// <summary>
        /// 주어진 등급 내에서 확률 가중치로 (종류, 표시명, 수치) 조합을 하나 뽑는다.
        /// 해당 등급에 정의된 행이 없으면 false.
        /// </summary>
        public bool TryPickRandom(CommonClass grade, out string optionType, out string displayName, out float value)
        {
            optionType = null;
            displayName = null;
            value = 0f;

            if (!rowsByGrade.TryGetValue(grade, out var list) || list.Count == 0) return false;
            if (!totalWeightByGrade.TryGetValue(grade, out var totalWeight) || totalWeight <= 0f)
            {
                // 확률 컬럼이 전부 0이면 균등 확률로 폴백한다.
                var fallback = list[Random.Range(0, list.Count)];
                optionType = fallback.OptionType;
                displayName = fallback.DisplayName;
                value = fallback.Value;
                return true;
            }

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var row in list)
            {
                cumulative += Mathf.Max(0f, row.Probability);
                if (roll <= cumulative)
                {
                    optionType = row.OptionType;
                    displayName = row.DisplayName;
                    value = row.Value;
                    return true;
                }
            }

            // 부동소수점 오차로 못 걸렸을 경우 마지막 행으로 폴백.
            var last = list[list.Count - 1];
            optionType = last.OptionType;
            displayName = last.DisplayName;
            value = last.Value;
            return true;
        }
    }
}
