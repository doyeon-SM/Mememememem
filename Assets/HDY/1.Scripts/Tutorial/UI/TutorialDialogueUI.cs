using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HDY.Tutorial
{
    /// <summary>
    /// 튜토리얼 대사를 보여주는 대화창(텍스트/말풍선 위주, 캐릭터 초상화는 아직 미정이라 이후 배치에서
    /// 자리를 추가할 예정).
    ///
    /// [씬 전환 대응] 이 컴포넌트는 각 씬에 배치되는 평범한 UI 오브젝트다. GameTimeTextBinder와 동일한
    /// 패턴으로, OnEnable에서 TutorialManager에 자기 자신을 등록하고 OnDisable에서 해제한다 - 씬이
    /// 바뀌어도 새 씬의 대화창이 자동으로 재연결된다.
    ///
    /// [버그 수정 - 자기 자신을 껐다 켜면 등록이 풀리는 문제] rootPanel을 따로 지정하지 않으면
    /// Awake에서 이 스크립트가 붙은 오브젝트 자신을 rootPanel로 쓴다. 이 경우 Hide()에서
    /// rootPanel.SetActive(false)를 그대로 호출하면 스크립트 자신이 비활성화되면서 OnDisable이
    /// 실행되어 TutorialManager 등록이 풀려버리고, 그 직후(같은 프레임) 다음 스텝이 바로 활성화돼도
    /// 대화창이 등록 해제된 상태라 아무것도 표시되지 않는다(실제로 발생했던 버그).
    /// 그래서 rootPanel이 자기 자신일 때는 SetActive 대신 CanvasGroup으로 화면 표시만 껐다 켠다 -
    /// 오브젝트 자체는 항상 활성 상태로 유지돼 등록이 끊기지 않는다. 별도 자식 오브젝트를 rootPanel로
    /// 지정한 경우엔 지금처럼 SetActive를 그대로 쓴다(이 스크립트가 붙은 오브젝트 자신은 그대로
    /// 켜져있으니 문제 없음).
    ///
    /// [HDY 요청 - 퀘스트 형태별 아이콘] questTypeIcons에 스프라이트 4개를 채워두면, ShowLine이 받은
    /// TutorialTriggerType에 따라 questTypeIconImage의 스프라이트를 바꿔 보여준다
    /// (GetQuestTypeIconIndex 참고: index0=Manual, index1=SceneEnter, index2=LevelReached,
    /// index3=그 외 전부). 해당 인덱스에 스프라이트가 비어있으면 아이콘 자체를 숨긴다.
    /// </summary>
    public class TutorialDialogueUI : MonoBehaviour
    {
        [Tooltip("비워두면 자동 탐색(TutorialManager.Resolve).")]
        [SerializeField] private TutorialManager tutorialManager;

        [Header("UI 참조")]
        [Tooltip("대사 내용이 표시될 텍스트. (캐릭터 초상화 이미지는 추후 배치에서 이 옆에 추가 예정)")]
        [SerializeField] private TMP_Text bodyText;
        [Tooltip("대화창 전체를 켜고 끄는 루트. 비워두면 이 오브젝트 자신을 사용한다(그 경우 SetActive 대신 CanvasGroup으로 표시만 껐다 켠다).")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private Button nextButton;

        [Header("퀘스트 형태별 아이콘 (HDY 요청)")]
        [Tooltip("스프라이트를 실제로 표시할 대상 Image.")]
        [SerializeField] private Image questTypeIconImage;
        [Tooltip("index0=Manual, index1=SceneEnter, index2=LevelReached, index3=그 외 전부(시야 감지/포획/상자 개봉/웨이포인트 해금/UI 패널 오픈 등). GetQuestTypeIconIndex 참고.")]
        [SerializeField] private List<Sprite> questTypeIcons = new List<Sprite>(4);

        private bool rootPanelIsSelf;
        private CanvasGroup selfCanvasGroup;

        private void Awake()
        {
            if (rootPanel == null) rootPanel = gameObject;
            rootPanelIsSelf = rootPanel == gameObject;

            if (rootPanelIsSelf)
            {
                selfCanvasGroup = rootPanel.GetComponent<CanvasGroup>();
                if (selfCanvasGroup == null) selfCanvasGroup = rootPanel.AddComponent<CanvasGroup>();
            }

            // [HDY 요청 - 방어 코드] TutorialManager가 OnEnable에서 등록해줄 때까지(혹은 등록되더라도
            // 아직 첫 스텝이 시작되기 전까지) 항상 숨김 상태로 시작한다. CanvasGroup 기본값
            // (alpha=1, interactable=true, blocksRaycasts=true)을 그대로 두면, 특히 Title씬처럼 패널만
            // 먼저 스폰되고 첫 스텝은 아직 대기 중인 상황에서 화면 전체 클릭이 막혀버린다.
            SetVisible(false);
        }

        private void OnEnable()
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);

            if (tutorialManager == null)
            {
                Debug.LogWarning("[TutorialDialogueUI] TutorialManager를 찾을 수 없어 등록하지 못했습니다.", this);
            }
            else
            {
                tutorialManager.RegisterDialogueUI(this);
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(HandleNextClicked);
                nextButton.onClick.AddListener(HandleNextClicked);
            }
        }

        private void OnDisable()
        {
            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(HandleNextClicked);
            }

            tutorialManager?.UnregisterDialogueUI(this);
        }

        private void HandleNextClicked()
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            tutorialManager?.AdvanceDialogue();
        }

        /// <summary>
        /// [HDY 요청 - 퀘스트 형태별 아이콘] TutorialTriggerType을 questTypeIcons 리스트의 인덱스(0~3)로
        /// 매핑한다. index0=Manual, index1=SceneEnter, index2=LevelReached, index3=그 외 전부(시야 감지/
        /// 포획/상자 개봉/웨이포인트 해금/UI 패널 오픈 등 - 아직 이 그룹을 더 세분화할 만큼 아이콘이
        /// 충분하지 않아 하나로 묶었다). 원하는 분류가 다르면 이 매핑만 바꾸면 된다.
        /// </summary>
        private static int GetQuestTypeIconIndex(TutorialTriggerType triggerType)
        {
            switch (triggerType)
            {
                case TutorialTriggerType.Manual: return 0;
                case TutorialTriggerType.SceneEnter: return 1;
                case TutorialTriggerType.LevelReached: return 2;
                default: return 3;
            }
        }

        /// <summary>questTypeIconImage의 스프라이트를 트리거 종류에 맞는 것으로 바꾼다. 해당 인덱스가 비어있으면 아이콘을 숨긴다.</summary>
        private void ApplyQuestTypeIcon(TutorialTriggerType triggerType)
        {
            if (questTypeIconImage == null) return;

            int index = GetQuestTypeIconIndex(triggerType);
            Sprite sprite = (questTypeIcons != null && index >= 0 && index < questTypeIcons.Count) ? questTypeIcons[index] : null;

            questTypeIconImage.sprite = sprite;
            questTypeIconImage.gameObject.SetActive(sprite != null);
        }

        /// <summary>TutorialManager가 현재 대사를 보여줄 때 호출한다. triggerType은 퀘스트 형태별 아이콘 선택에 쓰인다.</summary>
        public void ShowLine(string text, TutorialTriggerType triggerType)
        {
            SetVisible(true);
            if (bodyText != null) bodyText.text = text;
            ApplyQuestTypeIcon(triggerType);
        }

        /// <summary>대사가 끝나거나 스텝이 바뀔 때 TutorialManager가 호출한다.</summary>
        public void Hide()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (rootPanelIsSelf)
            {
                // 자기 자신을 SetActive(false)로 끄면 OnDisable이 실행돼 TutorialManager 등록이 풀려버리므로,
                // CanvasGroup으로 화면 표시/입력만 껐다 켠다(오브젝트 자체는 항상 활성 상태 유지).
                selfCanvasGroup.alpha = visible ? 1f : 0f;
                selfCanvasGroup.interactable = visible;
                selfCanvasGroup.blocksRaycasts = visible;
            }
            else
            {
                rootPanel.SetActive(visible);
            }
        }
    }
}
