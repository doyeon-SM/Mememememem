using System.Collections.Generic;
using UnityEngine;

namespace HDY.Recipe
{
    /// <summary>
    /// 제작품 하나의 레시피 정의 SO.
    ///
    /// [HDY 요청 - 시트 마이그레이션] ItemData와 동일하게, 개별 RecipeData SO를 Inspector에
    /// 하나씩 드래그하던 방식에서 시트(TextAsset, 탭 구분) 기반으로 전환했다. ItemCatalogManager가
    /// Awake 시 시트를 파싱해 각 행마다 ScriptableObject.CreateInstance&lt;RecipeData&gt;()로
    /// 런타임 인스턴스를 만들어 채운다.
    /// ItemCatalogManager가 Item_ID를 키로 딕셔너리에 로드해 Recipe_Item_ID로 탐색하는 것을 전제로 함.
    /// </summary>
    [CreateAssetMenu(fileName = "Recipe_Item_", menuName = "HDY/Item/Recipe Data", order = 0)]
    public class RecipeData : ScriptableObject
    {
        [Header("완성품 ID")]
        public string Recipe_Item_ID;

        [Header("재료 ID")]
        public List<Recipe_Requset_Item_Data> Requset_Items_ID = new List<Recipe_Requset_Item_Data>();

        [Header("제작 소요 시간")]
        public float time;
    }
}
