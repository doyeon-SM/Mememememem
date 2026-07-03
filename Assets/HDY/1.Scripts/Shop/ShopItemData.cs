using System.Collections.Generic;
using UnityEngine;

namespace Mem.Shop
{
    /// <summary>
    /// 상점에서 취급하는 아이템 정보 SO.
    /// HDY.Item.ItemData와는 Item_ID 문자열로 매칭한다 (딕셔너리 탐색 방식과 동일).
    /// </summary>
    [CreateAssetMenu(fileName = "Shop_", menuName = "HDY/Shop/Shop Item Data", order = 0)]
    public class ShopItemData : ScriptableObject
    {
        [Header("참조")]
        [Tooltip("HDY.Item.ItemData.Item_ID와 동일한 값으로 매칭됨")]
        public string Item_ID;

        [Header("판매 가격 (플레이어 -> 상점, 골드만)")]
        public int Selling_Price;

        [Header("구매 가격 (상점 -> 플레이어, 골드 또는 재료)")]
        public int Purchase_Price_Golds;
        public List<MaterialCost> Purchase_Price_Materials = new List<MaterialCost>();

        [Header("재고")]
        public int MaxAmount;

    }
}
