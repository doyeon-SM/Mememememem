#if UNITY_EDITOR
using Michsky.MUIP;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class GHResolutionSettingsPanelPrefabBuilder
{
    private const string TargetPath =
        "Assets/_GH/05.Prefeb/UI/GH_Resolution_Settings_Panel.prefab";
    private const string SelectorPath =
        "Assets/5.Assets/Modern UI Pack/Prefabs/Horizontal Selector/Horizontal Selector.prefab";
    private const string SliderPath =
        "Assets/5.Assets/Modern UI Pack/Prefabs/Slider/Standard/Slider - Standard.prefab";
    private const string FontPath =
        "Assets/Pikachu/Resource/Font/NanumSquareRoundB SDF.asset";
    private const string RoundedSpritePath =
        "Assets/5.Assets/Modern UI Pack/Textures/Border/Rounded/512px/Rounded Filled 512px.png";
    private static readonly Color TextColor = new Color(0.96f, 0.97f, 0.95f, 1f);
    private static readonly Color RowColor = new Color(0.22f, 0.23f, 0.21f, 0.82f);

    [MenuItem("Tools/GH/Rebuild Resolution Settings Panel")]
    public static void Rebuild()
    {
        GameObject selectorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SelectorPath);
        GameObject sliderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SliderPath);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Sprite roundedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);

        if (selectorPrefab == null || sliderPrefab == null || font == null || roundedSprite == null)
        {
            Debug.LogError(
                "[GH Settings Builder] Required Modern UI Pack prefab, font, or rounded sprite is missing.");
            return;
        }

        GameObject root = new GameObject(
            "GH_Resolution_Settings_Panel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(GHResolutionSettingsPanel));

        try
        {
            SetLayerRecursively(root, 5);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            Image overlay = root.GetComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.58f);
            overlay.raycastTarget = true;

            RectTransform panel = CreateImage(
                "Settings Card",
                root.transform,
                new Vector2(720f, 1040f),
                Vector2.zero,
                new Color(0.055f, 0.065f, 0.055f, 0.94f),
                roundedSprite);

            CreateText(
                "Title",
                panel,
                "환경 설정",
                font,
                38f,
                new Vector2(600f, 58f),
                new Vector2(-18f, 478f),
                TextAlignmentOptions.Left,
                FontStyles.Bold);

            Button closeButton = CreateButton(
                "Close",
                panel,
                "×",
                font,
                roundedSprite,
                new Vector2(60f, 60f),
                new Vector2(315f, 478f),
                new Color(0f, 0f, 0f, 0f),
                48f);

            CreateText(
                "Graphics Header",
                panel,
                "그래픽",
                font,
                30f,
                new Vector2(650f, 45f),
                new Vector2(0f, 410f),
                TextAlignmentOptions.Left,
                FontStyles.Bold);

            HorizontalSelector resolutionSelector = CreateSelectorRow(
                "Resolution Row",
                "화면 해상도",
                new[] { "1280 X 720", "1366 X 768", "1600 X 900", "1920 X 1080" },
                3,
                new Vector2(0f, 340f),
                panel,
                selectorPrefab,
                font,
                roundedSprite);

            HorizontalSelector screenModeSelector = CreateSelectorRow(
                "Screen Mode Row",
                "화면 모드",
                new[] { "창 모드", "전체 화면" },
                1,
                new Vector2(0f, 235f),
                panel,
                selectorPrefab,
                font,
                roundedSprite);

            HorizontalSelector viewDistanceSelector = CreateSelectorRow(
                "View Distance Row",
                "시야 거리",
                new[] { "하", "중", "상" },
                1,
                new Vector2(0f, 130f),
                panel,
                selectorPrefab,
                font,
                roundedSprite);

            CreateText(
                "Sound Header",
                panel,
                "사운드",
                font,
                30f,
                new Vector2(650f, 45f),
                new Vector2(0f, 54f),
                TextAlignmentOptions.Left,
                FontStyles.Bold);

            Slider masterSlider = CreateSliderRow(
                "Master Volume Row",
                "MASTER",
                new Vector2(0f, -24f),
                panel,
                sliderPrefab,
                font,
                roundedSprite);
            Slider musicSlider = CreateSliderRow(
                "BGM Volume Row",
                "BGM",
                new Vector2(0f, -126f),
                panel,
                sliderPrefab,
                font,
                roundedSprite);
            Slider sfxSlider = CreateSliderRow(
                "SFX Volume Row",
                "SFX",
                new Vector2(0f, -228f),
                panel,
                sliderPrefab,
                font,
                roundedSprite);

            CreateText(
                "Save Guide",
                panel,
                "※ 적용 버튼을 눌러야 환경 설정이 저장됩니다.",
                font,
                18f,
                new Vector2(650f, 34f),
                new Vector2(0f, -315f),
                TextAlignmentOptions.Right);

            Button resetButton = CreateButton(
                "Reset Defaults",
                panel,
                "↶   초기화",
                font,
                roundedSprite,
                new Vector2(300f, 72f),
                new Vector2(-164f, -382f),
                new Color(0.08f, 0.09f, 0.07f, 0.92f),
                27f);
            Button applyButton = CreateButton(
                "Apply",
                panel,
                "✓   적용",
                font,
                roundedSprite,
                new Vector2(300f, 72f),
                new Vector2(164f, -382f),
                new Color(0.08f, 0.09f, 0.07f, 0.92f),
                27f);

            SerializedObject settings = new SerializedObject(
                root.GetComponent<GHResolutionSettingsPanel>());
            settings.FindProperty("resolutionSelector").objectReferenceValue = resolutionSelector;
            settings.FindProperty("screenModeSelector").objectReferenceValue = screenModeSelector;
            settings.FindProperty("viewDistanceSelector").objectReferenceValue = viewDistanceSelector;
            settings.FindProperty("masterVolumeSlider").objectReferenceValue = masterSlider;
            settings.FindProperty("musicVolumeSlider").objectReferenceValue = musicSlider;
            settings.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxSlider;
            settings.FindProperty("closeButton").objectReferenceValue = closeButton;
            settings.FindProperty("resetDefaultsButton").objectReferenceValue = resetButton;
            settings.FindProperty("applyButton").objectReferenceValue = applyButton;
            settings.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, TargetPath, out bool success);
            if (!success)
            {
                Debug.LogError("[GH Settings Builder] Failed to save the settings panel prefab.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GH Settings Builder] Rebuilt GH_Resolution_Settings_Panel with Modern UI Pack controls.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static HorizontalSelector CreateSelectorRow(
        string rowName,
        string label,
        string[] items,
        int defaultIndex,
        Vector2 position,
        Transform parent,
        GameObject selectorPrefab,
        TMP_FontAsset font,
        Sprite roundedSprite)
    {
        RectTransform row = CreateImage(
            rowName,
            parent,
            new Vector2(650f, 92f),
            position,
            RowColor,
            roundedSprite);

        CreateText(
            "Label",
            row,
            label,
            font,
            25f,
            new Vector2(190f, 70f),
            new Vector2(-222f, 0f),
            TextAlignmentOptions.Left);

        GameObject selectorObject = (GameObject)PrefabUtility.InstantiatePrefab(
            selectorPrefab,
            row);
        selectorObject.name = label + " Selector";
        SetLayerRecursively(selectorObject, 5);

        RectTransform selectorRect = selectorObject.GetComponent<RectTransform>();
        SetCentered(selectorRect, new Vector2(420f, 40f), new Vector2(102f, 7f));

        HorizontalSelector selector = selectorObject.GetComponent<HorizontalSelector>();
        selector.enableIcon = false;
        if (selector.labelIcon != null)
        {
            selector.labelIcon.gameObject.SetActive(false);
        }

        if (selector.labelIconHelper != null)
        {
            selector.labelIconHelper.gameObject.SetActive(false);
        }

        selector.saveSelected = false;
        selector.enableIndicators = true;
        selector.invokeAtStart = false;
        selector.loopSelection = true;
        selector.defaultIndex = defaultIndex;
        selector.index = defaultIndex;
        selector.items.Clear();

        for (int i = 0; i < items.Length; i++)
        {
            selector.items.Add(new HorizontalSelector.Item { itemTitle = items[i] });
        }

        RebuildIndicators(selector, items, defaultIndex);
        SetSelectorText(selector, items[defaultIndex], font);

        UIManagerHSelector uiManager = selectorObject.GetComponent<UIManagerHSelector>();
        if (uiManager != null)
        {
            uiManager.overrideColors = true;
            uiManager.overrideFonts = true;
        }

        foreach (TMP_Text text in selectorObject.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = font;
            text.color = TextColor;
            text.fontSize = 25f;
        }

        return selector;
    }

    private static void RebuildIndicators(
        HorizontalSelector selector,
        string[] itemTitles,
        int selectedIndex)
    {
        if (selector.indicatorParent == null || selector.indicatorObject == null)
        {
            return;
        }

        for (int i = selector.indicatorParent.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(selector.indicatorParent.GetChild(i).gameObject);
        }

        for (int i = 0; i < itemTitles.Length; i++)
        {
            GameObject indicator = (GameObject)PrefabUtility.InstantiatePrefab(
                selector.indicatorObject,
                selector.indicatorParent);
            indicator.name = itemTitles[i];

            Transform onObject = indicator.transform.Find("On");
            Transform offObject = indicator.transform.Find("Off");
            if (onObject != null)
            {
                onObject.gameObject.SetActive(i == selectedIndex);
            }

            if (offObject != null)
            {
                offObject.gameObject.SetActive(i != selectedIndex);
            }
        }
    }

    private static void SetSelectorText(
        HorizontalSelector selector,
        string value,
        TMP_FontAsset font)
    {
        if (selector.label != null)
        {
            selector.label.text = value;
            selector.label.font = font;
        }

        if (selector.labelHelper != null)
        {
            selector.labelHelper.text = value;
            selector.labelHelper.font = font;
        }
    }

    private static Slider CreateSliderRow(
        string rowName,
        string label,
        Vector2 position,
        Transform parent,
        GameObject sliderPrefab,
        TMP_FontAsset font,
        Sprite roundedSprite)
    {
        RectTransform row = CreateImage(
            rowName,
            parent,
            new Vector2(650f, 86f),
            position,
            RowColor,
            roundedSprite);

        CreateText(
            "Label",
            row,
            label,
            font,
            25f,
            new Vector2(150f, 60f),
            new Vector2(-250f, 0f),
            TextAlignmentOptions.Left);

        GameObject sliderObject = (GameObject)PrefabUtility.InstantiatePrefab(sliderPrefab, row);
        sliderObject.name = label + " Slider";
        SetLayerRecursively(sliderObject, 5);

        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        SetCentered(sliderRect, new Vector2(390f, 20f), new Vector2(45f, 0f));

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = false;
        slider.SetValueWithoutNotify(50f);

        TMP_Text valueText = CreateText(
            "Value",
            row,
            "50%",
            font,
            22f,
            new Vector2(86f, 56f),
            new Vector2(276f, 0f),
            TextAlignmentOptions.Right);

        SliderManager manager = sliderObject.GetComponent<SliderManager>();
        manager.mainSlider = slider;
        manager.valueText = valueText as TextMeshProUGUI;
        manager.popupValueText = null;
        manager.enableSaving = false;
        manager.invokeOnAwake = false;
        manager.usePercent = true;
        manager.showValue = true;
        manager.showPopupValue = false;
        manager.useRoundValue = true;
        manager.minValue = 0f;
        manager.maxValue = 100f;

        UIManagerSlider uiManager = sliderObject.GetComponent<UIManagerSlider>();
        if (uiManager != null)
        {
            uiManager.overrideColors = true;
            uiManager.overrideFonts = true;
        }

        return slider;
    }

    private static RectTransform CreateImage(
        string name,
        Transform parent,
        Vector2 size,
        Vector2 position,
        Color color,
        Sprite sprite)
    {
        GameObject gameObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        gameObject.layer = 5;

        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetCentered(rect, size, position);

        Image image = gameObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = true;
        return rect;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string value,
        TMP_FontAsset font,
        float fontSize,
        Vector2 size,
        Vector2 position,
        TextAlignmentOptions alignment,
        FontStyles style = FontStyles.Normal)
    {
        GameObject gameObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        gameObject.layer = 5;

        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetCentered(rect, size, position);

        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = TextColor;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        TMP_FontAsset font,
        Sprite roundedSprite,
        Vector2 size,
        Vector2 position,
        Color color,
        float fontSize)
    {
        RectTransform rect = CreateImage(name, parent, size, position, color, roundedSprite);
        Image image = rect.GetComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.86f);
        colors.pressedColor = new Color(0.76f, 0.76f, 0.76f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        CreateText(
            "Label",
            rect,
            label,
            font,
            fontSize,
            size,
            Vector2.zero,
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void SetCentered(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
#endif
