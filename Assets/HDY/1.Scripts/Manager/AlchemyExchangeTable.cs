using System.Collections.Generic;
using System.Globalization;

namespace HDY.Shop
{
    /// <summary>
    /// AlchemyExchangeCatalog.csv를 파싱해 Recipe_ID -> AlchemyExchangeRecipe로 관리하는 테이블.
    /// FoodEffectTable/RefinementOptionCsvParser와 동일한 구조를 따른다.
    /// 컬럼 순서: Recipe_ID, Category, Cost_Item_ID, Cost_Amount, Result_Item_ID, Result_Amount.
    /// Category 컬럼 값은 AlchemyExchangeCategory 이름(Loot 또는 Enhance) 그대로 적는다.
    /// </summary>
    public class AlchemyExchangeTable
    {
        private readonly Dictionary<string, AlchemyExchangeRecipe> recipesById
            = new Dictionary<string, AlchemyExchangeRecipe>();
        private readonly List<AlchemyExchangeRecipe> recipeList = new List<AlchemyExchangeRecipe>();

        public IReadOnlyList<AlchemyExchangeRecipe> RecipeList => recipeList;

        public AlchemyExchangeTable(string csvText)
        {
            if (string.IsNullOrEmpty(csvText)) return;

            var lines = csvText.Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0번째 줄은 헤더라 건너뜀
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(',');
                if (cols.Length < 6) continue;

                var recipe = ParseRow(cols);
                if (recipe == null || string.IsNullOrEmpty(recipe.Recipe_ID)) continue;

                if (!recipesById.ContainsKey(recipe.Recipe_ID))
                {
                    recipesById.Add(recipe.Recipe_ID, recipe);
                    recipeList.Add(recipe);
                }
            }
        }

        /// <summary>Recipe_ID로 교환 레시피를 찾는다. 목록에 없으면 null.</summary>
        public AlchemyExchangeRecipe FindRecipe(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return null;
            return recipesById.TryGetValue(recipeId, out var recipe) ? recipe : null;
        }

        private static AlchemyExchangeRecipe ParseRow(string[] cols)
        {
            var recipe = new AlchemyExchangeRecipe
            {
                Recipe_ID = cols[0].Trim(),
                Category = System.Enum.TryParse(cols[1].Trim(), out AlchemyExchangeCategory category)
                    ? category
                    : AlchemyExchangeCategory.Loot,
                Cost_Item_ID = cols[2].Trim(),
                Cost_Amount = ParseInt(cols[3]),
                Result_Item_ID = cols[4].Trim(),
                Result_Amount = ParseInt(cols[5]),
            };

            return recipe;
        }

        private static int ParseInt(string s)
        {
            return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }
    }
}
