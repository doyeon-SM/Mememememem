using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// 활성화된 월드 드롭의 원본 프리팹과 자동 반환 시간을 기억하고 <see cref="WorldDropPool"/>로 돌려보냅니다.
/// </summary>
public class PooledWorldDrop : MonoBehaviour
{
    private GameObject prefab;
    private float autoReturnSeconds;
    private float lifeTime;
    private bool isReturned;

    // 풀에서 꺼낸 드롭 오브젝트가 원본 프리팹과 자동 반환 시간을 기억한다.
    /// <summary>풀 키와 자동 반환 시간을 지정해 이번 사용 상태를 초기화합니다.</summary>
    public void Initialize(GameObject sourcePrefab, float returnSeconds)
    {
        prefab = sourcePrefab;
        autoReturnSeconds = returnSeconds;
        lifeTime = 0f;
        isReturned = false;
    }

    /// <summary>아직 반환되지 않았다면 풀로 반환합니다.</summary>
    /// <returns>이번 호출에서 실제 반환됐으면 참입니다.</returns>
    public bool ReturnToPool()
    {
        if (isReturned || prefab == null)
        {
            return false;
        }

        isReturned = true;
        WorldDropPool.Release(prefab, gameObject);
        return true;
    }

    // 설정 시간이 지나면 드롭 오브젝트를 파괴하지 않고 풀로 되돌린다.
    private void Update()
    {
        if (autoReturnSeconds <= 0f)
        {
            return;
        }

        lifeTime += Time.deltaTime;
        if (lifeTime >= autoReturnSeconds)
        {
            ReturnToPool();
        }
    }
}
