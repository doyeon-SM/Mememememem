using System.Collections.Generic;
using System.Globalization;

namespace KMS.Equipment
{
    /// <summary>
    /// [멤] Item_ID -> 장비 전용 데이터(부위/체력/스탯/기본옵션) 매핑 테이블. 무기의 WeaponStatsTable과
    /// 완전히 같은 방식(별도 csv를 Item_ID 기준으로 파싱하는 순수 데이터 클래스, MonoBehaviour/SO 아님)이다.
    ///
    /// 장비의 "기본 데이터"(이름/카테고리/등급/아이콘 등)는 다른 아이템들과 동일하게 ItemCatalog.csv에
    /// 들어가고, 이 테이블은 장비에만 있는 추가 데이터(EquipmentCatalog.csv)만 담는다.
    ///
    /// csv 컬럼 순서: Item_ID, EquipSlot, DamageType, HealthBonus, PrimaryStatValue, SecondaryStatValue,
    /// BaseOptionStatType, BaseOptionValue.
    /// (방어구 행은 앞쪽 컬럼을, 장신구 행은 뒤쪽 두 컬럼을 쓴다 - 쓰지 않는 컬럼은 비워두거나 0으로 둔다.)
    /// </summary>
    public class EquipmentStatsTable
    {
        public struct Row
        {
            public EquipSlotType EquipSlot;
            public WeaponDamageType DamageType;
            public int HealthBonus;
            public int PrimaryStatValue;
            public int SecondaryStatValue;
            public CharacterStatType BaseOptionStatType;
            public int BaseOptionValue;
        }

        private readonly Dictionary<string, Row> rowsByItemId = new Dictionary<string, Row>();

        public EquipmentStatsTable(string csvText)
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

                rowsByItemId[itemId] = new Row
                {
                    EquipSlot = ParseEnum(cols[1], EquipSlotType.Head),
                    DamageType = cols.Length >= 3 ? ParseEnum(cols[2], WeaponDamageType.Physical) : WeaponDamageType.Physical,
                    HealthBonus = cols.Length >= 4 ? ParseInt(cols[3]) : 0,
                    PrimaryStatValue = cols.Length >= 5 ? ParseInt(cols[4]) : 0,
                    SecondaryStatValue = cols.Length >= 6 ? ParseInt(cols[5]) : 0,
                    BaseOptionStatType = cols.Length >= 7 ? ParseEnum(cols[6], CharacterStatType.Strength) : CharacterStatType.Strength,
                    BaseOptionValue = cols.Length >= 8 ? ParseInt(cols[7]) : 0,
                };
            }
        }

        /// <summary>Item_ID에 해당하는 장비 전용 데이터 행을 찾는다. 없으면 false.</summary>
        public bool TryGetRow(string itemId, out Row row)
        {
            row = default;
            return !string.IsNullOrEmpty(itemId) && rowsByItemId.TryGetValue(itemId, out row);
        }

        private static int ParseInt(string s)
        {
            return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        // [멤] csv의 enum 컬럼(부위/데미지타입/스탯종류)을 파싱한다. 비어있거나 알 수 없는 값은 기본값으로 처리한다.
        private static T ParseEnum<T>(string s, T fallback) where T : struct
        {
            var trimmed = s.Trim();
            return System.Enum.TryParse<T>(trimmed, true, out var value) ? value : fallback;
        }
    }
}
