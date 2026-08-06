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
        [Header("Celestial Orbit")]
        [Tooltip("Assigned clocks use the orbit presentation. Unassigned legacy HUDs keep the old phase-fill presentation.")]
        [SerializeField] private RectTransform celestialOrbit;
        [SerializeField, Min(0f)] private float orbitRadius = 32f;
        [SerializeField, Range(-180f, 180f)] private float dayStartAngle = 25f;
        [SerializeField] private bool clockwise;
        [SerializeField] private bool keepIconsUpright = true;
        [Header("Editor Time Preview")]
        [Tooltip("Overrides the clock only inside the Unity Editor so the orbit can be inspected without waiting.")]
        [SerializeField] private bool previewTimeInEditor;
        [Tooltip("Scrub the complete 20-minute day/night cycle. 0 and 20 represent the same cycle boundary.")]
        [SerializeField, Range(0f, 20f)] private float previewMinute;
        [SerializeField] private GameTimeManager gameTimeManager;
        [SerializeField, Min(1f)] private float collapsedWidth = 250f;
        [SerializeField, Min(1f)] private float expandedWidth = 360f;
        [SerializeField, Min(0.01f)] private float transitionDuration = 0.16f;

        private bool isHovered;
        private float expansion;
        private KMSCelestialOrbitGraphic celestialOrbitGraphic;

        private void Awake()
        {
            if (root == null) root = transform as RectTransform;
            ResolveTimeManager();
            PrepareOrbit();
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
#if UNITY_EDITOR
            if (previewTimeInEditor)
                timeOfDay = Mathf.Repeat(previewMinute * 60f, fullCycle);
#endif
            bool isDay = timeOfDay < halfCycle;
            float halfTime = Mathf.Repeat(timeOfDay, halfCycle);

            int totalSeconds = Mathf.Clamp(Mathf.FloorToInt(halfTime), 0, 599);
            if (gameTimeText != null)
                gameTimeText.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";

            if (celestialOrbit != null)
            {
                ApplyOrbit(timeOfDay / fullCycle);
            }
            else
            {
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
        }

        private void PrepareOrbit()
        {
            if (celestialOrbit == null) return;

            celestialOrbitGraphic = celestialOrbit.GetComponent<KMSCelestialOrbitGraphic>();
            if (celestialOrbitGraphic != null)
                celestialOrbitGraphic.SetDayCenterAngle(dayStartAngle);

            PositionIconOnOrbit(sunIcon, dayStartAngle);
            PositionIconOnOrbit(moonIcon, dayStartAngle + 180f);
            ApplyOrbit(0f);
        }

        private void PositionIconOnOrbit(GameObject icon, float angleDegrees)
        {
            if (icon == null || !(icon.transform is RectTransform iconRect)) return;

            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            iconRect.anchoredPosition = new Vector2(
                Mathf.Cos(angleRadians),
                Mathf.Sin(angleRadians)) * orbitRadius;
        }

        private void ApplyOrbit(float normalizedProgress)
        {
            float direction = clockwise ? -1f : 1f;
            float orbitAngle = Mathf.Repeat(normalizedProgress, 1f) * 360f * direction;
            celestialOrbit.localRotation = Quaternion.Euler(0f, 0f, orbitAngle);
            if (celestialOrbitGraphic != null)
                celestialOrbitGraphic.SetOrbitAngle(orbitAngle);

            SetOrbitIconPresentation(sunIcon, orbitAngle);
            SetOrbitIconPresentation(moonIcon, orbitAngle);
        }

        private void SetOrbitIconPresentation(GameObject icon, float orbitAngle)
        {
            if (icon == null) return;

            // Both bodies remain on the same orbit. Counter-rotation prevents their
            // artwork from tilting while the parent transform revolves.
            if (!icon.activeSelf) icon.SetActive(true);
            if (keepIconsUpright)
                icon.transform.localRotation = Quaternion.Euler(0f, 0f, -orbitAngle);
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            orbitRadius = Mathf.Max(0f, orbitRadius);
            previewMinute = Mathf.Clamp(previewMinute, 0f, 20f);

            if (celestialOrbit == null) return;

            PrepareOrbit();
            if (previewTimeInEditor)
            {
                ApplyOrbit(Mathf.Repeat(previewMinute / 20f, 1f));

                int halfCycleSeconds = 10 * 60;
                int totalSeconds = Mathf.FloorToInt(
                    Mathf.Repeat(previewMinute * 60f, halfCycleSeconds));
                if (gameTimeText != null)
                    gameTimeText.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
            }
        }
#endif
    }
}
