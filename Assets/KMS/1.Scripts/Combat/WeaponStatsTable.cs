using System.Collections.Generic;
using System.Globalization;

namespace KMS.Combat
{
    /// <summary>
    /// [멤] Item_ID -> 무기 전용 데이터(사거리/공격쿨타임/투사체 스펙) 매핑 테이블.
    /// 기존에는 WeaponItemData라는 SO를 무기 개수만큼 손으로 만들어
    /// ItemCatalogManager.weaponItemAssets에 하나하나 등록해야 했는데, "SO 말고 id 기반으로
    /// 구조화해달라"는 요청에 따라 그 방식을 폐기하고 HDY.Item.FoodEffectTable과 동일한 방식
    /// (별도 csv를 Item_ID 기준으로 파싱하는 순수 데이터 테이블)으로 바꿨다.
    ///
    /// 무기의 "기본 데이터"(이름/카테고리/등급/수량 등)는 다른 아이템들과 동일하게 여전히
    /// ItemCatalog.csv에 들어가고, 이 테이블은 무기에만 있는 추가 데이터(WeaponCatalog.csv)만 담는다.
    /// 실제 투사체 Prefab 참조는 csv에 담을 수 없으므로 ProjectileId 문자열만 갖고 있고,
    /// ProjectilePrefabTable(Inspector 등록)에서 ProjectileId -> GameObject로 다시 조회한다.
    ///
    /// csv 컬럼 순서: Item_ID, AttackDistance, AttackCooldown, ProjectileId, ProjectileSpeed,
    /// ProjectileLifetime, ProjectileDamage, ProjectileAttackCooldown.
    /// </summary>
    public class WeaponStatsTable
    {
        public struct Row
        {
            public float AttackDistance;
            public float AttackCooldown;
            public string ProjectileId;
            public float ProjectileSpeed;
            public float ProjectileLifetime;
            public int ProjectileDamage;
            public float ProjectileAttackCooldown;
            public WeaponDamageType DamageType;
            public string BasicAttackSkillId;
            public string DashSkillId;
        }

        private readonly Dictionary<string, Row> rowsByItemId = new Dictionary<string, Row>();

        public WeaponStatsTable(string csvText)
        {
            if (string.IsNullOrEmpty(csvText)) return;

            var lines = csvText.Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0번째 줄은 헤더라 건너뜀
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(',');
                if (cols.Length < 8) continue;

                var itemId = cols[0].Trim();
                if (string.IsNullOrEmpty(itemId)) continue;

                rowsByItemId[itemId] = new Row
                {
                    AttackDistance = ParseFloat(cols[1]),
                    AttackCooldown = ParseFloat(cols[2]),
                    ProjectileId = cols[3].Trim(),
                    ProjectileSpeed = ParseFloat(cols[4]),
                    ProjectileLifetime = ParseFloat(cols[5]),
                    ProjectileDamage = ParseInt(cols[6]),
                    ProjectileAttackCooldown = ParseFloat(cols[7]),
                    DamageType = cols.Length >= 9 ? ParseDamageType(cols[8]) : WeaponDamageType.Physical,
                    // [멤] 무기 고유 스킬(기본공격/돌진기) ID. 기존 9컬럼 데이터는 빈 문자열이 되어 예전 방식으로 자동 폴백된다.
                    BasicAttackSkillId = cols.Length >= 10 ? cols[9].Trim() : string.Empty,
                    DashSkillId = cols.Length >= 11 ? cols[10].Trim() : string.Empty,
                };
            }
        }

        /// <summary>Item_ID에 해당하는 무기 전용 데이터 행을 찾는다. 없으면 false.</summary>
        public bool TryGetRow(string itemId, out Row row)
        {
            row = default;
            return !string.IsNullOrEmpty(itemId) && rowsByItemId.TryGetValue(itemId, out row);
        }

        private static float ParseFloat(string s)
        {
            return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
        }

        private static int ParseInt(string s)
        {
            return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        // [멤] csv의 DamageType 컬럼("Physical"/"Magic")을 enum으로 파싱한다. 비어있거나 알 수 없는 값은 기본값(Physical)으로 처리한다.
        private static WeaponDamageType ParseDamageType(string s)
        {
            var trimmed = s.Trim();
            return System.Enum.TryParse<WeaponDamageType>(trimmed, true, out var value) ? value : WeaponDamageType.Physical;
        }
    }
}
