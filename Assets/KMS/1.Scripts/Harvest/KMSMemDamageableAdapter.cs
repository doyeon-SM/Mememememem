using KMS.Harvesting;
using MemSystem.Core;
using UnityEngine;

namespace KMS.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Mem))]
    public sealed class KMSMemDamageableAdapter : MonoBehaviour, IDamageable
    {
        private Mem mem;

        public bool IsDead =>
            mem == null ||
            !mem.IsActive ||
            mem.Stats == null ||
            mem.Stats.IsDead;

        private void Awake()
        {
            mem = GetComponent<Mem>();
        }

        public void TakeDamage(int damage)
        {
            if (mem == null || damage <= 0) return;

            mem.TakeDamage(damage);
        }
    }
}
