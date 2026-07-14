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

        public float FleeDistance => fleeDistance;

        /// <summary>NavMeshAgent의 현재 실제 이동 속도 (m/s). 애니메이션 동기화에 사용합니다.</summary>
        public float CurrentSpeed => agent != null && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;

        // =================================================================
        // Unity Lifecycle
        // =================================================================

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            
            // 초기 세팅
            agent.speed = walkSpeed;
            agent.stoppingDistance = 0.5f;
            agent.autoBraking = true;
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
            agent.ResetPath(); // 진행 중인 경로 초기화
        }

        /// <summary>
        /// 지정된 위치로 NavMeshAgent를 즉시 이동시킵니다. (스폰/풀링 재사용 시 안전)
        /// </summary>
        public void Warp(Vector3 position)
        {
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.Warp(position);
            }
            else
            {
                transform.position = position;
            }
        }

        /// <summary>
        /// 랜덤한 위치로 배회를 시작합니다.
        /// </summary>
        public void Wander()
        {
            if (agent == null || !agent.isOnNavMesh) return;

            currentMode = MovementMode.Wander;
            agent.speed = walkSpeed;
            agent.isStopped = false;

            // 랜덤 목적지 계산
            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
            randomDirection += transform.position;
            
            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
            else
            {
                // NavMesh 위가 아니면 현재 위치 유지 (Stuck 방지용 타이머가 다시 처리함)
                agent.SetDestination(transform.position);
            }
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
