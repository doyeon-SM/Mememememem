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
    // [멤] 풀링 지원 - null이면 기존처럼 Destroy(gameObject)로 소멸하고, 값이 있으면(기본 공격 풀링 경로) 이 콜백으로 되돌려보낸다. 스킬 발사 경로는 이 값을 넘기지 않아 기존과 동일하게 동작한다.
    private System.Action<GameObject> releaseToPool;
    private Coroutine lifetimeRoutine;


    // [멤] 스킬 시스템(원거리 공격) 연동을 위해 lifetime 파라미터를 추가했다 - 아무것도 맞추지 못해도 이 시간이 지나면 자동 소멸된다(WeaponItemData.ProjectileLifetime을 그대로 넘겨받을 예정). 기존 호출부가 없어서 시그니처를 바꿔도 안전하다.
public void Initialize(int damage, LayerMask damageLayer, Transform owner, float lifetime = 3f, System.Action<GameObject> releaseToPool = null)
    {
        this.damage = damage;
        this.damageLayer = damageLayer;
        this.owner = owner;
        this.releaseToPool = releaseToPool;
        hasHit = false;

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
        if (owner != null && hitCollider.transform.IsChildOf(owner)) return;

        if ((damageLayer.value & (1 << hitCollider.gameObject.layer)) == 0)
        {
            Despawn();
            return;
        }

        IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();

        if (damageable != null && damage > 0 && !damageable.IsDead)
        {
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
