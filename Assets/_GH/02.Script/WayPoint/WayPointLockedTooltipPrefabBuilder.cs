#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class WayPointLockedTooltipPrefabBuilder
{
    private const string SourcePanelPath =
        "Assets/_GH/05.Prefeb/UI/UI_Map_Panel.prefab";
    private const string TargetFolderPath = "Assets/_GH/Resources";
    private const string TargetPrefabPath =
        "Assets/_GH/Resources/WayPoint_ToolTip_Locked.prefab";

    static WayPointLockedTooltipPrefabBuilder()
    {
        EditorApplication.delayCall += BuildIfMissing;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem("Tools/GH/Rebuild Locked WayPoint Tooltip Prefab")]
    public static void Rebuild()
    {
        Build(true);
    }

    private static void BuildIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath) != null)
        {
            return;
        }

        Build(false);
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += BuildIfMissing;
        }
    }

    private static void Build(bool overwrite)
    {
        if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath) != null)
        {
            return;
        }

        EnsureTargetFolder();

        GameObject panelRoot = PrefabUtility.LoadPrefabContents(SourcePanelPath);
        if (panelRoot == null)
        {
            Debug.LogError("[WayPoint] UI_Map_Panel prefab을 불러오지 못했습니다.");
            return;
        }

        try
        {
            WayPointTooltipView sourceView =
                panelRoot.GetComponentInChildren<WayPointTooltipView>(true);
            if (sourceView == null)
            {
                Debug.LogError("[WayPoint] UI_Map_Panel에서 WayPointTooltipView를 찾지 못했습니다.");
                return;
            }

            GameObject lockedRoot = Object.Instantiate(sourceView.gameObject);
            lockedRoot.name = "WayPoint_ToolTip_Locked";
            lockedRoot.SetActive(true);

            RectTransform rootRect = lockedRoot.transform as RectTransform;
            if (rootRect != null)
            {
                rootRect.SetParent(null, false);
                rootRect.anchorMin = new Vector2(0.5f, 0.5f);
                rootRect.anchorMax = new Vector2(0.5f, 0.5f);
                rootRect.pivot = new Vector2(0.5f, 0.5f);
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.sizeDelta = new Vector2(300f, 100f);
            }

            WayPointTooltipView lockedView = lockedRoot.GetComponent<WayPointTooltipView>();
            SerializedObject viewProperties = new SerializedObject(lockedView);
            viewProperties.FindProperty("lockedVariant").boolValue = true;
            viewProperties.ApplyModifiedPropertiesWithoutUndo();

            TMP_Text title = lockedRoot.GetComponentInChildren<TMP_Text>(true);
            if (title != null)
            {
                title.text = "우거진 숲";
                title.alignment = TextAlignmentOptions.MidlineLeft;
                RectTransform titleRect = title.rectTransform;
                titleRect.anchorMin = new Vector2(0f, 0.5f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.offsetMin = new Vector2(18f, 0f);
                titleRect.offsetMax = new Vector2(-48f, -4f);
            }

            Image lockIcon = FindDirectChildImage(lockedRoot.transform, "Image");
            if (lockIcon != null)
            {
                RectTransform iconRect = lockIcon.rectTransform;
                iconRect.anchorMin = new Vector2(1f, 1f);
                iconRect.anchorMax = new Vector2(1f, 1f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(-24f, -24f);
                iconRect.sizeDelta = new Vector2(24f, 24f);
            }

            CreateRequirementText(lockedRoot.transform, title);

            Button travelButton = lockedRoot.GetComponentInChildren<Button>(true);
            if (travelButton != null)
            {
                travelButton.interactable = false;
            }

            PrefabUtility.SaveAsPrefabAsset(lockedRoot, TargetPrefabPath, out bool success);
            Object.DestroyImmediate(lockedRoot);

            if (!success)
            {
                Debug.LogError("[WayPoint] 잠금 툴팁 프리팹 저장에 실패했습니다.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WayPoint] WayPoint_ToolTip_Locked prefab을 생성했습니다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(panelRoot);
        }
    }

    private static void EnsureTargetFolder()
    {
        if (!AssetDatabase.IsValidFolder(TargetFolderPath))
        {
            AssetDatabase.CreateFolder("Assets/_GH", "Resources");
        }
    }

    private static void CreateRequirementText(Transform parent, TMP_Text title)
    {
        Transform oldText = parent.Find("Locked Requirement Text");
        if (oldText != null)
        {
            Object.DestroyImmediate(oldText.gameObject);
        }

        GameObject textObject = new GameObject(
            "Locked Requirement Text",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = parent.gameObject.layer;
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.offsetMin = new Vector2(18f, 6f);
        rect.offsetMax = new Vector2(-18f, -2f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = "① 포탈 개방 필요";
        text.font = title != null ? title.font : TMP_Settings.defaultFontAsset;
        text.fontSize = 18f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 18f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = new Color(0.78f, 0.78f, 0.78f, 1f);
        text.raycastTarget = false;
    }

    private static Image FindDirectChildImage(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }
}
#endif
