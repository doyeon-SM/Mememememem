using System.Collections.Generic;
using UnityEngine;

namespace HDY.Recipe
{
    /// <summary>
    /// 개별 아이템의 제작법 정의 SO
    /// ItemCatalogManager가 Item_ID를 키로 딕셔너리에 로드하여 탐색하는 것을 전제로 함.
    /// </summary>
    [CreateAssetMenu(fileName ="Recipe_Item_", menuName ="HDY/Item/Recipe Data", order =0)]
    public class RecipeData : ScriptableObject
    {
        [Header("가공품 ID")]
        public string Recipe_Item_ID;

        [Header("재료 ID")]
        public List<Recipe_Requset_Item_Data> Requset_Items_ID = new List<Recipe_Requset_Item_Data>();

        [Header("제작 소요 시간")]
        public float time;
    }
}
