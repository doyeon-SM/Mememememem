// ============================================================================
// MemPool.cs
// Object Pool — Instantiate/Destroy 없이 멤 인스턴스를 재사용
//
// [담당자 안내]
// - 멤은 빈번하게 스폰/디스폰 되므로, Instantiate/Destroy를 반복하면
//   가비지 컬렉션(GC) 스파이크로 인해 렉이 발생할 수 있습니다.
// - 이를 방지하기 위해 Unity 내장 ObjectPool<T>를 사용하여 미리 생성해두고 재사용합니다.
// ============================================================================
using UnityEngine;
using UnityEngine.Pool;
using MemSystem.Core;
using MemSystem.Data;

namespace MemSystem.Spawn
{
    /// <summary>
    /// 멤 인스턴스를 풀링하여 재사용하는 클래스.
    /// 
    /// [역할 분리]
    /// - MemFactory: 생성(Instantiate) 및 초기화(데이터 세팅) 담당
    /// - MemPool: 수명 관리(Get/Release) 담당
    /// - MemSpawner: 언제, 어디서, 어떤 멤을 스폰할지(조건/타이머) 담당
    /// </summary>
    public class MemPool : MonoBehaviour
    {
        // =================================================================
        // 설정값
        // =================================================================

        [Header("필수 참조")]
        [Tooltip("인스턴스 생성을 담당할 팩토리 컴포넌트")]
        [SerializeField] private MemFactory factory;

        [Header("풀 설정")]
        [Tooltip("초기 풀 사이즈 (시작 시점에 미리 생성해둘 개수)")]
        [SerializeField] private int defaultCapacity = 10;

        [Tooltip("최대 풀 사이즈 (이 개수를 초과해서 반환되면 파괴됨)")]
        [SerializeField] private int maxSize = 25;

        // =================================================================
        // 내부 상태
        // =================================================================

        private ObjectPool<Mem> pool;

        /// <summary>현재 월드에 활성화된 멤 수</summary>
        public int ActiveCount => pool != null ? pool.CountActive : 0;

        /// <summary>풀에서 대기 중인 멤 수 (비활성)</summary>
        public int InactiveCount => pool != null ? pool.CountInactive : 0;

        // =================================================================
        // Unity Lifecycle
        // =================================================================

        private void Awake()
        {
            if (factory == null)
            {
                factory = GetComponent<MemFactory>();
                if (factory == null)
                {
                    Debug.LogError("[MemPool] MemFactory 참조가 없습니다!");
                    return;
                }
            }

            // Unity 2021+ 내장 ObjectPool 초기화
            pool = new ObjectPool<Mem>(
                createFunc: OnCreatePoolItem,
                actionOnGet: OnGetFromPool,
                actionOnRelease: OnReturnToPool,
                actionOnDestroy: OnDestroyPoolItem,
                collectionCheck: true,        // 중복 반환 에러 체크
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
            );
        }

        // =================================================================
        // Public API (주로 MemSpawner가 호출)
        // =================================================================

        /// <summary>
        /// 풀에서 멤을 하나 꺼내어 지정된 위치에 스폰합니다.
        /// </summary>
        /// <param name="data">스폰할 멤의 데이터 (MemData)</param>
        /// <param name="position">스폰될 월드 좌표</param>
        /// <returns>스폰된 멤 인스턴스</returns>
        public Mem Spawn(MemData data, Vector3 position)
        {
            if (pool == null)
            {
                Debug.LogError("[MemPool] 풀이 초기화되지 않았습니다!");
                return null;
            }

            // 풀에서 인스턴스 가져오기 (부족하면 createFunc 자동 호출)
            var mem = pool.Get();
            if (mem != null)
            {
                // 팩토리를 통해 데이터 초기화 및 활성화
                factory.InitializeMem(mem, data, position);
            }

            return mem;
        }

        /// <summary>
        /// 사용이 끝난 멤을 풀에 반환합니다.
        /// (포획 성공, 도주 완료, 플레이어 이탈 시)
        /// </summary>
        /// <param name="mem">반환할 멤 인스턴스</param>
        public void Despawn(Mem mem)
        {
            if (pool == null || mem == null) return;

            // 상태 리셋 후 풀 반환 (비활성화 처리)
            mem.ResetForPool();
            pool.Release(mem);

            Debug.Log($"[MemPool] 멤 반환 — Active: {ActiveCount}, Inactive: {InactiveCount}");
        }

        /// <summary>
        /// 풀을 비우고 모든 인스턴스를 파괴합니다.
        /// (지역 이동, 씬 전환 등 완전히 비워야 할 때 호출)
        /// </summary>
        public void ClearPool()
        {
            pool?.Clear();
        }

        // =================================================================
        // ObjectPool 콜백 메서드
        // =================================================================

        /// <summary>풀에 재고가 없을 때 새로 생성하는 로직</summary>
        private Mem OnCreatePoolItem()
        {
            var mem = factory.CreateInstance();
            return mem;
        }

        /// <summary>풀에서 꺼낼 때 실행할 로직 (실제 활성화는 Factory가 함)</summary>
        private void OnGetFromPool(Mem mem)
        {
            // 의도적으로 비워둠 (Factory.InitializeMem에서 SetActive(true) 처리)
        }

        /// <summary>풀에 반환할 때 실행할 로직 (비활성화)</summary>
        private void OnReturnToPool(Mem mem)
        {
            mem.gameObject.SetActive(false);
        }

        /// <summary>풀 최대치(maxSize) 초과로 반환되어 파괴해야 할 때 로직</summary>
        private void OnDestroyPoolItem(Mem mem)
        {
            if (mem != null)
            {
                Destroy(mem.gameObject);
            }
        }
    }
}
