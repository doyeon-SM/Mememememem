using System.Collections;
using System.Collections.Generic;
using KMS.Audio;
using Michsky.MUIP;
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
        public int ResolutionIndex;
        public int ViewDistanceIndex;
        public bool FullScreen;
        public float MasterVolume;
        public float MusicVolume;
        public float SfxVolume;
    }

    [System.Serializable]
    private sealed class TerrainQualityPreset
    {
        [Min(0f)] public float detailDistance;
        [Min(0f)] public float treeDistance;
        [Min(0)] public int maxMeshTrees;
        [Range(0f, 100f)] public float detailDensityPercent;
        [Min(0)] public int activeRange;

        public TerrainQualityPreset(
            float newDetailDistance,
            float newTreeDistance,
            int newMaxMeshTrees,
            float newDetailDensityPercent,
            int newActiveRange)
        {
            detailDistance = newDetailDistance;
            treeDistance = newTreeDistance;
            maxMeshTrees = newMaxMeshTrees;
            detailDensityPercent = newDetailDensityPercent;
            activeRange = newActiveRange;
        }

        public void ClampValues()
        {
            detailDistance = Mathf.Max(0f, detailDistance);
            treeDistance = Mathf.Max(0f, treeDistance);
            maxMeshTrees = Mathf.Max(0, maxMeshTrees);
            detailDensityPercent = Mathf.Clamp(detailDensityPercent, 0f, 100f);
            activeRange = Mathf.Max(0, activeRange);
        }
    }

    private const string WidthPreferenceKey = "GH.Resolution.Width";
    private const string HeightPreferenceKey = "GH.Resolution.Height";
    private const string FullScreenPreferenceKey = "GH.Resolution.FullScreen";
    private const string MasterVolumePreferenceKey = "GH.Audio.MasterVolume";
    private const string SfxVolumePreferenceKey = "KMS.Audio.SfxVolume";
    private const string MusicVolumePreferenceKey = "KMS.Audio.MusicVolume";
    private const string TerrainQualityPreferenceKey = "GH.Graphics.TerrainQuality";
    private const string DetailDistancePreferenceKey = "GH.Graphics.DetailDistance";
    private const string TreeDistancePreferenceKey = "GH.Graphics.TreeDistance";
    private const string MaxMeshTreesPreferenceKey = "GH.Graphics.MaxMeshTrees";
    private const string DetailDensityPreferenceKey = "GH.Graphics.DetailDensity";
    private const string ChunkActiveRangePreferenceKey = "GH.Graphics.ChunkActiveRange";
    private const int DefaultResolutionWidth = 1920;
    private const int DefaultResolutionHeight = 1080;
    private const float DefaultApplyCloseFadeDuration = 0.35f;

    private static readonly Vector2Int[] SupportedResolutionPresets =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1366, 768),
        new Vector2Int(1600, 900),
        new Vector2Int(DefaultResolutionWidth, DefaultResolutionHeight)
    };

    [Header("Modern UI Pack Controls")]
    [SerializeField] private HorizontalSelector resolutionSelector;
    [SerializeField] private HorizontalSelector screenModeSelector;
    [SerializeField] private HorizontalSelector viewDistanceSelector;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Actions")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button resetDefaultsButton;
    [SerializeField] private Button applyButton;

    [Header("Terrain And Chunk Quality Presets")]
    [InspectorName("하")]
    [SerializeField] private TerrainQualityPreset lowTerrainQuality =
        new TerrainQualityPreset(75f, 175f, 1000, 50f, 1);
    [InspectorName("중")]
    [SerializeField] private TerrainQualityPreset mediumTerrainQuality =
        new TerrainQualityPreset(225f, 325f, 2000, 75f, 2);
    [InspectorName("상")]
    [SerializeField] private TerrainQualityPreset highTerrainQuality =
        new TerrainQualityPreset(375f, 475f, 3000, 100f, 3);

    private readonly List<Vector2Int> resolutionOptions = new List<Vector2Int>();
    private int selectedResolutionIndex;
    private int selectedViewDistanceIndex = 1;
    private bool selectedFullScreen = true;
    private bool isInitialized;
    private bool isRefreshingUi;
    private bool isApplicationQuitting;
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
        Initialize();
        applyCloseFadeCoroutine = null;
        ResetApplyFadeVisual();
        LoadCommittedSettingsIntoUi();
        CaptureCommittedSnapshot();
        RefreshAllControls();
    }

    private void OnDisable()
    {
        if (applyCloseFadeCoroutine != null)
        {
            StopCoroutine(applyCloseFadeCoroutine);
        }

        applyCloseFadeCoroutine = null;
        ResetApplyFadeVisual();

        if (isApplicationQuitting
            || KMSAudioService.IsApplicationQuitting
            || !isInitialized
            || !hasCommittedSnapshot)
        {
            return;
        }

        RestoreCommittedSnapshot();
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;

        if (!hasCommittedSnapshot)
        {
            return;
        }

        PlayerPrefs.SetFloat(MasterVolumePreferenceKey, committedSnapshot.MasterVolume);
        PlayerPrefs.SetFloat(MusicVolumePreferenceKey, committedSnapshot.MusicVolume);
        PlayerPrefs.SetFloat(SfxVolumePreferenceKey, committedSnapshot.SfxVolume);
        PlayerPrefs.Save();
    }

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

    public void RestoreDefaults()
    {
        selectedResolutionIndex = FindClosestResolutionIndex(
            resolutionOptions,
            DefaultResolutionWidth,
            DefaultResolutionHeight);
        selectedFullScreen = true;
        selectedViewDistanceIndex = 1;

        isRefreshingUi = true;
        SetSliderValueWithoutNotify(masterVolumeSlider, 1f);
        SetSliderValueWithoutNotify(musicVolumeSlider, 1f);
        SetSliderValueWithoutNotify(sfxVolumeSlider, 1f);
        isRefreshingUi = false;

        ApplyAudioPreview();
        RefreshAllControls();
    }

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

        BuildResolutionOptions();
        ConfigureSelectorItems();
        BindControls();
        isInitialized = true;
    }

    private void BuildResolutionOptions()
    {
        resolutionOptions.Clear();
        resolutionOptions.AddRange(SupportedResolutionPresets);
    }

    private void ConfigureSelectorItems()
    {
        if (resolutionSelector != null)
        {
            resolutionSelector.items.Clear();
            for (int i = 0; i < resolutionOptions.Count; i++)
            {
                Vector2Int resolution = resolutionOptions[i];
                resolutionSelector.items.Add(new HorizontalSelector.Item
                {
                    itemTitle = $"{resolution.x} X {resolution.y}"
                });
            }
        }

        SetSelectorItems(screenModeSelector, "창 모드", "전체 화면");
        SetSelectorItems(viewDistanceSelector, "하", "중", "상");
    }

    private static void SetSelectorItems(HorizontalSelector selector, params string[] titles)
    {
        if (selector == null)
        {
            return;
        }

        selector.items.Clear();
        for (int i = 0; i < titles.Length; i++)
        {
            selector.items.Add(new HorizontalSelector.Item { itemTitle = titles[i] });
        }
    }

    private void BindControls()
    {
        if (resolutionSelector != null)
        {
            resolutionSelector.onValueChanged.RemoveListener(HandleResolutionChanged);
            resolutionSelector.onValueChanged.AddListener(HandleResolutionChanged);
        }

        if (screenModeSelector != null)
        {
            screenModeSelector.onValueChanged.RemoveListener(HandleScreenModeChanged);
            screenModeSelector.onValueChanged.AddListener(HandleScreenModeChanged);
        }

        if (viewDistanceSelector != null)
        {
            viewDistanceSelector.onValueChanged.RemoveListener(HandleViewDistanceChanged);
            viewDistanceSelector.onValueChanged.AddListener(HandleViewDistanceChanged);
        }

        BindSlider(masterVolumeSlider, HandleMasterVolumeChanged);
        BindSlider(musicVolumeSlider, HandleMusicVolumeChanged);
        BindSlider(sfxVolumeSlider, HandleSfxVolumeChanged);
        BindButton(closeButton, CancelChanges);
        BindButton(resetDefaultsButton, RestoreDefaults);
        BindButton(applyButton, ApplySettings);
    }

    private static void BindSlider(Slider slider, UnityEngine.Events.UnityAction<float> listener)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.RemoveListener(listener);
        slider.onValueChanged.AddListener(listener);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction listener)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(listener);
        button.onClick.AddListener(listener);
    }

    private void LoadCommittedSettingsIntoUi()
    {
        int width = PlayerPrefs.GetInt(WidthPreferenceKey, Screen.width);
        int height = PlayerPrefs.GetInt(HeightPreferenceKey, Screen.height);

        selectedResolutionIndex = FindClosestResolutionIndex(resolutionOptions, width, height);
        selectedFullScreen = PlayerPrefs.GetInt(
            FullScreenPreferenceKey,
            Screen.fullScreenMode == FullScreenMode.Windowed ? 0 : 1) != 0;
        selectedViewDistanceIndex = Mathf.Clamp(
            PlayerPrefs.GetInt(TerrainQualityPreferenceKey, 1),
            0,
            2);

        isRefreshingUi = true;
        SetSliderValueWithoutNotify(
            masterVolumeSlider,
            PlayerPrefs.GetFloat(MasterVolumePreferenceKey, AudioListener.volume));
        SetSliderValueWithoutNotify(
            musicVolumeSlider,
            PlayerPrefs.GetFloat(MusicVolumePreferenceKey, 1f));
        SetSliderValueWithoutNotify(
            sfxVolumeSlider,
            PlayerPrefs.GetFloat(SfxVolumePreferenceKey, 1f));
        isRefreshingUi = false;
    }

    private void RefreshAllControls()
    {
        isRefreshingUi = true;
        SetSelectorIndexWithoutNotify(resolutionSelector, selectedResolutionIndex);
        SetSelectorIndexWithoutNotify(screenModeSelector, selectedFullScreen ? 1 : 0);
        SetSelectorIndexWithoutNotify(viewDistanceSelector, selectedViewDistanceIndex);
        RefreshSliderManager(masterVolumeSlider);
        RefreshSliderManager(musicVolumeSlider);
        RefreshSliderManager(sfxVolumeSlider);
        isRefreshingUi = false;
        RefreshApplyButtonState();
    }

    private static void SetSelectorIndexWithoutNotify(HorizontalSelector selector, int index)
    {
        if (selector == null || selector.items.Count == 0)
        {
            return;
        }

        index = Mathf.Clamp(index, 0, selector.items.Count - 1);
        selector.defaultIndex = index;
        selector.index = index;

        if (selector.label != null)
        {
            selector.label.text = selector.items[index].itemTitle;
        }

        if (selector.labelHelper != null)
        {
            selector.labelHelper.text = selector.items[index].itemTitle;
        }

        if (selector.enableIndicators && selector.indicatorParent != null)
        {
            for (int i = 0; i < selector.indicatorParent.childCount; i++)
            {
                Transform indicator = selector.indicatorParent.GetChild(i);
                Transform onObject = indicator.Find("On");
                Transform offObject = indicator.Find("Off");
                bool isSelected = i == index;

                if (onObject != null)
                {
                    onObject.gameObject.SetActive(isSelected);
                }

                if (offObject != null)
                {
                    offObject.gameObject.SetActive(!isSelected);
                }
            }
        }
    }

    private static void SetSliderValueWithoutNotify(Slider slider, float normalizedValue)
    {
        if (slider == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(Mathf.Clamp01(normalizedValue) * 100f);
        RefreshSliderManager(slider);
    }

    private static void RefreshSliderManager(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        SliderManager manager = slider.GetComponent<SliderManager>();
        if (manager != null)
        {
            manager.UpdateUI();
        }
    }

    private void HandleResolutionChanged(int index)
    {
        if (isRefreshingUi)
        {
            return;
        }

        selectedResolutionIndex = Mathf.Clamp(index, 0, resolutionOptions.Count - 1);
        RefreshApplyButtonState();
    }

    private void HandleScreenModeChanged(int index)
    {
        if (isRefreshingUi)
        {
            return;
        }

        selectedFullScreen = index == 1;
        RefreshApplyButtonState();
    }

    private void HandleViewDistanceChanged(int index)
    {
        if (isRefreshingUi)
        {
            return;
        }

        selectedViewDistanceIndex = Mathf.Clamp(index, 0, 2);
        RefreshApplyButtonState();
    }

    private void HandleMasterVolumeChanged(float value)
    {
        if (isRefreshingUi)
        {
            return;
        }

        AudioListener.volume = Mathf.Clamp01(value * 0.01f);
        RefreshApplyButtonState();
    }

    private void HandleMusicVolumeChanged(float value)
    {
        if (isRefreshingUi)
        {
            return;
        }

        KMSAudioService.SetMusicVolume(Mathf.Clamp01(value * 0.01f));
        RefreshApplyButtonState();
    }

    private void HandleSfxVolumeChanged(float value)
    {
        if (isRefreshingUi)
        {
            return;
        }

        KMSAudioService.SetSfxVolume(Mathf.Clamp01(value * 0.01f));
        RefreshApplyButtonState();
    }

    private void CaptureCommittedSnapshot()
    {
        committedSnapshot = CaptureCurrentSnapshot();
        hasCommittedSnapshot = true;
    }

    private SettingsSnapshot CaptureCurrentSnapshot()
    {
        return new SettingsSnapshot
        {
            ResolutionIndex = selectedResolutionIndex,
            ViewDistanceIndex = selectedViewDistanceIndex,
            FullScreen = selectedFullScreen,
            MasterVolume = GetNormalizedSliderValue(masterVolumeSlider),
            MusicVolume = GetNormalizedSliderValue(musicVolumeSlider),
            SfxVolume = GetNormalizedSliderValue(sfxVolumeSlider)
        };
    }

    private void RestoreCommittedSnapshot()
    {
        selectedResolutionIndex = committedSnapshot.ResolutionIndex;
        selectedViewDistanceIndex = committedSnapshot.ViewDistanceIndex;
        selectedFullScreen = committedSnapshot.FullScreen;

        isRefreshingUi = true;
        SetSliderValueWithoutNotify(masterVolumeSlider, committedSnapshot.MasterVolume);
        SetSliderValueWithoutNotify(musicVolumeSlider, committedSnapshot.MusicVolume);
        SetSliderValueWithoutNotify(sfxVolumeSlider, committedSnapshot.SfxVolume);
        isRefreshingUi = false;

        AudioListener.volume = committedSnapshot.MasterVolume;
        KMSAudioService.SetMusicVolume(committedSnapshot.MusicVolume);
        KMSAudioService.SetSfxVolume(committedSnapshot.SfxVolume);
        RefreshAllControls();
    }

    private void RefreshApplyButtonState()
    {
        if (applyButton == null)
        {
            return;
        }

        applyButton.interactable = !hasCommittedSnapshot || HasPendingChanges();
    }

    private bool HasPendingChanges()
    {
        SettingsSnapshot current = CaptureCurrentSnapshot();
        return current.ResolutionIndex != committedSnapshot.ResolutionIndex
            || current.ViewDistanceIndex != committedSnapshot.ViewDistanceIndex
            || current.FullScreen != committedSnapshot.FullScreen
            || !Mathf.Approximately(current.MasterVolume, committedSnapshot.MasterVolume)
            || !Mathf.Approximately(current.MusicVolume, committedSnapshot.MusicVolume)
            || !Mathf.Approximately(current.SfxVolume, committedSnapshot.SfxVolume);
    }

    private static float GetNormalizedSliderValue(Slider slider)
    {
        return slider != null ? Mathf.Clamp01(slider.value * 0.01f) : 1f;
    }

    private void ApplyAudioValues()
    {
        float master = GetNormalizedSliderValue(masterVolumeSlider);
        float music = GetNormalizedSliderValue(musicVolumeSlider);
        float sfx = GetNormalizedSliderValue(sfxVolumeSlider);

        AudioListener.volume = master;
        PlayerPrefs.SetFloat(MasterVolumePreferenceKey, master);
        KMSAudioService.SetMusicVolume(music);
        KMSAudioService.SetSfxVolume(sfx);
    }

    private void ApplyAudioPreview()
    {
        AudioListener.volume = GetNormalizedSliderValue(masterVolumeSlider);
        KMSAudioService.SetMusicVolume(GetNormalizedSliderValue(musicVolumeSlider));
        KMSAudioService.SetSfxVolume(GetNormalizedSliderValue(sfxVolumeSlider));
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
            preset.maxMeshTrees,
            preset.detailDensityPercent,
            preset.activeRange);

        PlayerPrefs.SetInt(TerrainQualityPreferenceKey, selectedViewDistanceIndex);
        PlayerPrefs.SetFloat(DetailDistancePreferenceKey, preset.detailDistance);
        PlayerPrefs.SetFloat(TreeDistancePreferenceKey, preset.treeDistance);
        PlayerPrefs.SetInt(MaxMeshTreesPreferenceKey, preset.maxMeshTrees);
        PlayerPrefs.SetFloat(DetailDensityPreferenceKey, preset.detailDensityPercent);
        PlayerPrefs.SetInt(ChunkActiveRangePreferenceKey, preset.activeRange);
    }

    private TerrainQualityPreset GetSelectedTerrainQualityPreset()
    {
        switch (Mathf.Clamp(selectedViewDistanceIndex, 0, 2))
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
        int maxMeshTrees,
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
            terrain.treeMaximumFullLODCount = Mathf.Max(0, maxMeshTrees);
            terrain.detailObjectDensity = density;
        }

        TreeDistanceCulling.ApplyTreeDistanceToAll(treeDistance);

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
            ApplyTerrainAndChunkValues(225f, 325f, 2000, 75f, 2);
            return;
        }

        int qualityIndex = Mathf.Clamp(
            PlayerPrefs.GetInt(TerrainQualityPreferenceKey, 1),
            0,
            2);
        int defaultMaxMeshTrees = qualityIndex switch
        {
            0 => 1000,
            2 => 3000,
            _ => 2000
        };

        ApplyTerrainAndChunkValues(
            PlayerPrefs.GetFloat(DetailDistancePreferenceKey),
            PlayerPrefs.GetFloat(TreeDistancePreferenceKey),
            PlayerPrefs.GetInt(MaxMeshTreesPreferenceKey, defaultMaxMeshTrees),
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

        var options = new List<Vector2Int>(SupportedResolutionPresets);
        int width = PlayerPrefs.GetInt(WidthPreferenceKey, DefaultResolutionWidth);
        int height = PlayerPrefs.GetInt(HeightPreferenceKey, DefaultResolutionHeight);
        bool fullScreen = PlayerPrefs.GetInt(FullScreenPreferenceKey, 1) != 0;
        Vector2Int resolution = options[FindClosestResolutionIndex(options, width, height)];

        Screen.SetResolution(
            resolution.x,
            resolution.y,
            fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);

        if (!PlayerPrefs.HasKey(WidthPreferenceKey)
            || !PlayerPrefs.HasKey(HeightPreferenceKey)
            || width != resolution.x
            || height != resolution.y
            || !PlayerPrefs.HasKey(FullScreenPreferenceKey))
        {
            PlayerPrefs.SetInt(WidthPreferenceKey, resolution.x);
            PlayerPrefs.SetInt(HeightPreferenceKey, resolution.y);
            PlayerPrefs.SetInt(FullScreenPreferenceKey, fullScreen ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    private static int FindClosestResolutionIndex(
        List<Vector2Int> options,
        int targetWidth,
        int targetHeight)
    {
        if (options == null || options.Count == 0)
        {
            return 0;
        }

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

    private void TryStartApplyCloseFade()
    {
        if (applyCloseFadeCoroutine != null)
        {
            return;
        }

        float fadeDuration = DefaultApplyCloseFadeDuration;
        if (SceneUIManager.Instance != null)
        {
            SceneUIManager.Instance.TryGetSettingsSubPanelApplyFadeDuration(out fadeDuration);
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
            applyFadeCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (applyFadeCanvasGroup == null)
        {
            return;
        }

        applyFadeCanvasGroup.alpha = 1f;
        applyFadeCanvasGroup.interactable = true;
        applyFadeCanvasGroup.blocksRaycasts = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (lowTerrainQuality == null)
        {
            lowTerrainQuality = new TerrainQualityPreset(75f, 175f, 1000, 50f, 1);
        }

        if (mediumTerrainQuality == null)
        {
            mediumTerrainQuality = new TerrainQualityPreset(225f, 325f, 2000, 75f, 2);
        }

        if (highTerrainQuality == null)
        {
            highTerrainQuality = new TerrainQualityPreset(375f, 475f, 3000, 100f, 3);
        }

        lowTerrainQuality.ClampValues();
        mediumTerrainQuality.ClampValues();
        highTerrainQuality.ClampValues();
    }
#endif
}
