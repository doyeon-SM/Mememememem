using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using HDY.Forge;
using HDY.Territory;
using KMS.Audio;
using KMS.InventoryDuped;

using KmsPlayerInventory = KMS.InventoryDuped.PlayerInventory;
using KmsPlayerInput = KMS.PlayerInput;

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
    ///
    /// [튜토리얼 패널 프리팹 자동 로드 - HDY 요청] 튜토리얼 대화창/하이라이트 패널(P_TutorialRoot)이
    /// 프리팹으로 바뀌면서, 각 씬에 수동으로 배치해두지 않아도 이 매니저가 씬 로드 시점마다 자동으로
    /// Instantiate한다(EnsureTutorialPanelSpawned 참고). 프리팹 내부의 TutorialDialogueUI/
    /// TutorialHighlightUI는 이미 자기 자신의 OnEnable에서 이 매니저에 스스로 등록하는 패턴이라,
    /// 이 매니저는 "심는 것"만 담당하면 되고 별도의 UI 연결 코드는 필요 없다.
    ///
    /// [사운드 - HDY 요청] 스텝(=퀘스트) 완료 시 KMSAudioService.Play2D(GameSfxId.QuestComplete)를 재생한다.
    ///
    /// [HDY 요청 - 대사 중 입력 차단] 대사가 화면에 떠 있는 동안 F(상호작용)와 클릭을 제외한 게임플레이
    /// 입력(이동/좌클릭 채집·공격/점프/핫바 등)을 막는다(SetDialogueInputBlock 참고). 상자(Chest)와
    /// 웨이포인트석(WayPointStone)은 전부 F(PlayerInteraction)로 여는 구조라, 이 차단 하나로 "대사 중에
    /// 미리 상자를 열거나 웨이포인트를 등록해서 튜토리얼 진행이 꼬이는" 문제까지 함께 막힌다.
    ///
    /// [HDY 요청 - 영지에서 F키로 보상을 못 받는 버그 수정] HandleSceneLoaded는 씬 전환 시 playerInput을
    /// 찾아 InteractPressed를 딱 한 번만 구독한다. 영지 씬은 플레이어(PlayerInput)가 씬 로드 시점에
    /// 아직 스폰되지 않은 경우가 있어(탐험 씬과 스폰 타이밍이 다름), 그 순간 못 찾으면 구독 자체가
    /// 비어있는 채로 남고 이후 아무도 다시 구독을 시도해주지 않아 클릭은 되는데 F키만 먹통이 되는
    /// 문제가 있었다. Update()에서 playerInput이 비어있을 때만 가볍게 매 프레임 재확인해서, 플레이어가
    /// 늦게 나타나도 자동으로 다시 연결되도록 방어 코드를 추가했다.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        private const string TerritoryLevelObjectiveKey = "territory_level";

        // [HDY 요청 - 골드 보상] Rewards CSV에서 itemId가 정확히 이 값이면 인벤토리가 아니라
        // TerritoryData.AddGold(amount)로 지급한다.
        private const string GoldRewardItemId = "gold";

        // [HDY 요청 - 고정 연마 보상] PlayerDefaultItemTest가 게임 시작 시 초기 도구에 적용하는 것과
        // 완전히 동일한 고정 값(Rare 등급 / 데미지 +1 한 줄). Rewards CSV에서 "refined" 접미사가 붙은
        // 항목에 지급 직후 그대로 재사용한다.
        private const CommonClass FixedRefinementGrade = CommonClass.Rare;
        private const string FixedRefinementOptionType = "DamageIncrease";
        private const string FixedRefinementDisplayName = "데미지";
        private const float FixedRefinementValue = 1f;

        /// <summary>할당된 목표가 없을 때(스텝 시작 전/모든 스텝 완료 후 등) HUD 텍스트에 대신 표시할 대기 문구.</summary>
        private const string NoObjectiveText = "편지 기다리기";

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

        // [HDY 요청 - F키(공통 상호작용키) 대사 넘기기] static Instance/Resolve 헬퍼가 없는 KMS.PlayerInput을
        // 직접 찾아 보관한다. 씬 전환 시 HandleSceneLoaded가 null로 비우고 EnsureReferences가 다시 채운다.
        private KmsPlayerInput playerInput;

        [Header("월드 하이라이트 / 시야감지 공용 카메라 (비어있으면 Camera.main)")]
        [Tooltip("TutorialSightDetector와 TutorialHighlightUI가 각자 Camera.main에 따로 의존하다 서로 다른\n" +
                 "카메라를 참조하게 되는 문제를 막기 위해, 여기 한 곳에 지정해두면 둘 다 이 카메라를 우선 쓴다.\n" +
                 "비워두면 지금까지처럼 각자 Camera.main으로 폴백한다.")]
        [SerializeField] private Camera worldCamera;

        [Header("튜토리얼 패널 프리팹 (씬이 로드될 때마다 자동으로 Instantiate됨)")]
        [Tooltip("P_TutorialRoot 프리팹. 비어있으면 자동 스폰을 건너뛴다(기존처럼 씬에 수동 배치해도 됨).")]
        [SerializeField] private GameObject tutorialPanelPrefab;

        [Header("디버그 - 진행 상태 (Play 모드 중 Inspector에서 실시간 확인용)")]
        // [HDY 요청 - Main_World_3 진입 시 진행 리셋 버그 수정] 예전엔 SerializeField였는데, 이 오브젝트가
        // TutorialManager.prefab의 PrefabInstance + DontDestroyOnLoad 조합이라, 씬 전환 시점에 에디터가
        // "프리팹 원본과 달라진 값"을 재확인하는 과정에서 순수 런타임 상태값인 이 필드를 프리팹 원본 값
        // (-1)으로 되돌려버리는 것으로 추정되는 현상이 있었다(Title에서 ctrl_guide가 대기 상태로 잘
        // 잡혔는데, Main_World_3 진입 시점엔 -1로 리셋되어 튜토리얼이 시작되지 않는 버그). Inspector에서
        // 편집할 값이 아니라 순수 실행 중 상태 추적용이라 SerializeField일 필요가 없어서 제거한다.
        // [HDY 요청 - 진단용] currentStepIndex가 어디서 바뀌는지 스택 트레이스와 함께 100% 확정하려고
        // 임시로 프로퍼티로 바꿔서 변경될 때마다 무조건 로그를 찍는다. 원인 확정되면 다시 평범한 필드로
        // 되돌릴 예정.
        // [HDY 요청] 진행 중인 스텝 인덱스. Inspector에서 실행 중 값을 확인할 수 있도록 노출해둔다.
        [SerializeField] private int currentStepIndex = -1;
        private bool currentStepAwaitingTrigger;
        private string currentStepId;
        private List<string> completedStepIds = new List<string>(); // [HDY 요청] 순수 런타임 상태값 - 위와 동일한 이유로 SerializeField 제거
        private List<ObjectiveProgressDebugEntry> currentObjectiveProgressDebug = new List<ObjectiveProgressDebugEntry>(); // [HDY 요청] 순수 런타임 상태값(디버그 표시용) - 위와 동일한 이유로 SerializeField 제거

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

        // 등록된 프레젠테이션(대화창/HUD 목표 텍스트/하이라이트/보상 미리보기). GameTimeTextBinder와 동일하게, UI 쪽이
        // 스스로 OnEnable/OnDisable에서 등록/해제하므로 씬이 바뀌어도 새 씬의 UI가 자동으로 재연결된다.
        private TutorialDialogueUI dialogueUI;
        private TutorialHighlightUI highlightUI;
        private readonly List<TMP_Text> objectiveTexts = new List<TMP_Text>();

        // [HDY 요청 - 보상 미리보기 UI] 등록돼 있지 않으면(프리팹 미배치) 보상이 있는 스텝도 기존처럼
        // 미리보기 없이 곧바로 완료된다 - CompleteCurrentStep 참고.
        private TutorialRewardPreviewUI rewardPreviewUI;

        // TutorialUIHighlightTarget들이 등록한 "키 -> UI RectTransform" 목록.
        private readonly Dictionary<string, RectTransform> uiHighlightTargets = new Dictionary<string, RectTransform>();

        // 이번 스텝에서 강조해야 할 대상 - 시야 감지가 넘겨준 월드 Transform, 또는 highlightKey로 찾은 UI.
        private Transform pendingHighlightTarget;
        private RectTransform pendingHighlightUITarget;

        private int currentDialogueLineIndex;

        // [HDY 요청 - 대사 중 입력 차단] 대사가 화면에 떠 있는 동안(ShowLine 호출 시점 ~ Hide 호출 시점) true.
        // Update()에서 F키 우회 폴링 여부를 판단하는 데도 같이 쓴다.
        private bool isDialogueInputBlockActive;

        /// <summary>이번 씬에 자동으로 심어둔 튜토리얼 패널 인스턴스(중복 스폰 방지용 추적).</summary>
        private GameObject spawnedTutorialPanel;

        /// <summary>[HDY 요청 - 영지 방문 시 진행 막힘 수정] EnsureTutorialPanelSpawned 재시도 코루틴 추적(중복 시작 방지).</summary>
        private Coroutine spawnPanelRetryCoroutine;

        /// <summary>[HDY 요청 - 이상한 Canvas에 붙는 문제 수정] UIManager가 없는 씬에서 만든 전용 폴백 Canvas(씬마다 새로 만듦).</summary>
        private Canvas fallbackCanvas;

        /// <summary>
        /// [KKS] 튜토리얼 저장 진행을 위한 이벤트 발행.
        /// </summary>
        public event Action OnTutorialProgressChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[TutorialManager] 씬에 TutorialManager가 이미 있어 중복 오브젝트를 파괴합니다. (이 인스턴스={GetInstanceID()}, 기존={Instance.GetInstanceID()})", this);
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
            SubscribeInteractInput();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeTerritoryLevel();
            UnsubscribeInteractInput();

            // [HDY 요청 - 대사 중 입력 차단 방어] 이 매니저 자체가 비활성화되는 극단적인 경우에도 플레이어가
            // 영원히 입력이 막힌 채로 남지 않도록 여기서도 확실히 풀어준다.
            if (isDialogueInputBlockActive)
            {
                isDialogueInputBlockActive = false;
                playerInput?.SetGameplayInputBlocked(false);
            }

            // [HDY 요청 - 영지 방문 시 진행 막힘 수정] OnDisable 시점에 Unity가 이 오브젝트의 코루틴을
            // 이미 전부 멈추므로 StopCoroutine은 불필요하지만, 참조만은 비워둬야 다음에 다시 활성화됐을 때
            // "이미 재시도 중"이라는 낡은 가드에 막히지 않는다.
            spawnPanelRetryCoroutine = null;
        }

        private void Start()
        {
            EnsureReferences();
            EnsureTutorialPanelSpawned(); // 최초 진입 씬은 sceneLoaded 이벤트를 놓칠 수 있어 한 번 더 보장
            TryActivateNextPendingStep();
        }

        /// <summary>
        /// [HDY 요청 - 대사 중 F키 우회] 대사가 떠 있는 동안은 SetDialogueInputBlock(true)가
        /// playerInput의 Gameplay 액션맵을 통째로 막아서 Interact(F) 액션도 함께 막히므로,
        /// PlayerInput.InteractPressed 이벤트에 더 이상 의존할 수 없다. 그래서 여기서 원시 키보드
        /// 입력(Keyboard.current)을 직접 읽어 F만 예외적으로 통과시킨다 - 차단 중이 아닐 때는 기존처럼
        /// SubscribeInteractInput으로 등록한 이벤트 경로가 정상 동작하므로 여기서는 아무 것도 하지 않는다.
        /// </summary>
        private void Update()
        {
            // [HDY 요청 - 영지에서 F키 안 되는 버그 수정] playerInput을 아직 못 찾았으면(씬 로드 시점에
            // 플레이어가 늦게 스폰된 경우 등) 매 프레임 가볍게 재시도한다 - 찾아지는 즉시 InteractPressed를
            // 다시 구독해서, 클릭만 되고 F키는 영영 안 먹는 상태가 되지 않도록 한다.
            if (playerInput == null)
            {
                EnsureReferences();
                if (playerInput != null)
                {
                    SubscribeInteractInput();
                }
            }

            if (!isDialogueInputBlockActive) return;
            if (Keyboard.current == null) return;

            if (Keyboard.current[Key.F].wasPressedThisFrame)
            {
                HandleInteractPressed();
            }
        }

        /// <summary>
        /// [EnsureReferences 패턴] Awake/OnEnable뿐 아니라 여러 공개 진입점에서 다시 호출해, 다른
        /// 매니저의 초기화 순서와 무관하게 참조를 안전하게 채운다.
        /// </summary>
        private void EnsureReferences()
        {
            territoryData = TerritoryData.Resolve(territoryData);
            tutorialCatalog = TutorialCatalogManager.Resolve(tutorialCatalog);

            // [HDY 요청 - F키(공통 상호작용키) 대사 넘기기] PlayerInput에는 static Instance/Resolve
            // 헬퍼가 없어서 직접 찾는다. 씬 전환으로 플레이어 인스턴스가 바뀌면 HandleSceneLoaded가
            // playerInput을 null로 비워두므로, 여기서 다시 채워진다.
            if (playerInput == null)
            {
                playerInput = FindFirstObjectByType<KmsPlayerInput>();
            }
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

        // =====================================================================
        // F키(공통 상호작용키)로 대사 넘기기
        // =====================================================================
        //
        // [HDY 요청] 영지에서는 대화창의 "다음" 버튼 클릭으로 자연스럽게 넘어가지만, 탐험 중에는
        // 마우스 커서가 기본적으로 잠겨있어(Alt로 풀어야 클릭 가능) 클릭이 불편하다. 그래서 탐험/영지
        // 공통으로 쓰는 상호작용키(F, KMS.PlayerInput.InteractPressed - Chest/WayPointStone이 쓰는 것과
        // 동일한 경로)로도 AdvanceDialogue()를 호출할 수 있게 한다. 클릭은 그대로 유지되고 F가 추가되는
        // 것뿐이다. AdvanceDialogue()는 대사가 없거나 트리거 대기 중이면 스스로 아무것도 하지 않으므로,
        // 별도 조건 체크 없이 그대로 연결해도 안전하다.

        private void SubscribeInteractInput()
        {
            EnsureReferences();
            if (playerInput == null) return;

            playerInput.InteractPressed -= HandleInteractPressed; // 중복 구독 방지
            playerInput.InteractPressed += HandleInteractPressed;
        }

        private void UnsubscribeInteractInput()
        {
            if (playerInput == null) return;
            playerInput.InteractPressed -= HandleInteractPressed;
        }

        /// <summary>
        /// [HDY 요청 - 탐험 중 F키로 보상 확인] 탐험 중에는 마우스 커서가 잠겨있어 보상 미리보기
        /// 팝업의 확인 버튼을 클릭할 수 없다. 그래서 보상 팝업이 확인을 기다리는 중이면(rewardPreviewUI.
        /// IsAwaitingConfirm) F 입력을 그 팝업의 확인으로 대신 위임하고, 대사 넘기기(AdvanceDialogue)는
        /// 시도하지 않는다 - 그렇지 않으면 대사가 이미 끝난 스텝에서 F를 누를 때마다
        /// CompleteCurrentStep이 재진입해 팝업이 계속 다시 그려지는 문제가 있었다. 팝업이 없는
        /// 평소(대부분의 스텝)에는 지금까지처럼 곧바로 AdvanceDialogue()로 넘어간다.
        /// </summary>
        private void HandleInteractPressed()
        {
            if (rewardPreviewUI != null && rewardPreviewUI.IsAwaitingConfirm)
            {
                rewardPreviewUI.Confirm();
                return;
            }

            AdvanceDialogue();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            spawnedTutorialPanel = null; // 이전 씬 인스턴스는 씬 전환과 함께 이미 파괴됨 - 참조만 정리
            fallbackCanvas = null; // [HDY 요청 - 이상한 Canvas에 붙는 문제 수정] 이전 씬의 폴백 Canvas도 마찬가지

            // [HDY 요청 - 대사 중 입력 차단 방어] 이전 씬의 PlayerInput 인스턴스는 곧 사라지므로 굳이
            // SetGameplayInputBlocked(false)를 호출할 필요는 없지만(씬 전환으로 어차피 파괴됨), 플래그는
            // 여기서 리셋해둬야 새 씬에서 RefreshDialoguePresentation이 다시 정확한 상태로 켜고 끌 수 있다.
            isDialogueInputBlockActive = false;

            EnsureTutorialPanelSpawned();
            NotifyTriggerFired(TutorialTriggerType.SceneEnter, scene.name);

            // [HDY 요청 - F키 대사 넘기기] 이전 씬의 PlayerInput 인스턴스도 씬 전환과 함께 파괴됐을 수
            // 있으므로 참조를 비우고, EnsureReferences로 새로 찾은 뒤 다시 구독한다.
            UnsubscribeInteractInput();
            playerInput = null;
            EnsureReferences();
            SubscribeInteractInput();
        }

        /// <summary>
        /// [HDY 요청 - 튜토리얼 패널 프리팹 자동 로드] tutorialPanelPrefab을 UIManager.UIRoot 밑에
        /// Instantiate한다. 이미 이번 씬에 심어져 있으면(spawnedTutorialPanel != null) 아무 것도 하지
        /// 않는다 - Start()와 HandleSceneLoaded 양쪽에서 호출돼도 중복 생성되지 않도록 하기 위함이다.
        /// 프리팹 내부의 TutorialDialogueUI/TutorialHighlightUI는 자기 자신의 OnEnable에서 이 매니저에
        /// 자동으로 등록되므로, 여기서는 심는 것 외에 별도 연결 작업이 필요 없다.
        /// [배치 순서 - 항상 맨 위에 그려지도록] Canvas 자식 목록의 맨 마지막(SetAsLastSibling)으로
        /// 옮겨서, 같은 Canvas 아래의 다른 UI보다 항상 위에 그려지도록 한다.
        /// </summary>
        private void EnsureTutorialPanelSpawned()
        {
            if (tutorialPanelPrefab == null) return;
            if (spawnedTutorialPanel != null) return;

            Transform parent = ResolveTutorialPanelParent();
            if (parent == null)
            {
                // [HDY 요청 - 영지 방문 시 진행 막힘 수정] 씬 로드 시점에 UIManager.Instance가 아직 준비되지
                // 않았을 수 있다(다른 오브젝트 초기화 순서에 따라 미묘하게 달라짐). 예전에는 여기서 경고만
                // 남기고 끝나서, 스텝은 내부적으로 활성화됐는데 화면엔 대화창이 아예 안 떠서 플레이어에게는
                // "막힌 것"처럼 보이는 문제가 있었다. 이제는 잠깐 대기했다가 자동으로 재시도한다.
                Debug.LogWarning("[TutorialManager] 튜토리얼 패널을 배치할 UI 루트(UIManager.UIRoot)를 찾지 못했습니다. 잠시 후 다시 시도합니다.", this);
                if (spawnPanelRetryCoroutine == null)
                {
                    spawnPanelRetryCoroutine = StartCoroutine(RetryEnsureTutorialPanelSpawned());
                }
                return;
            }

            spawnedTutorialPanel = Instantiate(tutorialPanelPrefab, parent);
            var t = spawnedTutorialPanel.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            t.SetAsLastSibling(); // Canvas 맨 아래(자식 목록 마지막) = 화면 맨 위에 그려짐

            Debug.Log("<color=lime>[TutorialManager]</color> 튜토리얼 패널 스폰 완료.");
        }

        /// <summary>
        /// [HDY 요청 - 영지 HUD 패널에 가려지는 문제 수정] UIManager.HandleHudButtonClicked는 상점/창고/
        /// 도감 등 HUD 패널을 열 때마다 그 패널을 uiRoot(P_UIRoot)의 맨 마지막 자식으로 옮긴다(항상
        /// 최상단에 그려지게 하려고). 튜토리얼 패널도 같은 uiRoot 밑에 스폰되는데, 스폰 시점에만 한 번
        /// 맨 뒤로 보내고 끝이라, 그 이후에 플레이어가 HUD 패널을 하나라도 열면(영지에서 특히 자주
        /// 발생 - 여러 스텝이 상점/창고 등을 열어보라고 안내함) 그 패널이 튜토리얼 패널보다 나중
        /// 자식이 되어 위에 그려져 버렸다. 그래서 튜토리얼 UI를 보여주기 직전마다 다시 맨 뒤로 보내
        /// 항상 최상단을 유지한다.
        /// </summary>
        private void EnsureTutorialPanelOnTop()
        {
            if (spawnedTutorialPanel != null)
            {
                spawnedTutorialPanel.transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// [HDY 요청 - 영지 방문 시 진행 막힘 수정] EnsureTutorialPanelSpawned가 UI 루트를 못 찾아
        /// 실패했을 때 짧은 간격으로 재시도한다. 성공하면(spawnedTutorialPanel이 채워지면) 자연히 멈추고,
        /// 일정 시간 넘게 계속 실패하면 포기하고 에러 로그를 남긴다(도연님이 UIManager 배치를 확인할 수 있게).
        /// </summary>
        private IEnumerator RetryEnsureTutorialPanelSpawned()
        {
            const float retryInterval = 0.2f;
            const float giveUpAfterSeconds = 5f;
            float elapsed = 0f;

            while (spawnedTutorialPanel == null && elapsed < giveUpAfterSeconds)
            {
                yield return new WaitForSeconds(retryInterval);
                elapsed += retryInterval;
                EnsureTutorialPanelSpawned();
            }

            if (spawnedTutorialPanel == null)
            {
                Debug.LogError(
                    $"[TutorialManager] {giveUpAfterSeconds}초 동안 튜토리얼 패널을 스폰하지 못해 재시도를 중단합니다. " +
                    "UIManager가 이 씬에 배치되어 있는지 확인해주세요.",
                    this);
            }

            spawnPanelRetryCoroutine = null;
        }

        /// <summary>UIManager.UIRoot를 우선 사용하고, 그 씬에 UIManager가 없으면 씬에서 Canvas를 하나 찾아 대신 쓴다.</summary>
        // [HDY 요청 - Canvas_Main] 탐험 씬들(Main_World_3 등)은 공통으로 "Canvas_Main"이라는 이름의
        // 캐릭터 Canvas를 쓴다. UIManager 다음으로 이 이름을 최우선으로 찾는다.
        private const string ExplorationCanvasName = "Canvas_Main";

        private Transform ResolveTutorialPanelParent()
        {
            if (HDY.UI.UIManager.Instance != null && HDY.UI.UIManager.Instance.UIRoot != null)
            {
                return HDY.UI.UIManager.Instance.UIRoot;
            }

            // [HDY 요청 - Canvas_Main] UIManager가 없는 씬(탐험)에서는 씬마다 공통으로 쓰는
            // "Canvas_Main"을 이름으로 직접 찾아 최우선으로 쓴다.
            var canvasMain = FindCanvasByName(ExplorationCanvasName);
            if (canvasMain != null) return canvasMain.transform;

            // [HDY 요청 - 이상한 Canvas에 붙는 문제 수정, 2차] Canvas_Main도 없는 씬(알려지지 않은 씬)에서는
            // 예전엔 전용 Canvas를 새로 만들어 아주 높은 sortingOrder를 줬는데, 그러면
            // GH.Loading.LoadingManager.FindActiveSceneCanvas()가 "활성 씬에서 sortingOrder가 가장 높은
            // 루트 캔버스"를 자기 로딩 화면용으로 가져다 쓰는 로직과 정확히 충돌해서, 게임 자체의 로딩
            // 화면이 이 캔버스에 붙어버리는 부작용이 있었다(그 캔버스는 영구 오브젝트가 아니라서 씬 전환
            // 때 로딩 화면째로 파괴돼버림). 그래서 새로 만들기 전에, LoadingManager와 완전히 동일한
            // 기준(활성 씬의 루트 캔버스 중 WorldSpace가 아니고 sortingOrder가 가장 높은 것)으로 기존
            // 캔버스를 먼저 찾아 재사용한다.
            var existing = FindBestExistingRootCanvas();
            if (existing != null) return existing.transform;

            return ResolveOrCreateFallbackCanvas().transform;
        }

        /// <summary>[HDY 요청 - Canvas_Main] 활성 씬에서 정확히 이 이름을 가진 Canvas를 찾는다(비활성 포함).</summary>
        private Canvas FindCanvasByName(string canvasName)
        {
            if (string.IsNullOrEmpty(canvasName)) return null;

            Scene activeScene = SceneManager.GetActiveScene();
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var canvas in canvases)
            {
                if (canvas != null
                    && canvas.gameObject.scene == activeScene
                    && string.Equals(canvas.gameObject.name, canvasName, StringComparison.Ordinal))
                {
                    return canvas;
                }
            }

            return null;
        }

        /// <summary>
        /// [HDY 요청 - 이상한 Canvas에 붙는 문제 수정, 2차] GH.Loading.LoadingManager.FindActiveSceneCanvas()와
        /// 동일한 기준으로 활성 씬의 "가장 적합한" 루트 캔버스를 찾는다. 일부러 그쪽 로직을 그대로 따라해서,
        /// 로딩 화면이 붙는 캔버스와 튜토리얼 패널이 붙는 캔버스가 항상 일치하도록 맞춘다.
        /// </summary>
        private Canvas FindBestExistingRootCanvas()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Canvas best = null;
            foreach (var canvas in canvases)
            {
                if (canvas == null
                    || canvas.gameObject.scene != activeScene
                    || !canvas.gameObject.activeInHierarchy
                    || !canvas.isRootCanvas
                    || canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                if (best == null || canvas.sortingOrder > best.sortingOrder)
                {
                    best = canvas;
                }
            }

            return best;
        }

        /// <summary>
        /// [HDY 요청 - 이상한 Canvas에 붙는 문제 수정] UIManager가 없는 씬에서 튜토리얼 패널을 위한
        /// 전용 Canvas를 하나 만든다. ScreenSpaceOverlay + 매우 높은 sortingOrder로 그 씬의 다른 UI보다
        /// 항상 위에 그려지도록 한다. 씬이 바뀌면(HandleSceneLoaded) 참조를 비워서, 다음 씬에서 다시
        /// 필요할 때 새로 만든다 - 이전 씬의 Canvas는 씬 언로드와 함께 자동으로 파괴되므로 별도 정리는
        /// 필요 없다.
        /// </summary>
        /// <summary>
        /// [HDY 요청 - 이상한 Canvas에 붙는 문제 수정] FindBestExistingRootCanvas()로도 못 찾았을 때만
        /// (그 씬에 캔버스가 정말 하나도 없는 극단적인 경우) 쓰는 최후 수단. sortingOrder를 예전처럼 거의
        /// 최댓값으로 주지 않는다 - 이 씬에 캔버스가 정말 하나도 없다면 LoadingManager도 어차피 캔버스를
        /// 못 찾아 자기 쪽에서 에러를 내는 상황이라 값 자체는 크게 안 중요하지만, 혹시 이후에 다른 캔버스가
        /// 늦게 생기더라도 그쪽과 무리하게 경쟁하지 않도록 적당히 낮은 값만 준다.
        /// </summary>
        private Canvas ResolveOrCreateFallbackCanvas()
        {
            if (fallbackCanvas != null) return fallbackCanvas;

            var go = new GameObject("TutorialFallbackCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            fallbackCanvas = go.GetComponent<Canvas>();
            fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fallbackCanvas.sortingOrder = 1; // 기본값(0)보다만 살짝 위 - 다른 시스템의 "최상단 캔버스" 탐색과 무리하게 경쟁하지 않는다

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Debug.LogWarning(
                $"[TutorialManager] 이 씬({SceneManager.GetActiveScene().name})에서 재사용할 캔버스를 찾지 못해 튜토리얼 전용 폴백 Canvas를 새로 만들었습니다.",
                this);

            return fallbackCanvas;
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

        /// <summary>
        /// [HDY 요청 - 보상 미리보기 UI] TutorialRewardPreviewUI가 OnEnable에서 자기 자신을 등록할 때
        /// 호출한다. 보상이 있는 스텝을 완료할 때 이 UI가 등록돼 있으면 즉시 지급하지 않고 먼저
        /// 보상 목록을 보여준다(CompleteCurrentStep 참고).
        /// </summary>
        public void RegisterRewardPreviewUI(TutorialRewardPreviewUI ui)
        {
            rewardPreviewUI = ui;
        }

        public void UnregisterRewardPreviewUI(TutorialRewardPreviewUI ui)
        {
            if (rewardPreviewUI == ui) rewardPreviewUI = null;
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

            OnTutorialProgressChanged?.Invoke();

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
        /// [HDY 요청 - 조기 소모 방지] 지금 대기 중인(트리거를 기다리는) 스텝이 있다면 그 트리거
        /// 종류를, 없으면 null을 반환한다. TutorialSightDetector가 매 스캔마다 "지금 상관없는
        /// 카테고리까지 미리 스캔해서 나중에 필요할 때 다시 감지되지 않는" 문제를 피하려고 참고하는 값.
        /// </summary>
        public TutorialTriggerType? GetPendingStepTriggerType()
        {
            var pending = GetPendingStep();
            return pending?.triggerType;
        }

        /// <summary>
        /// [HDY 요청 - 카메라 참조 통합] TutorialSightDetector와 TutorialHighlightUI가 공통으로 쓰는
        /// 카메라 조회 지점. worldCamera가 Inspector에 지정돼 있으면 그걸 우선 쓰고, 없으면
        /// Camera.main으로 폴백한다(폴백 결과도 worldCamera에 캐싱해서 이후 호출부터는 재조회 없이 재사용).
        /// 이전에는 두 컴포넌트가 각자 독립적으로 Camera.main을 조회해서, 씬에 카메라가 여러 개거나
        /// 초기화 타이밍이 어긋나면 서로 다른 카메라를 참조하게 될 수 있었다(감지는 되는데 하이라이트만
        /// 안 보이는 버그의 유력한 원인) - 이제는 한 곳에서만 조회해서 항상 같은 카메라를 쓰도록 한다.
        /// </summary>
        public Camera ResolveWorldCamera()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            return worldCamera;
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
            // [HDY 요청 - 버그 수정] 여기서 dialogueUI.Hide()만 직접 부르고 SetDialogueInputBlock(false)를
            // 빼먹어서, 목표 대기 상태로 넘어간 뒤에도(대사창은 사라졌는데) 입력 차단이 풀리지 않는 문제가
            // 있었다 - RefreshDialoguePresentation()을 거치지 않는 유일한 Hide() 호출 지점이었다.
            dialogueUI?.Hide();
            SetDialogueInputBlock(false);

            if (current.objectives == null || current.objectives.Count == 0)
            {
                CompleteCurrentStep();
            }
            else
            {
                RefreshObjectivePresentation();

                // [HDY 요청 - 대사 재출력 버그 수정] 대사를 다 본 시점(목표 대기 상태로 전환)에도
                // 저장 신호를 쏴야, 대사만 다 보고 목표 진행 전에 바로 씬을 나가거나 게임을 꺼도
                // currentDialogueLineIndex가 최신 상태로 저장된다 - 안 그러면 다음에 들어왔을 때
                // 마지막으로 저장된(더 이전) 대사 줄부터 다시 보일 수 있다.
                OnTutorialProgressChanged?.Invoke();
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

            // [HDY 요청 - 대기 힌트] 대기 상태로 넘어가는 즉시 새 스텝의 Waiting_Hint를 HUD에 반영한다.
            // 이 호출이 없으면 트리거를 기다리는 동안 이전 스텝의 목표 텍스트가 그대로 남아있을 수 있었다.
            RefreshObjectivePresentation();

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

            // [HDY 요청 - 영지 HUD 패널에 가려지는 문제 수정] 상점/창고 등 HUD 패널을 열 때마다 그 쪽이
            // uiRoot 맨 뒤로 옮겨가 버려서, 스폰 시점에만 맨 뒤로 보내고 끝이면 튜토리얼 패널이 그 뒤에
            // 묻힐 수 있었다. 새 스텝을 보여주기 직전마다 다시 맨 뒤로 보내 항상 최상단을 유지한다.
            EnsureTutorialPanelOnTop();

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

            OnTutorialProgressChanged?.Invoke();

            if (IsCurrentStepObjectivesComplete())
            {
                CompleteCurrentStep();
            }
        }

        /// <summary>
        /// 스텝 완료 진입점. 보상이 있고(step.rewards.Count > 0) 보상 미리보기 UI가 등록돼 있으면
        /// 곧바로 지급하지 않고 그 UI에 목록을 먼저 보여준 뒤, 확인 버튼 콜백에서 FinalizeStepCompletion을
        /// 호출한다. 보상이 없거나 UI가 없으면(프리팹 미배치) 기존처럼 즉시 완료 처리한다.
        /// </summary>
        private void CompleteCurrentStep()
        {
            var step = GetCurrentStep();
            if (step == null) return;

            // [HDY 요청 - 보상 미리보기 UI] 확인 버튼을 누를 때까지 실제 완료(지급 + 다음 스텝 진행)를 미룬다.
            if (step.rewards != null && step.rewards.Count > 0 && rewardPreviewUI != null)
            {
                EnsureTutorialPanelOnTop(); // 위와 동일한 이유 - 보상 팝업도 다른 HUD 패널에 가려지지 않도록
                rewardPreviewUI.Show(step.questTitle, step.rewards, () => FinalizeStepCompletion(step));
                return;
            }

            FinalizeStepCompletion(step);
        }

        /// <summary>
        /// 실제 완료 처리(보상 지급 + 완료 기록 + 다음 스텝 진행). 이전에는 CompleteCurrentStep이 이 전부를
        /// 곧바로 수행했는데, 보상 미리보기 UI의 확인 버튼 콜백에서도 같은 로직이 필요해져 분리했다 -
        /// 동작 자체는 이전과 동일하다.
        /// </summary>
        private void FinalizeStepCompletion(TutorialStepData step)
        {
            if (step == null) return;

            GrantRewards(step);

            if (!completedStepIds.Contains(step.stepId))
            {
                completedStepIds.Add(step.stepId);
            }

            objectiveProgress.Clear();
            currentObjectiveProgressDebug.Clear();

            Debug.Log($"<color=lime>[TutorialManager]</color> 스텝 완료: {step.stepId}");

            // [HDY 요청 - 사운드] 퀘스트(튜토리얼 스텝) 완료 효과음.
            KMSAudioService.Play2D(GameSfxId.QuestComplete);

            // [HDY 요청 - 저장 시점 버그 수정] 예전엔 여기서 바로 OnTutorialProgressChanged를 쐈는데,
            // 그러면 currentStepIndex가 아직 "방금 완료한 스텝"을 가리키는 채로 저장이 이뤄져버렸다
            // (TryActivateNextPendingStep이 다음 스텝으로 넘어가기 전이라). 다음 스텝이 즉시 활성화되지
            // 않고 트리거를 기다리는 상태로 남으면(예: SceneEnter, ObjectSighted 등) 그 뒤로 아무도 다시
            // 저장을 안 해서, 저장 파일엔 계속 "방금 완료한 스텝"이 currentStepIndex로 남아있었다 -
            // 불러오면 이미 끝난 스텝을 다시 보여주는 버그였다(예: 7번을 완료하고 8번 대기 중에 종료하면
            // 재시작 시 7번부터 다시 시작). 그래서 TryActivateNextPendingStep을 먼저 호출해 실제로 다음
            // 상태가 확정된 뒤에 저장 이벤트를 쏘도록 순서를 바꿨다.
            TryActivateNextPendingStep();

            OnTutorialProgressChanged?.Invoke();
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
        /// 완료 보상을 지급한다. 기본은 WorldObject/Chest/ProductionCraftRuntime이 이미 쓰고 있는 것과
        /// 동일한 PlayerInventory.AddItem(itemId, amount) 공용 API를 그대로 사용한다(크로스팀 파일 수정
        /// 없음). 두 가지 특수 케이스가 있다:
        /// - itemId == "gold": 인벤토리 대신 TerritoryData.AddGold(amount)로 지급한다.
        /// - reward.applyFixedToolRefinement == true: 지급 직후 그 자리에 놓인 스택에
        ///   PlayerDefaultItemTest와 동일한 고정 연마(Rare/DamageIncrease/1)를 강제로 적용한다.
        /// </summary>
        private void GrantRewards(TutorialStepData step)
        {
            if (step.rewards == null || step.rewards.Count == 0) return;

            var inventory = FindFirstObjectByType<KmsPlayerInventory>();

            foreach (var reward in step.rewards)
            {
                if (string.IsNullOrEmpty(reward.itemId) || reward.amount <= 0) continue;

                // [HDY 요청 - 골드 보상] 인벤토리가 아니라 영지 골드로 지급한다.
                if (string.Equals(reward.itemId, GoldRewardItemId, StringComparison.OrdinalIgnoreCase))
                {
                    EnsureReferences();
                    if (territoryData != null)
                    {
                        territoryData.AddGold(reward.amount);
                    }
                    else
                    {
                        Debug.LogWarning("[TutorialManager] TerritoryData를 찾지 못해 골드 보상을 지급하지 못했습니다.");
                    }
                    continue;
                }

                if (inventory == null)
                {
                    Debug.LogWarning("[TutorialManager] PlayerInventory를 찾지 못해 보상을 지급하지 못했습니다.");
                    continue;
                }

                int remaining = inventory.AddItem(reward.itemId, reward.amount);
                int granted = reward.amount - remaining;

                // [HDY 요청 - 고정 연마 보상] PlayerDefaultItemTest와 동일한 방식으로, 지급 직후 그 자리에
                // 놓인 스택을 찾아 ForgeManager로 고정 연마를 강제로 채운다.
                if (granted > 0 && reward.applyFixedToolRefinement)
                {
                    ApplyFixedToolRefinementReward(inventory, reward.itemId);
                }
            }
        }

        /// <summary>
        /// [HDY 요청 - 고정 연마 보상] PlayerDefaultItemTest.ApplyFixedToolRefinement와 동일한 로직 -
        /// 방금 지급되어 인벤토리/퀵슬롯 어딘가에 놓인 이 itemId의 스택을 찾아 ForgeManager를 통해
        /// Rare/DamageIncrease/1 연마를 정확히 1칸만 강제로 채운다. 대장간 대상이 아닌 아이템(예: 몽둥이)이면
        /// ForgeManager가 조용히 false를 반환하며 무시한다.
        /// </summary>
        private void ApplyFixedToolRefinementReward(KmsPlayerInventory inventory, string itemId)
        {
            if (ForgeManager.Instance == null)
            {
                Debug.LogWarning($"[TutorialManager] ForgeManager를 찾을 수 없어 {itemId}에 고정 연마를 적용하지 못했습니다.");
                return;
            }

            ItemStack stack = FindLiveStackByItemId(inventory, itemId);
            if (stack == null)
            {
                Debug.LogWarning($"[TutorialManager] 지급 직후 {itemId} 스택을 인벤토리에서 찾지 못해 고정 연마를 적용하지 못했습니다.");
                return;
            }

            ForgeManager.Instance.TryAssignFixedRefinement(
                stack, FixedRefinementGrade, FixedRefinementOptionType, FixedRefinementDisplayName, FixedRefinementValue);
        }

        /// <summary>inventory.inventory / inventory.quickSlots를 훑어 itemId가 일치하는 첫 라이브 스택을 찾는다.</summary>
        private static ItemStack FindLiveStackByItemId(KmsPlayerInventory inventory, string itemId)
        {
            ItemStack found = FindLiveStackInContainer(inventory.inventory, itemId);
            if (found != null) return found;

            return FindLiveStackInContainer(inventory.quickSlots, itemId);
        }

        private static ItemStack FindLiveStackInContainer(InventoryContainer container, string itemId)
        {
            if (container == null || container.slots == null) return null;

            foreach (ItemStack stack in container.slots)
            {
                if (stack != null && !stack.IsEmpty && stack.itemId == itemId)
                {
                    return stack;
                }
            }

            return null;
        }

        // =====================================================================
        // 프레젠테이션 갱신
        // =====================================================================

        private void RefreshDialoguePresentation()
        {
            if (dialogueUI == null) return;

            var current = GetCurrentStep();

            // [HDY 요청 - 방어 코드] 보여줄 대사가 없는 모든 경우(아직 첫 스텝이 시작되기 전, 트리거
            // 대기 중, 대사를 다 넘긴 뒤)에 명시적으로 Hide()를 호출한다. RefreshHighlightPresentation은
            // 이미 이렇게 하고 있었는데 이 메서드만 그냥 return해서, 패널이 스폰 직후 기본 상태(보임 +
            // 클릭 차단)로 남아있는 문제가 있었다 - 특히 Title씬처럼 첫 스텝이 아직 시작 전인 상태에서
            // 패널만 먼저 스폰되면 화면 클릭이 막혀버렸다.
            if (current == null || currentStepAwaitingTrigger ||
                current.dialogueLines == null || currentDialogueLineIndex >= current.dialogueLines.Count)
            {
                dialogueUI.Hide();
                SetDialogueInputBlock(false);
                return;
            }

            dialogueUI.ShowLine(current.dialogueLines[currentDialogueLineIndex].text, current.triggerType);
            SetDialogueInputBlock(true);
        }

        /// <summary>
        /// [HDY 요청 - 대사 중 입력 차단] 대사가 떠 있는 동안 F(상호작용)와 클릭을 제외한 게임플레이
        /// 입력을 막는다. playerInput.SetGameplayInputBlocked(true)는 이동/좌클릭 채집·공격/점프/재장전/
        /// 핫바/이전·다음뿐 아니라 F(Interact)까지 포함한 Gameplay 액션맵 전체를 막는데, 상자(Chest)와
        /// 웨이포인트석(WayPointStone)은 전부 KMS.PlayerInteraction이 이 F(input.InteractPressed)를
        /// 구독해서 여는 구조라 이 하나로 "대사 중엔 상자 열기/웨이포인트 등록도 진행되지 않는다"는
        /// 방어까지 자동으로 해결된다(각 바인더에 별도 가드를 추가할 필요 없음). F만은 예외로 계속
        /// 눌려야 하므로, isDialogueInputBlockActive가 true인 동안 Update()에서 원시 키보드 입력
        /// (Keyboard.current)을 직접 읽어 F를 우회 처리한다(HandleInteractPressed 재사용 -
        /// InteractPressed 이벤트에는 의존하지 않음). 마우스 클릭(대화창의 "다음" 버튼 등 UI 클릭)은
        /// 애초에 PlayerInput의 Gameplay 액션맵과 무관하게 Unity UI 이벤트 시스템(EventSystem/
        /// GraphicRaycaster)이 독립적으로 처리하므로 막히지 않는다.
        /// </summary>
        private void SetDialogueInputBlock(bool blocked)
        {
            if (isDialogueInputBlockActive == blocked) return;
            isDialogueInputBlockActive = blocked;

            EnsureReferences();
            playerInput?.SetGameplayInputBlocked(blocked);
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

            // [HDY 요청 - 대기 힌트] 트리거를 기다리는 중(대사 없음)이면 그 스텝의 Waiting_Hint가 있을 때
            // 그 문구를 보여주고(예: "멤 찾기"), 없으면 기존처럼 공용 대기 문구를 보여준다.
            if (currentStepAwaitingTrigger)
            {
                var pending = GetPendingStep();
                return !string.IsNullOrEmpty(pending?.waitingHintText) ? pending.waitingHintText : NoObjectiveText;
            }

            if (current == null || current.objectives == null || current.objectives.Count == 0)
            {
                // 할당된 목표가 없을 때(스텝 시작 전/완료 후 등) HUD가 빈 텍스트 대신 대기 문구를 보여준다.
                return NoObjectiveText;
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
                currentDialogueLineIndex = currentDialogueLineIndex,
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
            currentDialogueLineIndex = snapshot.currentDialogueLineIndex;
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

            // [HDY 요청 - Main_World_3 진입 시 튜토리얼이 시작되지 않는 버그 수정] TutorialRecordData가
            // (Kyusoo 저장/불러오기 연동) 씬 로드 때마다 이 메서드를 호출한다. 새 게임이라 세이브에
            // 튜토리얼 진행도가 없으면 snapshot.currentStepIndex == -1이라, 여기서 그대로 두면 Title에서
            // Start()가 대기시켜둔 첫 스텝이 "아무 스텝도 없음"으로 덮어써진 채 아무도 다시
            // TryActivateNextPendingStep을 불러주지 않아 튜토리얼이 영원히 멈춘다. 그래서 진행도가
            // "한 번도 시작 안 함"(-1)일 때만 여기서 이어서 첫 스텝을 대기시킨다 - 이미 진행 중이던
            // 세이브를 불러온 경우(0 이상)는 복원된 상태를 그대로 유지해야 하므로 건드리지 않는다(다시
            // 부르면 복원된 스텝을 건너뛰고 다음 스텝으로 넘어가버림).
            if (currentStepIndex < 0)
            {
                TryActivateNextPendingStep();
            }
            else
            {
                RefreshObjectiveProgressDebugList(GetCurrentStep());
                RefreshDialoguePresentation();
                RefreshObjectivePresentation();
                RefreshHighlightPresentation();
            }
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
