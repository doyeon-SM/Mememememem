using KMS;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 웨이포인트 이동 지점과 도착 위치를 제공하고 상호작용 시 이동용 지도를 여는 씬 오브젝트입니다.
/// 활성화될 때 <see cref="WayPointManager"/>에 자동 등록됩니다.
/// </summary>
public class WayPointStone : MonoBehaviour, IInteractable
{
    [Header("WayPoint")]
    [SerializeField] private WayPointDefinition definition;

    [Header("Map UI")]
    [SerializeField] private WayPointMapUI mapUI;

    [Header("Spawn")]
    [Tooltip("If assigned, the player is moved to this transform position.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Spawn Point를 기준으로 플레이어 도착 위치를 얼마나 이동할지 나타내는 로컬 오프셋입니다. Spawn Point가 없으면 WayPointStone을 기준으로 사용합니다.")]
    [FormerlySerializedAs("fallbackSpawnPosition")]
    [FormerlySerializedAs("spawnPoistion")]
    [SerializeField] private Vector3 spawnOffset;
    [Tooltip("활성화하면 Spawn Point 또는 WayPointStone 위치에 Spawn Offset을 더합니다.")]
    [FormerlySerializedAs("useFallbackSpawnPosition")]
    [SerializeField] private bool useSpawnOffset;

    [Header("Spawn Gizmo")]
    [SerializeField] private bool showSpawnGizmo = true;
    [Tooltip("활성화하면 이 웨이포인트를 선택했을 때만 스폰 기즈모를 표시합니다.")]
    [SerializeField] private bool showSpawnGizmoOnlyWhenSelected;
    [SerializeField] private Color spawnGizmoColor = new Color(0f, 0.9f, 1f, 0.9f);
    [Min(0.1f)]
    [SerializeField] private float spawnGizmoHeight = 1.8f;
    [Min(0.05f)]
    [SerializeField] private float spawnGizmoRadius = 0.35f;

    [Header("State")]
    [SerializeField] private bool isUnlocked;

    [Header("Interaction")]
    [SerializeField] private string interactionPrompt = "웨이포인트 지도 열기";

    public WayPointDefinition Definition => definition;
    public string Id => definition != null ? definition.id : string.Empty;
    public bool IsUnlocked => isUnlocked;
    public string InteractionPrompt => interactionPrompt;

    /// <summary>이 웨이포인트로 이동했을 때 플레이어를 배치할 월드 좌표입니다.</summary>
    public Vector3 SpawnPosition
    {
        get
        {
            Transform origin = spawnPoint != null ? spawnPoint : transform;
            if (!useSpawnOffset)
            {
                return origin.position;
            }

            // 좌표값은 Spawn Point의 로컬 축을 따르되, 부모 Scale 때문에 거리가
            // 늘어나지 않도록 회전만 반영한 월드 단위 오프셋으로 계산한다.
            return origin.position + origin.rotation * spawnOffset;
        }
    }

    private void OnEnable()
    {
        if (WayPointManager.Instance != null)
        {
            WayPointManager.Instance.RegisterStone(this);
        }
    }

    private void OnDestroy()
    {
        if (WayPointManager.Instance != null)
        {
            WayPointManager.Instance.UnregisterStone(this);
        }
    }

    /// <summary>웨이포인트 정의가 연결된 스톤만 상호작용할 수 있습니다.</summary>
    public bool CanInteract(PlayerInteraction interactor)
    {
        return definition != null;
    }

    /// <summary>KMS 상호작용 요청을 받아 웨이포인트 이동 모드로 지도 UI를 엽니다.</summary>
    public void Interact(PlayerInteraction interactor)
    {
        WayPointMapUI targetMapUI = ResolveMapUI();
        if (targetMapUI != null)
        {
            targetMapUI.OpenFromStone(definition);
        }
    }

    // 매니저가 해금 상태를 반영할 때 내부 상태만 갱신한다.
    /// <summary>매니저가 계산한 해금 상태를 씬 스톤에 반영합니다.</summary>
    public void SetUnlockedState(bool unlocked)
    {
        isUnlocked = unlocked;
    }

    // 이전 코드 호환용으로 활성 상태 설정을 유지한다.
    public void SetActive(bool active)
    {
        SetUnlockedState(active);
    }

    // 런타임에서 웨이포인트 정의를 바꿀 때 사용한다.
    /// <summary>런타임에 이 스톤이 나타낼 웨이포인트 정의를 교체합니다.</summary>
    public void SetDefinition(WayPointDefinition newDefinition)
    {
        definition = newDefinition;
    }

    // 런타임에서 도착 위치 Transform을 바꿀 때 사용한다.
    /// <summary>런타임에 플레이어 도착 위치를 교체합니다.</summary>
    public void SetSpawnPoint(Transform newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
    }

    // Inspector 연결이 없을 때 씬에서 지도 UI를 찾아온다.
    private WayPointMapUI ResolveMapUI()
    {
        if (mapUI != null)
        {
            return mapUI;
        }

        if (WayPointMapUI.Instance != null)
        {
            mapUI = WayPointMapUI.Instance;
            return mapUI;
        }

        mapUI = FindFirstObjectByType<WayPointMapUI>(FindObjectsInactive.Include);
        return mapUI;
    }

    // 선택하지 않은 상태에서도 실제 도착 위치를 확인할 수 있도록 기즈모를 그린다.
    private void OnDrawGizmos()
    {
        if (showSpawnGizmo && !showSpawnGizmoOnlyWhenSelected)
        {
            DrawSpawnGizmo();
        }
    }

    // 기즈모를 선택 시에만 표시하도록 설정한 경우 여기서 그린다.
    private void OnDrawGizmosSelected()
    {
        if (showSpawnGizmo && showSpawnGizmoOnlyWhenSelected)
        {
            DrawSpawnGizmo();
        }
    }

    private void DrawSpawnGizmo()
    {
        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Vector3 originPosition = origin.position;
        Vector3 position = SpawnPosition;
        float radius = Mathf.Max(0.05f, spawnGizmoRadius);
        float height = Mathf.Max(radius * 2f, spawnGizmoHeight);
        Vector3 bottomCenter = position + Vector3.up * radius;
        Vector3 topCenter = position + Vector3.up * (height - radius);
        float crossSize = radius * 1.4f;

        Gizmos.color = spawnGizmoColor;

        // 실제로 플레이어 Transform이 배치되는 정확한 좌표를 표시한다.
        Gizmos.DrawSphere(position, Mathf.Min(0.1f, radius * 0.3f));
        Gizmos.DrawLine(position - Vector3.right * crossSize, position + Vector3.right * crossSize);
        Gizmos.DrawLine(position - Vector3.forward * crossSize, position + Vector3.forward * crossSize);

        // Spawn Point 기준점과 오프셋이 적용된 최종 위치 사이를 표시한다.
        Gizmos.DrawWireSphere(originPosition, Mathf.Min(0.15f, radius * 0.4f));
        Gizmos.DrawLine(originPosition, position);

        // 플레이어가 서 있을 공간을 캡슐 형태로 표시한다.
        Gizmos.DrawWireSphere(bottomCenter, radius);
        Gizmos.DrawWireSphere(topCenter, radius);
        Gizmos.DrawLine(bottomCenter + Vector3.right * radius, topCenter + Vector3.right * radius);
        Gizmos.DrawLine(bottomCenter - Vector3.right * radius, topCenter - Vector3.right * radius);
        Gizmos.DrawLine(bottomCenter + Vector3.forward * radius, topCenter + Vector3.forward * radius);
        Gizmos.DrawLine(bottomCenter - Vector3.forward * radius, topCenter - Vector3.forward * radius);

#if UNITY_EDITOR
        string waypointName = definition != null && !string.IsNullOrWhiteSpace(definition.id)
            ? definition.id
            : name;
        string offsetLabel = useSpawnOffset ? $"\nOffset: {spawnOffset}" : string.Empty;
        UnityEditor.Handles.color = spawnGizmoColor;
        UnityEditor.Handles.Label(
            position + Vector3.up * (height + 0.25f),
            $"Player Spawn: {waypointName}{offsetLabel}");
#endif
    }
}

