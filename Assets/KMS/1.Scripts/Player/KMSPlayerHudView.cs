using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMS
{
    /// <summary>
    /// uGUI presentation layer for the player HUD. Gameplay state remains in PlayerHUD.
    /// </summary>
    public sealed class KMSPlayerHudView : MonoBehaviour
    {
        [Header("Top Right Status")]
        [SerializeField] private TMP_Text realTimeText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private Button collectionButton;
        [SerializeField] private Button inventoryButton;
        [SerializeField] private Button mapButton;

        [Header("Survival Status")]
        [SerializeField] private RectTransform survivalStatus;
        [SerializeField] private Image healthFill;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private Image hungerFill;
        [SerializeField] private TMP_Text hungerText;

        [Header("Transient UI")]
        [SerializeField] private RectTransform notificationContainer;
        [SerializeField] private GameObject notificationTemplate;
        [SerializeField] private GameObject throwGuide;
        [SerializeField] private GameObject defeatOverlay;
        [SerializeField] private TMP_Text defeatMessageText;
        [SerializeField] private Button respawnButton;

        [Header("Responsive Layout")]
        [SerializeField, Range(0.1f, 1f)] private float survivalWidthRatio = 0.42f;
        [SerializeField, Min(0f)] private float survivalMinWidth = 500f;
        [SerializeField, Min(0f)] private float survivalMaxWidth = 800f;

        public Button CollectionButton => collectionButton;
        public Button InventoryButton => inventoryButton;
        public Button MapButton => mapButton;
        public Button RespawnButton => respawnButton;

        private void Awake()
        {
            if (notificationTemplate != null)
            {
                notificationTemplate.SetActive(false);
            }

            SetThrowGuideVisible(false);
            EnsureRespawnButton();
            SetDefeatOverlayVisible(false, string.Empty);
            UpdateResponsiveLayout();
        }

        private void OnRectTransformDimensionsChange()
        {
            UpdateResponsiveLayout();
        }

        public void SetHealth(float current, float max)
        {
            SetProgress(healthFill, healthText, current, max);
        }

        public void SetHunger(float current, float max)
        {
            SetProgress(hungerFill, hungerText, current, max);
        }

        public void SetRealTime(string value)
        {
            if (realTimeText != null) realTimeText.text = value;
        }

        public void SetGold(string value)
        {
            if (goldText != null) goldText.text = value;
        }

        public void SetSurvivalStatusVisible(bool visible)
        {
            if (survivalStatus != null) survivalStatus.gameObject.SetActive(visible);
        }

        public void SetThrowGuideVisible(bool visible)
        {
            if (throwGuide != null) throwGuide.SetActive(visible);
        }

        public void SetDefeatOverlayVisible(bool visible, string message)
        {
            if (defeatMessageText != null) defeatMessageText.text = message;
            if (defeatOverlay != null) defeatOverlay.SetActive(visible);
        }

        public void ShowNotification(string message, float duration)
        {
            if (notificationContainer == null || notificationTemplate == null || string.IsNullOrEmpty(message)) return;

            GameObject item = Instantiate(notificationTemplate, notificationContainer);
            item.name = "Notification";
            TMP_Text itemText = item.GetComponentInChildren<TMP_Text>(true);
            if (itemText != null) itemText.text = message;
            item.SetActive(true);

            CanvasGroup canvasGroup = item.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = item.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            StartCoroutine(RemoveNotificationAfterDelay(item, canvasGroup, duration));
        }

        public bool HasRequiredReferences()
        {
            return realTimeText != null
                   && goldText != null
                   && collectionButton != null
                   && inventoryButton != null
                   && mapButton != null
                   && survivalStatus != null
                   && healthFill != null
                   && healthText != null
                   && hungerFill != null
                   && hungerText != null
                   && notificationContainer != null
                   && notificationTemplate != null
                   && throwGuide != null
                   && defeatOverlay != null
                   && defeatMessageText != null
                   && respawnButton != null;
        }

        private void EnsureRespawnButton()
        {
            if (respawnButton != null || defeatOverlay == null) return;

            // 기존 임시 카운트다운 UI는 버튼 방식 리스폰으로 교체한다.
            Transform countdown = defeatOverlay.transform.Find("CountdownText");
            if (countdown != null) countdown.gameObject.SetActive(false);
            Transform divider = defeatOverlay.transform.Find("MessageDivider");
            if (divider != null) divider.gameObject.SetActive(false);

            GameObject buttonObject = new GameObject("RespawnButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(defeatOverlay.transform, false);

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, -35f);
            buttonRect.sizeDelta = new Vector2(220f, 64f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color32(245, 245, 245, 255);
            respawnButton = buttonObject.GetComponent<Button>();
            respawnButton.targetGraphic = image;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = "Respawn";
            label.fontSize = 26f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color32(25, 25, 25, 255);
        }

        private void UpdateResponsiveLayout()
        {
            if (survivalStatus == null) return;

            RectTransform canvasRect = survivalStatus.GetComponentInParent<Canvas>()?.transform as RectTransform;
            if (canvasRect == null || canvasRect.rect.width <= 0f) return;

            float maxWidth = Mathf.Max(survivalMinWidth, survivalMaxWidth);
            float width = Mathf.Clamp(canvasRect.rect.width * survivalWidthRatio, survivalMinWidth, maxWidth);
            survivalStatus.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        private static void SetProgress(Image fill, TMP_Text label, float current, float max)
        {
            float normalized = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (fill != null)
            {
                RectTransform fillRect = fill.rectTransform;
                Vector2 anchorMax = fillRect.anchorMax;
                anchorMax.x = normalized;
                fillRect.anchorMax = anchorMax;
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = Vector2.zero;
            }
            if (label != null) label.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
        }

        private static IEnumerator FadeOut(CanvasGroup canvasGroup, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
        }

        private IEnumerator RemoveNotificationAfterDelay(GameObject item, CanvasGroup canvasGroup, float duration)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration));
            if (item == null) yield break;

            yield return FadeOut(canvasGroup, 0.3f);
            if (item != null) Destroy(item);
        }
    }
}
