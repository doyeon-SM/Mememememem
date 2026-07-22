using System.Collections.Generic;
using System.Globalization;
using HDY.Forge;
using KGH.Data;
using UnityEngine;

namespace HDY.Item
{
    /// <summary>
    /// 아이템 데이터(ItemData), 제작 레시피(RecipeData), 상점 품목(ShopItemData)을 보관하는 매니저.
    /// Item_ID / Recipe_Item_ID를 키로 하는 딕셔너리 탐색을 전제로 함.
    /// 씬에 배치되어 DontDestroyOnLoad로 유지되는 파괴불가 싱글톤 (ItemCatalogManager는 계속 싱글톤 유지).
    ///
    /// [HDY 요청 - 시트 마이그레이션] 개별 ItemData/RecipeData/ShopItemData SO를 Inspector에 하나씩
    /// 드래그하던 방식에서 시트(TextAsset, 탭 구분) 기반으로 전환했다. Awake 시 각 시트를 파싱해
    /// 행마다 ScriptableObject.CreateInstance&lt;T&gt;()로 런타임 인스턴스를 만들어 채운다.
    /// (강화 개체용 ForgeInstanceItemDataProvider가 이미 쓰던 것과 동일한 패턴.)
    /// 아이콘(Sprite)은 시트에 담을 수 없어 ItemIconTable로 따로 분리해 관리한다.
    ///
    /// [ShopItemData 참고] ShopStockManager가 재고를 Dictionary&lt;ShopItemData, int&gt;로(=객체 동일성
    /// 기준) 관리하기 때문에, FindShopItemData(id)는 매번 새 인스턴스를 만들지 않고 Awake 시 한 번만
    /// 만들어 캐싱한 같은 인스턴스를 계속 반환한다 - ItemData/RecipeData와 동일한 원칙.
    ///
    /// [대장간 연동] Item_ID가 "{BaseItemId}@{InstanceId}" 형태의 합성 ID(강화 개체)이면
    /// 일반 딕셔너리 탐색 대신 ForgeInstanceItemDataProvider에 위임해 강화 보너스가 반영된
    /// 런타임 전용 ItemData를 받아온다. 이 분기 덕분에 WorldObject/PlayerHarvestController 등
    /// 다른 팀 코드는 지금처럼 FindItemData(itemId) → Value만 읽어도 강화가 자동 반영된다.
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

            BuildDictionary();
            BuildRecipeDictionary();
            BuildShopItemDictionary();
        }

        [Header("아이템 데이터 시트 (탭 구분 텍스트, Item_ID 기준으로 파싱)")]
        [SerializeField] private TextAsset itemCatalogSheet;

        [Header("아이템 아이콘 테이블 (Item_ID -> Sprite)")]
        [SerializeField] private ItemIconTable iconTable;

        [Header("제작 레시피 시트 (탭 구분 텍스트, Recipe_Item_ID 기준으로 파싱)")]
        [SerializeField] private TextAsset recipeCatalogSheet;

        [Header("상점 품목 시트 (탭 구분 텍스트, Item_ID 기준으로 파싱)")]
        [SerializeField] private TextAsset shopItemCatalogSheet;

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

                var cols = line.Split('\t');
                if (cols.Length < 9)
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

        /// <summary>
        /// 레시피 시트를 파싱해 행마다 런타임 RecipeData 인스턴스를 만들고 Recipe_Item_ID 기준으로 딕셔너리에 채운다.
        /// Recipe_Item_ID가 중복되면 먼저 등록된 항목을 유지한다.
        /// </summary>
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

                var cols = line.Split('\t');
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

                var cols = line.Split('\t');
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

        /// <summary>시트 한 줄(컬럼 배열)을 런타임 ItemData로 변환한다.</summary>
        private ItemData ParseItemRow(string[] cols)
        {
            var data = ScriptableObject.CreateInstance<ItemData>();

            data.Item_ID = cols[0].Trim();
            data.ItemName = cols[1].Trim();
            data.Value = ParseInt(cols[2]);
            data.MaxStack = ParseInt(cols[3]);
            data.Category = ParseEnum<ItemCategory>(cols[4]);
            data.UseAction = ParseEnum<UseAction>(cols[5]);
            data.ObjectType = ParseEnum<ObjectType>(cols[6]);
            data.ItemClass = ParseEnum<CommonClass>(cols[7]);
            data.EatEffects = ParseEatEffects(cols[8]);
            data.ItemIcon = iconTable != null ? iconTable.GetIcon(data.Item_ID) : null;

            return data;
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
        /// 컬럼 순서: Item_ID, Selling_Price, Purchase_Price_Golds, Purchase_Material_ID,
        /// Purchase_Material_Amount, Purchase_MaxAmount, Selling_MaxAmount.
        /// Purchase_Material_ID가 비어있으면 골드 구매(Purchase_Price_Material = null)로 처리한다.
        /// </summary>
        private HDY.Shop.ShopItemData ParseShopItemRow(string[] cols)
        {
            var shopItem = ScriptableObject.CreateInstance<HDY.Shop.ShopItemData>();

            shopItem.Item_ID = cols[0].Trim();
            shopItem.Selling_Price = ParseInt(cols[1]);
            shopItem.Purchase_Price_Golds = ParseInt(cols[2]);

            var materialId = cols[3].Trim();
            shopItem.Purchase_Price_Material = string.IsNullOrEmpty(materialId)
                ? null
                : new HDY.Shop.MaterialCost { Item_ID = materialId, Amount = ParseInt(cols[4]) };

            shopItem.Purchase_MaxAmount = ParseInt(cols[5]);
            shopItem.Selling_MaxAmount = ParseInt(cols[6]);

            return shopItem;
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

        /// <summary>"Satiety:10;Speed:5" 형식을 파싱한다. 빈 문자열이면 빈 리스트를 반환한다.</summary>
        private static List<ItemEffect> ParseEatEffects(string raw)
        {
            var effects = new List<ItemEffect>();
            if (string.IsNullOrWhiteSpace(raw)) return effects;

            var entries = raw.Split(';');
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;

                var parts = entry.Split(':');
                if (parts.Length != 2) continue;

                if (System.Enum.TryParse(parts[0].Trim(), out EffectType effectType) &&
                    float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    effects.Add(new ItemEffect { Effect = effectType, Value = value });
                }
            }

            return effects;
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
