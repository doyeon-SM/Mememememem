using System;
using System.Collections.Generic;
using KMS.InventoryDuped;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KMS.Editor
{
    public static class KMSTestSceneHudIntegrationSetup
    {
        private const string ScenePath = "Assets/KMS/0.Scenes/TestScene_KMS.unity";
        private const string OldCanvasPrefabPath =
            "Assets/KMS/2.Prefabs/0714_InventoryCanvas_Root.prefab";
        private const string CanvasPrefabPath =
            "Assets/KMS/2.Prefabs/PlayerCanvas_Root.prefab";
        private const string MapPanelPrefabPath =
            "Assets/_GH/05.Prefeb/UI/UI_Map_Panel.prefab";
        private const string WayPointManagerPrefabPath =
            "Assets/2.Prefabs/Manager/WayPointManager.prefab";
        private const string ThirdMapDefinitionGuid =
            "313e77ee067b4c54496c21edb4084093";

        private const string MapCanvasName = "KMS_MapCanvas";
        private const string SceneUiManagerName = "KMS_SceneUIManager";

        [MenuItem("KMS/Setup/Integrate TestScene HUD and Map")]
        public static void Apply()
        {
            RenameCanvasPrefab();
            ConfigureHudButtons();
            ConfigureScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log(
                "[KMSTestSceneHudIntegrationSetup] TestScene_KMS HUD/map integration completed.");
        }

        public static void ApplyFromCommandLine()
        {
            Apply();
        }

        [MenuItem("KMS/Validate/TestScene HUD and Map")]
        public static void Validate()
        {
            Require(
                AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath) != null,
                $"Renamed Canvas prefab is missing: {CanvasPrefabPath}");
            Require(
                AssetDatabase.LoadAssetAtPath<GameObject>(OldCanvasPrefabPath) == null,
                $"Old Canvas prefab path still exists: {OldCanvasPrefabPath}");

            ValidateHudButtonPrefab();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(scene.IsValid() && scene.isLoaded, "TestScene_KMS could not be loaded.");

            SceneUIManager sceneUiManager =
                UnityEngine.Object.FindFirstObjectByType<SceneUIManager>(
                    FindObjectsInactive.Include);
            WayPointManager wayPointManager =
                UnityEngine.Object.FindFirstObjectByType<WayPointManager>(
                    FindObjectsInactive.Include);
            WayPointMapUI mapUi =
                UnityEngine.Object.FindFirstObjectByType<WayPointMapUI>(
                    FindObjectsInactive.Include);
            EventSystem eventSystem =
                UnityEngine.Object.FindFirstObjectByType<EventSystem>(
                    FindObjectsInactive.Include);

            Require(sceneUiManager != null, "SceneUIManager is missing.");
            Require(wayPointManager != null, "WayPointManager is missing.");
            Require(mapUi != null, "WayPointMapUI is missing.");
            Require(eventSystem != null, "EventSystem is missing.");
            Require(!mapUi.gameObject.activeSelf, "Map panel should start inactive.");

            SerializedObject serializedManager = new SerializedObject(sceneUiManager);
            SerializedProperty ids = serializedManager.FindProperty("managedUIIds");
            SerializedProperty objects = serializedManager.FindProperty("managedUIObjects");
            Require(ids != null && objects != null, "SceneUIManager managed UI lists are missing.");
            Require(ids.arraySize == objects.arraySize, "Managed UI ID/object counts differ.");
            Require(ContainsManagedId(ids, "Map"), "Map is not registered.");
            Require(ContainsManagedId(ids, "Inventory"), "Inventory is not registered.");
            Require(ContainsManagedId(ids, "MemDex"), "MemDex is not registered.");

            InventoryUI inventoryUi =
                UnityEngine.Object.FindFirstObjectByType<InventoryUI>(
                    FindObjectsInactive.Include);
            Require(inventoryUi != null, "InventoryUI is missing.");
            Require(
                inventoryUi.transform.root.name == "PlayerCanvas_Root",
                "Scene Canvas root does not use the PlayerCanvas_Root name.");

            Debug.Log(
                "[KMSTestSceneHudIntegrationSetup] Validation passed: HUD buttons, " +
                "SceneUIManager, map panel, WayPointManager, and Canvas rename are valid.");
        }

        private static void RenameCanvasPrefab()
        {
            GameObject newAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            GameObject oldAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(OldCanvasPrefabPath);

            if (newAsset == null)
            {
                Require(oldAsset != null, $"Canvas prefab not found: {OldCanvasPrefabPath}");
                string error = AssetDatabase.MoveAsset(
                    OldCanvasPrefabPath,
                    CanvasPrefabPath);
                Require(
                    string.IsNullOrEmpty(error),
                    $"Failed to rename Canvas prefab: {error}");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            try
            {
                root.name = "PlayerCanvas_Root";
                PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureHudButtons()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            try
            {
                ConfigureManagedButton(root.transform, "MapButton", "Map");
                ConfigureManagedButton(root.transform, "InventoryButton", "Inventory");
                ConfigureManagedButton(root.transform, "CollectionButton", "MemDex");
                PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateHudButtonPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            try
            {
                ValidateManagedButton(root.transform, "MapButton", "Map");
                ValidateManagedButton(root.transform, "InventoryButton", "Inventory");
                ValidateManagedButton(root.transform, "CollectionButton", "MemDex");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureManagedButton(
            Transform root,
            string buttonName,
            string managedUiId)
        {
            Transform buttonTransform = FindDescendant(root, buttonName);
            Require(buttonTransform != null, $"{buttonName} is missing.");

            Button button = buttonTransform.GetComponent<Button>();
            Require(button != null, $"{buttonName} has no Button component.");

            ManagedUIButton managedButton =
                buttonTransform.GetComponent<ManagedUIButton>();
            if (managedButton == null)
            {
                managedButton = buttonTransform.gameObject.AddComponent<ManagedUIButton>();
            }

            SerializedObject serializedButton = new SerializedObject(managedButton);
            serializedButton.FindProperty("managedUIId").stringValue = managedUiId;
            serializedButton.ApplyModifiedPropertiesWithoutUndo();

            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                if (button.onClick.GetPersistentTarget(i) is ManagedUIButton)
                {
                    UnityEventTools.RemovePersistentListener(button.onClick, i);
                }
            }

            UnityEventTools.AddPersistentListener(button.onClick, managedButton.Toggle);
            EditorUtility.SetDirty(button);
            EditorUtility.SetDirty(managedButton);
        }

        private static void ValidateManagedButton(
            Transform root,
            string buttonName,
            string managedUiId)
        {
            Transform buttonTransform = FindDescendant(root, buttonName);
            Require(buttonTransform != null, $"{buttonName} is missing.");
            Button button = buttonTransform.GetComponent<Button>();
            ManagedUIButton managedButton =
                buttonTransform.GetComponent<ManagedUIButton>();
            Require(button != null, $"{buttonName} has no Button.");
            Require(managedButton != null, $"{buttonName} has no ManagedUIButton.");

            SerializedObject serializedButton = new SerializedObject(managedButton);
            Require(
                serializedButton.FindProperty("managedUIId").stringValue == managedUiId,
                $"{buttonName} has the wrong managed UI ID.");

            bool foundToggle = false;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == managedButton
                    && button.onClick.GetPersistentMethodName(i) == nameof(ManagedUIButton.Toggle))
                {
                    foundToggle = true;
                    break;
                }
            }

            Require(foundToggle, $"{buttonName} OnClick is not connected to Toggle.");
        }

        private static void ConfigureScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(scene.IsValid() && scene.isLoaded, $"Scene could not be loaded: {ScenePath}");

            InventoryUI inventoryUi =
                UnityEngine.Object.FindFirstObjectByType<InventoryUI>(
                    FindObjectsInactive.Include);
            Require(inventoryUi != null, "InventoryUI is missing from TestScene_KMS.");
            inventoryUi.transform.root.name = "PlayerCanvas_Root";

            WayPointMapUI mapUi = EnsureMapPanel(scene);
            WayPointManager wayPointManager = EnsureWayPointManager(scene);
            ConfigureWayPointManager(wayPointManager, mapUi);

            GameObject memDexUi = FindDescendant(
                inventoryUi.transform.root,
                "P_MemDexUI")?.gameObject;
            if (memDexUi == null)
            {
                memDexUi = FindDescendant(
                    inventoryUi.transform.root,
                    "MemDexModalRoot")?.gameObject;
            }

            Require(inventoryUi.inventoryPanel != null, "InventoryPanel is missing.");
            Require(memDexUi != null, "MemDex managed UI root is missing.");

            List<string> ids = new List<string>
            {
                "Map",
                "Inventory",
                "MemDex"
            };
            List<GameObject> objects = new List<GameObject>
            {
                mapUi.gameObject,
                inventoryUi.inventoryPanel,
                memDexUi
            };

            Transform upgradePopup = FindDescendant(
                inventoryUi.transform.root,
                "P_UpgradePopup");
            if (upgradePopup != null)
            {
                ids.Add("P_UpgradePopup");
                objects.Add(upgradePopup.gameObject);
            }

            SceneUIManager sceneUiManager = EnsureSceneUiManager(scene);
            ConfigureSceneUiManager(sceneUiManager, ids, objects);

            mapUi.gameObject.SetActive(false);
            EditorUtility.SetDirty(inventoryUi.transform.root.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Failed to save TestScene_KMS.");
        }

        private static WayPointMapUI EnsureMapPanel(Scene scene)
        {
            WayPointMapUI existing =
                UnityEngine.Object.FindFirstObjectByType<WayPointMapUI>(
                    FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            GameObject canvasObject = FindRootObject(scene, MapCanvasName);
            if (canvasObject == null)
            {
                canvasObject = new GameObject(
                    MapCanvasName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);

                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;

                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            GameObject mapPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(MapPanelPrefabPath);
            Require(mapPrefab != null, $"Map panel prefab not found: {MapPanelPrefabPath}");

            GameObject mapInstance = (GameObject)PrefabUtility.InstantiatePrefab(
                mapPrefab,
                canvasObject.transform);
            mapInstance.name = "UI_Map_Panel";
            RectTransform rect = mapInstance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }

            WayPointMapUI mapUi = mapInstance.GetComponent<WayPointMapUI>();
            Require(mapUi != null, "Instantiated map panel has no WayPointMapUI.");
            return mapUi;
        }

        private static WayPointManager EnsureWayPointManager(Scene scene)
        {
            WayPointManager existing =
                UnityEngine.Object.FindFirstObjectByType<WayPointManager>(
                    FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(WayPointManagerPrefabPath);
            Require(prefab != null, $"WayPointManager prefab not found: {WayPointManagerPrefabPath}");

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "WayPointManager";
            WayPointManager manager = instance.GetComponent<WayPointManager>();
            Require(manager != null, "Instantiated WayPointManager component is missing.");
            return manager;
        }

        private static void ConfigureWayPointManager(
            WayPointManager manager,
            WayPointMapUI mapUi)
        {
            SerializedObject serializedManager = new SerializedObject(manager);
            SetOptionalReference(serializedManager, "mapUI", mapUi);
            SetOptionalReference(serializedManager, "targetUI", mapUi.gameObject);

            SerializedProperty mapDefinitions =
                serializedManager.FindProperty("mapDefinitions");
            if (mapDefinitions != null)
            {
                string thirdMapPath =
                    AssetDatabase.GUIDToAssetPath(ThirdMapDefinitionGuid);
                WayPointMapDefinition thirdMap =
                    AssetDatabase.LoadAssetAtPath<WayPointMapDefinition>(thirdMapPath);
                if (thirdMap != null && !ContainsReference(mapDefinitions, thirdMap))
                {
                    int index = mapDefinitions.arraySize;
                    mapDefinitions.InsertArrayElementAtIndex(index);
                    mapDefinitions.GetArrayElementAtIndex(index).objectReferenceValue =
                        thirdMap;
                }
            }

            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            WayPointUIToggle toggle = manager.GetComponent<WayPointUIToggle>();
            Require(toggle != null, "WayPointUIToggle is missing.");
            SerializedObject serializedToggle = new SerializedObject(toggle);
            SetOptionalReference(serializedToggle, "targetUI", mapUi.gameObject);
            SetOptionalReference(serializedToggle, "mapUI", mapUi);
            SerializedProperty hideOnStart = serializedToggle.FindProperty("hideOnStart");
            if (hideOnStart != null)
            {
                hideOnStart.boolValue = true;
            }
            serializedToggle.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(toggle);
        }

        private static SceneUIManager EnsureSceneUiManager(Scene scene)
        {
            SceneUIManager existing =
                UnityEngine.Object.FindFirstObjectByType<SceneUIManager>(
                    FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.gameObject.name = SceneUiManagerName;
                return existing;
            }

            GameObject managerObject = new GameObject(SceneUiManagerName);
            SceneManager.MoveGameObjectToScene(managerObject, scene);
            return managerObject.AddComponent<SceneUIManager>();
        }

        private static void ConfigureSceneUiManager(
            SceneUIManager manager,
            IReadOnlyList<string> ids,
            IReadOnlyList<GameObject> objects)
        {
            Require(ids.Count == objects.Count, "Managed UI ID/object counts differ.");

            SerializedObject serializedManager = new SerializedObject(manager);
            SerializedProperty settingsUi = serializedManager.FindProperty("settingsUI");
            if (settingsUi != null)
            {
                settingsUi.objectReferenceValue = null;
            }

            SerializedProperty managedObjects =
                serializedManager.FindProperty("managedUIObjects");
            SerializedProperty managedIds =
                serializedManager.FindProperty("managedUIIds");
            Require(
                managedObjects != null && managedIds != null,
                "SceneUIManager managed UI fields are missing.");

            managedObjects.arraySize = objects.Count;
            managedIds.arraySize = ids.Count;
            for (int i = 0; i < objects.Count; i++)
            {
                managedObjects.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
                managedIds.GetArrayElementAtIndex(i).stringValue = ids[i];
            }

            SerializedProperty allowMultiple =
                serializedManager.FindProperty("allowMultipleManagedUIs");
            if (allowMultiple != null)
            {
                allowMultiple.boolValue = true;
            }

            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        private static void SetOptionalReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static bool ContainsReference(
            SerializedProperty array,
            UnityEngine.Object target)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == target)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContainsManagedId(
            SerializedProperty ids,
            string target)
        {
            for (int i = 0; i < ids.arraySize; i++)
            {
                if (string.Equals(
                    ids.GetArrayElementAtIndex(i).stringValue,
                    target,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static GameObject FindRootObject(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                {
                    return root;
                }
            }
            return null;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }
            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDescendant(root.GetChild(i), objectName);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
