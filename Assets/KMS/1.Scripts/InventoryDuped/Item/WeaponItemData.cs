using HDY.Item;
using UnityEngine;

namespace KMS.Combat
{

    /// <summary>
    /// [멤] 원거리 스킬 시스템용 무기 데이터. 예전에는 이 SO를 무기마다 하나씩 직접 만들어서 Inspector에 수동으로 등록해야 했지만, 지금은 ItemCatalogManager가 ItemCatalog.csv(기본 데이터) + WeaponCatalog.csv(무기 전용 데이터, WeaponStatsTable이 파싱)를 조합해 런타임에 이 타입의 인스턴스를 직접 만들어 채운다(ParseItemRow/ApplyWeaponStats 참고) - 더 이상 이 타입의 .asset 파일을 손으로 만들 필요가 없다. ProjectilePrefab도 csv의 ProjectileId를 ProjectilePrefabTable에서 조회해 자동으로 채워진다.
    ///
    /// [멤] 기본 공격과 스킬의 구분: 여기 있는 필드들(ProjectileDamage 등)은 좌클릭 "기본 공격"에만 쓰인다 - 기본 공격은 무기마다 투사체 종류/데미지가 다르게 적용되는 "무기의 능력"이기 때문. 반면 스킬(장전 큐 또는 5등급 R키로 발동)은 "어떤 무기를 들고 있어도 항상 동일한 효과"여야 하므로 SkillData 자신이 갖는 별도의 ProjectileId/Speed/Lifetime을 쓰고, 이 무기 필드들을 빌려쓰지 않는다(PlayerWeaponSkillController 참고).
    /// </summary>
    
[CreateAssetMenu(fileName = "Item_Weapon", menuName = "KMS/Combat/Weapon Item Data")]
    public class WeaponItemData : ItemData
    {

        [Header("전투 타입 (캐릭터 스탯 시스템)")]
        [Tooltip("이 무기의 데미지 타입 - 공격력(힘/민첩) 또는 마력(지능/행운) 중 어느 스탯 조합을 사용할지 결정한다. 스킬도 자신의 DamageType을 가지며, 장착 무기 타입과 다르면 스킬 사용이 제한된다(PlayerWeaponSkillController 참고).")]
        public WeaponDamageType DamageType = WeaponDamageType.Physical;

        [Header("무기 고유 스킬 (스킬화된 기본공격/이동기)")]
        [Tooltip("이 무기의 고유 기본공격(좌클릭) 스킬 Skill_ID. SkillCatalog.csv의 Grade 0(무기 전용) 행을 가리킨다. 비어있으면 아래 Ranged Attack 필드(예전 방식)로 폴백한다. 무기가 강화되면 이 ID만 바꿔서 기본공격 자체를 통째로 교체할 수 있다.")]
        public string BasicAttackSkillId;

        [Tooltip("이 무기의 고유 이동기(Ctrl 돌진) 스킬 Skill_ID. CastType이 Dash인 스킬이어야 하며, 비어있으면 이 무기로는 돌진기를 쓸 수 없다.")]
        public string DashSkillId;

        [Header("Melee Attack")]

        [Min(0.1f)]
        public float AttackDistance = 3f;

        [Min(0f)]
        public float AttackCooldown = 0.5f;

        // [멤] 스킬 시스템(원거리 공격 + 장전/큐) 용 필드. 기존 Melee 필드는 그대로 두고(다른 무기 타입에서 쓸 수 있어 보존) 추가로 원거리 필드만 달았다.
        [Header("Ranged Attack (원거리 공격 - 스킬 시스템)")]

        [Tooltip("발사할 투사체 Prefab (KMS.InventoryDuped.ItemProjectile 컴포넌트가 붙어있어야 함)")]
        public GameObject ProjectilePrefab;

        [Min(0f)]
        public float ProjectileSpeed = 15f;

        [Min(0f)]
        [Tooltip("투사체가 아무것도 맞추지 못했을 때 자동 소멸되는 시간(초)")]
        public float ProjectileLifetime = 3f;

        [Min(0)]
        [Tooltip("원거리 기본공격(큐가 비어있을 때) 데미지. 큐에 있는 스킬을 발동할 때의 데미지는 그 SkillData.Damage를 따로 사용한다.")]
        public int ProjectileDamage = 1;

        [Min(0f)]
        [Tooltip("원거리 기본공격(큐가 비어있을 때)의 재사용 대기시간. 큐에 있는 스킬 발동은 그 SkillData.Cooldown을 따로 사용한다.")]
        public float ProjectileAttackCooldown = 0.5f;

    }

}
