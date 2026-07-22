// ============================================================================
// FacilityAnimationTester.cs
// 시설 작업 애니메이션 독립 테스트 스크립트
//
// [용도]
// 영지 씬 없이 Scene_Test_Pika에서 시설별 멤 애니메이션을 단독 검증합니다.
// FacilityWorkState를 직접 구동하므로 영지 이벤트 시스템 없이도 동작합니다.
//
// [씬 설정 방법]
// 1. Scene_Test_Pika에 빈 GameObject 생성 → 이름: "FacilityAnimationTester"
// 2. 이 컴포넌트 부착
// 3. Inspector에서 targetMem 슬롯에 씬의 Mem 오브젝트 드래그
//    (없으면 testMemData + memPool 설정 시 자동 스폰)
//
// [키보드 단축키 (Play Mode)]
//   F1  → 배치 시뮬레이션 (FacilityWorkState 진입, Idle 대기)
//   F2  → 가동 시작 (작업 애니메이션 재생)
//   F3  → 기아로 중지 → HungryState
//   F4  → 제작 완료로 중지 → Idle (제작대 전용)
//   F5  → 제작 취소로 중지 → Idle (제작대 전용)
//   F6  → 해제 → IdleState 복귀
//   F7  → 멤 스폰 (memPool + testMemData 설정 시)
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using MemSystem.Core;
using MemSystem.Data;
using MemSystem.AI;
using MemSystem.AI.States;
using MemSystem.Spawn;

namespace Pikachu.Test
{
    /// <summary>
    /// 시설 애니메이션 독립 테스트 도구.
    /// FacilityWorkState를 직접 조작하여 영지 없이 애니메이션을 검증합니다.
    /// </summary>
    public class FacilityAnimationTester : MonoBehaviour
    {
        // =================================================================
        // Inspector 설정
        // =================================================================

        [Header("테스트 대상")]
        [Tooltip("테스트할 Mem 오브젝트. 씬에 이미 있는 멤을 드래그하세요.")]
        [SerializeField] private Mem targetMem;

        [Header("시설 선택")]
        [Tooltip("테스트할 시설 종류")]
        [SerializeField] private BuildingType testBuildingType = BuildingType.LoggingCamp;

        [Header("자동 스폰 (targetMem이 없을 때)")]
        [Tooltip("멤 풀 (씬에 있으면 자동 탐색)")]
        [SerializeField] private MemPool memPool;
        [Tooltip("스폰할 MemData SO")]
        [SerializeField] private MemData testMemData;
        [Tooltip("스폰 위치")]
        [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 0f, 2f);

        // =================================================================
        // 내부 상태
        // =================================================================

        private MemAI CurrentAI => targetMem != null ? targetMem.AI : null;
        private string currentStateLabel = "없음";
        private string lastEvent = "-";

        // OnGUI 스타일 캐시
        private GUIStyle boxStyle;
        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private bool stylesInitialized = false;

        // =================================================================
        // Unity 생명주기
        // =================================================================

        private void Start()
        {
            if (memPool == null)
                memPool = FindFirstObjectByType<MemPool>();
        }

        private void Update()
        {
            HandleKeyInput();
            UpdateStateLabel();
        }

        // =================================================================
        // 키보드 단축키
        // =================================================================

        private void HandleKeyInput()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.f1Key.wasPressedThisFrame) SimulateMemAdded();
            if (Keyboard.current.f2Key.wasPressedThisFrame) SimulateFacilityStarted();
            if (Keyboard.current.f3Key.wasPressedThisFrame) SimulateFacilityStoppedStarvation();
            if (Keyboard.current.f4Key.wasPressedThisFrame) SimulateFacilityStoppedComplete();
            if (Keyboard.current.f5Key.wasPressedThisFrame) SimulateFacilityStoppedCancel();
            if (Keyboard.current.f6Key.wasPressedThisFrame) SimulateMemRemoved();
            if (Keyboard.current.f7Key.wasPressedThisFrame) SpawnTestMem();
        }

        // =================================================================
        // 테스트 액션 (Inspector 버튼 + 키보드 모두 호출)
        // =================================================================

        /// <summary>멤 배치 시뮬레이션 (F1). FacilityWorkState 진입, Idle 대기.</summary>
        public void SimulateMemAdded()
        {
            if (!ValidateTarget()) return;

            CurrentAI.FacilityWorkState.SetFacility(testBuildingType);
            CurrentAI.TransitionTo(CurrentAI.FacilityWorkState);

            lastEvent = $"✅ MemAdded(배치) → {testBuildingType}";
            Debug.Log($"[FacilityAnimationTester] 배치 시뮬: {testBuildingType} / 멤: {targetMem.Stats?.MemName}");
        }

        /// <summary>시설 가동 시뮬레이션 (F2). 작업 애니메이션 재생.</summary>
        public void SimulateFacilityStarted()
        {
            if (!ValidateTarget()) return;
            if (!EnsureInFacilityWorkState()) return;

            CurrentAI.FacilityWorkState.OnFacilityStarted(CurrentAI);

            lastEvent = $"▶ FacilityStarted → {testBuildingType}";
            Debug.Log($"[FacilityAnimationTester] 가동 시작: {testBuildingType}");
        }

        /// <summary>기아로 중지 시뮬레이션 (F3). → HungryState.</summary>
        public void SimulateFacilityStoppedStarvation()
        {
            if (!ValidateTarget()) return;
            if (!EnsureInFacilityWorkState()) return;

            CurrentAI.FacilityWorkState.OnFacilityStopped(CurrentAI, FacilityStopReason.Starvation);

            lastEvent = "⚠ FacilityStopped → Starvation (HungryState)";
            Debug.Log("[FacilityAnimationTester] 기아 중지 시뮬");
        }

        /// <summary>제작 완료로 중지 시뮬레이션 (F4). → Idle. 제작대 전용.</summary>
        public void SimulateFacilityStoppedComplete()
        {
            if (!ValidateTarget()) return;
            if (!EnsureInFacilityWorkState()) return;

            CurrentAI.FacilityWorkState.OnFacilityStopped(CurrentAI, FacilityStopReason.CompleteCrafting);

            lastEvent = "✔ FacilityStopped → CompleteCrafting (Idle)";
            Debug.Log("[FacilityAnimationTester] 제작 완료 시뮬");
        }

        /// <summary>제작 취소로 중지 시뮬레이션 (F5). → Idle. 제작대 전용.</summary>
        public void SimulateFacilityStoppedCancel()
        {
            if (!ValidateTarget()) return;
            if (!EnsureInFacilityWorkState()) return;

            CurrentAI.FacilityWorkState.OnFacilityStopped(CurrentAI, FacilityStopReason.CancelCrafting);

            lastEvent = "✔ FacilityStopped → CancelCrafting (Idle)";
            Debug.Log("[FacilityAnimationTester] 제작 취소 시뮬");
        }

        /// <summary>멤 해제 시뮬레이션 (F6). → IdleState 복귀.</summary>
        public void SimulateMemRemoved()
        {
            if (!ValidateTarget()) return;

            if (CurrentAI.CurrentState == CurrentAI.FacilityWorkState)
                CurrentAI.TransitionTo(CurrentAI.IdleState);

            lastEvent = "🚪 MemAdded(해제) → IdleState";
            Debug.Log("[FacilityAnimationTester] 해제 시뮬 → IdleState");
        }

        /// <summary>테스트용 멤 스폰 (F7).</summary>
        public void SpawnTestMem()
        {
            if (testMemData == null)
            {
                Debug.LogWarning("[FacilityAnimationTester] testMemData가 없습니다. Inspector에서 MemData SO를 설정하세요.");
                return;
            }
            if (memPool == null)
            {
                Debug.LogWarning("[FacilityAnimationTester] MemPool을 찾지 못했습니다. 씬에 MemPool 오브젝트가 필요합니다.");
                return;
            }

            targetMem = memPool.Spawn(testMemData, spawnPosition);

            if (targetMem != null)
            {
                lastEvent = $"🐾 멤 스폰: {targetMem.Stats?.MemName}";
                Debug.Log($"[FacilityAnimationTester] 멤 스폰 완료: {targetMem.Stats?.MemName}");
            }
        }

        // =================================================================
        // 유틸리티
        // =================================================================

        private bool ValidateTarget()
        {
            if (targetMem == null)
            {
                Debug.LogWarning("[FacilityAnimationTester] targetMem이 없습니다. Inspector에서 Mem을 설정하거나 F7로 스폰하세요.");
                lastEvent = "❌ targetMem 없음";
                return false;
            }
            if (CurrentAI == null)
            {
                Debug.LogWarning("[FacilityAnimationTester] MemAI를 찾을 수 없습니다.");
                lastEvent = "❌ MemAI 없음";
                return false;
            }
            return true;
        }

        /// <summary>현재 FacilityWorkState가 아니면 자동으로 진입시킵니다.</summary>
        private bool EnsureInFacilityWorkState()
        {
            if (CurrentAI.CurrentState != CurrentAI.FacilityWorkState)
            {
                Debug.LogWarning("[FacilityAnimationTester] FacilityWorkState가 아닙니다. 먼저 F1(배치)을 눌러주세요.");
                lastEvent = "⚠ 먼저 F1(배치)을 눌러주세요";
                return false;
            }
            return true;
        }

        private void UpdateStateLabel()
        {
            if (CurrentAI == null)
            {
                currentStateLabel = "없음";
                return;
            }
            currentStateLabel = CurrentAI.CurrentState?.GetType().Name ?? "null";
        }

        // =================================================================
        // OnGUI — 런타임 테스트 패널
        // =================================================================

        private void OnGUI()
        {
            if (!Application.isPlaying) return;

            InitStyles();

            float panelWidth  = 560f;
            float panelHeight = 720f;
            float x = Screen.width - panelWidth - 16f;
            float y = 16f;

            GUI.Box(new Rect(x - 8, y - 8, panelWidth + 16, panelHeight + 16), GUIContent.none, boxStyle);

            GUILayout.BeginArea(new Rect(x, y, panelWidth, panelHeight));

            GUILayout.Label("🏭 시설 애니메이션 테스터", headerStyle);
            GUILayout.Space(4);

            // 상태 표시
            GUILayout.Label($"멤: {(targetMem != null ? targetMem.Stats?.MemName ?? "?" : "없음")}", labelStyle);
            GUILayout.Label($"현재 상태: {currentStateLabel}", labelStyle);
            GUILayout.Label($"시설 선택: {testBuildingType}", labelStyle);
            GUILayout.Label($"마지막 이벤트: {lastEvent}", labelStyle);
            GUILayout.Space(8);

            // 시설 선택 버튼
            GUILayout.Label("▶ 시설 변경", labelStyle);
            GUILayout.BeginHorizontal();
            foreach (BuildingType bt in System.Enum.GetValues(typeof(BuildingType)))
            {
                if (GUILayout.Button(bt.ToString(), buttonStyle, GUILayout.Height(38)))
                {
                    testBuildingType = bt;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            // 이벤트 버튼
            GUILayout.Label("▶ 이벤트 시뮬레이션", labelStyle);

            if (GUILayout.Button("[F7] 멤 스폰", buttonStyle, GUILayout.Height(44))) SpawnTestMem();
            GUILayout.Space(4);
            if (GUILayout.Button("[F1] 배치 (MemAdded: true)", buttonStyle, GUILayout.Height(44))) SimulateMemAdded();
            if (GUILayout.Button("[F2] 가동 시작 (FacilityStarted)", buttonStyle, GUILayout.Height(44))) SimulateFacilityStarted();
            GUILayout.Space(4);
            if (GUILayout.Button("[F3] 기아 중지 → HungryState", buttonStyle, GUILayout.Height(44))) SimulateFacilityStoppedStarvation();
            if (GUILayout.Button("[F4] 제작 완료 → Idle  [제작대 전용]", buttonStyle, GUILayout.Height(44))) SimulateFacilityStoppedComplete();
            if (GUILayout.Button("[F5] 제작 취소 → Idle  [제작대 전용]", buttonStyle, GUILayout.Height(44))) SimulateFacilityStoppedCancel();
            GUILayout.Space(4);
            if (GUILayout.Button("[F6] 해제 (MemAdded: false)", buttonStyle, GUILayout.Height(44))) SimulateMemRemoved();

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
