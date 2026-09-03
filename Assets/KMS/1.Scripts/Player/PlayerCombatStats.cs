using System;
using HDY.Territory;
using KMS.Persistence;
using UnityEngine;

namespace KMS
{
    /// <summary>[멤] 캐릭터의 5가지 변동 스탯 종류. 1포인트 = 해당 스탯 +1.</summary>
    public enum CharacterStatType
    {
        Strength = 0,
        Intelligence = 1,
        Agility = 2,
        Luck = 3,
        Willpower = 4,
    }

    /// <summary>
    /// [멤] 캐릭터 스탯(힘/지능/민첩/행운/의지) + 투자 포인트를 보관하고, claude/character-stat-system-plan.md에
    /// 확정된 공식(CharacterStatFormulas)에 따라 기존 시스템(체력/배고픔/이동속도/방어력/공격력·마력)에
    /// 반영하는 중심 컴포넌트.
    ///
    /// - 체력: 이 컴포넌트는 배율만 계산하고, 실제 SetMaxHealth 호출 시점(언제 다시 계산해야 하는지)은
    ///   기존 PlayerTerritoryHealthController가 그대로 담당한다(단일 소유권 유지) - 이 컴포넌트는
    ///   RefreshFromExternalStatChange()로 그쪽에 재계산을 요청만 한다.
    /// - 배고픔최대치/이동속도: 이 컴포넌트가 직접 PlayerStats.SetMaxHunger / PlayerMovement.SetStatSpeedMultiplier를
    ///   호출한다(둘 다 이번 작업에서 새로 추가된 API).
    /// - 레벨업 포인트 지급: TerritoryData.OnLevelChanged를 구독해서 레벨이 오른 만큼(보통 1레벨당 1회)
    ///   PointsPerLevel(5)씩 지급한다. 세이브 복원 시에는 지급하지 않도록 lastKnownTerritoryLevel을
    ///   먼저 복원값으로 맞춰둔다.
    /// - 재분배(리스펙): 골드를 소비하고 투자한 포인트를 전부 되돌린다. 정확한 골드 산식은 기획 미정이라
    ///   respecGoldCost는 임시 고정값이다(추후 교체 예정).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerCombatStats : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStats stats;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private KMSFoodEffectController foodEffects;
        [SerializeField] private PlayerTerritoryHealthController territoryHealthController;
        [SerializeField] private TerritoryData territoryData;

        [Header("Hunger Base")]
        [Tooltip("배고픔최대치 공식의 기준값(100 * (100+의지/10)%). 의지 보정 전 기준값.")]
        [SerializeField, Min(1f)] private float baseMaxHunger = 100f;

        [Header("Respec")]
        [Tooltip("재분배(리스펙) 1회 골드 비용. 구체 산식이 아직 기획 확정 전이라 임시 고정값 - 추후 교체 예정.")]
        [SerializeField, Min(0)] private int respecGoldCost = 100;

        [Header("Stats (1 point = +1, 상한 2000)")]
        [SerializeField, Min(0)] private int strength;
        [SerializeField, Min(0)] private int intelligence;
        [SerializeField, Min(0)] private int agility;
        [SerializeField, Min(0)] private int luck;
        [SerializeField, Min(0)] private int willpower;
        [SerializeField, Min(0)] private int unspentPoints;

        [Header("장비/버프 보너스 자리 (현재 0, 추후 장비 시스템에서 채움)")]
        [Tooltip("최종데미지 = 공격력or마력 × (1+데미지%) × (크리티컬 배율)의 \"데미지%\" 자리. AttackPower와 동일한 플레이스홀더 패턴.")]
        [SerializeField] private float bonusDamagePercent;
        [Tooltip("크리티컬 데미지 보너스(%). 기본 50%에 더해져 최대 200%로 클램프된다.")]
        [SerializeField] private float bonusCritDamagePercent;

        // [멤] 장비 시스템: 지금 장착 중인 방어구/장신구가 주는 보너스 합계(PlayerEquipment가 넣어준다).
        // 캐릭터 스탯은 "총합 = 투자 포인트 + 장비"로 계산되며(GetTotalStat), 투자/리스펙은 투자분만 다룬다.
        private KMS.Equipment.EquipmentBonusSnapshot equipmentBonus;

        private TerritoryData subscribedTerritoryData;
        private int lastKnownTerritoryLevel = 1;
        private bool hasInitializedLevel;

        public int Strength => strength;
        public int Intelligence => intelligence;
        public int Agility => agility;
        public int Luck => luck;
        public int Willpower => willpower;
        public int UnspentPoints => unspentPoints;
        public int RespecGoldCost => respecGoldCost;

        // [멤] 장비 시스템: 아래 5개는 "투자 + 장비"를 합친 총합 스탯이다. 모든 파생 공식은 이 값을 쓴다.
        public int TotalStrength => GetTotalStat(CharacterStatType.Strength);
        public int TotalIntelligence => GetTotalStat(CharacterStatType.Intelligence);
        public int TotalAgility => GetTotalStat(CharacterStatType.Agility);
        public int TotalLuck => GetTotalStat(CharacterStatType.Luck);
        public int TotalWillpower => GetTotalStat(CharacterStatType.Willpower);

        /// <summary>[PlayerTerritoryHealthController 전용] 장비가 더해주는 최대 체력 고정 가산치.</summary>
        public int EquipmentHealthBonus => equipmentBonus.Health;

        public float DefensePercent => CharacterStatFormulas.DefensePercent(TotalStrength, TotalWillpower);
        public float ResistancePercent => CharacterStatFormulas.ResistancePercent(TotalIntelligence, TotalWillpower);
        public float CritChancePercent => CharacterStatFormulas.CritChancePercent(TotalIntelligence);
        public float CritDamagePercent => CharacterStatFormulas.CritDamagePercent(bonusCritDamagePercent);
        public float LuckGatherAmountMultiplier => CharacterStatFormulas.GatherAmountMultiplier(TotalLuck);

        /// <summary>스탯이 새로 투자되거나 재분배/복원될 때마다 발행된다(UI 갱신용).</summary>
        public event Action StatsChanged;

        private void Reset()
        {
            stats = GetComponent<PlayerStats>();
            movement = GetComponent<PlayerMovement>();
            foodEffects = GetComponent<KMSFoodEffectController>();
            territoryHealthController = GetComponent<PlayerTerritoryHealthController>();
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyAllDerivedStats();
        }

        private void OnEnable()
        {
            RebindTerritoryData();
        }

        private void OnDisable()
        {
            UnsubscribeTerritoryData();
        }

        private void ResolveReferences()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (foodEffects == null) foodEffects = GetComponent<KMSFoodEffectController>();
            if (territoryHealthController == null) territoryHealthController = GetComponent<PlayerTerritoryHealthController>();
            territoryData = TerritoryData.Resolve(territoryData);
        }

        private void RebindTerritoryData()
        {
            ResolveReferences();

            if (subscribedTerritoryData != territoryData)
            {
                UnsubscribeTerritoryData();
                subscribedTerritoryData = territoryData;

                if (subscribedTerritoryData != null)
                {
                    subscribedTerritoryData.OnLevelChanged += HandleTerritoryLevelChanged;
                }
            }

            if (!hasInitializedLevel && territoryData != null)
            {
                // [멤] 세이브 복원 등으로 lastKnownTerritoryLevel이 이미 설정된 경우가 아니면 지금 영지
                // 레벨을 기준선으로 잡는다 - 그래야 이후 실제 레벨업(OnLevelChanged)이 일어날 때만
                // 포인트가 지급되고, 이미 지나온 레벨만큼 중복 지급되지 않는다.
                lastKnownTerritoryLevel = territoryData.Level;
                hasInitializedLevel = true;
            }
        }

        private void UnsubscribeTerritoryData()
        {
            if (subscribedTerritoryData != null)
            {
                subscribedTerritoryData.OnLevelChanged -= HandleTerritoryLevelChanged;
            }

            subscribedTerritoryData = null;
        }

        private void HandleTerritoryLevelChanged(int newLevel)
        {
            if (!hasInitializedLevel)
            {
                lastKnownTerritoryLevel = newLevel;
                hasInitializedLevel = true;
                return;
            }

            int delta = newLevel - lastKnownTerritoryLevel;
            if (delta > 0)
            {
                unspentPoints += delta * CharacterStatFormulas.PointsPerLevel;
                StatsChanged?.Invoke();
            }

            lastKnownTerritoryLevel = newLevel;

            // 체력 테이블(KMSTerritoryHealthTable)이 레벨에 따라 기본 체력을 바꾸므로, 레벨업 자체로도
            // 파생 스탯을 다시 계산해야 한다(PlayerTerritoryHealthController가 같은 이벤트를 구독해
            // SetMaxHealth를 호출하지만, 그 값에 곱해지는 힘 배율은 항상 최신 상태여야 한다).
            ApplyAllDerivedStats();
        }

        // ---- 파생 스탯 적용 ----

        private void ApplyAllDerivedStats(bool preserveMissingHealth = true)
        {
            ApplyHunger();
            ApplyMoveSpeed();
            ApplyHealth(preserveMissingHealth);
        }

        private void ApplyHunger()
        {
            if (stats == null) return;
            stats.SetMaxHunger(baseMaxHunger * CharacterStatFormulas.HungerMultiplier(TotalWillpower));
        }

        private void ApplyMoveSpeed()
        {
            if (movement == null) return;
            movement.SetStatSpeedMultiplier(CharacterStatFormulas.MoveSpeedMultiplier(TotalAgility));
        }

        // [멤] 장비 시스템: preserveMissingHealth=false면 최대 체력이 늘어도 현재 체력이 함께 오르지 않는다
        // ("방어구로 늘어난 최대체력은 현재체력을 회복시키지 않는다" - 사용자 확정 사양). 레벨업/포인트 투자는
        // 기존 동작(부족분 유지)을 그대로 두어야 하므로 장비 변경 경로에서만 false를 넘긴다.
        private void ApplyHealth(bool preserveMissingHealth)
        {
            if (territoryHealthController == null) return;

            if (preserveMissingHealth) territoryHealthController.RefreshFromExternalStatChange();
            else territoryHealthController.RefreshFromEquipmentChange();
        }

        /// <summary>[PlayerTerritoryHealthController 전용] 힘 스탯 기반 체력 배율. 영지레벨 기본 체력에 곱해서 쓴다.</summary>
        public float GetHealthMultiplier()
        {
            return CharacterStatFormulas.HealthMultiplier(TotalStrength);
        }

        // ---- 공격력/마력/데미지 계산 (PlayerWeaponSkillController에서 호출) ----

        /// <summary>물리 무기면 힘(주력)/민첩(부), 마법 무기면 지능(주력)/행운(부)으로 공격력·마력을 계산한다.</summary>
        public float GetAttackOrMagicPower(float weaponPower, WeaponDamageType damageType)
        {
            int primary = damageType == WeaponDamageType.Physical ? TotalStrength : TotalIntelligence;
            int secondary = damageType == WeaponDamageType.Physical ? TotalAgility : TotalLuck;
            return CharacterStatFormulas.AttackOrMagicPower(weaponPower, primary, secondary);
        }

        private bool RollCritical()
        {
            return UnityEngine.Random.Range(0f, 100f) < CritChancePercent;
        }

        /// <summary>기본공격 최종데미지 = 공격력or마력 × (1+데미지%) × (1+크리티컬데미지%[크리티컬 발동 시]).</summary>
        public int ComputeBasicAttackDamage(float weaponPower, WeaponDamageType damageType, out bool isCritical)
        {
            float power = GetAttackOrMagicPower(weaponPower, damageType);
            isCritical = RollCritical();
            float final = power * (1f + bonusDamagePercent / 100f) * (isCritical ? 1f + CritDamagePercent / 100f : 1f);
            return Mathf.Max(0, Mathf.RoundToInt(final));
        }

        /// <summary>
        /// 스킬 데미지(다단히트 1회분) = 기본공격 최종데미지 × 스킬데미지% (히트마다 크리티컬 독립 판정).
        /// HitCount만큼 이 메서드를 반복 호출해서 각 히트를 개별 투사체로 발사한다(PlayerWeaponSkillController 참고).
        /// </summary>
        public int ComputeSkillHitDamage(float weaponPower, WeaponDamageType damageType, float skillDamagePercent, out bool isCritical)
        {
            float power = GetAttackOrMagicPower(weaponPower, damageType);
            float baseHit = power * (1f + bonusDamagePercent / 100f) * Mathf.Max(0f, skillDamagePercent) / 100f;
            isCritical = RollCritical();
            float final = baseHit * (isCritical ? 1f + CritDamagePercent / 100f : 1f);
            return Mathf.Max(0, Mathf.RoundToInt(final));
        }

        /// <summary>
        /// 받는 데미지에 방어력%를 적용한다. 낙사/굶주림/수중 등 환경 피해는 방어력의 영향을 받지 않고
        /// Generic/MemAttack(전투성 피해)에만 적용한다 - 방어력은 "적의 공격을 막아내는" 스탯이라는
        /// 전제로 정했다(기획에 명시되지 않아 낮은 리스크로 직접 결정, 다르게 원하면 조정 가능).
        /// </summary>
        public float ApplyDefenseReduction(float amount, PlayerDamageType damageType)
        {
            if (damageType != PlayerDamageType.Generic && damageType != PlayerDamageType.MemAttack)
            {
                return amount;
            }

            return amount * (1f - DefensePercent / 100f);
        }

        /// <summary>
        /// 채집 실제 소비 지점에서 쓰라고 노출하는 합산 배율(행운 보너스 × 음식 효과 배율).
        /// [멤] 참고: 기존 KMSFoodEffectController.GatherAmountMultiplier 자체가 아직 실제 채집 보상 계산
        /// (WorldObject.ObjectInteract/HarvestableResource)에 연결되어 있지 않다(이번 작업 이전부터 그랬음,
        /// GH팀 소유 코드라 이번 범위에서 직접 손대지 않았다) - 이 메서드는 그 연결 작업을 할 때 바로 곱해
        /// 쓸 수 있도록 자리를 마련해둔 것이다.
        /// </summary>
        public float GetCombinedGatherAmountMultiplier()
        {
            float foodMultiplier = foodEffects != null ? foodEffects.GatherAmountMultiplier : 1f;
            return LuckGatherAmountMultiplier * foodMultiplier;
        }

        // ---- 포인트 투자 / 재분배 ----

        public int GetStat(CharacterStatType type)
        {
            switch (type)
            {
                case CharacterStatType.Strength: return strength;
                case CharacterStatType.Intelligence: return intelligence;
                case CharacterStatType.Agility: return agility;
                case CharacterStatType.Luck: return luck;
                case CharacterStatType.Willpower: return willpower;
                default: return 0;
            }
        }

        /// <summary>
        /// [멤] 장비 시스템: 투자 포인트 + 장비 보너스를 합친 총합 스탯. 모든 파생 공식(체력/배고픔/이동속도/
        /// 방어력/저항력/크리티컬/공격력·마력)이 이 값을 쓴다. GetStat은 여전히 "투자분"만 돌려주므로
        /// 스탯 투자/리스펙 UI는 그대로 GetStat을 쓰면 된다.
        /// </summary>
        public int GetTotalStat(CharacterStatType type)
        {
            return Mathf.Clamp(GetStat(type) + equipmentBonus.GetStat(type), 0, CharacterStatFormulas.StatValueCap);
        }

        /// <summary>
        /// [멤] 장비 시스템: PlayerEquipment가 장착 상태가 바뀔 때마다 호출한다. 값이 실제로 달라졌을 때만
        /// 파생 스탯을 다시 계산하며, 이때 최대 체력은 현재 체력을 회복시키지 않는 방식으로 갱신된다.
        /// </summary>
        public void SetEquipmentBonus(KMS.Equipment.EquipmentBonusSnapshot snapshot)
        {
            if (equipmentBonus.Equals(snapshot)) return;

            equipmentBonus = snapshot;
            ApplyAllDerivedStats(preserveMissingHealth: false);
            StatsChanged?.Invoke();
        }

        private void SetStat(CharacterStatType type, int value)
        {
            value = Mathf.Clamp(value, 0, CharacterStatFormulas.StatValueCap);
            switch (type)
            {
                case CharacterStatType.Strength: strength = value; break;
                case CharacterStatType.Intelligence: intelligence = value; break;
                case CharacterStatType.Agility: agility = value; break;
                case CharacterStatType.Luck: luck = value; break;
                case CharacterStatType.Willpower: willpower = value; break;
            }
        }

        /// <summary>보유 포인트에서 amount만큼(기본 1) 스탯에 투자한다. 상한(2000)에 걸리면 남는 만큼만 적용한다.</summary>
        public bool TryAllocatePoint(CharacterStatType type, int amount = 1)
        {
            if (amount <= 0 || unspentPoints < amount) return false;

            int current = GetStat(type);
            int desired = current + amount;
            int applied = Mathf.Min(desired, CharacterStatFormulas.StatValueCap) - current;
            if (applied <= 0) return false;

            SetStat(type, current + applied);
            unspentPoints -= applied;

            ApplyAllDerivedStats();
            StatsChanged?.Invoke();
            return true;
        }

        /// <summary>투자한 포인트를 전부 되돌리고(스탯 0으로) unspentPoints에 환불한다. 골드를 소비한다(respecGoldCost, 임시 고정값).</summary>
        public bool TryRespec()
        {
            if (territoryData == null || !territoryData.TrySpendGold(respecGoldCost))
            {
                return false;
            }

            unspentPoints += strength + intelligence + agility + luck + willpower;
            strength = 0;
            intelligence = 0;
            agility = 0;
            luck = 0;
            willpower = 0;

            ApplyAllDerivedStats();
            StatsChanged?.Invoke();
            return true;
        }

        // ---- 저장/불러오기 ----

        public PlayerCombatStatsSaveData CaptureSaveData()
        {
            return new PlayerCombatStatsSaveData
            {
                strength = strength,
                intelligence = intelligence,
                agility = agility,
                luck = luck,
                willpower = willpower,
                unspentPoints = unspentPoints,
                lastKnownTerritoryLevel = lastKnownTerritoryLevel,
            };
        }

        public void RestoreSaveData(PlayerCombatStatsSaveData data)
        {
            if (data == null) return;

            strength = Mathf.Clamp(data.strength, 0, CharacterStatFormulas.StatValueCap);
            intelligence = Mathf.Clamp(data.intelligence, 0, CharacterStatFormulas.StatValueCap);
            agility = Mathf.Clamp(data.agility, 0, CharacterStatFormulas.StatValueCap);
            luck = Mathf.Clamp(data.luck, 0, CharacterStatFormulas.StatValueCap);
            willpower = Mathf.Clamp(data.willpower, 0, CharacterStatFormulas.StatValueCap);
            unspentPoints = Mathf.Max(0, data.unspentPoints);
            lastKnownTerritoryLevel = Mathf.Max(1, data.lastKnownTerritoryLevel);
            hasInitializedLevel = true;

            ApplyAllDerivedStats();
            StatsChanged?.Invoke();
        }

        private void OnValidate()
        {
            baseMaxHunger = Mathf.Max(1f, baseMaxHunger);
            respecGoldCost = Mathf.Max(0, respecGoldCost);
            strength = Mathf.Clamp(strength, 0, CharacterStatFormulas.StatValueCap);
            intelligence = Mathf.Clamp(intelligence, 0, CharacterStatFormulas.StatValueCap);
            agility = Mathf.Clamp(agility, 0, CharacterStatFormulas.StatValueCap);
            luck = Mathf.Clamp(luck, 0, CharacterStatFormulas.StatValueCap);
            willpower = Mathf.Clamp(willpower, 0, CharacterStatFormulas.StatValueCap);
            unspentPoints = Mathf.Max(0, unspentPoints);
        }
    }
}
