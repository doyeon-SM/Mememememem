using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HDY.Recipe;

namespace HDY.Territory
{
    /// <summary>
    /// 영지 확장 한 단계. RecipeUnlockEntry와 같은 모양(요구 레벨 + 재료 + 완료 여부)으로 맞췄다 -
    /// 여신상 UI의 레벨별 줄(GoddessStatueUI_LevelRow)에 레시피와 동일한 방식으로 표시하기 위함이다.
    /// 골드는 요구하지 않는다(재료만 소비).
    /// RewardExp: 이 확장 단계 완료에 성공했을 때 획득하는 영지 경험치(TerritoryData.CurrentExp).
    /// </summary>
    [Serializable]
    public class TerritoryExpansionEntry
    {
        [Tooltip("이 확장 단계가 나타나는(요구하는) 영지 레벨")]
        public int RequestTerritoryLevel = 1;

        [Tooltip("이 확장 단계에 필요한 재료 (골드는 없음)")]
        public List<Recipe_Requset_Item_Data> MaterialCosts = new List<Recipe_Requset_Item_Data>();

        [Tooltip("이 확장 단계에 성공하면 획득하는 영지 경험치")]
        public int RewardExp = 0;

        public bool IsExpanded;
    }

    /// <summary>
    /// 영지 확장(그리드 크기 확장)에 필요한 재료를 단계별로 관리하는 매니저.
    /// 실제 그리드 크기 변경은 GridManager.ExpandGrid(newWidth, newHeight)(_Kyusoo 팀원 파일, 수정하지 않음)를
    /// 그대로 호출해서 처리한다.
    ///
    /// [순차 진행 - 실제 확장 가능 여부] 확장은 리스트 순서대로 하나씩만 "진행"할 수 있다(CanAttemptExpand가
    /// GetNextPendingEntry()와 비교해서 판단). 완료된 단계는 IsExpanded=true로 표시된다.
    ///
    /// [UI 노출은 진행 순서와 별개] 여신상 UI(GoddessStatueUI)는 GetAllPendingEntries()로 아직 완료되지
    /// 않은 확장 단계를 전부(순서 무관) 각자의 요구 레벨 줄에 노출한다 - 예를 들어 Lv.3 단계가 아직 안
    /// 끝났어도 Lv.4 줄의 grid 자체는 보이고, 다만 그 슬롯은 잠금(비활성) 상태로 표시된다. "지금 클릭해서
    /// 실제로 진행할 수 있는지"는 여전히 CanAttemptExpand(= 순차 진행 규칙)로만 판단한다.
    ///
    /// [영지 경험치 보상] 확장에 성공하면(ApplyExpand) TerritoryExpansionEntry.RewardExp만큼 TerritoryData.AddExp가
    /// 호출된다. territoryData 참조는 인스펙터에 비어있으면 자동 탐색(FindFirstObjectByType)한다. ApplyExpand는
    /// 이미 entry.IsExpanded 가드가 있어 재호출로 인한 경험치 중복 지급은 발생하지 않는다.
    ///
    /// [그리드 크기 자체 추적 - 중요한 제약] GridManager의 현재 그리드 크기(currentWidth/currentHeight)는
    /// private이고 공개된 조회 방법이 없다(팀원 파일이라 추가 불가). 그래서 이 매니저가 startingGridSize(기본
    /// 5, GridManager.Start()의 InitializeGrid(5,5)와 맞춰야 함)부터 시작해서 확장할 때마다 +1씩 자체적으로
    /// 추적한다. 만약 이 매니저를 거치지 않고 다른 경로로 그리드 크기가 바뀌는 일이 생기면 추적값이
    /// 실제 그리드 크기와 어긋날 수 있다 - 현재는 그런 경로가 없어 보이지만, 나중에 GridManager 쪽에
    /// 그리드 크기를 바꾸는 다른 기능이 추가된다면 이 부분을 다시 확인해야 한다.
    ///
    /// [싱글톤 + DontDestroyOnLoad - 임시 조치] GameTimeManager와 동일한 패턴(Instance 싱글톤 + 중복 파괴 +
    /// DontDestroyOnLoad + Resolve(existing) 폴백)을 사용한다. 저장/불러오기 시스템이 붙기 전까지의 임시
    /// 조치이며, TerritoryData와 마찬가지로 추후 재검토가 필요하다.
    ///
    /// [씬 전환 시 GridManager 재탐색] gridManager는 Kyusoo팀 소유 파일이라 DontDestroyOnLoad를 걸 수 없다 -
    /// 즉 씬이 전환되면 이전 씬의 GridManager는 파괴되고 새 씬에 다시 배치된다. 이 매니저 자신은
    /// DontDestroyOnLoad로 유지되어 Awake가 다시 호출되지 않으므로, SceneManager.sceneLoaded 이벤트를
    /// 구독해 씬이 로드될 때마다 gridManager 참조를 다시 탐색한다. territoryData는 이 매니저처럼
    /// DontDestroyOnLoad로 함께 유지되므로 보통은 끊어지지 않지만, 혹시 몰라 같이 확인한다.
    /// </summary>
    public class TerritoryExpansionManager : MonoBehaviour
    {
        public static TerritoryExpansionManager Instance { get; private set; }

        [Header("데이터 참조")]
        [SerializeField] private GridManager gridManager;

        [Header("영지 데이터 참조 (경험치 지급용, 비어있으면 자동 탐색)")]
        [SerializeField] private TerritoryData territoryData;

        [Header("그리드 크기 추적 (GridManager가 현재 크기를 공개하지 않아 자체 추적)")]
        [Tooltip("GridManager.Start()의 InitializeGrid(5,5)와 반드시 맞춰야 한다.")]
        [SerializeField] private int startingGridSize = 5;

        private int currentGridSize;

        [Header("확장 단계 목록 (인스펙터에서 요구 레벨 + 재료 + 완료여부를 함께 등록/확인)")]
        [SerializeField] private List<TerritoryExpansionEntry> expansionSteps = new List<TerritoryExpansionEntry>();

        public IReadOnlyList<TerritoryExpansionEntry> ExpansionSteps => expansionSteps;

        /// <summary>영지 확장 상태(완료 여부/그리드 크기)가 바뀔 때마다 발행. 여신상 UI가 구독해서 다시 그린다.</summary>
        public event Action OnExpansionChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[TerritoryExpansionManager] 씬에 TerritoryExpansionManager가 이미 있어 중복 오브젝트를 파괴합니다.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
            if (gridManager == null) Debug.LogWarning("[TerritoryExpansionManager] gridManager를 찾을 수 없습니다.", this);

            if (territoryData == null) territoryData = FindFirstObjectByType<TerritoryData>();
            if (territoryData == null) Debug.LogWarning("[TerritoryExpansionManager] territoryData를 찾을 수 없습니다.", this);

            currentGridSize = startingGridSize;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        /// <summary>
        /// [씬 전환 대비] DontDestroyOnLoad로 유지되는 동안에는 Awake가 다시 호출되지 않으므로, 새 씬이
        /// 로드될 때마다 gridManager(Kyusoo팀 소유, 씬마다 새로 배치됨) 참조를 다시 탐색한다. territoryData도
        /// DontDestroyOnLoad로 함께 유지되어 보통은 끊어지지 않지만, 혹시 몰라 함께 확인한다.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            gridManager = FindFirstObjectByType<GridManager>();
            if (gridManager == null) Debug.LogWarning("[TerritoryExpansionManager] 씬 전환 후 gridManager를 찾을 수 없습니다.", this);

            if (territoryData == null) territoryData = FindFirstObjectByType<TerritoryData>();
        }

        /// <summary>아직 완료되지 않은 첫 번째(가장 앞선) 확장 단계를 반환한다. 전부 완료됐으면 null.
        /// "지금 실제로 진행 가능한 단계"를 뜻한다 - CanAttemptExpand/ApplyExpand가 이 값을 기준으로 판단한다.</summary>
        public TerritoryExpansionEntry GetNextPendingEntry()
        {
            foreach (var entry in expansionSteps)
            {
                if (!entry.IsExpanded) return entry;
            }

            return null;
        }

        /// <summary>
        /// 아직 완료되지 않은 확장 단계를 리스트 순서 그대로 전부 반환한다(GetNextPendingEntry와 달리 순서
        /// 제약 없이 전부). 여신상 UI가 "지금 진행 가능한 단계"뿐 아니라 "앞으로 나올 단계"도 각자의 요구
        /// 레벨 줄에 미리 보여주기(잠금 상태로) 위해 사용한다. 실제 진행 가능 여부 판단에는 쓰지 않는다 -
        /// 그건 여전히 CanAttemptExpand(=GetNextPendingEntry 기준)의 몫이다.
        /// </summary>
        public IEnumerable<TerritoryExpansionEntry> GetAllPendingEntries()
        {
            foreach (var entry in expansionSteps)
            {
                if (!entry.IsExpanded) yield return entry;
            }
        }

        /// <summary>
        /// 이 확장 단계를 지금 시도할 수 있는 상태인지 확인한다 - 영지 레벨 조건을 만족하고, 순서상
        /// "다음으로 진행할 단계"여야 한다(먼저 나오는 단계를 건너뛰고 나중 단계를 진행할 수 없음).
        /// </summary>
        public bool CanAttemptExpand(TerritoryExpansionEntry entry, int currentTerritoryLevel)
        {
            if (entry == null || entry.IsExpanded) return false;
            if (GetNextPendingEntry() != entry) return false;

            return currentTerritoryLevel >= entry.RequestTerritoryLevel;
        }

        /// <summary>
        /// [공용 업그레이드 팝업 경유 흐름에서 사용] 재료 결제가 이미 끝난 뒤 호출된다. 순수하게
        /// 완료 상태 반영 + 그리드 크기 자체 추적값 증가 + GridManager.ExpandGrid 호출 + RewardExp만큼
        /// 영지 경험치 지급을 담당한다. 이미 완료된 단계면 아무 것도 하지 않는다(재호출 시 경험치 중복 지급 방지).
        /// </summary>
        public void ApplyExpand(TerritoryExpansionEntry entry)
        {
            if (entry == null || entry.IsExpanded) return;

            entry.IsExpanded = true;
            currentGridSize += 1;

            gridManager?.ExpandGrid(currentGridSize, currentGridSize);

            if (territoryData == null) territoryData = FindFirstObjectByType<TerritoryData>();
            if (territoryData != null)
            {
                territoryData.AddExp(entry.RewardExp);
            }
            else
            {
                Debug.LogWarning("[TerritoryExpansionManager] territoryData를 찾을 수 없어 경험치를 지급하지 못했습니다.", this);
            }

            Debug.Log($"[TerritoryExpansionManager] 영지 확장 완료: {currentGridSize}x{currentGridSize}");

            OnExpansionChanged?.Invoke();
        }

        /// <summary>
        /// 다른 스크립트가 들고 있는 TerritoryExpansionManager 참조가 비어있을 때 쓰는 공용 폴백 탐색.
        /// 1) 이미 참조가 있으면 그대로 반환, 2) 없으면 싱글톤(Instance), 3) 그래도 없으면 씬 전체에서 검색.
        /// </summary>
        public static TerritoryExpansionManager Resolve(TerritoryExpansionManager existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<TerritoryExpansionManager>();
            if (found == null)
            {
                Debug.LogWarning("[TerritoryExpansionManager] 씬에서 TerritoryExpansionManager를 찾을 수 없습니다.");
            }

            return found;
        }
    }
}
