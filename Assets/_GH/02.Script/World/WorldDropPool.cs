using System.Collections.Generic;
using HDY.Item;
using UnityEngine;

/// <summary>
/// 아이템별 프리팹 없이 itemId를 주입해 사용하는 씬 단위 공용 월드 아이템 풀입니다.
/// 런타임 오브젝트에 필요한 Collider, Rigidbody, WorldItem, PooledWorldDrop을 자동 구성합니다.
/// </summary>
public static class WorldDropPool
{
    private const string PoolRootName = "World Drop Pool";
    private const string PooledObjectName = "Runtime World Item";

    private static readonly Queue<GameObject> Pool = new Queue<GameObject>();
    private static Transform poolRoot;

    /// <summary>
    /// 공용 풀에 최소 count개의 비활성 월드 아이템이 있도록 준비합니다.
    /// 여러 WorldObject가 호출해도 지정 수량을 초과해서 계속 생성하지 않습니다.
    /// </summary>
    public static void Prewarm(int count)
    {
        if (count <= 0)
        {
            return;
        }

        GetPoolRoot();
        while (Pool.Count < count)
        {
            GameObject instance = CreateRuntimeWorldItem();
            instance.SetActive(false);
            Pool.Enqueue(instance);
        }
    }

    /// <summary>
    /// itemId와 수량을 기반으로 공용 월드 아이템을 생성하거나 풀에서 재사용합니다.
    /// </summary>
    public static GameObject Spawn(
        string itemId,
        int amount,
        Vector3 position,
        Quaternion rotation,
        float autoReturnSeconds)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
        {
            Debug.LogWarning("[WorldDropPool] itemId가 비어 있거나 수량이 0 이하라 월드 아이템을 생성하지 않았습니다.");
            return null;
        }

        string normalizedItemId = itemId.Trim();
        ItemCatalogManager catalogManager = ItemCatalogManager.Instance;
        if (catalogManager == null)
        {
            catalogManager = Object.FindFirstObjectByType<ItemCatalogManager>();
        }

        if (catalogManager != null
            && catalogManager.ItemDataList.Count > 0
            && catalogManager.FindItemData(normalizedItemId) == null)
        {
            Debug.LogWarning($"[WorldDropPool] ItemCatalogManager에 '{normalizedItemId}' ID가 없어 생성하지 않았습니다.");
            return null;
        }

        GetPoolRoot();
        GameObject instance = Pool.Count > 0
            ? Pool.Dequeue()
            : CreateRuntimeWorldItem();

        instance.name = $"World Item [{normalizedItemId}]";
        instance.transform.SetParent(poolRoot, false);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = Vector3.one;

        WorldItem worldItem = instance.GetComponent<WorldItem>();
        worldItem.Configure(normalizedItemId, amount);

        Rigidbody dropRigidbody = instance.GetComponent<Rigidbody>();
        dropRigidbody.linearVelocity = Vector3.zero;
        dropRigidbody.angularVelocity = Vector3.zero;
        dropRigidbody.useGravity = false;
        dropRigidbody.isKinematic = true;

        PooledWorldDrop pooledDrop = instance.GetComponent<PooledWorldDrop>();
        pooledDrop.Initialize(autoReturnSeconds);

        instance.SetActive(true);
        return instance;
    }

    /// <summary>수량 1개짜리 월드 아이템을 생성하는 편의 오버로드입니다.</summary>
    public static GameObject Spawn(
        string itemId,
        Vector3 position,
        Quaternion rotation,
        float autoReturnSeconds)
    {
        return Spawn(itemId, 1, position, rotation, autoReturnSeconds);
    }

    /// <summary>사용이 끝난 런타임 월드 아이템을 공용 풀로 반환합니다.</summary>
    public static void Release(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        instance.SetActive(false);
        instance.name = PooledObjectName;
        instance.transform.SetParent(GetPoolRoot(), false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        Pool.Enqueue(instance);
    }

    private static GameObject CreateRuntimeWorldItem()
    {
        GameObject instance = new GameObject(PooledObjectName);
        instance.SetActive(false);
        instance.transform.SetParent(GetPoolRoot(), false);

        BoxCollider pickupCollider = instance.AddComponent<BoxCollider>();
        pickupCollider.isTrigger = true;
        pickupCollider.center = new Vector3(0f, 0.4f, 0f);
        pickupCollider.size = new Vector3(0.9f, 0.9f, 0.9f);

        Rigidbody dropRigidbody = instance.AddComponent<Rigidbody>();
        dropRigidbody.useGravity = false;
        dropRigidbody.isKinematic = true;
        dropRigidbody.interpolation = RigidbodyInterpolation.None;
        dropRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

        instance.AddComponent<WorldItem>();
        instance.AddComponent<PooledWorldDrop>();
        return instance;
    }

    private static Transform GetPoolRoot()
    {
        if (poolRoot != null)
        {
            return poolRoot;
        }

        Pool.Clear();

        GameObject rootObject = new GameObject(PoolRootName);
        poolRoot = rootObject.transform;
        return poolRoot;
    }
}
