using System.Collections;
using System.Collections.Generic;
using HDY.Item;
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
        [SerializeField] private Color speedFoodEffectColor = new Color32(255, 132, 43, 255);
        [SerializeField] private Color otherFoodEffectColor = new Color32(174, 92, 255, 255);

        [Header("Transient UI")]
        [SerializeField] private RectTransform notificationContainer;
        [SerializeField] private GameObject notificationTemplate;
        [SerializeField] private GameObject throwGuide;
        [SerializeField] private GameObject defeatOverlay;
        [SerializeField] private TMP_Text defeatMessageText;
        [SerializeField] private Button respawnButton;

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

        private readonly List<Image> hungerEffectSegmentImages = new List<Image>();
        private readonly List<ActiveItemObtainedToast> itemObtainedToasts = new List<ActiveItemObtainedToast>();

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
            SetDefeatOverlayVisible(false, string.Empty);
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
            ClearItemObtainedToasts();
        }

        public void SetHealth(float current, float max)
        {
            SetProgress(healthFill, healthText, current, max);
        }

        public void SetHunger(
            float current,
            float max,
            KMSFoodEffectController foodEffects = null)
        {
            SetProgress(hungerFill, hungerText, current, max);
            RenderHungerEffectSegments(foodEffects, max);
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
            if (respawnButton != null) respawnButton.interactable = visible;
            if (defeatOverlay != null)
            {
                defeatOverlay.SetActive(visible);
                if (visible) defeatOverlay.transform.SetAsLastSibling();
            }
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

            float cursor = 0f;
            for (int i = 0; i < hungerEffectSegmentImages.Count; i++)
            {
                Image image = hungerEffectSegmentImages[i];
                if (image == null) continue;

                if (i >= requiredCount || maxHunger <= 0f)
                {
                    image.gameObject.SetActive(false);
                    continue;
                }

                KMSFoodEffectSegment segment = foodEffects.FoodSegments[i];
                if (segment == null || segment.RemainingSatiety <= 0f)
                {
                    image.gameObject.SetActive(false);
                    continue;
                }

                float start = Mathf.Clamp01(cursor / maxHunger);
                cursor += segment.RemainingSatiety;
                float end = Mathf.Clamp01(cursor / maxHunger);

                if (segment.Effects.Count == 0)
                {
                    // 효과 없는(포만감만) 세그먼트: cursor는 이미 전진했으므로 뒤 세그먼트 위치는 정확하다.
                    image.gameObject.SetActive(false);
                    continue;
                }

                RectTransform rect = image.rectTransform;
                rect.anchorMin = new Vector2(start, 0f);
                rect.anchorMax = new Vector2(end, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                image.color = GetFoodEffectColor(segment);
                image.gameObject.SetActive(end > start);
            }
        }

        private void EnsureHungerEffectSegmentCount(int count)
        {
            if (hungerFill == null || hungerFill.rectTransform.parent == null) return;

            while (hungerEffectSegmentImages.Count < count)
            {
                var segmentObject = new GameObject(
                    $"FoodEffectSegment_{hungerEffectSegmentImages.Count}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                segmentObject.layer = hungerFill.gameObject.layer;
                segmentObject.transform.SetParent(hungerFill.rectTransform.parent, false);

                Image image = segmentObject.GetComponent<Image>();
                image.raycastTarget = false;
                image.maskable = hungerFill.maskable;
                image.sprite = null;
                image.type = Image.Type.Simple;
                hungerEffectSegmentImages.Add(image);
            }

            int firstSibling = hungerFill.transform.GetSiblingIndex() + 1;
            for (int i = 0; i < hungerEffectSegmentImages.Count; i++)
            {
                Image image = hungerEffectSegmentImages[i];
                if (image != null)
                {
                    image.transform.SetSiblingIndex(
                        Mathf.Min(firstSibling + i, image.transform.parent.childCount - 1));
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
