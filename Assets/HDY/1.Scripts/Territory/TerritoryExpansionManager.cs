using System;
using System.Collections.Generic;
using UnityEngine;
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
    /// [순차 진행] 확장 단계는 리스트 순서대로 하나씩만 진행 가능하다(GetNextPendingEntry). 완료된 단계는
    /// IsExpanded=true로 표시되고, 그 다음 단계가 정의된 요구 레벨에 맞는 줄에 노출된다.
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
    /// </summary>
    public class TerritoryExpansionManager : MonoBehaviour
    {
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
            if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
            if (gridManager == null) Debug.LogWarning("[TerritoryExpansionManager] gridManager를 찾을 수 없습니다.", this);

            if (territoryData == null) territoryData = FindFirstObjectByType<TerritoryData>();
            if (territoryData == null) Debug.LogWarning("[TerritoryExpansionManager] territoryData를 찾을 수 없습니다.", this);

            currentGridSize = startingGridSize;
        }

        /// <summary>아직 완료되지 않은 첫 번째(가장 앞선) 확장 단계를 반환한다. 전부 완료됐으면 null.</summary>
        public TerritoryExpansionEntry GetNextPendingEntry()
        {
            foreach (var entry in expansionSteps)
            {
                if (!entry.IsExpanded) return entry;
            }

            return null;
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
    }
}
