using System;
using System.Collections;
using HDY.Territory;
using UnityEngine;
using UnityEngine.SceneManagement;
using ToolkitButton = UnityEngine.UIElements.Button;
using ToolkitLabel = UnityEngine.UIElements.Label;
using ToolkitProgressBar = UnityEngine.UIElements.ProgressBar;
using UIDocument = UnityEngine.UIElements.UIDocument;
using VisualElement = UnityEngine.UIElements.VisualElement;
using DisplayStyle = UnityEngine.UIElements.DisplayStyle;

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
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private KMSPlayerHudView hudView;

        [Header("Legacy UI Toolkit (0714)")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private string healthBarName = "player-health-bar";
        [SerializeField] private string hungerBarName = "player-hunger-bar";
        [SerializeField] private string messageOverlayName = "message-overlay";
        [SerializeField] private string messageLabelName = "message-label";
        [SerializeField] private string notificationContainerName = "notification-container";
        [SerializeField] private string throwGuideName = "throw-guide";
        [SerializeField] private string survivalStatusContainerName = "health-info-container";
        [SerializeField] private string inventoryButtonName = "inventory-button";
        [SerializeField] private string mapButtonName = "map-button";
        [SerializeField] private string realTimeLabelName = "real-time-label";
        [SerializeField] private string goldLabelName = "gold-label";

        [Header("Status Text")]
        [SerializeField] private TerritoryData territoryData;
        [SerializeField] private GameTimeManager gameTimeManager;
        [SerializeField, Min(0.1f)] private float statusRefreshInterval = 0.25f;

        [Header("Notifications")]
        [SerializeField] private float notificationDuration = 2.5f;

        private KMSPlayerHudView boundHudView;
        private ToolkitProgressBar toolkitHealthBar;
        private ToolkitProgressBar toolkitHungerBar;
        private VisualElement toolkitMessageOverlay;
        private ToolkitLabel toolkitMessageLabel;
        private VisualElement toolkitNotificationContainer;
        private VisualElement toolkitThrowGuide;
        private VisualElement toolkitSurvivalStatus;
        private ToolkitButton toolkitInventoryButton;
        private ToolkitButton toolkitMapButton;
        private ToolkitLabel toolkitRealTimeLabel;
        private ToolkitLabel toolkitGoldLabel;

        private KMS.InventoryDuped.InventoryUI inventoryUi;
        private WayPointUIToggle mapUiToggle;
        private bool disabledLegacyMapToggleInput;
        private bool isSurvivalStatusVisible = true;
        private bool hasStarted;
        private Coroutine statusTextCoroutine;
        private string lastDisplayedTime;
        private int lastDisplayedGold = int.MinValue;
        private bool hasDisplayedGold;

        public bool UsesToolkitHud => uiDocument != null && uiDocument.enabled;

        private void Reset()
        {
            stats = GetComponent<PlayerStats>();
            playerInput = GetComponent<PlayerInput>();
            uiDocument = GetComponent<UIDocument>();
        }

        private void Awake()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (inventoryUi == null) inventoryUi = FindFirstObjectByType<KMS.InventoryDuped.InventoryUI>();
            ResolveHudView();
            EnsureGameTimeManager();
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

            if (playerInput != null) playerInput.MapPressed += HandleMapPressed;

            BindPresentation();
            if (hasStarted)
            {
                Refresh();
                StartStatusTextUpdates();
            }
        }

        private void Start()
        {
            BindPresentation();
            Refresh();
            hasStarted = true;
            StartStatusTextUpdates();
        }

        private void OnDisable()
        {
            if (territoryData != null) cachedSessionGold = territoryData.Gold;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

            if (statusTextCoroutine != null)
            {
                StopCoroutine(statusTextCoroutine);
                statusTextCoroutine = null;
            }

            UnbindPresentation();
            if (playerInput != null) playerInput.MapPressed -= HandleMapPressed;
            RestoreLegacyMapToggleInput();

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
            if (UsesToolkitHud)
            {
                if (toolkitNotificationContainer == null) BindToolkitElements();
                if (toolkitNotificationContainer == null) return;

                ToolkitLabel label = new ToolkitLabel(message);
                label.AddToClassList("notification");
                toolkitNotificationContainer.Add(label);
                StartCoroutine(RemoveToolkitNotificationAfterDelay(label));
                return;
            }

            ResolveHudView();
            hudView?.ShowNotification(message, notificationDuration);
        }

        public void SetThrowGuideVisible(bool visible)
        {
            if (UsesToolkitHud)
            {
                if (toolkitThrowGuide == null) BindToolkitElements();
                if (toolkitThrowGuide != null)
                    toolkitThrowGuide.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                return;
            }

            ResolveHudView();
            hudView?.SetThrowGuideVisible(visible);
        }

        public void SetSurvivalStatusVisible(bool visible)
        {
            isSurvivalStatusVisible = visible;
            if (UsesToolkitHud)
            {
                if (toolkitSurvivalStatus == null) BindToolkitElements();
                if (toolkitSurvivalStatus != null)
                    toolkitSurvivalStatus.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                return;
            }

            ResolveHudView();
            hudView?.SetSurvivalStatusVisible(visible);
        }

        private void ResolveHudView()
        {
            if (hudView == null)
                hudView = FindFirstObjectByType<KMSPlayerHudView>(FindObjectsInactive.Include);
        }

        private void BindPresentation()
        {
            UnbindPresentation();
            ResolveHudView();
            lastDisplayedTime = null;
            hasDisplayedGold = false;

            if (UsesToolkitHud)
            {
                if (hudView != null) hudView.gameObject.SetActive(false);
                BindToolkitElements();
                return;
            }

            if (hudView != null) hudView.gameObject.SetActive(true);
            boundHudView = hudView;
            if (boundHudView == null) return;

            if (boundHudView.InventoryButton != null)
                boundHudView.InventoryButton.onClick.AddListener(HandleInventoryButtonClicked);
            if (boundHudView.MapButton != null)
                boundHudView.MapButton.onClick.AddListener(HandleMapButtonClicked);
            boundHudView.SetSurvivalStatusVisible(isSurvivalStatusVisible);
        }

        private void UnbindPresentation()
        {
            if (boundHudView != null)
            {
                if (boundHudView.InventoryButton != null)
                    boundHudView.InventoryButton.onClick.RemoveListener(HandleInventoryButtonClicked);
                if (boundHudView.MapButton != null)
                    boundHudView.MapButton.onClick.RemoveListener(HandleMapButtonClicked);
            }
            boundHudView = null;
            UnbindToolkitElements();
        }

        private void BindToolkitElements()
        {
            if (uiDocument == null || !uiDocument.enabled || uiDocument.rootVisualElement == null) return;
            UnbindToolkitElements();

            VisualElement root = uiDocument.rootVisualElement;
            toolkitHealthBar = UnityEngine.UIElements.UQueryExtensions.Q<ToolkitProgressBar>(root, healthBarName);
            toolkitHungerBar = UnityEngine.UIElements.UQueryExtensions.Q<ToolkitProgressBar>(root, hungerBarName);
            toolkitMessageOverlay = UnityEngine.UIElements.UQueryExtensions.Q<VisualElement>(root, messageOverlayName);
            toolkitMessageLabel = UnityEngine.UIElements.UQueryExtensions.Q<ToolkitLabel>(root, messageLabelName);
            toolkitNotificationContainer = UnityEngine.UIElements.UQueryExtensions.Q<VisualElement>(root, notificationContainerName);
            toolkitThrowGuide = UnityEngine.UIElements.UQueryExtensions.Q<VisualElement>(root, throwGuideName);
            toolkitSurvivalStatus = UnityEngine.UIElements.UQueryExtensions.Q<VisualElement>(root, survivalStatusContainerName);
            toolkitInventoryButton = UnityEngine.UIElements.UQueryExtensions.Q<ToolkitButton>(root, inventoryButtonName);
            toolkitMapButton = UnityEngine.UIElements.UQueryExtensions.Q<ToolkitButton>(root, mapButtonName);
            toolkitRealTimeLabel = UnityEngine.UIElements.UQueryExtensions.Q<ToolkitLabel>(root, realTimeLabelName);
            toolkitGoldLabel = UnityEngine.UIElements.UQueryExtensions.Q<ToolkitLabel>(root, goldLabelName);

            if (toolkitInventoryButton != null) toolkitInventoryButton.clicked += HandleInventoryButtonClicked;
            if (toolkitMapButton != null) toolkitMapButton.clicked += HandleMapButtonClicked;
            if (toolkitSurvivalStatus != null)
                toolkitSurvivalStatus.style.display = isSurvivalStatusVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UnbindToolkitElements()
        {
            if (toolkitInventoryButton != null) toolkitInventoryButton.clicked -= HandleInventoryButtonClicked;
            if (toolkitMapButton != null) toolkitMapButton.clicked -= HandleMapButtonClicked;
            toolkitHealthBar = null;
            toolkitHungerBar = null;
            toolkitMessageOverlay = null;
            toolkitMessageLabel = null;
            toolkitNotificationContainer = null;
            toolkitThrowGuide = null;
            toolkitSurvivalStatus = null;
            toolkitInventoryButton = null;
            toolkitMapButton = null;
            toolkitRealTimeLabel = null;
            toolkitGoldLabel = null;
        }

        private void HandleInventoryButtonClicked()
        {
            if (inventoryUi == null) inventoryUi = FindFirstObjectByType<KMS.InventoryDuped.InventoryUI>();
            inventoryUi?.Toggle();
        }

        private void HandleMapPressed() => TogglePreviewMap();
        private void HandleMapButtonClicked() => OpenPreviewMap();

        private void OpenPreviewMap()
        {
            if (TryResolveMapUiToggle()) mapUiToggle.TogglePreviewMap();
        }

        private void TogglePreviewMap()
        {
            if (TryResolveMapUiToggle()) mapUiToggle.TogglePreviewMap();
        }

        private bool TryResolveMapUiToggle()
        {
            if (WayPointManager.Instance == null)
            {
                Debug.LogWarning("[PlayerHUD] WayPointManager.Instance가 없어 지도를 열 수 없습니다.", this);
                return false;
            }

            if (mapUiToggle == null) mapUiToggle = WayPointManager.Instance.GetComponent<WayPointUIToggle>();
            if (mapUiToggle == null)
                mapUiToggle = FindFirstObjectByType<WayPointUIToggle>(FindObjectsInactive.Include);
            if (mapUiToggle == null)
            {
                Debug.LogWarning("[PlayerHUD] WayPointUIToggle을 찾지 못해 지도를 열 수 없습니다.", this);
                return false;
            }

            if (mapUiToggle.enabled)
            {
                mapUiToggle.enabled = false;
                disabledLegacyMapToggleInput = true;
            }
            return true;
        }

        private void RestoreLegacyMapToggleInput()
        {
            if (!disabledLegacyMapToggleInput || mapUiToggle == null) return;
            mapUiToggle.enabled = true;
            disabledLegacyMapToggleInput = false;
        }

        private void Refresh()
        {
            if (stats == null) return;
            HandleHealthChanged(stats.CurrentHealth, stats.MaxHealth);
            HandleHungerChanged(stats.CurrentHunger, stats.MaxHunger);
        }

        private void StartStatusTextUpdates()
        {
            if (statusTextCoroutine != null) StopCoroutine(statusTextCoroutine);
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
            EnsureGameTimeManager();
            DateTime currentRealTime = gameTimeManager != null ? gameTimeManager.CurrentRealTimeKst : DateTime.Now;
            string currentTime = currentRealTime.ToString("HH:mm:ss");
            if (currentTime != lastDisplayedTime)
            {
                lastDisplayedTime = currentTime;
                string value = $"현재 시간 {currentTime}";
                if (UsesToolkitHud)
                {
                    if (toolkitRealTimeLabel != null) toolkitRealTimeLabel.text = value;
                }
                else
                {
                    ResolveHudView();
                    hudView?.SetRealTime(value);
                }
            }

            if (territoryData == null) territoryData = FindFirstObjectByType<TerritoryData>();
            if (territoryData != null) SynchronizeGoldSource();
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
                if (difference != 0) territoryData.AddGold(difference);
                connectedGoldSourceId = sourceId;
            }
            cachedSessionGold = territoryData.Gold;
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            UnbindPresentation();
            hudView = null;
            inventoryUi = null;
            mapUiToggle = null;
            territoryData = null;
            gameTimeManager = null;
            ResolveHudView();
            BindPresentation();
            EnsureGameTimeManager();

            if (hasStarted)
            {
                Refresh();
                RefreshStatusTexts();
            }
        }

        private void EnsureGameTimeManager()
        {
            if (gameTimeManager != null) return;
            gameTimeManager = FindFirstObjectByType<GameTimeManager>();
            if (gameTimeManager != null) return;
            if (territoryData == null) territoryData = FindFirstObjectByType<TerritoryData>();

            GameObject timeSystemObject;
            if (territoryData != null)
            {
                timeSystemObject = territoryData.gameObject;
            }
            else
            {
                timeSystemObject = new GameObject("KMS Time System");
                territoryData = timeSystemObject.AddComponent<TerritoryData>();
            }

            gameTimeManager = timeSystemObject.GetComponent<GameTimeManager>();
            if (gameTimeManager == null) gameTimeManager = timeSystemObject.AddComponent<GameTimeManager>();
        }

        private void SetGoldText(int gold)
        {
            if (hasDisplayedGold && gold == lastDisplayedGold) return;
            lastDisplayedGold = gold;
            hasDisplayedGold = true;
            string value = $"보유 골드 {gold:N0}";
            if (UsesToolkitHud)
            {
                if (toolkitGoldLabel != null) toolkitGoldLabel.text = value;
            }
            else
            {
                ResolveHudView();
                hudView?.SetGold(value);
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (UsesToolkitHud)
            {
                if (toolkitHealthBar == null) BindToolkitElements();
                SetToolkitProgress(toolkitHealthBar, current, max, "Health");
            }
            else
            {
                ResolveHudView();
                hudView?.SetHealth(current, max);
            }
        }

        private void HandleHungerChanged(float current, float max)
        {
            if (UsesToolkitHud)
            {
                if (toolkitHungerBar == null) BindToolkitElements();
                SetToolkitProgress(toolkitHungerBar, current, max, "Hunger");
            }
            else
            {
                ResolveHudView();
                hudView?.SetHunger(current, max);
            }
        }

        private void HandleDied()
        {
            if (UsesToolkitHud)
            {
                if (toolkitMessageOverlay != null) toolkitMessageOverlay.style.display = DisplayStyle.Flex;
                if (toolkitMessageLabel != null) toolkitMessageLabel.text = "Defeated";
            }
            else
            {
                ResolveHudView();
                hudView?.SetDefeatOverlayVisible(true, "Defeated");
            }
            ShowNotification("You were defeated.");
        }

        private void HandleRevived()
        {
            if (UsesToolkitHud)
            {
                if (toolkitMessageOverlay != null) toolkitMessageOverlay.style.display = DisplayStyle.None;
                if (toolkitMessageLabel != null) toolkitMessageLabel.text = string.Empty;
            }
            else
            {
                ResolveHudView();
                hudView?.SetDefeatOverlayVisible(false, string.Empty);
            }
            ShowNotification("Revived.");
        }

        private static void SetToolkitProgress(ToolkitProgressBar bar, float current, float max, string label)
        {
            if (bar == null) return;
            float percent = max > 0f ? current / max : 0f;
            bar.value = Mathf.Clamp01(percent) * 100f;
            bar.title = $"{label} {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        private IEnumerator RemoveToolkitNotificationAfterDelay(VisualElement element)
        {
            yield return new WaitForSeconds(notificationDuration);
            if (element != null && element.parent != null) element.parent.Remove(element);
        }
    }
}
