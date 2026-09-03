using HDY.Item;
using UnityEngine;

namespace KMS.Equipment
{
    /// <summary>
    /// [멤] 방어구/장신구 아이템 데이터. WeaponItemData와 완전히 같은 방식으로, ItemCatalogManager가
    /// ItemCatalog.csv(모든 아이템 공통 기본 데이터) + EquipmentCatalog.csv(장비 전용 데이터,
    /// EquipmentStatsTable이 파싱)를 조합해 런타임에 이 타입의 인스턴스를 만들어 채운다 -
    /// 이 타입의 .asset 파일을 손으로 만들 필요는 없다.
    ///
    /// [멤] 방어구와 장신구가 쓰는 필드가 다르다:
    /// - 방어구(Category=Armor): DamageType으로 주/부 스탯 종류가 자동 결정되고, HealthBonus +
    ///   PrimaryStatValue + SecondaryStatValue를 쓴다. 강화/연마가 가능하다(로직은 추후 제작).
    /// - 장신구(Category=Accessory): BaseOptionStatType + BaseOptionValue(기본옵션)만 쓴다.
    ///   강화/연마는 불가능하고, 대신 개체별 특수옵션(EquipmentInstanceData.SpecialOptions)과
    ///   전승 합성이 있다.
    /// </summary>
    public class EquipmentItemData : ItemData
    {
        [Header("장비 부위")]
        [Tooltip("이 장비가 들어갈 수 있는 칸. 귀걸이/반지는 장착창에 2칸씩 있고, 그 외에는 1칸씩이다.")]
        public EquipSlotType EquipSlot;

        [Header("방어구 전용 (Category = Armor)")]
        [Tooltip("주/부 스탯 종류를 결정한다. Physical = 힘(주)/민첩(부), Magic = 지능(주)/행운(부).")]
        public WeaponDamageType DamageType = WeaponDamageType.Physical;

        [Min(0)]
        [Tooltip("최대 체력 가산치(고정 수치). 장비로 늘어난 최대 체력은 현재 체력을 회복시키지 않는다.")]
        public int HealthBonus;

        [Min(0)]
        [Tooltip("주 스탯 가산치. DamageType이 Physical이면 힘, Magic이면 지능에 더해진다.")]
        public int PrimaryStatValue;

        [Min(0)]
        [Tooltip("부 스탯 가산치. DamageType이 Physical이면 민첩, Magic이면 행운에 더해진다.")]
        public int SecondaryStatValue;

        [Header("장신구 전용 (Category = Accessory)")]
        [Tooltip("기본옵션이 올려주는 스탯 종류. 아이템 종류가 결정하므로 개체가 달라도 항상 같다.")]
        public CharacterStatType BaseOptionStatType;

        [Min(0)]
        [Tooltip("기본옵션 수치. 전승을 해도 이 값은 베이스 장신구 것이 그대로 유지된다.")]
        public int BaseOptionValue;

        /// <summary>[멤] 방어구의 주 스탯 종류(DamageType으로 자동 결정).</summary>
        public CharacterStatType PrimaryStatType =>
            DamageType == WeaponDamageType.Physical ? CharacterStatType.Strength : CharacterStatType.Intelligence;

        /// <summary>[멤] 방어구의 부 스탯 종류(DamageType으로 자동 결정).</summary>
        public CharacterStatType SecondaryStatType =>
            DamageType == WeaponDamageType.Physical ? CharacterStatType.Agility : CharacterStatType.Luck;

        /// <summary>이 장비가 방어구(강화/연마 대상)인지 여부. 부위로 판정한다.</summary>
        public bool IsArmor => EquipmentSlotLayout.IsArmorType(EquipSlot);
    }
}
