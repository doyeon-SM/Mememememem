using HDY.Item;
using UnityEngine;

namespace KMS
{
    [DisallowMultipleComponent]
    public class PlayerHungerHealthRecovery : MonoBehaviour
    {
        [SerializeField] private PlayerStats stats;
        [SerializeField] private float recoveryDelayAfterDamage = 1f;
        [SerializeField] private float recoveryInterval = 1f;
        [SerializeField] private float healthPerTick = 5f;
        [SerializeField] private float hungerCostPerTick = 5f;
        [SerializeField] private float minimumHungerReserve = 10f;
        [SerializeField] private float recoveryDelayAfterEating = 2f;
        [SerializeField] private bool logRecovery;

        private float recoveryDelayTimer;
        private float recoveryTickTimer;

        private void Reset()
        {
            stats = GetComponent<PlayerStats>();
        }

        private void Awake()
        {
            if (stats == null)
            {
                stats = GetComponent<PlayerStats>();
            }
        }

        private void OnEnable()
        {
            if (stats != null)
            {
                stats.Damaged += HandleDamaged;
                stats.FoodApplied += HandleFoodApplied;
            }
        }

        private void OnDisable()
        {
            if (stats != null)
            {
                stats.Damaged -= HandleDamaged;
                stats.FoodApplied -= HandleFoodApplied;
            }
        }

        private void Update()
        {
            if (stats == null || !stats.IsAlive) return;
            if (stats.CurrentHealth >= stats.MaxHealth) return;

            if (recoveryDelayTimer > 0f)
            {
                recoveryDelayTimer -= Time.deltaTime;
                return;
            }

            recoveryTickTimer -= Time.deltaTime;
            if (recoveryTickTimer > 0f) return;

            float requiredHunger = hungerCostPerTick + minimumHungerReserve;
            if (!stats.HasHunger(requiredHunger)) return;

            if (!stats.TryConsumeHungerExact(hungerCostPerTick)) return;

            float previousHealth = stats.CurrentHealth;
            stats.Heal(healthPerTick);
            recoveryTickTimer = Mathf.Max(0.01f, recoveryInterval);

            if (logRecovery && stats.CurrentHealth > previousHealth)
            {
                Debug.Log(
                    $"[HealthRecovery] Consumed {hungerCostPerTick} hunger and healed {stats.CurrentHealth - previousHealth}.",
                    stats);
            }
        }

        private void HandleDamaged(float amount)
        {
            recoveryDelayTimer = Mathf.Max(0f, recoveryDelayAfterDamage);
            recoveryTickTimer = 0f;
        }

        private void HandleFoodApplied(ItemData item, float restoredAmount)
        {
            if (restoredAmount <= 0f) return;

            recoveryDelayTimer = Mathf.Max(
                recoveryDelayTimer,
                Mathf.Max(0f, recoveryDelayAfterEating));
        }

        private void OnValidate()
        {
            recoveryDelayAfterDamage = Mathf.Max(0f, recoveryDelayAfterDamage);
            recoveryInterval = Mathf.Max(0.01f, recoveryInterval);
            healthPerTick = Mathf.Max(0f, healthPerTick);
            hungerCostPerTick = Mathf.Max(0f, hungerCostPerTick);
            minimumHungerReserve = Mathf.Max(0f, minimumHungerReserve);
            recoveryDelayAfterEating = Mathf.Max(0f, recoveryDelayAfterEating);
        }
    }
}
