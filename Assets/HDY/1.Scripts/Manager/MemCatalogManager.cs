using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MemSystem.Data;

namespace HDY.Mem
{
    /// <summary>
    /// 멤 데이터(MemData)를 보관하는 매니저.
    /// [교통정리] 멤 데이터 정의는 Pikachu 팀의 MemSystem.Data.MemData를 그대로 사용한다.
    /// Mem_ID(memId)를 키로 하는 딕셔너리 탐색을 전제로 함.
    /// 씬에 배치되어 DontDestroyOnLoad로 유지되는 파괴불가 싱글톤.
    /// [ItemCatalogManager와 동일한 패턴] FindMemData/Resolve 구조를 그대로 맞췄다 - 다른 스크립트가
    /// 이미 ItemCatalogManager를 다루는 방식과 동일하게 MemCatalogManager도 다룰 수 있도록 하기 위함.
    ///
    /// [HDY 요청 - 시트 마이그레이션] 인스펙터에 개별 MemData SO를 하나씩 드래그하던 방식에서
    /// 시트(TextAsset, 쉼표 구분 CSV) 기반으로 전환했다. Awake 시 시트를 파싱해 행마다
    /// ScriptableObject.CreateInstance&lt;MemData&gt;()로 런타임 인스턴스를 만들어 채운다.
    /// (ItemCatalogManager가 이미 쓰던 것과 동일한 패턴이며, MemData.cs 자체는 Pikachu 소유라 건드리지 않는다.)
    /// 외형(모델 프리팹)은 시트에 담을 수 없어 MemAppearanceTable로 따로 분리해 관리한다.
    ///
    /// [HDY 요청 - 영지 배고픔 시스템] MemCatalog.csv의 MaxHunger 바로 뒤에 Consumption(분당 배고픔
    /// 소비량) 컬럼이 추가되어, 그 뒤 컬럼들의 인덱스가 전부 1칸씩 밀렸다.
    ///
    /// [HDY 요청 - 악세서리 지원] MemData.accessories(Pikachu가 추가)도 GameObject 참조를 담은
    /// MemAccessoryData[]라 시트에 직접 담을 수 없다. AccessoryIds 컬럼(맨 끝, 세미콜론 구분
    /// accessoryId 목록, 예: "acc_head_strawhat;acc_head_daisy")에 문자열 ID만 적어두고,
    /// MemAccessoryTable에서 실제 MemAccessoryData 에셋을 찾아 연결한다(MemAppearanceTable과 동일한
    /// 방식). MemIconBakerWindow도 ParseAccessoryIdsByMemId를 그대로 재사용해서, 같은 CSV를 기준으로
    /// 아이콘을 굽는다(파싱 로직 중복 방지).
    /// </summary>
    public class MemCatalogManager : MonoBehaviour
    {
        public static MemCatalogManager Instance { get; private set; }

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

        [Header("멤 데이터 시트 (쉼표 구분 CSV, Mem_ID 기준으로 파싱)")]
        [SerializeField] private TextAsset memCatalogSheet;

        [Header("멤 외형 테이블 (Mem_ID -> 모델 프리팹)")]
        [SerializeField] private MemAppearanceTable appearanceTable;

        [Header("멤 악세서리 테이블 (accessoryId -> MemAccessoryData)")]
        [SerializeField] private MemAccessoryTable accessoryTable;

        private readonly List<MemData> memDataList = new List<MemData>();
        public IReadOnlyList<MemData> MemDataList => memDataList;

        [Header("memId -> MemData 딕셔너리")]
        private Dictionary<string, MemData> memDictionary = new Dictionary<string, MemData>();

        /// <summary>
        /// 시트를 파싱해 행마다 런타임 MemData 인스턴스를 만들고 memId 기준으로 딕셔너리에 채운다.
        /// memId가 중복되면 먼저 등록된 항목을 유지한다.
        /// </summary>
        private void BuildDictionary()
        {
            memDictionary.Clear();
            memDataList.Clear();

            if (memCatalogSheet == null)
            {
                Debug.LogWarning("[MemCatalogManager] memCatalogSheet가 비어있습니다.");
                return;
            }

            var lines = memCatalogSheet.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0번째 줄은 헤더라 건너뜀
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(',');
                if (cols.Length < 23)
                {
                    Debug.LogWarning($"[MemCatalogManager] 멤 시트 {i + 1}번째 줄 컬럼 수가 부족합니다: {line}");
                    continue;
                }

                var data = ParseMemRow(cols);
                if (data == null || string.IsNullOrEmpty(data.memId)) continue;

                if (!memDictionary.ContainsKey(data.memId))
                {
                    memDictionary.Add(data.memId, data);
                    memDataList.Add(data);
                }
                else
                {
                    Debug.LogWarning($"[MemCatalogManager] memId가 중복되었습니다: {data.memId} (먼저 등록된 항목을 유지합니다)");
                }
            }
        }

        /// <summary>
        /// 시트 한 줄(컬럼 배열)을 런타임 MemData로 변환한다.
        /// 컬럼 순서: Mem_ID, MemName, Tier, Personality, MaxHp, MaxHunger, Consumption,
        /// Crafting, Logging, Mining, Transport, Farming, ExplorationStat,
        /// AttackDamage, AttackRange, AttackCooldown, DetectionRange, FleeHpThreshold,
        /// AllowedZoneIds, CanSpawnDay, CanSpawnNight, SpawnWeight, AccessoryIds.
        /// </summary>
        private MemData ParseMemRow(string[] cols)
        {
            var data = ScriptableObject.CreateInstance<MemData>();

            data.memId = cols[0].Trim();
            data.memName = cols[1].Trim();
            data.tier = ParseEnum<MemTier>(cols[2]);
            data.personality = ParseEnum<MemPersonality>(cols[3]);
            data.maxHp = ParseInt(cols[4]);
            data.maxHunger = ParseInt(cols[5]);
            data.consumption = ParseInt(cols[6]);

            data.productionStats = new ProductionStats
            {
                crafting = ParseInt(cols[7]),
                logging = ParseInt(cols[8]),
                mining = ParseInt(cols[9]),
                transport = ParseInt(cols[10]),
                farming = ParseInt(cols[11]),
            };

            data.explorationStat = ParseInt(cols[12]);
            data.attackDamage = ParseInt(cols[13]);
            data.attackRange = ParseFloat(cols[14]);
            data.attackCooldown = ParseFloat(cols[15]);
            data.detectionRange = ParseFloat(cols[16]);
            data.fleeHpThreshold = ParseFloat(cols[17]);

            data.spawnCondition = new SpawnCondition
            {
                allowedZoneIds = ParseSemicolonList(cols[18]),
                canSpawnDay = ParseBool(cols[19]),
                canSpawnNight = ParseBool(cols[20]),
                spawnWeight = ParseFloat(cols[21]),
            };

            data.modelPrefab = appearanceTable != null ? appearanceTable.GetAppearance(data.memId) : null;
            data.accessories = ResolveAccessories(cols[22]);

            return data;
        }

        /// <summary>AccessoryIds 컬럼("acc_head_strawhat;acc_head_daisy")을 accessoryTable로 실제 에셋 배열로 바꾼다.</summary>
        private MemAccessoryData[] ResolveAccessories(string rawAccessoryIds)
        {
            var ids = ParseSemicolonList(rawAccessoryIds);
            if (ids.Length == 0) return System.Array.Empty<MemAccessoryData>();

            if (accessoryTable == null)
            {
                Debug.LogWarning("[MemCatalogManager] accessoryTable이 비어있어 AccessoryIds를 해석하지 못했습니다.");
                return System.Array.Empty<MemAccessoryData>();
            }

            var resolved = new List<MemAccessoryData>(ids.Length);
            foreach (var id in ids)
            {
                var accessory = accessoryTable.GetAccessory(id);
                if (accessory == null)
                {
                    Debug.LogWarning($"[MemCatalogManager] accessoryId '{id}'를 MemAccessoryTable에서 찾을 수 없습니다.");
                    continue;
                }
                resolved.Add(accessory);
            }

            return resolved.ToArray();
        }

        private static int ParseInt(string s)
        {
            return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        private static float ParseFloat(string s)
        {
            return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
        }

        private static bool ParseBool(string s)
        {
            return bool.TryParse(s.Trim(), out var value) && value;
        }

        private static T ParseEnum<T>(string s) where T : struct
        {
            return System.Enum.TryParse(s.Trim(), out T value) ? value : default;
        }

        /// <summary>"zone_a;zone_b" 형식을 파싱한다. 빈 문자열이면 빈 배열을 반환한다.
        /// AllowedZoneIds/AccessoryIds 둘 다 같은 세미콜론 구분 형식이라 공용으로 쓴다.</summary>
        private static string[] ParseSemicolonList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new string[0];

            var entries = raw.Split(';');
            var list = new List<string>();
            foreach (var entry in entries)
            {
                var value = entry.Trim();
                if (!string.IsNullOrEmpty(value)) list.Add(value);
            }
            return list.ToArray();
        }

        /// <summary>memId로 MemData를 찾는다. 목록에 없으면 null.</summary>
        public MemData FindMemData(string memId)
        {
            if (string.IsNullOrEmpty(memId)) return null;
            return memDictionary.TryGetValue(memId, out var data) ? data : null;
        }

        /// <summary>
        /// 다른 스크립트가 들고 있는 MemCatalogManager 참조가 비어있을 때 공용으로 쓰는 폴백 탐색.
        /// 1) 이미 참조가 있으면 그대로 반환, 2) 없으면 싱글톤(Instance), 3) 그래도 없으면 씬 전체에서 검색.
        /// (ItemCatalogManager.Resolve와 동일한 패턴)
        /// </summary>
        public static MemCatalogManager Resolve(MemCatalogManager existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<MemCatalogManager>();
            if (found == null)
            {
                Debug.LogWarning("[MemCatalogManager] 씬에서 MemCatalogManager를 찾을 수 없습니다.");
            }

            return found;
        }

#if UNITY_EDITOR
        /// <summary>
        /// [HDY 요청 - 아이콘 굽기 도구 연동] MemIconBakerWindow가 런타임 매니저 인스턴스 없이도
        /// "CSV 기준 Mem_ID -> accessoryId 목록"만 뽑아 쓸 수 있도록 하는 에디터 전용 정적 헬퍼.
        /// ParseMemRow와 파싱 로직(컬럼 인덱스, 세미콜론 구분)을 공유해서 두 곳이 어긋나지 않게 한다.
        /// </summary>
        public static Dictionary<string, string[]> EditorParseAccessoryIdsByMemId(TextAsset sheet)
        {
            var result = new Dictionary<string, string[]>();
            if (sheet == null) return result;

            var lines = sheet.text.Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(',');
                if (cols.Length < 23) continue;

                var memId = cols[0].Trim();
                if (string.IsNullOrEmpty(memId)) continue;

                result[memId] = ParseSemicolonList(cols[22]);
            }

            return result;
        }
#endif
    }
}
