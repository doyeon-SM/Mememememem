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
        BluePrint
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
        Speed
    }
}
