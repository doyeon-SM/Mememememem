// ============================================================================
// TerritoryWanderTester.cs
// 영지 자유 배회(TerritoryWanderSpawner) 독립 테스트 스크립트 — 멤 담당자 전용
//
// [용도]
// 실제 영지 소환 호출부(영지 담당자 구현)가 없어도, 멤 쪽에서 단독으로
// TerritoryWanderSpawner.SpawnWanderer / RecallWanderer 동작을 검증합니다.
//
// [씬 설정 방법]
// 1. 영지(Territory) 씬에 TerritoryWanderSpawner 오브젝트가 이미 배치되어 있어야 합니다.
//    (memPool 연결 + BoxCollider 배회 경계 설정 완료 상태)
// 2. 빈 GameObject 생성 → 이름: "TerritoryWanderTester"
// 3. 이 컴포넌트 부착
// 4. Inspector의 testMemData 슬롯에 소환할 MemData SO를 넣으세요.
//    (예: Mem_Rare_01, Mem_Epic_01, Mem_Unique_01 — Assets/Pikachu/Data)
//
// [키보드 단축키 (Play Mode, New Input System)]
//   F1  → 다음 멤 1마리 소환 (testMemData 순서대로)
//   F2  → 소환된 멤 전원 회수
//   F3  → 방금 소환한 멤만 회수
//
// [주의]
// - TerritoryWanderSpawner는 같은 memId 중복 소환을 막습니다.
//   즉 testMemData에 넣은 서로 다른 MemData 개수만큼만 동시에 배회 가능합니다.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using MemSystem.Core;
using MemSystem.Data;

namespace Pikachu.Test
{
    /// <summary>
    /// 영지 자유 배회 기능 독립 테스트 도구.
    /// TerritoryWanderSpawner를 키보드/버튼으로 구동하여 소환·배회·회수를 검증합니다.
    /// </summary>
    public class TerritoryWanderTester : MonoBehaviour
    {
        // =================================================================
        // Inspector 설정
        // =================================================================

        [Header("소환할 멤 데이터")]
        [Tooltip("F1로 순서대로 소환할 MemData 목록. Assets/Pikachu/Data의 SO를 넣으세요.")]
        [SerializeField] private MemData[] testMemData;

        [Header("소환 위치 (선택)")]
        [Tooltip("지정하면 이 위치에 소환. 비우면 TerritoryWanderSpawner의 기본 지점(경계 중심 등) 사용.")]
        [SerializeField] private Transform spawnPointOverride;

        [Tooltip("여러 마리가 겹치지 않도록 소환 위치에 추가할 랜덤 반경(m).")]
        [SerializeField] private float spawnScatterRadius = 1.5f;

        // =================================================================
        // 내부 상태
        // =================================================================

        private int nextIndex = 0;
        private MemData lastSpawned = null;
        private Mem lastSpawnedMem = null;   // 진단용: 마지막 소환 멤 인스턴스
        private string lastEvent = "-";

        // OnGUI 스타일 캐시
        private bool stylesInitialized = false;
        private GUIStyle boxStyle, headerStyle, labelStyle, buttonStyle;

        // =================================================================
        // Unity Lifecycle
        // =================================================================

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f1Key.wasPressedThisFrame) SpawnNext();
            if (kb.f2Key.wasPressedThisFrame) RecallAll();
            if (kb.f3Key.wasPressedThisFrame) RecallLast();
        }

        // =================================================================
        // 테스트 동작
        // =================================================================

        /// <summary>testMemData 목록에서 다음 멤을 1마리 소환합니다. (F1)</summary>
        public void SpawnNext()
        {
            if (!ValidateSpawner()) return;

            if (testMemData == null || testMemData.Length == 0)
            {
                lastEvent = "⚠ testMemData가 비어있습니다.";
                Debug.LogWarning("[TerritoryWanderTester] testMemData가 비어있습니다. Inspector에서 MemData를 넣어주세요.");
                return;
            }

            // 목록을 순환하며, 아직 영지에 없는 멤을 찾아 소환
            for (int i = 0; i < testMemData.Length; i++)
            {
                MemData data = testMemData[nextIndex % testMemData.Length];
                nextIndex++;

                if (data == null) continue;

                // 이미 소환되어 있으면 건너뜀 (중복 소환 방지 규칙)
                if (TerritoryWanderSpawner.Instance.ActiveWanderers.ContainsKey(data.memId))
                    continue;

                Vector3 pos = ResolveSpawnPosition();
                Mem mem = TerritoryWanderSpawner.Instance.SpawnWanderer(data, pos);

                if (mem != null)
                {
                    lastSpawned = data;
                    lastSpawnedMem = mem;
                    lastEvent = $"✅ 소환: {data.memName} ({CountText()})";
                    // 스폰 직후 NavMesh 안착 여부 즉시 로그
                    Debug.Log($"[TerritoryWanderTester] {data.memName} 소환 위치={mem.transform.position}, " +
                              $"이동상태 → {mem.Movement?.DebugAgentStatus()}");
                }
                else
                {
                    lastEvent = $"⚠ 소환 실패: {data.memName}";
                }
                return;
            }

            lastEvent = "ℹ 목록의 멤이 모두 이미 영지에 있습니다.";
        }

        /// <summary>소환된 멤 전원을 회수합니다. (F2)</summary>
        public void RecallAll()
        {
            if (!ValidateSpawner()) return;

            int before = TerritoryWanderSpawner.Instance.WandererCount;
            TerritoryWanderSpawner.Instance.RecallAllWanderers();
            lastSpawned = null;
            lastEvent = $"↩ 전원 회수 ({before}마리)";
        }

        /// <summary>방금 소환한 멤만 회수합니다. (F3)</summary>
        public void RecallLast()
        {
            if (!ValidateSpawner()) return;

            if (lastSpawned == null)
            {
                lastEvent = "ℹ 회수할 최근 소환 멤이 없습니다.";
                return;
            }

            bool ok = TerritoryWanderSpawner.Instance.RecallWanderer(lastSpawned);
            lastEvent = ok ? $"↩ 회수: {lastSpawned.memName} ({CountText()})"
                           : $"⚠ 회수 실패: {lastSpawned.memName}";
            lastSpawned = null;
        }

        // =================================================================
        // 내부 유틸리티
        // =================================================================

        private bool ValidateSpawner()
        {
            if (TerritoryWanderSpawner.Instance == null)
            {
                lastEvent = "⚠ 씬에 TerritoryWanderSpawner가 없습니다.";
                Debug.LogWarning("[TerritoryWanderTester] TerritoryWanderSpawner.Instance가 없습니다. 씬에 배치했는지 확인하세요.");
                return false;
            }
            return true;
        }

        /// <summary>소환 위치를 결정합니다. override가 있으면 그 위치 + 랜덤 산포, 없으면 default.</summary>
        private Vector3 ResolveSpawnPosition()
        {
            if (spawnPointOverride == null)
                return default; // 스포너가 자체 기본 지점(경계 중심 등)으로 결정

            Vector2 r = Random.insideUnitCircle * spawnScatterRadius;
            Vector3 basePos = spawnPointOverride.position;
            return new Vector3(basePos.x + r.x, basePos.y, basePos.z + r.y);
        }

        private string CountText()
        {
            return $"현재 {TerritoryWanderSpawner.Instance.WandererCount}마리 배회 중";
        }

        // =================================================================
        // OnGUI — 런타임 테스트 패널
        // =================================================================

        private void OnGUI()
        {
            if (!Application.isPlaying) return;

            InitStyles();

            float panelWidth  = 460f;
            float panelHeight = 360f;
            float x = 16f;
            float y = 16f;

            GUI.Box(new Rect(x - 8, y - 8, panelWidth + 16, panelHeight + 16), GUIContent.none, boxStyle);
            GUILayout.BeginArea(new Rect(x, y, panelWidth, panelHeight));

            GUILayout.Label("🚶 영지 배회 테스터", headerStyle);
            GUILayout.Space(4);

            bool hasSpawner = TerritoryWanderSpawner.Instance != null;
            GUILayout.Label($"스포너: {(hasSpawner ? "연결됨" : "없음 ⚠")}", labelStyle);
            GUILayout.Label($"배회 중: {(hasSpawner ? TerritoryWanderSpawner.Instance.WandererCount : 0)}마리", labelStyle);
            GUILayout.Label($"등록된 MemData: {(testMemData != null ? testMemData.Length : 0)}종", labelStyle);
            GUILayout.Label($"마지막: {lastEvent}", labelStyle);

            // 진단: 마지막 소환 멤의 실시간 상태
            if (lastSpawnedMem != null)
            {
                string aiState = lastSpawnedMem.AI?.CurrentState?.GetType().Name ?? "?";
                GUILayout.Label($"AI 상태: {aiState}", labelStyle);
                GUILayout.Label($"이동: {lastSpawnedMem.Movement?.DebugAgentStatus()}", labelStyle);
            }
            GUILayout.Space(8);

            if (GUILayout.Button("[F1] 다음 멤 소환", buttonStyle, GUILayout.Height(46))) SpawnNext();
            GUILayout.Space(4);
            if (GUILayout.Button("[F3] 방금 멤 회수", buttonStyle, GUILayout.Height(46))) RecallLast();
            if (GUILayout.Button("[F2] 전원 회수", buttonStyle, GUILayout.Height(46))) RecallAll();

            GUILayout.EndArea();
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTex(1, 1, new Color(0.1f, 0.1f, 0.15f, 0.92f));

            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize  = 22;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = new Color(0.4f, 0.9f, 1f);

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 16;
            labelStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 15;
            buttonStyle.fontStyle = FontStyle.Bold;
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
