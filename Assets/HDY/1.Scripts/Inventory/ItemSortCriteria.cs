namespace HDY.Inventory
{
    /// <summary>
    /// 창고 정렬 기준. Mem 시스템의 MemSortCriteria와 달리 아이템에는 티어/생산스탯 개념이 없어서
    /// Item_ID / 카테고리 2가지로 단순하게 구성했다.
    /// </summary>
    public enum ItemSortCriteria
    {
        ItemId,
        Category
    }
}
