using UnityEngine;

namespace KMS
{
    /// <summary>
    /// Immediately kills a player that enters this trigger volume.
    /// Intended to be placed just below the visible surface of lethal water.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class KMSWaterDeathVolume : MonoBehaviour
    {
        [SerializeField] private bool ignoreInvulnerability = true;
        [SerializeField] private bool logPlayerDeath;

        private void Reset()
        {
            ConfigureCollider();
        }

        private void Awake()
        {
            ConfigureCollider();
        }

        private void OnValidate()
        {
            ConfigureCollider();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryKillPlayer(other);
        }

        private void OnTriggerStay(Collider other)
        {
            // Covers players already overlapping when the volume becomes active.
            TryKillPlayer(other);
        }

        private void TryKillPlayer(Collider other)
        {
            if (other == null) return;

            PlayerStats stats = other.GetComponentInParent<PlayerStats>();
            if (stats == null || !stats.IsAlive) return;

            if (logPlayerDeath)
            {
                Debug.Log($"[KMSWaterDeathVolume] '{stats.name}' entered lethal water.", this);
            }

            stats.Kill(PlayerDamageType.Water, ignoreInvulnerability);
        }

        private void ConfigureCollider()
        {
            Collider trigger = GetComponent<Collider>();
            if (trigger != null) trigger.isTrigger = true;
        }
    }
}
