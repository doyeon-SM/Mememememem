namespace HDY.Inventory
{
    /// <summary>
    /// 창고 정렬 기준. Mem 시스템의 MemSortCriteria와 달리 아이템에는 티어/생산스탯 개념이 없어서
    /// Item_ID / 카테고리 기본 2가지 + 카테고리 우선순위 3가지로 구성했다.
    ///
    /// [HDY 요청 - 카테고리 우선순위 정렬] ToolPriority/MaterialPriority/FoodPriority는 특정 카테고리
    /// 몇 개를 앞으로 배치하고 나머지는 원래 카테고리(enum 선언) 순서를 그대로 따른다:
    /// - ToolPriority(도구우선): 도구 -> 캡슐 -> 설계도 -> 이후 카테고리순(음식, 재료, 굿즈)
    /// - MaterialPriority(재료우선): 굿즈 -> 재료 -> 이후 카테고리순(음식, 캡슐, 도구, 설계도)
    /// - FoodPriority(음식우선): 음식 -> 이후 카테고리순(재료, 굿즈, 캡슐, 도구, 설계도)
    /// </summary>
    public enum ItemSortCriteria
    {
        ItemId,
        Category,
        ToolPriority,
        MaterialPriority,
        FoodPriority
    }
}
