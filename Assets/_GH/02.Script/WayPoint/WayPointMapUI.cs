using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WayPointMapUI : MonoBehaviour
{
    [Header("Map")]
    [SerializeField] private Image mapImage;
    [SerializeField] private RectTransform iconParent;
    [SerializeField] private WayPointMapIconUI iconPrefab;
    [SerializeField] private Vector2 generatedIconSize = new Vector2(48f, 48f);

    [Header("Close")]
    [SerializeField] private WayPointUIToggle uiToggle;
    [SerializeField] private bool closeAfterTravel = true;

    [Header("Tooltip")]
    [SerializeField] private RectTransform tooltipRoot;
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField] private Vector2 tooltipOffset = new Vector2(18f, -18f);
    [SerializeField] private Vector2 tooltipSize = new Vector2(220f, 72f);
    [SerializeField] private Color tooltipBackgroundColor = new Color(0f, 0f, 0f, 0.82f);
    [SerializeField] private Color canTravelColor = new Color(0.45f, 1f, 0.55f, 1f);
    [SerializeField] private Color cannotTravelColor = new Color(1f, 0.45f, 0.45f, 1f);

    private readonly Dictionary<string, WayPointMapIconUI> iconsById = new Dictionary<string, WayPointMapIconUI>();
    private bool subscribed;
    private WayPointRunTime currentTooltipState;

    private void Awake()
    {
        if (mapImage == null)
        {
            mapImage = GetComponentInChildren<Image>();
        }

        if (iconParent == null && mapImage != null)
        {
            iconParent = mapImage.rectTransform;
        }

        EnsureTooltip();
    }

    private void OnEnable()
    {
        TrySubscribe();
        RebuildIcons();
        RefreshAllIcons();
    }

    private void Start()
    {
        TrySubscribe();
        RebuildIcons();
        RefreshAllIcons();
    }

    private void OnDisable()
    {
        HideTooltip();
        Unsubscribe();
    }

    // Subscribe to waypoint state changes so map icons update immediately.
    private void TrySubscribe()
    {
        if (subscribed || WayPointManager.Instance == null)
        {
            return;
        }

        WayPointManager.Instance.OnWayPointStateChanged += HandleWayPointStateChanged;
        WayPointManager.Instance.OnWayPointUnlocked += HandleWayPointStateChanged;
        subscribed = true;
    }

    // Remove event subscriptions when the map UI is disabled.
    private void Unsubscribe()
    {
        if (!subscribed || WayPointManager.Instance == null)
        {
            subscribed = false;
            return;
        }

        WayPointManager.Instance.OnWayPointStateChanged -= HandleWayPointStateChanged;
        WayPointManager.Instance.OnWayPointUnlocked -= HandleWayPointStateChanged;
        subscribed = false;
    }

    // Create one map icon for each WayPointDefinition registered in the manager.
    public void RebuildIcons()
    {
        if (WayPointManager.Instance == null || iconParent == null)
        {
            return;
        }

        foreach (WayPointRunTime state in WayPointManager.Instance.GetAllStates())
        {
            if (state == null || state.Definition == null || string.IsNullOrWhiteSpace(state.Id))
            {
                continue;
            }

            if (!iconsById.TryGetValue(state.Id, out WayPointMapIconUI icon) || icon == null)
            {
                icon = CreateIcon(state);
                iconsById[state.Id] = icon;
            }

            icon.Initialize(this, state);
            icon.SetMapPosition(state.Definition.mapPosition);
        }
    }

    // Travel to the clicked waypoint and close the map when travel succeeds.
    public void TravelTo(string id)
    {
        if (WayPointManager.Instance == null)
        {
            return;
        }

        bool traveled = WayPointManager.Instance.TryTravel(id);
        if (traveled && closeAfterTravel)
        {
            CloseMap();
        }
    }

    // Refresh every icon sprite from the current unlock state.
    private void RefreshAllIcons()
    {
        foreach (WayPointMapIconUI icon in iconsById.Values)
        {
            if (icon != null)
            {
                icon.Refresh();
            }
        }
    }

    // Refresh only the icon whose waypoint state changed.
    private void HandleWayPointStateChanged(WayPointRunTime state)
    {
        if (state == null)
        {
            return;
        }

        if (iconsById.TryGetValue(state.Id, out WayPointMapIconUI icon) && icon != null)
        {
            icon.Refresh();
            RefreshTooltipIfCurrent(state);
            return;
        }

        RebuildIcons();
    }

    public void ShowTooltip(WayPointRunTime state, Vector2 screenPosition)
    {
        if (state == null || state.Definition == null)
        {
            return;
        }

        EnsureTooltip();
        if (tooltipRoot == null || tooltipText == null)
        {
            return;
        }

        currentTooltipState = state;
        tooltipRoot.gameObject.SetActive(true);
        RefreshTooltipText(state);
        MoveTooltip(screenPosition);
    }

    public void MoveTooltip(Vector2 screenPosition)
    {
        if (tooltipRoot == null || !tooltipRoot.gameObject.activeSelf)
        {
            return;
        }

        RectTransform parent = tooltipRoot.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        Camera eventCamera = null;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = canvas.worldCamera;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, eventCamera, out Vector2 localPoint))
        {
            tooltipRoot.anchoredPosition = localPoint + tooltipOffset;
        }
    }

    public void HideTooltip()
    {
        currentTooltipState = null;

        if (tooltipRoot != null)
        {
            tooltipRoot.gameObject.SetActive(false);
        }
    }

    // Use a prefab when assigned; otherwise create an Image/Button icon at runtime.
    private WayPointMapIconUI CreateIcon(WayPointRunTime state)
    {
        WayPointMapIconUI icon;

        if (iconPrefab != null)
        {
            icon = Instantiate(iconPrefab, iconParent);
        }
        else
        {
            GameObject iconObject = new GameObject($"WayPointIcon_{state.Id}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(WayPointMapIconUI));
            iconObject.transform.SetParent(iconParent, false);

            RectTransform rectTransform = iconObject.transform as RectTransform;
            rectTransform.sizeDelta = generatedIconSize;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            icon = iconObject.GetComponent<WayPointMapIconUI>();
        }

        icon.name = $"WayPointIcon_{state.Id}";
        return icon;
    }

    private void EnsureTooltip()
    {
        if (tooltipRoot != null && tooltipText != null)
        {
            tooltipRoot.gameObject.SetActive(false);
            return;
        }

        RectTransform parent = iconParent != null ? iconParent : transform as RectTransform;
        if (parent == null)
        {
            return;
        }

        if (tooltipRoot == null)
        {
            GameObject tooltipObject = new GameObject("WayPointTooltip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            tooltipObject.transform.SetParent(parent, false);

            tooltipRoot = tooltipObject.transform as RectTransform;
            tooltipRoot.sizeDelta = tooltipSize;
            tooltipRoot.anchorMin = new Vector2(0.5f, 0.5f);
            tooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
            tooltipRoot.pivot = new Vector2(0f, 1f);

            Image background = tooltipObject.GetComponent<Image>();
            background.color = tooltipBackgroundColor;
            background.raycastTarget = false;

            CanvasGroup canvasGroup = tooltipObject.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (tooltipText == null)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(tooltipRoot, false);

            RectTransform textRect = textObject.transform as RectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);

            tooltipText = textObject.GetComponent<TMP_Text>();
            tooltipText.fontSize = 18f;
            tooltipText.enableAutoSizing = true;
            tooltipText.fontSizeMin = 12f;
            tooltipText.fontSizeMax = 18f;
            tooltipText.alignment = TextAlignmentOptions.MidlineLeft;
            tooltipText.raycastTarget = false;
        }

        tooltipRoot.gameObject.SetActive(false);
    }

    private void RefreshTooltipIfCurrent(WayPointRunTime state)
    {
        if (state == null || currentTooltipState != state || tooltipRoot == null || !tooltipRoot.gameObject.activeSelf)
        {
            return;
        }

        RefreshTooltipText(state);
    }

    private void RefreshTooltipText(WayPointRunTime state)
    {
        if (tooltipText == null || state == null || state.Definition == null)
        {
            return;
        }

        string displayName = string.IsNullOrWhiteSpace(state.DisplayName) ? state.Id : state.DisplayName;
        string statusText = GetTravelStatusText(state, out Color statusColor);
        string statusHex = ColorUtility.ToHtmlStringRGBA(statusColor);

        tooltipText.text = $"{displayName}\n<color=#{statusHex}>{statusText}</color>";
    }

    private string GetTravelStatusText(WayPointRunTime state, out Color statusColor)
    {
        if (!state.IsActive)
        {
            statusColor = cannotTravelColor;
            return "이동 불가능: 잠김";
        }

        if (state.Stone == null)
        {
            statusColor = cannotTravelColor;
            return "이동 불가능: 목적지 없음";
        }

        statusColor = canTravelColor;
        return "이동 가능";
    }

    // Close through the toggle script so cursor/input state is restored too.
    private void CloseMap()
    {
        if (uiToggle != null)
        {
            uiToggle.Close();
            return;
        }

        if (InputManager.Instance != null)
        {
            InputManager.Instance.SetSystemMenuOpen(false);
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        gameObject.SetActive(false);
    }
}
