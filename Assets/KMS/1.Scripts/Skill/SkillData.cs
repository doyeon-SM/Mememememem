using UnityEngine;

namespace KMS.Combat
{
    /// <summary>
    /// [멤] 스킬의 발동 형태. 즉발형(Instant) = 사용 즉시 효과 발동, 스택형(Stack) = 사용할수록
    /// 효과가 누적, 버프(Buff) = 일정 시간 동안 지속되는 효과 부여. 스킬 정보 UI 표시와
    /// 보유 스킬 정렬(형태순) 기준으로 쓰인다.
    /// </summary>
    public enum SkillFormType
    {
        Instant = 0,
        Stack = 1,
        Buff = 2,
    }

    /// <summary>
    /// [멤] 스킬이 실제로 무엇을 하는지(발동 방식)를 구분한다. Projectile = 앞으로 투사체를 발사하는
    /// 공격 스킬, Dash = 캐릭터 자신이 이동하는 이동기(돌진기). 같은 SkillData 구조를 공격 스킬과
    /// 이동기가 함께 쓰기 위한 분기 키이며, PlayerWeaponSkillController가 이 값으로 발동 경로를 나눈다.
    /// FormType(즉발형/스택형/버프)은 "효과가 어떻게 적용되는가"라는 별개의 축이라 서로 무관하다.
    /// </summary>
    public enum SkillCastType
    {
        Projectile = 0,
        Dash = 1,
    }

    /// <summary>
    /// [멤] 스킬 하나의 정의 SO. SkillCatalogManager가 csv 시트(Skill_ID 기준)를 파싱해 런타임
    /// 인스턴스를 만들어 채운다 - ItemCatalogManager가 ItemData/CookRecipeData를 만드는 것과 동일한
    /// 패턴이다. 스킬은 인벤토리 아이템이 아니라 "보유 스킬 ID 목록"(SkillUnlockManager)으로만
    /// 관리되므로 ItemData를 상속하지 않는 완전히 별도의 데이터 타입이다.
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_", menuName = "KMS/Combat/Skill Data", order = 0)]
    public class SkillData : ScriptableObject
    {
        [Header("식별")]
        public string Skill_ID;
        public string SkillName;
        public Sprite SkillIcon;

        [Header("전투")]
        [Tooltip("스킬 데미지 배율(%) - 장착 무기 기반 기본공격 최종데미지에 이 %를 곱해 스킬 히트 1회당 데미지를 계산한다(PlayerCombatStats.ComputeSkillHitDamage 참고). 예전에는 무기와 무관한 고정값(Damage)이었으나, 무기 기반 %로 설계가 변경되었다.")]
        public float DamagePercent;

        [Tooltip("다단히트 횟수 - 스킬 1회 사용 시 투사체를 이 횟수만큼 발사하며, 각 히트는 독립적으로 크리티컬 판정을 받는다.")]
        public int HitCount = 1;

        [Tooltip("이 스킬의 데미지 타입(물리/마법) - 장착한 무기의 DamageType과 다르면 스킬 사용이 제한된다.")]
        public WeaponDamageType DamageType;

        [Tooltip("스킬 사용 직후 시작되는 재사용 대기시간(초)")]
        public float Cooldown;

        [Header("형태")]
        [Tooltip("스킬의 발동 형태 - 즉발형/스택형/버프. 스킬 정보 UI 표시 및 보유 스킬 정렬 기준으로 쓰인다.")]
        public SkillFormType FormType;

        [Header("등급")]
        [Tooltip("1~4등급 = 장전 단계(1~4단계) 칸과 1:1로 대응한다(PlayerSkillLoadout 참고). " +
                 "5등급 이상은 이번 장전 큐 시스템과 무관한 별도 체계(패시브/특수 스킬 등)로 예정되어 있어, " +
                 "이 필드에 5 이상 값이 들어있어도 로드아웃 4칸에는 등록되지 않는다.")]
        public int Grade;

        [Header("발동 이펙트 (무기와 무관하게 스킬 자신이 정의)")]
        [Tooltip("ProjectilePrefabTable(무기/스킬 공용 조회 테이블)에서 찾을 투사체 ID. csv에는 Prefab 참조를 담을 수 없어 문자열 ID만 갖고, SkillCatalogManager가 ProjectilePrefabTable로 실제 Prefab을 조회해 ProjectilePrefab 필드를 채운다.")]
        public string ProjectileId;

        [Tooltip("이 스킬 투사체의 속도 - 무기 속도가 아니라 이 값을 사용한다.")]
        public float ProjectileSpeed;

        [Tooltip("이 스킬 투사체의 최대 생존 시간(초) - 무기 값이 아니라 이 값을 사용한다.")]
        public float ProjectileLifetime;

        [System.NonSerialized] public GameObject ProjectilePrefab;

        [Header("발동 방식")]
        [Tooltip("이 스킬이 투사체를 발사하는 공격 스킬(Projectile)인지, 캐릭터가 이동하는 이동기(Dash)인지 구분한다. csv의 CastType 컬럼이 비어있으면 Projectile로 취급한다.")]
        public SkillCastType CastType = SkillCastType.Projectile;

        [Tooltip("[Dash 전용] 돌진 거리(m). 벽/오브젝트에 막히면 그 지점에서 멈추므로 실제 이동 거리는 이보다 짧을 수 있다.")]
        public float DashDistance;

        [Tooltip("[Dash 전용] 돌진에 걸리는 시간(초). 돌진 속도 = DashDistance / DashDuration 이다.")]
        public float DashDuration;


        [Header("설명")]
        [TextArea]
        public string Description;
    }
}
