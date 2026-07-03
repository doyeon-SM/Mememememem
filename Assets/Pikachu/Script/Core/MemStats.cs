// ============================================================================
// MemStats.cs
// 런타임 스탯 컴포넌트 — 멤의 현재 상태를 관리
//
// [담당자 안내]
// - 정적 데이터(MemData SO)로부터 초기화되고, 게임 중 동적으로 변경됩니다.
// - 2단계 초기화: Initialize(MemData) → ApplyTierSpec(MemTierSpec)
// - Object Pool 재사용 시 ResetStats()로 HP/허기만 복원합니다.
// ============================================================================
using UnityEngine;
using MemSystem.Data;

namespace MemSystem.Core
{
    /// <summary>
    /// 멤의 런타임 스탯을 관리하는 컴포넌트.
    /// 
    /// [초기화 흐름]
    /// 1. MemFactory가 Initialize(MemData)를 호출 → SO 데이터 복사
    /// 2. MemFactory가 ApplyTierSpec(MemTierSpec)을 호출 → 등급별 고정값 덮어쓰기
    /// 3. Object Pool 재사용 시 ResetStats()만 호출 → HP/허기 원복
    /// 
    /// [외부 시스템 참고]
    /// - 읽기: HpRatio, ShouldFlee, IsStarving 등 계산 프로퍼티 활용
    /// - 쓰기: CurrentHp, CurrentHunger는 public set 가능 (전투/영지 시스템)
    /// </summary>
    public class MemStats : MonoBehaviour
    {
        // =================================================================
        // 기본 정보 (읽기 전용 — 초기화 후 변경 불가)
        // =================================================================

        /// <summary>멤 고유 식별자</summary>
        public string MemId { get; private set; }

        /// <summary>멤 표시 이름</summary>
        public string MemName { get; private set; }

        /// <summary>등급</summary>
        public MemTier Tier { get; private set; }

        /// <summary>성격 (온순/평범/난폭)</summary>
        public MemPersonality Personality { get; private set; }

        // =================================================================
        // 전투 스탯 (CurrentHp는 외부에서 수정 가능)
        // =================================================================

        /// <summary>현재 HP — TakeDamage() 등에서 감소됩니다.</summary>
        public int CurrentHp { get; set; }

        /// <summary>최대 HP — 등급별 고정값</summary>
        public int MaxHp { get; private set; }

        // =================================================================
        // 허기 스탯 (영지 작업 시스템에서 사용)
        // =================================================================

        /// <summary>현재 허기 — 작업 중 분당 소비됩니다.</summary>
        public int CurrentHunger { get; set; }

        /// <summary>최대 허기 — 등급별 고정값</summary>
        public int MaxHunger { get; private set; }

        // =================================================================
        // 생산/탐험 스탯 (영지 시설 배치 시 참조)
        // =================================================================

        /// <summary>생산 스탯 5종 (제작/벌목/채광/이동/생산)</summary>
        public ProductionStats ProductionStats { get; private set; }

        /// <summary>탐험 스탯 — 개체마다 랜덤 결정</summary>
        public int ExplorationStat { get; private set; }

        // =================================================================
        // 전투 파라미터 (MemData에서 가져옴)
        // =================================================================

        /// <summary>공격력</summary>
        public int AttackDamage { get; private set; }

        /// <summary>공격 사거리</summary>
        public float AttackRange { get; private set; }

        /// <summary>공격 쿨타임 (초)</summary>
        public float AttackCooldown { get; private set; }

        /// <summary>인식 범위 — 난폭 멤의 선제 공격 범위</summary>
        public float DetectionRange { get; private set; }

        /// <summary>도주 임계 HP 비율 (0.0~1.0)</summary>
        public float FleeHpThreshold { get; private set; }

        // =================================================================
        // 계산 프로퍼티 (자주 사용되는 상태 체크)
        // =================================================================

        /// <summary>현재 HP 비율 (0.0 ~ 1.0). UI HP바 등에 사용합니다.</summary>
        public float HpRatio => MaxHp > 0 ? (float)CurrentHp / MaxHp : 0f;

        /// <summary>사망 여부. HP가 0 이하면 true.</summary>
        public bool IsDead => CurrentHp <= 0;

        /// <summary>도주 조건 충족 여부. 현재 HP 비율이 도주 임계치 이하면 true.</summary>
        public bool ShouldFlee => HpRatio <= FleeHpThreshold && !IsDead;

        /// <summary>현재 허기 비율 (0.0 ~ 1.0).</summary>
        public float HungerRatio => MaxHunger > 0 ? (float)CurrentHunger / MaxHunger : 0f;

        /// <summary>허기 고갈 여부. 0이면 작업 중단.</summary>
        public bool IsStarving => CurrentHunger <= 0;

        // =================================================================
        // 초기화 메서드
        // =================================================================

        /// <summary>
        /// MemData(SO)로부터 스탯을 초기화합니다.
        /// MemFactory.InitializeMem() 내부에서 호출됩니다.
        /// </summary>
        /// <param name="data">멤 정적 데이터 에셋</param>
        public void Initialize(MemData data)
        {
            MemId = data.memId;
            MemName = data.memName;
            Tier = data.tier;
            Personality = data.personality;

            MaxHp = data.maxHp;
            CurrentHp = data.maxHp;

            MaxHunger = data.maxHunger;
            CurrentHunger = data.maxHunger;

            ProductionStats = data.productionStats;
            ExplorationStat = data.explorationStat;

            AttackDamage = data.attackDamage;
            AttackRange = data.attackRange;
            AttackCooldown = data.attackCooldown;
            DetectionRange = data.detectionRange;
            FleeHpThreshold = data.fleeHpThreshold;
        }

        /// <summary>
        /// MemTierTable의 등급별 고정값으로 스탯을 덮어씁니다.
        /// Initialize() 이후에 호출되어 등급별 밸런싱을 적용합니다.
        /// </summary>
        /// <param name="spec">등급별 스펙 (MemTierTable에서 조회)</param>
        public void ApplyTierSpec(MemTierSpec spec)
        {
            if (spec == null) return;

            MaxHp = spec.baseHp;
            CurrentHp = spec.baseHp;
            MaxHunger = spec.baseHunger;
            CurrentHunger = spec.baseHunger;
            ExplorationStat = spec.RollExplorationStat();
        }

        /// <summary>
        /// Object Pool 재사용 시 HP/허기만 리셋합니다.
        /// 기본 정보(이름, 등급 등)는 유지됩니다.
        /// </summary>
        public void ResetStats()
        {
            CurrentHp = MaxHp;
            CurrentHunger = MaxHunger;
        }

        public override string ToString()
        {
            return $"[{MemName}] {Tier} | HP:{CurrentHp}/{MaxHp} | Hunger:{CurrentHunger}/{MaxHunger} | {Personality}";
        }
    }
}
