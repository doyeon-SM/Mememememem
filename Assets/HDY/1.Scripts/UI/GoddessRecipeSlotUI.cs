using System;
using UnityEngine;
using UnityEngine.UI;

namespace HDY.UI
{
    /// <summary>
    /// 여신상 UI 그리드의 레시피 슬롯 한 칸.
    /// 아이템 이미지(itemIconImage) 위에 해금 상태를 나타내는 잠금 이미지(lockImage)가 겹쳐진 구조.
    /// 클릭되면 OnClicked 이벤트로 Item_ID를 상위(GoddessStatueUI_LevelRow)에 전달하기만 한다.
    /// 실제 해금 가능 여부 판단, 구매 처리는 상위(GoddessStatueUI, RecipeUnlockManager)가 담당한다.
    /// </summary>
    public class GoddessRecipeSlotUI : MonoBehaviour
    {
        [Header("슬롯 UI 참조")]
        [SerializeField] private Button slotButton;
        [SerializeField] private Image itemIconImage;
        [SerializeField] private Image lockImage; // 해금 안 됐을 때 표시(아이템 이미지 위에 겹쳐짐)

        /// <summary>이 슬롯이 표시하고 있는 레시피의 Item_ID.</summary>
        public string ItemId { get; private set; }

        /// <summary>슬롯이 클릭되었을 때 발생. Item_ID를 함께 전달한다.</summary>
        public event Action<string> OnClicked;

        private void Awake()
        {
            if (slotButton != null)
            {
                slotButton.onClick.AddListener(HandleClick);
            }
            else
            {
                Debug.LogWarning($"[GoddessRecipeSlotUI] slotButton이 비어있습니다 ({gameObject.name}). 클릭이 동작하지 않습니다.", this);
            }
        }

        /// <summary>
        /// 슬롯에 표시할 레시피 데이터를 채운다.
        /// </summary>
        /// <param name="itemId">이 슬롯이 나타내는 레시피의 Item_ID</param>
        /// <param name="itemIcon">아이템 아이콘(ItemData.ItemIcon). null이면 비워둔다.</param>
        /// <param name="isUnlocked">이미 해금된 상태인지 여부 - true면 lockImage를 꺼서 잠금 표시를 없앤다.</param>
        /// <param name="interactable">지금 클릭(선택)이 가능한 상태인지 - 해금 조건을 만족하고 아직 미해금인 경우만 true.</param>
        public void SetData(string itemId, Sprite itemIcon, bool isUnlocked, bool interactable)
        {
            ItemId = itemId;

            if (itemIconImage != null)
            {
                itemIconImage.sprite = itemIcon;
            }

            if (lockImage != null)
            {
                lockImage.gameObject.SetActive(!isUnlocked);
            }

            if (slotButton != null)
            {
                slotButton.interactable = interactable;
            }

            SetSelected(false);
        }

        /// <summary>
        /// [HDY 요청] 선택 강조 표시(selectedHighlight)를 더 이상 쓰지 않기로 해서 실제로 하는 일은 없다.
        /// 다만 GoddessStatueUI_LevelRow.RefreshSelection이 모든 슬롯에 대해 이 메서드를 호출하고 있어서,
        /// 그 파일까지 함께 고치지 않아도 되도록 메서드 자체(빈 구현)는 남겨뒀다.
        /// </summary>
        public void SetSelected(bool selected)
        {
        }

        private void HandleClick()
        {
            OnClicked?.Invoke(ItemId);
        }
    }
}
