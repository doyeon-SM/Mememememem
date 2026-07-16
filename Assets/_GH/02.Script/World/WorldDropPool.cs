using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드롭 프리팹별 Queue를 관리하는 씬 단위 정적 오브젝트 풀입니다.
/// 풀 루트가 씬 전환으로 파괴되면 이전 씬의 캐시도 자동으로 초기화됩니다.
/// </summary>
/// <summary>
/// 드롭 프리팹별 Queue를 관리하는 씬 단위 정적 오브젝트 풀입니다.
/// 풀 루트가 씬 전환으로 파괴되면 이전 씬의 캐시도 자동으로 초기화됩니다.
/// </summary>
public static class WorldDropPool
{
    private static readonly Dictionary<GameObject, Queue<GameObject>> Pools = new Dictionary<GameObject, Queue<GameObject>>();
    private static Transform poolRoot;

    /// <summary>지정 프리팹 인스턴스를 미리 생성해 비활성 풀에 넣습니다.</summary>
    /// <param name="prefab">풀 키로 사용할 월드 드롭 프리팹입니다.</param>
    /// <param name="count">추가로 미리 생성할 개수입니다.</param>
    /// <summary>지정 프리팹 인스턴스를 미리 생성해 비활성 풀에 넣습니다.</summary>
    /// <param name="prefab">풀 키로 사용할 월드 드롭 프리팹입니다.</param>
    /// <param name="count">추가로 미리 생성할 개수입니다.</param>
    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;

        Queue<GameObject> pool = GetPool(prefab);

        for (int i = 0; i < count; i++)
        {
            GameObject instance = CreateInstance(prefab);
            instance.SetActive(false);
            pool.Enqueue(instance);
        }
    }

    /// <summary>풀에서 드롭을 꺼내 지정 위치에 활성화하고 자동 반환을 예약합니다.</summary>
    /// <returns>활성화된 드롭 인스턴스이며 프리팹이 없으면 null입니다.</returns>
    /// <summary>풀에서 드롭을 꺼내 지정 위치에 활성화하고 자동 반환을 예약합니다.</summary>
    /// <returns>활성화된 드롭 인스턴스이며 프리팹이 없으면 null입니다.</returns>
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float autoReturnSeconds)
    {
        if (prefab == null) return null;

        Queue<GameObject> pool = GetPool(prefab);
        GameObject instance = pool.Count > 0 ? pool.Dequeue() : CreateInstance(prefab);

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);

        Rigidbody dropRigidbody = instance.GetComponent<Rigidbody>();
        if (dropRigidbody != null)
        {
            dropRigidbody.linearVelocity = Vector3.zero;
            dropRigidbody.angularVelocity = Vector3.zero;
            dropRigidbody.useGravity = false;
            dropRigidbody.isKinematic = true;
        }

        PooledWorldDrop pooledDrop = instance.GetComponent<PooledWorldDrop>();
        if (pooledDrop == null)
        {
            pooledDrop = instance.AddComponent<PooledWorldDrop>();
        }

        pooledDrop.Initialize(prefab, autoReturnSeconds);

        return instance;
    }

    /// <summary>사용이 끝난 월드 드롭을 비활성화하고 원본 프리팹의 풀로 반환합니다.</summary>
    /// <summary>사용이 끝난 월드 드롭을 비활성화하고 원본 프리팹의 풀로 반환합니다.</summary>
    public static void Release(GameObject prefab, GameObject instance)
    {
        if (prefab == null || instance == null) return;

        instance.SetActive(false);
        instance.transform.SetParent(GetPoolRoot(), false);
        GetPool(prefab).Enqueue(instance);
    }

    private static Queue<GameObject> GetPool(GameObject prefab)
    {
        // 이전 씬의 풀 루트가 파괴되었다면 정적 Queue의 파괴된 참조도 함께 초기화한다.
        GetPoolRoot();

        if (!Pools.TryGetValue(prefab, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            Pools.Add(prefab, pool);
        }

        return pool;
    }

    private static GameObject CreateInstance(GameObject prefab)
    {
        GameObject instance = Object.Instantiate(prefab, GetPoolRoot());
        instance.name = prefab.name;
        return instance;
    }

    private static Transform GetPoolRoot()
    {
        if (poolRoot != null) return poolRoot;

        Pools.Clear();

        GameObject rootObject = new GameObject("World Drop Pool");
        poolRoot = rootObject.transform;

        return poolRoot;
    }
}
