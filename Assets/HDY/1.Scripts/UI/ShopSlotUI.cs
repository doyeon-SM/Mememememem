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
    /// [최대 수량 표시 - 숫자만, 대량이면 "무한"] 구매/판매 모두 ShopUI가 넘겨주는 값은 상점 재고 그
    /// 자체(구매는 GetPurchaseStock, 판매는 GetSellStock - ShopItemData의 Purchase_MaxAmount/
    /// Selling_MaxAmount에서 그동안 소모된 만큼을 뺀 값)다. 이 값을 라벨 없이 숫자만 보여준다(예: "1000").
    ///
    /// [무한 표시 기준 = 화면 숫자가 아니라 ShopItemData의 설계상 최대치] 화면에 표시될 숫자(재고,
    /// 소모되면서 계속 줄어듦)가 아니라 ShopItemData.Purchase_MaxAmount(구매) / Selling_MaxAmount(판매)
    /// 가 InfiniteDisplayThreshold(10,000) 이상이면 숫자 대신 "무한"이라고 표시한다 - 예를 들어
    /// Purchase_MaxAmount가 999,999(사실상 무제한 설계)인 아이템은, 재고가 소모되어 화면 숫자 자체가
    /// 10,000 미만으로 떨어지더라도 계속 "무한"으로 표시된다(설계상 최대치 자체는 안 바뀌므로).
    /// </summary>
    public class ShopSlotUI : MonoBehaviour
    {
        public enum Mode { Buy, Sell }

        /// <summary>ShopItemData의 설계상 최대치(Purchase_MaxAmount/Selling_MaxAmount)가 이 값 이상이면 숫자 대신 "무한"으로 표시한다.</summary>
        private const int InfiniteDisplayThreshold = 10000;

        /// <summary>설계상 최대치가 InfiniteDisplayThreshold 이상일 때 표시할 문구.</summary>
        private const string InfiniteDisplayText = "무한";

        [Header("공통 표시")]
        [SerializeField] private Button slotButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;

        [Header("가격 표시 (아이콘 + 금액 하나)")]
        [SerializeField] private Image costIconImage;
        [SerializeField] private TMP_Text costAmountText;

        [Header("최대 수량 표시 (구매/판매 재고, 숫자만 또는 \"무한\")")]
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
        /// <param name="stock">현재 구매 재고(ShopStockManager.GetPurchaseStock, 화면에 표시될 숫자). 0이면 슬롯이 비활성화된다.</param>
        public void SetBuyData(ShopItemData itemData, ItemData catalogItem, Sprite costIcon, int unitPrice, int stock)
        {
            cachedItemData = itemData;

            ApplyCommonDisplay(catalogItem, itemData);
            ApplyCostDisplay(costIcon, unitPrice);
            ApplyMaxQuantityDisplay(stock, itemData.Purchase_MaxAmount);

            if (slotButton != null) slotButton.interactable = stock > 0;
        }

        /// <summary>판매 모드로 슬롯을 채운다. 판매는 항상 골드로 받으므로 costIcon 자리엔 골드 아이콘이 넘어온다.</summary>
        /// <param name="stock">현재 판매 재고(ShopStockManager.GetSellStock, 화면에 표시될 숫자). 0이면 슬롯이 비활성화된다.</param>
        public void SetSellData(ShopItemData itemData, ItemData catalogItem, Sprite goldIcon, int unitPrice, int stock)
        {
            cachedItemData = itemData;

            ApplyCommonDisplay(catalogItem, itemData);
            ApplyCostDisplay(goldIcon, unitPrice);
            ApplyMaxQuantityDisplay(stock, itemData.Selling_MaxAmount);

            if (slotButton != null) slotButton.interactable = stock > 0;
        }

        /// <summary>
        /// 최대 수량 텍스트를 채운다. designMaxAmount(ShopItemData.Purchase_MaxAmount/Selling_MaxAmount,
        /// 설계상 최대치)가 InfiniteDisplayThreshold 이상이면 "무한", 아니면 displayQuantity(재고)를
        /// 숫자 그대로 표시한다. 판단 기준이 재고(displayQuantity)가 아니라 설계상 최대치인 이유는,
        /// 사실상 무제한으로 잡아둔 아이템의 재고가 소모되어 화면 숫자가 작아지더라도 계속 "무한"으로
        /// 보여야 하기 때문이다.
        /// </summary>
        private void ApplyMaxQuantityDisplay(int displayQuantity, int designMaxAmount)
        {
            if (maxQuantityText == null) return;

            maxQuantityText.text = designMaxAmount >= InfiniteDisplayThreshold
                ? InfiniteDisplayText
                : displayQuantity.ToString();
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
