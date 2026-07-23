namespace HDY.Forge
{
    /// <summary>
    /// 대장간에서 강화/승급이 가능한 도구 종류.
    /// 몽둥이(Club)는 강화/승급이 모두 불가능하므로 이 목록에 포함하지 않는다
    /// (ForgeToolTypeData 자산 자체를 만들지 않으면 자동으로 대장간 대상에서 제외됨).
    /// </summary>
    public enum ForgeToolType
    {
        Axe,
        Pickaxe,
        Hoe
    }

    /// <summary>대장간 시도 종류 - 강화(레벨업) 또는 승급(티어업, 아이템 자체 교체).</summary>
    public enum ForgeActionType
    {
        Enhance,
        Promotion
    }

    /// <summary>대장간 시도 한 번의 결과.</summary>
    public enum ForgeAttemptResult
    {
        Success,
        Failure
    }
}
