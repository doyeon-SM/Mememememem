using System;
using HDY.Item;
using UnityEngine;
using KMS.Persistence;
using MemSystem.Core;
using MemSystem.Events;

namespace KMS
{
    public class PlayerStats : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float startingHealth = 100f;

        [Header("Hunger")]
        [SerializeField] private float maxHunger = 100f;
        [SerializeField] private float startingHunger = 100f;
        [SerializeField] private float starvationDamagePerSecond = 5f;
        [SerializeField] private KMSFoodEffectController foodEffects;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public float MaxHunger => maxHunger;
        public float CurrentHunger { get; private set; }
        public KMSFoodEffectController FoodEffects => foodEffects;
        public bool IsAlive { get; private set; } = true;
        public bool IsInvulnerable { get; private set; }

        public event Action<float, float> HealthChanged;
        public event Action<float, float> HungerChanged;
        public event Action<float> Damaged;
        public event Action<float> Healed;
        public event Action Died;
        public event Action Revived;

        private void Awake()
        {
            CurrentHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
            CurrentHunger = Mathf.Clamp(startingHunger, 0f, maxHunger);
            IsAlive = CurrentHealth > 0f;

            ResolveFoodEffects();
            foodEffects.InitializeAsNormal(CurrentHunger, false);
        }

        private void Start()
        {
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            HungerChanged?.Invoke(CurrentHunger, maxHunger);
        }

        private void OnEnable()
        {
            MemEvents.OnMemAttackPlayer += HandleMemAttack;
        }

        private void OnDisable()
        {
            MemEvents.OnMemAttackPlayer -= HandleMemAttack;
        }

        private void Update()
        {
            ApplyStarvationDamage();
        }

        private void HandleMemAttack(Mem _, int damage)
        {
            TakeDamage(damage);
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive || IsInvulnerable || amount <= 0f) return;

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
            if (CurrentHunger <= 0f) return false;

            float previous = CurrentHunger;
            CurrentHunger = Mathf.Max(0f, CurrentHunger - amount);
            foodEffects?.ConsumeSatiety(previous - CurrentHunger);
            HungerChanged?.Invoke(CurrentHunger, maxHunger);

            return true;
        }

        public bool HasHunger(float amount)
        {
            return CurrentHunger >= amount;
        }

        public float RestoreHunger(float amount)
        {
            if (amount <= 0f) return 0f;

            float previous = CurrentHunger;
            CurrentHunger = Mathf.Min(maxHunger, CurrentHunger + amount);
            float restored = CurrentHunger - previous;
            foodEffects?.RegisterNormalRestoration(restored);
            HungerChanged?.Invoke(CurrentHunger, maxHunger);
            return restored;
        }

        public bool CanApplyFood(ItemData item, float satietyAmount)
        {
            ResolveFoodEffects();
            return foodEffects.CanApplyFood(
                item,
                satietyAmount,
                maxHunger,
                CurrentHunger);
        }

        public bool ApplyFood(ItemData item, float satietyAmount)
        {
            ResolveFoodEffects();
            if (!foodEffects.ApplyFood(
                    item,
                    satietyAmount,
                    maxHunger,
                    CurrentHunger,
                    out float resultingHunger))
            {
                return false;
            }

            CurrentHunger = resultingHunger;
            HungerChanged?.Invoke(CurrentHunger, maxHunger);
            return true;
        }

        public void Revive(float healthPercent = 1f)
        {
            healthPercent = Mathf.Clamp01(healthPercent);

            IsAlive = true;
            CurrentHealth = maxHealth * healthPercent;
            CurrentHunger = maxHunger;
            foodEffects?.InitializeAsNormal(CurrentHunger);

            Revived?.Invoke();
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            HungerChanged?.Invoke(CurrentHunger, maxHunger);
        }

        public void SetInvulnerable(bool invulnerable)
        {
            IsInvulnerable = invulnerable;
        }

        public void Kill()
        {
            TakeDamage(CurrentHealth);
        }

        public PlayerStatsSaveData CaptureSaveData()
        {
            return new PlayerStatsSaveData
            {
                currentHealth = CurrentHealth,
                currentHunger = CurrentHunger,
                foodEffects = foodEffects != null ? foodEffects.CaptureSaveData() : null
            };
        }

        public void RestoreSaveData(PlayerStatsSaveData data)
        {
            if (data == null) return;

            bool wasAlive = IsAlive;
            CurrentHealth = Mathf.Clamp(data.currentHealth, 0f, maxHealth);
            CurrentHunger = Mathf.Clamp(data.currentHunger, 0f, maxHunger);
            IsAlive = CurrentHealth > 0f;
            ResolveFoodEffects();
            foodEffects.RestoreSaveData(data.foodEffects, CurrentHunger);

            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            HungerChanged?.Invoke(CurrentHunger, maxHunger);

            if (wasAlive && !IsAlive) Died?.Invoke();
            else if (!wasAlive && IsAlive) Revived?.Invoke();
        }

        private void ApplyStarvationDamage()
        {
            if (!IsAlive) return;
            if (CurrentHunger > 0f) return;

            TakeDamage(starvationDamagePerSecond * Time.deltaTime);
        }

        private void ResolveFoodEffects()
        {
            if (foodEffects == null) foodEffects = GetComponent<KMSFoodEffectController>();
            if (foodEffects == null) foodEffects = gameObject.AddComponent<KMSFoodEffectController>();
        }
    }
}
