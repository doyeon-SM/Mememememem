using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace KMS
{
    public class PlayerHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStats stats;
        [SerializeField] private UIDocument uiDocument;

        [Header("UI Element Names")]
        [SerializeField] private string healthBarName = "player-health-bar";
        [FormerlySerializedAs("staminaBarName")]
        [SerializeField] private string hungerBarName = "player-stamina-bar";
        [SerializeField] private string messageOverlayName = "message-overlay";
        [SerializeField] private string messageLabelName = "message-label";
        [SerializeField] private string notificationContainerName = "notification-container";
        [SerializeField] private string throwGuideName = "throw-guide";
        [SerializeField] private string survivalStatusContainerName = "health-info-container";
        [SerializeField] private string inventoryButtonName = "inventory-button";

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
        private KMS.InventoryDuped.InventoryUI inventoryUi;
        private bool isSurvivalStatusVisible = true;

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
            if (stats != null)
            {
                stats.HealthChanged += HandleHealthChanged;
                stats.HungerChanged += HandleHungerChanged;
                stats.Died += HandleDied;
                stats.Revived += HandleRevived;
            }
        }

        private void Start()
        {
            BindElements();
            Refresh();
        }

        private void OnDisable()
        {
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

        private void BindElements()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            VisualElement root = uiDocument.rootVisualElement;
            if (inventoryButton != null)
            {
                inventoryButton.clicked -= HandleInventoryButtonClicked;
            }

            healthBar = root.Q<ProgressBar>(healthBarName);
            hungerBar = root.Q<ProgressBar>(hungerBarName);
            messageOverlay = root.Q<VisualElement>(messageOverlayName);
            messageLabel = root.Q<Label>(messageLabelName);
            notificationContainer = root.Q<VisualElement>(notificationContainerName);
            throwGuide = root.Q<VisualElement>(throwGuideName);
            survivalStatusContainer = root.Q<VisualElement>(survivalStatusContainerName);
            inventoryButton = root.Q<Button>(inventoryButtonName);

            if (inventoryButton != null)
            {
                inventoryButton.clicked += HandleInventoryButtonClicked;
            }

            if (survivalStatusContainer != null)
            {
                survivalStatusContainer.style.display = isSurvivalStatusVisible ? DisplayStyle.Flex : DisplayStyle.None;
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
