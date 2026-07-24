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
// [HDY 요청으로 수정됨] GetCaptureRate(): 최소 포획확률 1% 보장 + 캡슐이 최고 등급(Mythic)이면 무조건 성공
//
// ─────────────────────────────────────────────────────────────────────────────
// [플레이어 담당자 — 포획 흐름 전체 가이드]
//
// 포획 연출은 아래 순서로 진행됩니다:
//
// 1. [조준 중] 포획 확률 UI 실시간 표시
//    float rate = (capturable as ICapturable).GetCaptureRate(myCapsuleTier);
//    // rate를 기준으로 조준 UI에 그라데이션/파샘트 표시
//
// 2. [캡슐 명중 시] ICapturable.NotifyCaptureBallHit(캡슐의 월드좌표)
//    ⇒ 멤 빛남+축소+이동 연출 자동 시작
//    ⇒ MemEvents.OnMemCaptureStarted 자동 발행
//       → 이 이벤트를 구독하여 캡슐 흔들림 연출 시작
//
// 3-A. [포획 성공 시] ICapturable.OnCaptureSuccess()
//    ⇒ MemEvents.OnMemCaptured 자동 발행
//       → 이 이벤트를 구독하여 캡슐 반짜임+사라짐 연출 구현
//
// 3-B. [포획 실패 시] ICapturable.OnCaptureFail(shouldFlee)
//    ⇒ 멤 튀어나는 연출(빛남+팝업) 자동 재생
//    ⇒ MemEvents.OnMemCaptureFailed 자동 발행
//       → 이 이벤트를 구독하여 캡슐 파열 이펙트 구현
//
// ⚠️ 2번 NotifyCaptureBallHit을 호출하지 않으면 캡슐 위치를 파악할 수 없어
//    3-A/3-B의 연출 시작 위치가 월드원점(0,0,0)이 됩니다.
// ─────────────────────────────────────────────────────────────────────────────
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
            // 공격자 위치를 안 넘겨준 경우: 플레이어를 공격자로 간주해 "맞은 방향"으로 밀리게 한다.
            // (플레이어를 못 찾으면 기존 동작 - 멤이 바라보는 방향의 뒤쪽으로 밀림)
            Transform attacker = AI != null ? AI.PlayerTransform : null;

            if (attacker != null) ApplyDamage(damage, true, attacker.position);
            else                  ApplyDamage(damage, false, Vector3.zero);
        }

        /// <summary>
        /// 공격자 위치를 지정해 데미지를 입힙니다. 피격 연출이 "맞은 방향"으로 밀립니다.
        /// [플레이어/공격 담당자] 공격 지점을 알고 있으면 이 오버로드를 쓰면 연출이 더 정확합니다.
        /// </summary>
        /// <param name="damage">가할 데미지 양</param>
        /// <param name="attackerPosition">공격이 들어온 지점의 월드 좌표(공격자/무기 위치)</param>
        public void TakeDamage(int damage, Vector3 attackerPosition)
        {
            ApplyDamage(damage, true, attackerPosition);
        }

        private void ApplyDamage(int damage, bool hasAttackerPosition, Vector3 attackerPosition)
        {
            if (!IsActive || Stats.IsDead) return;

            Stats.CurrentHp = Mathf.Max(0, Stats.CurrentHp - damage);

            // 피격 연출 — 공격자 위치를 알면 그 반대 방향(맞은 방향)으로 밀린다.
            if (Visual != null)
            {
                if (hasAttackerPosition) Visual.PlayHitFrom(transform.position - attackerPosition);
                else                     Visual.PlayHit();
            }

            // 이벤트 발행 → UI 등 외부 시스템에 알림
            MemEvents.OnMemDamaged?.Invoke(this, damage);

            // AI에 피격 통보 → 성격에 따른 반응
            if (AI != null) AI.OnDamageTaken(damage);

            Debug.Log($"[Mem] {Stats.MemName} 피격! HP: {Stats.CurrentHp}/{Stats.MaxHp}");
        }

        // =================================================================
        // ICapturable 구현
        // [플레이어 담당자] 포획 시 이 인터페이스 메서드들을 사용하세요.
        // 자세한 사용법은 ICapturable.cs 파일 상단 주석을 참고하세요.
        // =================================================================

        /// <summary>당 멤이 빨려 들어갈 캡슐의 월드 좌표.
        /// NotifyCaptureBallHit()에서 설정되며, 포획 실패 시 PlayCaptureEject 연출에 사용됩니다.
        /// </summary>
        private Vector3 lastCapsulePosition;

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
        /// - [HDY 요청] 캡슐이 최고 등급(Mythic)이면 위 계산과 무관하게 무조건 100% 성공
        /// - [HDY 요청] 최종 확률은 최소 1%를 보장 (풀피 등으로 계산값이 0이 되어도 1%는 유지)
        /// 
        /// [플레이어 담당자]
        /// 조준 중 UI에 이 값을 실시간으로 표시합니다.
        /// </summary>
        /// <summary>
        /// 캡슐이 멤에 명중했을 때 호출됩니다.
        ///
        /// 내부에서 자동 처리되는 사항:
        /// 1. CapturedState에 캡슐 월드좌표 저장 (PlayCaptureAbsorb 참조용)
        /// 2. 멤 빛남+축소 연출 시작 (MemVisual.PlayCaptureAbsorb)
        /// 3. AI를 CapturedState로 전환 (이동 정지)
        /// 4. MemEvents.OnMemCaptureStarted 이벤트 발행
        ///
        /// [플레이어 담당자]
        /// 캡슐이 멤에 히트된 직후 이 메서드를 호출하세요.
        /// 이 호출 전후로 OnCaptureSuccess() 또는 OnCaptureFail()을 호출하세요.
        /// </summary>
        /// <param name="capsulePosition">캡슐이 명중한 순간의 월드 좌표</param>
        public void NotifyCaptureBallHit(Vector3 capsulePosition)
        {
            if (!IsActive) return;

            // 캡슐 위치 저장 (포획 실패 시 탈출 연출 시작점에 사용)
            lastCapsulePosition = capsulePosition;

            // CapturedState에 캡슐 위치 전달 (PlayCaptureAbsorb 목표 위치)
            if (AI != null)
                AI.CapturedState.SetCaptureTarget(capsulePosition);

            // AI를 CapturedState로 전환 → Enter()에서 PlayCaptureAbsorb() 자동 호출
            if (AI != null)
                AI.TransitionTo(AI.CapturedState);

            // 포획 흡수 시작 이벤트 발행 → 플레이어 담당자가 수신하여 캡슐 흔들림 연출
            MemEvents.OnMemCaptureStarted?.Invoke(this, capsulePosition);

            Debug.Log($"[Mem] {Stats.MemName} 포획 흡수 시작 (캡슐 위치: {capsulePosition})");
        }

        public float GetCaptureRate(int capsuleTier)
        {
            // [HDY 요청] 캡슐이 최고 등급(Mythic)이면 무조건 포획 성공
            if (capsuleTier >= (int)MemTier.Mythic)
            {
                return 1f;
            }

            if (Stats.MaxHp <= 0) return 1f;

            // HP가 낮을수록 포획 확률 증가
            float hpFactor = 1f - Stats.HpRatio;

            // 등급이 높을수록 포획 어려움 (역비례)
            float tierModifier = GetTierModifier(Stats.Tier);

            // 캡슐 등급이 높을수록 포획 쉬움 (+25%씩)
            float capsuleModifier = 1f + (capsuleTier * 0.25f);

            float rate = hpFactor * tierModifier * capsuleModifier;

            // [HDY 요청] 최소 포획확률 1% 보장
            return Mathf.Clamp(rate, 0.01f, 1f);
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
        ///
        /// 내부에서 자동 처리되는 사항:
        /// - MemVisual.PlayCaptureEject(): 캡슐에서 멤이 튀어나오는 연출 (빛남+팝업+바운스)
        /// - MemEvents.OnMemCaptureFailed 이벤트 발행
        ///
        /// [플레이어 담당자]
        /// 이 메서드 호출 후 MemEvents.OnMemCaptureFailed 이벤트를 수신하여
        /// 캡슐 파열 이펙트를 재생하세요.
        /// </summary>
        public void OnCaptureFail()
        {
            if (!IsActive) return;

            // 도망 여부 결정
            bool shouldFlee = false;

            // 1. HP가 0이면 무조건 도망 (디스폰)
            if (Stats.ShouldFlee)
            {
                shouldFlee = true;
            }
            // 2. 그 외의 경우 등급(Tier)에 따라 확률적으로 도망
            else
            {
                switch (Stats.Tier)
                {
                    case MemTier.Rare:
                        shouldFlee = Random.value < 0.20f; // 20%
                        break;
                    case MemTier.Epic:
                        shouldFlee = Random.value < 0.40f; // 40%
                        break;
                    case MemTier.Unique:
                        shouldFlee = Random.value < 0.60f; // 60%
                        break;
                    case MemTier.Legendary:
                        shouldFlee = Random.value < 0.80f; // 80%
                        break;
                    case MemTier.Mythic:
                        shouldFlee = Random.value < 0.95f; // 95%
                        break;
                }
            }

            Debug.Log($"[Mem] {Stats.MemName} 포획 실패! 도주 여부(자체판단): {shouldFlee}");

            // 캡슐에서 멤이 튀어나오는 연출 (빛남+팝업+바운스)
            // lastCapsulePosition: NotifyCaptureBallHit()에서 저장된 캡슐 위치
            if (Visual != null)
                Visual.PlayCaptureEject(lastCapsulePosition);

            // 포획 실패 이벤트 발행 → 플레이어 담당자가 수신하여 캡슐 파열 이펙트 재생
            MemEvents.OnMemCaptureFailed?.Invoke(this);

            if (shouldFlee && AI != null)
            {
                // 도망 성공
                AI.TransitionTo(AI.FleeState);
            }
            else if (!shouldFlee && AI != null)
            {
                // 도망 실패 (탈출)
                if (Stats.Personality == MemPersonality.Docile)
                {
                    // 온순한 멤은 공격하지 않고 배회 상태로 돌아감
                    AI.TransitionTo(AI.WanderState);
                }
                else
                {
                    // 전투 속행: 플레이어에게 반격 (CombatState로 복귀)
                    AI.TransitionTo(AI.CombatState);
                }
            }
        }

        // =================================================================
        // [하위 호환성] 타 파트에서 아직 수정하지 않은 코드가 깨지지 않도록 유지
        // =================================================================
        [System.Obsolete("도망 여부는 멤이 자체 판단합니다. 매개변수 없는 OnCaptureFail()을 사용하세요.")]
        public void OnCaptureFail(bool shouldFlee)
        {
            Debug.LogWarning("[Mem] OnCaptureFail(bool)은 더 이상 사용되지 않습니다. 멤의 자체 판단 로직으로 자동 전환됩니다.");
            OnCaptureFail();
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
