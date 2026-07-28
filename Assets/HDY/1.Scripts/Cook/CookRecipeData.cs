using System.Collections.Generic;
using UnityEngine;

namespace HDY.Cook
{
    /// <summary>
    /// 요리 하나의 레시피 정의 SO.
    ///
    /// 제작 레시피(HDY.Recipe.RecipeData)와 같은 시트 파싱 패턴을 쓰지만, 요리 재료는 기획상 항상
    /// 1개씩만 소비되므로 Recipe_Requset_Item_Data(Item_ID+Amount) 대신 List&lt;string&gt;으로
    /// 단순화했다. 요리 결과물도 항상 1개라 별도의 수량 필드가 없다(RecipeData와 동일한 원칙).
    ///
    /// ItemCatalogManager가 Awake 시 cookRecipeCatalogSheet(탭 구분 텍스트)를 파싱해 행마다
    /// ScriptableObject.CreateInstance&lt;CookRecipeData&gt;()로 런타임 인스턴스를 만들어 채운다.
    /// Result_Item_ID를 키로 딕셔너리에 로드되며, FindCookRecipeData(id)로 조회한다.
    /// </summary>
    [CreateAssetMenu(fileName = "CookRecipe_", menuName = "HDY/Cook/Cook Recipe Data", order = 0)]
    public class CookRecipeData : ScriptableObject
    {
        [Header("완성품 ID")]
        public string Result_Item_ID;

        [Header("재료 ID (재료는 항상 1개씩)")]
        public List<string> Ingredient_Item_IDs = new List<string>();

        [Header("조리 소요 시간")]
        public float Time;
    }
}
