using System;
using HDY.Item;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KMS.InventoryDuped
{
    /// <summary>
    /// 가운데 클릭으로 꺼낼 수량을 정하는 KMS 인벤토리 전용 모달.
    /// 기존 프리팹 구조를 침범하지 않도록 현재 Canvas 아래에 런타임으로 한 번 생성한다.
    /// </summary>
    public sealed class InventoryQuantityPopupUI : MonoBehaviour
    {
        private const float PanelWidth = 440f;
        private const float PanelHeight = 300f;

        private RectTransform panelRect;
        private Image itemIcon;
        private TMP_Text titleText;
        private TMP_Text amountText;
        private TMP_Text minText;
        private TMP_Text maxText;
        private Slider slider;
        private Action<int> confirmAction;
        private Action cancelAction;
        private int maximumAmount;
        private int selectedAmount;

        public bool IsOpen => gameObject.activeSelf;

        public static InventoryQuantityPopupUI Create(Canvas canvas, TMP_FontAsset font)
        {
            if (canvas == null) return null;

            GameObject root = new GameObject("InventoryQuantityPopup", typeof(RectTransform), typeof(CanvasGroup));
            root.layer = canvas.gameObject.layer;
            root.transform.SetParent(canvas.transform, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            InventoryQuantityPopupUI popup = root.AddComponent<InventoryQuantityPopupUI>();

            // 패널과 형제인 전용 차단막만 바깥 클릭을 취소한다. 루트에 Button을 두면
            // Slider가 좌클릭 해제를 처리할 때 부모의 취소 클릭까지 실행될 수 있다.
            GameObject blockerObject = CreateImage("OutsideBlocker", root.transform, new Color(0.02f, 0.03f, 0.035f, 0.48f));
            RectTransform blockerRect = blockerObject.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;

            Button blockerButton = blockerObject.AddComponent<Button>();
            blockerButton.targetGraphic = blockerObject.GetComponent<Image>();
            blockerButton.onClick.AddListener(popup.Cancel);

            popup.Build(font);
            root.SetActive(false);
            return popup;
        }

        public void Show(ItemData item, int availableAmount, Vector2 screenPosition, Action<int> onConfirm, Action onCancel)
        {
            if (item == null || availableAmount <= 0) return;

            maximumAmount = availableAmount;
            confirmAction = onConfirm;
            cancelAction = onCancel;

            titleText.text = $"{item.ItemName} 수량 선택";
            itemIcon.sprite = item.ItemIcon;
            itemIcon.enabled = item.ItemIcon != null;
            minText.text = "1";
            maxText.text = maximumAmount.ToString();

            slider.minValue = 1f;
            slider.maxValue = maximumAmount;
            slider.wholeNumbers = true;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            PositionPanel(screenPosition);
            SetSelected(Mathf.CeilToInt(maximumAmount * 0.5f));
        }

        public void Cancel()
        {
            if (!IsOpen) return;

            Action callback = cancelAction;
            HideInternal();
            callback?.Invoke();
        }

        private void Update()
        {
            if (!IsOpen) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null)
            {
                bool largeStep = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    Cancel();
                    return;
                }

                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    Confirm();
                    return;
                }

                if (keyboard.leftArrowKey.wasPressedThisFrame) Adjust(largeStep ? -10 : -1);
                if (keyboard.rightArrowKey.wasPressedThisFrame) Adjust(largeStep ? 10 : 1);
            }

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (!Mathf.Approximately(scroll, 0f))
                {
                    bool largeStep = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
                    Adjust((scroll > 0f ? 1 : -1) * (largeStep ? 10 : 1));
                }

                if (mouse.rightButton.wasPressedThisFrame) Cancel();
            }
        }

        private void Build(TMP_FontAsset font)
        {
            GameObject panel = CreateImage("Panel", transform, new Color(0.075f, 0.095f, 0.11f, 0.98f));
            panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            Image accent = CreateImage("Accent", panel.transform, new Color(0.25f, 0.78f, 0.66f, 1f)).GetComponent<Image>();
            SetRect(accent.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 5f), new Vector2(0.5f, 1f));

            itemIcon = CreateImage("ItemIcon", panel.transform, Color.white).GetComponent<Image>();
            itemIcon.preserveAspect = true;
            itemIcon.raycastTarget = false;
            SetRect(itemIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -24f), new Vector2(52f, 52f), new Vector2(0f, 1f));

            titleText = CreateText("Title", panel.transform, font, 23f, TextAlignmentOptions.Left);
            SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(98f, -25f), new Vector2(-124f, 48f), new Vector2(0f, 1f));

            Button closeButton = CreateButton("Close", panel.transform, "×", font, new Color(0.18f, 0.22f, 0.24f, 1f));
            SetRect((RectTransform)closeButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(40f, 40f), new Vector2(1f, 1f));
            closeButton.onClick.AddListener(Cancel);

            amountText = CreateText("Amount", panel.transform, font, 34f, TextAlignmentOptions.Center);
            amountText.color = new Color(0.65f, 0.96f, 0.87f, 1f);
            SetRect(amountText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -91f), new Vector2(-48f, 48f), new Vector2(0f, 1f));

            slider = CreateSlider(panel.transform);
            SetRect((RectTransform)slider.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(55f, -156f), new Vector2(-110f, 28f), new Vector2(0f, 1f));
            slider.onValueChanged.AddListener(value => SetSelected(Mathf.RoundToInt(value), false));

            minText = CreateText("Min", panel.transform, font, 16f, TextAlignmentOptions.Left);
            SetRect(minText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -150f), new Vector2(30f, 28f), new Vector2(0f, 1f));

            maxText = CreateText("Max", panel.transform, font, 16f, TextAlignmentOptions.Right);
            SetRect(maxText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -150f), new Vector2(44f, 28f), new Vector2(1f, 1f));

            CreateAdjustButton(panel.transform, "Minus10", "−10", font, new Vector2(42f, -202f), -10);
            CreateAdjustButton(panel.transform, "Minus1", "−1", font, new Vector2(132f, -202f), -1);
            CreateAdjustButton(panel.transform, "Plus1", "+1", font, new Vector2(222f, -202f), 1);
            CreateAdjustButton(panel.transform, "Plus10", "+10", font, new Vector2(312f, -202f), 10);

            Button cancelButton = CreateButton("Cancel", panel.transform, "취소", font, new Color(0.20f, 0.24f, 0.26f, 1f));
            SetRect((RectTransform)cancelButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 22f), new Vector2(184f, 48f), Vector2.zero);
            cancelButton.onClick.AddListener(Cancel);

            Button confirmButton = CreateButton("Confirm", panel.transform, "확인", font, new Color(0.15f, 0.58f, 0.48f, 1f));
            SetRect((RectTransform)confirmButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-26f, 22f), new Vector2(184f, 48f), new Vector2(1f, 0f));
            confirmButton.onClick.AddListener(Confirm);
        }

        private void Confirm()
        {
            if (!IsOpen) return;

            int amount = selectedAmount;
            Action<int> callback = confirmAction;
            HideInternal();
            callback?.Invoke(amount);
        }

        private void HideInternal()
        {
            confirmAction = null;
            cancelAction = null;
            gameObject.SetActive(false);
        }

        private void Adjust(int delta)
        {
            SetSelected(selectedAmount + delta);
        }

        private void SetSelected(int amount, bool updateSlider = true)
        {
            selectedAmount = Mathf.Clamp(amount, 1, Mathf.Max(1, maximumAmount));
            amountText.text = $"{selectedAmount} / {maximumAmount}";
            if (updateSlider) slider.SetValueWithoutNotify(selectedAmount);
        }

        private void PositionPanel(Vector2 screenPosition)
        {
            RectTransform rootRect = (RectTransform)transform;
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screenPosition, camera, out Vector2 localPoint);
            localPoint += new Vector2(PanelWidth * 0.32f, -PanelHeight * 0.25f);

            Rect bounds = rootRect.rect;
            float halfWidth = PanelWidth * 0.5f;
            float halfHeight = PanelHeight * 0.5f;
            localPoint.x = Mathf.Clamp(localPoint.x, bounds.xMin + halfWidth + 12f, bounds.xMax - halfWidth - 12f);
            localPoint.y = Mathf.Clamp(localPoint.y, bounds.yMin + halfHeight + 12f, bounds.yMax - halfHeight - 12f);
            panelRect.anchoredPosition = localPoint;
        }

        private void CreateAdjustButton(Transform parent, string name, string label, TMP_FontAsset font, Vector2 position, int delta)
        {
            Button button = CreateButton(name, parent, label, font, new Color(0.14f, 0.19f, 0.21f, 1f));
            SetRect((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(86f, 38f), new Vector2(0f, 1f));
            button.onClick.AddListener(() => Adjust(delta));
        }

        private static Slider CreateSlider(Transform parent)
        {
            GameObject root = CreateUiObject("Slider", parent, typeof(Slider));
            Slider result = root.GetComponent<Slider>();

            Image background = CreateImage("Background", root.transform, new Color(0.12f, 0.15f, 0.17f, 1f)).GetComponent<Image>();
            SetRect(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));

            GameObject fillArea = CreateUiObject("Fill Area", root.transform);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(7f, 7f);
            fillAreaRect.offsetMax = new Vector2(-7f, -7f);

            Image fill = CreateImage("Fill", fillArea.transform, new Color(0.25f, 0.78f, 0.66f, 1f)).GetComponent<Image>();
            fill.type = Image.Type.Filled;
            SetRect(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));

            GameObject handleArea = CreateUiObject("Handle Slide Area", root.transform);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            Image handle = CreateImage("Handle", handleArea.transform, new Color(0.76f, 1f, 0.94f, 1f)).GetComponent<Image>();
            handle.rectTransform.sizeDelta = new Vector2(20f, 32f);

            result.fillRect = fill.rectTransform;
            result.handleRect = handle.rectTransform;
            result.targetGraphic = handle;
            result.direction = Slider.Direction.LeftToRight;
            return result;
        }

        private static Button CreateButton(string name, Transform parent, string label, TMP_FontAsset font, Color color)
        {
            GameObject root = CreateImage(name, parent, color);
            Button button = root.AddComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();

            TMP_Text text = CreateText("Label", root.transform, font, 18f, TextAlignmentOptions.Center);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            text.text = label;
            text.raycastTarget = false;
            return button;
        }

        private static GameObject CreateImage(string name, Transform parent, Color color)
        {
            GameObject result = CreateUiObject(name, parent, typeof(Image));
            result.GetComponent<Image>().color = color;
            return result;
        }

        private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject result = CreateUiObject(name, parent, typeof(TextMeshProUGUI));
            TMP_Text text = result.GetComponent<TMP_Text>();
            if (font != null) text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = false;
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
        {
            Type[] types = new Type[components.Length + 1];
            types[0] = typeof(RectTransform);
            Array.Copy(components, 0, types, 1, components.Length);

            GameObject result = new GameObject(name, types);
            result.layer = parent.gameObject.layer;
            result.transform.SetParent(parent, false);
            return result;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
