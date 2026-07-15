using KMS;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class KMSGameClockPrefabBuilder
{
    private const string PrefabPath = "Assets/KMS/2.Prefabs/KMSGameClock.prefab";
    private const string TestScenePath = "Assets/KMS/0.Scenes/TestScene_KMS.unity";
    private const string RootName = "KMSGameClockCanvas";

    [MenuItem("Tools/KMS/Rebuild Game Clock Prefab")]
    public static void RebuildPrefab()
    {
        GameObject temporaryRoot = CreateClockHierarchy();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporaryRoot, PrefabPath);
        Object.DestroyImmediate(temporaryRoot);

        if (prefab == null)
        {
            throw new System.InvalidOperationException($"Failed to create {PrefabPath}.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[KMSGameClockPrefabBuilder] Rebuilt {PrefabPath}");
    }

    public static void RebuildPrefabAndPlaceInTestScene()
    {
        RebuildPrefab();

        Scene scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            throw new System.InvalidOperationException($"Could not load {PrefabPath}.");
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == RootName)
            {
                Object.DestroyImmediate(root);
            }
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = RootName;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[KMSGameClockPrefabBuilder] Placed {PrefabPath} in {TestScenePath}");
    }

    private static GameObject CreateClockHierarchy()
    {
        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.layer = LayerMask.NameToLayer("UI");

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.localScale = Vector3.one;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = Vector2.zero;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject clockObject = new GameObject("KMSGameClock", typeof(RectTransform), typeof(CanvasRenderer));
        clockObject.layer = root.layer;
        RectTransform clockRect = clockObject.GetComponent<RectTransform>();
        clockRect.SetParent(root.transform, false);
        clockRect.anchorMin = new Vector2(0f, 1f);
        clockRect.anchorMax = new Vector2(0f, 1f);
        clockRect.pivot = new Vector2(0.5f, 0.5f);
        clockRect.sizeDelta = new Vector2(72f, 72f);
        clockRect.anchoredPosition = new Vector2(96f, -146f);

        KMSGameClockGraphic graphic = clockObject.AddComponent<KMSGameClockGraphic>();
        graphic.raycastTarget = false;

        GameObject labelObject = new GameObject("PeriodLabel", typeof(RectTransform), typeof(CanvasRenderer));
        labelObject.layer = root.layer;
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(clockRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "DAY";
        label.fontSize = 18f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.91f, 0.66f);
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
        {
            label.font = TMP_Settings.defaultFontAsset;
        }

        KMSGameClockUI controller = root.AddComponent<KMSGameClockUI>();
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("clockGraphic").objectReferenceValue = graphic;
        serializedController.FindProperty("periodLabel").objectReferenceValue = label;
        serializedController.FindProperty("createTimeSystemIfMissing").boolValue = true;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }
}
