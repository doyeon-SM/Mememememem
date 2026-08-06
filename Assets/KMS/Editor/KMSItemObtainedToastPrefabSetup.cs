using KMS;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class KMSItemObtainedToastPrefabSetup
{
    private static readonly string[] HudPrefabPaths =
    {
        "Assets/KMS/2.Prefabs/PlayerCanvas_Root.prefab",
        "Assets/KMS/2.Prefabs/PlayerHUDLayer.prefab"
    };

    private static readonly string[] PlayerPrefabPaths =
    {
        "Assets/KMS/2.Prefabs/0720_Player_KMS.prefab"
    };

    [MenuItem("KMS/Setup/Item Obtained Toast HUD")]
    public static void Apply()
    {
        foreach (string path in HudPrefabPaths)
        {
            ApplyToPrefab(path);
        }
        foreach (string path in PlayerPrefabPaths)
        {
            ApplySettingsToPlayerPrefab(path);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[KMSItemObtainedToastPrefabSetup] Item obtained toast HUD configured.");
    }

    public static void ApplyFromCommandLine()
    {
        Apply();
    }

    public static void BuildInto(KMSPlayerHudView view)
    {
        if (view == null) throw new System.ArgumentNullException(nameof(view));

        Transform existing = view.transform.Find("ItemObtainedToastContainer");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        RectTransform container = CreateRect("ItemObtainedToastContainer", view.transform);
        SetRect(
            container,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-16f, 84f),
            new Vector2(340f, 360f));

        VerticalLayoutGroup stack = container.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.childAlignment = TextAnchor.LowerRight;
        stack.spacing = 6f;
        stack.childControlWidth = true;
        stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;

        SerializedObject serializedView = new SerializedObject(view);
        GameObject defeatOverlay = serializedView.FindProperty("defeatOverlay")?.objectReferenceValue as GameObject;
        if (defeatOverlay != null && defeatOverlay.transform.parent == view.transform)
        {
            container.SetSiblingIndex(defeatOverlay.transform.GetSiblingIndex());
        }

        TMP_Text fontSource = serializedView.FindProperty("healthText")?.objectReferenceValue as TMP_Text;

        Image background = CreateImage(
            "ItemObtainedToastTemplate",
            container,
            new Color(22f / 255f, 22f / 255f, 24f / 255f, 235f / 255f));
        LayoutElement rootLayout = background.gameObject.AddComponent<LayoutElement>();
        rootLayout.minHeight = 60f;
        rootLayout.preferredHeight = 64f;

        HorizontalLayoutGroup row = background.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(10, 12, 8, 8);
        row.spacing = 10f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;

        CanvasGroup canvasGroup = background.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Image icon = CreateImage("Icon", background.rectTransform, new Color32(60, 60, 64, 255));
        icon.preserveAspect = true;
        LayoutElement iconLayout = icon.gameObject.AddComponent<LayoutElement>();
        iconLayout.minWidth = 44f;
        iconLayout.minHeight = 44f;
        iconLayout.preferredWidth = 44f;
        iconLayout.preferredHeight = 44f;

        TMP_Text missingIcon = CreateText("MissingIcon", icon.rectTransform, "?", 22f,
            TextAlignmentOptions.Center, Color.white, FontStyles.Bold, fontSource);
        Stretch(missingIcon.rectTransform);

        TMP_Text itemName = CreateText("ItemName", background.rectTransform, "Item Name", 18f,
            TextAlignmentOptions.MidlineLeft, new Color32(240, 240, 240, 255), FontStyles.Normal, fontSource);
        itemName.textWrappingMode = TextWrappingModes.NoWrap;
        itemName.overflowMode = TextOverflowModes.Ellipsis;
        LayoutElement nameLayout = itemName.gameObject.AddComponent<LayoutElement>();
        nameLayout.minWidth = 100f;
        nameLayout.flexibleWidth = 1f;

        TMP_Text amount = CreateText("Amount", background.rectTransform, "X1", 18f,
            TextAlignmentOptions.MidlineRight, new Color32(255, 222, 120, 255), FontStyles.Bold, fontSource);
        LayoutElement amountLayout = amount.gameObject.AddComponent<LayoutElement>();
        amountLayout.minWidth = 48f;
        amountLayout.preferredWidth = 54f;

        KMSItemObtainedToastView toastView = background.gameObject.AddComponent<KMSItemObtainedToastView>();
        SerializedObject serializedToast = new SerializedObject(toastView);
        serializedToast.FindProperty("iconImage").objectReferenceValue = icon;
        serializedToast.FindProperty("itemNameText").objectReferenceValue = itemName;
        serializedToast.FindProperty("amountText").objectReferenceValue = amount;
        serializedToast.FindProperty("missingIconText").objectReferenceValue = missingIcon;
        serializedToast.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        serializedToast.ApplyModifiedPropertiesWithoutUndo();

        background.gameObject.SetActive(false);
        serializedView.FindProperty("itemObtainedToastContainer").objectReferenceValue = container;
        serializedView.FindProperty("itemObtainedToastTemplate").objectReferenceValue = toastView;
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);
    }

    private static void ApplyToPrefab(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            KMSPlayerHudView view = root.GetComponentInChildren<KMSPlayerHudView>(true);
            if (view == null)
                throw new System.InvalidOperationException($"KMSPlayerHudView is missing from '{path}'.");

            BuildInto(view);
            if (!view.HasRequiredReferences())
                throw new System.InvalidOperationException($"KMSPlayerHudView has missing references after configuring '{path}'.");
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ApplySettingsToPlayerPrefab(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            PlayerHUD hud = root.GetComponentInChildren<PlayerHUD>(true);
            if (hud == null)
                throw new System.InvalidOperationException($"PlayerHUD is missing from '{path}'.");

            SerializedObject serializedHud = new SerializedObject(hud);
            serializedHud.FindProperty("itemObtainedToastDuration").floatValue = 2.5f;
            serializedHud.FindProperty("itemObtainedToastFadeDuration").floatValue = 0.3f;
            serializedHud.FindProperty("maxVisibleItemObtainedToasts").intValue = 4;
            serializedHud.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject instance = new GameObject(name, typeof(RectTransform));
        instance.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(
        string name,
        RectTransform parent,
        string value,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color,
        FontStyles style,
        TMP_Text fontSource)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (fontSource != null) text.font = fontSource.font;
        else text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.fontStyle = style;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }
}
