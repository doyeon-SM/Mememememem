using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using HDY.Territory;

namespace HDY.UI
{
    /// <summary>
    /// 영지 골드/레벨을 HUD에 텍스트로 표시하는 컴포넌트.
    /// 골드는 "000" 형식, 레벨은 평소 "Lv. N"으로 표시하며, 레벨 버튼에 마우스를 올리면
    /// "현재경험치/필요경험치" 형식으로 바뀌고 글자 크기도 줄어든다(HandleLevelButtonHoverEnter/Exit).
    /// 값이 실제로 바뀐 프레임에만 텍스트를 다시 대입한다(KMS PlayerHUD.SetGoldText와 동일한 방식).
    /// TerritoryData에는 골드/경험치 변경 이벤트가 없어서 Update()에서 매 프레임 값을 직접 확인한다.
    ///
    /// [호버 시 경험치 표시 - HDY 요청] levelButton에 마우스를 올리면 레벨 텍스트가 경험치 표시로,
    /// 폰트 크기가 20(원래 값, Awake 시점에 그대로 캡처)에서 10으로 바뀐다. 마우스가 나가면 원래
    /// 레벨/폰트 크기로 되돌아온다. Button은 자체적으로 마우스 진입/이탈 이벤트를 주지 않으므로,
    /// UIManager.ManagedPanelCloseWatcher와 동일한 패턴으로 LevelButtonHoverRelay를 버튼 오브젝트에
    /// 자동으로 붙여서(Awake, AddComponent) 중계한다 - Inspector에서 EventTrigger를 따로 구성할
    /// 필요가 없다. 경험치 표시는 0 패딩 없이 그대로 숫자만 찍는다(요청 확인 완료).
    /// </summary>
    public class TerritoryHUDManager : MonoBehaviour
    {
        /// <summary>levelButton에 자동으로 붙어서 마우스 진입/이탈을 TerritoryHUDManager로 중계하는 컴포넌트.</summary>
        private class LevelButtonHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public TerritoryHUDManager Owner;

            public void OnPointerEnter(PointerEventData eventData) => Owner?.HandleLevelButtonHoverEnter();
            public void OnPointerExit(PointerEventData eventData) => Owner?.HandleLevelButtonHoverExit();
        }

        [Header("데이터 참조 (비어있으면 자동 탐색)")]
        [SerializeField] private TerritoryData territoryData;

        [Header("골드 텍스트 연결")]
        [Tooltip("\"Gold: 000 \" 형식으로 표시할 TMP_Text.")]
        [SerializeField] private TMP_Text goldText;

        [Header("영지 레벨 텍스트 연결")]
        [Tooltip("\"Lv. 00 \" 형식으로 표시할 TMP_Text.")]
        [SerializeField] private TMP_Text levelText;

        [Header("영지 레벨 버튼 (호버 시 경험치 표시로 전환)")]
        [Tooltip("이 버튼에 마우스를 올리면 levelText가 \"현재경험치/필요경험치\"로 바뀌고 폰트 크기가 10으로 줄어든다.")]
        [SerializeField] private Button levelButton;

        private const float HoverLevelFontSize = 13f;

        private int lastDisplayedGold = int.MinValue;
        private int lastDisplayedLevel = int.MinValue;
        private int lastDisplayedExp = int.MinValue;
        private int lastDisplayedRequiredExp = int.MinValue;
        private bool hasDisplayedGold;
        private bool hasDisplayedLevel;
        private bool hasDisplayedExp;

        private bool isHoveringLevelButton;
        private float normalLevelFontSize;

        private void Awake()
        {
            territoryData = TerritoryData.Resolve(territoryData);

            if (territoryData == null) Debug.LogWarning("[TerritoryHUDManager] TerritoryData를 찾을 수 없습니다. 골드가 표시되지 않습니다.", this);
            if (goldText == null) Debug.LogWarning("[TerritoryHUDManager] goldText가 비어있습니다.", this);
            if (levelText == null) Debug.LogWarning("[TerritoryHUDManager] levelText가 비어있습니다.", this);

            if (levelText != null) normalLevelFontSize = levelText.fontSize;

            if (levelButton != null)
            {
                var relay = levelButton.gameObject.AddComponent<LevelButtonHoverRelay>();
                relay.Owner = this;
            }
            else
            {
                Debug.LogWarning("[TerritoryHUDManager] levelButton이 비어있습니다. 호버 시 경험치 표시가 동작하지 않습니다.", this);
            }
        }

        private void Update()
        {
            if (territoryData == null)
            {
                territoryData = TerritoryData.Resolve(territoryData);
                if (territoryData == null) return;
            }

            RefreshGoldText(territoryData.Gold);

            if (isHoveringLevelButton)
            {
                RefreshExpText(territoryData.CurrentExp, territoryData.RequiredExp);
            }
            else
            {
                RefreshLevelText(territoryData.Level);
            }
        }

        /// <summary>골드 값이 실제로 바뀐 경우에만 텍스트를 다시 대입한다.</summary>
        private void RefreshGoldText(int gold)
        {
            if (goldText == null || (hasDisplayedGold && gold == lastDisplayedGold)) return;

            lastDisplayedGold = gold;
            hasDisplayedGold = true;
            goldText.text = $"{gold}";
        }

        private void RefreshLevelText(int level)
        {
            // [버그 수정] hasDisplayedGold를 검사하던 오타를 hasDisplayedLevel로 수정.
            if (levelText == null || (hasDisplayedLevel && level == lastDisplayedLevel)) return;
            lastDisplayedLevel = level;
            hasDisplayedLevel = true;
            levelText.text = $"Lv. {level}";
        }

        /// <summary>경험치 값이 실제로 바뀐 경우에만 텍스트를 다시 대입한다(호버 중일 때만 호출됨). 0 패딩 없음.</summary>
        private void RefreshExpText(int currentExp, int requiredExp)
        {
            if (levelText == null || (hasDisplayedExp && currentExp == lastDisplayedExp && requiredExp == lastDisplayedRequiredExp)) return;

            lastDisplayedExp = currentExp;
            lastDisplayedRequiredExp = requiredExp;
            hasDisplayedExp = true;
            levelText.text = $"{currentExp}/{requiredExp}";
        }

        private void HandleLevelButtonHoverEnter()
        {
            isHoveringLevelButton = true;
            if (levelText != null) levelText.fontSize = HoverLevelFontSize;

            hasDisplayedExp = false; // 다음 Update에서 즉시 새로 대입되도록
        }

        private void HandleLevelButtonHoverExit()
        {
            isHoveringLevelButton = false;
            if (levelText != null) levelText.fontSize = normalLevelFontSize;

            hasDisplayedLevel = false; // 다음 Update에서 즉시 새로 대입되도록
        }
    }
}
