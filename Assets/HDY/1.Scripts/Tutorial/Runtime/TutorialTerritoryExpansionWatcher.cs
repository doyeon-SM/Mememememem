using UnityEngine;
using HDY.Territory;

namespace HDY.Tutorial
{
    /// <summary>
    /// TerritoryExpansionManager.OnExpansionChanged를 구독해서 "영지 확장 완료"를
    /// TutorialManager.NotifyObjectiveProgress("territory_expanded", 1)로 알려주는 바인더.
    ///
    /// [정확한 시점] OnExpansionChanged는 TerritoryExpansionManager.ApplyExpand(entry) 내부에서, 실제로
    /// 새 확장이 성공했을 때만 발행된다(entry.IsExpanded 가드가 있어 중복 호출로 잘못 여러 번 울릴 일도
    /// 없음). 그래서 폴링 없이 구독만으로 정확하게 감지할 수 있다.
    ///
    /// [CSV 사용법] 이 값을 쓰는 스텝은 objectiveKey를 정확히 "territory_expanded"로 지정해야 한다.
    /// </summary>
    public class TutorialTerritoryExpansionWatcher : MonoBehaviour
    {
        private const string ObjectiveKey = "territory_expanded";

        [Tooltip("비워두면 자동 탐색(TutorialManager.Resolve).")]
        [SerializeField] private TutorialManager tutorialManager;

        [Tooltip("비워두면 자동 탐색(TerritoryExpansionManager.Resolve).")]
        [SerializeField] private TerritoryExpansionManager territoryExpansionManager;

        private void OnEnable()
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            territoryExpansionManager = TerritoryExpansionManager.Resolve(territoryExpansionManager);

            if (territoryExpansionManager == null)
            {
                Debug.LogWarning("[TutorialTerritoryExpansionWatcher] TerritoryExpansionManager를 찾을 수 없어 등록하지 못했습니다.", this);
                return;
            }

            territoryExpansionManager.OnExpansionChanged -= HandleExpansionChanged; // 중복 구독 방지
            territoryExpansionManager.OnExpansionChanged += HandleExpansionChanged;
        }

        private void OnDisable()
        {
            if (territoryExpansionManager != null)
            {
                territoryExpansionManager.OnExpansionChanged -= HandleExpansionChanged;
            }
        }

        private void HandleExpansionChanged()
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            tutorialManager?.NotifyObjectiveProgress(ObjectiveKey, 1);
        }
    }
}
