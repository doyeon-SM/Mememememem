using KMS.Harvesting;
using UnityEngine;

namespace KMS.Combat
{
    /// <summary>
    /// [멤] 스킬 시스템의 원거리 공격이 실제로 데미지를 줄 수 있는 최소 몬스터 스텁.
    /// "몬스터만 때리고 멤은 안 때린다"는 요구사항을 만족하려면 Mem과 확실히 구별되는 대상이
    /// 필요해서 만들었다 - AI/스폰/보상/드롭 등은 이번 단계 범위에 포함하지 않고, 투사체가 데미지를
    /// 주고받을 대상이 실제로 존재한다는 것만 보장한다. 추후 몬스터 AI/스폰 작업에서 이 컴포넌트를
    /// 그대로 갖다 쓰거나(같은 GameObject에 AI 스크립트를 추가) OnDied 이벤트를 구독해서
    /// 보상/드롭/스폰 카운트 처리를 얹으면 된다.
    ///
    /// [레이어] 이 컴포넌트가 붙은 GameObject(정확히는 투사체와 충돌하는 콜라이더)는 반드시
    /// "Monster" 레이어(인덱스 9)에 있어야 한다 - ItemProjectile이 데미지 판정 시 damageLayer로
    /// 이 레이어만 걸러서 맞추기 때문이다. Mem(레이어 10)/Player(레이어 3) 등 다른 레이어에 있으면
    /// 투사체가 아예 데미지 판정을 하지 않는다(=맞지 않는다).
    /// </summary>
    [DisallowMultipleComponent]
    public class Monster : MonoBehaviour, IDamageable
    {
        [Header("체력")]
        [SerializeField, Min(1)] private int maxHealth = 10;

        [SerializeField] private int currentHealth;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public bool IsDead { get; private set; }

        /// <summary>사망 시 발행된다 - 추후 몬스터 AI/스폰 작업이 보상/드롭/스폰 카운트 갱신 등에 구독해서 쓰면 된다.</summary>
        public event System.Action OnDied;

        // [멤] 데미지 숫자 팝업 표시용 정적 이벤트 - Mem 쪽의 MemEvents.OnMemDamaged와 동일한 목적이지만,
        // Monster는 Mem과 무관한 별도 클래스라 그 이벤트 버스(MemSystem.Events.MemEvents, 타 담당자 소유)에
        // 직접 얹지 않고 KMS 쪽에서 독립적으로 관리한다. KMSMemDamagePopupService가 이 이벤트를 구독해서
        // 동일한 데미지 숫자 팝업을 몬스터 피격 위치에도 띄운다.
        public static event System.Action<Monster, int> OnMonsterDamaged;

        private void Awake()
        {
            currentHealth = maxHealth;
            IsDead = false;

            // [멤] "Monster" 레이어가 없거나 이 오브젝트에 실수로 다른 레이어가 설정돼 있으면
            // 원거리 공격 판정이 전혀 동작하지 않으므로, 에디터에서 바로 알아챌 수 있게 경고한다.
            int monsterLayer = LayerMask.NameToLayer("Monster");
            if (monsterLayer == -1)
            {
                Debug.LogWarning("[Monster] \"Monster\" 레이어가 프로젝트에 없습니다. Edit > Project Settings > Tags and Layers에서 추가해야 원거리 공격이 맞습니다.", this);
            }
            else if (gameObject.layer != monsterLayer)
            {
                Debug.LogWarning($"[Monster] {name}의 레이어가 \"Monster\"가 아닙니다(현재: {LayerMask.LayerToName(gameObject.layer)}). 원거리 공격 판정에서 제외될 수 있습니다.", this);
            }
        }

        public void TakeDamage(int damage)
        {
            if (IsDead || damage <= 0) return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            Debug.Log($"[Monster] hit monster damage = {damage} / CurrentHealth = {currentHealth}");

            // [멤] 데미지 숫자 팝업(KMSMemDamagePopupService)이 이 이벤트를 구독해서 표시한다.
            OnMonsterDamaged?.Invoke(this, damage);

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// [멤] 최소 스텁이라 사망 연출/보상/드롭 없이 오브젝트만 비활성화한다.
        /// 실제 몬스터 AI/스폰 작업에서 OnDied를 구독하거나 이 메서드를 필요에 맞게 확장하면 된다.
        /// </summary>
        protected virtual void Die()
        {
            Debug.Log($"[Monster] Die monster");
            IsDead = true;
            OnDied?.Invoke();
            gameObject.SetActive(false);
        }
    }
}
