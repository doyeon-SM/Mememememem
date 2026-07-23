using System;
using System.Collections.Generic;
using System.Globalization;

namespace HDY.Forge
{
    /// <summary>
    /// 연마 옵션 txt(탭 구분) 1행. 컬럼 순서: 등급\t종류\t수치\t확률\t표시명
    /// 예: Rare\tDamageIncrease\t1\t50\t데미지
    /// 같은 등급+종류 조합이라도 수치가 다른 여러 행이 존재할 수 있다(예: Rare/DamageIncrease/1/50, Rare/DamageIncrease/2/30).
    /// 확률은 "등급이 이미 결정된 뒤, 그 등급 내에서 이 (종류,수치) 조합이 뽑힐 가중치"로 사용된다.
    /// 표시명은 종류(영문 키)를 화면에 보여줄 한글 라벨(툴팁 등)로, 코드 수정 없이 이 컬럼만 바꾸면 반영된다.
    /// </summary>
    [Serializable]
    public struct RefinementOptionRow
    {
        public CommonClass Grade;
        public string OptionType;
        public float Value;
        public float Probability;
        public string DisplayName;

        public RefinementOptionRow(CommonClass grade, string optionType, float value, float probability, string displayName)
        {
            Grade = grade;
            OptionType = optionType;
            Value = value;
            Probability = probability;
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// RefinementOptionRow용 탭 구분(TSV) 파서. ItemCatalog.txt와 동일하게 탭('\t')으로 컬럼을 나눈다.
    /// 헤더 행(등급\t종류\t수치\t확률\t표시명)은 있어도 없어도 무방하게 처리한다.
    /// </summary>
    public static class RefinementOptionCsvParser
    {
        /// <summary>txt 텍스트 전체를 파싱해 유효한 행만 리스트로 반환한다. 잘못된 행은 건너뛴다.</summary>
        public static List<RefinementOptionRow> Parse(string text)
        {
            var results = new List<RefinementOptionRow>();
            if (string.IsNullOrEmpty(text)) return results;

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("#")) continue; // 주석 행 허용

                var columns = line.Split('\t');
                if (columns.Length < 4) continue;

                var gradeText = columns[0].Trim();
                var optionType = columns[1].Trim();
                var valueText = columns[2].Trim();
                var probabilityText = columns[3].Trim();
                // 표시명 컬럼(5번째)은 선택 - 없으면 종류(영문 키)를 그대로 표시명으로 폴백한다.
                var displayName = columns.Length >= 5 ? columns[4].Trim() : optionType;

                // 헤더 행("등급\t종류\t수치\t확률\t표시명" 등)은 등급 파싱이 실패하므로 자연히 건너뛰어진다.
                if (!TryParseGrade(gradeText, out var grade)) continue;
                if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) continue;
                if (!float.TryParse(probabilityText, NumberStyles.Float, CultureInfo.InvariantCulture, out var probability)) continue;
                if (string.IsNullOrEmpty(optionType)) continue;

                if (string.IsNullOrEmpty(displayName)) displayName = optionType;

                results.Add(new RefinementOptionRow(grade, optionType, value, probability, displayName));
            }

            return results;
        }

        private static bool TryParseGrade(string text, out CommonClass grade)
        {
            return Enum.TryParse(text, true, out grade);
        }
    }
}
