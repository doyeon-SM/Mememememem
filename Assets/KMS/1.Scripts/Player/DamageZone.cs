using System.Collections.Generic;
using UnityEngine;

namespace KMS
{
    [DisallowMultipleComponent]
    public class DamageZone : MonoBehaviour
    {
        [SerializeField] private float damageAmount = 50f;
        [SerializeField] private bool damageOncePerEntry = true;
        [SerializeField] private bool logDamage;

        private readonly HashSet<PlayerStats> damagedPlayers = new HashSet<PlayerStats>();

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerStats stats = other.GetComponentInParent<PlayerStats>();
            if (stats == null) return;
            if (damageOncePerEntry && damagedPlayers.Contains(stats)) return;

            stats.TakeDamage(damageAmount);

            if (damageOncePerEntry)
            {
                damagedPlayers.Add(stats);
            }

            if (logDamage)
            {
                Debug.Log($"[DamageZone] Applied {damageAmount} damage to {stats.name}.", stats);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerStats stats = other.GetComponentInParent<PlayerStats>();
            if (stats != null)
            {
                damagedPlayers.Remove(stats);
            }
        }

        private void OnValidate()
        {
            damageAmount = Mathf.Max(0f, damageAmount);
        }
    }
}
