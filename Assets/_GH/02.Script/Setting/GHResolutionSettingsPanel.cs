using System.Collections;
using System.Collections.Generic;
using KMS.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(Image))]
[AddComponentMenu("GH/UI/Resolution And Audio Settings Panel")]
public sealed class GHResolutionSettingsPanel : MonoBehaviour
{
    private struct SettingsSnapshot
    {
        public int resolutionIndex;
        public int terrainQualityIndex;
        public bool fullScreen;
        public float masterVolume;
        public float musicVolume;
        public float sfxVolume;
    }

    [System.Serializable]
    private sealed class TerrainQualityPreset
    {
        [Min(0f)] public float detailDistance;
        [Min(0f)] public float treeDistance;
        [Range(0f, 100f)] public float detailDensityPercent;
        [Min(0)] public int activeRange;

        public TerrainQualityPreset(
            float newDetailDistance,
            float newTreeDistance,
            float newDetailDensityPercent,
            int newActiveRange)
        {
            detailDistance = newDetailDistance;
            treeDistance = newTreeDistance;
            detailDensityPercent = newDetailDensityPercent;
            activeRange = newActiveRange;
        }

        public void ClampValues()
        {
            detailDistance = Mathf.Max(0f, detailDistance);
            treeDistance = Mathf.Max(0f, treeDistance);
            detailDensityPercent = Mathf.Clamp(detailDensityPercent, 0f, 100f);
            activeRange = Mathf.Max(0, activeRange);
        }
    }

    private const string RuntimeRootName = "Runtime Settings UI";
    private const string WidthPreferenceKey = "GH.Resolution.Width";
    private const string HeightPreferenceKey = "GH.Resolution.Height";
    private const string FullScreenPreferenceKey = "GH.Resolution.FullScreen";
    private const string MasterVolumePreferenceKey = "GH.Audio.MasterVolume";
    private const string SfxVolumePreferenceKey = "KMS.Audio.SfxVolume";
    private const string MusicVolumePreferenceKey = "KMS.Audio.MusicVolume";
    private const string TerrainQualityPreferenceKey = "GH.Graphics.TerrainQuality";
    private const string DetailDistancePreferenceKey = "GH.Graphics.DetailDistance";
    private const string TreeDistancePreferenceKey = "GH.Graphics.TreeDistance";
    private const string DetailDensityPreferenceKey = "GH.Graphics.DetailDensity";
    private const string ChunkActiveRangePreferenceKey = "GH.Graphics.ChunkActiveRange";
    private const int DefaultResolutionWidth = 1920;
    private const int DefaultResolutionHeight = 1080;

    private static readonly Vector2Int[] SupportedResolutionPresets =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1366, 768),
        new Vector2Int(1600, 900),
        new Vector2Int(DefaultResolutionWidth, DefaultResolutionHeight)
    };

    [Header("UI Assets")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [Tooltip("Git에 포함된 Modern UI Pack Rounded Filled 스프라이트를 사용합니다.")]
    [SerializeField] private Sprite roundedFilledSprite;

    [Header("Panel Style")]
    [SerializeField] private Color overlayColor = new Color(0.015f, 0.025f, 0.05f, 0.72f);
    [SerializeField] private Color panelColor = new Color(0.075f, 0.105f, 0.16f, 0.99f);
    [SerializeField] private Color rowColor = new Color(0.045f, 0.065f, 0.1f, 0.98f);
    [SerializeField] private Color buttonColor = new Color(0.13f, 0.42f, 0.62f, 1f);
    [SerializeField] private Color applyButtonColor = new Color(0.12f, 0.62f, 0.45f, 1f);
    [SerializeField] private Color sliderFillColor = new Color(0.22f, 0.72f, 0.88f, 1f);
    [SerializeField] private Color textColor = new Color(0.95f, 0.98f, 1f, 1f);

    [Header("Terrain And Chunk Quality Presets")]
    [InspectorName("하")]
    [SerializeField] private TerrainQualityPreset lowTerrainQuality =
        new TerrainQualityPreset(75f, 100f, 50f, 1);
    [InspectorName("중")]
    [SerializeField] private TerrainQualityPreset mediumTerrainQuality =
        new TerrainQualityPreset(125f, 175f, 75f, 1);
    [InspectorName("상")]
    [SerializeField] private TerrainQualityPreset highTerrainQuality =
        new TerrainQualityPreset(225f, 325f, 100f, 2);

    [Header("Generated UI References")]
    [SerializeField] private RectTransform runtimeUiRoot;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button screenModeButton;
    [SerializeField] private Button applyButton;
    [SerializeField] private Button resetDefaultsButton;
    [SerializeField] private TMP_Text resolutionValueText;
    [SerializeField] private TMP_Text screenModeText;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMP_InputField masterVolumeInput;
    [SerializeField] private TMP_InputField musicVolumeInput;
    [SerializeField] private TMP_InputField sfxVolumeInput;
    [SerializeField] private TMP_Dropdown terrainQualityDropdown;

    private readonly List<Vector2Int> resolutionOptions = new List<Vector2Int>();
    private int selectedResolutionIndex;
    private int selectedTerrainQualityIndex = 1;
    private bool selectedFullScreen;
    private bool isInitialized;
    private bool isRefreshingAudioUi;
    private bool hasCommittedSnapshot;
    private SettingsSnapshot committedSnapshot;
    private CanvasGroup applyFadeCanvasGroup;
    private Coroutine applyCloseFadeCoroutine;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        applyCloseFadeCoroutine = null;
        ResetApplyFadeVisual();
        Initialize();
        LoadCommittedSettingsIntoUi();
        CaptureCommittedSnapshot();
        RefreshAllSettingVisuals();
    }

    private void OnDisable()
    {
        if (applyCloseFadeCoroutine != null)
        {
            StopCoroutine(applyCloseFadeCoroutine);
        }

        applyCloseFadeCoroutine = null;
        ResetApplyFadeVisual();

        if (!isInitialized || !hasCommittedSnapshot)
        {
            return;
        }

        RestoreCommittedSnapshot();
    }

    public void PreviousResolution()
    {
        if (resolutionOptions.Count == 0)
        {
            return;
        }

        selectedResolutionIndex =
            (selectedResolutionIndex - 1 + resolutionOptions.Count) % resolutionOptions.Count;
        RefreshAllSettingVisuals();
    }

    public void NextResolution()
    {
        if (resolutionOptions.Count == 0)
        {
            return;
        }

        selectedResolutionIndex = (selectedResolutionIndex + 1) % resolutionOptions.Count;
        RefreshAllSettingVisuals();
    }

    public void ToggleScreenMode()
    {
        selectedFullScreen = !selectedFullScreen;
        RefreshAllSettingVisuals();
    }

    // 기존에 이 메서드를 연결해 둔 경우를 위해 이름을 유지합니다.
    public void ApplyResolution()
    {
        ApplySettings();
    }

    public void ApplySettings()
    {
        if (resolutionOptions.Count > 0)
        {
            Vector2Int resolution = resolutionOptions[
                Mathf.Clamp(selectedResolutionIndex, 0, resolutionOptions.Count - 1)];
            FullScreenMode mode = selectedFullScreen
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;

            Screen.SetResolution(resolution.x, resolution.y, mode);
            PlayerPrefs.SetInt(WidthPreferenceKey, resolution.x);
            PlayerPrefs.SetInt(HeightPreferenceKey, resolution.y);
            PlayerPrefs.SetInt(FullScreenPreferenceKey, selectedFullScreen ? 1 : 0);
        }

        ApplyAudioValues();
        ApplySelectedTerrainQuality();
        PlayerPrefs.Save();
        CaptureCommittedSnapshot();
        RefreshApplyButtonState();
        TryStartApplyCloseFade();
    }

    private void TryStartApplyCloseFade()
    {
        if (applyCloseFadeCoroutine != null
            || SceneUIManager.Instance == null
            || !SceneUIManager.Instance.TryGetSettingsSubPanelApplyFadeDuration(
                out float fadeDuration))
        {
            return;
        }

        applyCloseFadeCoroutine = StartCoroutine(FadeAndCloseThisPanel(fadeDuration));
    }

    private IEnumerator FadeAndCloseThisPanel(float duration)
    {
        CanvasGroup canvasGroup = GetOrCreateApplyFadeCanvasGroup();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration && gameObject.activeInHierarchy)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        applyCloseFadeCoroutine = null;
        gameObject.SetActive(false);
    }

    private CanvasGroup GetOrCreateApplyFadeCanvasGroup()
    {
        if (applyFadeCanvasGroup == null)
        {
            applyFadeCanvasGroup = GetComponent<CanvasGroup>();
            if (applyFadeCanvasGroup == null)
            {
                applyFadeCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        return applyFadeCanvasGroup;
    }

    private void ResetApplyFadeVisual()
    {
        if (applyFadeCanvasGroup == null)
        {
            return;
        }

        applyFadeCanvasGroup.alpha = 1f;
        applyFadeCanvasGroup.interactable = true;
        applyFadeCanvasGroup.blocksRaycasts = true;
    }

    /// <summary>기본 해상도, 전체 화면, 중간 품질, 최대 음량을 편집 값으로 불러옵니다.</summary>
    public void RestoreDefaults()
    {
        selectedResolutionIndex = FindClosestResolutionIndex(
            resolutionOptions,
            DefaultResolutionWidth,
            DefaultResolutionHeight);
        selectedFullScreen = true;
        selectedTerrainQualityIndex = 1;

        isRefreshingAudioUi = true;
        masterVolumeSlider.SetValueWithoutNotify(1f);
        musicVolumeSlider.SetValueWithoutNotify(1f);
        sfxVolumeSlider.SetValueWithoutNotify(1f);
        isRefreshingAudioUi = false;

        ApplyAudioPreview();
        RefreshAllSettingVisuals();
    }

    /// <summary>편집 중인 값을 취소하고 마지막 적용 상태로 돌아간 뒤 패널을 닫습니다.</summary>
    public void CancelChanges()
    {
        if (hasCommittedSnapshot)
        {
            RestoreCommittedSnapshot();
        }

        gameObject.SetActive(false);
    }

    private void Initialize()
    {
        if (isInitialized)
        {
            return;
        }

        ConfigureRootOverlay();
        BuildRuntimeUi();
        EnsureActionButtons();
        BuildResolutionOptions();
        BindControls();
        isInitialized = true;
    }

    private void ConfigureRootOverlay()
    {
        RectTransform rootRect = (RectTransform)transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = Vector2.zero;

        Image overlay = GetComponent<Image>();
        overlay.color = overlayColor;
        overlay.raycastTarget = true;
    }

    private void BuildRuntimeUi()
    {
        if (runtimeUiRoot != null
            && previousButton != null
            && nextButton != null
            && screenModeButton != null
            && applyButton != null
            && resolutionValueText != null
            && screenModeText != null
            && masterVolumeSlider != null
            && musicVolumeSlider != null
            && sfxVolumeSlider != null
            && masterVolumeInput != null
            && musicVolumeInput != null
            && sfxVolumeInput != null)
        {
            return;
        }

        Transform existingRoot = transform.Find(RuntimeRootName);
        if (existingRoot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(existingRoot.gameObject);
            }
            else
            {
                DestroyImmediate(existingRoot.gameObject);
            }
        }

        RectTransform panel = CreateImageObject(
            RuntimeRootName,
            transform,
            new Vector2(760f, 900f),
            Vector2.zero,
            panelColor);
        runtimeUiRoot = panel;

        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.55f, 0.78f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        CreateText(
            "Title",
            panel,
            "화면 및 사운드 설정",
            38f,
            new Vector2(650f, 58f),
            new Vector2(0f, 390f),
            FontStyles.Bold);

        CreateText(
            "Resolution Label",
            panel,
            "게임 해상도",
            23f,
            new Vector2(560f, 38f),
            new Vector2(0f, 336f),
            FontStyles.Bold);

        previousButton = CreateButton(
            "Previous Resolution",
            panel,
            "〈",
            new Vector2(74f, 58f),
            new Vector2(-282f, 284f),
            buttonColor);

        resolutionValueText = CreateText(
            "Resolution Value",
            panel,
            string.Empty,
            28f,
            new Vector2(430f, 58f),
            new Vector2(0f, 284f),
            FontStyles.Bold);

        nextButton = CreateButton(
            "Next Resolution",
            panel,
            "〉",
            new Vector2(74f, 58f),
            new Vector2(282f, 284f),
            buttonColor);

        screenModeButton = CreateButton(
            "Screen Mode",
            panel,
            string.Empty,
            new Vector2(650f, 54f),
            new Vector2(0f, 217f),
            new Color(0.13f, 0.18f, 0.28f, 1f),
            out screenModeText);

        CreateImageObject(
            "Section Divider",
            panel,
            new Vector2(650f, 2f),
            new Vector2(0f, 176f),
            new Color(0.2f, 0.4f, 0.58f, 0.55f));

        CreateText(
            "Graphics Label",
            panel,
            "그래픽",
            25f,
            new Vector2(620f, 40f),
            new Vector2(0f, 148f),
            FontStyles.Bold);

        CreateTerrainQualityRow(
            panel,
            new Vector2(0f, 91f),
            out terrainQualityDropdown);

        CreateImageObject(
            "Graphics Section Divider",
            panel,
            new Vector2(650f, 2f),
            new Vector2(0f, 47f),
            new Color(0.2f, 0.4f, 0.58f, 0.55f));

        CreateText(
            "Audio Label",
            panel,
            "사운드",
            25f,
            new Vector2(620f, 40f),
            new Vector2(0f, 20f),
            FontStyles.Bold);

        CreateVolumeRow(
            "Master Volume",
            panel,
            "MASTER",
            new Vector2(0f, -44f),
            out masterVolumeSlider,
            out masterVolumeInput);

        CreateVolumeRow(
            "Music Volume",
            panel,
            "BGM",
            new Vector2(0f, -126f),
            out musicVolumeSlider,
            out musicVolumeInput);

        CreateVolumeRow(
            "SFX Volume",
            panel,
            "SFX",
            new Vector2(0f, -208f),
            out sfxVolumeSlider,
            out sfxVolumeInput);

        resetDefaultsButton = CreateButton(
            "Reset Defaults",
            panel,
            "기본값 복원",
            new Vector2(280f, 62f),
            new Vector2(-155f, -322f),
            buttonColor);

        applyButton = CreateButton(
            "Apply",
            panel,
            "적용",
            new Vector2(280f, 62f),
            new Vector2(155f, -322f),
            applyButtonColor);

        CreateText(
            "Guide",
            panel,
            "사운드는 미리 들을 수 있으며, 적용 버튼을 눌러야 전체 설정이 저장됩니다.",
            17f,
            new Vector2(660f, 32f),
            new Vector2(0f, -395f));
    }

    private void EnsureActionButtons()
    {
        if (runtimeUiRoot == null || applyButton == null)
        {
            return;
        }

        if (resetDefaultsButton == null)
        {
            Transform existingReset = runtimeUiRoot.Find("Reset Defaults");
            resetDefaultsButton = existingReset != null
                ? existingReset.GetComponent<Button>()
                : null;
        }

        Transform guideTransform = runtimeUiRoot.Find("Guide");
        TMP_Text guideText = guideTransform != null
            ? guideTransform.GetComponent<TMP_Text>()
            : null;
        if (guideText != null)
        {
            guideText.text =
                "사운드는 미리 들을 수 있으며, 적용 버튼을 눌러야 전체 설정이 저장됩니다.";
        }
    }

    private void CreateTerrainQualityRow(
        Transform parent,
        Vector2 anchoredPosition,
        out TMP_Dropdown dropdown)
    {
        RectTransform row = CreateImageObject(
            "Terrain Quality",
            parent,
            new Vector2(650f, 70f),
            anchoredPosition,
            rowColor);

        CreateImageObject(
            "Accent",
            row,
            new Vector2(4f, 34f),
            new Vector2(-313f, 0f),
            sliderFillColor);

        TMP_Text labelText = CreateText(
            "Label",
            row,
            "렌더 거리",
            19f,
            new Vector2(180f, 42f),
            new Vector2(-215f, 0f),
            FontStyles.Bold);
        labelText.alignment = TextAlignmentOptions.Left;

        dropdown = CreateTerrainQualityDropdown(
            "Quality Dropdown",
            row,
            new Vector2(380f, 48f),
            new Vector2(105f, 0f));
    }

    private void CreateVolumeRow(
        string objectName,
        Transform parent,
        string label,
        Vector2 anchoredPosition,
        out Slider slider,
        out TMP_InputField inputField)
    {
        RectTransform row = CreateImageObject(
            objectName,
            parent,
            new Vector2(650f, 70f),
            anchoredPosition,
            rowColor);

        CreateImageObject(
            "Accent",
            row,
            new Vector2(4f, 34f),
            new Vector2(-313f, 0f),
            sliderFillColor);

        TMP_Text labelText = CreateText(
            "Label",
            row,
            label,
            19f,
            new Vector2(130f, 42f),
            new Vector2(-238f, 0f),
            FontStyles.Bold);
        labelText.alignment = TextAlignmentOptions.Left;

        slider = CreateModernInputSlider(
            "Slider",
            row,
            new Vector2(340f, 36f),
            new Vector2(18f, 0f));

        inputField = CreateNumericInput(
            "Input",
            row,
            new Vector2(84f, 44f),
            new Vector2(278f, 0f));
    }

    private TMP_Dropdown CreateTerrainQualityDropdown(
        string objectName,
        Transform parent,
        Vector2 size,
        Vector2 anchoredPosition)
    {
        RectTransform root = CreateImageObject(
            objectName,
            parent,
            size,
            anchoredPosition,
            new Color(0.095f, 0.13f, 0.2f, 1f));
        Image rootImage = root.GetComponent<Image>();

        TMP_Text caption = CreateText(
            "Label",
            root,
            "중",
            21f,
            new Vector2(size.x - 70f, size.y),
            new Vector2(-18f, 0f),
            FontStyles.Bold);
        caption.alignment = TextAlignmentOptions.Left;

        CreateText(
            "Arrow",
            root,
            "▼",
            17f,
            new Vector2(48f, size.y),
            new Vector2(size.x * 0.5f - 28f, 0f),
            FontStyles.Bold);

        RectTransform template = CreateImageObject(
            "Template",
            root,
            Vector2.zero,
            Vector2.zero,
            new Color(0.045f, 0.065f, 0.1f, 0.99f));
        template.anchorMin = new Vector2(0f, 0f);
        template.anchorMax = new Vector2(1f, 0f);
        template.pivot = new Vector2(0.5f, 1f);
        template.anchoredPosition = new Vector2(0f, -4f);
        template.sizeDelta = new Vector2(0f, 132f);

        ScrollRect scrollRect = template.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        RectTransform viewport = CreateImageObject(
            "Viewport",
            template,
            Vector2.zero,
            Vector2.zero,
            new Color(1f, 1f, 1f, 0.01f));
        StretchRect(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateRectObject(
            "Content",
            viewport,
            Vector2.zero,
            Vector2.zero);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 126f);

        RectTransform item = CreateImageObject(
            "Item",
            content,
            Vector2.zero,
            Vector2.zero,
            new Color(0.075f, 0.105f, 0.16f, 1f));
        item.anchorMin = new Vector2(0f, 1f);
        item.anchorMax = new Vector2(1f, 1f);
        item.pivot = new Vector2(0.5f, 1f);
        item.anchoredPosition = Vector2.zero;
        item.sizeDelta = new Vector2(0f, 42f);

        Image itemImage = item.GetComponent<Image>();
        Toggle itemToggle = item.gameObject.AddComponent<Toggle>();
        itemToggle.targetGraphic = itemImage;

        TMP_Text itemLabel = CreateText(
            "Item Label",
            item,
            "중",
            20f,
            new Vector2(size.x - 32f, 42f),
            Vector2.zero,
            FontStyles.Bold);
        itemLabel.alignment = TextAlignmentOptions.Left;

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        template.gameObject.SetActive(false);

        TMP_Dropdown dropdown = root.gameObject.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = rootImage;
        dropdown.template = template;
        dropdown.captionText = (TextMeshProUGUI)caption;
        dropdown.itemText = (TextMeshProUGUI)itemLabel;
        dropdown.options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("하"),
            new TMP_Dropdown.OptionData("중"),
            new TMP_Dropdown.OptionData("상")
        };
        dropdown.SetValueWithoutNotify(1);

        ColorBlock colors = dropdown.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.88f, 0.98f, 1f, 1f);
        colors.pressedColor = new Color(0.72f, 0.8f, 0.9f, 1f);
        colors.selectedColor = colors.highlightedColor;
        dropdown.colors = colors;
        return dropdown;
    }

    private Slider CreateModernInputSlider(
        string objectName,
        Transform parent,
        Vector2 size,
        Vector2 anchoredPosition)
    {
        RectTransform sliderRect = CreateRectObject(objectName, parent, size, anchoredPosition);

        RectTransform background = CreateImageObject(
            "Background",
            sliderRect,
            new Vector2(size.x, 10f),
            Vector2.zero,
            new Color(1f, 1f, 1f, 0.18f));

        RectTransform fillArea = CreateRectObject(
            "Fill Area",
            sliderRect,
            new Vector2(size.x - 20f, 10f),
            Vector2.zero);
        RectTransform fill = CreateImageObject(
            "Fill",
            fillArea,
            Vector2.zero,
            Vector2.zero,
            sliderFillColor);
        StretchRect(fill);

        RectTransform handleArea = CreateRectObject(
            "Handle Slide Area",
            sliderRect,
            new Vector2(size.x - 20f, size.y),
            Vector2.zero);
        RectTransform handle = CreateImageObject(
            "Handle",
            handleArea,
            new Vector2(26f, 26f),
            Vector2.zero,
            Color.white);

        Image backgroundImage = background.GetComponent<Image>();
        Image fillImage = fill.GetComponent<Image>();
        Image handleImage = handle.GetComponent<Image>();
        ConfigureRoundedImage(backgroundImage);
        ConfigureRoundedImage(fillImage);
        ConfigureRoundedImage(handleImage);

        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.value = 1f;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;

        ColorBlock colors = slider.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.84f, 0.96f, 1f, 1f);
        colors.pressedColor = new Color(0.68f, 0.86f, 0.94f, 1f);
        colors.selectedColor = colors.highlightedColor;
        slider.colors = colors;
        return slider;
    }

    private TMP_InputField CreateNumericInput(
        string objectName,
        Transform parent,
        Vector2 size,
        Vector2 anchoredPosition)
    {
        RectTransform inputRect = CreateImageObject(
            objectName,
            parent,
            size,
            anchoredPosition,
            new Color(0.095f, 0.13f, 0.2f, 1f));
        Image background = inputRect.GetComponent<Image>();
        ConfigureRoundedImage(background);

        Outline inputOutline = inputRect.gameObject.AddComponent<Outline>();
        inputOutline.effectColor = new Color(
            sliderFillColor.r,
            sliderFillColor.g,
            sliderFillColor.b,
            0.65f);
        inputOutline.effectDistance = new Vector2(1f, -1f);

        RectTransform textArea = CreateRectObject(
            "Text Area",
            inputRect,
            new Vector2(size.x - 12f, size.y - 8f),
            Vector2.zero);
        textArea.gameObject.AddComponent<RectMask2D>();

        TMP_Text inputText = CreateText(
            "Text",
            textArea,
            "100",
            19f,
            new Vector2(size.x - 14f, size.y - 8f),
            Vector2.zero,
            FontStyles.Bold);
        inputText.raycastTarget = false;

        TMP_InputField input = inputRect.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = background;
        input.textViewport = textArea;
        input.textComponent = (TextMeshProUGUI)inputText;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 3;
        input.text = "100";
        input.selectionColor = new Color(
            sliderFillColor.r,
            sliderFillColor.g,
            sliderFillColor.b,
            0.55f);
        return input;
    }

    private void BuildResolutionOptions()
    {
        PopulateAvailableResolutionOptions(resolutionOptions);
    }

    private static void PopulateAvailableResolutionOptions(List<Vector2Int> target)
    {
        target.Clear();
        Resolution[] displayResolutions = Screen.resolutions;
        bool hasDisplayResolutionInfo = displayResolutions != null
            && displayResolutions.Length > 0;

        for (int i = 0; i < SupportedResolutionPresets.Length; i++)
        {
            Vector2Int preset = SupportedResolutionPresets[i];
            if (!hasDisplayResolutionInfo || IsResolutionSupportedByDisplay(
                    preset,
                    displayResolutions))
            {
                target.Add(preset);
            }
        }

        // 일부 플랫폼과 에디터에서는 Screen.resolutions가 비거나 제한적으로 반환될 수 있습니다.
        // 그 경우에도 설정 UI에는 합의된 네 가지 해상도만 노출합니다.
        if (target.Count == 0)
        {
            target.AddRange(SupportedResolutionPresets);
        }
    }

    private static bool IsResolutionSupportedByDisplay(
        Vector2Int target,
        Resolution[] displayResolutions)
    {
        for (int i = 0; i < displayResolutions.Length; i++)
        {
            Resolution resolution = displayResolutions[i];
            if (resolution.width == target.x && resolution.height == target.y)
            {
                return true;
            }
        }

        return false;
    }

    private void BindControls()
    {
        previousButton.onClick.RemoveListener(PreviousResolution);
        nextButton.onClick.RemoveListener(NextResolution);
        screenModeButton.onClick.RemoveListener(ToggleScreenMode);
        applyButton.onClick.RemoveListener(ApplySettings);
        previousButton.onClick.AddListener(PreviousResolution);
        nextButton.onClick.AddListener(NextResolution);
        screenModeButton.onClick.AddListener(ToggleScreenMode);
        applyButton.onClick.AddListener(ApplySettings);
        if (resetDefaultsButton != null)
        {
            resetDefaultsButton.onClick.RemoveListener(RestoreDefaults);
            resetDefaultsButton.onClick.AddListener(RestoreDefaults);
        }

        masterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);
        musicVolumeSlider.onValueChanged.AddListener(HandleMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);

        masterVolumeInput.onEndEdit.AddListener(HandleMasterVolumeInput);
        musicVolumeInput.onEndEdit.AddListener(HandleMusicVolumeInput);
        sfxVolumeInput.onEndEdit.AddListener(HandleSfxVolumeInput);
        if (terrainQualityDropdown != null)
        {
            terrainQualityDropdown.onValueChanged.AddListener(
                HandleTerrainQualityChanged);
        }
    }

    private void LoadTerrainQualitySelection()
    {
        selectedTerrainQualityIndex = Mathf.Clamp(
            PlayerPrefs.GetInt(TerrainQualityPreferenceKey, 1),
            0,
            2);

        if (terrainQualityDropdown != null)
        {
            terrainQualityDropdown.SetValueWithoutNotify(selectedTerrainQualityIndex);
            terrainQualityDropdown.RefreshShownValue();
        }
    }

    private void HandleTerrainQualityChanged(int value)
    {
        selectedTerrainQualityIndex = Mathf.Clamp(value, 0, 2);
        RefreshApplyButtonState();
    }

    private void LoadAudioValues()
    {
        float master = Mathf.Clamp01(
            PlayerPrefs.GetFloat(MasterVolumePreferenceKey, AudioListener.volume));
        float music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePreferenceKey, 1f));
        float sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePreferenceKey, 1f));

        isRefreshingAudioUi = true;
        masterVolumeSlider.SetValueWithoutNotify(master);
        musicVolumeSlider.SetValueWithoutNotify(music);
        sfxVolumeSlider.SetValueWithoutNotify(sfx);
        RefreshVolumeInput(masterVolumeInput, master);
        RefreshVolumeInput(musicVolumeInput, music);
        RefreshVolumeInput(sfxVolumeInput, sfx);
        isRefreshingAudioUi = false;

        ApplyAudioPreview();
    }

    private void LoadCommittedSettingsIntoUi()
    {
        SelectCurrentResolution();
        LoadAudioValues();
        LoadTerrainQualitySelection();
    }

    private void CaptureCommittedSnapshot()
    {
        committedSnapshot = CreateCurrentSnapshot();
        hasCommittedSnapshot = true;
    }

    private SettingsSnapshot CreateCurrentSnapshot()
    {
        return new SettingsSnapshot
        {
            resolutionIndex = selectedResolutionIndex,
            terrainQualityIndex = selectedTerrainQualityIndex,
            fullScreen = selectedFullScreen,
            masterVolume = masterVolumeSlider != null ? masterVolumeSlider.value : 1f,
            musicVolume = musicVolumeSlider != null ? musicVolumeSlider.value : 1f,
            sfxVolume = sfxVolumeSlider != null ? sfxVolumeSlider.value : 1f
        };
    }

    private void RestoreCommittedSnapshot()
    {
        selectedResolutionIndex = committedSnapshot.resolutionIndex;
        selectedTerrainQualityIndex = committedSnapshot.terrainQualityIndex;
        selectedFullScreen = committedSnapshot.fullScreen;

        isRefreshingAudioUi = true;
        masterVolumeSlider.SetValueWithoutNotify(committedSnapshot.masterVolume);
        musicVolumeSlider.SetValueWithoutNotify(committedSnapshot.musicVolume);
        sfxVolumeSlider.SetValueWithoutNotify(committedSnapshot.sfxVolume);
        isRefreshingAudioUi = false;

        // KMSAudioService의 미리듣기 setter가 PlayerPrefs 메모리 값도 갱신하므로
        // 취소 시 마지막 적용 값으로 함께 되돌립니다. 디스크 저장은 하지 않습니다.
        PlayerPrefs.SetFloat(MasterVolumePreferenceKey, committedSnapshot.masterVolume);
        PlayerPrefs.SetFloat(MusicVolumePreferenceKey, committedSnapshot.musicVolume);
        PlayerPrefs.SetFloat(SfxVolumePreferenceKey, committedSnapshot.sfxVolume);
        ApplyAudioPreview();
        RefreshAllSettingVisuals();
    }

    private void RefreshAllSettingVisuals()
    {
        RefreshResolutionLabels();
        RefreshVolumeInput(
            masterVolumeInput,
            masterVolumeSlider != null ? masterVolumeSlider.value : 1f);
        RefreshVolumeInput(
            musicVolumeInput,
            musicVolumeSlider != null ? musicVolumeSlider.value : 1f);
        RefreshVolumeInput(
            sfxVolumeInput,
            sfxVolumeSlider != null ? sfxVolumeSlider.value : 1f);

        if (terrainQualityDropdown != null)
        {
            terrainQualityDropdown.SetValueWithoutNotify(selectedTerrainQualityIndex);
            terrainQualityDropdown.RefreshShownValue();
        }

        RefreshApplyButtonState();
    }

    private void RefreshApplyButtonState()
    {
        if (applyButton == null)
        {
            return;
        }

        applyButton.interactable = hasCommittedSnapshot && HasUnappliedChanges();
    }

    private bool HasUnappliedChanges()
    {
        SettingsSnapshot current = CreateCurrentSnapshot();
        return current.resolutionIndex != committedSnapshot.resolutionIndex
            || current.terrainQualityIndex != committedSnapshot.terrainQualityIndex
            || current.fullScreen != committedSnapshot.fullScreen
            || !Mathf.Approximately(current.masterVolume, committedSnapshot.masterVolume)
            || !Mathf.Approximately(current.musicVolume, committedSnapshot.musicVolume)
            || !Mathf.Approximately(current.sfxVolume, committedSnapshot.sfxVolume);
    }

    private void HandleMasterVolumeChanged(float value)
    {
        if (isRefreshingAudioUi)
        {
            return;
        }

        RefreshVolumeInput(masterVolumeInput, value);
        AudioListener.volume = Mathf.Clamp01(value);
        RefreshApplyButtonState();
    }

    private void HandleMusicVolumeChanged(float value)
    {
        if (isRefreshingAudioUi)
        {
            return;
        }

        RefreshVolumeInput(musicVolumeInput, value);
        KMSAudioService.SetMusicVolume(value);
        RefreshApplyButtonState();
    }

    private void HandleSfxVolumeChanged(float value)
    {
        if (isRefreshingAudioUi)
        {
            return;
        }

        RefreshVolumeInput(sfxVolumeInput, value);
        KMSAudioService.SetSfxVolume(value);
        RefreshApplyButtonState();
    }

    private void HandleMasterVolumeInput(string value)
    {
        SetSliderFromInput(masterVolumeSlider, masterVolumeInput, value);
    }

    private void HandleMusicVolumeInput(string value)
    {
        SetSliderFromInput(musicVolumeSlider, musicVolumeInput, value);
    }

    private void HandleSfxVolumeInput(string value)
    {
        SetSliderFromInput(sfxVolumeSlider, sfxVolumeInput, value);
    }

    private static void SetSliderFromInput(
        Slider slider,
        TMP_InputField input,
        string value)
    {
        if (!float.TryParse(value, out float percentage))
        {
            RefreshVolumeInput(input, slider.value);
            return;
        }

        slider.value = Mathf.Clamp(percentage, 0f, 100f) * 0.01f;
        RefreshVolumeInput(input, slider.value);
    }

    private static void RefreshVolumeInput(TMP_InputField input, float value)
    {
        if (input != null)
        {
            input.SetTextWithoutNotify(Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString());
        }
    }

    private void ApplyAudioValues()
    {
        float master = masterVolumeSlider != null ? masterVolumeSlider.value : 1f;
        float music = musicVolumeSlider != null ? musicVolumeSlider.value : 1f;
        float sfx = sfxVolumeSlider != null ? sfxVolumeSlider.value : 1f;

        AudioListener.volume = Mathf.Clamp01(master);
        PlayerPrefs.SetFloat(MasterVolumePreferenceKey, AudioListener.volume);
        KMSAudioService.SetMusicVolume(music);
        KMSAudioService.SetSfxVolume(sfx);
    }

    private void ApplyAudioPreview()
    {
        float master = masterVolumeSlider != null ? masterVolumeSlider.value : 1f;
        float music = musicVolumeSlider != null ? musicVolumeSlider.value : 1f;
        float sfx = sfxVolumeSlider != null ? sfxVolumeSlider.value : 1f;

        AudioListener.volume = Mathf.Clamp01(master);
        KMSAudioService.SetMusicVolume(music);
        KMSAudioService.SetSfxVolume(sfx);
    }

    private void ApplySelectedTerrainQuality()
    {
        TerrainQualityPreset preset = GetSelectedTerrainQualityPreset();
        if (preset == null)
        {
            return;
        }

        preset.ClampValues();
        ApplyTerrainAndChunkValues(
            preset.detailDistance,
            preset.treeDistance,
            preset.detailDensityPercent,
            preset.activeRange);

        PlayerPrefs.SetInt(TerrainQualityPreferenceKey, selectedTerrainQualityIndex);
        PlayerPrefs.SetFloat(DetailDistancePreferenceKey, preset.detailDistance);
        PlayerPrefs.SetFloat(TreeDistancePreferenceKey, preset.treeDistance);
        PlayerPrefs.SetFloat(DetailDensityPreferenceKey, preset.detailDensityPercent);
        PlayerPrefs.SetInt(ChunkActiveRangePreferenceKey, preset.activeRange);
    }

    private TerrainQualityPreset GetSelectedTerrainQualityPreset()
    {
        switch (Mathf.Clamp(selectedTerrainQualityIndex, 0, 2))
        {
            case 0:
                return lowTerrainQuality;
            case 2:
                return highTerrainQuality;
            default:
                return mediumTerrainQuality;
        }
    }

    private static void ApplyTerrainAndChunkValues(
        float detailDistance,
        float treeDistance,
        float detailDensityPercent,
        int activeRange)
    {
        Terrain[] terrains = FindObjectsByType<Terrain>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        float density = Mathf.Clamp01(detailDensityPercent * 0.01f);

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null)
            {
                continue;
            }

            terrain.detailObjectDistance = Mathf.Max(0f, detailDistance);
            terrain.treeDistance = Mathf.Max(0f, treeDistance);
            terrain.detailObjectDensity = density;
        }

        WorldChunkManager[] chunkManagers = FindObjectsByType<WorldChunkManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < chunkManagers.Length; i++)
        {
            if (chunkManagers[i] != null)
            {
                chunkManagers[i].SetActiveRange(activeRange, true);
            }
        }
    }

    private static void ApplyStoredTerrainQuality()
    {
        if (!PlayerPrefs.HasKey(TerrainQualityPreferenceKey)
            || !PlayerPrefs.HasKey(DetailDistancePreferenceKey)
            || !PlayerPrefs.HasKey(TreeDistancePreferenceKey)
            || !PlayerPrefs.HasKey(DetailDensityPreferenceKey)
            || !PlayerPrefs.HasKey(ChunkActiveRangePreferenceKey))
        {
            return;
        }

        ApplyTerrainAndChunkValues(
            PlayerPrefs.GetFloat(DetailDistancePreferenceKey),
            PlayerPrefs.GetFloat(TreeDistancePreferenceKey),
            PlayerPrefs.GetFloat(DetailDensityPreferenceKey),
            PlayerPrefs.GetInt(ChunkActiveRangePreferenceKey));
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyStoredTerrainQuality();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeRuntimeTerrainQuality()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyStoredTerrainQuality();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyInitialResolution()
    {
        if (Application.isBatchMode)
        {
            return;
        }

        var availableOptions = new List<Vector2Int>();
        PopulateAvailableResolutionOptions(availableOptions);

        int requestedWidth = PlayerPrefs.GetInt(
            WidthPreferenceKey,
            DefaultResolutionWidth);
        int requestedHeight = PlayerPrefs.GetInt(
            HeightPreferenceKey,
            DefaultResolutionHeight);
        bool fullScreen = PlayerPrefs.GetInt(FullScreenPreferenceKey, 1) != 0;
        int selectedIndex = FindClosestResolutionIndex(
            availableOptions,
            requestedWidth,
            requestedHeight);
        Vector2Int resolution = availableOptions[selectedIndex];
        FullScreenMode mode = fullScreen
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;

        Screen.SetResolution(resolution.x, resolution.y, mode);

        bool shouldNormalizePreferences = !PlayerPrefs.HasKey(WidthPreferenceKey)
            || !PlayerPrefs.HasKey(HeightPreferenceKey)
            || !PlayerPrefs.HasKey(FullScreenPreferenceKey)
            || requestedWidth != resolution.x
            || requestedHeight != resolution.y;
        if (shouldNormalizePreferences)
        {
            PlayerPrefs.SetInt(WidthPreferenceKey, resolution.x);
            PlayerPrefs.SetInt(HeightPreferenceKey, resolution.y);
            PlayerPrefs.SetInt(FullScreenPreferenceKey, fullScreen ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    private void SelectCurrentResolution()
    {
        if (resolutionOptions.Count == 0)
        {
            return;
        }

        selectedResolutionIndex = FindClosestResolutionIndex(
            resolutionOptions,
            Screen.width,
            Screen.height);
        selectedFullScreen = Screen.fullScreenMode != FullScreenMode.Windowed;
    }

    private static int FindClosestResolutionIndex(
        List<Vector2Int> options,
        int targetWidth,
        int targetHeight)
    {
        int bestIndex = 0;
        long bestDistance = long.MaxValue;

        for (int i = 0; i < options.Count; i++)
        {
            Vector2Int option = options[i];
            long widthDifference = option.x - targetWidth;
            long heightDifference = option.y - targetHeight;
            long distance = widthDifference * widthDifference
                + heightDifference * heightDifference;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void RefreshResolutionLabels()
    {
        if (resolutionValueText != null && resolutionOptions.Count > 0)
        {
            Vector2Int option = resolutionOptions[
                Mathf.Clamp(selectedResolutionIndex, 0, resolutionOptions.Count - 1)];
            resolutionValueText.text = $"{option.x} × {option.y}";
        }

        if (screenModeText != null)
        {
            screenModeText.text = selectedFullScreen
                ? "화면 모드: 전체 화면"
                : "화면 모드: 창 모드";
        }
    }

    private RectTransform CreateRectObject(
        string objectName,
        Transform parent,
        Vector2 size,
        Vector2 anchoredPosition)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.layer = gameObject.layer;

        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetCenteredRect(rect, size, anchoredPosition);
        return rect;
    }

    private RectTransform CreateImageObject(
        string objectName,
        Transform parent,
        Vector2 size,
        Vector2 anchoredPosition,
        Color color)
    {
        GameObject child = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        child.layer = gameObject.layer;

        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetCenteredRect(rect, size, anchoredPosition);

        Image image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        ConfigureRoundedImage(image);
        return rect;
    }

    private void ConfigureRoundedImage(Image image)
    {
        if (image == null || roundedFilledSprite == null)
        {
            return;
        }

        image.sprite = roundedFilledSprite;
        image.type = Image.Type.Sliced;
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 size,
        Vector2 anchoredPosition,
        Color backgroundColor)
    {
        return CreateButton(
            objectName,
            parent,
            label,
            size,
            anchoredPosition,
            backgroundColor,
            out _);
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 size,
        Vector2 anchoredPosition,
        Color backgroundColor,
        out TMP_Text labelText)
    {
        RectTransform rect = CreateImageObject(
            objectName,
            parent,
            size,
            anchoredPosition,
            backgroundColor);
        Image image = rect.GetComponent<Image>();

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.88f, 0.98f, 1f, 1f);
        colors.pressedColor = new Color(0.72f, 0.8f, 0.9f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        labelText = CreateText(
            "Label",
            rect,
            label,
            23f,
            size,
            Vector2.zero,
            FontStyles.Bold);
        return button;
    }

    private TMP_Text CreateText(
        string objectName,
        Transform parent,
        string value,
        float fontSize,
        Vector2 size,
        Vector2 anchoredPosition,
        FontStyles style = FontStyles.Normal)
    {
        GameObject child = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        child.layer = gameObject.layer;

        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetCenteredRect(rect, size, anchoredPosition);

        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static void SetCenteredRect(
        RectTransform rect,
        Vector2 size,
        Vector2 anchoredPosition)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void StretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (lowTerrainQuality == null)
        {
            lowTerrainQuality = new TerrainQualityPreset(75f, 100f, 50f, 1);
        }

        if (mediumTerrainQuality == null)
        {
            mediumTerrainQuality = new TerrainQualityPreset(125f, 175f, 75f, 1);
        }

        if (highTerrainQuality == null)
        {
            highTerrainQuality = new TerrainQualityPreset(225f, 325f, 100f, 2);
        }

        lowTerrainQuality.ClampValues();
        mediumTerrainQuality.ClampValues();
        highTerrainQuality.ClampValues();
    }
#endif
}
