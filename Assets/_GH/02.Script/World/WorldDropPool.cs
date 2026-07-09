using System.Collections.Generic;
using UnityEngine;

public static class WorldDropPool
{
    private static readonly Dictionary<GameObject, Queue<GameObject>> Pools = new Dictionary<GameObject, Queue<GameObject>>();
    private static Transform poolRoot;

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

    public static void Release(GameObject prefab, GameObject instance)
    {
        if (prefab == null || instance == null) return;

        instance.SetActive(false);
        instance.transform.SetParent(GetPoolRoot(), false);
        GetPool(prefab).Enqueue(instance);
    }

    private static Queue<GameObject> GetPool(GameObject prefab)
    {
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

        GameObject rootObject = new GameObject("World Drop Pool");
        Object.DontDestroyOnLoad(rootObject);
        poolRoot = rootObject.transform;

        return poolRoot;
    }
}
