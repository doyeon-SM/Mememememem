using KMS.InventoryDuped;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KMS.EditorTools
{
    /// <summary>
    /// Applies the compact translucent inventory styling from the approved UI
    /// guideline without replacing inventory data or interaction components.
    /// Everything this tool owns is confined to the KMS player canvas prefab.
    /// </summary>
    public static class KMSInventoryGuidelineStyleSetup
    {
        private const string CanvasPrefabPath = "Assets/KMS/2.Prefabs/PlayerCanvas_Root.prefab";
        private const string ModernUiRoot = "Assets/5.Assets/Modern UI Pack/Textures";
        private const string KmsSpriteRoot = "Assets/KMS/1.Scripts/InventoryDuped/UI/Sprite";
        private const string RoundedFillPath = KmsSpriteRoot + "/Rounded Filled 1024px_pp1000.png";
        private const string RoundedOutlineSourcePath = ModernUiRoot + "/Border/Rounded/1024px/Rounded Outline 1024px - 1x.png";
        private const string RoundedOutlinePath = KmsSpriteRoot + "/Rounded Outline 1024px - 1x_pp1000.png";
        private const string ExistingSortIconPath = "Assets/KMS/3.UI/Icons/KMS_Inventory_Category.png";
        private const string TrashIconPath = ModernUiRoot + "/Icon/System/Trash.png";

        private static readonly Color32 OuterPanelColor = new Color32(43, 59, 64, 130);
        private static readonly Color32 InnerPanelColor = new Color32(10, 17, 20, 118);
        private static readonly Color32 SlotColor = new Color32(11, 18, 21, 196);
        private static readonly Color32 FooterActionColor = new Color32(112, 119, 119, 170);
        private static readonly Color32 BorderColor = new Color32(174, 184, 184, 48);

        private const float SlotSize = 74f;
        private const float SlotSpacing = 6f;

        [MenuItem("KMS/Inventory/Apply Guideline Visual Style")]
        public static void Apply()
        {
            EnsureKmsRoundedOutline();
            Sprite roundedFill = LoadSprite(RoundedFillPath);
            Sprite roundedOutline = LoadSprite(RoundedOutlinePath);
            Sprite existingSortIcon = LoadSprite(ExistingSortIconPath);
            Sprite trashIcon = LoadSprite(TrashIconPath);

            GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            try
            {
                InventoryUI inventoryUI = root.GetComponentInChildren<InventoryUI>(true);
                Require(inventoryUI != null && inventoryUI.inventoryPanel != null,
                    "Player inventory panel is missing.");

                RectTransform panel = inventoryUI.inventoryPanel.GetComponent<RectTransform>();
                RectTransform chrome = Find(panel, "LongTermInventoryChrome") as RectTransform;
                RectTransform scrollRoot = Find(panel, "InventoryScrollView") as RectTransform;
                RectTransform content = Find(scrollRoot, "Content") as RectTransform;
                Require(chrome != null && scrollRoot != null && content != null,
                    "Long-term inventory chrome or scroll content is missing.");

                ConfigurePanels(panel, chrome, roundedFill);
                ConfigureHeader(chrome, existingSortIcon);
                ConfigureScroll(scrollRoot, roundedFill);

                GridLayoutGroup gridLayout = inventoryUI.inventoryGrid.GetComponent<GridLayoutGroup>();
                Require(gridLayout != null, "Inventory grid layout is missing.");
                gridLayout.cellSize = new Vector2(SlotSize, SlotSize);
                gridLayout.spacing = new Vector2(SlotSpacing, SlotSpacing);
                gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayout.constraintCount = 5;
                RectTransform gridRect = inventoryUI.inventoryGrid as RectTransform;
                Require(gridRect != null, "Inventory grid RectTransform is missing.");
                Vector2 gridSize = gridRect.sizeDelta;
                gridSize.x = SlotSize * 5f + SlotSpacing * 4f;
                gridRect.sizeDelta = gridSize;

                foreach (InventorySlotUI slot in inventoryUI.inventoryGrid.GetComponentsInChildren<InventorySlotUI>(true))
                {
                    StyleSlot(slot.transform, roundedFill, roundedOutline);
                }

                RemoveMinimumSlotPlaceholders(inventoryUI.inventoryGrid);

                SerializedObject serializedInventory = new SerializedObject(inventoryUI);
                Button upgrade = serializedInventory.FindProperty("upgradeButton")?.objectReferenceValue as Button;
                InventorySlotUI trash = serializedInventory.FindProperty("trashSlotUI")?.objectReferenceValue as InventorySlotUI;
                Require(upgrade != null && trash != null, "Upgrade or trash action is missing.");

                ConfigureFooterAction(upgrade, content, Vector2.zero, roundedFill, roundedOutline);
                ConfigureTrash(trash, content, trashIcon, roundedFill, roundedOutline);
                ConfigureScrollableView(panel, upgrade, trash);

                PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KMSInventoryGuidelineStyleSetup] Applied reference-scale panels, the original 10-slot presentation, compact filters, and extra-thin rounded borders.");
        }

        [MenuItem("KMS/Validate/Inventory Guideline Visual Style")]
        public static void Validate()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            try
            {
                InventoryUI inventoryUI = root.GetComponentInChildren<InventoryUI>(true);
                Require(inventoryUI != null, "InventoryUI is missing.");
                RectTransform panel = inventoryUI.inventoryPanel.GetComponent<RectTransform>();
                Image panelImage = panel.GetComponent<Image>();
                Require(panelImage != null && Mathf.RoundToInt(panelImage.color.a * 255f) == 130,
                    "Outer inventory panel alpha is not 130.");

                Transform inner = Find(panel, "InventoryBackground");
                Require(inner != null && inner.GetComponent<Image>() != null
                        && RelativeLuminance(inner.GetComponent<Image>().color)
                        < RelativeLuminance(panelImage.color),
                    "Inner inventory panel is not darker than the outer panel.");

                Require(panelImage.sprite != null && panelImage.sprite.pixelsPerUnit >= 999f,
                    "Outer rounded sprite is not using the KMS PPU 1000 copy.");

                InventorySlotUI[] slots = inventoryUI.inventoryGrid.GetComponentsInChildren<InventorySlotUI>(true);
                Require(slots.Length == 60, "Inventory slot count changed.");
                foreach (InventorySlotUI slot in slots)
                {
                    Image border = Find(slot.transform, "GuidelineBorder")?.GetComponent<Image>();
                    Image background = Find(slot.transform, "Slot_BG")?.GetComponent<Image>();
                    Require(border?.sprite != null && border.sprite.pixelsPerUnit >= 999f,
                        $"Thin PPU 1000 slot border is missing: {slot.name}");
                    Require(background != null && background.rectTransform.anchorMin == Vector2.zero
                            && background.rectTransform.anchorMax == Vector2.one,
                        $"Slot background does not fill its cell: {slot.name}");
                }

                GridLayoutGroup layout = inventoryUI.inventoryGrid.GetComponent<GridLayoutGroup>();
                Require(layout != null && layout.cellSize == new Vector2(SlotSize, SlotSize),
                    "Inventory slots are not using the 76px reference scale.");
                Require(CountDirectChildrenWithPrefix(inventoryUI.inventoryGrid, "GuidelinePlaceholder_") == 0,
                    "Decorative placeholder slots were not removed.");

                SerializedObject serializedInventory = new SerializedObject(inventoryUI);
                Button upgrade = serializedInventory.FindProperty("upgradeButton")?.objectReferenceValue as Button;
                InventorySlotUI trash = serializedInventory.FindProperty("trashSlotUI")?.objectReferenceValue as InventorySlotUI;
                Require(upgrade != null && trash != null, "Footer actions are missing.");
                Require(((RectTransform)upgrade.transform).sizeDelta == new Vector2(SlotSize, SlotSize)
                        && ((RectTransform)trash.transform).sizeDelta == new Vector2(SlotSize, SlotSize),
                    "Footer actions are not slot-sized.");
                Require((upgrade.targetGraphic as Image)?.color == FooterActionColor,
                    "Upgrade action is not using the reference gray fill.");
                Require(Find(trash.transform, "Slot_BG")?.GetComponent<Image>()?.color == FooterActionColor,
                    "Trash action is not using the reference gray fill.");
                Require(Find(trash.transform, "TrashSlotIcon")?.GetComponent<Image>()?.sprite == LoadSprite(TrashIconPath),
                    "Modern UI Pack trash icon is not installed.");

                Button closeButton = Find(panel, "CloseShell")?.GetComponent<Button>();
                Require(closeButton != null && (closeButton.targetGraphic as Image)?.color.a == 0f,
                    "Close button still has a visible square background.");

                ScrollRect scrollRect = Find(panel, "InventoryScrollView")?.GetComponent<ScrollRect>();
                Require(scrollRect != null
                        && scrollRect.verticalScrollbarVisibility == ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport,
                    "Scrollbar does not auto-hide when the 5x5 content fits.");

                Debug.Log("[KMSInventoryGuidelineStyleSetup] Validation passed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void ApplyAndValidateFromCommandLine()
        {
            Apply();
            Validate();
        }

        private static void ConfigurePanels(RectTransform panel, RectTransform chrome, Sprite roundedFill)
        {
            Image outer = panel.GetComponent<Image>();
            if (outer == null) outer = panel.gameObject.AddComponent<Image>();
            ApplySliced(outer, roundedFill, OuterPanelColor);
            outer.raycastTarget = true;

            RectTransform inner = Find(panel, "InventoryBackground") as RectTransform;
            Require(inner != null, "InventoryBackground is missing.");
            inner.SetParent(panel, false);
            SetTopLeft(inner, new Vector2(14f, -102f), new Vector2(422f, 682f));
            ApplySliced(inner.GetComponent<Image>(), roundedFill, InnerPanelColor);
            inner.GetComponent<Image>().raycastTarget = false;
            inner.SetSiblingIndex(0);
            chrome.SetAsLastSibling();
        }

        private static void ConfigureHeader(RectTransform chrome, Sprite existingSortIcon)
        {
            TMP_Text title = Find(chrome, "Title")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                SetTopLeft(title.rectTransform, new Vector2(18f, -12f), new Vector2(170f, 32f));
                title.fontSize = 19f;
            }

            RectTransform close = Find(chrome, "CloseShell") as RectTransform;
            if (close != null)
            {
                SetTopRight(close, new Vector2(-16f, -12f), new Vector2(30f, 30f));
                SetNamedIconSize(close, "ModernUIIcon", new Vector2(20f, 20f));
                Button closeButton = close.GetComponent<Button>();
                Image closeBackground = closeButton != null ? closeButton.targetGraphic as Image : close.GetComponent<Image>();
                if (closeBackground != null)
                {
                    closeBackground.sprite = null;
                    closeBackground.color = Color.clear;
                }
            }

            RectTransform filters = Find(chrome, "P_sort") as RectTransform;
            Require(filters != null, "Inventory filter row is missing.");
            SetTopLeft(filters, new Vector2(18f, -56f), new Vector2(348f, 34f));
            KMSCapsuleGraphic capsule = filters.GetComponent<KMSCapsuleGraphic>();
            if (capsule != null) capsule.color = new Color32(7, 14, 18, 180);

            GridLayoutGroup layout = filters.GetComponent<GridLayoutGroup>();
            if (layout != null)
            {
                layout.cellSize = new Vector2(87f, 34f);
                layout.spacing = Vector2.zero;
            }

            string[] names = { "B_category", "B_Tool", "B_Material", "B_Food" };
            Vector2[] sizes =
            {
                new Vector2(20f, 20f), new Vector2(23f, 23f),
                new Vector2(27f, 27f), new Vector2(23f, 23f)
            };
            for (int i = 0; i < names.Length; i++)
            {
                Transform button = Find(filters, names[i]);
                SetNamedIconSize(button, "Image", sizes[i]);
                RectTransform separator = Find(button, "Separator") as RectTransform;
                if (separator != null)
                {
                    separator.anchoredPosition = new Vector2(43.5f, 0f);
                    separator.sizeDelta = new Vector2(1f, 22f);
                    Image separatorImage = separator.GetComponent<Image>();
                    if (separatorImage != null) separatorImage.color = new Color(1f, 1f, 1f, 0.32f);
                }
            }

            // Modern UI Pack has no ID-in-a-box or descending-line sort glyph.
            // Preserve the authored KMS glyph instead of substituting an unrelated
            // list-view icon.
            Button listButton = Find(chrome, "B_ID")?.GetComponent<Button>();
            Require(listButton != null, "Independent inventory sort button is missing.");
            SetTopRight((RectTransform)listButton.transform, new Vector2(-18f, -56f), new Vector2(42f, 34f));
            SetButtonSprite(listButton, existingSortIcon, new Vector2(24f, 24f));
        }

        private static void ConfigureScroll(RectTransform scrollRoot, Sprite roundedFill)
        {
            // InventoryBackground starts at x=14 and y=-102.  Keeping the scroll
            // content at x=24 and y=-112 gives the first slot an even 10px inset
            // from the inner panel on both axes.
            SetTopLeft(scrollRoot, new Vector2(24f, -112f), new Vector2(410f, 660f));
            Image raycast = scrollRoot.GetComponent<Image>();
            if (raycast != null) raycast.color = Color.clear;

            Scrollbar scrollbar = Find(scrollRoot, "Scrollbar Vertical")?.GetComponent<Scrollbar>();
            if (scrollbar == null) return;
            ScrollRect scrollRect = scrollRoot.GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
                scrollRect.verticalScrollbarSpacing = 4f;
            }
            RectTransform barRect = (RectTransform)scrollbar.transform;
            barRect.sizeDelta = new Vector2(8f, -10f);
            Image barBackground = scrollbar.GetComponent<Image>();
            if (barBackground != null) barBackground.color = new Color(1f, 1f, 1f, 0.08f);
            Image handle = scrollbar.handleRect != null ? scrollbar.handleRect.GetComponent<Image>() : null;
            if (handle != null) ApplySliced(handle, roundedFill, new Color(1f, 1f, 1f, 0.72f));
        }

        private static void ConfigureFooterAction(Button button, RectTransform content, Vector2 position,
            Sprite roundedFill, Sprite roundedOutline)
        {
            RectTransform rect = (RectTransform)button.transform;
            rect.SetParent(content, false);
            SetTopLeft(rect, position, new Vector2(SlotSize, SlotSize));
            ApplySliced(button.targetGraphic as Image, roundedFill, FooterActionColor);
            EnsureBorder(rect, roundedOutline);
            SetNamedIconSize(rect, "ModernUIIcon", new Vector2(34f, 34f));
        }

        private static void ConfigureTrash(InventorySlotUI trash, RectTransform content, Sprite iconSprite,
            Sprite roundedFill, Sprite roundedOutline)
        {
            RectTransform rect = (RectTransform)trash.transform;
            rect.SetParent(content, false);
            SetTopLeft(rect, new Vector2(320f, 0f), new Vector2(SlotSize, SlotSize));
            trash.gameObject.SetActive(true);
            StyleSlot(rect, roundedFill, roundedOutline);
            Image trashBackground = Find(rect, "Slot_BG")?.GetComponent<Image>();
            if (trashBackground != null)
                ApplySliced(trashBackground, roundedFill, FooterActionColor);

            Image icon = Find(rect, "TrashSlotIcon")?.GetComponent<Image>();
            Require(icon != null, "TrashSlotIcon is missing.");
            icon.sprite = iconSprite;
            icon.color = new Color(1f, 1f, 1f, 0.78f);
            icon.preserveAspect = true;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            icon.rectTransform.sizeDelta = new Vector2(34f, 34f);
        }

        private static void ConfigureScrollableView(
            RectTransform panel,
            Button upgrade,
            InventorySlotUI trash)
        {
            KMSScrollableInventoryView view = panel.GetComponent<KMSScrollableInventoryView>();
            Require(view != null, "KMSScrollableInventoryView is missing.");
            SerializedObject serialized = new SerializedObject(view);
            SetRef(serialized, "upgradeButtonRect", upgrade.GetComponent<RectTransform>());
            SetRef(serialized, "trashSlotRect", trash.GetComponent<RectTransform>());
            serialized.FindProperty("cellHeight").floatValue = SlotSize;
            serialized.FindProperty("rowSpacing").floatValue = SlotSpacing;
            serialized.FindProperty("upgradeHeight").floatValue = SlotSize;
            serialized.FindProperty("upgradeGap").floatValue = 8f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void StyleSlot(Transform slot, Sprite roundedFill, Sprite roundedOutline)
        {
            Image background = Find(slot, "Slot_BG")?.GetComponent<Image>();
            if (background != null)
            {
                Stretch(background.rectTransform);
                ApplySliced(background, roundedFill, SlotColor);
            }
            EnsureBorder((RectTransform)slot, roundedOutline);
        }

        private static void EnsureBorder(RectTransform root, Sprite outline)
        {
            RectTransform border = Find(root, "GuidelineBorder") as RectTransform;
            if (border == null)
            {
                GameObject borderObject = new GameObject("GuidelineBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                borderObject.layer = root.gameObject.layer;
                border = borderObject.GetComponent<RectTransform>();
                border.SetParent(root, false);
            }
            Stretch(border);
            Image image = border.GetComponent<Image>();
            image.sprite = outline;
            image.type = Image.Type.Sliced;
            image.color = BorderColor;
            image.pixelsPerUnitMultiplier = 1.5f;
            image.raycastTarget = false;
            Transform background = Find(root, "Slot_BG");
            if (background != null) border.SetSiblingIndex(background.GetSiblingIndex() + 1);
        }

        private static void SetButtonSprite(Button button, Sprite sprite, Vector2 size)
        {
            Transform iconTransform = Find(button.transform, "ModernUIIcon");
            Require(iconTransform != null, $"{button.name} icon is missing.");
            Image icon = iconTransform.GetComponent<Image>();
            icon.sprite = sprite;
            icon.color = Color.white;
            icon.preserveAspect = true;
            SetNamedIconSize(button.transform, "ModernUIIcon", size);
        }

        private static void SetNamedIconSize(Transform root, string iconName, Vector2 size)
        {
            RectTransform icon = Find(root, iconName) as RectTransform;
            if (icon == null) return;
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = Vector2.zero;
            icon.sizeDelta = size;
        }

        private static void ApplySliced(Image image, Sprite sprite, Color color)
        {
            Require(image != null, "Expected UI Image is missing.");
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = color;
        }

        private static void RemoveMinimumSlotPlaceholders(Transform grid)
        {
            for (int i = grid.childCount - 1; i >= 0; i--)
            {
                Transform child = grid.GetChild(i);
                if (child.name.StartsWith("GuidelinePlaceholder_", System.StringComparison.Ordinal))
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void EnsureKmsRoundedOutline()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(RoundedOutlinePath) == null)
            {
                Require(AssetDatabase.LoadAssetAtPath<Texture2D>(RoundedOutlineSourcePath) != null,
                    $"Modern UI outline source is missing: {RoundedOutlineSourcePath}");
                Require(AssetDatabase.CopyAsset(RoundedOutlineSourcePath, RoundedOutlinePath),
                    $"Could not copy rounded outline into KMS: {RoundedOutlinePath}");
            }

            TextureImporter importer = AssetImporter.GetAtPath(RoundedOutlinePath) as TextureImporter;
            Require(importer != null, $"Rounded outline importer is missing: {RoundedOutlinePath}");
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 1000f))
            {
                importer.spritePixelsPerUnit = 1000f;
                importer.SaveAndReimport();
            }
        }

        private static int CountDirectChildrenWithPrefix(Transform root, string prefix)
        {
            int count = 0;
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i).name.StartsWith(prefix, System.StringComparison.Ordinal)) count++;
            return count;
        }

        private static float RelativeLuminance(Color color)
        {
            return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Require(sprite != null, $"Modern UI Pack sprite is missing: {path}");
            return sprite;
        }

        private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetTopRight(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
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
