using GH.Loading;
using UnityEngine;

/// <summary>
/// 플레이어가 트리거 콜라이더에 진입하면 지정한 지도의 씬을
/// <see cref="LoadingManager"/>를 통해 로드합니다.
/// </summary>
[AddComponentMenu("GH/World/Map Transition Trigger")]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class MapTransitionTrigger : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("이동할 지도입니다. 이 에셋의 Scene Name을 LoadingManager에 전달합니다.")]
    [SerializeField] private WayPointMapDefinition targetMap;
    [Tooltip("새 씬에서 플레이어가 도착할 웨이포인트입니다. 비워 두면 LoadingManager의 기본 배치를 사용합니다.")]
    [SerializeField] private WayPointDefinition arrivalWayPoint;
    [Tooltip("활성화하면 대상 지도의 Required Completed Maps 조건을 만족해야 이동합니다.")]
    [SerializeField] private bool requireTargetMapAvailable = true;

    [Header("Player Filter")]
    [SerializeField] private string playerTag = PlayerReferenceResolver.DefaultPlayerTag;
    [SerializeField] private string playerLayerName = PlayerReferenceResolver.DefaultPlayerLayerName;

    [Header("Debug")]
    [SerializeField] private bool logTransition = true;

    private bool isLoading;

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
        if (isLoading || other == null)
        {
            return;
        }

        if (!PlayerReferenceResolver.IsInPlayerHierarchy(
                other.gameObject,
                playerTag,
                playerLayerName))
        {
            return;
        }

        TryLoadTargetMap();
    }

    /// <summary>
    /// 현재 설정을 검증하고 LoadingManager를 통해 대상 지도 씬 로딩을 요청합니다.
    /// </summary>
    /// <returns>로딩 요청이 정상적으로 시작되었으면 참입니다.</returns>
    public bool TryLoadTargetMap()
    {
        if (isLoading)
        {
            return false;
        }

        if (!TryGetTargetSceneName(out string targetSceneName))
        {
            return false;
        }

        WayPointManager wayPointManager = WayPointManager.Instance;
        if (requireTargetMapAvailable)
        {
            if (wayPointManager == null)
            {
                Debug.LogWarning(
                    "[MapTransitionTrigger] 지도 해금 조건을 확인할 WayPointManager가 없습니다.",
                    this);
                return false;
            }

            if (!wayPointManager.IsMapAvailable(targetMap))
            {
                Debug.LogWarning(
                    $"[MapTransitionTrigger] 아직 이동할 수 없는 지도입니다: {targetMap.displayName}",
                    this);
                return false;
            }
        }

        if (!TryGetArrivalWayPointId(wayPointManager, out string arrivalWayPointId))
        {
            return false;
        }

        if (LoadingManager.Instance == null)
        {
            Debug.LogError(
                "[MapTransitionTrigger] LoadingManager가 없어 지도 이동을 시작할 수 없습니다.",
                this);
            return false;
        }

        isLoading = true;
        bool started = LoadingManager.Instance.LoadScene(targetSceneName, arrivalWayPointId);
        if (!started)
        {
            isLoading = false;
            Debug.LogWarning(
                $"[MapTransitionTrigger] LoadingManager가 씬 로딩 요청을 시작하지 못했습니다: {targetSceneName}",
                this);
            return false;
        }

        // 실제 씬 이동이 시작된 경우에만 대상 지도를 최초 방문한 것으로 기록한다.
        if (wayPointManager != null)
        {
            wayPointManager.RegisterMapVisit(targetMap);
        }

        if (logTransition)
        {
            string arrivalMessage = string.IsNullOrWhiteSpace(arrivalWayPointId)
                ? "기본 도착 위치"
                : $"웨이포인트 '{arrivalWayPointId}'";
            Debug.Log(
                $"[MapTransitionTrigger] 지도 이동 시작: {targetMap.displayName} / {targetSceneName} / {arrivalMessage}",
                this);
        }

        return true;
    }

    private bool TryGetTargetSceneName(out string targetSceneName)
    {
        targetSceneName = targetMap != null ? targetMap.sceneName?.Trim() : string.Empty;

        if (targetMap == null)
        {
            Debug.LogError(
                "[MapTransitionTrigger] Target Map이 지정되지 않았습니다.",
                this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogError(
                $"[MapTransitionTrigger] 대상 지도 '{targetMap.name}'의 Scene Name이 비어 있습니다.",
                targetMap);
            return false;
        }

        return true;
    }

    private bool TryGetArrivalWayPointId(
        WayPointManager wayPointManager,
        out string arrivalWayPointId)
    {
        arrivalWayPointId = string.Empty;
        if (arrivalWayPoint == null)
        {
            return true;
        }

        if (arrivalWayPoint.mapDefinition != targetMap)
        {
            Debug.LogError(
                $"[MapTransitionTrigger] Arrival WayPoint '{arrivalWayPoint.name}'가 Target Map에 속하지 않습니다.",
                this);
            return false;
        }

        arrivalWayPointId = arrivalWayPoint.id?.Trim();
        if (string.IsNullOrWhiteSpace(arrivalWayPointId))
        {
            Debug.LogError(
                $"[MapTransitionTrigger] Arrival WayPoint '{arrivalWayPoint.name}'의 ID가 비어 있습니다.",
                arrivalWayPoint);
            return false;
        }

        if (wayPointManager != null && wayPointManager.GetState(arrivalWayPointId) == null)
        {
            Debug.LogError(
                $"[MapTransitionTrigger] WayPointManager에 Arrival WayPoint ID가 등록되지 않았습니다: {arrivalWayPointId}",
                arrivalWayPoint);
            return false;
        }

        return true;
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
