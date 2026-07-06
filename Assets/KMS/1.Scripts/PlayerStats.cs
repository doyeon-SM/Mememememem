using System;
using UnityEngine;

namespace KMS
{
    public class PlayerStats : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float startingHealth = 100f;

        [Header("Stamina")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float startingStamina = 100f;
        [SerializeField] private float staminaRegenPerSecond = 18f;
        [SerializeField] private float staminaRegenDelay = 0.6f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public float MaxStamina => maxStamina;
        public float CurrentStamina { get; private set; }
        public bool IsAlive { get; private set; } = true;

        public event Action<float, float> HealthChanged;
        public event Action<float, float> StaminaChanged;
        public event Action<float> Damaged;
        public event Action<float> Healed;
        public event Action Died;
        public event Action Revived;

        private float lastStaminaUseTime;

        private void Awake()
        {
            CurrentHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
            CurrentStamina = Mathf.Clamp(startingStamina, 0f, maxStamina);
            IsAlive = CurrentHealth > 0f;
        }

        private void Start()
        {
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            StaminaChanged?.Invoke(CurrentStamina, maxStamina);
        }

        private void Update()
        {
            RegenerateStamina();
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Damaged?.Invoke(amount);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);

            if (CurrentHealth <= 0f)
            {
                IsAlive = false;
                Died?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;

            float previous = CurrentHealth;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);

            float healedAmount = CurrentHealth - previous;
            if (healedAmount > 0f)
            {
                Healed?.Invoke(healedAmount);
                HealthChanged?.Invoke(CurrentHealth, maxHealth);
            }
        }

        public bool ConsumeStamina(float amount)
        {
            if (amount <= 0f) return true;
            if (CurrentStamina < amount) return false;

            CurrentStamina -= amount;
            lastStaminaUseTime = Time.time;
            StaminaChanged?.Invoke(CurrentStamina, maxStamina);

            return true;
        }

        public bool HasStamina(float amount)
        {
            return CurrentStamina >= amount;
        }

        public void RestoreStamina(float amount)
        {
            if (amount <= 0f) return;

            CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + amount);
            StaminaChanged?.Invoke(CurrentStamina, maxStamina);
        }

        public void Revive(float healthPercent = 1f)
        {
            healthPercent = Mathf.Clamp01(healthPercent);

            IsAlive = true;
            CurrentHealth = maxHealth * healthPercent;
            CurrentStamina = maxStamina;

            Revived?.Invoke();
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            StaminaChanged?.Invoke(CurrentStamina, maxStamina);
        }

        public void Kill()
        {
            TakeDamage(CurrentHealth);
        }

        private void RegenerateStamina()
        {
            if (!IsAlive) return;
            if (CurrentStamina >= maxStamina) return;
            if (Time.time < lastStaminaUseTime + staminaRegenDelay) return;

            CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + staminaRegenPerSecond * Time.deltaTime);
            StaminaChanged?.Invoke(CurrentStamina, maxStamina);
        }
    }
}
