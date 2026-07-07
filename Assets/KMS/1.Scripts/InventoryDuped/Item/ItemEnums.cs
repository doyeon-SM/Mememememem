namespace KMS.InventoryDuped
{
public enum ItemCategory
{
    Material,
    Food,
    Drink,
    Tool,
    Weapon,
    Misc,
    Seed
}

// 애니메이션에서 들고 있는 구조를 구분하기 위함.
public enum ItemHoldType
{
    None,
    Hand,
    Spear,
    Hoe
}

// 아이템을 사용했을때 발동하는 액션의 종류를 구분하기 위함.
public enum ItemUseAction
{
    None,
    SpearAttack,
    HoeUse,
    Eat,
    Drink,
    Throw,
    WaterCanUse,
    PlantSeed,
    Butcher
}

// 아이템 효과 종류를 나타냄
public enum ItemEffectType
{
    Hp,
    Hunger,
    Thirst,
    Mental,
    Damage,
    Stamina
}

}
