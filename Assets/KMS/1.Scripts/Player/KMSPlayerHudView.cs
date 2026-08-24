using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using HDY.Item;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        [SerializeField] private Color speedFoodEffectColor = new Color32(255, 132, 43, 255);
        [SerializeField] private Color otherFoodEffectColor = new Color32(174, 92, 255, 255);

        [Header("Health Restore Feedback")]
        [Tooltip("체력이 바뀌어 슬라이더(채움바)가 새 값까지 DOTween으로 서서히 차오르는 데 걸리는 시간(초).")]
        [SerializeField, Min(0.05f)] private float healthTweenDuration = 0.9f;

        [Header("Hunger Restore Feedback")]
        [SerializeField, Min(0.05f)] private float hungerRestoreDuration = 0.55f;
        [SerializeField, Range(1f, 1.25f)] private float hungerPulseScale = 1.06f;
        [SerializeField, Min(0.1f)] private float hungerRestorePopupDuration = 0.8f;
        [SerializeField, Min(0f)] private float hungerRestorePopupRise = 28f;
        [SerializeField, Min(8f)] private float hungerRestorePopupFontSize = 16f;
        [SerializeField] private Color hungerRestorePopupColor = new Color32(121, 255, 137, 255);

        [Header("Transient UI")]
        [SerializeField] private RectTransform notificationContainer;
        [SerializeField] private GameObject notificationTemplate;
        [SerializeField] private GameObject throwGuide;
        [SerializeField] private GameObject defeatOverlay;
        [SerializeField] private TMP_Text defeatMessageText;
        [SerializeField] private TMP_Text respawnGuidanceText;
        [SerializeField] private Button respawnButton;
        [SerializeField] private TMP_FontAsset deathFont;
        [SerializeField] private Sprite deathTitleIconSprite;
        [SerializeField] private Sprite respawnButtonIconSprite;
        [SerializeField] private Sprite deathDividerSprite;
        [SerializeField] private Sprite respawnButtonBackgroundSprite;
        [SerializeField, Min(0f)] private float deathOverlayDelay = 0.65f;
        [SerializeField, Min(0f)] private float deathOverlayFadeDuration = 0.35f;

        [Header("Item Obtained Toasts")]
        [SerializeField] private RectTransform itemObtainedToastContainer;
        [SerializeField] private KMSItemObtainedToastView itemObtainedToastTemplate;

        [Header("Responsive Layout")]
        [SerializeField, Range(0.1f, 1f)] private float survivalWidthRatio = 0.42f;
        [SerializeField, Min(0f)] private float survivalMinWidth = 500f;
        [SerializeField, Min(0f)] private float survivalMaxWidth = 800f;

        public Button CollectionButton => collectionButton;
        public Button InventoryButton => inventoryButton;
        public Button MapButton => mapButton;
        public Button RespawnButton => respawnButton;

        private readonly List<KMSHungerSegmentGraphic> hungerEffectSegmentGraphics =
            new List<KMSHungerSegmentGraphic>();
        private readonly List<ActiveItemObtainedToast> itemObtainedToasts = new List<ActiveItemObtainedToast>();
        private readonly List<GameObject> hungerRestorePopups = new List<GameObject>();
        private Coroutine hungerRestoreRoutine;
        private Coroutine deathPresentationRoutine;
        private CanvasGroup deathCanvasGroup;
        private Image deathTitleIconImage;
        private Image respawnButtonIconImage;
        private bool hungerInitialized;
        private float displayedHunger;
        private float displayedMaxHunger;
        private float requestedHunger;
        private float pendingFoodHungerRestore;
        private string pendingFoodEffectText;
        private Color pendingFoodEffectColor;
        private Vector3 hungerTrackBaseScale = Vector3.one;
        private float displayedHealth;
        private bool healthInitializedView;
        private Tween healthTween;

        private sealed class ActiveItemObtainedToast
        {
            public KMSItemObtainedToastView view;
            public Coroutine lifetimeRoutine;
        }

        private void Awake()
        {
            EnsureCollectionButtonInputLayer();

            if (notificationTemplate != null)
            {
                notificationTemplate.SetActive(false);
            }
            if (itemObtainedToastTemplate != null)
            {
                itemObtainedToastTemplate.gameObject.SetActive(false);
            }

            SetThrowGuideVisible(false);
            EnsureRespawnButton();
            ConfigureDeathPresentation();
            HideDeathPresentation();
            UpdateResponsiveLayout();
        }

        private void EnsureCollectionButtonInputLayer()
        {
            if (collectionButton == null) return;

            GameObject buttonObject = collectionButton.gameObject;
            Canvas buttonCanvas = buttonObject.GetComponent<Canvas>();
            if (buttonCanvas == null) buttonCanvas = buttonObject.AddComponent<Canvas>();

            // Keep the MemDex toggle reachable while the modal is rendered above the HUD.
            buttonCanvas.overrideSorting = true;
            buttonCanvas.sortingOrder = 1000;

            if (buttonObject.GetComponent<GraphicRaycaster>() == null)
            {
                buttonObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            UpdateResponsiveLayout();
        }

        private void OnDisable()
        {
            if (deathPresentationRoutine != null)
            {
                StopCoroutine(deathPresentationRoutine);
                deathPresentationRoutine = null;
            }
            healthTween?.Kill();
            healthTween = null;
            ResetHungerFeedback();
            ClearItemObtainedToasts();
        }

        /// <summary>
        /// 체력바 값을 즉시 적용하는 대신, 동일한 느낌의 ease-out-cubic 이징을 DOTween으로 구현해
        /// 슬라이더가 서서히 차오르도록(또는 줄어들도록) 한다.
        /// 체력은 자연회복 틱마다(1초) 값이 변하므로, 변화가 생길 때마다 매번 새로 재생한다.
        /// </summary>
        public void SetHealth(float current, float max)
        {
            current = Mathf.Clamp(current, 0f, Mathf.Max(0f, max));
            max = Mathf.Max(0f, max);

            if (!healthInitializedView)
            {
                healthInitializedView = true;
                displayedHealth = current;
                SetProgress(healthFill, healthText, current, max);
                return;
            }

            if (Mathf.Approximately(current, displayedHealth))
            {
                SetProgress(healthFill, healthText, current, max);
                return;
            }

            healthTween?.Kill();
            healthTween = DOTween.To(
                    () => displayedHealth,
                    value =>
                    {
                        displayedHealth = value;
                        SetProgress(healthFill, healthText, displayedHealth, max);
                    },
                    current,
                    healthTweenDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }

        public void SetHunger(
            float current,
            float max,
            KMSFoodEffectController foodEffects = null)
        {
            current = Mathf.Clamp(current, 0f, Mathf.Max(0f, max));
            max = Mathf.Max(0f, max);

            if (!hungerInitialized)
            {
                hungerInitialized = true;
                displayedHunger = current;
                displayedMaxHunger = max;
                requestedHunger = current;
                CacheHungerTrackScale();
                SetProgress(hungerFill, hungerText, current, max);
                ShowPendingFoodFeedback(0f);
                RenderHungerEffectSegments(foodEffects, max);
                return;
            }

            float restoredAmount = current - requestedHunger;
            float previousMax = displayedMaxHunger;
            requestedHunger = current;
            displayedMaxHunger = max;
            bool hasPendingFoodFeedback = pendingFoodHungerRestore > 0.001f
                                          || !string.IsNullOrEmpty(pendingFoodEffectText);
            bool animateRestore = restoredAmount > 0.001f && pendingFoodHungerRestore > 0.001f;

            if (hasPendingFoodFeedback)
            {
                ShowPendingFoodFeedback(Mathf.Max(0f, restoredAmount));
            }

            if (animateRestore)
            {
                StopHungerRestoreAnimation();
                CacheHungerTrackScale();
                hungerRestoreRoutine = StartCoroutine(AnimateHungerRestore(current, max));
            }
            else if (!Mathf.Approximately(current, displayedHunger)
                     || !Mathf.Approximately(max, previousMax))
            {
                StopHungerRestoreAnimation();
                displayedHunger = current;
                SetProgress(hungerFill, hungerText, current, max);
            }

            RenderHungerEffectSegments(foodEffects, max);
        }

        public void PrepareFoodFeedback(ItemData item, float restoredAmount)
        {
            pendingFoodHungerRestore = Mathf.Max(0f, restoredAmount);
            pendingFoodEffectText = BuildSpecialFoodEffectText(
                item,
                out bool hasSpeedEffect,
                out bool hasOtherEffect);
            pendingFoodEffectColor = hasSpeedEffect && !hasOtherEffect
                ? speedFoodEffectColor
                : otherFoodEffectColor;
        }

        private IEnumerator AnimateHungerRestore(float target, float max)
        {
            float start = displayedHunger;
            float duration = Mathf.Max(0.05f, hungerRestoreDuration);
            float elapsed = 0f;
            RectTransform track = GetHungerTrack();

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                displayedHunger = Mathf.Lerp(start, target, eased);
                SetProgress(hungerFill, hungerText, displayedHunger, max);
                if (track != null)
                {
                    float pulse = Mathf.Sin(progress * Mathf.PI) * (hungerPulseScale - 1f);
                    track.localScale = hungerTrackBaseScale * (1f + pulse);
                }
                yield return null;
            }

            displayedHunger = target;
            SetProgress(hungerFill, hungerText, target, max);
            if (track != null) track.localScale = hungerTrackBaseScale;
            hungerRestoreRoutine = null;
        }

        private void ShowPendingFoodFeedback(float actualRestoredAmount)
        {
            float popupRestore = Mathf.Min(
                Mathf.Max(0f, actualRestoredAmount),
                pendingFoodHungerRestore);
            Color popupColor = string.IsNullOrEmpty(pendingFoodEffectText)
                ? hungerRestorePopupColor
                : pendingFoodEffectColor;
            ShowFoodFeedbackPopup(popupRestore, pendingFoodEffectText, popupColor);
            pendingFoodHungerRestore = 0f;
            pendingFoodEffectText = null;
        }

        private void StopHungerRestoreAnimation()
        {
            if (hungerRestoreRoutine != null)
            {
                StopCoroutine(hungerRestoreRoutine);
                hungerRestoreRoutine = null;
            }

            RectTransform track = GetHungerTrack();
            if (track != null) track.localScale = hungerTrackBaseScale;
        }

        private void ShowFoodFeedbackPopup(
            float restoredAmount,
            string specialEffectText,
            Color popupColor)
        {
            RectTransform track = GetHungerTrack();
            if (track == null || hungerText == null) return;
            if (restoredAmount <= 0.001f && string.IsNullOrEmpty(specialEffectText)) return;

            var popupObject = new GameObject(
                "FoodFeedbackPopup",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(CanvasGroup));
            popupObject.layer = hungerText.gameObject.layer;
            popupObject.transform.SetParent(track, false);

            RectTransform rect = popupObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 5f);
            bool hasSpecialEffect = !string.IsNullOrEmpty(specialEffectText);
            rect.sizeDelta = new Vector2(
                Mathf.Max(140f, track.rect.width),
                hasSpecialEffect && restoredAmount > 0.001f ? 54f : 34f);

            TextMeshProUGUI label = popupObject.GetComponent<TextMeshProUGUI>();
            label.text = BuildFoodFeedbackText(restoredAmount, specialEffectText);
            label.font = hungerText.font;
            label.fontSharedMaterial = hungerText.fontSharedMaterial;
            label.fontSize = Mathf.Max(8f, hungerRestorePopupFontSize);
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = popupColor;
            label.lineSpacing = -8f;
            label.raycastTarget = false;

            CanvasGroup canvasGroup = popupObject.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            hungerRestorePopups.Add(popupObject);
            StartCoroutine(AnimateHungerRestorePopup(popupObject, rect, canvasGroup));
        }

        private IEnumerator AnimateHungerRestorePopup(
            GameObject popupObject,
            RectTransform rect,
            CanvasGroup canvasGroup)
        {
            float duration = Mathf.Max(0.1f, hungerRestorePopupDuration);
            Vector2 start = rect.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < duration && popupObject != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition = start + Vector2.up * (hungerRestorePopupRise * progress);
                rect.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f, Mathf.Clamp01(progress / 0.18f));
                canvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, progress));
                yield return null;
            }

            hungerRestorePopups.Remove(popupObject);
            if (popupObject != null) Destroy(popupObject);
        }

        private static string FormatHungerRestoreAmount(float amount)
        {
            float rounded = Mathf.Round(amount);
            return Mathf.Abs(amount - rounded) < 0.01f
                ? $"+{rounded:0}"
                : $"+{amount:0.#}";
        }

        private static string BuildFoodFeedbackText(float restoredAmount, string specialEffectText)
        {
            if (restoredAmount <= 0.001f) return specialEffectText;
            string restoredText = FormatHungerRestoreAmount(restoredAmount);
            return string.IsNullOrEmpty(specialEffectText)
                ? restoredText
                : $"{restoredText}\n{specialEffectText}";
        }

        private static string BuildSpecialFoodEffectText(
            ItemData item,
            out bool hasSpeedEffect,
            out bool hasOtherEffect)
        {
            hasSpeedEffect = false;
            hasOtherEffect = false;
            if (item == null || item.EatEffects == null) return string.Empty;

            var labels = new List<string>();
            for (int i = 0; i < item.EatEffects.Count; i++)
            {
                ItemEffect effect = item.EatEffects[i];
                if (effect == null
                    || effect.Effect == EffectType.Satiety
                    || Mathf.Approximately(effect.Value, 0f))
                {
                    continue;
                }

                string value = effect.Value > 0f
                    ? $"+{effect.Value:0.#}"
                    : $"{effect.Value:0.#}";
                switch (effect.Effect)
                {
                    case EffectType.Speed:
                        hasSpeedEffect = true;
                        labels.Add($"이동 속도 {value}%");
                        break;
                    case EffectType.Fulling:
                        hasOtherEffect = true;
                        labels.Add($"포만감 유지 {value}");
                        break;
                    default:
                        hasOtherEffect = true;
                        labels.Add($"{effect.Effect} {value}");
                        break;
                }
            }

            return string.Join(" · ", labels);
        }

        private RectTransform GetHungerTrack()
        {
            return hungerFill != null ? hungerFill.rectTransform.parent as RectTransform : null;
        }

        private void CacheHungerTrackScale()
        {
            RectTransform track = GetHungerTrack();
            if (track != null)
            {
                hungerTrackBaseScale = track.localScale;
            }
        }

        private void ResetHungerFeedback()
        {
            StopHungerRestoreAnimation();

            for (int i = hungerRestorePopups.Count - 1; i >= 0; i--)
            {
                if (hungerRestorePopups[i] != null) Destroy(hungerRestorePopups[i]);
            }

            hungerRestorePopups.Clear();
            hungerInitialized = false;
            pendingFoodHungerRestore = 0f;
            pendingFoodEffectText = null;
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

        public void ShowDeathPresentation(bool showWayPointGuidance)
        {
            ConfigureDeathPresentation();
            if (defeatMessageText != null) defeatMessageText.text = "캐릭터 사망";
            if (respawnGuidanceText != null)
            {
                respawnGuidanceText.text = "등록된 웨이포인트 중,\n가장 가까운 곳에서 부활합니다.";
                respawnGuidanceText.gameObject.SetActive(showWayPointGuidance);
            }
            SetRespawnButtonText("부활");
            SetRespawnButtonPosition(showWayPointGuidance);
            if (respawnButton != null) respawnButton.interactable = false;

            if (deathPresentationRoutine != null)
            {
                StopCoroutine(deathPresentationRoutine);
            }
            deathPresentationRoutine = StartCoroutine(ShowDeathPresentationRoutine());
        }

        public void HideDeathPresentation()
        {
            if (deathPresentationRoutine != null)
            {
                StopCoroutine(deathPresentationRoutine);
                deathPresentationRoutine = null;
            }

            if (respawnButton != null) respawnButton.interactable = false;
            if (deathCanvasGroup != null) deathCanvasGroup.alpha = 0f;
            if (defeatOverlay != null) defeatOverlay.SetActive(false);
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

        public void ShowItemObtained(
            ItemData item,
            int amount,
            float visibleDuration,
            float fadeDuration,
            int maxVisible)
        {
            if (item == null || amount <= 0 || itemObtainedToastContainer == null || itemObtainedToastTemplate == null)
                return;

            PruneItemObtainedToasts();
            while (itemObtainedToasts.Count >= Mathf.Max(1, maxVisible))
            {
                RemoveItemObtainedToast(itemObtainedToasts[0]);
            }

            KMSItemObtainedToastView instance = Instantiate(itemObtainedToastTemplate, itemObtainedToastContainer);
            instance.name = $"ItemObtained_{item.Item_ID}";
            instance.SetData(item, amount);
            instance.gameObject.SetActive(true);

            ActiveItemObtainedToast toast = new ActiveItemObtainedToast { view = instance };
            itemObtainedToasts.Add(toast);
            toast.lifetimeRoutine = StartCoroutine(
                RunItemObtainedToastLifetime(toast, visibleDuration, fadeDuration));
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
                   && respawnButton != null
                   && itemObtainedToastContainer != null
                   && itemObtainedToastTemplate != null;
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

        private void ConfigureDeathPresentation()
        {
            if (defeatOverlay == null) return;

            Image background = defeatOverlay.GetComponent<Image>();
            if (background != null) background.color = new Color32(0, 0, 0, 180);

            deathCanvasGroup = defeatOverlay.GetComponent<CanvasGroup>();
            if (deathCanvasGroup == null) deathCanvasGroup = defeatOverlay.AddComponent<CanvasGroup>();

            Transform countdown = defeatOverlay.transform.Find("CountdownText");
            if (countdown != null) countdown.gameObject.SetActive(false);

            if (respawnGuidanceText == null)
            {
                Transform existing = defeatOverlay.transform.Find("RespawnGuidanceText");
                if (existing != null) respawnGuidanceText = existing.GetComponent<TMP_Text>();
            }

            if (respawnGuidanceText == null)
            {
                GameObject guidanceObject = new GameObject(
                    "RespawnGuidanceText",
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI));
                guidanceObject.transform.SetParent(defeatOverlay.transform, false);
                respawnGuidanceText = guidanceObject.GetComponent<TextMeshProUGUI>();
            }

            ConfigureDeathText(defeatMessageText, 22f, new Vector2(12f, 48f), new Vector2(160f, 40f));
            ConfigureDeathText(respawnGuidanceText, 18f, new Vector2(0f, -28f), new Vector2(420f, 56f));

            deathTitleIconImage = EnsureDeathImage(
                "DeathTitleIcon",
                deathTitleIconSprite,
                defeatOverlay.transform,
                new Vector2(-70f, 48f),
                new Vector2(24f, 24f));

            Transform divider = defeatOverlay.transform.Find("MessageDivider");
            if (divider != null)
            {
                divider.gameObject.SetActive(true);
                Image dividerImage = divider.GetComponent<Image>();
                if (dividerImage != null)
                {
                    dividerImage.sprite = deathDividerSprite;
                    dividerImage.type = Image.Type.Sliced;
                    dividerImage.color = new Color32(255, 255, 255, 235);
                    dividerImage.raycastTarget = false;
                    dividerImage.pixelsPerUnitMultiplier = 25f;
                }
                RectTransform dividerRect = divider.GetComponent<RectTransform>();
                if (dividerRect != null)
                {
                    dividerRect.anchorMin = dividerRect.anchorMax = new Vector2(0.5f, 0.5f);
                    dividerRect.pivot = new Vector2(0.5f, 0.5f);
                    dividerRect.anchoredPosition = new Vector2(0f, 15f);
                    dividerRect.sizeDelta = new Vector2(170f, 3f);
                }
            }

            if (respawnButton != null)
            {
                RectTransform buttonRect = respawnButton.GetComponent<RectTransform>();
                if (buttonRect != null)
                {
                    buttonRect.sizeDelta = new Vector2(150f, 46f);
                }

                Image buttonBackground = respawnButton.GetComponent<Image>();
                if (buttonBackground != null)
                {
                    buttonBackground.sprite = respawnButtonBackgroundSprite;
                    buttonBackground.type = Image.Type.Sliced;
                    buttonBackground.color = new Color32(255, 255, 255, 100);
                    buttonBackground.pixelsPerUnitMultiplier = 12f;
                }
                Outline outline = respawnButton.GetComponent<Outline>();
                if (outline != null) outline.enabled = false;

                TMP_Text buttonLabel = respawnButton.GetComponentInChildren<TMP_Text>(true);
                if (buttonLabel != null)
                {
                    if (deathFont != null) buttonLabel.font = deathFont;
                    buttonLabel.fontSize = 18f;
                    buttonLabel.fontStyle = FontStyles.Bold;
                    buttonLabel.color = Color.white;
                    RectTransform labelRect = buttonLabel.rectTransform;
                    labelRect.offsetMin = new Vector2(30f, 0f);
                    labelRect.offsetMax = Vector2.zero;
                }

                respawnButtonIconImage = EnsureDeathImage(
                    "RespawnIcon",
                    respawnButtonIconSprite,
                    respawnButton.transform,
                    new Vector2(-38f, 0f),
                    new Vector2(24f, 24f));
            }

            if (deathTitleIconImage != null) deathTitleIconImage.transform.SetAsLastSibling();
            if (defeatMessageText != null) defeatMessageText.transform.SetAsLastSibling();
            if (divider != null) divider.SetAsLastSibling();
            if (respawnGuidanceText != null) respawnGuidanceText.transform.SetAsLastSibling();
            if (respawnButton != null) respawnButton.transform.SetAsLastSibling();
        }

        private Image EnsureDeathImage(
            string objectName,
            Sprite sprite,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            if (parent == null) return null;
            Transform existing = parent.Find(objectName);
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null)
            {
                GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
                imageObject.transform.SetParent(parent, false);
                image = imageObject.GetComponent<Image>();
            }

            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.gameObject.SetActive(sprite != null);

            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return image;
        }

        private void SetRespawnButtonPosition(bool showWayPointGuidance)
        {
            if (respawnButton == null) return;
            RectTransform rect = respawnButton.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(0f, showWayPointGuidance ? -92f : -42f);
            }
        }

        private void ConfigureDeathText(
            TMP_Text text,
            float fontSize,
            Vector2 position,
            Vector2 size)
        {
            if (text == null) return;
            if (deathFont != null) text.font = deathFont;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void SetRespawnButtonText(string value)
        {
            if (respawnButton == null) return;
            TMP_Text label = respawnButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = value;
        }

        private IEnumerator ShowDeathPresentationRoutine()
        {
            if (defeatOverlay == null) yield break;

            defeatOverlay.SetActive(false);
            if (deathOverlayDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(deathOverlayDelay);
            }

            defeatOverlay.SetActive(true);
            defeatOverlay.transform.SetAsLastSibling();
            deathCanvasGroup.alpha = deathOverlayFadeDuration > 0f ? 0f : 1f;

            float elapsed = 0f;
            while (elapsed < deathOverlayFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                deathCanvasGroup.alpha = Mathf.Clamp01(elapsed / deathOverlayFadeDuration);
                yield return null;
            }

            deathCanvasGroup.alpha = 1f;
            if (respawnButton != null)
            {
                respawnButton.interactable = true;
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(respawnButton.gameObject);
                }
            }
            deathPresentationRoutine = null;
        }

        private IEnumerator RunItemObtainedToastLifetime(
            ActiveItemObtainedToast toast,
            float visibleDuration,
            float fadeDuration)
        {
            if (toast.view == null) yield break;
            CanvasGroup canvasGroup = toast.view.CanvasGroup;
            if (canvasGroup == null)
            {
                RemoveItemObtainedToast(toast, false);
                yield break;
            }

            float fadeInElapsed = 0f;
            float fadeInDuration = Mathf.Min(0.16f, Mathf.Max(0f, fadeDuration));
            canvasGroup.alpha = fadeInDuration > 0f ? 0f : 1f;
            while (fadeInElapsed < fadeInDuration && toast.view != null)
            {
                fadeInElapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(fadeInElapsed / fadeInDuration);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, visibleDuration));
            if (toast.view == null) yield break;

            yield return FadeOut(canvasGroup, Mathf.Max(0f, fadeDuration));
            RemoveItemObtainedToast(toast, false);
        }

        private void PruneItemObtainedToasts()
        {
            itemObtainedToasts.RemoveAll(toast => toast == null || toast.view == null);
        }

        private void RemoveItemObtainedToast(ActiveItemObtainedToast toast, bool stopRoutine = true)
        {
            if (toast == null) return;
            if (stopRoutine && toast.lifetimeRoutine != null) StopCoroutine(toast.lifetimeRoutine);
            itemObtainedToasts.Remove(toast);
            if (toast.view != null) Destroy(toast.view.gameObject);
        }

        private void ClearItemObtainedToasts()
        {
            for (int i = itemObtainedToasts.Count - 1; i >= 0; i--)
            {
                RemoveItemObtainedToast(itemObtainedToasts[i]);
            }
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

        // [HDY 요청 - KMS 승인 - 음식 큐 통합] KMSFoodEffectController가 효과 없는(포만감만) 음식과
        // 효과 있는 음식을 하나의 큐(FoodSegments)로 통합했다. 위치 계산(cursor)은 큐의 모든 세그먼트에
        // 대해 실제 취식 순서 그대로 진행해야 뒤 세그먼트 위치가 정확하지만, 화면에 색 오버레이를 그리는
        // 것은 기존과 동일하게 실제 게임플레이 효과가 있는 세그먼트만으로 유지한다(효과 없는 세그먼트는
        // 오버레이 없이 기본 배고픔 바 색 그대로 노출된다).
        private void RenderHungerEffectSegments(
            KMSFoodEffectController foodEffects,
            float maxHunger)
        {
            int requiredCount = foodEffects != null ? foodEffects.FoodSegments.Count : 0;
            EnsureHungerEffectSegmentCount(requiredCount);

            float trackedSatiety = 0f;
            if (foodEffects != null)
            {
                for (int i = 0; i < foodEffects.FoodSegments.Count; i++)
                {
                    KMSFoodEffectSegment trackedSegment = foodEffects.FoodSegments[i];
                    if (trackedSegment != null)
                    {
                        trackedSatiety += Mathf.Max(0f, trackedSegment.RemainingSatiety);
                    }
                }
            }
            float visibleEnd = maxHunger > 0f ? Mathf.Clamp01(trackedSatiety / maxHunger) : 0f;

            float cursor = 0f;
            for (int i = 0; i < hungerEffectSegmentGraphics.Count; i++)
            {
                KMSHungerSegmentGraphic graphic = hungerEffectSegmentGraphics[i];
                if (graphic == null) continue;

                if (i >= requiredCount || maxHunger <= 0f)
                {
                    graphic.gameObject.SetActive(false);
                    continue;
                }

                KMSFoodEffectSegment segment = foodEffects.FoodSegments[i];
                if (segment == null || segment.RemainingSatiety <= 0f)
                {
                    graphic.gameObject.SetActive(false);
                    continue;
                }

                float start = Mathf.Clamp01(cursor / maxHunger);
                cursor += segment.RemainingSatiety;
                float end = Mathf.Clamp01(cursor / maxHunger);

                if (segment.Effects.Count == 0)
                {
                    // 효과 없는(포만감만) 세그먼트: cursor는 이미 전진했으므로 뒤 세그먼트 위치는 정확하다.
                    graphic.gameObject.SetActive(false);
                    continue;
                }

                RectTransform rect = graphic.rectTransform;
                rect.anchorMin = new Vector2(start, 0f);
                rect.anchorMax = new Vector2(end, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                graphic.color = GetFoodEffectColor(segment);
                graphic.SetRoundedEnds(start <= 0.0001f, end >= visibleEnd - 0.0001f);
                graphic.gameObject.SetActive(end > start);
            }
        }

        private void EnsureHungerEffectSegmentCount(int count)
        {
            if (hungerFill == null || hungerFill.rectTransform.parent == null) return;

            while (hungerEffectSegmentGraphics.Count < count)
            {
                var segmentObject = new GameObject(
                    $"FoodEffectSegment_{hungerEffectSegmentGraphics.Count}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(KMSHungerSegmentGraphic));
                segmentObject.layer = hungerFill.gameObject.layer;
                segmentObject.transform.SetParent(hungerFill.rectTransform.parent, false);

                KMSHungerSegmentGraphic graphic = segmentObject.GetComponent<KMSHungerSegmentGraphic>();
                graphic.raycastTarget = false;
                graphic.maskable = hungerFill.maskable;
                hungerEffectSegmentGraphics.Add(graphic);
            }

            int firstSibling = hungerFill.transform.GetSiblingIndex() + 1;
            for (int i = 0; i < hungerEffectSegmentGraphics.Count; i++)
            {
                KMSHungerSegmentGraphic graphic = hungerEffectSegmentGraphics[i];
                if (graphic != null)
                {
                    graphic.transform.SetSiblingIndex(
                        Mathf.Min(firstSibling + i, graphic.transform.parent.childCount - 1));
                }
            }
        }

        private Color GetFoodEffectColor(KMSFoodEffectSegment segment)
        {
            return segment != null && segment.GetEffectTotal(EffectType.Speed) > 0f
                ? speedFoodEffectColor
                : otherFoodEffectColor;
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
