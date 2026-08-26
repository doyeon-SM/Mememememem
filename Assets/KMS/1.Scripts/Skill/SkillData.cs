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
        [Tooltip("스킬 기본 데미지")]
        public int Damage;

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


        [Header("설명")]
        [TextArea]
        public string Description;
    }
}
