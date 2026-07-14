using System;
using System.Collections;
using HDY.Territory;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace KMS
{
    public class PlayerHUD : MonoBehaviour
    {
        private static int cachedSessionGold;
        private static bool hasConnectedGoldSource;
        private static int connectedGoldSourceId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessionCache()
        {
            cachedSessionGold = 0;
            hasConnectedGoldSource = false;
            connectedGoldSourceId = 0;
        }

        [Header("References")]
        [SerializeField] private PlayerStats stats;
        [SerializeField] private UIDocument uiDocument;

        [Header("UI Element Names")]
        [SerializeField] private string healthBarName = "player-health-bar";
        [SerializeField] private string hungerBarName = "player-hunger-bar";
        [SerializeField] private string messageOverlayName = "message-overlay";
        [SerializeField] private string messageLabelName = "message-label";
        [SerializeField] private string notificationContainerName = "notification-container";
        [SerializeField] private string throwGuideName = "throw-guide";
        [SerializeField] private string survivalStatusContainerName = "health-info-container";
        [SerializeField] private string inventoryButtonName = "inventory-button";

        [Header("Day/Night Clock")]
        [SerializeField] private string clockHandName = "clock-hand";
        [SerializeField] private string clockMarkerName = "clock-marker";
        [SerializeField] private string clockPeriodLabelName = "clock-period-label";
        [SerializeField, Range(0f, 1f)] private float previewCycleProgress = 0.2f;
        [SerializeField] private bool playClockPreview;
        [SerializeField, Min(1f)] private float previewCycleDurationSeconds = 20f;

        [Header("Status Text")]
        [SerializeField] private TerritoryData territoryData;
        [SerializeField] private string realTimeLabelName = "real-time-label";
        [SerializeField] private string goldLabelName = "gold-label";
        [SerializeField, Min(0.1f)] private float statusRefreshInterval = 0.25f;

        [Header("Notifications")]
        [SerializeField] private float notificationDuration = 2.5f;

        private ProgressBar healthBar;
        private ProgressBar hungerBar;
        private VisualElement messageOverlay;
        private Label messageLabel;
        private VisualElement notificationContainer;
        private VisualElement throwGuide;
        private VisualElement survivalStatusContainer;
        private Button inventoryButton;
        private VisualElement clockHand;
        private VisualElement clockMarker;
        private Label clockPeriodLabel;
        private Label realTimeLabel;
        private Label goldLabel;
        private KMS.InventoryDuped.InventoryUI inventoryUi;
        private bool isSurvivalStatusVisible = true;
        private bool hasStarted;
        private Coroutine statusTextCoroutine;
        private string lastDisplayedTime;
        private int lastDisplayedGold = int.MinValue;
        private bool hasDisplayedGold;

        private void Update()
        {
            if (!playClockPreview || clockHand == null) return;

            previewCycleProgress = Mathf.Repeat(
                previewCycleProgress + Time.unscaledDeltaTime / Mathf.Max(1f, previewCycleDurationSeconds),
                1f);
            ApplyDayCycleProgress(previewCycleProgress);
        }

        private void Reset()
        {
            stats = GetComponent<PlayerStats>();
            uiDocument = GetComponent<UIDocument>();
        }

        private void Awake()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (inventoryUi == null) inventoryUi = FindFirstObjectByType<KMS.InventoryDuped.InventoryUI>();
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;

            if (stats != null)
            {
                stats.HealthChanged += HandleHealthChanged;
                stats.HungerChanged += HandleHungerChanged;
                stats.Died += HandleDied;
                stats.Revived += HandleRevived;
            }

            if (hasStarted)
            {
                StartStatusTextUpdates();
            }
        }

        private void Start()
        {
            BindElements();
            Refresh();
            hasStarted = true;
            StartStatusTextUpdates();
        }

        private void OnDisable()
        {
            if (territoryData != null)
            {
                cachedSessionGold = territoryData.Gold;
            }

            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

            if (statusTextCoroutine != null)
            {
                StopCoroutine(statusTextCoroutine);
                statusTextCoroutine = null;
            }

            if (inventoryButton != null)
            {
                inventoryButton.clicked -= HandleInventoryButtonClicked;
            }

            if (stats != null)
            {
                stats.HealthChanged -= HandleHealthChanged;
                stats.HungerChanged -= HandleHungerChanged;
                stats.Died -= HandleDied;
                stats.Revived -= HandleRevived;
            }
        }

        public void ShowNotification(string message)
        {
            if (notificationContainer == null) return;

            Label label = new Label(message);
            label.AddToClassList("notification");
            notificationContainer.Add(label);

            StartCoroutine(RemoveNotificationAfterDelay(label));
        }

        public void SetThrowGuideVisible(bool isVisible)
        {
            if (throwGuide != null)
            {
                throwGuide.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void SetSurvivalStatusVisible(bool isVisible)
        {
            isSurvivalStatusVisible = isVisible;

            if (survivalStatusContainer == null)
            {
                BindElements();
            }

            if (survivalStatusContainer != null)
            {
                survivalStatusContainer.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// Updates the clock from a normalized full-day value: 0-0.5 is day, 0.5-1 is night.
        /// Calling this hands control to the external time system and stops Inspector preview playback.
        /// </summary>
        public void SetDayCycleProgress(float normalizedProgress)
        {
            playClockPreview = false;
            previewCycleProgress = Mathf.Repeat(normalizedProgress, 1f);
            ApplyDayCycleProgress(previewCycleProgress);
        }

        private void BindElements()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            VisualElement root = uiDocument.rootVisualElement;
            if (inventoryButton != null)
            {
                inventoryButton.clicked -= HandleInventoryButtonClicked;
            }

            healthBar = root.Q<ProgressBar>(healthBarName);
            hungerBar = root.Q<ProgressBar>(hungerBarName)
                ?? root.Q<ProgressBar>("player-hunger-bar");
            messageOverlay = root.Q<VisualElement>(messageOverlayName);
            messageLabel = root.Q<Label>(messageLabelName);
            notificationContainer = root.Q<VisualElement>(notificationContainerName);
            throwGuide = root.Q<VisualElement>(throwGuideName);
            survivalStatusContainer = root.Q<VisualElement>(survivalStatusContainerName);
            inventoryButton = root.Q<Button>(inventoryButtonName);
            clockHand = root.Q<VisualElement>(clockHandName);
            clockMarker = root.Q<VisualElement>(clockMarkerName);
            clockPeriodLabel = root.Q<Label>(clockPeriodLabelName);
            realTimeLabel = root.Q<Label>(realTimeLabelName);
            goldLabel = root.Q<Label>(goldLabelName);

            if (inventoryButton != null)
            {
                inventoryButton.clicked += HandleInventoryButtonClicked;
            }

            if (survivalStatusContainer != null)
            {
                survivalStatusContainer.style.display = isSurvivalStatusVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            ApplyDayCycleProgress(previewCycleProgress);
        }

        private void ApplyDayCycleProgress(float normalizedProgress)
        {
            bool isDay = normalizedProgress < 0.5f;

            if (clockHand != null)
            {
                clockHand.style.rotate = new Rotate(new Angle(normalizedProgress * 360f, AngleUnit.Degree));
            }

            if (clockPeriodLabel != null)
            {
                clockPeriodLabel.text = isDay ? "DAY" : "NIGHT";
                clockPeriodLabel.style.color = isDay
                    ? new Color(1f, 0.91f, 0.66f)
                    : new Color(0.68f, 0.78f, 1f);
            }

            if (clockMarker != null)
            {
                clockMarker.EnableInClassList("clock-marker--day", isDay);
                clockMarker.EnableInClassList("clock-marker--night", !isDay);
            }
        }

        private void HandleInventoryButtonClicked()
        {
            if (inventoryUi == null)
            {
                inventoryUi = FindFirstObjectByType<KMS.InventoryDuped.InventoryUI>();
            }

            inventoryUi?.Toggle();
        }

        private void Refresh()
        {
            if (stats == null) return;

            HandleHealthChanged(stats.CurrentHealth, stats.MaxHealth);
            HandleHungerChanged(stats.CurrentHunger, stats.MaxHunger);
        }

        private void StartStatusTextUpdates()
        {
            if (statusTextCoroutine != null)
            {
                StopCoroutine(statusTextCoroutine);
            }

            RefreshStatusTexts();
            statusTextCoroutine = StartCoroutine(RefreshStatusTextsRoutine());
        }

        private IEnumerator RefreshStatusTextsRoutine()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(Mathf.Max(0.1f, statusRefreshInterval));

            while (true)
            {
                yield return wait;
                RefreshStatusTexts();
            }
        }

        private void RefreshStatusTexts()
        {
            string currentTime = DateTime.Now.ToString("HH:mm:ss");
            if (realTimeLabel != null && currentTime != lastDisplayedTime)
            {
                lastDisplayedTime = currentTime;
                realTimeLabel.text = $"현재 시간 {currentTime}";
            }

            if (territoryData == null)
            {
                territoryData = FindFirstObjectByType<TerritoryData>();
            }

            if (territoryData != null)
            {
                SynchronizeGoldSource();
            }

            SetGoldText(cachedSessionGold);
        }

        private void SynchronizeGoldSource()
        {
            int sourceId = territoryData.GetInstanceID();

            if (!hasConnectedGoldSource)
            {
                cachedSessionGold = territoryData.Gold;
                hasConnectedGoldSource = true;
                connectedGoldSourceId = sourceId;
                return;
            }

            if (connectedGoldSourceId != sourceId)
            {
                int difference = cachedSessionGold - territoryData.Gold;
                if (difference != 0)
                {
                    territoryData.AddGold(difference);
                }

                connectedGoldSourceId = sourceId;
            }

            cachedSessionGold = territoryData.Gold;
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            territoryData = null;

            if (hasStarted)
            {
                RefreshStatusTexts();
            }
        }

        private void SetGoldText(int gold)
        {
            if (goldLabel == null || (hasDisplayedGold && gold == lastDisplayedGold)) return;

            lastDisplayedGold = gold;
            hasDisplayedGold = true;
            goldLabel.text = $"보유 골드 {gold:N0}";
        }

        private void HandleHealthChanged(float current, float max)
        {
            SetProgress(healthBar, current, max, "Health");
        }

        private void HandleHungerChanged(float current, float max)
        {
            SetProgress(hungerBar, current, max, "Hunger");
        }

        private void HandleDied()
        {
            if (messageOverlay != null)
            {
                messageOverlay.style.display = DisplayStyle.Flex;
            }

            if (messageLabel != null)
            {
                messageLabel.text = "Defeated";
            }

            ShowNotification("You were defeated.");
        }

        private void HandleRevived()
        {
            if (messageOverlay != null)
            {
                messageOverlay.style.display = DisplayStyle.None;
            }

            if (messageLabel != null)
            {
                messageLabel.text = string.Empty;
            }

            ShowNotification("Revived.");
        }

        private void SetProgress(ProgressBar bar, float current, float max, string label)
        {
            if (bar == null) return;

            float percent = max > 0f ? current / max : 0f;
            bar.value = Mathf.Clamp01(percent) * 100f;
            bar.title = $"{label} {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        private IEnumerator RemoveNotificationAfterDelay(VisualElement element)
        {
            yield return new WaitForSeconds(notificationDuration);

            if (element != null && element.parent != null)
            {
                element.parent.Remove(element);
            }
        }
    }
}
