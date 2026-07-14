using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HDY.Item;
using HDY.Shop;

namespace HDY.UI
{
    /// <summary>
    /// 상점 슬롯 하나. 표시 전용이다 - 수량 조절이나 실제 결제는 이 슬롯이 하지 않는다.
    /// 슬롯 전체가 하나의 버튼이라, 클릭하면 ShopUI가 팝업(ShopTransactionPopupUI)을 열어
    /// 실제 수량 선택/결제를 진행한다.
    ///
    /// [클릭 핸들러 = 매번 교체] 구매 탭과 판매 탭이 같은 컨테이너/슬롯 풀을 공유하기 때문에, 같은 슬롯
    /// 오브젝트가 어떤 때는 구매용으로, 어떤 때는 판매용으로 재사용된다. 그래서 클릭 이벤트를
    /// 멀티캐스트(event, +=/-=)로 두지 않고 SetClickHandler로 "지금 이 슬롯이 눌렸을 때 호출할 함수
    /// 하나"만 들고 있다가 매번 통째로 교체한다 - +=/-=였다면 이전 모드의 핸들러가 제거되지 않고 같이
    /// 호출되는 문제가 생긴다.
    ///
    /// [가격 표시] 구매/판매 모두 재화가 정확히 하나(골드 또는 재료 하나)라서, 아이콘 하나 + 금액
    /// 하나로 통일해서 보여준다. 골드인지 재료인지, 그 아이콘이 무엇인지는 ShopUI가 미리 판단해서
    /// costIcon으로 넘겨준다 - 이 슬롯은 그 판단을 하지 않는다.
    ///
    /// [최대 수량 표시] 구매는 "재고와 결제 가능한 재화 둘 다 고려한 실제 최대 구매 수량", 판매는
    /// "보유 수량"을 표시한다. 이 값도 ShopUI가 계산해서 넘겨준다.
    /// </summary>
    public class ShopSlotUI : MonoBehaviour
    {
        public enum Mode { Buy, Sell }

        [Header("공통 표시")]
        [SerializeField] private Button slotButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;

        [Header("가격 표시 (아이콘 + 금액 하나)")]
        [SerializeField] private Image costIconImage;
        [SerializeField] private TMP_Text costAmountText;

        [Header("최대 수량 표시 (구매 가능 수량 / 판매 가능 수량)")]
        [SerializeField] private TMP_Text maxQuantityText;

        private ShopItemData cachedItemData;
        private Action<ShopItemData> onSlotClicked;

        private void Awake()
        {
            if (slotButton != null) slotButton.onClick.AddListener(HandleClicked);
        }

        /// <summary>이 슬롯이 클릭되었을 때 호출할 함수를 지정한다(이전에 지정된 핸들러가 있었다면 교체된다).</summary>
        public void SetClickHandler(Action<ShopItemData> handler)
        {
            onSlotClicked = handler;
        }

        /// <summary>구매 모드로 슬롯을 채운다.</summary>
        /// <param name="costIcon">골드 아이콘 또는 재료 아이콘(어느 쪽인지는 ShopUI가 판단해서 넘겨줌).</param>
        /// <param name="unitPrice">1개당 가격(골드 또는 재료 수량).</param>
        /// <param name="maxQuantity">재고와 결제 가능 여부를 모두 고려한 실제 최대 구매 수량. 0이면 슬롯이 비활성화된다.</param>
        public void SetBuyData(ShopItemData itemData, ItemData catalogItem, Sprite costIcon, int unitPrice, int maxQuantity)
        {
            cachedItemData = itemData;

            ApplyCommonDisplay(catalogItem, itemData);
            ApplyCostDisplay(costIcon, unitPrice);

            if (maxQuantityText != null) maxQuantityText.text = $"구매 가능 {maxQuantity}";
            if (slotButton != null) slotButton.interactable = maxQuantity > 0;
        }

        /// <summary>판매 모드로 슬롯을 채운다. 판매는 항상 골드로 받으므로 costIcon 자리엔 골드 아이콘이 넘어온다.</summary>
        /// <param name="ownedAmount">플레이어가 현재 보유한 수량(= 판매 가능한 최대 수량).</param>
        public void SetSellData(ShopItemData itemData, ItemData catalogItem, Sprite goldIcon, int unitPrice, int ownedAmount)
        {
            cachedItemData = itemData;

            ApplyCommonDisplay(catalogItem, itemData);
            ApplyCostDisplay(goldIcon, unitPrice);

            if (maxQuantityText != null) maxQuantityText.text = $"판매 가능 {ownedAmount}";
            if (slotButton != null) slotButton.interactable = ownedAmount > 0;
        }

        private void ApplyCommonDisplay(ItemData catalogItem, ShopItemData itemData)
        {
            if (iconImage != null)
            {
                iconImage.sprite = catalogItem != null ? catalogItem.ItemIcon : null;
                iconImage.gameObject.SetActive(catalogItem != null && catalogItem.ItemIcon != null);
            }

            if (nameText != null) nameText.text = catalogItem != null ? catalogItem.ItemName : itemData.Item_ID;
        }

        private void ApplyCostDisplay(Sprite costIcon, int unitPrice)
        {
            if (costIconImage != null)
            {
                costIconImage.sprite = costIcon;
                costIconImage.gameObject.SetActive(costIcon != null);
            }

            if (costAmountText != null) costAmountText.text = unitPrice.ToString();
        }

        private void HandleClicked()
        {
            if (cachedItemData == null) return;
            onSlotClicked?.Invoke(cachedItemData);
        }
    }
}
