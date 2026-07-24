// ============================================================================
// MemFactory.cs
// 멤 개체 생성/초기화 팩토리
//
// [담당자 안내]
// - 역할: 멤 인스턴스를 Instantiate하고 데이터를 주입합니다.
// - MemPool과의 역할 분리:
//   · MemFactory = 생성/초기화만 담당
//   · MemPool = 수명 관리(Get/Release)만 담당
//   · MemSpawner = 언제/어디서 Spawn 할지 결정
// - Inspector에서 memPrefab과 tierTable을 반드시 할당해주세요.
// ============================================================================
using UnityEngine;
using MemSystem.Data;

namespace MemSystem.Core
{
    /// <summary>
    /// 멤 개체를 생성하고 초기화하는 팩토리.
    /// 
    /// [호출 흐름]
    /// 1. MemPool.Spawn() → pool.Get() → CreateInstance() 호출
    /// 2. MemPool.Spawn() → InitializeMem() 호출 → 위치/데이터 세팅
    /// 3. 멤이 월드에 활성화됨
    /// </summary>
    public class MemFactory : MonoBehaviour
    {
        // =================================================================
        // Inspector 설정
        // =================================================================

        [Header("필수 참조")]
        [Tooltip("멤 기본 프리팹 — Mem, MemStats, MemAI, MemMovement, NavMeshAgent가 부착된 프리팹")]
        [SerializeField] private GameObject memPrefab;

        [Tooltip("등급별 고정 스펙 테이블 — MemTierTable 에셋을 할당하세요")]
        [SerializeField] private MemTierTable tierTable;

        [Header("설정")]
        [Tooltip("생성된 멤의 부모 Transform — 하이어라키 정리용. 미설정 시 이 오브젝트 하위에 생성됩니다.")]
        [SerializeField] private Transform memParent;

        // =================================================================
        // Public API
        // =================================================================

        /// <summary>
        /// 새로운 멤 인스턴스를 생성합니다 (비활성 상태).
        /// Object Pool의 createFunc 콜백에서 호출됩니다.
        /// </summary>
        /// <returns>비활성 상태의 멤 인스턴스. Pool에서 Get 시 InitializeMem으로 활성화합니다.</returns>
        public Mem CreateInstance()
        {
            var parent = memParent != null ? memParent : transform;
            var go = Instantiate(memPrefab, parent);
            go.SetActive(false); // Pool에서 Get할 때 활성화

            var mem = go.GetComponent<Mem>();
            if (mem == null)
            {
                Debug.LogError("[MemFactory] memPrefab에 Mem 컴포넌트가 없습니다! " +
                               "프리팹에 Mem.cs를 부착해주세요.");
                Destroy(go);
                return null;
            }

            return mem;
        }

        /// <summary>
        /// 멤 인스턴스를 특정 데이터로 초기화하고 활성화합니다.
        /// Pool에서 Get한 후 호출됩니다.
        /// </summary>
        /// <param name="mem">초기화할 멤 인스턴스 (Pool에서 가져온 것)</param>
        /// <param name="data">멤 데이터 에셋 (어떤 멤인지)</param>
        /// <param name="position">스폰 위치 (월드 좌표)</param>
        public void InitializeMem(Mem mem, MemData data, Vector3 position)
        {
            // 1. 활성화 전 임시 위치 세팅 (초기 바인딩용)
            mem.transform.position = position;
            mem.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            // 2. 오브젝트 활성화 (비활성 상태에서 Animator 제어 불가)
            mem.gameObject.SetActive(true);

            // 3. 활성화된 상태에서 안전하게 NavMeshAgent Warp 호출 (이전 경로/보간 찌꺼기 제거)
            //    실패하면 NavMesh 밖에 놓인 채로 스폰된다 → 이동 명령이 전부 무시되므로 반드시 확인한다.
            if (mem.Movement != null && !mem.Movement.Warp(position))
            {
                Debug.LogError(
                    $"[MemFactory] {data.memName} 이 NavMesh 밖에 스폰되었습니다 (위치: {position}). " +
                    $"이 멤은 이동하지 못합니다 — 스폰 지점을 NavMesh 위로 옮기세요.", mem);
            }

            // 데이터 주입 + 초기화 (AI 초기화 → PlayIdle 등 Animator 제어 포함)
            mem.Initialize(data, tierTable);

            Debug.Log($"[MemFactory] {data.memName} 초기화 완료 — 위치: {position}");
        }

        /// <summary>
        /// 스폰 테이블에서 가중치 기반으로 랜덤 멤 데이터를 선택합니다.
        /// 
        /// 가중치(SpawnCondition.spawnWeight)가 높을수록 자주 선택됩니다.
        /// 예: A(weight=3), B(weight=1) → A가 75%, B가 25% 확률로 선택
        /// </summary>
        /// <param name="spawnTable">이 구역에서 스폰 가능한 멤 데이터 배열</param>
        /// <returns>선택된 MemData</returns>
        public MemData SelectRandomMemData(MemData[] spawnTable)
        {
            if (spawnTable == null || spawnTable.Length == 0)
            {
                Debug.LogWarning("[MemFactory] spawnTable이 비어있습니다! " +
                                 "MemSpawner의 spawnTable에 MemData를 추가해주세요.");
                return null;
            }

            // 전체 가중치 합산
            float totalWeight = 0f;
            for (int i = 0; i < spawnTable.Length; i++)
            {
                totalWeight += spawnTable[i].spawnCondition?.spawnWeight ?? 1f;
            }

            // 가중치 기반 랜덤 선택
            float randomValue = Random.Range(0f, totalWeight);
            float cumulativeWeight = 0f;

            for (int i = 0; i < spawnTable.Length; i++)
            {
                cumulativeWeight += spawnTable[i].spawnCondition?.spawnWeight ?? 1f;
                if (randomValue <= cumulativeWeight)
                {
                    return spawnTable[i];
                }
            }

            // fallback (이론상 도달하지 않음)
            return spawnTable[spawnTable.Length - 1];
        }
    }
}
