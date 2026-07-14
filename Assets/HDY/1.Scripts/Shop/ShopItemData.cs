using UnityEngine;
using UnityEngine.Serialization;

namespace HDY.Shop
{
    /// <summary>
    /// 상점에서 취급하는 아이템 정보 SO.
    /// HDY.Item.ItemData와는 Item_ID 문자열로 매칭한다 (딕셔너리 탐색 방식과 동일).
    ///
    /// [구매 가격 = 골드 또는 재료 "하나" 중 하나] 기획 확정 사항: 재료로 구매하는 아이템은 골드를
    /// 같이 요구하지 않고, 재료도 여러 종류가 아니라 딱 하나만 소비한다.
    /// - Purchase_Price_Material이 비어있으면(= Item_ID가 비어있거나 Amount가 0이면) 골드로만 구매.
    /// - Purchase_Price_Material이 채워져 있으면 재료로만 구매하고, Purchase_Price_Golds 값은 무시한다.
    ///
    /// [구매 재고 vs 판매 재고 - 별도 관리] 상점이 "플레이어에게 팔 수 있는 양"(구매 탭에서 소모)과
    /// "플레이어에게서 사들일 수 있는 양"(판매 탭에서 소모)은 서로 다른 재고라서 필드를 분리했다.
    /// 예를 들어 상점이 사과를 50개 갖고 있어도(Purchase_MaxAmount), 플레이어에게서 사과를 살 수 있는
    /// 한도(Selling_MaxAmount)는 그것과 무관하게 별도로 정한다. 둘 다 ShopStockManager가 재입고 주기마다
    /// 각자의 최대치로 리셋한다.
    /// </summary>
    [CreateAssetMenu(fileName = "Shop_", menuName = "HDY/Shop/Shop Item Data", order = 0)]
    public class ShopItemData : ScriptableObject
    {
        [Header("참조")]
        [Tooltip("HDY.Item.ItemData.Item_ID와 동일한 값으로 매칭됨")]
        public string Item_ID;

        [Header("판매 가격 (플레이어 -> 상점, 골드만)")]
        public int Selling_Price;

        [Header("구매 가격 (상점 -> 플레이어, 골드 또는 재료 중 하나)")]
        [Tooltip("재료로 구매하지 않는 아이템이면 여기에 골드 가격을 넣는다. Purchase_Price_Material이 채워져 있으면 이 값은 무시된다.")]
        public int Purchase_Price_Golds;
        [Tooltip("재료로 구매하는 아이템이면 여기에 재료 1개를 넣는다(Item_ID + 필요 수량). 비워두면 골드 구매로 취급.")]
        public MaterialCost Purchase_Price_Material;

        [Header("재고 (구매/판매를 서로 다른 재고로 관리)")]
        [FormerlySerializedAs("MaxAmount")]
        [Tooltip("구매 가능 재고: 상점이 플레이어에게 팔 수 있는 최대 수량(재입고 시 이 값으로 리셋).")]
        public int Purchase_MaxAmount;
        [Tooltip("판매 가능 재고: 상점이 플레이어에게서 사들일 수 있는 최대 수량(재입고 시 이 값으로 리셋).")]
        public int Selling_MaxAmount;
    }
}
