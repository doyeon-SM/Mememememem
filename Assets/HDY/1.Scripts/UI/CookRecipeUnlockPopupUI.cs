using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HDY.UI
{
    /// <summary>
    /// [HDY 요청 - 상점 레시피북 해금 안내 팝업] 상점 프리팹에 미리 배치해두고 평소에는 꺼져 있다가,
    /// 레시피가 해금되거나(또는 해금할 레시피가 없어 환불되었을 때) 켜지는 안내 팝업.
    /// 아이콘(Image) / 이름·안내 텍스트(TMP_Text) / 확인 버튼(Button) 3개의 자식만 가진다(요청하신 구조 그대로).
    ///
    /// [큐 방식 - 여러 개 해금 시 갱신] 레시피북 1개로 여러 마리(수량)를 구매해 여러 레시피가 한 번에
    /// 해금될 수 있으므로, Enqueue로 표시할 항목을 전부 쌓아두고 Present()로 첫 항목을 보여준다.
    /// 확인 버튼을 누르면 큐의 다음 항목으로 갱신되고, 큐가 비면 그때 팝업이 닫힌다(재확인 절차 없음 -
    /// 요청하신 대로 확인 버튼은 항상 "다음으로 넘어가기 또는 닫기"만 한다).
    ///
    /// [환불 안내 표시] 해금할 레시피가 없어 환불되는 경우는 아이콘 없이(icon=null) nameText 자리에
    /// "해금가능한 레시피가 없어 000원이 환불되었습니다" 형식의 문구를 그대로 넣어서 Enqueue한다 -
    /// 별도의 텍스트 필드를 추가하지 않고 기존 이름 표시 자리를 그대로 재사용한다(요청하신 방식).
    ///
    /// [팝업 표시 주체 = 이 오브젝트 자신] popupRoot를 별도로 두지 않고 이 스크립트가 붙은 게임 오브젝트
    /// 자체를 켜고 끈다 - "팝업창에 3개의 자식이 있다"는 요청 그대로, 이 오브젝트가 곧 팝업창이다.
    /// </summary>
    public class CookRecipeUnlockPopupUI : MonoBehaviour
    {
        [Header("팝업 UI 참조 (아이콘 / 이름·안내 텍스트 / 확인 버튼 - 이 3개만 자식으로 둔다)")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Button confirmButton;

        /// <summary>큐에 쌓아둘 표시 항목 하나. icon이 null이면 아이콘 영역을 감추고 text만 보여준다(환불 안내용).</summary>
        private readonly struct PopupEntry
        {
            public readonly Sprite Icon;
            public readonly string Text;

            public PopupEntry(Sprite icon, string text)
            {
                Icon = icon;
                Text = text;
            }
        }

        private readonly Queue<PopupEntry> queue = new Queue<PopupEntry>();

        private void Awake()
        {
            if (iconImage == null) Debug.LogWarning("[CookRecipeUnlockPopupUI] iconImage가 비어있습니다.", this);
            if (nameText == null) Debug.LogWarning("[CookRecipeUnlockPopupUI] nameText가 비어있습니다. 안내 문구가 표시되지 않습니다.", this);

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(HandleConfirmClicked);
            }
            else
            {
                Debug.LogWarning("[CookRecipeUnlockPopupUI] confirmButton이 비어있습니다. 팝업을 넘기거나 닫을 수 없습니다.", this);
            }

            // 평소에는 꺼져 있다가 Present()가 호출될 때만 켜진다(상점 프리팹에 미리 배치되는 방식).
            gameObject.SetActive(false);
        }

        /// <summary>레시피가 해금되었을 때 표시할 항목을 큐에 쌓는다. Present()를 호출해야 실제로 보인다.</summary>
        public void EnqueueRecipeUnlocked(Sprite icon, string recipeName)
        {
            queue.Enqueue(new PopupEntry(icon, recipeName));
        }

        /// <summary>
        /// [HDY 요청 - 환불 안내] 해금할 레시피가 없어 환불되었을 때 표시할 항목을 큐에 쌓는다.
        /// 아이콘 없이 message를 그대로 nameText 자리에 넣는다 - message는 호출 쪽(ShopUI)이
        /// "해금가능한 레시피가 없어 000원이 환불되었습니다" 형식으로 완성해서 넘겨준다.
        /// </summary>
        public void EnqueueRefundNotice(string message)
        {
            queue.Enqueue(new PopupEntry(null, message));
        }

        /// <summary>
        /// 큐에 쌓아둔 항목을 실제로 보여주기 시작한다. 이미 팝업이 열려 다른 항목을 보여주는 중이면
        /// 아무 것도 하지 않는다 - 방금 Enqueue한 항목은 지금 보여주는 항목을 확인(다음으로 넘김)한 뒤
        /// 자연스럽게 이어서 나온다.
        /// </summary>
        public void Present()
        {
            if (gameObject.activeSelf) return;

            ShowNext();
        }

        /// <summary>큐에서 다음 항목을 꺼내 보여준다. 큐가 비어있으면 팝업을 닫는다.</summary>
        private void ShowNext()
        {
            if (queue.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            var entry = queue.Dequeue();
            gameObject.SetActive(true);

            if (iconImage != null)
            {
                iconImage.sprite = entry.Icon;
                iconImage.gameObject.SetActive(entry.Icon != null);
            }

            if (nameText != null)
            {
                nameText.text = entry.Text;
            }
        }

        /// <summary>확인 버튼 클릭 - 큐의 다음 항목으로 갱신하거나(남아있으면), 없으면 팝업을 닫는다.</summary>
        private void HandleConfirmClicked()
        {
            ShowNext();
        }
    }
}
