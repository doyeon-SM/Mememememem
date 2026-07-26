using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 새 StageButton 프리팹의 하위 UI를 맵 해금 상태에 맞춰 표시합니다.
/// 클릭 이벤트와 맵 선택 자체는 WayPointMapUI가 계속 관리합니다.
/// </summary>
[DisallowMultipleComponent]
public class WayPointMapButtonView : MonoBehaviour
{
    [Header("Stage Button Parts")]
    [SerializeField] private Image fillBackground;
    [SerializeField] private Image outline;
    [SerializeField] private TMP_Text stageNameText;
    [SerializeField] private Image stageIcon;
    [SerializeField] private Image lockOverlay;
    [SerializeField] private Button button;

    [Header("Locked")]
    [Range(0, 255)]
    [SerializeField] private int lockedOverlayAlpha = 200;

    private Sprite defaultLockedIcon;
    private Color defaultFillBackgroundColor;
    private Color defaultTextColor;
    private bool defaultsCached;

    public Button Button
    {
        get
        {
            ResolveReferences();
            return button;
        }
    }

    private void Awake()
    {
        ResolveReferences();
        CacheDefaults();
    }

    private void Reset()
    {
        ResolveReferences();
        CacheDefaults();
    }

    private void OnValidate()
    {
        lockedOverlayAlpha = Mathf.Clamp(lockedOverlayAlpha, 0, 255);
        ResolveReferences();
        CacheDefaults();
    }

    /// <summary>맵 이름, 아이콘, 잠금 오버레이 및 선택 배경을 한 번에 갱신합니다.</summary>
    public void Refresh(
        WayPointMapDefinition mapDefinition,
        string displayName,
        bool isAvailable)
    {
        ResolveReferences();
        CacheDefaults();

        if (button != null)
        {
            button.transition = Selectable.Transition.None;
            button.interactable = isAvailable;
        }

        if (stageNameText != null)
        {
            stageNameText.text = displayName;
            stageNameText.color = defaultTextColor;
        }

        if (stageIcon != null)
        {
            Sprite icon = isAvailable && mapDefinition != null
                ? mapDefinition.stageButtonSprite
                : defaultLockedIcon;

            stageIcon.sprite = icon;
            stageIcon.enabled = icon != null;
        }

        if (lockOverlay != null)
        {
            Color overlayColor = lockOverlay.color;
            overlayColor.a = isAvailable ? 0f : lockedOverlayAlpha / 255f;
            lockOverlay.color = overlayColor;
            lockOverlay.raycastTarget = true;
        }

        if (fillBackground != null)
        {
            // 선택 여부와 관계없이 디자이너가 프리팹에 설정한 원본 배경색을 유지합니다.
            // 잠금 표현은 잠금 아이콘과 FillBG (1) 오버레이만 담당합니다.
            fillBackground.color = defaultFillBackgroundColor;
        }
    }

    private void CacheDefaults()
    {
        if (defaultsCached)
        {
            return;
        }

        defaultLockedIcon = stageIcon != null ? stageIcon.sprite : null;
        defaultFillBackgroundColor = fillBackground != null
            ? fillBackground.color
            : Color.black;
        defaultTextColor = stageNameText != null
            ? stageNameText.color
            : Color.white;
        defaultsCached = true;
    }

    private void ResolveReferences()
    {
        Transform root = transform;

        if (fillBackground == null)
        {
            fillBackground = FindDirectChildComponent<Image>(root, "FillBG");
        }

        if (outline == null)
        {
            outline = FindDirectChildComponent<Image>(root, "outline");
        }

        if (stageNameText == null)
        {
            stageNameText = FindDirectChildComponent<TMP_Text>(root, "Text (TMP)");
        }

        if (stageIcon == null)
        {
            stageIcon = FindDirectChildComponent<Image>(root, "Image");
        }

        if (lockOverlay == null)
        {
            lockOverlay = FindDirectChildComponent<Image>(root, "FillBG (1)");
        }

        if (button == null && lockOverlay != null)
        {
            button = lockOverlay.GetComponent<Button>();
        }

        if (button == null)
        {
            button = GetComponentInChildren<Button>(true);
        }
    }

    private static T FindDirectChildComponent<T>(Transform root, string childName)
        where T : Component
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != null && child.name == childName)
            {
                return child.GetComponent<T>();
            }
        }

        return null;
    }
}
