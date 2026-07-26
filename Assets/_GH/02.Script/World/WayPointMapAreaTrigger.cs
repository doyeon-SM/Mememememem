using UnityEngine;

/// <summary>
/// 같은 씬 안에 지도 지역이 여러 개 있을 때 플레이어가 해당 지역에 들어왔음을 기록합니다.
/// 최초 진입 후부터 해당 지도 버튼이 지도 UI에 표시됩니다.
/// </summary>
[AddComponentMenu("GH/World/WayPoint Map Area Trigger")]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class WayPointMapAreaTrigger : MonoBehaviour
{
    [Header("Map Area")]
    [Tooltip("이 트리거가 나타내는 지도 지역입니다.")]
    [SerializeField] private WayPointMapDefinition mapDefinition;

    [Header("Player Filter")]
    [SerializeField] private string playerTag = PlayerReferenceResolver.DefaultPlayerTag;
    [SerializeField] private string playerLayerName = PlayerReferenceResolver.DefaultPlayerLayerName;

    [Header("Debug")]
    [SerializeField] private bool logFirstVisit = true;

    private void Reset()
    {
        ConfigureTriggerCollider();
    }

    private void OnValidate()
    {
        ConfigureTriggerCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null
            || !PlayerReferenceResolver.IsInPlayerHierarchy(
                other.gameObject,
                playerTag,
                playerLayerName))
        {
            return;
        }

        if (mapDefinition == null)
        {
            Debug.LogWarning("[WayPointMapAreaTrigger] Map Definition이 지정되지 않았습니다.", this);
            return;
        }

        if (WayPointManager.Instance == null)
        {
            Debug.LogWarning("[WayPointMapAreaTrigger] WayPointManager가 없습니다.", this);
            return;
        }

        bool firstVisit = WayPointManager.Instance.RegisterMapVisit(mapDefinition);
        if (firstVisit && logFirstVisit)
        {
            string mapName = string.IsNullOrWhiteSpace(mapDefinition.displayName)
                ? mapDefinition.name
                : mapDefinition.displayName;
            Debug.Log($"[WayPointMapAreaTrigger] 지도 최초 방문: {mapName}", this);
        }
    }

    private void ConfigureTriggerCollider()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }
}
