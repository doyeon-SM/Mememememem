// ============================================================================
// MemSpawner.cs
// 스폰/디스폰 관리자 — 기획 규칙에 따른 멤 출현 제어
//
// [담당자 안내]
// 기획서 [1. 스폰 규칙] 구현부입니다.
// - 플레이어가 구역 반경(spawnRadius) 내에 진입 후 10~30초 체류 시 스폰 트리거
// - 스폰 쿨타임(60~120초) 적용
// - 플레이어가 구역을 1분 이상 이탈 시 해당 구역 멤 전원 디스폰(자동 회수)
// - 지역당 최대 20마리 제한
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using MemSystem.Core;
using MemSystem.Data;
using MemSystem.Events;
using MemSystem.Interface;

namespace MemSystem.Spawn
{
    /// <summary>
    /// 특정 구역의 멤 스폰 로직을 담당하는 컴포넌트.
    /// 구역(Zone)마다 하나씩 배치되어 해당 구역의 멤 개체수를 관리합니다.
    /// </summary>
    public class MemSpawner : MonoBehaviour
    {
        // =================================================================
        // 설정값
        // =================================================================

        [Header("필수 참조")]
        [SerializeField] private MemPool memPool;
        [SerializeField] private MemFactory memFactory;

        [Header("스폰 대상 (이 구역에 나올 멤들)")]
        [Tooltip("이 스파우너가 생성할 수 있는 멤 데이터 목록. 가중치 기반으로 랜덤 선택됩니다.")]
        [SerializeField] private MemData[] spawnTable;

        [Header("임시 스폰 위치 (월드 연동 전)")]
        [Tooltip("월드 시스템이 IMemSpawnPointProvider를 주입하기 전, 에디터에서 수동으로 지정한 스폰 위치들")]
        [SerializeField] private Transform[] waypoints;

        [Header("스폰 규칙")]
        [Tooltip("플레이어를 감지할 구역 반경")]
        [SerializeField] private float spawnRadius = 200f;
        
        [Tooltip("이 구역에서 동시에 활성화될 수 있는 멤의 최대 수 (기획 기준 20)")]
        [SerializeField] private int maxActiveCount = 20;

        [Tooltip("플레이어가 구역 진입 후 스폰이 시작되기까지 필요한 최소 체류 시간 (초)")]
        [SerializeField] private float minStayTime = 10f;
        
        [Tooltip("플레이어가 구역 진입 후 스폰이 시작되기까지 필요한 최대 체류 시간 (초)")]
        [SerializeField] private float maxStayTime = 30f;

        [Tooltip("한 번에 스폰할 마리 수 (배치 스폰)")]
        [SerializeField] private int spawnBatchSize = 3;
        
        [Tooltip("마리 당 스폰 간격 (초). 동시에 우르르 나오는 것을 방지합니다.")]
        [SerializeField] private float spawnInterval = 0.5f;

        [Header("디스폰 규칙")]
        [Tooltip("플레이어가 구역을 이탈한 후 멤들이 자동 반환될 때까지의 시간 (초)")]
        [SerializeField] private float despawnDelay = 60f;

        [Header("쿨타임")]
        [Tooltip("모든 멤이 사라졌을 때 재스폰까지 걸리는 최소 쿨타임 (초)")]
        [SerializeField] private float cooldownMin = 60f;
        
        [Tooltip("모든 멤이 사라졌을 때 재스폰까지 걸리는 최대 쿨타임 (초)")]
        [SerializeField] private float cooldownMax = 120f;

        // =================================================================
        // 내부 상태
        // =================================================================

        private IMemSpawnPointProvider spawnPointProvider;
        
        [Header("플레이어 참조")]
        [Tooltip("플레이어 Transform (미할당 시 태그로 자동 탐색)")]
        [SerializeField] private Transform playerTransform;
        
        // 현재 이 스파우너가 관리하는 활성 멤 목록
        private List<Mem> activeMems = new List<Mem>();

        private float playerStayTimer;
        private float stayTriggerDuration; // 이번 턴에 요구되는 체류 시간 (랜덤 결정됨)
        private float despawnTimer;
        
        private float spawnCooldownTimer;
        private float spawnIntervalTimer;
        private int pendingSpawnCount;

        private bool isPlayerInZone;
        private bool isSpawnTriggered;
        private bool isInCooldown;

        // =================================================================
        // Unity Lifecycle
        // =================================================================

        private void Start()
        {
            // 플레이어 자동 탐색
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;

            // 첫 번째 스폰에 필요한 체류 시간 결정
            stayTriggerDuration = Random.Range(minStayTime, maxStayTime);

            // 이벤트 구독 (멤이 포획되거나 도망가면 목록에서 제거하기 위함)
            MemEvents.OnMemCaptured += HandleMemCaptured;
            MemEvents.OnMemFled += HandleMemRemoved;
            MemEvents.OnMemDespawned += HandleMemRemoved;
        }

        private void OnDestroy()
        {
            MemEvents.OnMemCaptured -= HandleMemCaptured;
            MemEvents.OnMemFled -= HandleMemRemoved;
            MemEvents.OnMemDespawned -= HandleMemRemoved;
        }

        private void Update()
        {
            if (playerTransform == null) return;

            UpdatePlayerPresence();

            if (isInCooldown)
            {
                UpdateCooldown();
            }
            else if (isPlayerInZone)
            {
                UpdateSpawnLogic();
                UpdatePendingSpawns(); // 배치 스폰 처리
            }
            else
            {
                UpdateDespawnLogic();
            }
        }

        // =================================================================
        // 외부 연동 API
        // =================================================================

        /// <summary>
        /// 월드 담당자가 스폰 위치 정보를 제공하기 위해 호출합니다.
        /// 이 값이 세팅되면 에디터에 할당된 waypoints 대신 이 Provider를 사용합니다.
        /// </summary>
        public void SetSpawnPointProvider(IMemSpawnPointProvider provider)
        {
            spawnPointProvider = provider;
        }

        // =================================================================
        // 로직 업데이트
        // =================================================================

        /// <summary>
        /// 플레이어가 구역(반경) 안에 있는지 판단하고 타이머를 갱신합니다.
        /// </summary>
        private void UpdatePlayerPresence()
        {
            // 월드 Provider가 있으면 그것을 우선 사용
            if (spawnPointProvider != null)
            {
                bool wasInZone = isPlayerInZone;
                isPlayerInZone = spawnPointProvider.IsPlayerInZone("CurrentZone_ID_TODO"); // TODO: ZoneID 처리 필요
                
                if (isPlayerInZone)
                {
                    playerStayTimer = spawnPointProvider.GetPlayerStayDuration("CurrentZone_ID_TODO");
                    despawnTimer = 0f;
                }
                else if (wasInZone)
                {
                    despawnTimer = 0f; // 방금 이탈했으면 디스폰 타이머 시작
                }
                return;
            }

            // 프로토타입용 Fallback: 가장 가까운 웨이포인트 기준으로 반경 체크
            float minDistance = float.MaxValue;
            if (waypoints != null)
            {
                for (int i = 0; i < waypoints.Length; i++)
                {
                    if (waypoints[i] == null) continue;
                    float dist = Vector3.Distance(playerTransform.position, waypoints[i].position);
                    if (dist < minDistance) minDistance = dist;
                }
            }

            bool prevInZone = isPlayerInZone;
            isPlayerInZone = minDistance <= spawnRadius;

            // 구역 진입/이탈 시 타이머 초기화
            if (isPlayerInZone && !prevInZone)
            {
                playerStayTimer = 0f;
                despawnTimer = 0f;
            }
            else if (!isPlayerInZone && prevInZone)
            {
                despawnTimer = 0f;
            }

            if (isPlayerInZone)
            {
                playerStayTimer += Time.deltaTime;
            }
        }

        /// <summary>
        /// 스폰 조건(체류 시간) 달성 시 스폰을 트리거합니다.
        /// </summary>
        private void UpdateSpawnLogic()
        {
            if (isSpawnTriggered) return;
            if (activeMems.Count >= maxActiveCount) return;

            // 정해진 체류 시간(10~30초) 달성
            if (playerStayTimer >= stayTriggerDuration)
            {
                isSpawnTriggered = true;
                
                // 최대 한도 내에서 스폰할 개수 결정
                int spawnCount = Mathf.Min(spawnBatchSize, maxActiveCount - activeMems.Count);
                pendingSpawnCount = spawnCount;
                spawnIntervalTimer = 0f;

                Debug.Log($"[MemSpawner] 스폰 트리거! {spawnCount}마리 스폰 예정 (체류: {playerStayTimer:F1}초)");
            }
        }

        /// <summary>
        /// 한 번에 여러 마리를 스폰할 때 간격(Interval)을 두고 하나씩 생성합니다.
        /// </summary>
        private void UpdatePendingSpawns()
        {
            if (pendingSpawnCount <= 0) return;

            spawnIntervalTimer -= Time.deltaTime;
            if (spawnIntervalTimer <= 0f)
            {
                SpawnOneMem();
                
                pendingSpawnCount--;
                spawnIntervalTimer = spawnInterval;

                // 이번 배치 배치가 끝났다면 다시 다음 트리거를 기다릴 준비
                if (pendingSpawnCount <= 0 && activeMems.Count < maxActiveCount)
                {
                    isSpawnTriggered = false;
                    playerStayTimer = 0f;
                    stayTriggerDuration = Random.Range(minStayTime, maxStayTime);
                }
            }
        }

        /// <summary>
        /// 실질적인 단일 객체 스폰 로직
        /// </summary>
        private void SpawnOneMem()
        {
            if (memPool == null || memFactory == null) return;

            // 1. Factory를 이용해 가중치 기반 랜덤 데이터 선택
            MemData data = memFactory.SelectRandomMemData(spawnTable);
            if (data == null) return;

            // 2. 랜덤 위치 탐색 (NavMesh 위)
            Vector3 spawnPos = GetRandomSpawnPosition();

            // 3. Pool에서 꺼내기 (Factory 초기화 자동 진행됨)
            Mem mem = memPool.Spawn(data, spawnPos);
            if (mem != null)
            {
                activeMems.Add(mem);
                MemEvents.OnMemSpawned?.Invoke(mem);
            }
        }

        /// <summary>
        /// 플레이어가 구역을 비운 지 1분이 지나면 모든 멤을 자동 디스폰(회수)합니다.
        /// </summary>
        private void UpdateDespawnLogic()
        {
            if (activeMems.Count == 0) return;

            despawnTimer += Time.deltaTime;
            if (despawnTimer >= despawnDelay)
            {
                Debug.Log($"[MemSpawner] 플레이어 장기 부재({despawnDelay}초) — 활성 멤 {activeMems.Count}마리 일괄 디스폰");
                
                // 뒤에서부터 지워야 안전함
                for (int i = activeMems.Count - 1; i >= 0; i--)
                {
                    var mem = activeMems[i];
                    if (mem != null)
                    {
                        mem.OnDespawn(); // 이벤트 발행 등 내부 처리
                        memPool.Despawn(mem);
                    }
                }
                activeMems.Clear();
                
                // 구역이 비워졌으므로 쿨타임 돌입
                StartCooldown();
            }
        }

        private void StartCooldown()
        {
            isInCooldown = true;
            isSpawnTriggered = false;
            spawnCooldownTimer = Random.Range(cooldownMin, cooldownMax);
            Debug.Log($"[MemSpawner] 재스폰 쿨타임 시작: {spawnCooldownTimer:F0}초");
        }

        private void UpdateCooldown()
        {
            spawnCooldownTimer -= Time.deltaTime;
            if (spawnCooldownTimer <= 0f)
            {
                isInCooldown = false;
                playerStayTimer = 0f;
                stayTriggerDuration = Random.Range(minStayTime, maxStayTime);
                Debug.Log("[MemSpawner] 쿨타임 종료 — 플레이어 진입 대기");
            }
        }

        // =================================================================
        // 이벤트 핸들러 (개별 멤 소멸 시 목록에서 제외)
        // =================================================================

        private void HandleMemCaptured(Mem mem, MemSnapshot snap) => RemoveActiveMem(mem);
        private void HandleMemRemoved(Mem mem) => RemoveActiveMem(mem);

        private void RemoveActiveMem(Mem mem)
        {
            if (activeMems.Remove(mem))
            {
                memPool.Despawn(mem);
                
                // 만약 이 구역의 모든 멤이 사라졌다면(포획/도주 등으로) 쿨타임 시작
                if (activeMems.Count == 0 && !isInCooldown)
                {
                    StartCooldown();
                }
            }
        }

        // =================================================================
        // 위치 탐색 유틸
        // =================================================================

        private Vector3 GetRandomSpawnPosition()
        {
            Vector3 basePos = transform.position;

            // Provider가 있으면 해당 위치 사용 (우선)
            if (spawnPointProvider != null)
            {
                Vector3[] pts = spawnPointProvider.GetSpawnPoints("CurrentZone_ID_TODO");
                if (pts != null && pts.Length > 0)
                {
                    basePos = pts[Random.Range(0, pts.Length)];
                }
            }
            // 없으면 에디터 설정 Waypoint 사용
            else if (waypoints != null && waypoints.Length > 0)
            {
                var wp = waypoints[Random.Range(0, waypoints.Length)];
                if (wp != null) basePos = wp.position;
            }

            // 중심점(basePos) 근방 반경 내에서 NavMesh 위 유효 위치 탐색
            for (int i = 0; i < 10; i++)
            {
                Vector2 randCircle = Random.insideUnitCircle * (spawnRadius * 0.5f);
                Vector3 candidate = basePos + new Vector3(randCircle.x, 0, randCircle.y);

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }

            return basePos; // 10번 실패 시 걍 베이스 위치 반환
        }

        // =================================================================
        // 에디터 시각화 (Gizmos)
        // =================================================================
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (waypoints != null)
            {
                Gizmos.color = new Color(0, 1, 0, 0.2f);
                foreach (var wp in waypoints)
                {
                    if (wp != null)
                        Gizmos.DrawWireSphere(wp.position, spawnRadius);
                }
            }

            Gizmos.color = Color.yellow;
            foreach (var mem in activeMems)
            {
                if (mem != null)
                    Gizmos.DrawWireCube(mem.transform.position + Vector3.up, Vector3.one * 0.5f);
            }
        }
#endif
    }
}
