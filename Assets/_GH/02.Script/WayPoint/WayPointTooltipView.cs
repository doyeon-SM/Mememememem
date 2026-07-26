using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 새 웨이포인트 툴팁 프리팹의 이름, 잠금 이미지, 아이콘과 이동 버튼을 갱신합니다.
/// 프리팹에서 설정한 색상과 스프라이트는 기본값으로 보존합니다.
/// </summary>
[DisallowMultipleComponent]
public class WayPointTooltipView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [SerializeField] private TMP_Text waypointNameText;
    [SerializeField] private Image waypointIcon;
    [Tooltip("잠금 상태의 반투명 배경입니다. 해금 후에도 클릭 영역으로 유지됩니다.")]
    [SerializeField] private Image fillBackground;
    [Tooltip("Fill_BG 오브젝트에 있는 이동 버튼입니다.")]
    [SerializeField] private Button travelButton;

    [Header("State")]
    [Range(0, 255)]
    [SerializeField] private int lockedBackgroundAlpha = 200;
    [SerializeField] private Color previewUnavailableNameColor = new Color(1f, 0.25f, 0.25f, 1f);

    private WayPointMapUI owner;
    private string waypointId;
    private Sprite defaultLockedIcon;
    private Color defaultNameColor = Color.white;
    private bool defaultsCached;

    /// <summary>툴팁 배치 계산에 사용할 실제 디자인 루트입니다.</summary>
    public RectTransform RectTransform => transform as RectTransform;

    /// <summary>소유 지도 UI를 연결하고 프리팹 기본 디자인을 저장합니다.</summary>
    public void Initialize(WayPointMapUI newOwner)
    {
        owner = newOwner;
        ResolveReferences();
        CacheDefaults();

        if (travelButton == null)
        {
            return;
        }

        // 투명한 해금 상태에서도 Fill_BG가 클릭 영역으로 동작하게 유지한다.
        travelButton.targetGraphic = fillBackground;
        travelButton.transition = Selectable.Transition.None;
        travelButton.onClick.RemoveListener(HandleTravelClicked);
        travelButton.onClick.AddListener(HandleTravelClicked);
    }

    /// <summary>현재 웨이포인트 상태와 지도 열기 모드를 새 디자인에 반영합니다.</summary>
    public void Refresh(WayPointRunTime state, WayPointMapOpenMode openMode, bool canTravel)
    {
        if (state == null || state.Definition == null)
        {
            return;
        }

        ResolveReferences();
        CacheDefaults();

        waypointId = state.Id;
        bool isUnlocked = state.IsActive;
        bool isPreviewUnlocked = isUnlocked && openMode == WayPointMapOpenMode.PreviewOnly;

        if (waypointNameText != null)
        {
            waypointNameText.text = string.IsNullOrWhiteSpace(state.DisplayName)
                ? state.Id
                : state.DisplayName;
            waypointNameText.color = isPreviewUnlocked
                ? previewUnavailableNameColor
                : defaultNameColor;
        }

        if (waypointIcon != null)
        {
            Sprite sprite = isUnlocked
                ? state.Definition.tooltipIcon
                : defaultLockedIcon;
            waypointIcon.sprite = sprite;
            waypointIcon.enabled = sprite != null;
        }

        if (fillBackground != null)
        {
            Color color = fillBackground.color;
            color.a = isUnlocked ? 0f : lockedBackgroundAlpha / 255f;
            fillBackground.color = color;
            fillBackground.raycastTarget = true;
        }

        if (travelButton != null)
        {
            travelButton.interactable = canTravel;
        }
    }

    private void OnDestroy()
    {
        if (travelButton != null)
        {
            travelButton.onClick.RemoveListener(HandleTravelClicked);
        }
    }

    /// <summary>아이콘에서 툴팁 버튼으로 마우스를 옮긴 경우 예약된 숨김을 취소합니다.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.NotifyTooltipPointerEnter();
        }
    }

    /// <summary>툴팁에서도 마우스가 벗어나면 현재 툴팁을 숨기도록 요청합니다.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.NotifyTooltipPointerExit();
        }
    }

    private void ResolveReferences()
    {
        if (waypointNameText == null)
        {
            waypointNameText = GetComponentInChildren<TMP_Text>(true);
        }

        if (waypointIcon == null)
        {
            Transform iconTransform = transform.Find("Image");
            waypointIcon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        }

        if (fillBackground == null)
        {
            Transform backgroundTransform = transform.Find("Fill_BG");
            if (backgroundTransform == null)
            {
                backgroundTransform = transform.Find("Fill_Bg");
            }

            fillBackground = backgroundTransform != null
                ? backgroundTransform.GetComponent<Image>()
                : null;
        }

        if (travelButton == null && fillBackground != null)
        {
            travelButton = fillBackground.GetComponent<Button>();
        }
    }

    private void CacheDefaults()
    {
        if (defaultsCached)
        {
            return;
        }

        if (waypointIcon != null)
        {
            defaultLockedIcon = waypointIcon.sprite;
        }

        if (waypointNameText != null)
        {
            defaultNameColor = waypointNameText.color;
        }

        defaultsCached = true;
    }

    private void HandleTravelClicked()
    {
        if (owner == null || string.IsNullOrWhiteSpace(waypointId))
        {
            return;
        }

        owner.TravelTo(waypointId);
    }
}
