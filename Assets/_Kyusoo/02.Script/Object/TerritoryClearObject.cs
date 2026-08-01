using UnityEngine;

/// <summary>
/// 영지 타일 확장 시 영역이 겹치면 자동으로 파괴되는 환경 오브젝트 컴포넌트
/// </summary>
[RequireComponent(typeof(Collider))]
public class TerritoryClearObject : MonoBehaviour
{
    [Header("레이어")]
    [SerializeField] private LayerMask tileLayer;

    private void OnTriggerEnter(Collider other)
    {
        // 1. 특정 레이어만 지정된 경우 레이어 검사
        if (((1 << other.gameObject.layer) & tileLayer) != 0)
        {
            DestroyObject();
        }
    }

    private void DestroyObject()
    {
        // 필요 시 파괴 이펙트나 사운드 재생 가능
        Destroy(gameObject);
    }
}