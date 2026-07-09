using UnityEngine;

[DisallowMultipleComponent]
public class PooledWorldDrop : MonoBehaviour
{
    private GameObject prefab;
    private float autoReturnSeconds;
    private float lifeTime;

    // 풀에서 꺼낸 드랍 오브젝트가 원본 프리팹과 자동 반환 시간을 기억한다.
    public void Initialize(GameObject sourcePrefab, float returnSeconds)
    {
        prefab = sourcePrefab;
        autoReturnSeconds = returnSeconds;
        lifeTime = 0f;
    }

    // 설정 시간이 지나면 드랍 오브젝트를 파괴하지 않고 풀로 되돌린다.
    private void Update()
    {
        if (autoReturnSeconds <= 0f)
        {
            return;
        }

        lifeTime += Time.deltaTime;
        if (lifeTime >= autoReturnSeconds)
        {
            WorldDropPool.Release(prefab, gameObject);
        }
    }
}
