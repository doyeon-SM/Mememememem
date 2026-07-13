using KMS.InventoryDuped;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KMS.Editor
{
    public static class KMSInventoryHudSetup
    {
        private const string CanvasPrefabPath = "Assets/KMS/1.Scripts/InventoryDuped/Prefeb/Canvas_Root.prefab";
        private const string SlotPrefabPath = "Assets/KMS/1.Scripts/InventoryDuped/UI/ItemSlot.prefab";
        private const string PlayerPrefabPath = "Assets/KMS/2.Prefabs/0712_Player_KMS.prefab";
        private const string TestScenePath = "Assets/KMS/0.Scenes/TestScene_KMS.unity";
        private const string Test2ScenePath = "Assets/KMS/0.Scenes/Test2Scene_KMS.unity";
        private const int InventoryColumns = 10;
        private const int InventoryRows = 6;
        private const float SlotSize = 72f;
        private const float SlotSpacing = 6f;
        private const float PanelWidth = 800f;
        private const float HotbarHeight = 72f;

        [MenuItem("KMS/Setup/Apply Inventory HUD Layout")]
        public static void Apply()
        {
            GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
            if (slotPrefab == null)
            {
                throw new System.InvalidOperationException($"Item slot prefab not found: {SlotPrefabPath}");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            try
            {
                Transform inventoryPanel = FindDescendant(root.transform, "InventoryPanel");
                Transform inventoryGrid = FindDescendant(root.transform, "InventoryGrid");
                Transform quickSlot = FindDescendant(root.transform, "QuickSlot");

                if (inventoryPanel == null || inventoryGrid == null || quickSlot == null)
                {
                    throw new System.InvalidOperationException("InventoryPanel, InventoryGrid, or QuickSlot is missing.");
                }

                ConfigureHotbar(quickSlot);
                ConfigureInventoryPanel(inventoryPanel, inventoryGrid);
                RebuildInventorySlots(inventoryGrid, slotPrefab);

                PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[KMSInventoryHudSetup] Applied 10x6 inventory layout and hid quick-slot key labels.");
        }

        public static void ApplyFromCommandLine()
        {
            Apply();
        }

        [MenuItem("KMS/Setup/Ensure Scene Inventory UI")]
        public static void EnsureSceneInventoryUi()
        {
            EnsureSceneInventoryUi(Test2ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[KMSInventoryHudSetup] Ensured inventory Canvas and EventSystem in Test2Scene_KMS.");
        }

        public static void EnsureSceneInventoryUiFromCommandLine()
        {
            EnsureSceneInventoryUi();
        }

        public static void ValidateFromCommandLine()
        {
            GameObject canvasRoot = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            try
            {
                Transform inventoryPanel = FindDescendant(canvasRoot.transform, "InventoryPanel");
                Transform inventoryGrid = FindDescendant(canvasRoot.transform, "InventoryGrid");
                Transform quickSlot = FindDescendant(canvasRoot.transform, "QuickSlot");
                PlayerInventory inventory = playerRoot.GetComponent<PlayerInventory>();

                Require(inventoryPanel != null, "InventoryPanel is missing.");
                Require(inventoryGrid != null, "InventoryGrid is missing.");
                Require(quickSlot != null, "QuickSlot is missing.");
                Require(inventory != null, "PlayerInventory is missing from the player prefab.");

                InventorySlotUI[] inventorySlots = inventoryGrid.GetComponentsInChildren<InventorySlotUI>(true);
                InventorySlotUI[] quickSlots = quickSlot.GetComponentsInChildren<InventorySlotUI>(true);
                Require(inventorySlots.Length == InventoryColumns * InventoryRows, $"Expected 60 inventory slots, found {inventorySlots.Length}.");
                Require(quickSlots.Length == InventoryColumns, $"Expected 10 quick slots, found {quickSlots.Length}.");
                Require(inventory.inventory.width == InventoryColumns && inventory.inventory.height == InventoryRows,
                    $"Player inventory is {inventory.inventory.width}x{inventory.inventory.height}, expected 10x6.");

                foreach (InventorySlotUI slot in quickSlots)
                {
                    Require(slot.keyText == null || !slot.keyText.gameObject.activeSelf, $"Quick-slot key label is active on {slot.name}.");
                }

                RectTransform panelRect = (RectTransform)inventoryPanel;
                Require(Mathf.Approximately(panelRect.anchoredPosition.y, HotbarHeight),
                    $"Inventory panel Y is {panelRect.anchoredPosition.y}, expected {HotbarHeight}.");

                GridLayoutGroup layout = inventoryGrid.GetComponent<GridLayoutGroup>();
                Require(layout != null && layout.constraintCount == InventoryColumns,
                    "Inventory grid is not configured for 10 columns.");

                Debug.Log("[KMSInventoryHudSetup] Validation passed: inventory=10x6, quick slots=10, labels hidden, panel attached above hotbar.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
                PrefabUtility.UnloadPrefabContents(canvasRoot);
            }

            Scene scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            Require(scene.IsValid() && scene.isLoaded, "TestScene_KMS could not be loaded.");

            InventoryUI sceneInventoryUi = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
            PlayerInventory sceneInventory = Object.FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            Require(sceneInventoryUi != null, "InventoryUI is missing from TestScene_KMS.");
            Require(sceneInventory != null, "PlayerInventory is missing from TestScene_KMS.");
            Require(sceneInventoryUi.inventoryGrid.GetComponentsInChildren<InventorySlotUI>(true).Length == InventoryColumns * InventoryRows,
                "TestScene_KMS does not resolve 60 inventory slots from Canvas_Root.");
            Require(sceneInventoryUi.quickSlotRoot.GetComponentsInChildren<InventorySlotUI>(true).Length == InventoryColumns,
                "TestScene_KMS does not resolve 10 quick slots from Canvas_Root.");
            Require(sceneInventory.inventory.width == InventoryColumns && sceneInventory.inventory.height == InventoryRows,
                "TestScene_KMS player inventory is not 10x6.");

            Debug.Log("[KMSInventoryHudSetup] Scene validation passed: TestScene_KMS resolves the 60+10 slot layout.");

            Scene test2Scene = EditorSceneManager.OpenScene(Test2ScenePath, OpenSceneMode.Single);
            Require(test2Scene.IsValid() && test2Scene.isLoaded, "Test2Scene_KMS could not be loaded.");

            PlayerInventory test2Inventory = Object.FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            InventoryUI test2InventoryUi = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
            EventSystem test2EventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            Require(test2Inventory != null, "PlayerInventory is missing from Test2Scene_KMS.");
            Require(test2InventoryUi != null, "InventoryUI is missing from Test2Scene_KMS.");
            Require(test2EventSystem != null, "EventSystem is missing from Test2Scene_KMS.");
            Require(test2EventSystem.GetComponent<InputSystemUIInputModule>() != null,
                "InputSystemUIInputModule is missing from Test2Scene_KMS EventSystem.");
            Require(test2Inventory.inventory.width == InventoryColumns && test2Inventory.inventory.height == InventoryRows,
                $"Test2Scene_KMS player inventory is {test2Inventory.inventory.width}x{test2Inventory.inventory.height}, expected 10x6.");
            Require(test2Inventory.quickSlots.width == InventoryColumns && test2Inventory.quickSlots.height == 1,
                "Test2Scene_KMS quick slots are not 10x1.");
            Require(test2InventoryUi.inventoryGrid.GetComponentsInChildren<InventorySlotUI>(true).Length == InventoryColumns * InventoryRows,
                "Test2Scene_KMS does not resolve 60 inventory slots from Canvas_Root.");
            Require(test2InventoryUi.quickSlotRoot.GetComponentsInChildren<InventorySlotUI>(true).Length == InventoryColumns,
                "Test2Scene_KMS does not resolve 10 quick slots from Canvas_Root.");

            Debug.Log("[KMSInventoryHudSetup] Scene validation passed: Test2Scene_KMS resolves the 60+10 player inventory and clickable UI.");
        }

        private static void EnsureSceneInventoryUi(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Require(scene.IsValid() && scene.isLoaded, $"Scene could not be loaded: {scenePath}");

            InventoryUI inventoryUi = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
            if (inventoryUi == null)
            {
                GameObject canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
                Require(canvasPrefab != null, $"Canvas prefab not found: {CanvasPrefabPath}");

                GameObject canvasInstance = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab, scene);
                canvasInstance.name = "InventoryCanvas_Root";
            }

            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            StandaloneInputModule legacyInputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyInputModule != null)
            {
                Object.DestroyImmediate(legacyInputModule);
            }

            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                inputModule.AssignDefaultActions();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureHotbar(Transform quickSlot)
        {
            RectTransform rect = (RectTransform)quickSlot;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(PanelWidth, HotbarHeight);

            HorizontalLayoutGroup layout = quickSlot.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = SlotSpacing;
                layout.childAlignment = TextAnchor.MiddleCenter;
            }

            foreach (InventorySlotUI slot in quickSlot.GetComponentsInChildren<InventorySlotUI>(true))
            {
                if (slot.keyText == null) continue;
                slot.keyText.text = string.Empty;
                slot.keyText.gameObject.SetActive(false);
            }
        }

        private static void ConfigureInventoryPanel(Transform inventoryPanel, Transform inventoryGrid)
        {
            float panelHeight = InventoryRows * SlotSize + (InventoryRows - 1) * SlotSpacing;

            RectTransform panelRect = (RectTransform)inventoryPanel;
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, HotbarHeight);
            panelRect.sizeDelta = new Vector2(PanelWidth, panelHeight);

            RectTransform gridRect = (RectTransform)inventoryGrid;
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.anchoredPosition = Vector2.zero;
            gridRect.sizeDelta = Vector2.zero;

            GridLayoutGroup layout = inventoryGrid.GetComponent<GridLayoutGroup>();
            if (layout == null)
            {
                layout = inventoryGrid.gameObject.AddComponent<GridLayoutGroup>();
            }

            layout.cellSize = new Vector2(SlotSize, SlotSize);
            layout.spacing = new Vector2(SlotSpacing, SlotSpacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = InventoryColumns;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.childAlignment = TextAnchor.MiddleCenter;
        }

        private static void RebuildInventorySlots(Transform inventoryGrid, GameObject slotPrefab)
        {
            for (int i = inventoryGrid.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(inventoryGrid.GetChild(i).gameObject);
            }

            int slotCount = InventoryColumns * InventoryRows;
            for (int i = 0; i < slotCount; i++)
            {
                GameObject slot = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, inventoryGrid);
                slot.name = i == 0 ? "ItemSlot" : $"ItemSlot ({i})";
                slot.SetActive(true);

                RectTransform rect = slot.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;
                }

                InventorySlotUI slotUI = slot.GetComponent<InventorySlotUI>();
                if (slotUI != null && slotUI.keyText != null)
                {
                    slotUI.keyText.text = string.Empty;
                    slotUI.keyText.gameObject.SetActive(false);
                }
            }
        }

        private static Transform FindDescendant(Transform parent, string objectName)
        {
            if (parent.name == objectName) return parent;

            foreach (Transform child in parent)
            {
                Transform result = FindDescendant(child, objectName);
                if (result != null) return result;
            }

            return null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new System.InvalidOperationException(message);
            }
        }
    }
}
