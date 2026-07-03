// ============================================================================
// MemAI.cs
// FSM(유한 상태 머신) 메인 컨트롤러
//
// [담당자 안내]
// - 멤의 AI 행동을 5개 상태(Idle/Wander/Combat/Flee/Captured)로 관리합니다.
// - 성격(Personality)에 따라 피격 시 반응이 다릅니다:
//   · 온순(Docile): 피격 → 무조건 Flee
//   · 평범(Normal): 피격 → Combat, HP 30% 이하 → Flee
//   · 난폭(Aggressive): 플레이어 인식 → 선제 Combat, HP 30% 이하 → Flee
// - 플레이어 Transform은 "Player" 태그로 자동 탐색하거나 Inspector에서 수동 할당합니다.
// ============================================================================
using UnityEngine;
using MemSystem.Core;
using MemSystem.Data;
using MemSystem.Movement;
using MemSystem.Visual;
using MemSystem.AI.States;

namespace MemSystem.AI
{
    /// <summary>
    /// 멤 AI FSM 컨트롤러.
    /// 
    /// [상태 전환 다이어그램]
    ///              ┌─────────┐
    ///              │  Idle   │◄──── 목표 도달 / 추적 포기
    ///              └────┬────┘
    ///   시간 경과 ──────┤
    ///              ┌────▼────┐
    ///              │ Wander  │
    ///              └────┬────┘
    ///   피격/감지 ──────┤
    ///         ┌────────┴────────┐
    ///   온순: │                 │ 평범/난폭:
    ///    ┌────▼────┐     ┌─────▼─────┐
    ///    │  Flee   │     │  Combat   │
    ///    └────┬────┘     └─────┬─────┘
    ///         │          HP 낮음│
    ///         │          ┌─────▼─────┐
    ///         │          │   Flee    │
    ///         └──────────┴─────┬─────┘
    ///   도주 완료 ─────────────┤
    ///                    ┌─────▼─────┐
    ///                    │  Despawn  │
    ///                    └───────────┘
    ///   포획 성공 (어디서든) ──► Captured ──► Pool 반환
    /// </summary>
    public class MemAI : MonoBehaviour
    {
        // =================================================================
        // Inspector 설정
        // =================================================================

        [Header("감지 설정")]
        [Tooltip("플레이어 Transform — 미설정 시 'Player' 태그로 자동 탐색합니다.")]
        [SerializeField] private Transform playerTransform;

        [Header("Idle/Wander 설정")]
        [Tooltip("Idle 상태 유지 시간 범위 (초). x=최소, y=최대.")]
        [SerializeField] private Vector2 idleDurationRange = new Vector2(2f, 5f);

        [Tooltip("Wander 목표 도달 후 Idle로 복귀할 확률 (0~1). 나머지는 재배회.")]
        [SerializeField] private float returnToIdleChance = 0.3f;

        // =================================================================
        // 상태 인스턴스 (한번 생성, 재사용)
        // =================================================================

        /// <summary>대기 상태</summary>
        public IdleState IdleState { get; private set; }

        /// <summary>배회 상태</summary>
        public WanderState WanderState { get; private set; }

        /// <summary>도주 상태</summary>
        public FleeState FleeState { get; private set; }

        /// <summary>전투 상태</summary>
        public CombatState CombatState { get; private set; }

        /// <summary>포획 상태</summary>
        public CapturedState CapturedState { get; private set; }

        // =================================================================
        // 현재 상태
        // =================================================================

        /// <summary>현재 활성 상태</summary>
        public IMemState CurrentState { get; private set; }

        // =================================================================
        // 참조 (다른 컴포넌트/외부에서 접근)
        // =================================================================

        /// <summary>이 멤의 루트 엔티티</summary>
        public Mem Owner { get; private set; }

        /// <summary>이동 컴포넌트</summary>
        public MemMovement Movement { get; private set; }

        /// <summary>비주얼 컴포넌트</summary>
        public MemVisual Visual { get; private set; }

        /// <summary>플레이어 Transform</summary>
        public Transform PlayerTransform => playerTransform;

        /// <summary>플레이어까지 거리 (플레이어 없으면 float.MaxValue)</summary>
        public float DistanceToPlayer =>
            playerTransform != null
                ? Vector3.Distance(transform.position, playerTransform.position)
                : float.MaxValue;

        // =================================================================
        // 설정값 접근자 (상태 클래스에서 사용)
        // =================================================================

        /// <summary>Idle 유지 시간 범위</summary>
        public Vector2 IdleDurationRange => idleDurationRange;

        /// <summary>Idle 복귀 확률</summary>
        public float ReturnToIdleChance => returnToIdleChance;

        // =================================================================
        // 초기화
        // =================================================================

        /// <summary>
        /// AI를 초기화합니다. Mem.Initialize()에서 호출됩니다.
        /// </summary>
        public void Initialize(Mem owner)
        {
            Owner = owner;
            Movement = owner.Movement;
            Visual = owner.Visual;

            // 플레이어 자동 탐색 (Inspector에서 미설정 시)
            if (playerTransform == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                }
            }

            // 상태 인스턴스 생성 (최초 1회만, 이후 재사용)
            if (IdleState == null)
            {
                IdleState = new IdleState();
                WanderState = new WanderState();
                FleeState = new FleeState();
                CombatState = new CombatState();
                CapturedState = new CapturedState();
            }

            // 초기 상태: Idle
            TransitionTo(IdleState);
        }

        /// <summary>
        /// 풀 반환 시 상태를 리셋합니다.
        /// </summary>
        public void ResetState()
        {
            if (CurrentState != null)
            {
                CurrentState.Exit(this);
            }
            CurrentState = null;
        }

        // =================================================================
        // Update
        // =================================================================

        private void Update()
        {
            if (Owner == null || !Owner.IsActive) return;

            // 난폭(Aggressive) 멤: Idle/Wander 중 플레이어 감지 시 선제 공격
            CheckAggressiveDetection();

            // 현재 상태 업데이트
            CurrentState?.Update(this);
        }

        // =================================================================
        // 상태 전환
        // =================================================================

        /// <summary>
        /// 지정된 상태로 전환합니다.
        /// 현재 상태의 Exit() → 새 상태의 Enter() 순서로 호출됩니다.
        /// </summary>
        /// <param name="newState">전환할 상태 인스턴스</param>
        public void TransitionTo(IMemState newState)
        {
            if (newState == null) return;

            CurrentState?.Exit(this);
            CurrentState = newState;
            CurrentState.Enter(this);

            Debug.Log($"[MemAI] {Owner?.Stats?.MemName} 상태 전환: {newState.GetType().Name}");
        }

        // =================================================================
        // 이벤트 수신 (Mem.TakeDamage에서 호출)
        // =================================================================

        /// <summary>
        /// 피격당했을 때 호출됩니다.
        /// 성격(Personality)에 따라 다르게 반응합니다.
        /// </summary>
        /// <param name="damage">받은 데미지</param>
        public void OnDamageTaken(int damage)
        {
            if (Owner == null || Owner.Stats == null) return;

            var personality = Owner.Stats.Personality;

            switch (personality)
            {
                case MemPersonality.Docile:
                    // 온순: 무조건 도망
                    if (CurrentState != FleeState && CurrentState != CapturedState)
                    {
                        TransitionTo(FleeState);
                    }
                    break;

                case MemPersonality.Normal:
                    // 평범: 피격 시 반격, HP 낮으면 도주
                    if (Owner.Stats.ShouldFlee)
                    {
                        TransitionTo(FleeState);
                    }
                    else if (CurrentState != CombatState && CurrentState != CapturedState)
                    {
                        TransitionTo(CombatState);
                    }
                    break;

                case MemPersonality.Aggressive:
                    // 난폭: HP 낮으면 도주, 아니면 계속 전투
                    if (Owner.Stats.ShouldFlee)
                    {
                        TransitionTo(FleeState);
                    }
                    else if (CurrentState != CombatState && CurrentState != CapturedState)
                    {
                        TransitionTo(CombatState);
                    }
                    break;
            }
        }

        // =================================================================
        // 내부 로직
        // =================================================================

        /// <summary>
        /// 난폭 멤의 선제 공격 감지.
        /// Idle/Wander 상태에서만 작동하며,
        /// 인식 범위(DetectionRange) 내에 플레이어가 진입하면 Combat으로 전환합니다.
        /// </summary>
        private void CheckAggressiveDetection()
        {
            if (Owner?.Stats == null) return;
            if (Owner.Stats.Personality != MemPersonality.Aggressive) return;
            if (CurrentState == CombatState || CurrentState == FleeState || CurrentState == CapturedState) return;
            if (playerTransform == null) return;

            float distance = DistanceToPlayer;
            if (distance <= Owner.Stats.DetectionRange)
            {
                Debug.Log($"[MemAI] {Owner.Stats.MemName} 난폭 멤: 플레이어 감지! 선제 공격 전환");
                TransitionTo(CombatState);
            }
        }
    }
}
