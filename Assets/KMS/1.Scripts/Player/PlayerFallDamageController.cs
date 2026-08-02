using System;
using UnityEngine;

namespace KMS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerFallDamageController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private PlayerStats stats;

        [Header("Fall Damage")]
        [Tooltip("No damage is applied at or below this landing speed (m/s).")]
        [SerializeField, Min(0f)] private float safeImpactSpeed = 11f;
        [Tooltip("Damage applied for each m/s above the safe landing speed.")]
        [SerializeField, Min(0f)] private float damagePerExcessSpeed = 8f;
        [Tooltip("Maximum damage that one landing can apply.")]
        [SerializeField, Min(0f)] private float maximumDamage = 100f;

        public float SafeImpactSpeed => safeImpactSpeed;
        public float DamagePerExcessSpeed => damagePerExcessSpeed;
        public float MaximumDamage => maximumDamage;

        public event Action<float, float> FallDamageApplied;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (movement != null)
            {
                movement.Landed += HandleLanded;
            }
        }

        private void OnDisable()
        {
            if (movement != null)
            {
                movement.Landed -= HandleLanded;
            }
        }

        private void OnValidate()
        {
            safeImpactSpeed = Mathf.Max(0f, safeImpactSpeed);
            damagePerExcessSpeed = Mathf.Max(0f, damagePerExcessSpeed);
            maximumDamage = Mathf.Max(0f, maximumDamage);
            ResolveReferences();
        }

        public float CalculateDamage(float impactSpeed)
        {
            float excessSpeed = Mathf.Max(0f, impactSpeed - safeImpactSpeed);
            return Mathf.Min(maximumDamage, excessSpeed * damagePerExcessSpeed);
        }

        private void HandleLanded(float impactSpeed)
        {
            if (stats == null || !stats.IsAlive) return;

            float damage = CalculateDamage(impactSpeed);
            if (damage <= 0f) return;

            float previousHealth = stats.CurrentHealth;
            stats.TakeDamage(damage);
            float appliedDamage = previousHealth - stats.CurrentHealth;

            if (appliedDamage > 0f)
            {
                FallDamageApplied?.Invoke(impactSpeed, appliedDamage);
            }
        }

        private void ResolveReferences()
        {
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (stats == null) stats = GetComponent<PlayerStats>();
        }
    }
}
