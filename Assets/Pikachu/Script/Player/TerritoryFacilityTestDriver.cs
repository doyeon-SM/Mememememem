// ============================================================================
// TerritoryFacilityTestDriver.cs
// 영지 테스트 씬 전용 — 여러 생산시설을 런타임 생성하고 멤 작업을 검증 (멤 담당자 테스트용)
//
// [목적]
// 영지 UI(멤 슬롯 드래그 / 가동 버튼) 없이도, 실제 시설의 public API를 호출해
// 진짜 이벤트(MemAdded / FacilityStarted / FacilityStopped)를 발생시킵니다.
// 이 이벤트가 FacilityEventBridge를 통해 멤의 FacilityWorkState를 구동하므로,
// "시설 설치 → 멤 배정 → 가동 → 작업 애니메이션" 전체 배선을 실제 그대로 검증합니다.
//
// [비침투 / 제거 안전]
// - 영지(_Kyusoo) 스크립트/씬 원본을 전혀 수정하지 않습니다. 시설 public 메서드만 호출.
// - 테스트가 끝나면 이 오브젝트만 지우면 흔적이 남지 않습니다.
//
// [씬 설정]
// 1. 빈 GameObject 생성 → "TerritoryFacilityTestDriver" → 이 컴포넌트 부착.
// 2. Inspector의 facilities 목록에 시설을 추가:
//    - prefab: Assets/_Kyusoo/03.Prefab/Building/ 의 시설 (Crafting Table / Berry Farm /
//              Logging Camp / Mining Camp). ※ 발전기/목장/운반은 프리팹 없음.
//    - position: 그리드 좌표 (타일 중심). 겹치지 않게 서로 다른 좌표로. 예: (1.5,0,1.5), (3.5,0,3.5)
// 3. produceItem에 아이템(ItemData) 하나 지정 (Assets/_Kyusoo/SOData/Product/Item_*.asset).
// 4. 먼저 TerritoryWanderTester로 멤을 여러 마리 소환(F1)해 두세요. 드라이버가 각 시설
//    타입에 맞는(필요 스탯 보유) 멤을 자동으로 골라 배정합니다.
//
// [키보드 (Play Mode)]
//   F8 → 모든 시설 (재)생성
//   F6 → 각 시설에 적합한 멤을 배정 + 가동 (한 번에 전체)
//   F7 → 모든 시설의 멤 배정 해제 / 중지
//
// [배정 가능 스탯] Workshop=Crafting, LoggingCamp=Logging, MiningCamp=Mining,
//   Farm/Ranch=Farming, Transport/Generator=Transport (멤 productionStats ≥ 1 필요)
// ============================================================================

using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using MemSystem.Core;
using MemSystem.Data;
using HDY.Item;
using HDY.Capture;

namespace Pikachu.Test
{
    /// <summary>
    /// 여러 생산시설을 런타임 생성하고, 각 시설에 적합한 배회 멤을 배정해 작업시키는 테스트 드라이버.
    /// ProductionCraftRuntime(제작대) / ProductionFacilityRuntime(벌목·채굴·밭) 모두 지원.
    /// </summary>
    public class TerritoryFacilityTestDriver : MonoBehaviour
    {
        // =================================================================
        // Inspector 설정
        // =================================================================

        [System.Serializable]
        public class FacilitySpawnSpec
        {
            [Tooltip("생성할 시설 프리팹 (Assets/_Kyusoo/03.Prefab/Building/).")]
            public GameObject prefab;

            [Tooltip("생성 좌표 (그리드 타일 중심, y=0). 시설끼리 겹치지 않게 다른 값으로.")]
            public Vector3 position = new Vector3(2.5f, 0f, 2.5f);
        }

        [Header("시설 목록 (여러 개 배치 가능)")]
        [SerializeField] private FacilitySpawnSpec[] facilities;

        [Header("생산 아이템")]
        [Tooltip("가동 시 생산할 아이템(ItemData). 애니메이션 검증엔 종류 무관.")]
        [SerializeField] private ItemData produceItem;

        [Header("옵션")]
        [Tooltip("Play 시작 시 모든 시설을 자동 생성합니다.")]
        [SerializeField] private bool spawnOnStart = true;

        [Tooltip("특정 멤만 쓰려면 지정. 비우면 배회 멤 중 각 시설에 맞는 멤을 자동 선택.")]
        [SerializeField] private MemData deployMemOverride;

        [Tooltip("켜면 식량 부족(굶주림)으로 가동이 막히는 것을 우회합니다. 테스트 씬 필수.")]
        [SerializeField] private bool bypassFoodStarvation = true;

        // =================================================================
        // 내부 상태
        // =================================================================

        /// <summary>생성된 시설 하나의 런타임 정보.</summary>
        private class FacInst
        {
            public GameObject go;
            public ProductionCraftRuntime craft;
            public ProductionFacilityRuntime facility;
            public MemData deployedMem;
            public Mem deployedMemObj;

            public BuildingType Type =>
                craft != null && craft.buildingData != null ? craft.buildingData.buildingType :
                facility != null && facility.buildingData != null ? facility.buildingData.buildingType :
                BuildingType.Workshop;

            public bool IsProducing => craft != null ? craft.isProducing
                                     : facility != null && facility.isProducing;
        }

        private readonly List<FacInst> instances = new List<FacInst>();
        private string lastEvent = "-";

        private bool stylesInitialized;
        private GUIStyle boxStyle, headerStyle, labelStyle, buttonStyle;

        // =================================================================
        // Unity Lifecycle
        // =================================================================

        private void Start()
        {
            EnsureEventBridge();
            if (spawnOnStart) SpawnAll();
        }

        private void Update()
        {
            if (bypassFoodStarvation) ForceNoStarvation();

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f6Key.wasPressedThisFrame) DeployAll();
            if (kb.f7Key.wasPressedThisFrame) StopAll();
            if (kb.f8Key.wasPressedThisFrame) SpawnAll();
        }

        // =================================================================
        // 시설 생성
        // =================================================================

        /// <summary>facilities 목록의 모든 시설을 (재)생성합니다. (F8)</summary>
        public void SpawnAll()
        {
            // 기존 생성분 제거
            foreach (var inst in instances)
                if (inst.go != null) Destroy(inst.go);
            instances.Clear();

            if (facilities == null || facilities.Length == 0)
            {
                lastEvent = "⚠ facilities 목록이 비어있습니다. Inspector에서 시설을 추가하세요.";
                return;
            }

            foreach (var spec in facilities)
            {
                if (spec == null || spec.prefab == null) continue;

                var go = Instantiate(spec.prefab, spec.position, Quaternion.identity);
                go.name = spec.prefab.name + " (TestDriver)";
                AddCarveObstacle(go);

                instances.Add(new FacInst
                {
                    go       = go,
                    craft    = go.GetComponent<ProductionCraftRuntime>(),
                    facility = go.GetComponent<ProductionFacilityRuntime>()
                });
            }

            lastEvent = $"🏭 시설 {instances.Count}개 생성";
            Debug.Log($"[TerritoryFacilityTestDriver] 시설 {instances.Count}개 생성 완료.");
        }

        /// <summary>
        /// 시설에 카빙 NavMeshObstacle을 붙여 멤이 시설 위/안으로 못 가게 합니다.
        /// 크기는 시설의 실제 격자 footprint(BuildingData.width×height, 보통 1×1)로만 —
        /// 시각 모델(tripo) 전체 바운드로 카빙하면 좁은 그리드가 뭉텅 잘려 멤이 갇히므로.
        /// 카빙(구멍)은 길찾기가 애초에 우회하게 만들어 진동 없이 깔끔합니다.
        /// </summary>
        private void AddCarveObstacle(GameObject go)
        {
            // 시설 격자 footprint 가져오기 (없으면 1×1)
            BuildingData bd = null;
            var c = go.GetComponent<ProductionCraftRuntime>();
            if (c != null) bd = c.buildingData;
            if (bd == null)
            {
                var f = go.GetComponent<ProductionFacilityRuntime>();
                if (f != null) bd = f.buildingData;
            }
            float w = (bd != null && bd.width  > 0) ? bd.width  : 1f;
            float h = (bd != null && bd.height > 0) ? bd.height : 1f;

            var obs = go.GetComponent<NavMeshObstacle>();
            if (obs == null) obs = go.AddComponent<NavMeshObstacle>();
            obs.shape = NavMeshObstacleShape.Box;
            obs.carving = true;
            obs.carveOnlyStationary = true;

            // 카빙을 아주 작게(중앙 코어만) → 멤이 시설에 바짝 붙어 작업 가능.
            // (한가운데 관통만 막고, 접근은 최대한 허용)
            const float carveScale = 0.3f;
            Vector3 ls = go.transform.lossyScale;
            obs.center = new Vector3(0f, 0.5f, 0f);
            obs.size   = new Vector3(
                w * carveScale / Mathf.Max(ls.x, 0.0001f),
                1.5f          / Mathf.Max(ls.y, 0.0001f),
                h * carveScale / Mathf.Max(ls.z, 0.0001f));
        }

        // =================================================================
        // 배정 / 가동 / 중지
        // =================================================================

        /// <summary>각 시설에 적합한 배회 멤을 배정하고 가동합니다. (F6)</summary>
        public void DeployAll()
        {
            if (produceItem == null)
            {
                lastEvent = "⚠ produceItem이 비어있습니다.";
                Debug.LogWarning("[TerritoryFacilityTestDriver] produceItem이 필요합니다.");
                return;
            }
            if (instances.Count == 0)
            {
                lastEvent = "⚠ 생성된 시설이 없습니다. (F8)";
                return;
            }

            // 이미 배정된 멤은 중복 배정 금지
            var used = new HashSet<string>();
            foreach (var inst in instances)
                if (inst.deployedMem != null) used.Add(inst.deployedMem.memId);

            int deployed = 0;
            foreach (var inst in instances)
                if (DeployToInstance(inst, used)) deployed++;

            lastEvent = $"▶ {deployed}/{instances.Count}개 시설 배정+가동";
        }

        private bool DeployToInstance(FacInst inst, HashSet<string> used)
        {
            if (inst.deployedMem != null) return false; // 이미 배정됨

            BuildingType type = inst.Type;
            MemData mem = PickDeployableMem(type, used);
            if (mem == null)
            {
                ProductionStatType need = ProductionCalculator.GetRequiredStatType(type);
                lastEvent = $"⚠ {type}: 배정 가능 멤 없음 (필요 {need}≥1)";
                return false;
            }

            var entry = new CapturedMemEntry
            {
                KeyId = "TESTDRIVER-" + mem.memId,
                MemId = mem.memId,
                ExplorationStat = 0,
                IsActive = false
            };

            bool added = inst.craft != null ? inst.craft.TryAddMem(mem, entry)
                                            : inst.facility.TryAddMem(mem, entry);
            if (!added) return false;

            inst.deployedMem = mem;
            used.Add(mem.memId);
            if (TerritoryWanderSpawner.Instance != null)
                TerritoryWanderSpawner.Instance.ActiveWanderers.TryGetValue(mem.memId, out inst.deployedMemObj);

            // 가동 시작
            if (inst.craft != null)
            {
                inst.craft.SelectAndStartCrafting(produceItem, 1);
            }
            else
            {
                inst.facility.craftingItem = produceItem;
                inst.facility.CheckProductionCondition();
            }
            return true;
        }

        /// <summary>모든 시설의 멤을 해제하고 가동을 중지합니다. (F7)</summary>
        public void StopAll()
        {
            int n = 0;
            foreach (var inst in instances)
            {
                if (inst.deployedMem == null) continue;

                if (inst.craft != null)
                {
                    inst.craft.RemoveMem(inst.deployedMem);
                    inst.craft.CancelCrafting();
                }
                else
                {
                    inst.facility.RemoveMem(inst.deployedMem);
                }
                inst.deployedMem = null;
                inst.deployedMemObj = null;
                n++;
            }
            lastEvent = $"■ {n}개 시설 중지/해제";
        }

        /// <summary>해당 시설 타입에 배정 가능하며 아직 안 쓰인 배회 멤의 MemData를 반환.</summary>
        private MemData PickDeployableMem(BuildingType type, HashSet<string> used)
        {
            if (deployMemOverride != null && !used.Contains(deployMemOverride.memId) &&
                ProductionCalculator.CanDeployToFacility(deployMemOverride, type))
                return deployMemOverride;

            if (TerritoryWanderSpawner.Instance == null) return null;

            foreach (var pair in TerritoryWanderSpawner.Instance.ActiveWanderers)
            {
                Mem m = pair.Value;
                if (m == null || m.Data == null) continue;
                if (used.Contains(m.Data.memId)) continue;
                if (ProductionCalculator.CanDeployToFacility(m.Data, type))
                    return m.Data;
            }
            return null;
        }

        // =================================================================
        // FacilityEventBridge / 굶주림 우회
        // =================================================================

        private void EnsureEventBridge()
        {
            if (FindFirstObjectByType<FacilityEventBridge>() == null)
            {
                new GameObject("FacilityEventBridge (TestDriver)").AddComponent<FacilityEventBridge>();
                Debug.Log("[TerritoryFacilityTestDriver] 씬에 FacilityEventBridge가 없어 자동 생성했습니다.");
            }
        }

        private FieldInfo starvationField;
        private bool starvationFieldResolved;

        private void ForceNoStarvation()
        {
            var cf = ConsumeFoodSystem.Instance;
            if (cf == null) return;

            if (!starvationFieldResolved)
            {
                starvationField = typeof(ConsumeFoodSystem).GetField(
                    "isWorkStoppedDueToStarvation",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                starvationFieldResolved = true;
            }
            starvationField?.SetValue(cf, false);
        }

        // =================================================================
        // OnGUI — 런타임 테스트 패널
        // =================================================================

        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            InitStyles();

            float w = 480f;
            float h = 140f + instances.Count * 44f;
            float x = 16f, y = 400f;

            GUI.Box(new Rect(x - 8, y - 8, w + 16, h + 16), GUIContent.none, boxStyle);
            GUILayout.BeginArea(new Rect(x, y, w, h));

            GUILayout.Label("🏭 시설 구동 테스터 (다중)", headerStyle);
            GUILayout.Label($"produceItem: {(produceItem != null ? produceItem.name : "미지정 ⚠")}  |  시설 {instances.Count}개", labelStyle);
            GUILayout.Label($"마지막: {lastEvent}", labelStyle);
            GUILayout.Space(4);

            // 시설별 상태
            foreach (var inst in instances)
            {
                string memName = inst.deployedMem != null ? inst.deployedMem.memName : "-";
                string anim = inst.deployedMemObj != null && inst.deployedMemObj.Visual != null
                              ? inst.deployedMemObj.Visual.CurrentAnimState.ToString() : "-";
                GUILayout.Label($"· {inst.Type}  |  가동:{inst.IsProducing}  |  멤:{memName}  애니:{anim}", labelStyle);
            }
            GUILayout.Space(6);

            if (GUILayout.Button("[F6] 전체 배정+가동", buttonStyle, GUILayout.Height(42))) DeployAll();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("[F7] 전체 중지", buttonStyle, GUILayout.Height(38))) StopAll();
            if (GUILayout.Button("[F8] 시설 재생성", buttonStyle, GUILayout.Height(38))) SpawnAll();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTex(1, 1, new Color(0.15f, 0.1f, 0.1f, 0.92f));

            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            headerStyle.normal.textColor = new Color(1f, 0.7f, 0.4f);

            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            labelStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = col;
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
