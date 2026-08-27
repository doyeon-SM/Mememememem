using UnityEngine;
using UnityEngine.UI;
using HDY.Territory;

namespace HDY.UI
{
    /// <summary>
    /// (멤) HUD 버튼 하나에 부착해서 영지 레벨(TerritoryData.Level)에 따라 interactable을 갱신하는 컴포넌트.
    ///
    /// 예전 UIManager.HudEntry.RequiredLevel + ApplyLevelGates()가 하던 일을 버튼 단위로 분리한 것입니다.
    /// "버튼이 눌릴 수 있는가"는 "패널이 열리고 닫히는가"와는 별개의 관심사라, SceneUIManager나
    /// HudPanelBootstrapper와 전혀 무관하게 독립적으로 동작합니다.
    ///
    /// [재진입 시 잠금 상태 갱신] TerritoryData는 DontDestroyOnLoad 싱글톤이라 레벨 자체는 씬을 나갔다
    /// 들어와도 유지되지만, 이 버튼이 SetActive(false) -> SetActive(true)로 재활성화되는 경우 Awake가 아니라
    /// OnEnable만 다시 실행됩니다. 그래서 OnEnable에서도 항상 최신 레벨 기준으로 다시 계산합니다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class HudButtonLevelGate : MonoBehaviour
    {
        [Tooltip("이 버튼이 활성화되는 데 필요한 영지 레벨. 0이면 레벨 제한 없이 항상 활성화.")]
        [SerializeField] private int requiredLevel = 0;

        [Tooltip("영지 레벨 참조 (비어있으면 자동 탐색).")]
        [SerializeField] private TerritoryData territoryData;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            territoryData = TerritoryData.Resolve(territoryData);
            if (territoryData != null)
            {
                territoryData.OnLevelChanged += HandleTerritoryLevelChanged;
            }

            ApplyGate();
        }

        private void OnDisable()
        {
            if (territoryData != null)
            {
                territoryData.OnLevelChanged -= HandleTerritoryLevelChanged;
            }
        }

        private void HandleTerritoryLevelChanged(int newLevel)
        {
            ApplyGate();
        }

        private void ApplyGate()
        {
            if (territoryData == null)
            {
                territoryData = TerritoryData.Resolve(territoryData);
            }

            int currentLevel = territoryData != null ? territoryData.Level : int.MaxValue;
            bool unlocked = requiredLevel <= 0 || currentLevel >= requiredLevel;

            if (button == null) button = GetComponent<Button>();
            if (button != null) button.interactable = unlocked;
        }
    }
}
