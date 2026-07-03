using UnityEngine;

public class ItemProjectile : MonoBehaviour
{
    private int damage;
    private LayerMask damageLayer;
    private Transform owner;
    private bool hasHit;

    public void Initialize(int damage, LayerMask damageLayer, Transform owner)
    {
        this.damage = damage;
        this.damageLayer = damageLayer;
        this.owner = owner;
        hasHit = false;
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
            Destroy(gameObject);
            return;
        }

/*        IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();

        if (damageable != null && damage > 0)
        {
            damageable.TakeDamage(damage);
        }*/

        hasHit = true;
        Destroy(gameObject);
    }
}
