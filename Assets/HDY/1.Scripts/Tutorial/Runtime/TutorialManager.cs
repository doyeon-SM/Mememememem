using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using HDY.Territory;

using KmsPlayerInventory = KMS.InventoryDuped.PlayerInventory;

namespace HDY.Tutorial
{
    /// <summary>
    /// 튜토리얼 전체 흐름(퀘스트형 스텝 진행)을 관장하는 매니저.
    ///
    /// [설계 요약]
    /// - TutorialCatalogManager가 CSV 시트에서 만든 스텝 목록(AllSteps)을 순서대로 "하나씩만" 진행한다
    ///   (동시에 여러 스텝이 열리지 않음). 시트의 행 순서가 곧 진행 순서다.
    /// - 각 스텝은 활성화 조건(TutorialTriggerType)을 만족해야 화면에 뜬다. 조건을 만족하기 전까지는
    ///   "대기 중(pending)" 상태로만 존재한다.
    /// - 대사(dialogueLines)는 대화창에서 "다음"으로 순서대로 넘기고, 마지막 대사 이후 목표
    ///   (objectives)가 있으면 그 목표가 전부 채워질 때까지 HUD 텍스트로 진행 상황을 보여준다.
    ///   목표가 없으면 대사만 끝나도 바로 스텝을 완료 처리한다.
    /// - 목표 진행/트리거 발생은 이 매니저가 스스로 감지하지 않고, 외부 바인더가
    ///   NotifyTriggerFired / NotifyObjectiveProgress를 호출해주는 방식으로 들어온다(이벤트 버스 형태).
    /// - 하이라이트는 두 경로로 채워진다:
    ///   1) 시야 감지(TutorialSightDetector)가 오브젝트/멤/웨이포인트/상자를 처음 포착하면
    ///      SetPendingHighlightTarget(...)으로 그 월드 Transform을 넘겨준다.
    ///   2) 스텝 데이터의 highlightKey가 채워져 있으면, ActivateStep 시점에 UI 하이라이트 레지스트리
    ///      (RegisterUIHighlightTarget으로 등록된 버튼/패널)에서 같은 키를 찾아 그 RectTransform을 쓴다.
    ///   두 값 다 등록된 TutorialHighlightUI로 중계해서 화면에 스포트라이트로 보여준다. 스텝이 끝나거나
    ///   다음 스텝으로 넘어가면 강조를 끈다.
    ///
    /// [영지 레벨 목표 특수 처리] objectiveKey가 정확히 "territory_level"인 목표는 일반적인 "누적 획득"
    /// 방식이 아니라 "현재 영지 레벨 값 자체"를 진행도로 쓴다(레벨이 오르내리는 값이 아니라 절대값이라
    /// 누적 더하기가 맞지 않기 때문). TerritoryData.OnLevelChanged가 울릴 때마다 진행도를 그 레벨 값으로
    /// 덮어쓰고, 스텝이 활성화되는 시점에도 즉시 현재 레벨로 한 번 채워준다(활성화 시점에 이미 목표
    /// 레벨을 넘긴 경우도 놓치지 않도록).
    ///
    /// [현재 배치에서 실제로 동작하는 트리거]
    /// - Manual: 즉시 활성화
    /// - SceneEnter: UnityEngine.SceneManagement.SceneManager 이벤트로 직접 감지(엔진 API라 크로스팀
    ///   이슈 없음)
    /// - LevelReached: HDY 소유인 TerritoryData.OnLevelChanged 이벤트를 직접 구독
    /// - ObjectSighted / MemSighted / WaypointSighted / ChestSighted: TutorialSightDetector가 호출
    /// 나머지(MemCaptured/ChestOpened/WaypointUnlocked/UIPanelOpened)는 이후 배치의 바인더가
    /// NotifyTriggerFired(...)를 호출해주는 자리만 마련해뒀다.
    ///
    /// [저장 - 현재 배치] 팀원의 저장 연결 전까지는 진행 상태를 이 컴포넌트의 SerializeField에만
    /// 들고 있는다(Inspector로 Play 모드 중 실시간 확인 가능). CaptureSnapshot()/ApplySnapshot()만
    /// 미리 만들어두었고 지금은 아무도 호출하지 않으므로, 항상 "최초 시작" 상태로 테스트된다.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        private const string TerritoryLevelObjectiveKey = "territory_level";

        public static TutorialManager Instance { get; private set; }

        /// <summary>
        /// 다른 스크립트가 들고 있는 TutorialManager 참조가 비어있을 때 쓰는 공용 폴백 탐색.
        /// TerritoryData.Resolve(existing)/GameTimeManager.Resolve(existing)와 동일한 패턴.
        /// </summary>
        public static TutorialManager Resolve(TutorialManager existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<TutorialManager>();
            if (found == null)
            {
                Debug.LogWarning("[TutorialManager] 씬에서 TutorialManager를 찾을 수 없습니다.");
            }
            return found;
        }

        [Header("튜토리얼 스텝 시트 참조 (비어있으면 자동 탐색)")]
        [Tooltip("CSV 시트에서 스텝 목록을 만드는 매니저. 행 순서가 곧 진행 순서다.")]
        [SerializeField] private TutorialCatalogManager tutorialCatalog;

        [Header("영지 레벨 참조 (비어있으면 자동 탐색)")]
        [SerializeField] private TerritoryData territoryData;

        [Header("디버그 - 진행 상태 (Play 모드 중 Inspector에서 실시간 확인용)")]
        [SerializeField] private int currentStepIndex = -1;
        [SerializeField] private string currentStepId;
        [SerializeField] private bool currentStepAwaitingTrigger;
        [SerializeField] private List<string> completedStepIds = new List<string>();
        [SerializeField] private List<ObjectiveProgressDebugEntry> currentObjectiveProgressDebug = new List<ObjectiveProgressDebugEntry>();

        /// <summary>Inspector에 목표 진행 상황을 보여주기 위한 디버그 전용 표시 구조체. 값을 직접 수정해도 반영되지 않는다.</summary>
        [Serializable]
        private class ObjectiveProgressDebugEntry
        {
            public string objectiveKey;
            public string displayLabel;
            public int currentAmount;
            public int targetAmount;
        }

        // 실제 진행 계산에 쓰는 내부 상태(목표 키 -> 현재 수량). Inspector 디버그 리스트는 이 값을 그대로 반영만 한다.
        private readonly Dictionary<string, int> objectiveProgress = new Dictionary<string, int>();

        // 등록된 프레젠테이션(대화창/HUD 목표 텍스트/하이라이트). GameTimeTextBinder와 동일하게, UI 쪽이
        // 스스로 OnEnable/OnDisable에서 등록/해제하므로 씬이 바뀌어도 새 씬의 UI가 자동으로 재연결된다.
        private TutorialDialogueUI dialogueUI;
        private TutorialHighlightUI highlightUI;
        private readonly List<TMP_Text> objectiveTexts = new List<TMP_Text>();

        // TutorialUIHighlightTarget들이 등록한 "키 -> UI RectTransform" 목록.
        private readonly Dictionary<string, RectTransform> uiHighlightTargets = new Dictionary<string, RectTransform>();

        // 이번 스텝에서 강조해야 할 대상 - 시야 감지가 넘겨준 월드 Transform, 또는 highlightKey로 찾은 UI.
        private Transform pendingHighlightTarget;
        private RectTransform pendingHighlightUITarget;

        private int currentDialogueLineIndex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[TutorialManager] 씬에 TutorialManager가 이미 있어 중복 오브젝트를 파괴합니다.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureReferences();
            SubscribeTerritoryLevel();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeTerritoryLevel();
        }

        private void Start()
        {
            EnsureReferences();
            TryActivateNextPendingStep();
        }

        /// <summary>
        /// [EnsureReferences 패턴] Awake/OnEnable뿐 아니라 여러 공개 진입점에서 다시 호출해, 다른
        /// 매니저의 초기화 순서와 무관하게 참조를 안전하게 채운다.
        /// </summary>
        private void EnsureReferences()
        {
            territoryData = TerritoryData.Resolve(territoryData);
            tutorialCatalog = TutorialCatalogManager.Resolve(tutorialCatalog);
        }

        private void SubscribeTerritoryLevel()
        {
            EnsureReferences();
            if (territoryData == null) return;

            territoryData.OnLevelChanged -= HandleTerritoryLevelChanged; // 중복 구독 방지
            territoryData.OnLevelChanged += HandleTerritoryLevelChanged;
        }

        private void UnsubscribeTerritoryLevel()
        {
            if (territoryData == null) return;
            territoryData.OnLevelChanged -= HandleTerritoryLevelChanged;
        }

        private void HandleTerritoryLevelChanged(int newLevel)
        {
            NotifyTriggerFired(TutorialTriggerType.LevelReached, newLevel.ToString());
            SetTerritoryLevelObjectiveProgress(newLevel);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            NotifyTriggerFired(TutorialTriggerType.SceneEnter, scene.name);
        }

        // =====================================================================
        // 프레젠테이션(UI) 등록 - GameTimeTextBinder와 동일한 "UI가 스스로 등록" 패턴
        // =====================================================================

        /// <summary>TutorialDialogueUI가 OnEnable에서 자기 자신을 등록할 때 호출한다.</summary>
        public void RegisterDialogueUI(TutorialDialogueUI ui)
        {
            dialogueUI = ui;
            RefreshDialoguePresentation();
        }

        public void UnregisterDialogueUI(TutorialDialogueUI ui)
        {
            if (dialogueUI == ui) dialogueUI = null;
        }

        /// <summary>TutorialObjectiveHUD가 OnEnable에서 자기 자신(TMP_Text)을 등록할 때 호출한다.</summary>
        public void RegisterObjectiveText(TMP_Text text)
        {
            if (text == null || objectiveTexts.Contains(text)) return;
            objectiveTexts.Add(text);
            RefreshObjectivePresentation();
        }

        public void UnregisterObjectiveText(TMP_Text text)
        {
            objectiveTexts.Remove(text);
        }

        /// <summary>TutorialHighlightUI가 OnEnable에서 자기 자신을 등록할 때 호출한다.</summary>
        public void RegisterHighlightUI(TutorialHighlightUI ui)
        {
            highlightUI = ui;
            RefreshHighlightPresentation();
        }

        public void UnregisterHighlightUI(TutorialHighlightUI ui)
        {
            if (highlightUI == ui) highlightUI = null;
        }

        /// <summary>TutorialUIHighlightTarget이 OnEnable에서 자기 자신(키+RectTransform)을 등록할 때 호출한다.</summary>
        public void RegisterUIHighlightTarget(string key, RectTransform rect)
        {
            if (string.IsNullOrEmpty(key) || rect == null) return;
            uiHighlightTargets[key] = rect;
        }

        public void UnregisterUIHighlightTarget(string key, RectTransform rect)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (uiHighlightTargets.TryGetValue(key, out var existing) && existing == rect)
            {
                uiHighlightTargets.Remove(key);
            }
        }

        // =====================================================================
        // 외부 바인더 공개 API - 시야 감지(TutorialSightDetector), 목표 진행(생산 완료 감시 등)이
        // 이 메서드들을 호출한다.
        // =====================================================================

        /// <summary>
        /// 특정 종류의 트리거가 발생했음을 알린다. 현재 "대기 중"인 스텝의 조건과 일치할 때만 반응한다.
        /// </summary>
        public void NotifyTriggerFired(TutorialTriggerType type, string param)
        {
            var pending = GetPendingStep();
            if (pending == null) return;
            if (pending.triggerType != type) return;

            // triggerParam이 비어있으면 종류만 맞으면 통과, 채워져 있으면 값까지 일치해야 함.
            if (!string.IsNullOrEmpty(pending.triggerParam) &&
                !string.Equals(pending.triggerParam, param, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ActivateStep(pending);
        }

        /// <summary>
        /// 목표 진행(예: 아이템 획득, 생산 완료 등)을 알린다. 현재 활성 스텝의 objectives 중
        /// objectiveKey가 일치하는 항목의 진행도를 amount만큼 더한다. 모든 목표가 채워지면 스텝을
        /// 자동으로 완료 처리한다.
        /// </summary>
        public void NotifyObjectiveProgress(string objectiveKey, int amount = 1)
        {
            var current = GetCurrentStep();
            if (current == null || currentStepAwaitingTrigger) return;
            if (current.objectives == null || current.objectives.Count == 0) return;
            if (!current.objectives.Any(o => o.objectiveKey == objectiveKey)) return;

            objectiveProgress.TryGetValue(objectiveKey, out int existing);
            objectiveProgress[objectiveKey] = existing + Mathf.Max(1, amount);

            RefreshObjectivePresentation();

            if (IsCurrentStepObjectivesComplete())
            {
                CompleteCurrentStep();
            }
        }

        /// <summary>
        /// 시야 감지 바인더가 "이번에 발동시킬 트리거와 관련된 월드 대상"을 미리 알려줄 때 호출한다.
        /// NotifyTriggerFired보다 먼저(또는 같이) 호출되면, 그 직후 스텝이 활성화될 때 이 대상을
        /// 하이라이트로 강조한다.
        /// </summary>
        public void SetPendingHighlightTarget(Transform target)
        {
            pendingHighlightTarget = target;
        }

        /// <summary>
        /// objectiveKey가 "territory_level"인 목표의 진행도를 현재 레벨 값으로 덮어쓴다(누적 더하기가
        /// 아니라 절대값 대입). 활성 스텝에 이 목표가 없으면 아무 일도 하지 않는다.
        /// </summary>
        private void SetTerritoryLevelObjectiveProgress(int level)
        {
            var current = GetCurrentStep();
            if (current == null || currentStepAwaitingTrigger || current.objectives == null) return;
            if (!current.objectives.Any(o => o.objectiveKey == TerritoryLevelObjectiveKey)) return;

            objectiveProgress[TerritoryLevelObjectiveKey] = level;
            RefreshObjectivePresentation();

            if (IsCurrentStepObjectivesComplete())
            {
                CompleteCurrentStep();
            }
        }

        // =====================================================================
        // 대화창 진행 (TutorialDialogueUI의 "다음" 버튼이 호출)
        // =====================================================================

        public void AdvanceDialogue()
        {
            var current = GetCurrentStep();
            if (current == null || currentStepAwaitingTrigger || current.dialogueLines == null) return;

            currentDialogueLineIndex++;

            if (currentDialogueLineIndex < current.dialogueLines.Count)
            {
                RefreshDialoguePresentation();
                return;
            }

            // 대사 끝 - 목표가 있으면 목표 완료를 기다리고(대화창은 닫음), 없으면 바로 스텝 완료.
            dialogueUI?.Hide();

            if (current.objectives == null || current.objectives.Count == 0)
            {
                CompleteCurrentStep();
            }
            else
            {
                RefreshObjectivePresentation();
            }
        }

        // =====================================================================
        // 내부 진행 로직
        // =====================================================================

        private TutorialStepData GetCurrentStep()
        {
            if (tutorialCatalog == null || tutorialCatalog.AllSteps == null) return null;
            if (currentStepIndex < 0 || currentStepIndex >= tutorialCatalog.AllSteps.Count) return null;
            return tutorialCatalog.AllSteps[currentStepIndex];
        }

        /// <summary>아직 activate 되지 않고 "대기 중"인 현재 스텝을 조회한다. 이미 활성화됐으면 null.</summary>
        private TutorialStepData GetPendingStep()
        {
            return currentStepAwaitingTrigger ? GetCurrentStep() : null;
        }

        private void TryActivateNextPendingStep()
        {
            EnsureReferences();
            if (tutorialCatalog == null || tutorialCatalog.AllSteps == null || tutorialCatalog.AllSteps.Count == 0) return;

            int nextIndex = currentStepIndex + 1;
            if (nextIndex >= tutorialCatalog.AllSteps.Count)
            {
                Debug.Log("<color=lime>[TutorialManager]</color> 모든 튜토리얼 스텝을 완료했습니다.");
                return;
            }

            currentStepIndex = nextIndex;
            var next = tutorialCatalog.AllSteps[currentStepIndex];
            currentStepId = next.stepId;
            currentStepAwaitingTrigger = true;
            objectiveProgress.Clear();
            RefreshObjectiveProgressDebugList(next);

            // 이전 스텝에서 쓰던 하이라이트 대상은 여기서 초기화 - 새 스텝이 시야 감지 트리거라면
            // TutorialSightDetector가 SetPendingHighlightTarget으로, highlightKey가 있다면
            // ActivateStep에서 다시 채워줄 것이다.
            pendingHighlightTarget = null;
            pendingHighlightUITarget = null;
            RefreshHighlightPresentation();

            if (IsTriggerAlreadySatisfied(next))
            {
                ActivateStep(next);
            }
            else
            {
                Debug.Log($"<color=yellow>[TutorialManager]</color> 스텝 대기 중: {next.stepId} (조건: {next.triggerType}/{next.triggerParam})");
            }
        }

        /// <summary>Manual이거나, 이미 만족된 상태(현재 씬/현재 레벨)라면 이벤트를 기다릴 필요 없이 즉시 활성화 가능하다고 판단한다.</summary>
        private bool IsTriggerAlreadySatisfied(TutorialStepData step)
        {
            switch (step.triggerType)
            {
                case TutorialTriggerType.Manual:
                    return true;

                case TutorialTriggerType.SceneEnter:
                    return string.IsNullOrEmpty(step.triggerParam) ||
                           string.Equals(SceneManager.GetActiveScene().name, step.triggerParam, StringComparison.OrdinalIgnoreCase);

                case TutorialTriggerType.LevelReached:
                    EnsureReferences();
                    return territoryData != null &&
                           int.TryParse(step.triggerParam, out int requiredLevel) &&
                           territoryData.Level >= requiredLevel;

                default:
                    // ObjectSighted / MemSighted / WaypointSighted / ChestSighted / MemCaptured /
                    // ChestOpened / WaypointUnlocked / UIPanelOpened - 해당 바인더가
                    // NotifyTriggerFired로 알려줄 때까지 대기한다.
                    return false;
            }
        }

        private void ActivateStep(TutorialStepData step)
        {
            currentStepAwaitingTrigger = false;
            currentDialogueLineIndex = 0;

            // highlightKey가 지정된 스텝이면 UI 하이라이트 레지스트리에서 찾아 강조 대상으로 쓴다.
            // 시야 감지로 이미 pendingHighlightTarget(월드 오브젝트)가 채워져 있는 스텝은 highlightKey를
            // 따로 안 쓰는 게 일반적이라 서로 충돌하지 않는다.
            if (!string.IsNullOrEmpty(step.highlightKey) && uiHighlightTargets.TryGetValue(step.highlightKey, out var uiTarget))
            {
                pendingHighlightUITarget = uiTarget;
            }

            // "territory_level" 목표가 있는 스텝은 활성화되는 순간 현재 레벨로 즉시 시드해준다 -
            // 스텝이 뜨기 전에 이미 목표 레벨을 넘긴 경우도 놓치지 않기 위함.
            if (step.objectives != null && step.objectives.Any(o => o.objectiveKey == TerritoryLevelObjectiveKey))
            {
                EnsureReferences();
                if (territoryData != null)
                {
                    objectiveProgress[TerritoryLevelObjectiveKey] = territoryData.Level;
                }
            }

            Debug.Log($"<color=cyan>[TutorialManager]</color> 스텝 활성화: {step.stepId}");

            RefreshDialoguePresentation();
            RefreshObjectivePresentation();
            RefreshHighlightPresentation();

            if (IsCurrentStepObjectivesComplete())
            {
                CompleteCurrentStep();
            }
        }

        private void CompleteCurrentStep()
        {
            var step = GetCurrentStep();
            if (step == null) return;

            GrantRewards(step);

            if (!completedStepIds.Contains(step.stepId))
            {
                completedStepIds.Add(step.stepId);
            }

            objectiveProgress.Clear();
            currentObjectiveProgressDebug.Clear();

            Debug.Log($"<color=lime>[TutorialManager]</color> 스텝 완료: {step.stepId}");

            TryActivateNextPendingStep();
        }

        private bool IsCurrentStepObjectivesComplete()
        {
            var current = GetCurrentStep();
            if (current == null || current.objectives == null || current.objectives.Count == 0) return false;

            foreach (var objective in current.objectives)
            {
                objectiveProgress.TryGetValue(objective.objectiveKey, out int amount);
                if (amount < objective.targetAmount) return false;
            }
            return true;
        }

        /// <summary>
        /// 완료 보상을 지급한다. WorldObject/Chest/ProductionCraftRuntime이 이미 쓰고 있는 것과 동일한
        /// PlayerInventory.AddItem(itemId, amount) 공용 API를 그대로 사용한다(크로스팀 파일 수정 없음).
        /// </summary>
        private void GrantRewards(TutorialStepData step)
        {
            if (step.rewards == null || step.rewards.Count == 0) return;

            var inventory = FindFirstObjectByType<KmsPlayerInventory>();
            if (inventory == null)
            {
                Debug.LogWarning("[TutorialManager] PlayerInventory를 찾지 못해 보상을 지급하지 못했습니다.");
                return;
            }

            foreach (var reward in step.rewards)
            {
                if (string.IsNullOrEmpty(reward.itemId) || reward.amount <= 0) continue;
                inventory.AddItem(reward.itemId, reward.amount);
            }
        }

        // =====================================================================
        // 프레젠테이션 갱신
        // =====================================================================

        private void RefreshDialoguePresentation()
        {
            if (dialogueUI == null) return;

            var current = GetCurrentStep();
            if (current == null || currentStepAwaitingTrigger) return;
            if (current.dialogueLines == null || currentDialogueLineIndex >= current.dialogueLines.Count) return;

            dialogueUI.ShowLine(current.dialogueLines[currentDialogueLineIndex].text);
        }

        private void RefreshObjectivePresentation()
        {
            RefreshObjectiveProgressDebugList(GetCurrentStep());

            if (objectiveTexts.Count == 0) return;

            string text = BuildObjectiveDisplayText();
            foreach (var t in objectiveTexts)
            {
                if (t != null) t.text = text;
            }
        }

        /// <summary>
        /// 등록된 하이라이트 UI에 현재 상태를 그대로 반영한다. UI 하이라이트(highlightKey로 찾은 버튼)가
        /// 있으면 그걸 우선하고, 없으면 월드 하이라이트(시야 감지 대상)를 쓴다.
        /// </summary>
        private void RefreshHighlightPresentation()
        {
            if (highlightUI == null) return;

            if (currentStepAwaitingTrigger)
            {
                highlightUI.Hide();
                return;
            }

            if (pendingHighlightUITarget != null)
            {
                highlightUI.ShowUI(pendingHighlightUITarget);
            }
            else if (pendingHighlightTarget != null)
            {
                highlightUI.Show(pendingHighlightTarget);
            }
            else
            {
                highlightUI.Hide();
            }
        }

        private string BuildObjectiveDisplayText()
        {
            var current = GetCurrentStep();
            if (current == null || currentStepAwaitingTrigger || current.objectives == null || current.objectives.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var objective in current.objectives)
            {
                objectiveProgress.TryGetValue(objective.objectiveKey, out int amount);
                parts.Add($"{objective.displayLabel} {amount}/{objective.targetAmount}");
            }
            return string.Join("   ", parts);
        }

        private void RefreshObjectiveProgressDebugList(TutorialStepData step)
        {
            currentObjectiveProgressDebug.Clear();
            if (step == null || step.objectives == null) return;

            foreach (var objective in step.objectives)
            {
                objectiveProgress.TryGetValue(objective.objectiveKey, out int amount);
                currentObjectiveProgressDebug.Add(new ObjectiveProgressDebugEntry
                {
                    objectiveKey = objective.objectiveKey,
                    displayLabel = objective.displayLabel,
                    currentAmount = amount,
                    targetAmount = objective.targetAmount
                });
            }
        }

        // =====================================================================
        // 저장 연동 자리 (팀원이 JSON을 붙일 때 이 두 메서드만 호출하면 됨 - 지금은 아무도 호출하지 않음)
        // =====================================================================

        public TutorialProgressSnapshot CaptureSnapshot()
        {
            return new TutorialProgressSnapshot
            {
                currentStepIndex = currentStepIndex,
                currentStepAwaitingTrigger = currentStepAwaitingTrigger,
                completedStepIds = new List<string>(completedStepIds),
                objectiveProgressKeys = objectiveProgress.Keys.ToList(),
                objectiveProgressValues = objectiveProgress.Values.ToList(),
            };
        }

        public void ApplySnapshot(TutorialProgressSnapshot snapshot)
        {
            if (snapshot == null) return;

            currentStepIndex = snapshot.currentStepIndex;
            currentStepAwaitingTrigger = snapshot.currentStepAwaitingTrigger;
            completedStepIds = new List<string>(snapshot.completedStepIds ?? new List<string>());

            objectiveProgress.Clear();
            if (snapshot.objectiveProgressKeys != null && snapshot.objectiveProgressValues != null)
            {
                int count = Mathf.Min(snapshot.objectiveProgressKeys.Count, snapshot.objectiveProgressValues.Count);
                for (int i = 0; i < count; i++)
                {
                    objectiveProgress[snapshot.objectiveProgressKeys[i]] = snapshot.objectiveProgressValues[i];
                }
            }

            pendingHighlightTarget = null;
            pendingHighlightUITarget = null;

            RefreshObjectiveProgressDebugList(GetCurrentStep());
            RefreshDialoguePresentation();
            RefreshObjectivePresentation();
            RefreshHighlightPresentation();
        }

        // =====================================================================
        // 디버그 전용 - Play 모드 중 Inspector 우클릭(⋮ 메뉴)으로 실행 가능
        // =====================================================================

        [ContextMenu("디버그 - 현재 스텝 목표 강제 채우기")]
        private void DebugForceCompleteObjectives()
        {
            var current = GetCurrentStep();
            if (current == null || currentStepAwaitingTrigger || current.objectives == null) return;

            foreach (var objective in current.objectives)
            {
                objectiveProgress[objective.objectiveKey] = objective.targetAmount;
            }
            RefreshObjectivePresentation();

            if (IsCurrentStepObjectivesComplete())
            {
                CompleteCurrentStep();
            }
        }

        [ContextMenu("디버그 - 현재 스텝 강제로 다음 단계 진행")]
        private void DebugForceAdvanceStep()
        {
            if (currentStepAwaitingTrigger)
            {
                var pending = GetPendingStep();
                if (pending != null) ActivateStep(pending);
                return;
            }

            CompleteCurrentStep();
        }
    }
}
