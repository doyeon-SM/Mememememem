using System;
using HDY.Item;
using UnityEngine;
using KMS.Persistence;
using MemSystem.Core;
using MemSystem.Events;

namespace KMS
{
    public enum PlayerDamageType
    {
        Generic,
        MemAttack,
        Fall,
        Starvation,
        Water
    }

    public class PlayerStats : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private float startingHealth = 100f;

        [Header("Hunger")]
        [SerializeField] private float maxHunger = 100f;
        [SerializeField] private float startingHunger = 100f;
        [SerializeField] private float starvationDamagePerSecond = 5f;
        [SerializeField] private KMSFoodEffectController foodEffects;
        [SerializeField] private PlayerHealthRegenController healthRegen;
        [Tooltip("[멤] 방어력(받는 데미지 감소) 적용을 위한 선택적 참조. 없으면(구 프리팹 등) 방어력 없이 기존과 동일하게 동작한다.")]
        [SerializeField] private PlayerCombatStats combatStats;

        [Header("Combat")]
        [Tooltip("[멤] 기본 공격(원거리 투사체) 데미지에 더해지는 캐릭터 스펙 보너스. 무기마다 다른 WeaponItemData.ProjectileDamage(무기 자체 데미지)에 이 값을 더해 최종 데미지를 계산한다(PlayerWeaponSkillController.FireBasicAttack 참고). 기본값 0이라 지금은 무기 자체 데미지만 적용되며(기존 동작과 동일), 캐릭터 성장/장비 시스템이 생기면 이 값을 올리면 된다. 스킬 데미지(SkillData.Damage)는 이 보너스의 영향을 받지 않는다 - 스킬은 어떤 무기를 쓰든 동일한 효과라는 요구사항 때문.")]
        [SerializeField, Min(0)] private int attackPower = 0;

        public int AttackPower => attackPower;


        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public float MaxHunger => maxHunger;
        public float CurrentHunger { get; private set; }
        public KMSFoodEffectController FoodEffects => foodEffects;
        public bool IsAlive { get; private set; } = true;
        public bool IsInvulnerable { get; private set; }
        public PlayerDamageType LastDamageType { get; private set; } = PlayerDamageType.Generic;

        public event Action<float, float> HealthChanged;
        public event Action<float, float> HungerChanged;
        public event Action<ItemData, float> FoodApplied;
        public event Action<float> Damaged;
        public event Action<float, PlayerDamageType> DamageReceived;
        public event Action<float> Healed;
        public event Action Died;
        public event Action Revived;
        public event Action<PlayerStats> PlayerStatsDestroyed;

        private bool healthInitialized;

        private void Awake()
        {
            CurrentHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
            CurrentHunger = Mathf.Clamp(startingHunger, 0f, maxHunger);
            IsAlive = CurrentHealth > 0f;
            healthInitialized = true;

            ResolveFoodEffects();
            foodEffects.InitializeAsNormal(CurrentHunger, false);
            ResolveHealthRegen();
            if (combatStats == null) combatStats = GetComponent<PlayerCombatStats>();
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

        private void OnDestroy()
        {
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            HungerChanged?.Invoke(CurrentHunger, maxHunger);
            PlayerStatsDestroyed?.Invoke(this);
        }

        private void Update()
        {
            ApplyStarvationDamage();
        }

        private void HandleMemAttack(Mem _, int damage)
        {
            TakeDamage(damage, PlayerDamageType.MemAttack);
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, PlayerDamageType.Generic);
        }

        public void TakeDamage(float amount, PlayerDamageType damageType)
        {
            ApplyDamage(amount, damageType, false);
        }

        private void ApplyDamage(float amount, PlayerDamageType damageType, bool ignoreInvulnerability)
        {
            if (!IsAlive || (!ignoreInvulnerability && IsInvulnerable) || amount <= 0f) return;

            // [멤] 방어력 적용 - 전투성 피해(Generic/MemAttack)에만 감산한다(PlayerCombatStats.ApplyDefenseReduction 참고).
            if (combatStats != null)
            {
                amount = combatStats.ApplyDefenseReduction(amount, damageType);
                if (amount <= 0f) return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            LastDamageType = damageType;
            Damaged?.Invoke(amount);
            DamageReceived?.Invoke(amount, damageType);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);

            if (CurrentHealth <= 0f)
            {
                IsAlive = false;
                healthRegen?.ResetChannels();
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

        /// <summary>
        /// Updates the runtime maximum health. When increasing the maximum, the
        /// amount of missing health is preserved so a level-up is not a full heal.
        /// </summary>
        public void SetMaxHealth(float value, bool preserveMissingHealth = true)
        {
            value = Mathf.Max(1f, value);
            if (Mathf.Approximately(maxHealth, value)) return;

            float previousMaxHealth = maxHealth;
            float previousHealth = CurrentHealth;
            maxHealth = value;

            // The territory-health controller can run before PlayerStats.Awake so
            // save restoration sees the correct maximum health from the beginning.
            if (!healthInitialized)
            {
                if (startingHealth >= previousMaxHealth - Mathf.Epsilon)
                {
                    startingHealth = maxHealth;
                }
                else
                {
                    startingHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
                }

                return;
            }

            if (!IsAlive)
            {
                CurrentHealth = 0f;
            }
            else if (preserveMissingHealth && maxHealth > previousMaxHealth)
            {
                float missingHealth = Mathf.Max(0f, previousMaxHealth - previousHealth);
                CurrentHealth = Mathf.Clamp(maxHealth - missingHealth, 0f, maxHealth);
            }
            else
            {
                CurrentHealth = Mathf.Clamp(previousHealth, 0f, maxHealth);
            }

            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        /// <summary>
        /// [멤] SetMaxHealth와 동일한 패턴 - 의지 스탯 기반 배고픔최대치 재계산(PlayerCombatStats)에서 호출된다.
        /// 최대치가 늘어나면(preserveMissingHunger) 부족분(이미 소모된 양)을 유지한 채 새 최대치에 맞춰
        /// CurrentHunger를 늘려준다 - 레벨업/포인트 투자로 최대 배고픔이 늘었다고 즉시 배가 가득 차버리지 않게 한다.
        /// </summary>
        public void SetMaxHunger(float value, bool preserveMissingHunger = true)
        {
            value = Mathf.Max(1f, value);
            if (Mathf.Approximately(maxHunger, value)) return;

            float previousMaxHunger = maxHunger;
            float previousHunger = CurrentHunger;
            maxHunger = value;

            // PlayerCombatStats가 PlayerStats.Awake보다 먼저 실행될 수 있어(실행 순서 미보장), SetMaxHealth와
            // 동일하게 아직 초기화 전이면 startingHunger만 맞춰두고 실제 CurrentHunger 계산은 Awake에 맡긴다.
            if (!healthInitialized)
            {
                if (startingHunger >= previousMaxHunger - Mathf.Epsilon)
                {
                    startingHunger = maxHunger;
                }
                else
                {
                    startingHunger = Mathf.Clamp(startingHunger, 0f, maxHunger);
                }

                return;
            }

            if (preserveMissingHunger && maxHunger > previousMaxHunger)
            {
                float missingHunger = Mathf.Max(0f, previousMaxHunger - previousHunger);
                CurrentHunger = Mathf.Clamp(maxHunger - missingHunger, 0f, maxHunger);
            }
            else
            {
                CurrentHunger = Mathf.Clamp(previousHunger, 0f, maxHunger);
            }

            HungerChanged?.Invoke(CurrentHunger, maxHunger);
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

        /// <summary>
        /// Consumes hunger only when the full requested amount is available.
        /// Use this for transactional costs that must not partially drain hunger.
        /// </summary>
        public bool TryConsumeHungerExact(float amount)
        {
            if (amount <= 0f) return true;
            if (!HasHunger(amount)) return false;

            return ConsumeHunger(amount);
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
            float previousHunger = CurrentHunger;
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
            float restoredAmount = CurrentHunger - previousHunger;
            FoodApplied?.Invoke(item, restoredAmount);
            HungerChanged?.Invoke(CurrentHunger, maxHunger);

            ApplyHealEffect(item);

            return true;
        }

        /// <summary>
        /// 음식의 EatEffects 중 Heal 효과를 합산해 기존 Heal(float)로 즉시 회복시킨다.
        /// 만복도처럼 큐에 쌓이는 지속효과가 아니라, 먹는 즉시 1회성으로 적용된다.
        /// </summary>
        private void ApplyHealEffect(ItemData item)
        {
            if (item?.EatEffects == null) return;

            // [체력회복 개편] Heal은 더 이상 즉시 고정수치로 회복하지 않는다.
            // 최대체력 대비 %로 해석해 PlayerHealthRegenController의 음식 큐에 넣고,
            // 그곳에서 일정 시간 동안 서서히 회복된다.
            float healPercent = 0f;
            for (int i = 0; i < item.EatEffects.Count; i++)
            {
                ItemEffect effect = item.EatEffects[i];
                if (effect != null && effect.Effect == EffectType.Heal)
                {
                    healPercent += effect.Value;
                }
            }

            if (healPercent > 0f)
            {
                ResolveHealthRegen();
                healthRegen.EnqueueFoodHeal(healPercent);
            }
        }

        public void Revive(float healthPercent = 1f)
        {
            healthPercent = Mathf.Clamp01(healthPercent);

            IsAlive = true;
            CurrentHealth = maxHealth * healthPercent;
            CurrentHunger = maxHunger;
            foodEffects?.InitializeAsNormal(CurrentHunger);
            healthRegen?.ResetChannels();

            Revived?.Invoke();
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            HungerChanged?.Invoke(CurrentHunger, maxHunger);
        }

        public void SetInvulnerable(bool invulnerable)
        {
            IsInvulnerable = invulnerable;
        }

        /// <summary>
        /// Immediately reduces health to zero. Environmental kill volumes can opt
        /// out of temporary respawn invulnerability when the hazard must be lethal.
        /// </summary>
        public void Kill(
            PlayerDamageType damageType = PlayerDamageType.Generic,
            bool ignoreInvulnerability = false)
        {
            ApplyDamage(CurrentHealth, damageType, ignoreInvulnerability);
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

            TakeDamage(
                starvationDamagePerSecond * Time.deltaTime,
                PlayerDamageType.Starvation);
        }

        private void ResolveFoodEffects()
        {
            if (foodEffects == null) foodEffects = GetComponent<KMSFoodEffectController>();
            if (foodEffects == null) foodEffects = gameObject.AddComponent<KMSFoodEffectController>();
        }

        private void ResolveHealthRegen()
        {
            if (healthRegen == null) healthRegen = GetComponent<PlayerHealthRegenController>();
            if (healthRegen == null) healthRegen = gameObject.AddComponent<PlayerHealthRegenController>();
        }
    }
}
