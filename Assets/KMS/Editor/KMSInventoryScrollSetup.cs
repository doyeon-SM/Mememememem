using KMS.InventoryDuped;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KMS.EditorTools
{
    public static class KMSInventoryScrollSetup
    {
        private const string CanvasPrefabPath = "Assets/KMS/2.Prefabs/0714_InventoryCanvas_Root.prefab";
        private const int UiLayer = 5;
        private const string PackTextureRoot = "Assets/Pikachu/Modern UI Pack/Textures";
        private const string SquareFillPath = PackTextureRoot + "/Border/Flat/Square Filled.png";
        private const string TrashIconPath = PackTextureRoot + "/Icon/System/Trash Filled.png";

        [MenuItem("KMS/Inventory/Convert Player Inventory To Scroll View")]
        public static void Apply()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            try
            {
                Configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
                Debug.Log($"[KMSInventoryScrollSetup] Scroll inventory applied: {CanvasPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("KMS/Validate/Player Inventory Scroll View")]
        public static void Validate()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            try
            {
                InventoryUI inventoryUI = root.GetComponentInChildren<InventoryUI>(true);
                Require(inventoryUI != null, "InventoryUI is missing.");

                RectTransform panel = inventoryUI.inventoryPanel.GetComponent<RectTransform>();
                Transform scrollRoot = Find(panel, "InventoryScrollView");
                Transform viewport = Find(scrollRoot, "Viewport");
                Transform content = Find(viewport, "Content");
                Button upgrade = Find(content, "B_Upgrade")?.GetComponent<Button>();
                SerializedObject serializedInventory = new SerializedObject(inventoryUI);
                InventorySlotUI trashSlot =
                    serializedInventory.FindProperty("trashSlotUI")?.objectReferenceValue as InventorySlotUI;

                Require(scrollRoot != null && scrollRoot.GetComponent<ScrollRect>() != null,
                    "Inventory ScrollRect is missing.");
                Require(viewport != null && viewport.GetComponent<RectMask2D>() != null,
                    "Inventory viewport mask is missing.");
                Require(content != null, "Inventory scroll content is missing.");
                Require(inventoryUI.inventoryGrid.IsChildOf(content),
                    "InventoryGrid is not inside scroll content.");
                Require(upgrade != null, "Upgrade button is not inside scroll content.");
                Require(trashSlot != null && trashSlot.transform.IsChildOf(panel),
                    "Trash slot is not inside the inventory panel.");
                Require(((RectTransform)trashSlot.transform).sizeDelta == new Vector2(60f, 60f),
                    "Trash slot does not use the inventory slot size.");
                Require(Find(trashSlot.transform, "TrashSlotIcon")?.GetComponent<Image>()?.sprite != null,
                    "Modern UI Pack trash icon is missing.");
                Require(trashSlot.amountText != null
                        && trashSlot.amountText.textWrappingMode == TextWrappingModes.NoWrap,
                    "Trash amount text is not configured for one line.");
                Require(inventoryUI.inventoryGrid.GetComponentsInChildren<InventorySlotUI>(true).Length == 60,
                    "The sixty inventory slots were not preserved.");
                Require(panel.GetComponent<KMSScrollableInventoryView>() != null,
                    "KMSScrollableInventoryView is missing.");
                Require(Find(panel, "PreviousPageButton") == null
                        && Find(panel, "NextPageButton") == null
                        && Find(panel, "PageLabel") == null,
                    "Legacy page controls are still present.");

                Debug.Log("[KMSInventoryScrollSetup] Validation passed: 60 slots, ScrollRect, viewport, content, upgrade button.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        internal static void Configure(GameObject root)
        {
            InventoryUI inventoryUI = root.GetComponentInChildren<InventoryUI>(true);
            Require(inventoryUI != null, "InventoryUI is missing.");
            Require(inventoryUI.inventoryPanel != null, "InventoryPanel is missing.");
            Require(inventoryUI.inventoryGrid is RectTransform, "InventoryGrid is missing.");

            RectTransform panel = inventoryUI.inventoryPanel.GetComponent<RectTransform>();
            RectTransform chrome = Find(panel, "LongTermInventoryChrome") as RectTransform;
            Require(chrome != null, "LongTermInventoryChrome is missing.");

            SerializedObject serializedInventory = new SerializedObject(inventoryUI);
            Button upgradeButton =
                serializedInventory.FindProperty("upgradeButton")?.objectReferenceValue as Button;
            if (upgradeButton == null)
                upgradeButton = Find(panel, "B_Upgrade")?.GetComponent<Button>();
            Require(upgradeButton != null, "B_Upgrade is missing.");
            InventorySlotUI trashSlot =
                serializedInventory.FindProperty("trashSlotUI")?.objectReferenceValue as InventorySlotUI;
            Require(trashSlot != null, "Trash slot is missing.");

            RectTransform grid = (RectTransform)inventoryUI.inventoryGrid;
            Transform previousScroll = Find(panel, "InventoryScrollView");
            if (previousScroll != null)
            {
                grid.SetParent(panel, false);
                upgradeButton.transform.SetParent(panel, false);
                Object.DestroyImmediate(previousScroll.gameObject);
            }

            DestroyIfPresent(panel, "PreviousPageButton");
            DestroyIfPresent(panel, "NextPageButton");
            DestroyIfPresent(panel, "PageLabel");

            RectTransform scrollRoot = CreateRect("InventoryScrollView", chrome);
            SetRect(scrollRoot, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -104f), new Vector2(388f, 420f));
            Image scrollRaycast = scrollRoot.gameObject.AddComponent<Image>();
            scrollRaycast.color = new Color(1f, 1f, 1f, 0f);

            ScrollRect scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 36f;

            RectTransform viewport = CreateRect("Viewport", scrollRoot);
            Stretch(viewport);
            viewport.offsetMax = new Vector2(-16f, 0f);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            grid.SetParent(content, false);
            SetRect(grid, new Vector2(0f, 1f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(324f, 126f));
            GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
            Require(layout != null, "InventoryGrid has no GridLayoutGroup.");
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            layout.cellSize = new Vector2(60f, 60f);
            layout.spacing = new Vector2(6f, 6f);
            layout.childAlignment = TextAnchor.UpperLeft;

            RectTransform upgradeRect = (RectTransform)upgradeButton.transform;
            upgradeRect.SetParent(content, false);
            SetRect(upgradeRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -136f), new Vector2(324f, 54f));

            Scrollbar scrollbar = CreateVerticalScrollbar(scrollRoot);
            scrollRect.content = content;
            scrollRect.viewport = viewport;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scrollRect.verticalScrollbarSpacing = 4f;

            Button closeButton = Find(chrome, "CloseShell")?.GetComponent<Button>();
            KMSScrollableInventoryView view = panel.GetComponent<KMSScrollableInventoryView>();
            if (view == null) view = panel.gameObject.AddComponent<KMSScrollableInventoryView>();

            SerializedObject serializedView = new SerializedObject(view);
            SetRef(serializedView, "inventoryUI", inventoryUI);
            SetRef(serializedView, "inventoryGrid", grid);
            SetRef(serializedView, "upgradeButton", upgradeButton);
            SetRef(serializedView, "closeButton", closeButton);
            SetRef(serializedView, "scrollRect", scrollRect);
            SetRef(serializedView, "contentRect", content);
            SetRef(serializedView, "gridRect", grid);
            SetRef(serializedView, "upgradeButtonRect", upgradeRect);
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            ConfigureTrashSlot(chrome, trashSlot);
        }

        private static void ConfigureTrashSlot(RectTransform chrome, InventorySlotUI trashSlot)
        {
            RectTransform trashRect = (RectTransform)trashSlot.transform;
            trashRect.SetParent(chrome, false);
            SetRect(trashRect, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-18f, 16f), new Vector2(60f, 60f));
            trashSlot.gameObject.SetActive(true);

            Image background = Find(trashSlot.transform, "Slot_BG")?.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquareFillPath);
                background.type = Image.Type.Sliced;
                background.preserveAspect = false;
                background.color = new Color32(17, 25, 31, 225);
            }

            Transform previousIcon = Find(trashSlot.transform, "TrashSlotIcon");
            if (previousIcon != null) Object.DestroyImmediate(previousIcon.gameObject);

            RectTransform iconRect = CreateRect("TrashSlotIcon", trashSlot.transform);
            Stretch(iconRect);
            iconRect.offsetMin = new Vector2(14f, 14f);
            iconRect.offsetMax = new Vector2(-14f, -14f);
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TrashIconPath);
            icon.color = icon.sprite != null
                ? new Color(1f, 1f, 1f, 0.72f)
                : Color.black;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            Transform itemIcon = Find(trashSlot.transform, "Item_Icon");
            if (itemIcon != null) iconRect.SetSiblingIndex(itemIcon.GetSiblingIndex());

            ConfigureAmountText(trashSlot.amountText);
            if (trashSlot.keyText != null) trashSlot.keyText.gameObject.SetActive(false);

            SerializedObject serializedSlot = new SerializedObject(trashSlot);
            SetRef(serializedSlot, "emptyPlaceholder", iconRect.gameObject);
            serializedSlot.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAmountText(TMP_Text amount)
        {
            if (amount == null) return;

            RectTransform rect = amount.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-4f, 4f);
            rect.sizeDelta = new Vector2(52f, 24f);

            amount.text = amount.text.Trim();
            amount.alignment = TextAlignmentOptions.BottomRight;
            amount.textWrappingMode = TextWrappingModes.NoWrap;
            amount.overflowMode = TextOverflowModes.Overflow;
            amount.enableAutoSizing = true;
            amount.fontSize = 20f;
            amount.fontSizeMin = 12f;
            amount.fontSizeMax = 20f;
        }

        private static Scrollbar CreateVerticalScrollbar(RectTransform parent)
        {
            RectTransform root = CreateRect("Scrollbar Vertical", parent);
            root.anchorMin = new Vector2(1f, 0f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(10f, -4f);

            Image background = root.gameObject.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.1f);

            Scrollbar scrollbar = root.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            RectTransform slidingArea = CreateRect("Sliding Area", root);
            Stretch(slidingArea);
            slidingArea.offsetMin = new Vector2(2f, 2f);
            slidingArea.offsetMax = new Vector2(-2f, -2f);

            RectTransform handle = CreateRect("Handle", slidingArea);
            Stretch(handle);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(1f, 1f, 1f, 0.55f);

            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            return scrollbar;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = parent != null ? parent.gameObject.layer : UiLayer;
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x, anchorMax.y);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void DestroyIfPresent(Transform root, string name)
        {
            Transform target = Find(root, name);
            if (target != null) Object.DestroyImmediate(target.gameObject);
        }

        private static void SetRef(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Require(property != null, $"Serialized property is missing: {propertyName}");
            property.objectReferenceValue = value;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new MissingReferenceException(message);
        }
    }
}
