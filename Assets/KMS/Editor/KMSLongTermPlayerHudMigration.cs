using System;
using System.Linq;
using KMS;
using KMS.InventoryDuped;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KMS.EditorTools
{
    /// <summary>
    /// Applies the long-term exploration HUD structure while retaining the
    /// existing InventoryUI, sixty inventory slots, quick slots, and gameplay wiring.
    /// </summary>
    public static class KMSLongTermPlayerHudMigration
    {
        private const string CanvasPrefabPath = "Assets/KMS/2.Prefabs/0714_InventoryCanvas_Root.prefab";
        private const string KoreanFontAssetPath = "Assets/4.Font/JalnanGothic SDF.asset";
        private const string IconFolder = "Assets/KMS/3.UI/Icons";
        private const string SunIconPath = IconFolder + "/KMS_HUD_Sun.png";
        private const string MoonIconPath = IconFolder + "/KMS_HUD_Moon.png";
        private const string HungerIconPath = IconFolder + "/KMS_HUD_Hunger.png";
        private const string PackTextureRoot = "Assets/Pikachu/Modern UI Pack/Textures";
        private const string SquareFillPath = PackTextureRoot + "/Border/Flat/Square Filled.png";
        private const string RoundedFillPath = PackTextureRoot + "/Border/Rounded/128px/Rounded Filled 128px.png";
        private const string RadialFillPath = PackTextureRoot + "/Border/Radial/128px/Radial Filled 128px.png";
        private const string MapIconPath = PackTextureRoot + "/Icon/Map/Location Mark Filled.png";
        private const string BagIconPath = PackTextureRoot + "/Icon/Business & Commerce/Shopping Bag Filled.png";
        private const string DexIconPath = PackTextureRoot + "/Icon/Document/Book Filled.png";
        private const string CoinIconPath = PackTextureRoot + "/Icon/Business & Commerce/Diamond Coin Filled.png";
        private const string HeartIconPath = PackTextureRoot + "/Icon/Common/Heart Filled.png";
        private const string CloseIconPath = PackTextureRoot + "/Icon/Navigation/Close.png";
        private const string AddIconPath = PackTextureRoot + "/Icon/Navigation/Add.png";

        [MenuItem("KMS/Setup/Apply Long-term Exploration HUD")]
        public static void Run()
        {
            EnsureClockIcons();
            KMSPlayerUguiHudMigration.ApplyCurrentPlayerUguiHud();
            ConfigureCanvas();
            Validate();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KMSLongTermPlayerHudMigration] Exploration HUD and paged inventory applied.");
        }

        public static void RunFromCommandLine() => Run();

        private static void ConfigureCanvas()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            try
            {
                Transform hud = Find(root.transform, "HUDLayer");
                Require(hud != null, "HUDLayer is missing.");
                ConfigureClockAndRightMenu(hud);
                ConfigureSurvival(hud);
                ConfigureInventory(root);
                ConfigureQuickSlots(root);
                PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureClockAndRightMenu(Transform hud)
        {
            Transform oldMenu = Find(hud, "TopRightMenu");
            Require(oldMenu != null, "TopRightMenu is missing.");

            RectTransform menuRect = (RectTransform)oldMenu;
            SetRect(menuRect, Vector2.one, Vector2.one, new Vector2(-20f, -74f), new Vector2(68f, 250f));
            Image menuImage = oldMenu.GetComponent<Image>();
            if (menuImage != null) menuImage.color = Color.clear;

            Button map = Find(oldMenu, "MapButton")?.GetComponent<Button>();
            Button inventory = Find(oldMenu, "InventoryButton")?.GetComponent<Button>();
            Button collection = Find(oldMenu, "CollectionButton")?.GetComponent<Button>();
            Require(map != null && inventory != null && collection != null, "Right menu buttons are missing.");

            ConfigureRoundMenuButton(map, MapIconPath, new Vector2(0f, 0f));
            ConfigureRoundMenuButton(inventory, BagIconPath, new Vector2(0f, -78f));
            ConfigureRoundMenuButton(collection, DexIconPath, new Vector2(0f, -156f));

            TMP_Text realTime = Find(oldMenu, "RealTimeText")?.GetComponent<TMP_Text>();
            TMP_Text gold = Find(oldMenu, "GoldText")?.GetComponent<TMP_Text>();
            Require(realTime != null && gold != null, "Time or gold label is missing.");

            RectTransform clock = CreatePanel("ExplorationClock", hud, new Color(0.04f, 0.12f, 0.15f, 0.58f));
            SetRect(clock, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(250f, 54f));
            ApplySquareSkin(clock.GetComponent<Image>(), new Color32(0, 0, 0, 100));

            RectTransform icon = CreatePanel("DayNightIcon", clock, new Color(0.08f, 0.20f, 0.25f, 0.9f));
            SetRect(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(5f, 0f), new Vector2(48f, 44f));
            ApplyRadialSkin(icon.GetComponent<Image>(), new Color32(29, 46, 56, 235));

            RectTransform fill = CreatePanel("PhaseFill", icon, new Color(0f, 0f, 0f, 0.32f));
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(1f, 0f);
            fill.pivot = new Vector2(0.5f, 0f);
            fill.anchoredPosition = Vector2.zero;
            fill.sizeDelta = Vector2.zero;

            Image sun = CreateIcon("SunIcon", icon, AssetDatabase.LoadAssetAtPath<Sprite>(SunIconPath));
            Stretch(sun.rectTransform, new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.86f));
            Image moon = CreateIcon("MoonIcon", icon, AssetDatabase.LoadAssetAtPath<Sprite>(MoonIconPath));
            Stretch(moon.rectTransform, new Vector2(0.16f, 0.16f), new Vector2(0.84f, 0.84f));

            realTime.transform.SetParent(clock, false);
            SetRect(realTime.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(170f, 44f));
            realTime.alignment = TextAlignmentOptions.Center;
            realTime.fontSize = 22f;

            RectTransform gameGroup = CreateRect("GameTimeGroup", clock);
            SetRect(gameGroup, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(238f, 0f), new Vector2(112f, 44f));
            CanvasGroup canvasGroup = gameGroup.gameObject.AddComponent<CanvasGroup>();
            TMP_Text gameTime = CreateText("GameTimeText", gameGroup, "00:00", 20f, TextAlignmentOptions.Center, Color.white);
            Stretch(gameTime.rectTransform);

            KMSExplorationClockView clockView = clock.gameObject.AddComponent<KMSExplorationClockView>();
            SerializedObject serializedClock = new SerializedObject(clockView);
            SetRef(serializedClock, "root", clock);
            SetRef(serializedClock, "realTimeText", realTime);
            SetRef(serializedClock, "gameTimeText", gameTime);
            SetRef(serializedClock, "gameTimeGroup", canvasGroup);
            SetRef(serializedClock, "sunIcon", sun.gameObject);
            SetRef(serializedClock, "moonIcon", moon.gameObject);
            SetRef(serializedClock, "phaseFill", fill);
            serializedClock.ApplyModifiedPropertiesWithoutUndo();

            RectTransform goldPill = CreatePanel("GoldPill", hud, new Color(0.04f, 0.12f, 0.15f, 0.58f));
            SetRect(goldPill, Vector2.one, Vector2.one, new Vector2(-16f, -16f), new Vector2(250f, 54f));
            ApplySquareSkin(goldPill.GetComponent<Image>(), new Color32(0, 0, 0, 100));
            Image coin = CreateIcon("CoinIcon", goldPill, LoadPackSprite(CoinIconPath));
            SetRect(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(13f, 0f), new Vector2(30f, 30f));
            gold.transform.SetParent(goldPill, false);
            Stretch(gold.rectTransform, new Vector2(0.20f, 0f), new Vector2(0.94f, 1f));
            gold.alignment = TextAlignmentOptions.MidlineRight;
            gold.fontSize = 21f;
        }

        private static void ConfigureSurvival(Transform hud)
        {
            RectTransform survival = Find(hud, "SurvivalStatus") as RectTransform;
            Require(survival != null, "SurvivalStatus is missing.");
            SetRect(survival, Vector2.zero, Vector2.zero, new Vector2(16f, 16f), new Vector2(242f, 78f));
            Image survivalBackground = survival.GetComponent<Image>();
            if (survivalBackground == null) survivalBackground = survival.gameObject.AddComponent<Image>();
            ApplySquareSkin(survivalBackground, new Color32(0, 0, 0, 82));
            survivalBackground.raycastTarget = false;

            RectTransform health = Find(survival, "HealthBar") as RectTransform;
            RectTransform hunger = Find(survival, "HungerBar") as RectTransform;
            Require(health != null && hunger != null, "HealthBar or HungerBar is missing.");
            SetRect(health, Vector2.zero, Vector2.zero, new Vector2(10f, 42f), new Vector2(222f, 26f));
            SetRect(hunger, Vector2.zero, Vector2.zero, new Vector2(10f, 10f), new Vector2(222f, 26f));

            ConfigureSurvivalRow(health, LoadPackSprite(HeartIconPath), new Color32(13, 184, 101, 255));
            ConfigureSurvivalRow(hunger, AssetDatabase.LoadAssetAtPath<Sprite>(HungerIconPath),
                new Color32(255, 190, 18, 255));

            KMSPlayerHudView view = hud.GetComponent<KMSPlayerHudView>();
            if (view != null)
            {
                SerializedObject serializedView = new SerializedObject(view);
                serializedView.FindProperty("survivalMinWidth").floatValue = 242f;
                serializedView.FindProperty("survivalMaxWidth").floatValue = 242f;
                serializedView.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void ConfigureSurvivalRow(RectTransform row, Sprite iconSprite, Color fillColor)
        {
            Require(iconSprite != null, $"{row.name} icon is missing.");

            Image rowImage = row.GetComponent<Image>();
            if (rowImage != null)
            {
                rowImage.sprite = null;
                rowImage.color = Color.clear;
                rowImage.raycastTarget = false;
            }

            Image fill = Find(row, "Fill")?.GetComponent<Image>();
            TMP_Text value = Find(row, "ValueText")?.GetComponent<TMP_Text>();
            Require(fill != null && value != null, $"{row.name} fill or value label is missing.");

            Transform previousTrack = row.Find("StatusTrack");
            if (previousTrack != null)
            {
                fill.transform.SetParent(row, false);
                value.transform.SetParent(row, false);
                UnityEngine.Object.DestroyImmediate(previousTrack.gameObject);
            }
            Transform previousIcon = row.Find("StatusIcon");
            if (previousIcon != null) UnityEngine.Object.DestroyImmediate(previousIcon.gameObject);

            Image icon = CreateIcon("StatusIcon", row, iconSprite);
            SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(1f, 0f), new Vector2(24f, 24f));

            RectTransform track = CreatePanel("StatusTrack", row, new Color32(0, 0, 0, 155));
            SetRect(track, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(34f, 0f), new Vector2(188f, 20f));
            ApplySquareSkin(track.GetComponent<Image>(), new Color32(0, 0, 0, 155));

            fill.transform.SetParent(track, false);
            Stretch(fill.rectTransform);
            fill.sprite = null;
            fill.type = Image.Type.Simple;
            fill.color = fillColor;
            fill.raycastTarget = false;

            value.transform.SetParent(track, false);
            Stretch(value.rectTransform);
            value.rectTransform.offsetMin = new Vector2(4f, 0f);
            value.rectTransform.offsetMax = new Vector2(-5f, 0f);
            value.fontSize = 11f;
            value.enableAutoSizing = false;
            value.alignment = TextAlignmentOptions.MidlineRight;
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.color = Color.white;
        }

        private static void ConfigureInventory(GameObject root)
        {
            InventoryUI inventoryUI = root.GetComponentInChildren<InventoryUI>(true);
            Require(inventoryUI != null, "InventoryUI is missing.");
            Require(inventoryUI.inventoryPanel != null && inventoryUI.inventoryGrid != null, "Inventory panel/grid is missing.");

            RectTransform panel = inventoryUI.inventoryPanel.GetComponent<RectTransform>();
            SetRect(panel, Vector2.one, Vector2.one, new Vector2(-104f, -70f), new Vector2(430f, 610f));
            Image panelImage = panel.GetComponent<Image>();
            ApplySquareSkin(panelImage, new Color32(0, 0, 0, 100));
            Image inventoryBackground = Find(panel, "InventoryBackground")?.GetComponent<Image>();
            Require(inventoryBackground != null, "InventoryBackground is missing.");
            ApplySquareSkin(inventoryBackground, new Color32(8, 18, 24, 190));

            RectTransform grid = inventoryUI.inventoryGrid as RectTransform;
            Require(grid != null, "InventoryGrid is not a RectTransform.");
            SetRect(grid, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -104f), new Vector2(324f, 390f));
            GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
            Require(layout != null, "InventoryGrid has no GridLayoutGroup.");
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            layout.cellSize = new Vector2(60f, 60f);
            layout.spacing = new Vector2(6f, 6f);
            layout.childAlignment = TextAnchor.UpperLeft;
            foreach (InventorySlotUI slot in grid.GetComponentsInChildren<InventorySlotUI>(true))
            {
                Image slotBackground = Find(slot.transform, "Slot_BG")?.GetComponent<Image>();
                ApplySquareSkin(slotBackground, new Color32(17, 25, 31, 225));
                ConfigureAmountText(slot.amountText);
            }
            ItemDragUI dragUI = root.GetComponentInChildren<ItemDragUI>(true);
            if (dragUI != null) ConfigureAmountText(dragUI.amountText);

            SerializedObject serializedInventory = new SerializedObject(inventoryUI);
            Button upgrade = serializedInventory.FindProperty("upgradeButton")?.objectReferenceValue as Button;
            InventorySlotUI trash =
                serializedInventory.FindProperty("trashSlotUI")?.objectReferenceValue as InventorySlotUI;
            if (upgrade == null) upgrade = Find(panel, "B_Upgrade")?.GetComponent<Button>();
            Transform sortControls = Find(panel, "P_InventorySortControls") ?? Find(panel, "InventorySortControls");

            Transform previousChrome = panel.Find("LongTermInventoryChrome");
            if (previousChrome != null)
            {
                if (grid.IsChildOf(previousChrome))
                    grid.SetParent(panel, false);
                if (upgrade != null && upgrade.transform.IsChildOf(previousChrome))
                    upgrade.transform.SetParent(panel, false);
                if (trash != null && trash.transform.IsChildOf(previousChrome))
                    trash.transform.SetParent(panel, false);
                if (sortControls != null && sortControls.IsChildOf(previousChrome))
                    sortControls.SetParent(panel, false);
                UnityEngine.Object.DestroyImmediate(previousChrome.gameObject);
            }
            RectTransform chrome = CreateRect("LongTermInventoryChrome", panel);
            Stretch(chrome);

            TMP_Text title = CreateText("Title", chrome, "가방", 20f, TextAlignmentOptions.MidlineLeft, Color.white);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(180f, 34f));

            Button close = CreateButton("CloseShell", chrome, "×", new Color(1f, 1f, 1f, 0.12f));
            SetRect((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-12f, -10f), new Vector2(38f, 38f));
            SetButtonIcon(close, CloseIconPath, 9f);

            RectTransform filters = CreateRect("FilterShell", chrome);
            SetRect(filters, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -56f), new Vector2(388f, 38f));
            string[] labels = { "C", "EQP", "MAT", "FOD" };
            Button[] filterButtons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                filterButtons[i] = CreateButton($"Filter{i}", filters, labels[i], new Color(1f, 1f, 1f, 0.22f));
                SetRect((RectTransform)filterButtons[i].transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(i * 76f, 0f), new Vector2(70f, 34f));
            }

            Button menu = CreateButton("SortMenuButton", filters, "ID", new Color(1f, 1f, 1f, 0.22f));
            SetRect((RectTransform)menu.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(60f, 34f));

            if (sortControls != null)
            {
                sortControls.SetParent(chrome, false);
                RectTransform sortRect = sortControls as RectTransform;
                if (sortRect != null)
                    SetRect(sortRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -98f), new Vector2(210f, 90f));
            }

            KMSInventoryFilterShell filterShell = filters.gameObject.AddComponent<KMSInventoryFilterShell>();
            SerializedObject serializedFilter = new SerializedObject(filterShell);
            SetArray(serializedFilter, "filterButtons", filterButtons);
            SetRef(serializedFilter, "menuButton", menu);
            if (sortControls != null) SetRef(serializedFilter, "existingSortControls", sortControls.gameObject);
            serializedFilter.ApplyModifiedPropertiesWithoutUndo();

            Require(upgrade != null, "B_Upgrade is missing.");
            upgrade.transform.SetParent(chrome, false);
            RectTransform upgradeRect = (RectTransform)upgrade.transform;
            SetRect(upgradeRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -236f), new Vector2(324f, 54f));
            TMP_Text upgradeLabel = upgrade.GetComponentInChildren<TMP_Text>(true);
            if (upgradeLabel != null)
            {
                upgradeLabel.text = string.Empty;
            }
            ApplySquareSkin(upgrade.targetGraphic as Image, new Color32(255, 255, 255, 62));
            SetButtonIcon(upgrade, AddIconPath, 12f);

            KMSInventoryScrollSetup.Configure(root);
        }

        private static void ConfigureQuickSlots(GameObject root)
        {
            Transform quickRoot = Find(root.transform, "QuickSlot");
            Require(quickRoot != null, "QuickSlot is missing.");
            InventorySlotUI[] slots = quickRoot.GetComponentsInChildren<InventorySlotUI>(true)
                .OrderBy(slot => slot.slotIndex)
                .ToArray();
            Require(slots.Length == 10, "QuickSlot must contain ten slots.");

            for (int i = 0; i < slots.Length; i++)
            {
                TMP_Text key = slots[i].keyText;
                Image slotBackground = Find(slots[i].transform, "Slot_BG")?.GetComponent<Image>();
                ApplySquareSkin(slotBackground, new Color32(17, 25, 31, 205));
                ConfigureAmountText(slots[i].amountText);
                if (key == null) continue;
                key.text = i == 9 ? "0" : (i + 1).ToString();
                key.fontSize = 11f;
                key.alignment = TextAlignmentOptions.TopLeft;
                key.gameObject.SetActive(true);
                RectTransform keyRect = key.rectTransform;
                keyRect.anchorMin = Vector2.zero;
                keyRect.anchorMax = Vector2.one;
                keyRect.offsetMin = new Vector2(5f, 3f);
                keyRect.offsetMax = new Vector2(-3f, -3f);
            }
        }

        [MenuItem("KMS/Validate/Long-term Exploration HUD")]
        public static void Validate()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            try
            {
                Require(root.GetComponentInChildren<KMSExplorationClockView>(true) != null, "Exploration clock is missing.");
                KMSScrollableInventoryView scrollable =
                    root.GetComponentInChildren<KMSScrollableInventoryView>(true);
                Require(scrollable != null, "Scrollable inventory is missing.");
                InventoryUI inventory = root.GetComponentInChildren<InventoryUI>(true);
                Require(inventory.inventoryGrid.GetComponentsInChildren<InventorySlotUI>(true).Length == 60,
                    "Inventory data slots were not preserved.");
                Require(Find(root.transform, "QuickSlot").GetComponentsInChildren<InventorySlotUI>(true).Length == 10,
                    "Quick slots were not preserved.");

                int modernUiSpriteCount = root.GetComponentsInChildren<Image>(true)
                    .Count(image => image.sprite != null
                                    && AssetDatabase.GetAssetPath(image.sprite).StartsWith(
                                        PackTextureRoot,
                                        StringComparison.Ordinal));
                Require(modernUiSpriteCount >= 20,
                    $"Modern UI Pack skin is incomplete. Found {modernUiSpriteCount} sprite references.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureRoundMenuButton(Button button, string iconPath, Vector2 position)
        {
            RectTransform rect = (RectTransform)button.transform;
            SetRect(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Vector2(64f, 64f));
            Image image = button.targetGraphic as Image;
            ApplyRadialSkin(image, new Color32(19, 35, 45, 220));
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = string.Empty;
            }
            SetButtonIcon(button, iconPath, 17f);
        }

        private static RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static Image CreateIcon(string name, Transform parent, Sprite sprite)
        {
            Require(sprite != null, $"{name} sprite is missing.");
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite LoadPackSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Require(sprite != null, $"Modern UI Pack sprite is missing: {path}");
            return sprite;
        }

        private static void ApplyRoundedSkin(Image image, Color color)
        {
            if (image == null) return;
            image.sprite = LoadPackSprite(RoundedFillPath);
            image.type = Image.Type.Sliced;
            image.color = color;
        }

        private static void ApplySquareSkin(Image image, Color color)
        {
            if (image == null) return;
            image.sprite = LoadPackSprite(SquareFillPath);
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.color = color;
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

        private static void ApplyRadialSkin(Image image, Color color)
        {
            if (image == null) return;
            image.sprite = LoadPackSprite(RadialFillPath);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = color;
        }

        private static void SetButtonIcon(Button button, string iconPath, float padding)
        {
            if (button == null) return;

            Outline outline = button.GetComponent<Outline>();
            if (outline != null) UnityEngine.Object.DestroyImmediate(outline);

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = string.Empty;

            Transform previous = button.transform.Find("ModernUIIcon");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);

            Image icon = CreateIcon("ModernUIIcon", button.transform, LoadPackSprite(iconPath));
            icon.rectTransform.anchorMin = Vector2.zero;
            icon.rectTransform.anchorMax = Vector2.one;
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            icon.rectTransform.sizeDelta = new Vector2(-padding * 2f, -padding * 2f);
        }

        private static void EnsureClockIcons()
        {
            if (!AssetDatabase.IsValidFolder(IconFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/KMS/3.UI"))
                    AssetDatabase.CreateFolder("Assets/KMS", "3.UI");
                AssetDatabase.CreateFolder("Assets/KMS/3.UI", "Icons");
            }

            CopyIconIfMissing("Assets/Pikachu/Resource/Icon/noto--sun.png", SunIconPath);
            CopyIconIfMissing("Assets/Pikachu/Resource/Icon/fluent-emoji-flat--crescent-moon.png", MoonIconPath);
            CopyIconIfMissing("Assets/Pikachu/Resource/Icon/flowbite--bowl-food-solid (1).png", HungerIconPath);
            AssetDatabase.ImportAsset(SunIconPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(MoonIconPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(HungerIconPath, ImportAssetOptions.ForceUpdate);
        }

        private static void CopyIconIfMissing(string source, string destination)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(destination) != null) return;
            Require(AssetDatabase.CopyAsset(source, destination), $"Failed to copy {source} to KMS.");
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color)
        {
            RectTransform rect = CreatePanel(name, parent, color);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ApplySquareSkin(button.targetGraphic as Image, color);
            TMP_Text text = CreateText("Label", rect, label, 14f, TextAlignmentOptions.Center, Color.white);
            Stretch(text.rectTransform);
            return button;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, float size,
            TextAlignmentOptions alignment, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontAssetPath);
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect) => Stretch(rect, Vector2.zero, Vector2.one);

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetRef(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            Require(property != null, $"Serialized property '{propertyName}' was not found.");
            property.objectReferenceValue = value;
        }

        private static void SetArray(SerializedObject serialized, string propertyName, Button[] values)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            Require(property != null && property.isArray, $"Serialized array '{propertyName}' was not found.");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
