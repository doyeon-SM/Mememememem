using System;

namespace HDY.Shop
{
    /// <summary>
    /// 연금술사의 집 교환 탭(전리품/강화)에서 쓰는 단방향 아이템 교환 레시피 1개.
    /// 소비 재료는 항상 1종류(N개), 결과 아이템도 항상 1종류(M개)다 - 다대다라는 표현은 종류가
    /// 여러 개라는 뜻이 아니라 소비 수량과 획득 수량의 비율이 1:1이 아니어도 된다는 뜻이다
    /// (예: 여명의 강화석 1개 소비 -> 연마석 5개 획득).
    ///
    /// [단방향 전용] 기존 상점 아이템(ShopItemData)은 구매가격/판매가격으로 한 아이템을 양방향으로
    /// 거래할 수 있었지만, 교환 레시피는 반대 방향이 없다 - 결과 아이템을 다시 소비 재료로 되돌리려면
    /// 별도의 반대 레시피를 새로 등록해야 한다.
    /// </summary>
    [Serializable]
    public class AlchemyExchangeRecipe
    {
        public string Recipe_ID;
        public AlchemyExchangeCategory Category;
        public string Cost_Item_ID;
        public int Cost_Amount;
        public string Result_Item_ID;
        public int Result_Amount;
    }

    /// <summary>
    /// 연금술사의 집 안에서 교환 레시피가 속하는 탭 구분. 이 상점은 구매/판매 탭 대신 전리품/강화
    /// 탭을 쓰고, 두 탭 모두 동일한 AlchemyExchangeRecipe 매커니즘을 공유하되 이 값으로 필터링된다.
    /// </summary>
    public enum AlchemyExchangeCategory
    {
        Loot,
        Enhance,
    }
}
