// ============================================================================
// MemMovement.cs
// NavMeshAgent 기반 멤 이동 제어
//
// [담당자 안내]
// - NavMeshAgent를 래핑하여 Wander(배회), Chase(추적), Flee(도주) 기능을 제공합니다.
// - [최적화] 여러 멤이 동시에 경로를 탐색하면 CPU 스파이크가 발생할 수 있습니다.
//   이를 방지하기 위해 랜덤 오프셋(Stagger) 타이머를 적용하여 경로 갱신 시점을 분산시켰습니다.
// ============================================================================
using UnityEngine;
using UnityEngine.AI;

namespace MemSystem.Movement
{
    /// <summary>
    /// NavMeshAgent를 이용한 멤의 이동 제어 컴포넌트.
    /// FSM 상태(Idle/Wander/Combat/Flee)에서 이 컴포넌트의 메서드를 호출하여 이동을 제어합니다.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MemMovement : MonoBehaviour
    {
        // =================================================================
        // 설정값
        // =================================================================

        /// <summary>기본 정지 거리(배회/추격 간격 조율용). 시설 진입 시에만 MoveTo에서 좁힙니다.</summary>
        private const float DefaultStoppingDistance = 0.5f;

        /// <summary>
        /// 생산 시설이 설치된 그리드 칸에 부여하는 NavMesh Area 인덱스.
        /// 순찰(배회) 멤의 areaMask에서 이 Area를 제외해 시설 칸을 밟거나 통과하지 못하게 하고,
        /// 배치된 작업 멤만 이 Area를 areaMask에 포함시켜 칸 안으로 들어가 작업하게 합니다.
        /// NavMesh에 구멍(카빙)을 뚫지 않으므로 navmesh는 항상 연결된 상태 → 배회 멤이 갇히지 않습니다.
        /// (TerritoryTestNavMeshBaker가 시설 칸을 이 Area로 굽습니다.)
        /// </summary>
        public const int FacilityNavMeshArea = 3;

        /// <summary>FacilityNavMeshArea의 비트마스크.</summary>
        public static int FacilityAreaMask => 1 << FacilityNavMeshArea;

        [Header("이동 속도 설정")]
        [Tooltip("배회 시 이동 속도")]
        [SerializeField] private float walkSpeed = 1.0f;

        [Tooltip("추적/도주 시 이동 속도 (배회보다 빠름)")]
        [SerializeField] private float runSpeed = 2.5f;

        [Header("배회 설정")]
        [Tooltip("배회 시 현재 위치를 기준으로 목적지를 찾을 반경")]
        [SerializeField] private float wanderRadius = 15f;

        [Header("도주 설정")]
        [Tooltip("위협으로부터 이 거리만큼 멀어지면 도주 성공으로 간주")]
        [SerializeField] private float fleeDistance = 30f;

        [Header("최적화 설정 (경로 갱신 스태거)")]
        [Tooltip("목표를 추적/도주할 때 경로를 갱신하는 기본 주기 (초)")]
        [SerializeField] private float pathUpdateInterval = 0.5f;

        // =================================================================
        // 상태 변수
        // =================================================================

        private NavMeshAgent agent;
        private float pathUpdateTimer;
        private float currentStaggerOffset;

        private Transform chaseTarget;
        private Vector3 fleeSourcePosition;

        private enum MovementMode { None, Wander, Chase, Flee }
        private MovementMode currentMode = MovementMode.None;

        /// <summary>영지 배회 경계. SetWanderBounds()로 설정, ClearWanderBounds()로 해제.</summary>
        private bool hasBounds = false;
        private Bounds wanderBounds;

        public float FleeDistance => fleeDistance;

        /// <summary>NavMeshAgent의 현재 실제 이동 속도 (m/s). 애니메이션 동기화에 사용합니다.</summary>
        public float CurrentSpeed => agent != null && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;

        /// <summary>[디버그] NavMeshAgent가 현재 NavMesh 위에 있는지. 배회가 안 될 때 진단용.</summary>
        public bool IsOnNavMesh => agent != null && agent.isOnNavMesh;

        /// <summary>[디버그] NavMeshAgent 상태 요약 문자열 (테스트/진단용).</summary>
        public string DebugAgentStatus()
        {
            if (agent == null) return "agent=null";
            if (!agent.isOnNavMesh)
                return $"onNavMesh=FALSE ⚠ (pos={transform.position}) — 스폰 위치가 NavMesh 밖입니다";
            return $"onNavMesh=true, pathStatus={agent.pathStatus}, hasPath={agent.hasPath}, " +
                   $"pathPending={agent.pathPending}, remain={agent.remainingDistance:F2}, " +
                   $"vel={agent.velocity.magnitude:F2}, isStopped={agent.isStopped}";
        }

        // =================================================================
        // Unity Lifecycle
        // =================================================================

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            
            // 초기 세팅
            agent.speed = walkSpeed;
            agent.stoppingDistance = DefaultStoppingDistance;
            agent.autoBraking = true;

            // 기본값: 시설 칸(Area)은 밟지 않는다(순찰 멤이 시설을 통과하지 않도록).
            // 배치된 작업 멤만 FacilityWorkState에서 SetFacilityAreaAllowed(true)로 허용.
            agent.areaMask = NavMesh.AllAreas & ~FacilityAreaMask;
        }

        private void OnEnable()
        {
            // 컴포넌트 활성화 시 (풀에서 꺼내질 때 등) 타이머 랜덤화
            RandomizeStaggerOffset();
        }

        private void Update()
        {
            if (currentMode == MovementMode.Chase)
            {
                UpdateChase();
            }
            else if (currentMode == MovementMode.Flee)
            {
                UpdateFlee();
            }
        }

        // =================================================================
        // 이동 제어 API
        // =================================================================

        /// <summary>
        /// 이동을 정지합니다.
        /// </summary>
        public void Stop()
        {
            if (agent == null || !agent.isOnNavMesh) return;

            currentMode = MovementMode.None;
            agent.isStopped = true;
            agent.stoppingDistance = DefaultStoppingDistance; // 시설 접근용으로 좁혔던 값 복구
            agent.ResetPath(); // 진행 중인 경로 초기화
        }

        /// <summary>
        /// 에이전트가 NavMesh 밖(시설 카빙 구멍/여백 등)에 놓였으면 가장 가까운 NavMesh 지점으로
        /// 복귀시킵니다. 배회 중 시설 칸이 카빙돼 갇히는 경우를 자가 복구합니다.
        /// 이미 위에 있거나 복구에 성공하면 true.
        /// </summary>
        public bool TryRecoverToNavMesh(float searchRadius = 3f)
        {
            if (agent == null || !agent.isActiveAndEnabled) return false;
            if (agent.isOnNavMesh) return true;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
                return agent.Warp(hit.position);

            return false;
        }

        /// <summary>
        /// 지정된 위치로 NavMeshAgent를 즉시 이동시킵니다. (스폰/풀링 재사용 시 안전)
        ///
        /// 요청 위치가 NavMesh에서 살짝 벗어나 있으면 가장 가까운 NavMesh 지점으로 보정합니다.
        /// 보정 없이 그냥 Warp하면 실패해도 티가 나지 않고, NavMesh 밖에 놓인 멤은
        /// 이후 모든 이동 명령(ChaseTo/Stop/MoveTo)이 무시되어 제자리에 굳습니다.
        /// </summary>
        /// <param name="position">배치할 위치</param>
        /// <param name="sampleRadius">NavMesh 보정 탐색 반경</param>
        /// <returns>NavMesh 위에 정상적으로 배치되었으면 true</returns>
        public bool Warp(Vector3 position, float sampleRadius = 5f)
        {
            if (agent == null || !agent.isActiveAndEnabled)
            {
                transform.position = position;
                return false;
            }

            // NavMesh 위의 유효 지점으로 보정한 뒤 워프
            Vector3 target = position;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
                target = hit.position;

            if (agent.Warp(target) && agent.isOnNavMesh)
                return true;

            Debug.LogWarning(
                $"[MemMovement] {name} NavMesh 배치 실패 — 요청 위치 {position} 기준 반경 {sampleRadius}m 안에 " +
                $"NavMesh가 없습니다. 스폰 지점/NavMesh 베이크를 확인하세요.", this);
            return false;
        }

        /// <summary>
        /// 랜덤한 위치로 배회를 시작합니다.
        /// SetWanderBounds()로 경계가 설정된 경우, 그 영역 안에서만 목적지를 선택합니다.
        /// </summary>
        public void Wander()
        {
            if (agent == null) return;

            // 시설 카빙 등으로 NavMesh 밖(구멍 안)에 갇혔으면 먼저 복귀를 시도한다.
            // (복귀 못 하면 이번 프레임은 대기 — 다음 호출에서 재시도)
            if (!agent.isOnNavMesh && !TryRecoverToNavMesh()) return;

            currentMode = MovementMode.Wander;
            agent.speed = walkSpeed;
            agent.stoppingDistance = DefaultStoppingDistance;
            agent.isStopped = false;

            // 랜덤 목적지 계산 (경계 설정 여부에 따라 분기)
            Vector3 candidatePos;
            if (hasBounds)
            {
                // 경계 영역 안의 랜덤 점을 골라 NavMesh 위 유효 위치 탐색
                candidatePos = new Vector3(
                    Random.Range(wanderBounds.min.x, wanderBounds.max.x),
                    transform.position.y,
                    Random.Range(wanderBounds.min.z, wanderBounds.max.z)
                );
            }
            else
            {
                // 기존 방식: 현재 위치 기준 wanderRadius 반경
                Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
                candidatePos = randomDirection + transform.position;
            }

            // 이 멤이 갈 수 있는 Area(순찰이면 시설 칸 제외) 안에서만 목적지를 고른다.
            if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, wanderRadius, agent.areaMask))
            {
                agent.SetDestination(hit.position);
            }
            else
            {
                // NavMesh 위가 아니면 현재 위치 유지 (Stuck 방지용 타이머가 다시 처리함)
                agent.SetDestination(transform.position);
            }
        }

        // =================================================================
        // 시설 칸(Area) 통행 제어
        // =================================================================

        /// <summary>
        /// 이 멤이 시설 칸(FacilityNavMeshArea)을 밟을 수 있는지 설정합니다.
        /// 배치된 작업 멤은 true(칸 진입 허용), 순찰 멤은 false(진입 차단).
        /// </summary>
        public void SetFacilityAreaAllowed(bool allowed)
        {
            if (agent == null) return;
            if (allowed) agent.areaMask |= FacilityAreaMask;
            else         agent.areaMask &= ~FacilityAreaMask;
        }

        /// <summary>
        /// 시설 칸 위에 서 있던 작업 멤을, 시설 칸을 제외한 가장 가까운 일반 칸으로 옮겨 세웁니다.
        /// 시설을 떠날 때 시설 칸 진입 권한을 회수하기 전에 호출해, 이후 정상 배회가 가능하게 합니다.
        /// </summary>
        public void WarpToNonFacilityArea(float searchRadius = 3f)
        {
            if (agent == null || !agent.isActiveAndEnabled) return;

            int nonFacility = NavMesh.AllAreas & ~FacilityAreaMask;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, searchRadius, nonFacility))
                agent.Warp(hit.position);
        }

        /// <summary>
        /// 영지 배회 경계를 설정합니다.
        /// 이후 Wander() 호출 시 이 Bounds 안에서만 목적지를 선택합니다.
        /// </summary>
        /// <param name="bounds">배회를 제한할 영역 (Box Collider 등에서 가져오세요)</param>
        public void SetWanderBounds(Bounds bounds)
        {
            hasBounds   = true;
            wanderBounds = bounds;
        }

        /// <summary>
        /// 영지 배회 경계를 해제합니다. 이후 Wander()는 기본 wanderRadius 방식으로 동작합니다.
        /// </summary>
        public void ClearWanderBounds()
        {
            hasBounds = false;
        }

        /// <summary>
        /// 지정한 위치로 걷기 속도로 이동합니다. (일회성 — 지속 추적 아님)
        /// 시설 근처로 걸어가 작업할 때 사용합니다.
        /// </summary>
        /// <param name="destination">이동 목표 지점.</param>
        /// <param name="stopDistance">
        /// 목표에서 이만큼 앞에서 정지합니다. 음수면 기본값(DefaultStoppingDistance)을 사용합니다.
        /// 시설 칸 "안"까지 들어가야 할 때는 0에 가까운 값을 넘기세요.
        /// </param>
        public void MoveTo(Vector3 destination, float stopDistance = -1f)
        {
            if (agent == null || !agent.isOnNavMesh) return;

            currentMode = MovementMode.None; // Update의 지속 재추적 로직이 개입하지 않도록
            agent.speed = walkSpeed;
            agent.stoppingDistance = stopDistance < 0f ? DefaultStoppingDistance : stopDistance;
            agent.isStopped = false;

            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
            else
                agent.SetDestination(destination);
        }

        /// <summary>
        /// 특정 대상을 추적합니다.
        /// </summary>
        /// <param name="target">추적할 대상 (주로 플레이어)</param>
        public void ChaseTo(Transform target)
        {
            if (target == null || agent == null || !agent.isOnNavMesh) return;

            if (currentMode == MovementMode.Chase && chaseTarget == target)
            {
                // 이미 추적 중이라면 상태만 확인하고 리턴
                if (agent.isStopped) agent.isStopped = false;
                if (agent.speed != runSpeed) agent.speed = runSpeed;
                return;
            }

            currentMode = MovementMode.Chase;
            chaseTarget = target;
            agent.speed = runSpeed;
            agent.stoppingDistance = DefaultStoppingDistance;
            agent.isStopped = false;

            RandomizeStaggerOffset();
            UpdateChase(forceUpdate: true);
        }

        /// <summary>
        /// 특정 위치로부터 멀어지는 방향으로 도주합니다.
        /// </summary>
        /// <param name="sourcePos">위협의 위치 (주로 플레이어 위치)</param>
        public void FleeFrom(Vector3 sourcePos)
        {
            if (agent == null || !agent.isOnNavMesh) return;

            // 이미 같은 위치를 기준으로 도주 중이면 무시
            if (currentMode == MovementMode.Flee && fleeSourcePosition == sourcePos) return;

            currentMode = MovementMode.Flee;
            fleeSourcePosition = sourcePos;
            agent.speed = runSpeed;
            agent.stoppingDistance = DefaultStoppingDistance;
            agent.isStopped = false;

            RandomizeStaggerOffset();
            UpdateFlee(forceUpdate: true);
        }

        /// <summary>
        /// 지정한 방향을 즉시 바라보게 합니다. (공격 전 회전용)
        /// </summary>
        public void LookAt(Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            direction.y = 0; // 수평 회전만
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        /// <summary>
        /// 현재 목적지에 도달했는지 확인합니다.
        /// </summary>
        public bool HasReachedDestination()
        {
            if (agent == null || !agent.isOnNavMesh) return false;

            if (!agent.pathPending)
            {
                if (agent.remainingDistance <= agent.stoppingDistance)
                {
                    if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // =================================================================
        // 내부 로직 (추적/도주 경로 갱신)
        // =================================================================

        private void UpdateChase(bool forceUpdate = false)
        {
            if (chaseTarget == null || !agent.isOnNavMesh) return;

            pathUpdateTimer -= Time.deltaTime;
            if (pathUpdateTimer <= 0f || forceUpdate)
            {
                // 플레이어의 좌표가 내비메쉬와 정확히 일치하지 않을 수 있으므로(Pivot 위치 등) 유효한 좌표를 탐색하여 이동
                if (NavMesh.SamplePosition(chaseTarget.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
                else
                {
                    // 탐색 실패 시 일단 원래 좌표 쑤셔넣기 시도
                    agent.SetDestination(chaseTarget.position);
                }
                ResetPathUpdateTimer();
            }
        }

        private void UpdateFlee(bool forceUpdate = false)
        {
            if (!agent.isOnNavMesh) return;

            pathUpdateTimer -= Time.deltaTime;
            if (pathUpdateTimer <= 0f || forceUpdate)
            {
                // 플레이어(위협)와 반대 방향 벡터 계산
                Vector3 fleeDir = (transform.position - fleeSourcePosition).normalized;
                Vector3 fleeTarget = transform.position + fleeDir * fleeDistance;

                // 도주 목적지가 NavMesh 밖일 수 있으므로 SamplePosition으로 보정
                if (NavMesh.SamplePosition(fleeTarget, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
                else
                {
                    // 샘플링 실패 시, 적당히 멀어지는 방향으로 설정 (근사치)
                    agent.SetDestination(transform.position + fleeDir * 5f);
                }
                
                ResetPathUpdateTimer();
            }
        }

        // =================================================================
        // 최적화: 경로 갱신 스태거 (Stagger)
        // =================================================================

        private void RandomizeStaggerOffset()
        {
            // 다수의 개체가 같은 프레임에 경로를 갱신하지 않도록 오프셋 추가
            currentStaggerOffset = Random.Range(0f, 0.2f);
            ResetPathUpdateTimer();
        }

        private void ResetPathUpdateTimer()
        {
            pathUpdateTimer = pathUpdateInterval + currentStaggerOffset;
        }
    }
}
