using System;
using System.Globalization;
using HDY.Territory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KMS
{
    /// <summary>
    /// Exploration-only clock. The compact state shows KST, while hover reveals
    /// the elapsed time inside the current ten-minute day/night half-cycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KMSExplorationClockView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private TMP_Text realTimeText;
        [SerializeField] private TMP_Text gameTimeText;
        [SerializeField] private CanvasGroup gameTimeGroup;
        [SerializeField] private GameObject sunIcon;
        [SerializeField] private GameObject moonIcon;
        [SerializeField] private RectTransform phaseFill;
        [SerializeField] private GameTimeManager gameTimeManager;
        [SerializeField, Min(1f)] private float collapsedWidth = 250f;
        [SerializeField, Min(1f)] private float expandedWidth = 360f;
        [SerializeField, Min(0.01f)] private float transitionDuration = 0.16f;

        private bool isHovered;
        private float expansion;

        private void Awake()
        {
            if (root == null) root = transform as RectTransform;
            ResolveTimeManager();
            SetPresentation(0f);
        }

        private void Start()
        {
            KMSGameClockUI[] standaloneClocks =
                FindObjectsByType<KMSGameClockUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < standaloneClocks.Length; i++)
            {
                KMSGameClockUI standaloneClock = standaloneClocks[i];
                if (standaloneClock != null && standaloneClock.gameObject != gameObject)
                    standaloneClock.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            ResolveTimeManager();
            RefreshTime();

            float target = isHovered ? 1f : 0f;
            expansion = Mathf.MoveTowards(expansion, target, Time.unscaledDeltaTime / transitionDuration);
            SetPresentation(expansion);
        }

        public void OnPointerEnter(PointerEventData eventData) => isHovered = true;

        public void OnPointerExit(PointerEventData eventData) => isHovered = false;

        private void ResolveTimeManager()
        {
            if (gameTimeManager == null)
                gameTimeManager = FindFirstObjectByType<GameTimeManager>();
        }

        private void RefreshTime()
        {
            if (gameTimeManager == null) return;

            DateTime kst = gameTimeManager.CurrentRealTimeKst;
            if (realTimeText != null)
                realTimeText.text = kst.ToString("tt hh:mm", CultureInfo.InvariantCulture);

            float fullCycle = Mathf.Max(0.01f, gameTimeManager.DayLengthSeconds);
            float halfCycle = fullCycle * 0.5f;
            float timeOfDay = Mathf.Repeat(gameTimeManager.InGameTimeOfDaySeconds, fullCycle);
            bool isDay = timeOfDay < halfCycle;
            float halfTime = Mathf.Repeat(timeOfDay, halfCycle);

            int totalSeconds = Mathf.Clamp(Mathf.FloorToInt(halfTime), 0, 599);
            if (gameTimeText != null)
                gameTimeText.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";

            if (sunIcon != null) sunIcon.SetActive(isDay);
            if (moonIcon != null) moonIcon.SetActive(!isDay);

            if (phaseFill != null)
            {
                Vector2 anchors = phaseFill.anchorMax;
                anchors.y = Mathf.Clamp01(halfTime / halfCycle);
                phaseFill.anchorMax = anchors;
                phaseFill.anchoredPosition = Vector2.zero;
                phaseFill.sizeDelta = Vector2.zero;
            }
        }

        private void SetPresentation(float value)
        {
            if (root != null)
                root.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    Mathf.Lerp(collapsedWidth, expandedWidth, value));

            if (gameTimeGroup != null)
            {
                gameTimeGroup.alpha = value;
                gameTimeGroup.interactable = value > 0.95f;
                gameTimeGroup.blocksRaycasts = false;
            }
        }
    }
}
