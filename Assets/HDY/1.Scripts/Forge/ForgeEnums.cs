namespace HDY.Forge
{
    /// <summary>
    /// 대장간 시스템이 인식하는 도구 종류.
    /// 대부분(도끼/곡괭이/괭이)은 강화/승급이 가능하지만, Club(몽둥이)은 강화·승급이 모두 불가능하고
    /// 고정 데미지(해당 티어 ItemData.Value)만 사용하는 특수 케이스다 - ForgeToolTypeData 자산에서
    /// CanEnhance=false, CanPromote=false로 등록해 표현한다(자산 자체를 안 만드는 게 아니라, 등록은
    /// 하되 두 액션만 막는 방식 - 그래야 ForgeManager가 "미등록 아이템"이 아니라 "등록됐지만 강화/승급
    /// 불가"로 정확히 구분한다).
    /// </summary>
    public enum ForgeToolType
    {
        Axe,
        Pickaxe,
        Hoe,
        Club
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
