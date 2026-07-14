using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HDY.Item;
using HDY.Shop;

namespace HDY.UI
{
    /// <summary>
    /// 상점 슬롯을 클릭하면 뜨는 공용 구매/판매 팝업.
    /// 수량은 가로 슬라이더(1개씩 이동하는 +/- 버튼 포함) 또는 숫자 입력창으로 고르고,
    /// 고른 수량 기준 예상 가격(골드 또는 재료 하나)을 실시간으로 미리 보여준다.
    ///
    /// [배치] ShopUI와 같은 위치에 배치되어 있고(둘 다 씬에 하나씩), 평소에는 popupRoot가 꺼져 있다가
    /// Open()에서 켜진다.
    ///
    /// [역할 분리] 이 팝업은 수량 입력 UI와 미리보기만 담당한다. 실제 재고 차감/골드·재료 차감 같은
    /// 트랜잭션 로직은 ShopUI가 담당하고, Open()을 호출할 때 "결제를 실행하는 함수"(onConfirm)를
    /// 넘겨받아서 확인 버튼을 누르면 그 함수를 호출하기만 한다. onConfirm이 true(성공)를 반환할 때만
    /// 팝업을 닫는다 - false가 나오면(예: 그 사이 재고가 바뀐 경우) 열린 채로 둔다.
    ///
    /// [슬라이더 최대치] Open()으로 받는 maxQuantity는 이미 ShopUI가 "재고"와 "결제 가능한 재화" 둘 다
    /// 고려해서 계산한 값이다 - 즉 슬라이더를 끝까지 밀어도 정상적으로는 항상 결제가 가능해야 한다.
    ///
    /// [기본 수량 = 최대치부터 시작해서 내려감] 팝업을 열면 수량이 1부터 시작해서 올라가는 게 아니라
    /// maxQuantity(구매 가능/판매 가능한 최대치)부터 채워진 채로 시작한다 - "가진 만큼 전부 팔기/살 수
    /// 있는 만큼 전부 사기"가 기본값이고, 덜 사고 싶으면 -를 눌러 내리는 방식이다.
    /// </summary>
    public class ShopTransactionPopupUI : MonoBehaviour
    {
        [Header("팝업 루트 (평소에는 꺼져 있다가 Open()에서 켜짐)")]
        [SerializeField] private GameObject popupRoot;

        [Header("아이템 표시")]
        [SerializeField] private Image itemIconImage;
        [SerializeField] private TMP_Text itemNameText;

        [Header("가격 미리보기 (아이콘 + 선택한 수량만큼의 총액)")]
        [SerializeField] private Image costIconImage;
        [SerializeField] private TMP_Text costPreviewText;

        [Header("수량 입력")]
        [SerializeField] private Slider quantitySlider;
        [SerializeField] private Button decrementButton;
        [SerializeField] private Button incrementButton;
        [SerializeField] private TMP_InputField quantityInputField;

        [Header("액션 버튼")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmButtonLabel;
        [SerializeField] private Button cancelButton;

        private int unitPrice;
        private int maxQuantity;

        /// <summary>확인 버튼을 눌렀을 때 호출된다. 인자는 확정된 수량, 반환값은 결제 성공 여부 - 성공했을 때만 팝업이 닫힌다.</summary>
        private Func<int, bool> onConfirm;

        private void Awake()
        {
            if (quantitySlider != null)
            {
                quantitySlider.wholeNumbers = true;
                quantitySlider.onValueChanged.AddListener(HandleSliderChanged);
            }

            if (decrementButton != null) decrementButton.onClick.AddListener(HandleDecrementClicked);
            if (incrementButton != null) incrementButton.onClick.AddListener(HandleIncrementClicked);
            if (quantityInputField != null) quantityInputField.onEndEdit.AddListener(HandleInputFieldEndEdit);
            if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(Close);

            if (popupRoot != null) popupRoot.SetActive(false);
        }

        /// <summary>
        /// 팝업을 연다.
        /// </summary>
        /// <param name="mode">구매/판매 - 확인 버튼 라벨("구매"/"판매")에만 사용된다.</param>
        /// <param name="catalogItem">아이콘/이름 조회용 ItemData. 없으면 itemData.Item_ID를 그대로 표시.</param>
        /// <param name="costIcon">골드 아이콘 또는 재료 아이콘(어느 쪽인지는 ShopUI가 이미 판단해서 넘겨줌).</param>
        /// <param name="unitPrice">수량 1개당 가격.</param>
        /// <param name="maxQuantity">슬라이더 최대치(재고 + 결제 가능 여부까지 이미 반영된 값). 0이면 아무것도 할 수 없는 상태로 열린다.</param>
        /// <param name="onConfirm">확인 버튼을 눌렀을 때 호출할 콜백. 반환값이 true일 때만 팝업이 닫힌다.</param>
        public void Open(ShopSlotUI.Mode mode, ItemData catalogItem, ShopItemData itemData, Sprite costIcon, int unitPrice, int maxQuantity, Func<int, bool> onConfirm)
        {
            this.unitPrice = unitPrice;
            this.maxQuantity = Mathf.Max(0, maxQuantity);
            this.onConfirm = onConfirm;

            if (itemIconImage != null)
            {
                itemIconImage.sprite = catalogItem != null ? catalogItem.ItemIcon : null;
                itemIconImage.gameObject.SetActive(catalogItem != null && catalogItem.ItemIcon != null);
            }

            if (itemNameText != null) itemNameText.text = catalogItem != null ? catalogItem.ItemName : itemData.Item_ID;

            if (costIconImage != null)
            {
                costIconImage.sprite = costIcon;
                costIconImage.gameObject.SetActive(costIcon != null);
            }

            if (confirmButtonLabel != null) confirmButtonLabel.text = mode == ShopSlotUI.Mode.Buy ? "구매" : "판매";

            // 최대치부터 시작해서 내려가는 방식 - "가진/살 수 있는 만큼 전부"가 기본값이다.
            int initialQuantity = this.maxQuantity;

            if (quantitySlider != null)
            {
                quantitySlider.minValue = this.maxQuantity > 0 ? 1 : 0;
                quantitySlider.maxValue = this.maxQuantity;
                quantitySlider.interactable = this.maxQuantity > 0;
                quantitySlider.SetValueWithoutNotify(initialQuantity);
            }

            if (decrementButton != null) decrementButton.interactable = this.maxQuantity > 0;
            if (incrementButton != null) incrementButton.interactable = this.maxQuantity > 0;
            if (quantityInputField != null) quantityInputField.interactable = this.maxQuantity > 0;

            RefreshDisplay(initialQuantity);

            if (popupRoot != null) popupRoot.SetActive(true);
        }

        /// <summary>취소 버튼 및 결제 성공 시 호출. 콜백을 정리하고 팝업을 닫는다.</summary>
        public void Close()
        {
            onConfirm = null;
            if (popupRoot != null) popupRoot.SetActive(false);
        }

        private void HandleSliderChanged(float value)
        {
            RefreshDisplay(Mathf.RoundToInt(value));
        }

        private void HandleDecrementClicked()
        {
            if (quantitySlider == null) return;
            quantitySlider.value = Mathf.Max(quantitySlider.minValue, quantitySlider.value - 1);
        }

        private void HandleIncrementClicked()
        {
            if (quantitySlider == null) return;
            quantitySlider.value = Mathf.Min(quantitySlider.maxValue, quantitySlider.value + 1);
        }

        /// <summary>입력창에서 수량을 직접 타이핑했을 때. 범위를 벗어나면 clamp하고, 슬라이더 쪽으로 값을 밀어넣어 나머지 표시를 동기화한다.</summary>
        private void HandleInputFieldEndEdit(string text)
        {
            if (!int.TryParse(text, out int parsed)) parsed = maxQuantity;

            int minQuantity = maxQuantity > 0 ? 1 : 0;
            parsed = Mathf.Clamp(parsed, minQuantity, maxQuantity);

            if (quantitySlider != null) quantitySlider.value = parsed; // onValueChanged를 통해 RefreshDisplay가 호출된다
            else RefreshDisplay(parsed);
        }

        /// <summary>수량이 바뀔 때마다 입력창/가격 미리보기/확인 버튼 활성화 여부를 다시 그린다.</summary>
        private void RefreshDisplay(int quantity)
        {
            if (quantityInputField != null) quantityInputField.SetTextWithoutNotify(quantity.ToString());
            if (costPreviewText != null) costPreviewText.text = (unitPrice * quantity).ToString();
            if (confirmButton != null) confirmButton.interactable = quantity > 0 && quantity <= maxQuantity;
        }

        private void HandleConfirmClicked()
        {
            if (quantitySlider == null || onConfirm == null) return;

            int quantity = Mathf.RoundToInt(quantitySlider.value);
            if (quantity <= 0) return;

            bool success = onConfirm(quantity);
            if (success) Close();
        }
    }
}
