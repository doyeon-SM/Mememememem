using UnityEngine;

/// <summary>
/// 영지 타일 확장 시 영역이 50% 이상 겹치면 자동으로 파괴되는 환경 오브젝트 컴포넌트
/// </summary>
[RequireComponent(typeof(Collider))]
public class TerritoryClearObject : MonoBehaviour
{
    [Header("레이어")]
    [SerializeField] private LayerMask tileLayer;

    [Header("제거 임계값 (0.5 = 50%)")]
    [Range(0f, 1f)]
    [SerializeField] private float destroyThreshold = 0.5f;

    private Collider myCollider;

    private void Awake()
    {
        myCollider = GetComponent<Collider>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (((1 << other.gameObject.layer) & tileLayer) != 0)
        {
            if (IsOverlappedEnoughXZ(other))
            {
                DestroyObject();
            }
        }
    }

    /// <summary>
    /// XZ 평면 기준으로 상대 콜라이더와 겹치는 면적 비율을 계산합니다.
    /// </summary>
    private bool IsOverlappedEnoughXZ(Collider other)
    {
        Bounds myBounds = myCollider.bounds;
        Bounds otherBounds = other.bounds;

        float xOverlap = Mathf.Max(0f, Mathf.Min(myBounds.max.x, otherBounds.max.x) - Mathf.Max(myBounds.min.x, otherBounds.min.x));

        float zOverlap = Mathf.Max(0f, Mathf.Min(myBounds.max.z, otherBounds.max.z) - Mathf.Max(myBounds.min.z, otherBounds.min.z));

        float overlapAreaXZ = xOverlap * zOverlap;

        float myAreaXZ = myBounds.size.x * myBounds.size.z;

        if (myAreaXZ <= 0f) return false;

        float overlapRatio = overlapAreaXZ / myAreaXZ;

        return overlapRatio >= destroyThreshold;
    }

    private void DestroyObject()
    {
        Destroy(gameObject);
    }
}