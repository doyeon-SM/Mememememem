// ============================================================================
// Mem.cs
// 멤 엔티티 루트 컴포넌트
//
// [담당자 안내]
// - 멤 GameObject의 최상위 컴포넌트입니다.
// - 하위 컴포넌트(Stats, AI, Movement, Visual)에 대한 참조를 보유합니다.
// - ICapturable 인터페이스를 구현하여 플레이어 시스템과 연동합니다.
// - TakeDamage(), OnCaptureSuccess() 등이 외부 시스템의 진입점입니다.
//
// [플레이어 담당자]
// - ICapturable로 캐스팅하여 포획 로직을 수행하세요.
// - TakeDamage(int)로 멤에게 데미지를 줄 수 있습니다.
// ============================================================================
using UnityEngine;
using MemSystem.Data;
using MemSystem.Events;
using MemSystem.Interface;
using MemSystem.AI;
using MemSystem.Movement;
using MemSystem.Visual;

namespace MemSystem.Core
{
    /// <summary>
    /// 멤 엔티티의 루트 컴포넌트.
    /// 
    /// [GameObject 구성]
    /// Mem (Root)
    ///  ├─ Mem.cs (이 스크립트)
    ///  ├─ MemStats.cs
    ///  ├─ MemAI.cs
    ///  ├─ MemMovement.cs
    ///  ├─ NavMeshAgent
    ///  ├─ CapsuleCollider (Trigger)
    ///  └─ [ModelRoot]
    ///      └─ MemVisual.cs
    /// </summary>
    [RequireComponent(typeof(MemStats))]
    public class Mem : MonoBehaviour, ICapturable
    {
        // =================================================================
        // 컴포넌트 참조 (Awake에서 자동 캐싱)
        // =================================================================

        /// <summary>런타임 스탯 관리</summary>
        public MemStats Stats { get; private set; }

        /// <summary>AI 상태머신 (FSM)</summary>
        public MemAI AI { get; private set; }

        /// <summary>NavMeshAgent 이동 제어</summary>
        public MemMovement Movement { get; private set; }

        /// <summary>모델/애니메이션 제어</summary>
        public MemVisual Visual { get; private set; }

        /// <summary>이 멤의 원본 데이터 에셋 참조</summary>
        public MemData Data { get; private set; }

        /// <summary>
        /// Object Pool에서 활성 상태인지 여부.
        /// false이면 풀에 반환 대기 중이므로 상호작용하면 안 됩니다.
        /// </summary>
        public bool IsActive { get; private set; }

        // =================================================================
        // Unity Lifecycle
        // =================================================================

        private void Awake()
        {
            // 컴포넌트 자동 캐싱
            Stats = GetComponent<MemStats>();
            AI = GetComponent<MemAI>();
            Movement = GetComponent<MemMovement>();
            Visual = GetComponentInChildren<MemVisual>();
        }

        // =================================================================
        // 초기화 / 리셋 (MemFactory에서 호출)
        // =================================================================

        /// <summary>
        /// 멤을 초기화합니다.
        /// 
        /// 호출 순서:
        /// 1. Stats.Initialize(data) — SO 데이터 복사
        /// 2. Stats.ApplyTierSpec(spec) — 등급별 고정값 적용
        /// 3. Visual.SetupModel(modelPrefab) — 등급별 모델 스왑
        /// 4. AI.Initialize(this) — FSM 초기 상태(Idle) 진입
        /// </summary>
        /// <param name="data">멤 정적 데이터</param>
        /// <param name="tierTable">등급별 스펙 테이블</param>
        public void Initialize(MemData data, MemTierTable tierTable)
        {
            Data = data;

            // 1. 스탯 초기화
            Stats.Initialize(data);

            // 2. 등급별 고정 스펙 적용
            var spec = tierTable?.GetSpec(data.tier);
            if (spec != null)
            {
                Stats.ApplyTierSpec(spec);
            }

            // 3. 외형 세팅 (등급별 모델)
            if (Visual != null && data.modelPrefab != null)
            {
                Visual.SetupModel(data.modelPrefab);
            }

            // 4. AI 초기 상태
            if (AI != null)
            {
                AI.Initialize(this);
            }

            IsActive = true;
        }

        /// <summary>
        /// Object Pool 반환 시 상태를 리셋합니다.
        /// MemPool.Despawn()에서 호출됩니다.
        /// </summary>
        public void ResetForPool()
        {
            IsActive = false;
            Stats.ResetStats();

            if (AI != null) AI.ResetState();
            if (Movement != null) Movement.Stop();
            if (Visual != null) Visual.ResetVisual();
        }

        // =================================================================
        // 전투 — 피격 처리
        // [플레이어 담당자] 멤을 공격할 때 이 메서드를 호출하세요.
        // =================================================================

        /// <summary>
        /// 멤이 데미지를 받습니다.
        /// 
        /// [플레이어 담당자 사용법]
        /// mem.TakeDamage(weaponDamage);
        /// 
        /// 내부 처리:
        /// 1. HP 감소
        /// 2. 피격 연출 (빨간 플래시 + 흔들림)
        /// 3. MemEvents.OnMemDamaged 이벤트 발행
        /// 4. AI에 피격 통보 → 성격에 따른 반응 (도주/반격/선제공격)
        /// </summary>
        /// <param name="damage">가할 데미지 양</param>
        public void TakeDamage(int damage)
        {
            if (!IsActive || Stats.IsDead) return;

            Stats.CurrentHp = Mathf.Max(0, Stats.CurrentHp - damage);

            // 피격 연출
            if (Visual != null) Visual.PlayHit();

            // 이벤트 발행 → UI 등 외부 시스템에 알림
            MemEvents.OnMemDamaged?.Invoke(this, damage);

            // AI에 피격 통보 → 성격에 따른 반응
            if (AI != null) AI.OnDamageTaken(damage);

            Debug.Log($"[Mem] {Stats.MemName} 피격! HP: {Stats.CurrentHp}/{Stats.MaxHp}");
        }

        // =================================================================
        // ICapturable 구현
        // [플레이어 담당자] 포획 시 이 인터페이스 메서드들을 사용하세요.
        // =================================================================

        /// <summary>현재 등급</summary>
        public MemTier Tier => Stats.Tier;

        /// <summary>현재 HP</summary>
        public int CurrentHp => Stats.CurrentHp;

        /// <summary>최대 HP</summary>
        public int MaxHp => Stats.MaxHp;

        /// <summary>
        /// 현재 포획 확률을 계산합니다.
        /// 
        /// 공식: (1 - 현재HP/최대HP) × 등급보정 × 캡슐보정
        /// - HP가 낮을수록 확률 증가
        /// - 등급이 높을수록 확률 감소 (레어=1.0, 신화=0.15)
        /// - 캡슐 등급이 높을수록 확률 증가 (+25%씩)
        /// 
        /// [플레이어 담당자]
        /// 조준 중 UI에 이 값을 실시간으로 표시합니다.
        /// </summary>
        public float GetCaptureRate(int capsuleTier)
        {
            if (Stats.MaxHp <= 0) return 1f;

            // HP가 낮을수록 포획 확률 증가
            float hpFactor = 1f - Stats.HpRatio;

            // 등급이 높을수록 포획 어려움 (역비례)
            float tierModifier = GetTierModifier(Stats.Tier);

            // 캡슐 등급이 높을수록 포획 쉬움 (+25%씩)
            float capsuleModifier = 1f + (capsuleTier * 0.25f);

            float rate = hpFactor * tierModifier * capsuleModifier;
            return Mathf.Clamp01(rate);
        }

        /// <summary>
        /// 포획 성공 처리.
        /// 1. MemSnapshot 생성 (풀 반환 전에 데이터 복사)
        /// 2. AI → CapturedState 전환 (축소 연출)
        /// 3. MemEvents.OnMemCaptured 이벤트 발행 (영지/창고/도감이 수신)
        /// </summary>
        public void OnCaptureSuccess()
        {
            if (!IsActive) return;

            Debug.Log($"[Mem] {Stats.MemName} 포획 성공!");

            // 스냅샷 생성 (풀 반환 전에!)
            var snapshot = MemSnapshot.FromMem(this);

            // AI 상태 전환 (포획 연출)
            if (AI != null) AI.TransitionTo(AI.CapturedState);

            // 이벤트 발행 → 영지/창고 시스템이 수신
            MemEvents.OnMemCaptured?.Invoke(this, snapshot);

            IsActive = false;
        }

        /// <summary>
        /// 포획 실패 처리.
        /// shouldFlee가 true면 도망(디스폰), false면 전투 속행.
        /// </summary>
        public void OnCaptureFail(bool shouldFlee)
        {
            if (!IsActive) return;

            Debug.Log($"[Mem] {Stats.MemName} 포획 실패! 도주: {shouldFlee}");

            MemEvents.OnMemCaptureFailed?.Invoke(this);

            if (shouldFlee && AI != null)
            {
                AI.TransitionTo(AI.FleeState);
            }
        }

        // =================================================================
        // 디스폰 처리 (MemSpawner에서 호출)
        // =================================================================

        /// <summary>
        /// 자연 디스폰 시 호출 (플레이어 이탈 1분 이상 등).
        /// MemSpawner에서 호출됩니다.
        /// </summary>
        public void OnDespawn()
        {
            if (!IsActive) return;

            MemEvents.OnMemDespawned?.Invoke(this);
            IsActive = false;
        }

        /// <summary>
        /// 도주 완료 시 호출.
        /// FleeState에서 도주 거리 달성 후 호출됩니다.
        /// </summary>
        public void OnFleeComplete()
        {
            if (!IsActive) return;

            MemEvents.OnMemFled?.Invoke(this);
            IsActive = false;
        }

        // =================================================================
        // 내부 유틸
        // =================================================================

        /// <summary>
        /// 등급별 포획 난이도 보정치.
        /// 등급이 높을수록 값이 낮아 포획이 어려워집니다.
        /// </summary>
        private float GetTierModifier(MemTier tier)
        {
            return tier switch
            {
                MemTier.Rare => 1.0f,       // 레어: 보정 없음
                MemTier.Epic => 0.7f,       // 에픽: 30% 감소
                MemTier.Unique => 0.5f,     // 유니크: 50% 감소
                MemTier.Legendary => 0.3f,  // 레전더리: 70% 감소
                MemTier.Mythic => 0.15f,    // 신화: 85% 감소
                _ => 1.0f
            };
        }
    }
}
