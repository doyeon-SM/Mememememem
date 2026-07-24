using KMS.InventoryDuped;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 지도 선택 버튼, 웨이포인트 아이콘, 툴팁과 이동 요청을 관리하는 지도 UI입니다.
/// 실제 해금 및 이동 가능 여부는 <see cref="WayPointManager"/>에 위임합니다.
/// </summary>
public class WayPointMapUI : MonoBehaviour
{
    /// <summary>현재 씬의 지도 UI 인스턴스입니다.</summary>
    public static WayPointMapUI Instance { get; private set; }

    [Header("Map")]
    [SerializeField] private Image mapImage;
    [SerializeField] private RectTransform iconParent;
    [SerializeField] private WayPointMapDefinition defaultMap;
    [SerializeField] private WayPointMapIconUI iconPrefab;
    [SerializeField] private Vector2 generatedIconSize = new Vector2(48f, 48f);

    [Header("Map Select")]
    [SerializeField] private RectTransform mapButtonParent;
    [Tooltip("자동 생성할 StageButton 프리팹 루트의 WayPointMapButtonView를 지정합니다.")]
    [SerializeField] private WayPointMapButtonView stageButtonPrefab;

    // 새 StageButton 이전의 Button 프리팹 및 자동 생성 UI 호환용입니다.
    // 기존 직렬화는 유지하되 새 Inspector에서는 노출하지 않습니다.
    [HideInInspector] [SerializeField] private Button mapButtonPrefab;
    [HideInInspector] [SerializeField] private Vector2 generatedMapButtonSize = new Vector2(160f, 34f);
    [HideInInspector] [SerializeField] private Color selectedMapButtonBackgroundColor = new Color(0.25f, 0.55f, 0.9f, 1f);
    [HideInInspector] [SerializeField] private Color normalMapButtonBackgroundColor = new Color(0.08f, 0.11f, 0.14f, 0.92f);
    [HideInInspector] [SerializeField] private Color normalMapButtonTextColor = new Color(1f, 1f, 1f, 1f);
    [HideInInspector] [SerializeField] private Color lockedMapButtonTextColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("Territory Travel")]
    [Tooltip("별도로 만든 영지 이동 버튼을 연결합니다. 일반 지도에서는 숨기고, 스톤에서 연 지도에서만 표시합니다.")]
    [SerializeField] private Button territoryTravelButton;
    [Tooltip("비워 두면 Territory Travel Button의 자식 TMP_Text를 자동으로 찾습니다.")]
    [SerializeField] private TMP_Text territoryTravelButtonText;
    [SerializeField] private string territoryTravelUnavailableSuffix = " (이동불가)";

    [Header("Close")]
    [SerializeField] private bool closeAfterTravel = true;

    [Header("Canvas")]
    [SerializeField] private bool bringCanvasToFrontOnOpen = true;
    [SerializeField] private int openSortingOrder = 1000;

    [Header("Tooltip")]
    [SerializeField] private RectTransform tooltipRoot;
    [Tooltip("새 WayPoint_ToolTip 디자인 루트의 전용 뷰입니다.")]
    [SerializeField] private WayPointTooltipView tooltipView;
    [FormerlySerializedAs("tooltipText")]
    [SerializeField] private TMP_Text tooltipTitleText;
    [SerializeField] private TMP_Text tooltipDescriptionText;
    [SerializeField] private TMP_FontAsset tooltipFont;
    [SerializeField] private float tooltipHorizontalGap = 18f;
    [SerializeField] private float tooltipViewportPadding = 8f;
    [Tooltip("아이콘에서 툴팁의 Fill_BG로 마우스를 옮길 수 있도록 숨김을 잠시 유예하는 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float tooltipHideDelay = 0.08f;
    [SerializeField] private Vector2 tooltipSize = new Vector2(240f, 90f);
    [SerializeField] private Color tooltipBackgroundColor = new Color(0f, 0f, 0f, 0.82f);
    [SerializeField] private Color canTravelColor = new Color(0.45f, 1f, 0.55f, 1f);
    [SerializeField] private Color cannotTravelColor = new Color(1f, 0.45f, 0.45f, 1f);

    [Header("Preview Mode")]
    [Tooltip("프리뷰 모드 안내를 덧붙일 기존 TMP_Text입니다.")]
    [SerializeField] private TMP_Text previewModeText;
    [SerializeField] private string previewModeSuffix = " (프리뷰 모드)";

    [Header("Tooltip Text")]
    [TextArea]
    [SerializeField] private string previewOnlyStatusText = "보기 모드: 웨이포인트 스톤에서만 이동 가능";
    [TextArea]
    [SerializeField] private string lockedWayPointStatusText = "이동 불가: 미해금 웨이포인트";
    [TextArea]
    [SerializeField] private string missingStoneStatusText = "이동 불가: 목적지 Stone 없음";
    [TextArea]
    [SerializeField] private string lockedMapStatusText = "이동 불가: 잠긴 맵";
    [TextArea]
    [SerializeField] private string canTravelStatusText = "이동 가능";

    private readonly Dictionary<string, WayPointMapIconUI> iconsById = new Dictionary<string, WayPointMapIconUI>();
    private readonly Dictionary<WayPointMapDefinition, Button> mapButtonsByDefinition = new Dictionary<WayPointMapDefinition, Button>();
    private WayPointMapDefinition currentMap;
    private WayPointMapOpenMode currentOpenMode = WayPointMapOpenMode.PreviewOnly;
    private WayPointRunTime currentTooltipState;
    private string openedFromWayPointId;
    private bool subscribed;
    private Canvas cachedCanvas;
    private bool cachedCanvasOriginalOverrideSorting;
    private int cachedCanvasOriginalSortingOrder;
    private Text territoryTravelLegacyText;
    private string territoryTravelButtonBaseText;
    private string previewModeOriginalText;
    private bool previewModeTextCached;
    private bool pointerOverWayPointIcon;
    private bool pointerOverTooltip;
    private bool tooltipHideRequested;
    private float tooltipHideAtTime;

    /// <summary>현재 지도를 열 때 적용된 보기 전용 또는 이동 모드입니다.</summary>
    public WayPointMapOpenMode CurrentOpenMode => currentOpenMode;

    /// <summary>현재 화면에 표시 중인 지도 정의입니다.</summary>
    public WayPointMapDefinition CurrentMap => currentMap;

    /// <summary>지도 Panel 전체가 현재 계층에서 실제로 표시되는지 나타냅니다.</summary>
    public bool IsVisible => gameObject.activeInHierarchy;

    /// <summary>매니저가 열고 닫을 WayPointMapUI가 부착된 Panel 오브젝트입니다.</summary>
    public GameObject VisibilityTarget => gameObject;

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

        CacheCanvasState();
        EnsureTooltip();
        CachePreviewModeText();
        BindTerritoryTravelButton();
    }

    private void OnEnable()
    {
        TrySubscribe();
        RefreshMapView();
        RefreshTerritoryTravelButton();
    }

    private void Start()
    {
        TrySubscribe();
        RefreshMapView();
    }

    private void LateUpdate()
    {
        if (!tooltipHideRequested)
        {
            return;
        }

        if (pointerOverWayPointIcon || pointerOverTooltip)
        {
            tooltipHideRequested = false;
            return;
        }

        if (Time.unscaledTime >= tooltipHideAtTime)
        {
            HideTooltip();
        }
    }

    private void OnDisable()
    {
        HideTooltip();
        RestorePreviewModeText();
        RestoreCanvasSorting();
        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnbindTerritoryTravelButton();
    }

    private void OnValidate()
    {
        ApplyTooltipFont();
    }

    // UI가 열릴 때 보기 전용인지 이동 가능 모드인지 설정한다.
    /// <summary>다음 UI 열기에 사용할 모드와 초기 지도를 준비하고 화면을 갱신합니다.</summary>
    /// <param name="openMode">보기 전용 또는 웨이포인트 이동 모드입니다.</param>
    /// <param name="mapOverride">null이 아니면 처음 표시할 지도입니다.</param>
    public void PrepareOpen(WayPointMapOpenMode openMode, WayPointMapDefinition mapOverride = null)
    {
        BringCanvasToFront();
        currentOpenMode = openMode;
        openedFromWayPointId = string.Empty;
        currentMap = ResolveInitialMap(mapOverride);
        RefreshMapView();
        RefreshTerritoryTravelButton();
        RefreshPreviewModeText();
    }

    /// <summary>지도 표시가 꺼지기 전에 툴팁과 Canvas 정렬 상태를 정리합니다.</summary>
    public void PrepareClose()
    {
        HideTooltip();
        RestorePreviewModeText();
        RestoreCanvasSorting();
    }

    /// <summary>현재 이동 지도를 연 출발 웨이포인트를 기록합니다.</summary>
    internal void SetOpenedFromWayPoint(WayPointDefinition sourceWayPoint)
    {
        openedFromWayPointId = sourceWayPoint != null ? sourceWayPoint.id : string.Empty;
        RefreshTerritoryTravelButton();
    }

    /// <summary>런타임 또는 인스펙터에서 만든 영지 이동 버튼 참조를 교체합니다.</summary>
    public void SetTerritoryTravelButton(Button button)
    {
        UnbindTerritoryTravelButton();
        territoryTravelButton = button;
        territoryTravelButtonText = null;
        territoryTravelLegacyText = null;
        territoryTravelButtonBaseText = string.Empty;
        BindTerritoryTravelButton();
        RefreshTerritoryTravelButton();
    }

    // Stone에서 직접 지도 이동 모드를 열고 싶을 때 호출한다.
    /// <summary>상호작용한 스톤의 지도를 이동 모드로 엽니다.</summary>
    /// <param name="sourceWayPoint">지도를 연 출발 웨이포인트입니다.</param>
    public void OpenFromStone(WayPointDefinition sourceWayPoint)
    {
        WayPointMapDefinition targetMap = sourceWayPoint != null ? sourceWayPoint.mapDefinition : null;
        openedFromWayPointId = sourceWayPoint != null ? sourceWayPoint.id : string.Empty;

        if (WayPointManager.Instance != null)
        {
            WayPointManager.Instance.OpenMapFromStone(sourceWayPoint);
            openedFromWayPointId = sourceWayPoint != null ? sourceWayPoint.id : string.Empty;
            return;
        }

        PrepareOpen(WayPointMapOpenMode.Travel, targetMap);
        openedFromWayPointId = sourceWayPoint != null ? sourceWayPoint.id : string.Empty;
        gameObject.SetActive(true);
    }

    // 현재 모드에서 이 웨이포인트를 클릭 이동할 수 있는지 확인한다.
    /// <summary>현재 UI 모드와 웨이포인트 상태를 기준으로 아이콘 클릭 이동 가능 여부를 확인합니다.</summary>
    public bool CanTravelByClick(WayPointRunTime state)
    {
        return currentOpenMode == WayPointMapOpenMode.Travel
            && state != null
            && WayPointManager.Instance != null
            && WayPointManager.Instance.CanTravel(state.Id);
    }

    // 지도 버튼을 눌렀을 때 해당 맵으로 지도 배경과 아이콘을 바꾼다.
    /// <summary>사용 가능한 지도라면 지도 배경과 아이콘을 해당 지도로 전환합니다.</summary>
    public void SelectMap(WayPointMapDefinition mapDefinition)
    {
        if (mapDefinition == null || WayPointManager.Instance == null)
        {
            return;
        }

        if (!WayPointManager.Instance.IsMapVisibleInList(mapDefinition)
            || !WayPointManager.Instance.IsMapAvailable(mapDefinition))
        {
            return;
        }

        currentMap = mapDefinition;
        HideTooltip();
        RefreshMapView();
    }

    // 아이콘 클릭 시 이동 모드일 때만 순간이동을 시도한다.
    /// <summary>아이콘에서 전달받은 웨이포인트 ID로 이동을 요청하고 성공 시 지도를 닫습니다.</summary>
    public void TravelTo(string id)
    {
        if (currentOpenMode != WayPointMapOpenMode.Travel || WayPointManager.Instance == null)
        {
            Debug.LogWarning("[WayPointMapUI] Travel ignored because map is not in Travel mode or WayPointManager is missing.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(openedFromWayPointId) && openedFromWayPointId == id)
        {
            Debug.Log("[WayPointMapUI] The selected waypoint is the same stone that opened the map. Teleport can succeed but may look unchanged.");
        }

        bool traveled = WayPointManager.Instance.TryTravel(id);
        if (!traveled)
        {
            Debug.LogWarning($"[WayPointMapUI] Travel failed for waypoint id: {id}");
        }

        if (traveled && closeAfterTravel)
        {
            CloseMap();
        }
    }

    // 아이콘 위에 마우스를 올렸을 때 툴팁을 표시한다.
    /// <summary>지정 웨이포인트 아이콘 옆에 상태 툴팁을 표시합니다.</summary>
    public void ShowTooltip(WayPointRunTime state, RectTransform iconRectTransform)
    {
        if (state == null || state.Definition == null)
        {
            return;
        }

        EnsureTooltip();
        bool hasNewTooltip = tooltipView != null;
        bool hasLegacyTooltip = tooltipTitleText != null && tooltipDescriptionText != null;
        if (tooltipRoot == null || (!hasNewTooltip && !hasLegacyTooltip))
        {
            return;
        }

        currentTooltipState = state;
        tooltipHideRequested = false;
        tooltipRoot.gameObject.SetActive(true);
        tooltipRoot.SetAsLastSibling();
        RefreshTooltip(state);
        MoveTooltip(iconRectTransform);
    }

    /// <summary>웨이포인트 아이콘 진입 시 해당 툴팁을 표시하고 예약된 숨김을 취소합니다.</summary>
    public void NotifyWayPointPointerEnter(WayPointRunTime state, RectTransform iconRectTransform)
    {
        pointerOverWayPointIcon = true;
        tooltipHideRequested = false;
        ShowTooltip(state, iconRectTransform);
    }

    /// <summary>웨이포인트 아이콘에서 벗어나면 툴팁 숨김을 예약합니다.</summary>
    public void NotifyWayPointPointerExit()
    {
        pointerOverWayPointIcon = false;
        RequestTooltipHide();
    }

    /// <summary>툴팁 버튼 영역에 진입하면 아이콘에서 예약한 숨김을 취소합니다.</summary>
    public void NotifyTooltipPointerEnter()
    {
        pointerOverTooltip = true;
        tooltipHideRequested = false;
    }

    /// <summary>툴팁 버튼 영역에서도 벗어나면 툴팁 숨김을 예약합니다.</summary>
    public void NotifyTooltipPointerExit()
    {
        pointerOverTooltip = false;
        RequestTooltipHide();
    }

    private void RequestTooltipHide()
    {
        if (currentTooltipState == null)
        {
            return;
        }

        tooltipHideRequested = true;
        tooltipHideAtTime = Time.unscaledTime + tooltipHideDelay;
    }

    // 아이콘 위치를 기준으로 좌/우 중 더 여유 있는 쪽에 툴팁을 고정한다.
    /// <summary>현재 툴팁을 지정 아이콘 위치에 맞춰 재배치합니다.</summary>
    public void MoveTooltip(RectTransform iconRectTransform)
    {
        if (tooltipRoot == null || !tooltipRoot.gameObject.activeSelf || iconRectTransform == null)
        {
            return;
        }

        RectTransform parent = tooltipRoot.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        Camera eventCamera = GetCanvasEventCamera();
        Vector2 iconScreenPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, iconRectTransform.position);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, iconScreenPosition, eventCamera, out Vector2 iconLocalPosition))
        {
            return;
        }

        tooltipRoot.SetAsLastSibling();

        Rect parentRect = parent.rect;
        Vector2 tooltipHalfSize = GetTooltipSize() * 0.5f;
        float iconHalfWidth = GetWidthInParentSpace(iconRectTransform, parent) * 0.5f;
        float parentCenterX = parentRect.center.x;
        float side = iconLocalPosition.x >= parentCenterX ? -1f : 1f;

        Vector2 anchoredPosition = iconLocalPosition;
        anchoredPosition.x += side * (iconHalfWidth + tooltipHorizontalGap + tooltipHalfSize.x);
        anchoredPosition.x = Mathf.Clamp(
            anchoredPosition.x,
            parentRect.xMin + tooltipViewportPadding + tooltipHalfSize.x,
            parentRect.xMax - tooltipViewportPadding - tooltipHalfSize.x);
        anchoredPosition.y = Mathf.Clamp(
            anchoredPosition.y,
            parentRect.yMin + tooltipViewportPadding + tooltipHalfSize.y,
            parentRect.yMax - tooltipViewportPadding - tooltipHalfSize.y);

        tooltipRoot.anchoredPosition = anchoredPosition;
    }

    private Camera GetCanvasEventCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            return canvas.worldCamera;
        }

        return null;
    }

    private Vector2 GetTooltipSize()
    {
        if (tooltipView != null && tooltipView.RectTransform != null)
        {
            Vector2 viewSize = tooltipView.RectTransform.rect.size;
            if (viewSize.x > 0f && viewSize.y > 0f)
            {
                return viewSize;
            }
        }

        if (tooltipRoot == null)
        {
            return tooltipSize;
        }

        Vector2 size = tooltipRoot.rect.size;
        if (size.x <= 0f || size.y <= 0f)
        {
            return tooltipSize;
        }

        return size;
    }

    private float GetWidthInParentSpace(RectTransform source, RectTransform parent)
    {
        if (source == null || parent == null)
        {
            return generatedIconSize.x;
        }

        float parentScale = Mathf.Max(0.0001f, parent.lossyScale.x);
        return source.rect.width * source.lossyScale.x / parentScale;
    }

    // 마우스가 아이콘에서 벗어나면 툴팁을 숨긴다.
    /// <summary>현재 표시 중인 웨이포인트 툴팁을 숨깁니다.</summary>
    public void HideTooltip()
    {
        currentTooltipState = null;
        pointerOverWayPointIcon = false;
        pointerOverTooltip = false;
        tooltipHideRequested = false;
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
        RebuildMapButtons();
        RebuildIcons();
        RefreshAllIcons();
    }

    // 지도를 처음 열 때 현재 씬에 연결된 지도를 우선한다.
    private WayPointMapDefinition ResolveInitialMap(WayPointMapDefinition requestedMap)
    {
        if (WayPointManager.Instance == null)
        {
            return requestedMap != null ? requestedMap : defaultMap;
        }

        WayPointMapDefinition sceneMap = WayPointManager.Instance.GetPreferredMapForScene(
            SceneManager.GetActiveScene().name,
            requestedMap);

        return sceneMap != null ? sceneMap : ResolveMap(requestedMap);
    }

    // 지정된 맵이 없거나 잠겨 있으면 볼 수 있는 첫 맵을 선택한다.
    private WayPointMapDefinition ResolveMap(WayPointMapDefinition requestedMap)
    {
        if (WayPointManager.Instance == null)
        {
            return requestedMap != null ? requestedMap : defaultMap;
        }

        // 현재 씬에 실제로 연결된 지도는 잠금 상태여도 현재 위치를 올바르게
        // 표시해야 하므로 다른 지도로 대체하지 않는다.
        if (requestedMap != null
            && WayPointManager.Instance.IsMapVisibleInList(requestedMap)
            && WayPointManager.Instance.IsMapAssignedToScene(
                requestedMap,
                SceneManager.GetActiveScene().name))
        {
            return requestedMap;
        }

        if (requestedMap != null
            && WayPointManager.Instance.IsMapVisibleInList(requestedMap)
            && WayPointManager.Instance.IsMapAvailable(requestedMap))
        {
            return requestedMap;
        }

        if (defaultMap != null
            && WayPointManager.Instance.IsMapVisibleInList(defaultMap)
            && WayPointManager.Instance.IsMapAvailable(defaultMap))
        {
            return defaultMap;
        }

        List<WayPointMapDefinition> maps = WayPointManager.Instance.GetAllMaps();
        foreach (WayPointMapDefinition map in maps)
        {
            if (WayPointManager.Instance.IsMapVisibleInList(map)
                && WayPointManager.Instance.IsMapAvailable(map))
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

    // 스테이지 기본 지도는 항상 표시하고, 하위 지도는 실제 방문 후에만 표시한다.
    private void RebuildMapButtons()
    {
        if (WayPointManager.Instance == null || mapButtonParent == null)
        {
            return;
        }

        HashSet<WayPointMapDefinition> visibleMaps = new HashSet<WayPointMapDefinition>();
        List<WayPointMapDefinition> orderedMaps = new List<WayPointMapDefinition>();

        // 스테이지 등록 순서를 유지한다. 각 스테이지의 기본 지도 뒤에
        // 최초 방문한 동굴/실내 같은 하위 지도 버튼을 배치한다.
        foreach (WayPointStageDefinition stageDefinition in WayPointManager.Instance.GetAllStages())
        {
            if (stageDefinition == null)
            {
                continue;
            }

            AddVisibleMapButtonTarget(orderedMaps, stageDefinition.GetDefaultMap());

            if (stageDefinition.maps == null)
            {
                continue;
            }

            foreach (WayPointMapDefinition mapDefinition in stageDefinition.maps)
            {
                if (mapDefinition == null || stageDefinition.IsDefaultMap(mapDefinition))
                {
                    continue;
                }

                AddVisibleMapButtonTarget(orderedMaps, mapDefinition);
            }
        }

        // 아직 스테이지 데이터로 옮기지 않은 기존 지도도 계속 표시한다.
        foreach (WayPointMapDefinition mapDefinition in WayPointManager.Instance.GetAllMaps())
        {
            if (WayPointManager.Instance.GetStageForMap(mapDefinition) == null)
            {
                AddVisibleMapButtonTarget(orderedMaps, mapDefinition);
            }
        }

        int siblingIndex = 0;
        foreach (WayPointMapDefinition mapDefinition in orderedMaps)
        {
            visibleMaps.Add(mapDefinition);

            if (!mapButtonsByDefinition.TryGetValue(mapDefinition, out Button button) || button == null)
            {
                button = CreateMapButton(mapDefinition);
                mapButtonsByDefinition[mapDefinition] = button;
            }

            if (button == null)
            {
                continue;
            }

            GameObject buttonRoot = GetMapButtonRoot(button);
            buttonRoot.SetActive(true);
            buttonRoot.transform.SetSiblingIndex(siblingIndex++);
            RefreshMapButton(button, mapDefinition);
        }

        foreach (KeyValuePair<WayPointMapDefinition, Button> pair in mapButtonsByDefinition)
        {
            if (pair.Value != null && !visibleMaps.Contains(pair.Key))
            {
                GetMapButtonRoot(pair.Value).SetActive(false);
            }
        }
    }

    private void AddVisibleMapButtonTarget(
        List<WayPointMapDefinition> orderedMaps,
        WayPointMapDefinition mapDefinition)
    {
        if (mapDefinition == null
            || orderedMaps.Contains(mapDefinition)
            || WayPointManager.Instance == null
            || !WayPointManager.Instance.IsMapVisibleInList(mapDefinition))
        {
            return;
        }

        orderedMaps.Add(mapDefinition);
    }

    private Button CreateMapButton(WayPointMapDefinition mapDefinition)
    {
        string buttonId = string.IsNullOrWhiteSpace(mapDefinition.id)
            ? mapDefinition.name
            : mapDefinition.id;
        Button button = CreateMapButtonObject(buttonId);
        if (button == null)
        {
            return null;
        }

        button.onClick.AddListener(() => SelectMap(mapDefinition));
        return button;
    }

    // 새 버튼 프리팹은 실제 Button이 하위 FillBG (1)에 있으므로
    // 루트 WayPointMapButtonView를 생성한 뒤 내부 Button을 반환한다.
    private Button CreateMapButtonObject(string buttonId)
    {
        Button button;

        if (stageButtonPrefab != null)
        {
            WayPointMapButtonView instanceView =
                Instantiate(stageButtonPrefab, mapButtonParent);
            button = instanceView != null ? instanceView.Button : null;
        }
        else if (mapButtonPrefab != null)
        {
            WayPointMapButtonView templateView =
                mapButtonPrefab.GetComponentInParent<WayPointMapButtonView>(true);

            if (templateView != null)
            {
                GameObject instanceObject = Instantiate(templateView.gameObject, mapButtonParent);
                WayPointMapButtonView instanceView =
                    instanceObject.GetComponent<WayPointMapButtonView>();
                button = instanceView != null ? instanceView.Button : null;
            }
            else
            {
                // 기존 루트 Button 프리팹도 계속 지원한다.
                button = Instantiate(mapButtonPrefab, mapButtonParent);
            }
        }
        else
        {
            GameObject buttonObject = new GameObject($"MapButton_{buttonId}", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(mapButtonParent, false);

            RectTransform rectTransform = buttonObject.transform as RectTransform;
            rectTransform.sizeDelta = generatedMapButtonSize;

            Image background = buttonObject.GetComponent<Image>();
            background.color = normalMapButtonBackgroundColor;

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);

            RectTransform textRect = textObject.transform as RectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);

            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.fontSize = 18f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 18f;
            text.raycastTarget = false;

            button = buttonObject.GetComponent<Button>();
        }

        if (button == null)
        {
            Debug.LogError(
                $"[WayPointMapUI] Map button '{buttonId}'에 사용할 Button을 찾을 수 없습니다. "
                + "StageButton/FillBG (1)의 Button 연결을 확인하세요.",
                this);
            return null;
        }

        GetMapButtonRoot(button).name = $"MapButton_{buttonId}";
        button.onClick.RemoveAllListeners();
        return button;
    }

    private static GameObject GetMapButtonRoot(Button button)
    {
        if (button == null)
        {
            return null;
        }

        WayPointMapButtonView view = button.GetComponentInParent<WayPointMapButtonView>();
        return view != null ? view.gameObject : button.gameObject;
    }

    // 맵 해금 상태와 현재 선택 상태에 맞춰 버튼 표시를 갱신한다.
    private void RefreshMapButton(Button button, WayPointMapDefinition mapDefinition)
    {
        if (button == null)
        {
            return;
        }

        bool isAvailable = WayPointManager.Instance != null && WayPointManager.Instance.IsMapAvailable(mapDefinition);
        bool isSelected = currentMap == mapDefinition;

        WayPointMapButtonView view = button.GetComponentInParent<WayPointMapButtonView>();
        if (view != null)
        {
            view.Refresh(
                mapDefinition,
                GetMapDisplayName(mapDefinition),
                isAvailable);
            return;
        }

        ApplyMapButtonVisuals(button, GetMapDisplayName(mapDefinition), isAvailable, isSelected);
    }

    private void ApplyMapButtonVisuals(Button button, string displayName, bool isAvailable, bool isSelected)
    {
        button.interactable = isAvailable;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = isAvailable ? displayName : $"{displayName} (잠김)";
            text.color = isAvailable ? normalMapButtonTextColor : lockedMapButtonTextColor;
        }

        Text legacyText = button.GetComponentInChildren<Text>(true);
        if (legacyText != null)
        {
            legacyText.text = isAvailable ? displayName : $"{displayName} (잠김)";
            legacyText.color = isAvailable ? normalMapButtonTextColor : lockedMapButtonTextColor;
        }

        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            background.color = isSelected ? selectedMapButtonBackgroundColor : normalMapButtonBackgroundColor;
        }
    }

    private static string GetMapDisplayName(WayPointMapDefinition mapDefinition)
    {
        if (mapDefinition == null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(mapDefinition.displayName)
            ? mapDefinition.id
            : mapDefinition.displayName;
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
            RefreshTooltip(state);
        }

        RefreshTerritoryTravelButton();
    }

    // 맵 해금 조건이 바뀌면 현재 맵 선택과 아이콘 목록을 다시 확인한다.
    private void HandleMapAvailabilityChanged(WayPointMapDefinition mapDefinition)
    {
        RefreshMapView();
        RefreshTerritoryTravelButton();
    }

    private void BindTerritoryTravelButton()
    {
        if (territoryTravelButton == null)
        {
            return;
        }

        territoryTravelButton.onClick.RemoveListener(HandleTerritoryTravelButtonClicked);
        territoryTravelButton.onClick.AddListener(HandleTerritoryTravelButtonClicked);

        if (territoryTravelButtonText == null)
        {
            territoryTravelButtonText = territoryTravelButton.GetComponentInChildren<TMP_Text>(true);
        }

        territoryTravelLegacyText = territoryTravelButton.GetComponentInChildren<Text>(true);
        if (string.IsNullOrEmpty(territoryTravelButtonBaseText))
        {
            territoryTravelButtonBaseText = territoryTravelButtonText != null
                ? territoryTravelButtonText.text
                : territoryTravelLegacyText != null
                    ? territoryTravelLegacyText.text
                    : "영지로 이동";
        }
    }

    private void UnbindTerritoryTravelButton()
    {
        if (territoryTravelButton != null)
        {
            territoryTravelButton.onClick.RemoveListener(HandleTerritoryTravelButtonClicked);
        }
    }

    private void HandleTerritoryTravelButtonClicked()
    {
        if (WayPointManager.Instance == null || !WayPointManager.Instance.TryTravelToTerritory())
        {
            RefreshTerritoryTravelButton();
        }
    }

    /// <summary>
    /// 일반 지도에서는 영지 버튼을 숨기고, 스톤 지도에서는 이동 가능 여부와 문구를 갱신합니다.
    /// </summary>
    private void RefreshTerritoryTravelButton()
    {
        if (territoryTravelButton == null)
        {
            return;
        }

        bool openedFromStone = currentOpenMode == WayPointMapOpenMode.Travel
            && !string.IsNullOrWhiteSpace(openedFromWayPointId);
        territoryTravelButton.gameObject.SetActive(openedFromStone);
        if (!openedFromStone)
        {
            return;
        }

        bool canTravel = WayPointManager.Instance != null
            && WayPointManager.Instance.CanTravelToTerritoryFromOpenedStone();
        territoryTravelButton.interactable = canTravel;

        string label = canTravel
            ? territoryTravelButtonBaseText
            : territoryTravelButtonBaseText + territoryTravelUnavailableSuffix;

        if (territoryTravelButtonText != null)
        {
            territoryTravelButtonText.text = label;
        }

        if (territoryTravelLegacyText != null)
        {
            territoryTravelLegacyText.text = label;
        }
    }

    // 프리팹이 있으면 프리팹을 쓰고, 없으면 기본 Image/Button 아이콘을 런타임에 만든다.
    private WayPointMapIconUI CreateIcon(WayPointRunTime state)
    {
        WayPointMapIconUI icon;

        if (iconPrefab != null)
        {
            icon = Instantiate(iconPrefab, iconParent);
            ApplyGeneratedIconRect(icon);
        }
        else
        {
            GameObject iconObject = new GameObject($"WayPointIcon_{state.Id}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(WayPointMapIconUI));
            iconObject.transform.SetParent(iconParent, false);

            icon = iconObject.GetComponent<WayPointMapIconUI>();
            ApplyGeneratedIconRect(icon);
        }

        icon.name = $"WayPointIcon_{state.Id}";
        return icon;
    }

    // 아이콘 프리팹의 RectTransform 설정이 비어 있어도 지도에서 클릭/호버 가능한 크기를 보장한다.
    private void ApplyGeneratedIconRect(WayPointMapIconUI icon)
    {
        RectTransform rectTransform = icon != null ? icon.transform as RectTransform : null;
        if (rectTransform == null)
        {
            return;
        }

        if (rectTransform.sizeDelta.sqrMagnitude <= 0.01f)
        {
            rectTransform.sizeDelta = generatedIconSize;
        }

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    // 지도 Canvas의 원래 정렬값을 저장한다.
    private void CacheCanvasState()
    {
        if (cachedCanvas != null)
        {
            return;
        }

        cachedCanvas = GetComponentInParent<Canvas>();
        if (cachedCanvas == null)
        {
            return;
        }

        cachedCanvasOriginalOverrideSorting = cachedCanvas.overrideSorting;
        cachedCanvasOriginalSortingOrder = cachedCanvas.sortingOrder;
    }

    // 플레이어 UI나 인벤토리 Canvas가 위에 있어도 지도 아이콘이 마우스 이벤트를 받도록 앞으로 올린다.
    private void BringCanvasToFront()
    {
        if (!bringCanvasToFrontOnOpen)
        {
            return;
        }

        CacheCanvasState();
        if (cachedCanvas == null)
        {
            return;
        }

        cachedCanvas.overrideSorting = true;
        cachedCanvas.sortingOrder = openSortingOrder;
    }

    // 지도 UI가 닫히거나 꺼질 때 Canvas 정렬값을 원래대로 되돌린다.
    private void RestoreCanvasSorting()
    {
        if (!bringCanvasToFrontOnOpen || cachedCanvas == null)
        {
            return;
        }

        cachedCanvas.overrideSorting = cachedCanvasOriginalOverrideSorting;
        cachedCanvas.sortingOrder = cachedCanvasOriginalSortingOrder;
    }

    // 툴팁 오브젝트가 없으면 기본 툴팁 UI를 자동 생성한다.
    private void EnsureTooltip()
    {
        if (tooltipView != null)
        {
            if (tooltipRoot == null)
            {
                tooltipRoot = tooltipView.transform.parent as RectTransform;
                if (tooltipRoot == null)
                {
                    tooltipRoot = tooltipView.RectTransform;
                }
            }

            tooltipView.Initialize(this);
            tooltipRoot.gameObject.SetActive(false);
            return;
        }

        if (tooltipRoot != null && tooltipTitleText != null && tooltipDescriptionText != null)
        {
            ConfigureTooltipRect();
            ApplyTooltipFont();
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
            ConfigureTooltipRect();

            Image background = tooltipObject.GetComponent<Image>();
            background.color = tooltipBackgroundColor;
            background.raycastTarget = false;

            CanvasGroup canvasGroup = tooltipObject.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (tooltipTitleText == null)
        {
            tooltipTitleText = CreateTooltipText("TitleText", new Vector2(12f, Mathf.Max(8f, tooltipSize.y - 34f)), new Vector2(-12f, -8f), 20f, FontStyles.Bold);
        }

        if (tooltipDescriptionText == null)
        {
            tooltipDescriptionText = CreateTooltipText("DescriptionText", new Vector2(12f, 8f), new Vector2(-12f, -36f), 16f, FontStyles.Normal);
        }

        ApplyTooltipFont();
        tooltipRoot.gameObject.SetActive(false);
    }

    private void ConfigureTooltipRect()
    {
        if (tooltipRoot == null)
        {
            return;
        }

        tooltipRoot.sizeDelta = tooltipSize;
        tooltipRoot.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRoot.pivot = new Vector2(0.5f, 0.5f);
    }

    private TMP_Text CreateTooltipText(string objectName, Vector2 offsetMin, Vector2 offsetMax, float fontSize, FontStyles fontStyle)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(tooltipRoot, false);

        RectTransform textRect = textObject.transform as RectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = offsetMin;
        textRect.offsetMax = offsetMax;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;

        return text;
    }

    // 인스펙터에 지정한 TMP 폰트를 툴팁 텍스트에 적용한다.
    private void ApplyTooltipFont()
    {
        if (tooltipFont == null)
        {
            return;
        }

        if (tooltipTitleText != null)
        {
            tooltipTitleText.font = tooltipFont;
        }

        if (tooltipDescriptionText != null)
        {
            tooltipDescriptionText.font = tooltipFont;
        }
    }

    // 웨이포인트 이름, 설명, 현재 이동 가능 여부를 툴팁 텍스트에 반영한다.
    private void RefreshTooltip(WayPointRunTime state)
    {
        if (tooltipView != null)
        {
            tooltipView.Refresh(state, currentOpenMode, CanTravelByClick(state));
            return;
        }

        RefreshTooltipText(state);
    }

    private void RefreshTooltipText(WayPointRunTime state)
    {
        if (tooltipTitleText == null || tooltipDescriptionText == null || state == null || state.Definition == null)
        {
            return;
        }

        string displayName = string.IsNullOrWhiteSpace(state.DisplayName) ? state.Id : state.DisplayName;
        string description = string.IsNullOrWhiteSpace(state.Definition.tooltipDescription) ? string.Empty : $"{state.Definition.tooltipDescription}\n";
        string statusText = GetTravelStatusText(state, out Color statusColor);
        string statusHex = ColorUtility.ToHtmlStringRGBA(statusColor);

        tooltipTitleText.text = displayName;
        tooltipDescriptionText.text = $"{description}<color=#{statusHex}>{statusText}</color>";
    }

    // 현재 모드와 해금 상태를 기준으로 툴팁 상태 문구를 만든다.
    private string GetTravelStatusText(WayPointRunTime state, out Color statusColor)
    {
        if (currentOpenMode == WayPointMapOpenMode.PreviewOnly)
        {
            statusColor = cannotTravelColor;
            return previewOnlyStatusText;
        }

        if (state == null || !state.IsActive)
        {
            statusColor = cannotTravelColor;
            return lockedWayPointStatusText;
        }

        if (WayPointManager.Instance != null
            && WayPointManager.Instance.IsWayPointInCurrentScene(state.Definition)
            && state.Stone == null)
        {
            statusColor = cannotTravelColor;
            return missingStoneStatusText;
        }

        if (WayPointManager.Instance != null && !WayPointManager.Instance.IsMapAvailable(state.Definition.mapDefinition))
        {
            statusColor = cannotTravelColor;
            return lockedMapStatusText;
        }

        statusColor = canTravelColor;
        return canTravelStatusText;
    }

    // 이동 성공 후 지도 UI를 닫는다. 입력/커서 상태 복구는 WayPointManager가 담당한다.
    private void CloseMap()
    {
        HideTooltip();
        RestorePreviewModeText();
        RestoreCanvasSorting();

        if (WayPointManager.Instance != null)
        {
            WayPointManager.Instance.CloseMap();
            return;
        }

        gameObject.SetActive(false);
    }

    private void CachePreviewModeText()
    {
        if (previewModeTextCached || previewModeText == null)
        {
            return;
        }

        previewModeOriginalText = previewModeText.text;
        previewModeTextCached = true;
    }

    private void RefreshPreviewModeText()
    {
        CachePreviewModeText();
        if (!previewModeTextCached || previewModeText == null)
        {
            return;
        }

        previewModeText.text = currentOpenMode == WayPointMapOpenMode.PreviewOnly
            ? previewModeOriginalText + previewModeSuffix
            : previewModeOriginalText;
    }

    private void RestorePreviewModeText()
    {
        if (previewModeTextCached && previewModeText != null)
        {
            previewModeText.text = previewModeOriginalText;
        }
    }
}
