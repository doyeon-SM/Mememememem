using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// 활성화된 런타임 월드 아이템의 자동 반환 시간을 기억하고 공용 <see cref="WorldDropPool"/>로 돌려보냅니다.
/// </summary>
public class PooledWorldDrop : MonoBehaviour
{
    private float autoReturnSeconds;
    private float lifeTime;
    private bool isReturned;

    /// <summary>자동 반환 시간을 지정하고 이번 사용 상태를 초기화합니다.</summary>
    public void Initialize(float returnSeconds)
    {
        autoReturnSeconds = Mathf.Max(0f, returnSeconds);
        lifeTime = 0f;
        isReturned = false;
    }

    /// <summary>아직 반환되지 않았다면 공용 월드 아이템 풀로 반환합니다.</summary>
    /// <returns>이번 호출에서 실제 반환됐으면 참입니다.</returns>
    public bool ReturnToPool()
    {
        if (isReturned)
        {
            return false;
        }

        isReturned = true;
        WorldDropPool.Release(gameObject);
        return true;
    }

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
