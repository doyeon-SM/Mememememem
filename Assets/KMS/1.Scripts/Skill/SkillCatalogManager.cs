using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace KMS.Combat
{
    /// <summary>
    /// [멤] 스킬 데이터(SkillData)를 csv 시트에서 읽어 관리하는 매니저. HDY.Item.ItemCatalogManager가
    /// ItemData/CookRecipeData 등을 시트로 관리하는 것과 동일한 패턴(Awake 시 파싱, Skill_ID 키
    /// 딕셔너리, 아이콘은 SkillIconTable로 분리)이다.
    ///
    /// [멤] 스킬 보유/등록 여부는 이 매니저가 아니라 SkillUnlockManager/PlayerSkillLoadout이 담당한다 -
    /// 이 매니저는 "존재하는 모든 스킬 정의"(카탈로그 전체)만 들고 있고, "플레이어가 실제로 갖고 있는
    /// 스킬"은 다루지 않는다(HDY.Cook.CookRecipeData 카탈로그와 CookRecipeUnlockManager의 관계와 동일).
    /// </summary>
    public class SkillCatalogManager : MonoBehaviour
    {
        public static SkillCatalogManager Instance { get; private set; }

        [Header("스킬 데이터 시트 (쉼표 구분 CSV, Skill_ID 기준으로 파싱)")]
        [Tooltip("컬럼 순서: Skill_ID, Name, Damage, Cooldown, Grade, Description")]
        [SerializeField] private TextAsset skillCatalogSheet; // 컬럼: Skill_ID, Name, Damage, Cooldown, Grade, Description, FormType(선택)

        [Header("스킬 아이콘 테이블 (Skill_ID -> Sprite)")]
        [SerializeField] private SkillIconTable iconTable;

        [Header("ProjectileId -> 투사체 Prefab 테이블 (무기 기본 공격과 공용)")]
        [SerializeField] private ProjectilePrefabTable projectileTable;


        private readonly List<SkillData> skillDataList = new List<SkillData>();
        public IReadOnlyList<SkillData> SkillDataList => skillDataList;

        private readonly Dictionary<string, SkillData> skillDictionary = new Dictionary<string, SkillData>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildDictionary();
        }

        /// <summary>시트를 파싱해 행마다 런타임 SkillData 인스턴스를 만들고 Skill_ID 기준으로 딕셔너리에 채운다.</summary>
        private void BuildDictionary()
        {
            skillDictionary.Clear();
            skillDataList.Clear();

            if (skillCatalogSheet == null)
            {
                Debug.LogWarning("[SkillCatalogManager] skillCatalogSheet가 비어있습니다.");
                return;
            }

            var lines = skillCatalogSheet.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0번째 줄은 헤더라 건너뜀
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(',');
                if (cols.Length < 6)
                {
                    Debug.LogWarning($"[SkillCatalogManager] 스킬 시트 {i + 1}번째 줄 컬럼 수가 부족합니다: {line}");
                    continue;
                }

                var data = ParseSkillRow(cols);
                if (data == null || string.IsNullOrEmpty(data.Skill_ID)) continue;

                if (!skillDictionary.ContainsKey(data.Skill_ID))
                {
                    skillDictionary.Add(data.Skill_ID, data);
                    skillDataList.Add(data);
                }
                else
                {
                    Debug.LogWarning($"[SkillCatalogManager] Skill_ID가 중복되었습니다: {data.Skill_ID} (먼저 등록된 항목을 유지합니다)");
                }
            }
        }

        /// <summary>
        /// 시트 한 줄(컬럼 배열)을 런타임 SkillData로 변환한다.
        /// 컬럼 순서: Skill_ID, Name, Damage, Cooldown, Grade, Description.
        /// </summary>
private SkillData ParseSkillRow(string[] cols)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();

            data.Skill_ID = cols[0].Trim();
            data.SkillName = cols[1].Trim();
            data.DamagePercent = ParseFloat(cols[2]);
            data.Cooldown = ParseFloat(cols[3]);
            data.Grade = ParseInt(cols[4]);
            data.Description = cols[5].Trim();
            data.FormType = cols.Length >= 7 ? ParseFormType(cols[6]) : SkillFormType.Instant;

            data.SkillIcon = iconTable != null ? iconTable.GetIcon(data.Skill_ID) : null;
            data.ProjectileId = cols.Length >= 8 ? cols[7].Trim() : string.Empty;
            data.ProjectileSpeed = cols.Length >= 9 ? ParseFloat(cols[8]) : 0f;
            data.ProjectileLifetime = cols.Length >= 10 ? ParseFloat(cols[9]) : 0f;
            data.ProjectilePrefab = projectileTable != null ? projectileTable.GetPrefab(data.ProjectileId) : null;
            data.HitCount = cols.Length >= 11 ? System.Math.Max(1, ParseInt(cols[10])) : 1;
            data.DamageType = cols.Length >= 12 ? ParseDamageType(cols[11]) : WeaponDamageType.Physical;
            // [멤] 무기 고유 스킬(기본공격/돌진기) 지원용 컬럼. 기존 12컬럼 데이터는 그대로 동작한다(Projectile/0/0).
            data.CastType = cols.Length >= 13 ? ParseCastType(cols[12]) : SkillCastType.Projectile;
            data.DashDistance = cols.Length >= 14 ? ParseFloat(cols[13]) : 0f;
            data.DashDuration = cols.Length >= 15 ? ParseFloat(cols[14]) : 0f;


            return data;
        }

        private static SkillFormType ParseFormType(string s)
        {
            var trimmed = s.Trim();
            return System.Enum.TryParse<SkillFormType>(trimmed, true, out var value) ? value : SkillFormType.Instant;
        }

        // [멤] csv의 DamageType 컬럼("Physical"/"Magic")을 enum으로 파싱한다. 비어있거나 알 수 없는 값은 기본값(Physical)으로 처리한다.
        private static WeaponDamageType ParseDamageType(string s)
        {
            var trimmed = s.Trim();
            return System.Enum.TryParse<WeaponDamageType>(trimmed, true, out var value) ? value : WeaponDamageType.Physical;
        }

        // [멤] csv의 CastType 컬럼("Projectile"/"Dash")을 enum으로 파싱한다. 비어있거나 알 수 없는 값은 Projectile로 처리한다.
        private static SkillCastType ParseCastType(string s)
        {
            var trimmed = s.Trim();
            return System.Enum.TryParse<SkillCastType>(trimmed, true, out var value) ? value : SkillCastType.Projectile;
        }

        private static int ParseInt(string s)
        {
            return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        private static float ParseFloat(string s)
        {
            return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
        }

        /// <summary>Skill_ID로 SkillData를 찾는다. 목록에 없으면 null.</summary>
        public SkillData FindSkillData(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return null;
            return skillDictionary.TryGetValue(skillId, out var data) ? data : null;
        }

        /// <summary>
        /// 다른 스크립트가 들고 있는 SkillCatalogManager 참조가 비어있을 때 쓰는 공용 폴백 탐색.
        /// (ItemCatalogManager.Resolve와 동일한 패턴)
        /// </summary>
        public static SkillCatalogManager Resolve(SkillCatalogManager existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<SkillCatalogManager>();
            if (found == null)
            {
                Debug.LogWarning("[SkillCatalogManager] 씬에서 SkillCatalogManager를 찾을 수 없습니다.");
            }

            return found;
        }
    }
}
