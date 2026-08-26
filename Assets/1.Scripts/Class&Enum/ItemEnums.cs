namespace HDY.Item
{
    /// <summary>
    /// 아이템 대분류
    /// </summary>
    public enum ItemCategory
    {
        Food,
        Material,
        Goods,
        Capsule,
        Tool,
        BluePrint,
        // [멤] 스킬 시스템용 원거리 무기 카테고리. 기존 값들과 순서가 바뀌지 않게 끝에 추가해서 csv 시트의 Category 컴럼(이름 기준 파싱)에는 영향이 없다.
        Weapon
    }

    /// <summary>
    /// 아이템 사용 방식
    /// </summary>
    public enum UseAction
    {
        Default,
        Eat,
        Use
    }

    /// <summary>
    /// 섭취(Eat) 시 적용되는 효과 종류. 추후 계속 추가될 예정.
    /// </summary>
    public enum EffectType
    {
        Satiety,
        Speed,
        Fulling,
        Heal,
        Luck
    }
}
