using UnityEngine;
using KMS.Harvesting;

namespace KMS.InventoryDuped
{

public class ItemProjectile : MonoBehaviour
{
    private int damage;
    private LayerMask damageLayer;
    private Transform owner;
    private bool hasHit;
    // [멤] 생성 직후(총구/스킬북 근처 지형이나 자기 자신 콜라이더와의 겹침 등으로) 바로 충돌 판정이 나서 즉시 소멸하는 문제를 막기 위한 짧은 유예시간이다.
    private float spawnTime;
    [SerializeField] private float spawnGracePeriod = 0.05f;
    // [멤] 풀링 지원 - null이면 기존처럼 Destroy(gameObject)로 소멸하고, 값이 있으면(기본 공격 풀링 경로) 이 콜백으로 되돌려보낸다. 스킬 발사 경로는 이 값을 넘기지 않아 기존과 동일하게 동작한다.
    private System.Action<GameObject> releaseToPool;
    private Coroutine lifetimeRoutine;


    // [멤] 스킬 시스템(원거리 공격) 연동을 위해 lifetime 파라미터를 추가했다 - 아무것도 맞추지 못해도 이 시간이 지나면 자동 소멸된다(WeaponItemData.ProjectileLifetime을 그대로 넘겨받을 예정). 기존 호출부가 없어서 시그니처를 바꿔도 안전하다.
public void Initialize(int damage, LayerMask damageLayer, Transform owner, float lifetime = 5f, System.Action<GameObject> releaseToPool = null)
    {
        this.damage = damage;
        this.damageLayer = damageLayer;
        this.owner = owner;
        this.releaseToPool = releaseToPool;
        hasHit = false;
        spawnTime = Time.time;

        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = null;
        }

        if (lifetime > 0f)
        {
            lifetimeRoutine = StartCoroutine(AutoDespawnAfter(lifetime));
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        TryHit(collision.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

private void TryHit(Collider hitCollider)
    {
        if (hasHit || hitCollider == null) return;
        if (Time.time - spawnTime < spawnGracePeriod)
        {
            // [멤] 진단 로그: 생성 직후 유예시간 때문에 이 충돌을 무시했다는 것을 알 수 있게 남긴다
            // ("충돌은 됐는데 데미지가 안 들어간다"는 문제를 조사할 때, 유예시간 때문인지 다른 이유인지
            // 바로 구분할 수 있도록 함).
            Debug.Log($"[ItemProjectile] {name} - '{hitCollider.name}'(레이어: {LayerMask.LayerToName(hitCollider.gameObject.layer)})과 충돌했지만 생성 직후 유예시간({spawnGracePeriod}초) 중이라 무시함.");
            return;
        }
        if (owner != null && hitCollider.transform.IsChildOf(owner)) return;

        // [멤] 진단 로그: 어떤 콜라이더와 부딪혔는지(이름/레이어) 항상 남긴다 - "투사체는 충돌해서
        // 사라지는데 데미지가 안 들어간다"는 문제의 원인이 (a) 몬스터가 아닌 다른 콜라이더(지형/장식물
        // 등)에 먼저 맞아서인지, (b) 몬스터 레이어이긴 한데 IDamageable을 못 찾아서인지, (c) 데미지
        // 적용 자체가 막혀서인지를 로그만 보고 바로 구분할 수 있게 한다.
        Debug.Log($"[ItemProjectile] {name} - '{hitCollider.name}'(레이어: {LayerMask.LayerToName(hitCollider.gameObject.layer)})과 충돌함. damageLayer 마스크={damageLayer.value}");

        if ((damageLayer.value & (1 << hitCollider.gameObject.layer)) == 0)
        {
            Debug.Log($"[ItemProjectile] {name} - '{hitCollider.name}'은 데미지 대상 레이어가 아니라서(damageLayer에 포함 안 됨) 데미지 없이 소멸함.");
            Despawn();
            return;
        }

        IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();

        if (damageable == null)
        {
            Debug.Log($"[ItemProjectile] {name} - '{hitCollider.name}'은 대상 레이어이지만 IDamageable 컴포넌트를 찾지 못함(부모 계층 포함) - 데미지 적용 안 됨.");
        }
        else if (damage <= 0)
        {
            Debug.Log($"[ItemProjectile] {name} - 데미지 값이 {damage}(0 이하)라서 '{hitCollider.name}'에 데미지 적용 안 됨.");
        }
        else if (damageable.IsDead)
        {
            Debug.Log($"[ItemProjectile] {name} - '{hitCollider.name}'은 이미 사망 상태라 데미지 적용 안 됨.");
        }
        else
        {
            Debug.Log($"[ItemProjectile] {name} - '{hitCollider.name}'에 데미지 {damage} 적용.");
            damageable.TakeDamage(damage);
        }

        hasHit = true;
        Despawn();
    }

private System.Collections.IEnumerator AutoDespawnAfter(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        lifetimeRoutine = null;
        Despawn();
    }

    // [멤] 풀링 지원을 위해 Destroy 직접 호출을 이 메서드로 통일했다 - releaseToPool이 있으면 풀에 돌려보내고, 없으면(스킬 발사 등) 기존처럼 Destroy한다.
    private void Despawn()
    {
        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = null;
        }

        if (releaseToPool != null)
        {
            var callback = releaseToPool;
            releaseToPool = null;
            callback(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

}

}
