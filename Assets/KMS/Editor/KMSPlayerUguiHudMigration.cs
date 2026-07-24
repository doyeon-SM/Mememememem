using KMS;
using KMS.InventoryDuped;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UIDocument = UnityEngine.UIElements.UIDocument;

public static class KMSPlayerUguiHudMigration
{
    private const string CanvasPrefabPath = "Assets/KMS/2.Prefabs/0714_InventoryCanvas_Root.prefab";
    private const string LegacyPlayerPrefabPath = "Assets/KMS/2.Prefabs/0714_Player_KMS.prefab";
    private const string UguiPlayerPrefabPath = "Assets/KMS/2.Prefabs/0720_Player_KMS.prefab";
    private const string KoreanFontAssetPath = "Assets/4.Font/JalnanGothic SDF.asset";
    private const string HudRootName = "HUDLayer";

    [MenuItem("KMS/Setup/Build 0720 Player uGUI HUD")]
    public static void ApplyCurrentPlayerUguiHud()
    {
        BuildHudCanvas();
        CreateAndConfigurePlayerVariants();
        ValidateMigration();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[KMSPlayerUguiHudMigration] 0714 restored to Toolkit HUD and 0720 configured for uGUI HUD.");
    }

    public static void ApplyCurrentPlayerUguiHudFromCommandLine()
    {
        ApplyCurrentPlayerUguiHud();
    }

    [MenuItem("KMS/Validate/0714 and 0720 Player HUDs")]
    public static void ValidateMigration()
    {
        GameObject canvasRoot = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        GameObject legacyPlayerRoot = PrefabUtility.LoadPrefabContents(LegacyPlayerPrefabPath);
        GameObject uguiPlayerRoot = PrefabUtility.LoadPrefabContents(UguiPlayerPrefabPath);

        try
        {
            KMSPlayerHudView view = canvasRoot.GetComponentInChildren<KMSPlayerHudView>(true);
            Require(view != null, "KMSPlayerHudView is missing from the current Canvas prefab.");
            Require(view.HasRequiredReferences(), "KMSPlayerHudView has one or more missing uGUI references.");

            Transform healthFill = FindDescendant(FindDescendant(canvasRoot.transform, "HealthBar"), "Fill");
            Transform hungerFill = FindDescendant(FindDescendant(canvasRoot.transform, "HungerBar"), "Fill");
            Require(healthFill != null && healthFill.GetComponent<Image>().type == Image.Type.Simple,
                "Health Fill is not configured for RectTransform-based shrinking.");
            Require(hungerFill != null && hungerFill.GetComponent<Image>().type == Image.Type.Simple,
                "Hunger Fill is not configured for RectTransform-based shrinking.");

            RectTransform throwTitle = FindDescendant(FindDescendant(canvasRoot.transform, "ThrowGuide"), "Title") as RectTransform;
            RectTransform throwCancel = FindDescendant(FindDescendant(canvasRoot.transform, "ThrowGuide"), "Cancel") as RectTransform;
            Require(throwTitle != null && throwTitle.rect.width >= 300f, "Throw guide title width is collapsed.");
            Require(throwCancel != null && throwCancel.rect.width >= 300f, "Throw guide cancel text width is collapsed.");

            Transform inventoryGrid = FindDescendant(canvasRoot.transform, "InventoryGrid");
            Transform quickSlot = FindDescendant(canvasRoot.transform, "QuickSlot");
            Require(inventoryGrid != null, "InventoryGrid was removed during HUD migration.");
            Require(quickSlot != null, "QuickSlot was removed during HUD migration.");
            Require(inventoryGrid.GetComponentsInChildren<InventorySlotUI>(true).Length == 60,
                "The current Canvas no longer contains 60 inventory slots.");
            Require(quickSlot.GetComponentsInChildren<InventorySlotUI>(true).Length == 10,
                "The current Canvas no longer contains 10 quick slots.");

            ValidatePlayerVariant(legacyPlayerRoot, "0714_Player_KMS", true);
            ValidatePlayerVariant(uguiPlayerRoot, "0720_Player_KMS", false);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(canvasRoot);
            PrefabUtility.UnloadPrefabContents(legacyPlayerRoot);
            PrefabUtility.UnloadPrefabContents(uguiPlayerRoot);
        }

        Debug.Log("[KMSPlayerUguiHudMigration] Validation passed: Fill sizing, throw text, slots, and 0714/0720 HUD modes are intact.");
    }

    private static void BuildHudCanvas()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);

        try
        {
            Transform canvasMain = FindDescendant(root.transform, "Canvas_Main");
            Require(canvasMain != null, "Canvas_Main is missing from the current Canvas prefab.");

            Transform previousHud = canvasMain.Find(HudRootName);
            if (previousHud != null)
            {
                Object.DestroyImmediate(previousHud.gameObject);
            }

            RectTransform hudRoot = CreateRect(HudRootName, canvasMain);
            Stretch(hudRoot);
            KMSPlayerHudView view = hudRoot.gameObject.AddComponent<KMSPlayerHudView>();

            BuildTopRightMenu(hudRoot, out TMP_Text realTimeText, out TMP_Text goldText,
                out Button collectionButton, out Button inventoryButton, out Button mapButton);
            BuildSurvivalStatus(hudRoot, out RectTransform survivalStatus, out Image healthFill,
                out TMP_Text healthText, out Image hungerFill, out TMP_Text hungerText);
            BuildNotifications(hudRoot, out RectTransform notificationContainer, out GameObject notificationTemplate);
            GameObject throwGuide = BuildThrowGuide(hudRoot);
            BuildDefeatOverlay(hudRoot, out GameObject defeatOverlay, out TMP_Text defeatMessageText,
                out Button respawnButton);

            SerializedObject serializedView = new SerializedObject(view);
            SetReference(serializedView, "realTimeText", realTimeText);
            SetReference(serializedView, "goldText", goldText);
            SetReference(serializedView, "collectionButton", collectionButton);
            SetReference(serializedView, "inventoryButton", inventoryButton);
            SetReference(serializedView, "mapButton", mapButton);
            SetReference(serializedView, "survivalStatus", survivalStatus);
            SetReference(serializedView, "healthFill", healthFill);
            SetReference(serializedView, "healthText", healthText);
            SetReference(serializedView, "hungerFill", hungerFill);
            SetReference(serializedView, "hungerText", hungerText);
            SetReference(serializedView, "notificationContainer", notificationContainer);
            SetReference(serializedView, "notificationTemplate", notificationTemplate);
            SetReference(serializedView, "throwGuide", throwGuide);
            SetReference(serializedView, "defeatOverlay", defeatOverlay);
            SetReference(serializedView, "defeatMessageText", defeatMessageText);
            SetReference(serializedView, "respawnButton", respawnButton);
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CreateAndConfigurePlayerVariants()
    {
        ConfigurePlayerVariant(LegacyPlayerPrefabPath, "0714_Player_KMS", true);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(UguiPlayerPrefabPath) == null)
        {
            Require(AssetDatabase.CopyAsset(LegacyPlayerPrefabPath, UguiPlayerPrefabPath),
                "Failed to copy 0714 player prefab to 0720 player prefab.");
            AssetDatabase.ImportAsset(UguiPlayerPrefabPath, ImportAssetOptions.ForceUpdate);
        }

        ConfigurePlayerVariant(UguiPlayerPrefabPath, "0720_Player_KMS", false);
    }

    private static void ConfigurePlayerVariant(string prefabPath, string rootName, bool toolkitEnabled)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            root.name = rootName;
            UIDocument uiDocument = root.GetComponent<UIDocument>();
            Require(uiDocument != null, $"UIDocument is missing from {rootName}.");
            uiDocument.enabled = toolkitEnabled;

            PlayerHUD playerHud = root.GetComponent<PlayerHUD>();
            KMSMemDexLauncher memDexLauncher = root.GetComponent<KMSMemDexLauncher>();
            Require(playerHud != null, $"PlayerHUD is missing from {rootName}.");
            Require(memDexLauncher != null, $"KMSMemDexLauncher is missing from {rootName}.");

            SerializedObject serializedHud = new SerializedObject(playerHud);
            SetReference(serializedHud, "uiDocument", uiDocument);
            serializedHud.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedLauncher = new SerializedObject(memDexLauncher);
            SetReference(serializedLauncher, "uiDocument", uiDocument);
            serializedLauncher.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(uiDocument);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ValidatePlayerVariant(GameObject root, string expectedName, bool toolkitEnabled)
    {
        Require(root.name == expectedName, $"Expected root name '{expectedName}', found '{root.name}'.");
        UIDocument uiDocument = root.GetComponent<UIDocument>();
        Require(uiDocument != null, $"UIDocument is missing from {expectedName}.");
        Require(uiDocument.enabled == toolkitEnabled, $"{expectedName} has the wrong HUD mode.");
        Require(root.GetComponent<PlayerHUD>() != null, $"PlayerHUD is missing from {expectedName}.");
        Require(root.GetComponent<KMSMemDexLauncher>() != null, $"KMSMemDexLauncher is missing from {expectedName}.");
        Require(root.GetComponent<PlayerConsumableController>() != null,
            $"The user's PlayerConsumableController change was not preserved in {expectedName}.");
    }

    private static void BuildTopRightMenu(
        RectTransform parent,
        out TMP_Text realTimeText,
        out TMP_Text goldText,
        out Button collectionButton,
        out Button inventoryButton,
        out Button mapButton)
    {
        Image panel = CreateImage("TopRightMenu", parent, new Color32(12, 12, 12, 224), false);
        SetRect(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(300f, 120f));

        realTimeText = CreateText("RealTimeText", panel.rectTransform, "00시 00분", 14f, TextAlignmentOptions.MidlineRight, Color.white, FontStyles.Bold);
        SetRect(realTimeText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -7f), new Vector2(284f, 22f));

        collectionButton = CreateButton("CollectionButton", panel.rectTransform, "도감");
        inventoryButton = CreateButton("InventoryButton", panel.rectTransform, "가방");
        mapButton = CreateButton("MapButton", panel.rectTransform, "지도");
        SetRect((RectTransform)collectionButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -36f), new Vector2(90f, 50f));
        SetRect((RectTransform)inventoryButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(105f, -36f), new Vector2(90f, 50f));
        SetRect((RectTransform)mapButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(202f, -36f), new Vector2(90f, 50f));

        goldText = CreateText("GoldText", panel.rectTransform, "Gold: 0 ", 14f, TextAlignmentOptions.MidlineRight, Color.white, FontStyles.Bold);
        SetRect(goldText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -92f), new Vector2(284f, 20f));
    }

    private static void BuildSurvivalStatus(
        RectTransform parent,
        out RectTransform status,
        out Image healthFill,
        out TMP_Text healthText,
        out Image hungerFill,
        out TMP_Text hungerText)
    {
        status = CreateRect("SurvivalStatus", parent);
        SetRect(status, new Vector2(0.5f, 0.08f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(800f, 30f));

        BuildProgressBar("HealthBar", status, new Vector2(0f, 0f), new Vector2(0.42f, 1f), new Color(145f / 255f, 213f / 255f, 97f / 255f, 0.75f),
            "Health 100 / 100", out healthFill, out healthText);
        BuildProgressBar("HungerBar", status, new Vector2(0.58f, 0f), new Vector2(1f, 1f), new Color(19f / 255f, 181f / 255f, 231f / 255f, 0.75f),
            "Hunger 100 / 100", out hungerFill, out hungerText);
    }

    private static void BuildProgressBar(
        string name,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color fillColor,
        string initialText,
        out Image fill,
        out TMP_Text label)
    {
        Image background = CreateImage(name, parent, new Color(16f / 255f, 16f / 255f, 16f / 255f, 0.75f), false);
        Stretch(background.rectTransform, anchorMin, anchorMax);

        fill = CreateImage("Fill", background.rectTransform, fillColor, false);
        Stretch(fill.rectTransform);
        fill.type = Image.Type.Simple;

        label = CreateText("ValueText", background.rectTransform, initialText, 14f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        Stretch(label.rectTransform);
    }

    private static void BuildNotifications(RectTransform parent, out RectTransform container, out GameObject template)
    {
        container = CreateRect("NotificationContainer", parent);
        SetRect(container, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -150f), new Vector2(350f, 200f));

        VerticalLayoutGroup layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperRight;
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Image templateBackground = CreateImage("NotificationTemplate", container, new Color(22f / 255f, 22f / 255f, 24f / 255f, 0.9f), false);
        template = templateBackground.gameObject;
        LayoutElement layoutElement = template.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 42f;
        layoutElement.minHeight = 36f;
        template.AddComponent<CanvasGroup>();

        TMP_Text text = CreateText("MessageText", templateBackground.rectTransform, string.Empty, 11f, TextAlignmentOptions.Center,
            new Color32(229, 229, 229, 255), FontStyles.Bold);
        Stretch(text.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 6f), new Vector2(-24f, -12f));
        template.SetActive(false);
    }

    private static GameObject BuildThrowGuide(RectTransform parent)
    {
        Image background = CreateImage("ThrowGuide", parent, new Color(0f, 0f, 0f, 0.72f), false);
        SetRect(background.rectTransform, new Vector2(0.5f, 0.88f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(360f, 72f));

        TMP_Text title = CreateText("Title", background.rectTransform, "우클릭을 놓아 캡슐 던지기", 20f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(336f, 32f));

        TMP_Text cancel = CreateText("Cancel", background.rectTransform, "숫자키 또는 휠로 취소", 14f, TextAlignmentOptions.Center, new Color32(190, 190, 190, 255), FontStyles.Normal);
        SetRect(cancel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(336f, 24f));

        background.gameObject.SetActive(false);
        return background.gameObject;
    }

    private static void BuildDefeatOverlay(
        RectTransform parent,
        out GameObject overlay,
        out TMP_Text messageText,
        out Button respawnButton)
    {
        Image background = CreateImage("DefeatOverlay", parent, new Color(0f, 0f, 0f, 0.8f), true);
        Stretch(background.rectTransform);
        overlay = background.gameObject;

        messageText = CreateText("MessageText", background.rectTransform, "RESPAWN IN", 28f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        SetRect(messageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 100f), new Vector2(400f, 50f));

        Image divider = CreateImage("MessageDivider", background.rectTransform, Color.white, false);
        SetRect(divider.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 66f), new Vector2(194f, 4f));

        TMP_Text countdown = CreateText("CountdownText", background.rectTransform, "5", 150f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        SetRect(countdown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(300f, 180f));

        respawnButton = CreateButton("RespawnButton", background.rectTransform, "Respawn");
        SetRect((RectTransform)respawnButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -90f), new Vector2(220f, 64f));

        overlay.SetActive(false);
    }

    private static Button CreateButton(string name, RectTransform parent, string label)
    {
        Image image = CreateImage(name, parent, new Color32(245, 245, 245, 255), true);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color32(245, 245, 245, 255);
        colors.highlightedColor = new Color32(220, 220, 220, 255);
        colors.pressedColor = new Color32(185, 185, 185, 255);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Outline outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(25, 25, 25, 255);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text text = CreateText("Label", image.rectTransform, label, 15f, TextAlignmentOptions.Center, new Color32(20, 20, 20, 255), FontStyles.Bold);
        Stretch(text.rectTransform);
        return button;
    }

    private static Image CreateImage(string name, RectTransform parent, Color color, bool raycastTarget)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static TMP_Text CreateText(
        string name,
        RectTransform parent,
        string value,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color,
        FontStyles fontStyle)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontAssetPath);
        text.font = koreanFont != null ? koreanFont : TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.fontStyle = fontStyle;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        Stretch(rect, Vector2.zero, Vector2.one);
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        Stretch(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static void SetReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Require(property != null, $"Serialized property '{propertyName}' was not found.");
        property.objectReferenceValue = value;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root.name == name) return root;

        foreach (Transform child in root)
        {
            Transform result = FindDescendant(child, name);
            if (result != null) return result;
        }

        return null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new System.InvalidOperationException(message);
    }
}
