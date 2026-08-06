using System;
using System.Collections;
using System.Collections.Generic;
using HDY.Item;
using HDY.Territory;
using KMS.InventoryDuped;
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
        [SerializeField] private KMSFoodEffectController foodEffects;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private KMSPlayerHudView hudView;
        [SerializeField] private PlayerInventory inventory;

        [Header("Legacy UI Toolkit (0714)")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private string healthBarName = "player-health-bar";
        [SerializeField] private string hungerBarName = "player-hunger-bar";
        [SerializeField] private string messageOverlayName = "message-overlay";
        [SerializeField] private string messageLabelName = "message-label";
        [SerializeField] private string respawnButtonName = "respawn-button";
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

        [Header("Item Obtained Toasts")]
        [Tooltip("각 아이템 획득 메시지가 완전히 보이는 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float itemObtainedToastDuration = 2.5f;
        [Tooltip("메시지가 나타나고 사라질 때 사용하는 페이드 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float itemObtainedToastFadeDuration = 0.3f;
        [Tooltip("우하단에 동시에 쌓일 수 있는 아이템 획득 메시지의 최대 개수입니다.")]
        [SerializeField, Min(1)] private int maxVisibleItemObtainedToasts = 4;

        private KMSPlayerHudView boundHudView;
        private ToolkitProgressBar toolkitHealthBar;
        private ToolkitProgressBar toolkitHungerBar;
        private VisualElement toolkitMessageOverlay;
        private ToolkitLabel toolkitMessageLabel;
        private ToolkitButton toolkitRespawnButton;
        private VisualElement toolkitNotificationContainer;
        private VisualElement toolkitThrowGuide;
        private VisualElement toolkitSurvivalStatus;
        private ToolkitButton toolkitInventoryButton;
        private ToolkitButton toolkitMapButton;
        private ToolkitLabel toolkitRealTimeLabel;
        private ToolkitLabel toolkitGoldLabel;
        private VisualElement toolkitItemObtainedContainer;

        private KMS.InventoryDuped.InventoryUI inventoryUi;
        private WayPointUIToggle mapUiToggle;
        private bool disabledLegacyMapToggleInput;
        private bool isSurvivalStatusVisible = true;
        private bool hasStarted;
        private Coroutine statusTextCoroutine;
        private string lastDisplayedTime;
        private int lastDisplayedGold = int.MinValue;
        private bool hasDisplayedGold;
        private readonly List<ToolkitItemObtainedToast> toolkitItemObtainedToasts = new List<ToolkitItemObtainedToast>();

        private sealed class ToolkitItemObtainedToast
        {
            public VisualElement root;
            public Coroutine lifetimeRoutine;
        }

        public bool UsesToolkitHud => uiDocument != null && uiDocument.enabled;
        public event Action RespawnRequested;

        private void Reset()
        {
            stats = GetComponent<PlayerStats>();
            foodEffects = GetComponent<KMSFoodEffectController>();
            playerInput = GetComponent<PlayerInput>();
            inventory = GetComponent<PlayerInventory>();
            uiDocument = GetComponent<UIDocument>();
        }

        private void Awake()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (foodEffects == null)
                foodEffects = stats != null ? stats.FoodEffects : GetComponent<KMSFoodEffectController>();
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
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
            if (foodEffects != null) foodEffects.Changed += HandleFoodEffectsChanged;

            if (playerInput != null) playerInput.MapPressed += HandleMapPressed;
            if (inventory != null) inventory.OnItemObtained += HandleItemObtained;

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
            if (inventory != null) inventory.OnItemObtained -= HandleItemObtained;
            RestoreLegacyMapToggleInput();

            if (stats != null)
            {
                stats.HealthChanged -= HandleHealthChanged;
                stats.HungerChanged -= HandleHungerChanged;
                stats.Died -= HandleDied;
                stats.Revived -= HandleRevived;
            }
            if (foodEffects != null) foodEffects.Changed -= HandleFoodEffectsChanged;
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

            // Temporarily disabled so the HUD buttons can be tested with Inspector-assigned OnClick events.
            // if (boundHudView.InventoryButton != null)
            //     boundHudView.InventoryButton.onClick.AddListener(HandleInventoryButtonClicked);
            // if (boundHudView.MapButton != null)
            //     boundHudView.MapButton.onClick.AddListener(HandleMapButtonClicked);
            if (boundHudView.RespawnButton != null)
                boundHudView.RespawnButton.onClick.AddListener(HandleRespawnButtonClicked);
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
                if (boundHudView.RespawnButton != null)
                    boundHudView.RespawnButton.onClick.RemoveListener(HandleRespawnButtonClicked);
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
            toolkitRespawnButton = UnityEngine.UIElements.UQueryExtensions.Q<ToolkitButton>(root, respawnButtonName);
            toolkitNotificationContainer = UnityEngine.UIElements.UQueryExtensions.Q<VisualElement>(root, notificationContainerName);
            toolkitThrowGuide = UnityEngine.UIElements.UQueryExtensions.Q<VisualElement>(root, throwGuideName);
            toolkitSurvivalStatus = UnityEngine.UIElements.UQueryExtensions.Q<VisualElement>(root, survivalStatusContainerName);
            toolkitInventoryButton = UnityEngine.UIElements.UQueryExtensions.Q<ToolkitButton>(root, inventoryButtonName);
            toolkitMapButton = UnityEngine.UIElements.UQueryExtensions.Q<ToolkitButton>(root, mapButtonName);
            toolkitRealTimeLabel = UnityEngine.UIElements.UQueryExtensions.Q<ToolkitLabel>(root, realTimeLabelName);
            toolkitGoldLabel = UnityEngine.UIElements.UQueryExtensions.Q<ToolkitLabel>(root, goldLabelName);
            EnsureToolkitItemObtainedContainer(root);

            // Temporarily disabled while testing non-runtime-bound HUD buttons.
            // if (toolkitInventoryButton != null) toolkitInventoryButton.clicked += HandleInventoryButtonClicked;
            // if (toolkitMapButton != null) toolkitMapButton.clicked += HandleMapButtonClicked;
            if (toolkitRespawnButton != null) toolkitRespawnButton.clicked += HandleRespawnButtonClicked;
            if (toolkitSurvivalStatus != null)
                toolkitSurvivalStatus.style.display = isSurvivalStatusVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UnbindToolkitElements()
        {
            ClearToolkitItemObtainedToasts();
            if (toolkitInventoryButton != null) toolkitInventoryButton.clicked -= HandleInventoryButtonClicked;
            if (toolkitMapButton != null) toolkitMapButton.clicked -= HandleMapButtonClicked;
            if (toolkitRespawnButton != null) toolkitRespawnButton.clicked -= HandleRespawnButtonClicked;
            toolkitHealthBar = null;
            toolkitHungerBar = null;
            toolkitMessageOverlay = null;
            toolkitMessageLabel = null;
            toolkitRespawnButton = null;
            toolkitNotificationContainer = null;
            toolkitThrowGuide = null;
            toolkitSurvivalStatus = null;
            toolkitInventoryButton = null;
            toolkitMapButton = null;
            toolkitRealTimeLabel = null;
            toolkitGoldLabel = null;
            if (toolkitItemObtainedContainer != null && toolkitItemObtainedContainer.parent != null)
                toolkitItemObtainedContainer.parent.Remove(toolkitItemObtainedContainer);
            toolkitItemObtainedContainer = null;
        }

        private void HandleItemObtained(ItemData item, int amount)
        {
            if (item == null || amount <= 0) return;

            if (UsesToolkitHud)
            {
                if (toolkitItemObtainedContainer == null) BindToolkitElements();
                ShowToolkitItemObtained(item, amount);
                return;
            }

            ResolveHudView();
            hudView?.ShowItemObtained(
                item,
                amount,
                itemObtainedToastDuration,
                itemObtainedToastFadeDuration,
                maxVisibleItemObtainedToasts);
        }

        private void EnsureToolkitItemObtainedContainer(VisualElement root)
        {
            if (toolkitItemObtainedContainer != null || root == null) return;

            toolkitItemObtainedContainer = new VisualElement { name = "kms-item-obtained-container" };
            toolkitItemObtainedContainer.pickingMode = UnityEngine.UIElements.PickingMode.Ignore;
            toolkitItemObtainedContainer.style.position = UnityEngine.UIElements.Position.Absolute;
            toolkitItemObtainedContainer.style.right = 20f;
            toolkitItemObtainedContainer.style.bottom = 142f;
            toolkitItemObtainedContainer.style.width = 340f;
            toolkitItemObtainedContainer.style.maxHeight = 280f;
            toolkitItemObtainedContainer.style.flexDirection = UnityEngine.UIElements.FlexDirection.Column;
            toolkitItemObtainedContainer.style.justifyContent = UnityEngine.UIElements.Justify.FlexEnd;
            root.Add(toolkitItemObtainedContainer);
        }

        private void ShowToolkitItemObtained(ItemData item, int amount)
        {
            if (toolkitItemObtainedContainer == null) return;

            toolkitItemObtainedToasts.RemoveAll(toast => toast == null || toast.root == null || toast.root.parent == null);
            string itemId = string.IsNullOrEmpty(item.Item_ID) ? item.GetInstanceID().ToString() : item.Item_ID;
            while (toolkitItemObtainedToasts.Count >= Mathf.Max(1, maxVisibleItemObtainedToasts))
            {
                RemoveToolkitItemObtainedToast(toolkitItemObtainedToasts[0]);
            }

            VisualElement row = new VisualElement { name = $"item-obtained-{itemId}" };
            row.pickingMode = UnityEngine.UIElements.PickingMode.Ignore;
            row.style.height = 64f;
            row.style.minHeight = 60f;
            row.style.marginTop = 4f;
            row.style.paddingLeft = 10f;
            row.style.paddingRight = 12f;
            row.style.paddingTop = 8f;
            row.style.paddingBottom = 8f;
            row.style.flexDirection = UnityEngine.UIElements.FlexDirection.Row;
            row.style.alignItems = UnityEngine.UIElements.Align.Center;
            row.style.backgroundColor = new Color(22f / 255f, 22f / 255f, 24f / 255f, 235f / 255f);
            row.style.borderTopLeftRadius = 8f;
            row.style.borderTopRightRadius = 8f;
            row.style.borderBottomLeftRadius = 8f;
            row.style.borderBottomRightRadius = 8f;
            row.style.opacity = 0f;

            VisualElement icon = new VisualElement { name = "icon" };
            icon.pickingMode = UnityEngine.UIElements.PickingMode.Ignore;
            icon.style.width = 44f;
            icon.style.height = 44f;
            icon.style.minWidth = 44f;
            icon.style.marginRight = 10f;
            icon.style.backgroundColor = item.ItemIcon != null
                ? Color.clear
                : new Color(60f / 255f, 60f / 255f, 64f / 255f, 1f);
            if (item.ItemIcon != null)
            {
                icon.style.backgroundImage = new UnityEngine.UIElements.StyleBackground(item.ItemIcon);
                icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            else
            {
                ToolkitLabel placeholder = new ToolkitLabel("?");
                placeholder.pickingMode = UnityEngine.UIElements.PickingMode.Ignore;
                placeholder.style.unityTextAlign = TextAnchor.MiddleCenter;
                placeholder.style.fontSize = 20f;
                placeholder.style.color = Color.white;
                placeholder.style.flexGrow = 1f;
                icon.Add(placeholder);
            }

            ToolkitLabel nameLabel = new ToolkitLabel(!string.IsNullOrEmpty(item.ItemName) ? item.ItemName : item.Item_ID);
            nameLabel.pickingMode = UnityEngine.UIElements.PickingMode.Ignore;
            nameLabel.style.flexGrow = 1f;
            nameLabel.style.fontSize = 18f;
            nameLabel.style.color = new Color(240f / 255f, 240f / 255f, 240f / 255f, 1f);
            nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            ToolkitLabel amountLabel = new ToolkitLabel($"X{amount}");
            amountLabel.pickingMode = UnityEngine.UIElements.PickingMode.Ignore;
            amountLabel.style.width = 54f;
            amountLabel.style.fontSize = 18f;
            amountLabel.style.color = new Color(1f, 222f / 255f, 120f / 255f, 1f);
            amountLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            amountLabel.style.unityTextAlign = TextAnchor.MiddleRight;

            row.Add(icon);
            row.Add(nameLabel);
            row.Add(amountLabel);
            toolkitItemObtainedContainer.Add(row);

            ToolkitItemObtainedToast toast = new ToolkitItemObtainedToast { root = row };
            toolkitItemObtainedToasts.Add(toast);
            toast.lifetimeRoutine = StartCoroutine(RunToolkitItemObtainedLifetime(toast));
        }

        private IEnumerator RunToolkitItemObtainedLifetime(ToolkitItemObtainedToast toast)
        {
            float elapsed = 0f;
            float fadeInDuration = Mathf.Min(0.16f, Mathf.Max(0f, itemObtainedToastFadeDuration));
            toast.root.style.opacity = fadeInDuration > 0f ? 0f : 1f;
            while (elapsed < fadeInDuration && toast.root != null && toast.root.parent != null)
            {
                elapsed += Time.unscaledDeltaTime;
                toast.root.style.opacity = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, itemObtainedToastDuration));
            if (toast.root == null || toast.root.parent == null) yield break;

            elapsed = 0f;
            float fadeOutDuration = Mathf.Max(0f, itemObtainedToastFadeDuration);
            while (elapsed < fadeOutDuration && toast.root.parent != null)
            {
                elapsed += Time.unscaledDeltaTime;
                toast.root.style.opacity = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
                yield return null;
            }
            RemoveToolkitItemObtainedToast(toast, false);
        }

        private void RemoveToolkitItemObtainedToast(ToolkitItemObtainedToast toast, bool stopRoutine = true)
        {
            if (toast == null) return;
            if (stopRoutine && toast.lifetimeRoutine != null) StopCoroutine(toast.lifetimeRoutine);
            toolkitItemObtainedToasts.Remove(toast);
            if (toast.root != null && toast.root.parent != null) toast.root.parent.Remove(toast.root);
        }

        private void ClearToolkitItemObtainedToasts()
        {
            for (int i = toolkitItemObtainedToasts.Count - 1; i >= 0; i--)
            {
                RemoveToolkitItemObtainedToast(toolkitItemObtainedToasts[i]);
            }
        }

        private void HandleInventoryButtonClicked()
        {
            if (stats != null && !stats.IsAlive) return;
            if (inventoryUi == null) inventoryUi = FindFirstObjectByType<KMS.InventoryDuped.InventoryUI>();
            inventoryUi?.Toggle();
        }

        private void HandleMapPressed()
        {
            if (stats == null || stats.IsAlive)
                SceneUIManager.TryToggleManagedUI("Map");
        }

        private void HandleMapButtonClicked()
        {
            if (stats == null || stats.IsAlive) OpenPreviewMap();
        }

        private void HandleRespawnButtonClicked() => RespawnRequested?.Invoke();

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
            string currentTime = gameTimeManager != null
                ? gameTimeManager.GetRealTimeText()
                : $"{DateTime.Now.Hour:00}시 {DateTime.Now.Minute:00}분";
            if (currentTime != lastDisplayedTime)
            {
                lastDisplayedTime = currentTime;
                if (UsesToolkitHud)
                {
                    if (toolkitRealTimeLabel != null) toolkitRealTimeLabel.text = currentTime;
                }
                else
                {
                    ResolveHudView();
                    hudView?.SetRealTime(currentTime);
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
            string value = $"{gold}";
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
                hudView?.SetHunger(current, max, foodEffects);
            }
        }

        private void HandleFoodEffectsChanged()
        {
            if (stats == null) return;
            HandleHungerChanged(stats.CurrentHunger, stats.MaxHunger);
        }

        private void HandleDied()
        {
            if (UsesToolkitHud)
            {
                if (toolkitMessageOverlay != null)
                {
                    toolkitMessageOverlay.style.display = DisplayStyle.Flex;
                    toolkitMessageOverlay.BringToFront();
                }
                toolkitRespawnButton?.SetEnabled(true);
                if (toolkitMessageLabel != null) toolkitMessageLabel.text = "사망했습니다";
            }
            else
            {
                ResolveHudView();
                hudView?.SetDefeatOverlayVisible(true, "사망했습니다");
            }
        }

        private void HandleRevived()
        {
            if (UsesToolkitHud)
            {
                toolkitRespawnButton?.SetEnabled(false);
                if (toolkitMessageOverlay != null) toolkitMessageOverlay.style.display = DisplayStyle.None;
                if (toolkitMessageLabel != null) toolkitMessageLabel.text = string.Empty;
            }
            else
            {
                ResolveHudView();
                hudView?.SetDefeatOverlayVisible(false, string.Empty);
            }
            ShowNotification("리스폰했습니다.");
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
