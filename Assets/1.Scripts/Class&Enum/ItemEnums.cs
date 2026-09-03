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
        Weapon,
        // [멤] 스킬북 / 궁극의 스킬북 카테고리. 두 종류 모두 기능은 동일(우클릭 사용 시 고정된 Skill_ID의 스킬을 획득)하고
        // 아이템 카테고리 이름(툴팁 표기)만 다르다. 기존 값 순서 유지를 위해 끝에 추가.
        SkillBook,
        UltimateSkillBook,
        // [멤] 장비 시스템. 방어구(머리/갑옷/다리/신발)와 장신구(귀걸이/반지/목걸이/벨트/팔찌/머리핀)의 대분류이며,
        // 구체적인 부위는 이 enum이 아니라 EquipmentCatalog.csv의 EquipSlot 컬럼(KMS.Equipment.EquipSlotType)이 정한다.
        // 방어구는 강화/연마가 가능하고 장신구는 전승 합성이 가능하다는 차이 때문에 두 카테고리로 나눴다.
        Armor,
        Accessory
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
