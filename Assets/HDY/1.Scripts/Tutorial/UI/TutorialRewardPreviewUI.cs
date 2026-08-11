using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HDY.Item;

namespace HDY.Tutorial
{
    /// <summary>
    /// 보상이 있는 스텝을 완료하기 직전, 무엇을 받을지 미리 보여주고 확인 버튼을 누르면 그제서야
    /// TutorialManager가 실제 보상을 지급하도록 하는 팝업.
    ///
    /// [등록 방식] TutorialDialogueUI/TutorialHighlightUI와 동일한 패턴 - OnEnable에서 스스로
    /// TutorialManager에 등록하고 OnDisable에서 해제한다. 씬이 바뀌어도 새 씬에 배치된(또는 자동
    /// 스폰되는) 패널이 자동으로 재연결된다.
    ///
    /// [등록 안 됐을 때] 이 컴포넌트가 등록돼 있지 않으면(프리팹에 아직 배치 전) TutorialManager는
    /// 이 UI를 거치지 않고 보상이 있는 스텝도 곧바로 완료 처리한다 - 즉 이 UI를 프리팹에 넣기
    /// 전까지는 지금까지와 동일하게 동작한다.
    ///
    /// [아이콘 조회] itemId가 "gold"면 인벤토리 아이템이 아니라 ItemCatalogManager에서 찾을 수 없는
    /// 재화라, Inspector에 미리 등록해둔 goldIconSprite를 대신 쓴다. 그 외에는
    /// ItemCatalogManager.FindItemData로 조회한 ItemData.ItemIcon을 쓴다(ShopSlotUI가 쓰는 것과
    /// 동일한 조회 방식).
    ///
    /// [HDY 요청 - 아이템 이름 표시] 슬롯(TutorialRewardSlotUI)이 아이콘 + 개수뿐 아니라 이름도 함께
    /// 표시한다. 이름 조회는 ResolveItemName이 ResolveIcon과 같은 방식으로 처리한다(골드는 하드코딩된
    /// "골드", 나머지는 ItemCatalogManager 조회).
    ///
    /// [HDY 요청 - F키로도 확인 가능] 탐험 중에는 마우스 커서가 잠겨있어 버튼 클릭이 불가능하다.
    /// 그래서 확인 버튼 클릭과 TutorialManager가 F키(상호작용키) 입력을 받아 대신 호출해주는 경로가
    /// 완전히 동일하게 동작해야 한다 - 그래서 실제 확인 처리는 Confirm() 한 곳에 모아두고, 버튼
    /// 클릭 핸들러(HandleConfirmClicked)와 TutorialManager(IsAwaitingConfirm으로 확인 후 Confirm() 호출)
    /// 둘 다 이 메서드를 거친다.
    /// </summary>
    public class TutorialRewardPreviewUI : MonoBehaviour
    {
        // [HDY 요청] TutorialManager.GoldRewardItemId와 값은 같지만 접근자를 바꾸고 싶지 않아 별도로 둔다 -
        // "gold"라는 특수 itemId를 골드 재화로 취급하는 규칙 자체는 TutorialManager.GrantRewards가
        // 이미 갖고 있고, 여기서는 그 규칙을 그대로 따라 아이콘만 골라줄 뿐 지급 로직에는 관여하지 않는다.
        private const string GoldRewardItemId = "gold";

        // [HDY 요청 - 즉시 닫힘 방지] 대사를 넘기던 F키 연타가 그대로 이어지면, 팝업이 뜨는 바로 그
        // 입력 직후에 다음 F 입력이 곧바로 확인 처리를 해버려 팝업이 사실상 안 보이고 지나가는 문제가
        // 있었다. 뜬 직후 이 시간(초) 동안은 확인 입력(F키/버튼 모두)을 무시해서 최소한 눈에 보일
        // 시간을 확보한다.
        private const float ConfirmInputGuardSeconds = 0.2f;

        [Tooltip("비워두면 자동 탐색(TutorialManager.Resolve).")]
        [SerializeField] private TutorialManager tutorialManager;

        [Tooltip("비워두면 자동 탐색(ItemCatalogManager.Resolve). 아이템 아이콘 조회용.")]
        [SerializeField] private ItemCatalogManager itemCatalogManager;

        [Header("UI 참조")]
        [Tooltip("팝업 전체를 켜고 끄는 루트. 비워두면 이 오브젝트 자신을 사용한다(그 경우 SetActive 대신 CanvasGroup으로 표시만 껐다 켠다 - TutorialDialogueUI와 동일한 이유).")]
        [SerializeField] private GameObject rootPanel;
        [Tooltip("Quest_Title 컬럼 값을 표시할 텍스트. 제목 없는 스텝이면 빈 문자열이 그대로 들어간다.")]
        [SerializeField] private TMP_Text titleText;
        [Tooltip("보상 슬롯이 생성될 부모(Horizontal/Grid Layout Group 등이 붙은 Transform).")]
        [SerializeField] private Transform rewardListParent;
        [SerializeField] private TutorialRewardSlotUI rewardSlotPrefab;
        [SerializeField] private Button confirmButton;

        [Header("골드 아이콘 (인스펙터에 미리 등록)")]
        [Tooltip("보상 itemId가 \"gold\"일 때 쓸 아이콘. 골드는 인벤토리 아이템이 아니라 ItemCatalogManager에서 조회되지 않아 카탈로그 대신 이 값을 쓴다.")]
        [SerializeField] private Sprite goldIconSprite;

        private bool rootPanelIsSelf;
        private CanvasGroup selfCanvasGroup;
        private Action pendingConfirmCallback;
        private float shownAtTime;
        private readonly List<TutorialRewardSlotUI> spawnedSlots = new List<TutorialRewardSlotUI>();

        private void Awake()
        {
            if (rootPanel == null) rootPanel = gameObject;
            rootPanelIsSelf = rootPanel == gameObject;

            if (rootPanelIsSelf)
            {
                selfCanvasGroup = rootPanel.GetComponent<CanvasGroup>();
                if (selfCanvasGroup == null) selfCanvasGroup = rootPanel.AddComponent<CanvasGroup>();

                // [HDY 요청 - 부모 CanvasGroup과 곱연산되는 문제 수정] 이 오브젝트(P_Reward)는
                // P_TutorialRoot(대화창, 자체 CanvasGroup으로 alpha 0/1을 켰다 껐다 함)의 자식이다.
                // ignoreParentGroups를 켜두지 않으면 대화창이 숨겨질 때(alpha=0) 이 팝업의 alpha=1이
                // 곱연산으로 0이 되어 완전히 투명해진다 - 데이터상(자기 CanvasGroup 값)으로는 "보이는
                // 중"이라 F키/버튼 로직은 정상 동작하는데 실제로는 화면에 아무것도 안 보이는 버그가 있었다.
                selfCanvasGroup.ignoreParentGroups = true;
            }

            // TutorialDialogueUI와 동일한 방어 코드 - 등록되기 전/스텝이 시작되기 전까지 항상 숨김으로 시작한다.
            SetVisible(false);
        }

        private void OnEnable()
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);

            if (tutorialManager == null)
            {
                Debug.LogWarning("[TutorialRewardPreviewUI] TutorialManager를 찾을 수 없어 등록하지 못했습니다.", this);
            }
            else
            {
                tutorialManager.RegisterRewardPreviewUI(this);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(HandleConfirmClicked);
                confirmButton.onClick.AddListener(HandleConfirmClicked);
            }
        }

        private void OnDisable()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(HandleConfirmClicked);
            }

            tutorialManager?.UnregisterRewardPreviewUI(this);

            // 비활성화되는 시점(씬 전환 등)에 대기 중이던 콜백이 있어도 자동으로 호출하지 않는다 -
            // 보상 지급은 오직 확인 버튼 클릭(HandleConfirmClicked)에서만 일어난다.
            pendingConfirmCallback = null;
        }

        /// <summary>
        /// TutorialManager가 보상이 있는 스텝을 완료하기 직전 호출한다. 확인 버튼을 누르기 전까지는
        /// 실제 보상이 지급되지 않는다(콜백은 TutorialManager.FinalizeStepCompletion을 감싼 것).
        /// </summary>
        /// <param name="title">Quest_Title 컬럼 값. 비어있으면 빈 문자열을 그대로 표시한다.</param>
        /// <param name="rewards">이 스텝의 보상 목록(TutorialStepData.rewards).</param>
        /// <param name="onConfirmed">확인 버튼을 눌렀을 때 호출할 콜백.</param>
        public void Show(string title, IReadOnlyList<TutorialRewardEntry> rewards, Action onConfirmed)
        {
            pendingConfirmCallback = onConfirmed;
            shownAtTime = Time.unscaledTime;

            if (titleText != null) titleText.text = title ?? string.Empty;

            RebuildSlots(rewards);

            SetVisible(true);
        }

        private void RebuildSlots(IReadOnlyList<TutorialRewardEntry> rewards)
        {
            foreach (var slot in spawnedSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            spawnedSlots.Clear();

            if (rewardListParent == null || rewardSlotPrefab == null || rewards == null) return;

            foreach (var reward in rewards)
            {
                var slot = Instantiate(rewardSlotPrefab, rewardListParent);
                slot.Setup(ResolveIcon(reward.itemId), reward.amount, ResolveItemName(reward.itemId));
                spawnedSlots.Add(slot);
            }
        }

        private Sprite ResolveIcon(string itemId)
        {
            if (string.Equals(itemId, GoldRewardItemId, StringComparison.OrdinalIgnoreCase))
            {
                return goldIconSprite;
            }

            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);
            var itemData = itemCatalogManager != null ? itemCatalogManager.FindItemData(itemId) : null;
            return itemData != null ? itemData.ItemIcon : null;
        }

        /// <summary>ResolveIcon과 동일한 규칙으로 아이템 표시 이름을 찾는다. "gold"는 카탈로그에 없어 하드코딩된 이름을 대신 쓴다.</summary>
        private string ResolveItemName(string itemId)
        {
            if (string.Equals(itemId, GoldRewardItemId, StringComparison.OrdinalIgnoreCase))
            {
                return "골드";
            }

            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);
            var itemData = itemCatalogManager != null ? itemCatalogManager.FindItemData(itemId) : null;
            return itemData != null ? itemData.ItemName : itemId;
        }

        /// <summary>보상 팝업이 떠 있어 확인을 기다리는 중인지. TutorialManager가 F키 입력을 이 팝업의 확인으로 넘길지 판단할 때 쓴다.</summary>
        public bool IsAwaitingConfirm => pendingConfirmCallback != null;

        private void HandleConfirmClicked()
        {
            Confirm();
        }

        /// <summary>
        /// 확인 동작을 실행한다(패널 숨김 + 콜백 실행 → 실제 보상 지급/다음 스텝 진행). 확인 버튼 클릭과
        /// TutorialManager.HandleInteractPressed(F키)가 둘 다 이 메서드를 호출한다 - 두 경로가 똑같이
        /// 동작해야 탐험 중(마우스 사용 불가)에도 F만으로 보상을 받고 진행할 수 있다. 대기 중인 콜백이
        /// 없으면(이미 닫혔거나 애초에 안 열려있었으면) 아무 일도 하지 않는다 - F를 여러 번 눌러도 안전.
        /// </summary>
        public void Confirm()
        {
            if (pendingConfirmCallback == null) return;
            if (Time.unscaledTime - shownAtTime < ConfirmInputGuardSeconds) return; // 뜨자마자 같은 입력으로 닫히는 것 방지

            SetVisible(false);

            var callback = pendingConfirmCallback;
            pendingConfirmCallback = null;
            callback?.Invoke();
        }

        private void SetVisible(bool visible)
        {
            if (rootPanelIsSelf)
            {
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
