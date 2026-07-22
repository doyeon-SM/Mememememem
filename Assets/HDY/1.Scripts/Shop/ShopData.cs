using System.Collections.Generic;
using UnityEngine;

namespace HDY.Shop
{
    /// <summary>
    /// 상점 1개(마트/식당/철물점 등)를 나타내는 SO. 어떤 아이템(ShopItemData)들을 취급하는지와
    /// 재입고 주기를 갖는다.
    ///
    /// [현재 재고는 여기 없음] "지금 몇 개 남았는지" 같은 가변 상태는 이 SO가 아니라 런타임에
    /// ShopStockManager가 메모리로 들고 있는다 - SO는 여러 씬/여러 실행에서 공유되는 에셋이라
    /// 여기에 매 순간 바뀌는 재고 수치를 저장하면 에디터에서 값이 남는 등 문제가 생긴다.
    ///
    /// [HDY 요청 - 시트 마이그레이션] ShopItemData SO를 Items 리스트에 직접 드래그하던 방식에서,
    /// Item_ID 문자열 목록(ItemIds)만 갖도록 바꿨다. 실제 ShopItemData 인스턴스는
    /// ItemCatalogManager가 shopItemCatalogSheet에서 파싱해 갖고 있고, ShopStockManager가
    /// ItemIds를 그 카탈로그에서 resolve해서 사용한다.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopData_", menuName = "HDY/Shop/Shop Data", order = 0)]
    public class ShopData : ScriptableObject
    {
        [Header("상점 식별")]
        public string ShopName;

        [Header("취급 품목 (구매/판매 공통 목록, Item_ID)")]
        [Tooltip("ItemCatalogManager.FindShopItemData(id)로 조회되는 Item_ID 목록. 판매(재료 -> 골드)도 이 목록에 있는 아이템만 가능하다.")]
        public List<string> ItemIds = new List<string>();

        [Header("재입고 주기")]
        [Tooltip("이 상점의 모든 품목 재고가 이 주기(분)마다 한 번에 MaxAmount로 리셋된다. 상점마다 다르게 설정 가능.")]
        public int RestockIntervalMinutes = 20;
    }
}
