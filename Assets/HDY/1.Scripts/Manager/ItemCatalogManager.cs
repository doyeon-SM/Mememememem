using System.Collections.Generic;
using System.Globalization;
using HDY.Forge;
using KGH.Data;
using UnityEngine;

namespace HDY.Item
{
    /// <summary>
    /// 아이템 데이터(ItemData), 제작 레시피(RecipeData), 상점 품목(ShopItemData), 요리 레시피(CookRecipeData)를
    /// 보관하는 매니저.
    /// Item_ID / Recipe_Item_ID / Result_Item_ID를 키로 하는 딕셔너리 탐색을 전제로 함.
    /// 씬에 배치되어 DontDestroyOnLoad로 유지되는 파괴불가 싱글톤 (ItemCatalogManager는 계속 싱글톤 유지).
    ///
    /// [HDY 요청 - 시트 마이그레이션] 개별 ItemData/RecipeData/ShopItemData SO를 Inspector에 하나씩
    /// 드래그하던 방식에서 시트(TextAsset, 쉼표 구분 CSV) 기반으로 전환했다. Awake 시 각 시트를 파싱해
    /// 행마다 ScriptableObject.CreateInstance<T>()로 런타임 인스턴스를 만들어 채운다.
    /// (강화 개체용 ForgeInstanceItemDataProvider가 이미 쓰던 것과 동일한 패턴.)
    /// 아이콘(Sprite)은 시트에 담을 수 없어 ItemIconTable로 따로 분리해 관리한다.
    ///
    /// [HDY 요청 - txt to csv 마이그레이션] 시트 원본 파일은 전부 tsv(탭 구분) .txt에서 csv(쉼표 구분)
    /// .csv로 전환했다. 엑셀에서 더블클릭으로 바로 열리게 하기 위함. AssetDatabase.MoveAsset으로 확장자만
    /// 바꿔 기존 TextAsset 참조(GUID)는 그대로 유지되므로 Inspector 재연결은 필요 없다. 데이터 자체에
    /// 쉼표가 들어가는 필드는 없는 것을 확인했다(있으면 Split(',')에서 컬럼이 밀리므로 주의).
    ///
    /// [ShopItemData 참고] ShopStockManager가 재고를 Dictionary<ShopItemData, int>로(=객체 동일성
    /// 기준) 관리하기 때문에, FindShopItemData(id)는 매번 새 인스턴스를 만들지 않고 Awake 시 한 번만
    /// 만들어 캐싱한 같은 인스턴스를 계속 반환한다 - ItemData/RecipeData와 동일한 원칙.
    ///
    /// [대장간 연동] Item_ID가 "{BaseItemId}@{InstanceId}" 형태의 합성 ID(강화 개체)이면
    /// 일반 딕셔너리 탐색 대신 ForgeInstanceItemDataProvider에 위임해 강화 보너스가 반영된
    /// 런타임 전용 ItemData를 받아온다. 이 분기 덕분에 WorldObject/PlayerHarvestController 등
    /// 다른 팀 코드는 지금처럼 FindItemData(itemId) → Value만 읽어도 강화가 자동 반영된다.
    ///
    /// [HDY 요청 - 요리 레시피 추가] 제작 레시피(RecipeData)와 동일한 시트 파싱 패턴으로 요리 레시피
    /// (CookRecipeData)를 추가했다. 제작 레시피와의 차이는 재료 쪽인데, 요리 재료는 기획상 항상 1개씩만
    /// 소비되므로 Recipe_Requset_Item_Data(Item_ID+Amount) 대신 List&lt;string&gt;으로 단순화했다.
    /// 요리시설(CookingFacilityData)은 ShopData와 동일하게 시트가 아니라 SO 에셋 자체에 취급 레시피
    /// (Result_Item_ID) 목록을 직접 채우는 방식이라 여기서는 다루지 않는다.
    ///
    /// [HDY 요청 - KMS 크로스 승인 - 내구도] 시트에 이미 추가되어 있던 Durability 컬럼(Size 다음, 마지막
    /// 컬럼)을 파싱해 ItemData.MaxDurability에 채운다. Size와 동일하게 없는 행(구버전 시트)도 방어적으로
    /// 처리한다.
    /// </summary>
    public class ItemCatalogManager : MonoBehaviour
    {
        public static ItemCatalogManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 음식 효과 테이블을 먼저 구성해야 BuildDictionary()의 ParseItemRow()에서 참조할 수 있다.
            foodEffectTable = new FoodEffectTable(foodEffectCatalogSheet != null ? foodEffectCatalogSheet.text : string.Empty);
            alchemyExchangeTable = new HDY.Shop.AlchemyExchangeTable(alchemyExchangeCatalogSheet != null ? alchemyExchangeCatalogSheet.text : string.Empty);
            weaponStatsTable = new KMS.Combat.WeaponStatsTable(weaponCatalogSheet != null ? weaponCatalogSheet.text : string.Empty);


            BuildDictionary();
            BuildRecipeDictionary();
            BuildShopItemDictionary();
            BuildCookRecipeDictionary();
        }

        [Header("아이템 데이터 시트 (쉼표 구분 CSV, Item_ID 기준으로 파싱)")]
        [SerializeField] private TextAsset itemCatalogSheet;

        [Header("음식 효과 시트 (쉼표 구분 CSV, Item_ID + EatEffects만 있는 별도 시트)")]
        [SerializeField] private TextAsset foodEffectCatalogSheet;

        // 음식 효과 시트를 파싱해 Item_ID -> 섭취 효과 목록으로 조회하는 테이블. Awake에서 구성한다.
        private FoodEffectTable foodEffectTable;

        [Header("연금술 교환 레시피 시트 (쉼표 구분 CSV, Recipe_ID 기준으로 파싱 - HDY 요청, 연금술사의 집)")]
        [SerializeField] private TextAsset alchemyExchangeCatalogSheet;

        // 연금술 교환 레시피 시트를 파싱해 Recipe_ID -> AlchemyExchangeRecipe로 조회하는 테이블. Awake에서 구성한다.
        private HDY.Shop.AlchemyExchangeTable alchemyExchangeTable;

        [Header("아이템 아이콘 테이블 (Item_ID -> Sprite)")]
        [SerializeField] private ItemIconTable iconTable;

        [Header("제작 레시피 시트 (쉼표 구분 CSV, Recipe_Item_ID 기준으로 파싱)")]
        [SerializeField] private TextAsset recipeCatalogSheet;

        [Header("상점 품목 시트 (쉼표 구분 CSV, Item_ID 기준으로 파싱)")]
        [SerializeField] private TextAsset shopItemCatalogSheet;

        [Header("요리 레시피 시트 (쉼표 구분 CSV, Result_Item_ID 기준으로 파싱)")]
        [SerializeField] private TextAsset cookRecipeCatalogSheet;

        // [멤] 스킬 시스템용 원거리 무기. 이전에는 WeaponItemData를 수동으로 만든 SO 자산을 이 리스트에 하나하나 등록해야 했지만, "SO 말고 id 기반으로 구조화해달라"는 요청에 따라 폐지했다. 이제 무기의 기본 데이터(이름/카테고리 등)는 다른 아이템과 동일하게 itemCatalogSheet에서, 무기 전용 추가 데이터(사거리/투사체 스펙 등)는 아래 weaponCatalogSheet에서 Item_ID 기준으로 들어간다(WeaponStatsTable이 파싱). 투사체 Prefab 참조만 csv에 담을 수 없어 projectileTable(ProjectilePrefabTable)에서 따로 조회한다.
        [Header("무기 전용 데이터 시트 (쉼표 구분 CSV, Item_ID 기준 - AttackDistance,AttackCooldown,ProjectileId,ProjectileSpeed,ProjectileLifetime,ProjectileDamage,ProjectileAttackCooldown)")]
        [SerializeField] private TextAsset weaponCatalogSheet;

        [Header("ProjectileId -> 투사체 Prefab 테이블 (기본 공격/스킬 공용)")]
        [SerializeField] private KMS.Combat.ProjectilePrefabTable projectileTable;

        // [멤] Item_ID -> 무기 전용 데이터 조회 테이블. foodEffectTable과 동일하게 Awake에서 BuildDictionary() 전에 구성한다.
        private KMS.Combat.WeaponStatsTable weaponStatsTable;

        private readonly List<ItemData> itemDataList = new List<ItemData>();
        public IReadOnlyList<ItemData> ItemDataList => itemDataList;

        [Header("Item_ID -> ItemData 딕셔너리")]
        private Dictionary<string, ItemData> itemDictionary = new Dictionary<string, ItemData>();

        private readonly List<HDY.Recipe.RecipeData> recipeDataList = new List<HDY.Recipe.RecipeData>();
        public IReadOnlyList<HDY.Recipe.RecipeData> RecipeDataList => recipeDataList;

        [Header("Recipe_Item_ID -> RecipeData 딕셔너리")]
        private Dictionary<string, HDY.Recipe.RecipeData> recipeDictionary = new Dictionary<string, HDY.Recipe.RecipeData>();

        private readonly List<HDY.Shop.ShopItemData> shopItemDataList = new List<HDY.Shop.ShopItemData>();
        public IReadOnlyList<HDY.Shop.ShopItemData> ShopItemDataList => shopItemDataList;

        [Header("Item_ID -> ShopItemData 딕셔너리")]
        private Dictionary<string, HDY.Shop.ShopItemData> shopItemDictionary = new Dictionary<string, HDY.Shop.ShopItemData>();

        private readonly List<HDY.Cook.CookRecipeData> cookRecipeDataList = new List<HDY.Cook.CookRecipeData>();
        public IReadOnlyList<HDY.Cook.CookRecipeData> CookRecipeDataList => cookRecipeDataList;

        [Header("Result_Item_ID -> CookRecipeData 딕셔너리")]
        private Dictionary<string, HDY.Cook.CookRecipeData> cookRecipeDictionary = new Dictionary<string, HDY.Cook.CookRecipeData>();

        /// <summary>
        /// 시트를 파싱해 행마다 런타임 ItemData 인스턴스를 만들고 Item_ID 기준으로 딕셔너리에 채운다.
        /// Item_ID가 중복되면 먼저 등록된 항목을 유지한다.
        /// </summary>
        private void BuildDictionary()
        {
            itemDictionary.Clear();
            itemDataList.Clear();

            if (itemCatalogSheet == null)
            {
                Debug.LogWarning("[ItemCatalogManager] itemCatalogSheet가 비어있습니다.");
                return;
            }

            var lines = itemCatalogSheet.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0번째 줄은 헤더라 건너뜀
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(',');
                if (cols.Length < 8)
                {
                    Debug.LogWarning($"[ItemCatalogManager] 아이템 시트 {i + 1}번째 줄 컬럼 수가 부족합니다: {line}");
                    continue;
                }

                var data = ParseItemRow(cols);
                if (data == null || string.IsNullOrEmpty(data.Item_ID)) continue;

                if (!itemDictionary.ContainsKey(data.Item_ID))
                {
                    itemDictionary.Add(data.Item_ID, data);
                    itemDataList.Add(data);
                }
                else
                {
                    Debug.LogWarning($"[ItemCatalogManager] Item_ID가 중복되었습니다: {data.Item_ID} (먼저 등록된 항목을 유지합니다)");
                }
            }
        }

                private void BuildRecipeDictionary()
        {
            recipeDictionary.Clear();
            recipeDataList.Clear();

            if (recipeCatalogSheet == null)
            {
                Debug.LogWarning("[ItemCatalogManager] recipeCatalogSheet가 비어있습니다.");
                return;
            }

            var lines = recipeCatalogSheet.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0번째 줄은 헤더라 건너뜀
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(',');
                if (cols.Length < 3)
                {
                    Debug.LogWarning($"[ItemCatalogManager] 레시피 시트 {i + 1}번째 줄 컬럼 수가 부족합니다: {line}");
                    continue;
                }

                var recipe = ParseRecipeRow(cols);
                if (recipe == null || string.IsNullOrEmpty(recipe.Recipe_Item_ID)) continue;

                if (!recipeDictionary.ContainsKey(recipe.Recipe_Item_ID))
                {
                    recipeDictionary.Add(recipe.Recipe_Item_ID, recipe);
                    recipeDataList.Add(recipe);
                }
                else
                {
                    Debug.LogWarning($"[ItemCatalogManager] Recipe_Item_ID가 중복되었습니다: {recipe.Recipe_Item_ID} (먼저 등록된 항목을 유지합니다)");
                }
            }
        }

        /// <summary>
        /// 상점 품목 시트를 파싱해 행마다 런타임 ShopItemData 인스턴스를 만들고 Item_ID 기준으로 딕셔너리에 채운다.
        /// Item_ID가 중복되면 먼저 등록된 항목을 유지한다.
        /// </summary>
        private void BuildShopItemDictionary()
        {
            shopItemDictionary.Clear();
            shopItemDataList.Clear();

            if (shopItemCatalogSheet == null)
            {
                Debug.LogWarning("[ItemCatalogManager] shopItemCatalogSheet가 비어있습니다.");
                return;
            }

            var lines = shopItemCatalogSheet.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0번째 줄은 헤더라 건너뜀
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(',');
                if (cols.Length < 7)
                {
                    Debug.LogWarning($"[ItemCatalogManager] 상점 품목 시트 {i + 1}번째 줄 컬럼 수가 부족합니다: {line}");
                    continue;
                }

                var shopItem = ParseShopItemRow(cols);
                if (shopItem == null || string.IsNullOrEmpty(shopItem.Item_ID)) continue;

                if (!shopItemDictionary.ContainsKey(shopItem.Item_ID))
                {
                    shopItemDictionary.Add(shopItem.Item_ID, shopItem);
                    shopItemDataList.Add(shopItem);
                }
                else
                {
                    Debug.LogWarning($"[ItemCatalogManager] 상점 품목 Item_ID가 중복되었습니다: {shopItem.Item_ID} (먼저 등록된 항목을 유지합니다)");
                }
            }
        }

        /// <summary>
        /// 요리 레시피 시트를 파싱해 행마다 런타임 CookRecipeData 인스턴스를 만들고 Result_Item_ID 기준으로
        /// 딕셔너리에 채운다. Result_Item_ID가 중복되면 먼저 등록된 항목을 유지한다.
        /// </summary>
        private void BuildCookRecipeDictionary()
        {
            cookRecipeDictionary.Clear();
            cookRecipeDataList.Clear();

            if (cookRecipeCatalogSheet == null)
            {
                Debug.LogWarning("[ItemCatalogManager] cookRecipeCatalogSheet가 비어있습니다.");
                return;
            }

            var lines = cookRecipeCatalogSheet.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0번째 줄은 헤더라 건너뜀
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(',');
                if (cols.Length < 3)
                {
                    Debug.LogWarning($"[ItemCatalogManager] 요리 레시피 시트 {i + 1}번째 줄 컬럼 수가 부족합니다: {line}");
                    continue;
                }

                var cookRecipe = ParseCookRecipeRow(cols);
                if (cookRecipe == null || string.IsNullOrEmpty(cookRecipe.Result_Item_ID)) continue;

                if (!cookRecipeDictionary.ContainsKey(cookRecipe.Result_Item_ID))
                {
                    cookRecipeDictionary.Add(cookRecipe.Result_Item_ID, cookRecipe);
                    cookRecipeDataList.Add(cookRecipe);
                }
                else
                {
                    Debug.LogWarning($"[ItemCatalogManager] 요리 레시피 Result_Item_ID가 중복되었습니다: {cookRecipe.Result_Item_ID} (먼저 등록된 항목을 유지합니다)");
                }
            }
        }

        /// <summary>시트 한 줄(컬럼 배열)을 런타임 ItemData로 변환한다.</summary>
private ItemData ParseItemRow(string[] cols)
        {
            var category = ParseEnum<ItemCategory>(cols[4]);
            ItemData data = category == ItemCategory.Weapon
                ? (ItemData)ScriptableObject.CreateInstance<KMS.Combat.WeaponItemData>()
                : ScriptableObject.CreateInstance<ItemData>();

            data.Item_ID = cols[0].Trim();
            data.ItemName = cols[1].Trim();
            data.Value = ParseInt(cols[2]);
            data.MaxStack = ParseInt(cols[3]);
            data.Category = category;
            data.UseAction = ParseEnum<UseAction>(cols[5]);
            data.ObjectType = ParseEnum<ObjectType>(cols[6]);
            data.ItemClass = ParseEnum<CommonClass>(cols[7]);

            data.EatEffects = foodEffectTable != null
                ? foodEffectTable.GetEffects(data.Item_ID)
                : new List<ItemEffect>();

            data.Size = cols.Length > 8 ? cols[8].Trim() : string.Empty;

            data.MaxDurability = cols.Length > 9 ? ParseInt(cols[9]) : 0;

            data.ItemIcon = iconTable != null ? iconTable.GetIcon(data.Item_ID) : null;

            if (data is KMS.Combat.WeaponItemData weaponData)
            {
                ApplyWeaponStats(weaponData);
            }

            return data;
        }

// [멤] WeaponCatalog.csv에서 이 Item_ID에 해당하는 무기 전용 데이터를 찾아 채운다. 행이 없으면 경고만 남기고 WeaponItemData의 기본값(0/null)을 그대로 둔다.
        private void ApplyWeaponStats(KMS.Combat.WeaponItemData weaponData)
        {
            if (weaponStatsTable != null && weaponStatsTable.TryGetRow(weaponData.Item_ID, out var row))
            {
                weaponData.AttackDistance = row.AttackDistance;
                weaponData.AttackCooldown = row.AttackCooldown;
                weaponData.ProjectileSpeed = row.ProjectileSpeed;
                weaponData.ProjectileLifetime = row.ProjectileLifetime;
                weaponData.ProjectileDamage = row.ProjectileDamage;
                weaponData.ProjectileAttackCooldown = row.ProjectileAttackCooldown;
                weaponData.ProjectilePrefab = projectileTable != null ? projectileTable.GetPrefab(row.ProjectileId) : null;
            }
            else
            {
                Debug.LogWarning($"[ItemCatalogManager] 무기 Item_ID({weaponData.Item_ID})에 대한 WeaponCatalog.csv 행을 찾을 수 없습니다 - 기본값을 사용합니다.");
            }
        }


        /// <summary>레시피 시트 한 줄(컬럼 배열)을 런타임 RecipeData로 변환한다.</summary>
        private HDY.Recipe.RecipeData ParseRecipeRow(string[] cols)
        {
            var recipe = ScriptableObject.CreateInstance<HDY.Recipe.RecipeData>();

            recipe.Recipe_Item_ID = cols[0].Trim();
            recipe.time = ParseFloat(cols[1]);
            recipe.Requset_Items_ID = ParseMaterials(cols[2]);

            return recipe;
        }

        /// <summary>
        /// 상점 품목 시트 한 줄(컬럼 배열)을 런타임 ShopItemData로 변환한다.
        /// [HDY 요청 - 컬럼 순서 변경] 컬럼 순서: Item_ID, Selling_Price, Selling_MaxAmount,
        /// Purchase_Price_Golds, Purchase_Material_ID, Purchase_Material_Amount, Purchase_MaxAmount.
        /// Purchase_Material_ID가 비어있으면 골드 구매(Purchase_Price_Material = null)로 처리한다.
        /// </summary>
        private HDY.Shop.ShopItemData ParseShopItemRow(string[] cols)
        {
            var shopItem = ScriptableObject.CreateInstance<HDY.Shop.ShopItemData>();

            shopItem.Item_ID = cols[0].Trim();
            shopItem.Selling_Price = ParseInt(cols[1]);
            shopItem.Selling_MaxAmount = ParseInt(cols[2]);
            shopItem.Purchase_Price_Golds = ParseInt(cols[3]);

            var materialId = cols[4].Trim();
            shopItem.Purchase_Price_Material = string.IsNullOrEmpty(materialId)
                ? null
                : new HDY.Shop.MaterialCost { Item_ID = materialId, Amount = ParseInt(cols[5]) };

            shopItem.Purchase_MaxAmount = ParseInt(cols[6]);

            return shopItem;
        }

        /// <summary>
        /// 요리 레시피 시트 한 줄(컬럼 배열)을 런타임 CookRecipeData로 변환한다.
        /// 컬럼 순서: Result_Item_ID, Time, Ingredients("item_a;item_b" 형식, 재료는 항상 1개씩이라 수량 없음).
        /// </summary>
        private HDY.Cook.CookRecipeData ParseCookRecipeRow(string[] cols)
        {
            var cookRecipe = ScriptableObject.CreateInstance<HDY.Cook.CookRecipeData>();

            cookRecipe.Result_Item_ID = cols[0].Trim();
            cookRecipe.Time = ParseFloat(cols[1]);
            cookRecipe.Ingredient_Item_IDs = ParseIngredientIds(cols[2]);

            return cookRecipe;
        }

        private static int ParseInt(string s)
        {
            return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        private static float ParseFloat(string s)
        {
            return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
        }

        private static T ParseEnum<T>(string s) where T : struct
        {
            return System.Enum.TryParse(s.Trim(), out T value) ? value : default;
        }

        /// <summary>"item_wood:30;item_baseblueprint:1" 형식을 파싱한다. 빈 문자열이면 빈 리스트를 반환한다.</summary>
        private static List<HDY.Recipe.Recipe_Requset_Item_Data> ParseMaterials(string raw)
        {
            var materials = new List<HDY.Recipe.Recipe_Requset_Item_Data>();
            if (string.IsNullOrWhiteSpace(raw)) return materials;

            var entries = raw.Split(';');
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;

                var parts = entry.Split(':');
                if (parts.Length != 2) continue;

                var itemId = parts[0].Trim();
                if (string.IsNullOrEmpty(itemId)) continue;

                if (int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
                {
                    materials.Add(new HDY.Recipe.Recipe_Requset_Item_Data { Item_ID = itemId, Amount = amount });
                }
            }

            return materials;
        }

        /// <summary>
        /// "item_berry;item_sugar" 형식을 파싱한다. 요리 재료는 기획상 항상 1개씩만 소비되므로
        /// 제작 레시피(ParseMaterials)와 달리 수량 없이 Item_ID 문자열만 리스트로 담는다.
        /// 빈 문자열이면 빈 리스트를 반환한다.
        /// </summary>
        private static List<string> ParseIngredientIds(string raw)
        {
            var ingredients = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return ingredients;

            var entries = raw.Split(';');
            foreach (var entry in entries)
            {
                var itemId = entry.Trim();
                if (string.IsNullOrEmpty(itemId)) continue;

                ingredients.Add(itemId);
            }

            return ingredients;
        }

        /// <summary>
        /// Item_ID로 ItemData를 찾는다. 목록에 없으면 null.
        /// 합성 ID(강화 개체)면 ForgeInstanceItemDataProvider를 통해 런타임 ItemData를 반환한다.
        /// </summary>
        public ItemData FindItemData(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            if (ForgeInstanceRegistry.IsCompositeId(itemId))
            {
                var provider = ForgeInstanceItemDataProvider.Instance;
                if (provider != null)
                {
                    return provider.ResolveRuntimeItemData(itemId);
                }

                Debug.LogWarning($"[ItemCatalogManager] 강화 개체 ID이지만 ForgeInstanceItemDataProvider를 찾을 수 없습니다: {itemId}");
                return null;
            }

            return itemDictionary.TryGetValue(itemId, out var data) ? data : null;
        }

        /// <summary>Recipe_Item_ID로 RecipeData를 찾는다. 목록에 없으면 null.</summary>
        public HDY.Recipe.RecipeData FindRecipeData(string recipeItemId)
        {
            if (string.IsNullOrEmpty(recipeItemId)) return null;

            return recipeDictionary.TryGetValue(recipeItemId, out var recipe) ? recipe : null;
        }

        /// <summary>
        /// Item_ID로 ShopItemData를 찾는다. 목록에 없으면 null.
        /// Awake 시 한 번만 만들어 캐싱한 인스턴스를 그대로 반환한다(ShopStockManager의
        /// Dictionary&lt;ShopItemData,int&gt; 키가 같은 객체를 유지해야 하기 때문).
        /// </summary>
        public HDY.Shop.ShopItemData FindShopItemData(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            return shopItemDictionary.TryGetValue(itemId, out var shopItem) ? shopItem : null;
        }

        /// <summary>Result_Item_ID로 CookRecipeData를 찾는다. 목록에 없으면 null.</summary>
        public HDY.Cook.CookRecipeData FindCookRecipeData(string resultItemId)
        {
            if (string.IsNullOrEmpty(resultItemId)) return null;

            return cookRecipeDictionary.TryGetValue(resultItemId, out var cookRecipe) ? cookRecipe : null;
        }

        /// <summary>Recipe_ID로 연금술 교환 레시피(AlchemyExchangeRecipe)를 찾는다. 목록에 없으면 null.</summary>
        public HDY.Shop.AlchemyExchangeRecipe FindAlchemyExchangeRecipe(string recipeId)
        {
            return alchemyExchangeTable != null ? alchemyExchangeTable.FindRecipe(recipeId) : null;
        }

        /// <summary>
        /// 다른 스크립트가 들고 있는 ItemCatalogManager 참조가 비어있을 때 공용으로 쓰는 폴백 탐색.
        /// 1) 이미 참조가 있으면 그대로 반환, 2) 없으면 싱글톤(Instance), 3) 그래도 없으면 씬 전체에서 검색.
        /// (RecipeUnlockManager, GoddessStatueUI 등 여러 곳에서 동일한 폴백 로직을 반복하지 않기 위한 헬퍼)
        /// </summary>
        public static ItemCatalogManager Resolve(ItemCatalogManager existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<ItemCatalogManager>();
            if (found == null)
            {
                Debug.LogWarning("[ItemCatalogManager] 씬에서 ItemCatalogManager를 찾을 수 없습니다.");
            }

            return found;
        }
    }
}
