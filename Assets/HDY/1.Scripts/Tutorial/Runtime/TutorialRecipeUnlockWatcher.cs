using UnityEngine;
using HDY.Recipe;

namespace HDY.Tutorial
{
    /// <summary>
    /// RecipeUnlockManager.OnRecipeUnlocksChanged를 구독해서, 지정한 특정 레시피(watchedItemId)가
    /// 실제로 해금됐을 때만 TutorialManager.NotifyObjectiveProgress(objectiveKey, 1)로 알려주는 바인더.
    ///
    /// [특정 아이템만 감지하는 이유] OnRecipeUnlocksChanged는 매개변수 없는 이벤트라(어떤 Item_ID가
    /// 방금 해금됐는지 알려주지 않는다), RecipeUnlockManager가 관리하는 수십 개의 레시피 중 아무거나
    /// 해금돼도 똑같이 울린다. 그래서 이벤트가 울릴 때마다 recipeUnlockManager.IsUnlocked(watchedItemId)를
    /// 직접 다시 확인해서, 우리가 감시하는 그 레시피가 실제로 해금된 경우에만 진행도를 보고한다 - 다른
    /// 레시피가 먼저/나중에 해금돼도 잘못 반응하지 않는다.
    ///
    /// [재사용 가능] watchedItemId/objectiveKey를 인스펙터에서 지정하는 구조라, 다른 레시피 해금을 확인해야
    /// 하는 튜토리얼 스텝이 생기면 이 컴포넌트를 하나 더 배치해서(다른 값으로) 재사용할 수 있다.
    ///
    /// [HDY 요청 - goddess_forge_unlock 스텝용] "제작대(blueprint_production_stand)를 실제로 해금했는지"를
    /// 확인하고 나서야 그 스텝의 보상(Rewards 컬럼)이 지급되도록 하기 위해 만들었다. 기본값이 이 스텝에
    /// 바로 맞춰져 있다 - TutorialStepCatalog.csv의 goddess_forge_unlock 행 Objectives 컬럼에
    /// "forge_unlocked:제작대 해금:1"을 추가해뒀으니, objectiveKey 기본값(forge_unlocked)을 그대로 쓰면 된다.
    /// </summary>
    public class TutorialRecipeUnlockWatcher : MonoBehaviour
    {
        [Tooltip("비워두면 자동 탐색(TutorialManager.Resolve).")]
        [SerializeField] private TutorialManager tutorialManager;

        [Tooltip("비워두면 씬에서 자동 탐색(FindFirstObjectByType).")]
        [SerializeField] private RecipeUnlockManager recipeUnlockManager;

        [Tooltip("이 Item_ID가 해금되는 순간만 감지한다(RecipeUnlockManager.RecipeUnlocks의 Item_ID와 동일해야 함).")]
        [SerializeField] private string watchedItemId = "blueprint_production_stand";

        [Tooltip("TutorialManager.NotifyObjectiveProgress에 전달할 목표 키. 튜토리얼 시트의 Objectives 컬럼과 일치해야 한다.")]
        [SerializeField] private string objectiveKey = "forge_unlocked";

        // 이미 이번에 통지했으면 다시 통지하지 않는다(다른 레시피가 나중에 또 해금돼서 이벤트가 다시
        // 울려도 중복 NotifyObjectiveProgress를 막기 위함 - 과다 호출 자체가 치명적이진 않지만 깔끔하게
        // 한 번만 보고한다).
        private bool hasNotified;

        private void OnEnable()
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            if (recipeUnlockManager == null) recipeUnlockManager = FindFirstObjectByType<RecipeUnlockManager>();

            if (recipeUnlockManager == null)
            {
                Debug.LogWarning("[TutorialRecipeUnlockWatcher] RecipeUnlockManager를 찾을 수 없어 등록하지 못했습니다.", this);
                return;
            }

            hasNotified = false;

            recipeUnlockManager.OnRecipeUnlocksChanged -= HandleRecipeUnlocksChanged; // 중복 구독 방지
            recipeUnlockManager.OnRecipeUnlocksChanged += HandleRecipeUnlocksChanged;

            // [방어 코드] 이 컴포넌트가 활성화되는 시점에 이미 해금되어 있는 경우(예: 씬을 나갔다
            // 들어오는 사이 다른 경로로 먼저 해금됨)도 놓치지 않도록 한 번 즉시 확인한다.
            HandleRecipeUnlocksChanged();
        }

        private void OnDisable()
        {
            if (recipeUnlockManager != null)
            {
                recipeUnlockManager.OnRecipeUnlocksChanged -= HandleRecipeUnlocksChanged;
            }
        }

        private void HandleRecipeUnlocksChanged()
        {
            if (hasNotified) return;
            if (recipeUnlockManager == null || string.IsNullOrEmpty(watchedItemId)) return;
            if (!recipeUnlockManager.IsUnlocked(watchedItemId)) return;

            hasNotified = true;

            tutorialManager = TutorialManager.Resolve(tutorialManager);
            tutorialManager?.NotifyObjectiveProgress(objectiveKey, 1);
        }
    }
}
