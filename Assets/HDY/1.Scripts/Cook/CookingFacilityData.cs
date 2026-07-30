using System.Collections.Generic;
using UnityEngine;

namespace HDY.Cook
{
    /// <summary>
    /// 요리시설(모닥불조리/주방조리 등) 1개를 나타내는 SO. 어떤 요리 레시피(CookRecipeData)를
    /// 취급하는지만 Result_Item_ID 목록으로 갖는다.
    ///
    /// [HDY.Shop.ShopData와 동일한 패턴] 상점 SO처럼 시트에서 파싱하지 않고, 이 SO 에셋 자체에
    /// RecipeIds를 직접 채운다(에디터 Inspector에서 수동 편집). ShopData와 달리 시설 이름/재입고
    /// 주기 같은 필드는 없다 - 요리시설은 판매/재고 개념이 없고 "이 시설에서 어떤 요리를 할 수
    /// 있는가"만 필요하기 때문이다.
    ///
    /// 실제 CookRecipeData 인스턴스는 ItemCatalogManager가 cookRecipeCatalogSheet에서 파싱해
    /// 갖고 있고, 이 SO의 RecipeIds는 ItemCatalogManager.FindCookRecipeData(id)로 resolve해서
    /// 사용하는 것을 전제로 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "CookingFacility_", menuName = "HDY/Cook/Cooking Facility Data", order = 0)]
    public class CookingFacilityData : ScriptableObject
    {
        [Header("취급 레시피 (Result_Item_ID 목록)")]
        [Tooltip("ItemCatalogManager.FindCookRecipeData(id)로 조회되는 Result_Item_ID 목록. 이 시설에서 조리 가능한 요리들이다.")]
        public List<string> RecipeIds = new List<string>();
    }
}
