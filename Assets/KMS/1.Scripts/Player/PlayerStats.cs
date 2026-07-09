using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace KMS
{
    public class PlayerStats : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float startingHealth = 100f;

        [Header("Hunger")]
        [FormerlySerializedAs("maxStamina")]
        [SerializeField] private float maxHunger = 100f;
        [FormerlySerializedAs("startingStamina")]
        [SerializeField] private float startingHunger = 100f;
        [SerializeField] private float starvationDamagePerSecond = 5f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public float MaxHunger => maxHunger;
        public float CurrentHunger { get; private set; }
        public bool IsAlive { get; private set; } = true;

        public event Action<float, float> HealthChanged;
        public event Action<float, float> HungerChanged;
        public event Action<float> Damaged;
        public event Action<float> Healed;
        public event Action Died;
        public event Action Revived;

        public float MaxStamina => MaxHunger;
        public float CurrentStamina => CurrentHunger;
        public event Action<float, float> StaminaChanged
        {
            add => HungerChanged += value;
            remove => HungerChanged -= value;
        }

        private void Awake()
        {
            CurrentHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
            CurrentHunger = Mathf.Clamp(startingHunger, 0f, maxHunger);
            IsAlive = CurrentHealth > 0f;
        }

        private void Start()
        {
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            HungerChanged?.Invoke(CurrentHunger, maxHunger);
        }

        private void Update()
        {
            ApplyStarvationDamage();
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

        public bool ConsumeHunger(float amount)
        {
            if (amount <= 0f) return true;
            if (CurrentHunger < amount) return false;

            CurrentHunger -= amount;
            HungerChanged?.Invoke(CurrentHunger, maxHunger);

            return true;
        }

        public bool HasHunger(float amount)
        {
            return CurrentHunger >= amount;
        }

        public void RestoreHunger(float amount)
        {
            if (amount <= 0f) return;

            CurrentHunger = Mathf.Min(maxHunger, CurrentHunger + amount);
            HungerChanged?.Invoke(CurrentHunger, maxHunger);
        }

        public bool ConsumeStamina(float amount) => ConsumeHunger(amount);

        public bool HasStamina(float amount) => HasHunger(amount);

        public void RestoreStamina(float amount) => RestoreHunger(amount);

        public void Revive(float healthPercent = 1f)
        {
            healthPercent = Mathf.Clamp01(healthPercent);

            IsAlive = true;
            CurrentHealth = maxHealth * healthPercent;
            CurrentHunger = maxHunger;

            Revived?.Invoke();
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            HungerChanged?.Invoke(CurrentHunger, maxHunger);
        }

        public void Kill()
        {
            TakeDamage(CurrentHealth);
        }

        private void ApplyStarvationDamage()
        {
            if (!IsAlive) return;
            if (CurrentHunger > 0f) return;

            TakeDamage(starvationDamagePerSecond * Time.deltaTime);
        }
    }
}
