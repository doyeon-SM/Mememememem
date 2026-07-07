using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WayPointMapUI : MonoBehaviour
{
    public static WayPointMapUI Instance { get; private set; }

    [Header("Map")]
    [SerializeField] private Image mapImage;
    [SerializeField] private RectTransform iconParent;
    [SerializeField] private WayPointMapDefinition defaultMap;
    [SerializeField] private WayPointMapIconUI iconPrefab;
    [SerializeField] private Vector2 generatedIconSize = new Vector2(48f, 48f);

    [Header("Close")]
    [SerializeField] private WayPointUIToggle uiToggle;
    [SerializeField] private bool closeAfterTravel = true;

    [Header("Tooltip")]
    [SerializeField] private RectTransform tooltipRoot;
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField] private Vector2 tooltipOffset = new Vector2(18f, -18f);
    [SerializeField] private Vector2 tooltipSize = new Vector2(240f, 90f);
    [SerializeField] private Color tooltipBackgroundColor = new Color(0f, 0f, 0f, 0.82f);
    [SerializeField] private Color canTravelColor = new Color(0.45f, 1f, 0.55f, 1f);
    [SerializeField] private Color cannotTravelColor = new Color(1f, 0.45f, 0.45f, 1f);

    private readonly Dictionary<string, WayPointMapIconUI> iconsById = new Dictionary<string, WayPointMapIconUI>();
    private WayPointMapDefinition currentMap;
    private WayPointMapOpenMode currentOpenMode = WayPointMapOpenMode.PreviewOnly;
    private WayPointRunTime currentTooltipState;
    private bool subscribed;

    public WayPointMapOpenMode CurrentOpenMode => currentOpenMode;
    public WayPointMapDefinition CurrentMap => currentMap;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

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
        RefreshMapView();
    }

    private void Start()
    {
        TrySubscribe();
        RefreshMapView();
    }

    private void OnDisable()
    {
        HideTooltip();
        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // UI가 열릴 때 보기 전용인지 이동 가능 모드인지 설정한다.
    public void PrepareOpen(WayPointMapOpenMode openMode, WayPointMapDefinition mapOverride = null)
    {
        currentOpenMode = openMode;
        currentMap = ResolveMap(mapOverride);
        RefreshMapView();
    }

    // Stone에서 직접 지도 이동 모드를 열고 싶을 때 호출한다.
    public void OpenFromStone(WayPointDefinition sourceWayPoint)
    {
        WayPointMapDefinition targetMap = sourceWayPoint != null ? sourceWayPoint.mapDefinition : null;

        if (uiToggle != null)
        {
            uiToggle.Open(WayPointMapOpenMode.Travel, targetMap);
            return;
        }

        PrepareOpen(WayPointMapOpenMode.Travel, targetMap);
        gameObject.SetActive(true);
    }

    // 현재 모드에서 이 웨이포인트를 클릭 이동할 수 있는지 확인한다.
    public bool CanTravelByClick(WayPointRunTime state)
    {
        return currentOpenMode == WayPointMapOpenMode.Travel
            && state != null
            && state.IsActive
            && state.Stone != null
            && WayPointManager.Instance != null
            && WayPointManager.Instance.IsMapAvailable(state.Definition.mapDefinition);
    }

    // 아이콘 클릭 시 이동 모드일 때만 순간이동을 시도한다.
    public void TravelTo(string id)
    {
        if (currentOpenMode != WayPointMapOpenMode.Travel || WayPointManager.Instance == null)
        {
            return;
        }

        bool traveled = WayPointManager.Instance.TryTravel(id);
        if (traveled && closeAfterTravel)
        {
            CloseMap();
        }
    }

    // 아이콘 위에 마우스를 올렸을 때 툴팁을 표시한다.
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

    // 마우스 위치를 따라 툴팁 위치를 갱신한다.
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

    // 마우스가 아이콘에서 벗어나면 툴팁을 숨긴다.
    public void HideTooltip()
    {
        currentTooltipState = null;

        if (tooltipRoot != null)
        {
            tooltipRoot.gameObject.SetActive(false);
        }
    }

    // WayPointManager 이벤트를 구독해서 해금 상태가 바뀌면 지도도 갱신한다.
    private void TrySubscribe()
    {
        if (subscribed || WayPointManager.Instance == null)
        {
            return;
        }

        WayPointManager.Instance.OnWayPointStateChanged += HandleWayPointStateChanged;
        WayPointManager.Instance.OnWayPointUnlocked += HandleWayPointStateChanged;
        WayPointManager.Instance.OnMapAvailabilityChanged += HandleMapAvailabilityChanged;
        subscribed = true;
    }

    // UI가 꺼질 때 이벤트 구독을 정리한다.
    private void Unsubscribe()
    {
        if (!subscribed || WayPointManager.Instance == null)
        {
            subscribed = false;
            return;
        }

        WayPointManager.Instance.OnWayPointStateChanged -= HandleWayPointStateChanged;
        WayPointManager.Instance.OnWayPointUnlocked -= HandleWayPointStateChanged;
        WayPointManager.Instance.OnMapAvailabilityChanged -= HandleMapAvailabilityChanged;
        subscribed = false;
    }

    // 현재 맵 배경과 아이콘을 다시 그린다.
    private void RefreshMapView()
    {
        if (WayPointManager.Instance == null)
        {
            return;
        }

        currentMap = ResolveMap(currentMap);
        ApplyMapSprite();
        RebuildIcons();
        RefreshAllIcons();
    }

    // 지정된 맵이 없거나 잠겨 있으면 볼 수 있는 첫 맵을 선택한다.
    private WayPointMapDefinition ResolveMap(WayPointMapDefinition requestedMap)
    {
        if (WayPointManager.Instance == null)
        {
            return requestedMap != null ? requestedMap : defaultMap;
        }

        if (requestedMap != null && WayPointManager.Instance.IsMapAvailable(requestedMap))
        {
            return requestedMap;
        }

        if (defaultMap != null && WayPointManager.Instance.IsMapAvailable(defaultMap))
        {
            return defaultMap;
        }

        List<WayPointMapDefinition> maps = WayPointManager.Instance.GetAllMaps();
        foreach (WayPointMapDefinition map in maps)
        {
            if (WayPointManager.Instance.IsMapAvailable(map))
            {
                return map;
            }
        }

        return requestedMap != null ? requestedMap : defaultMap;
    }

    // 현재 맵에 설정된 배경 스프라이트를 지도 Image에 적용한다.
    private void ApplyMapSprite()
    {
        if (mapImage == null || currentMap == null || currentMap.mapSprite == null)
        {
            return;
        }

        mapImage.sprite = currentMap.mapSprite;
    }

    // 현재 맵에 속한 웨이포인트 아이콘만 생성하거나 갱신한다.
    private void RebuildIcons()
    {
        if (WayPointManager.Instance == null || iconParent == null)
        {
            return;
        }

        HashSet<string> visibleIds = new HashSet<string>();
        List<WayPointRunTime> states = WayPointManager.Instance.GetStatesByMap(currentMap);

        foreach (WayPointRunTime state in states)
        {
            if (state == null || state.Definition == null || string.IsNullOrWhiteSpace(state.Id))
            {
                continue;
            }

            visibleIds.Add(state.Id);

            if (!iconsById.TryGetValue(state.Id, out WayPointMapIconUI icon) || icon == null)
            {
                icon = CreateIcon(state);
                iconsById[state.Id] = icon;
            }

            icon.gameObject.SetActive(true);
            icon.Initialize(this, state);
            icon.SetMapPosition(state.Definition.mapPosition);
        }

        foreach (KeyValuePair<string, WayPointMapIconUI> pair in iconsById)
        {
            if (pair.Value != null && !visibleIds.Contains(pair.Key))
            {
                pair.Value.gameObject.SetActive(false);
            }
        }
    }

    // 모든 아이콘 이미지를 현재 상태에 맞춰 갱신한다.
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

    // 웨이포인트 해금 상태가 바뀌면 해당 아이콘과 툴팁을 갱신한다.
    private void HandleWayPointStateChanged(WayPointRunTime state)
    {
        if (state == null)
        {
            return;
        }

        if (iconsById.TryGetValue(state.Id, out WayPointMapIconUI icon) && icon != null)
        {
            icon.Refresh();
        }

        if (currentTooltipState == state)
        {
            RefreshTooltipText(state);
        }
    }

    // 맵 해금 조건이 바뀌면 현재 맵 선택과 아이콘 목록을 다시 확인한다.
    private void HandleMapAvailabilityChanged(WayPointMapDefinition mapDefinition)
    {
        RefreshMapView();
    }

    // 프리팹이 있으면 프리팹을 쓰고, 없으면 기본 Image/Button 아이콘을 런타임에 만든다.
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

    // 툴팁 오브젝트가 없으면 기본 툴팁 UI를 자동 생성한다.
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

    // 웨이포인트 이름, 설명, 현재 이동 가능 여부를 툴팁 텍스트에 반영한다.
    private void RefreshTooltipText(WayPointRunTime state)
    {
        if (tooltipText == null || state == null || state.Definition == null)
        {
            return;
        }

        string displayName = string.IsNullOrWhiteSpace(state.DisplayName) ? state.Id : state.DisplayName;
        string description = string.IsNullOrWhiteSpace(state.Definition.tooltipDescription) ? string.Empty : $"\n{state.Definition.tooltipDescription}";
        string statusText = GetTravelStatusText(state, out Color statusColor);
        string statusHex = ColorUtility.ToHtmlStringRGBA(statusColor);

        tooltipText.text = $"{displayName}{description}\n<color=#{statusHex}>{statusText}</color>";
    }

    // 현재 모드와 해금 상태를 기준으로 툴팁 상태 문구를 만든다.
    private string GetTravelStatusText(WayPointRunTime state, out Color statusColor)
    {
        if (currentOpenMode == WayPointMapOpenMode.PreviewOnly)
        {
            statusColor = cannotTravelColor;
            return "보기 모드: 웨이포인트 스톤에서만 이동 가능";
        }

        if (state == null || !state.IsActive)
        {
            statusColor = cannotTravelColor;
            return "이동 불가: 미해금 웨이포인트";
        }

        if (state.Stone == null)
        {
            statusColor = cannotTravelColor;
            return "이동 불가: 목적지 Stone 없음";
        }

        if (WayPointManager.Instance != null && !WayPointManager.Instance.IsMapAvailable(state.Definition.mapDefinition))
        {
            statusColor = cannotTravelColor;
            return "이동 불가: 잠긴 맵";
        }

        statusColor = canTravelColor;
        return "이동 가능";
    }

    // 이동 성공 후 지도 UI를 닫고 입력/커서 상태를 복구한다.
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
