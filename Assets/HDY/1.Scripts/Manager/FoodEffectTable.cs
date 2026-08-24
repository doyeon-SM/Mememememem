using System.Collections.Generic;
using System.Globalization;

namespace HDY.Item
{
    /// <summary>
    /// 음식(Food) Item_ID -> 섭취 효과(EatEffects) 매핑 테이블.
    /// 기존에는 ItemCatalog.csv 메인 시트의 EatEffects 컬럼에 같이 들어있었지만,
    /// 음식 효과를 별도로 관리하기 쉽도록 "Item_ID, EatEffects" 두 컬럼만 있는
    /// 전용 csv(FoodEffectCatalog.csv)로 분리했다.
    /// RefinementConfig의 optionDataCsv + RefinementOptionCsvParser와 동일한 구조를 따른다.
    /// </summary>
    public class FoodEffectTable
    {
        private readonly Dictionary<string, List<ItemEffect>> effectsByItemId
            = new Dictionary<string, List<ItemEffect>>();

        public FoodEffectTable(string csvText)
        {
            if (string.IsNullOrEmpty(csvText)) return;

            var lines = csvText.Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0번째 줄은 헤더라 건너뜀
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(',');
                if (cols.Length < 2) continue;

                var itemId = cols[0].Trim();
                if (string.IsNullOrEmpty(itemId)) continue;

                effectsByItemId[itemId] = ParseEffects(cols[1]);
            }
        }

        /// <summary>Item_ID에 등록된 섭취 효과 목록을 반환한다. 등록된 게 없으면 빈 리스트.</summary>
        public List<ItemEffect> GetEffects(string itemId)
        {
            if (!string.IsNullOrEmpty(itemId) && effectsByItemId.TryGetValue(itemId, out var effects))
            {
                return effects;
            }

            return new List<ItemEffect>();
        }

        /// <summary>"Satiety:10;Speed:5" 형식을 파싱한다. 빈 문자열이면 빈 리스트를 반환한다.</summary>
        private static List<ItemEffect> ParseEffects(string raw)
        {
            var effects = new List<ItemEffect>();
            if (string.IsNullOrWhiteSpace(raw)) return effects;

            var entries = raw.Split(';');
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;

                var parts = entry.Split(':');
                if (parts.Length != 2) continue;

                if (System.Enum.TryParse(parts[0].Trim(), out EffectType effectType) &&
                    float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    effects.Add(new ItemEffect { Effect = effectType, Value = value });
                }
            }

            return effects;
        }
    }
}
