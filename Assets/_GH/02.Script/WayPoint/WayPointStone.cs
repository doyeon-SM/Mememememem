using KMS;
using System.Collections;
using System.Collections.Generic;
using KMS.Audio;
using TMPro;
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

    [Header("Unlocked Visual")]
    [Tooltip("웨이포인트가 등록된 동안에만 표시할 하위 오브젝트입니다.")]
    [SerializeField] private GameObject unlockedVisualObject;

    [Header("Area Entry Notification")]
    [Tooltip("활성화하면 플레이어가 스톤 주변 범위에 진입할 때 웨이포인트 이름을 표시합니다.")]
    [SerializeField] private bool enableAreaNotification = true;
    [Tooltip(
        "실행 중 생성되는 AreaNotificationTrigger 오브젝트에 적용할 레이어 이름입니다. " +
        "이 레이어는 플레이어 Interaction/Harvest 마스크에서 제외해야 합니다.")]
    [SerializeField] private string areaNotificationLayerName = "Area";
    [Tooltip("웨이포인트 스톤 위치를 기준으로 한 감지 범위 중심 오프셋입니다.")]
    [SerializeField] private Vector3 areaNotificationCenter;
    [Tooltip("감지 박스의 로컬 전체 크기입니다. 0 이하인 축은 기존 Radius의 지름을 사용합니다.")]
    [SerializeField] private Vector3 areaNotificationSize;
    // 이전 씬/프리팹에 저장된 루트 Trigger 참조를 읽어 비활성화하기 위한 마이그레이션 필드입니다.
    [SerializeField, HideInInspector] private BoxCollider areaNotificationBoxTrigger;
    // 기존 씬과 프리팹 오버라이드의 직렬화 경로를 보존하기 위해 이름을 유지한다.
    [SerializeField, HideInInspector] private SphereCollider areaNotificationTrigger;
    [SerializeField, HideInInspector] private float areaNotificationRadius = 30f;
    [Tooltip("웨이포인트 이름을 표시할 전용 TMP 텍스트입니다.")]
    [SerializeField] private TMP_Text areaNotificationText;
    [Tooltip("함께 표시하거나 숨길 UI 루트입니다. 비워 두면 TMP Text 오브젝트만 사용합니다.")]
    [SerializeField] private GameObject areaNotificationRoot;
    [Tooltip("웨이포인트 이름을 표시할 시간입니다.")]
    [Min(0.1f)]
    [SerializeField] private float areaNotificationDuration = 3f;
    [Tooltip("활성화하면 이미 등록된 웨이포인트에 진입했을 때만 이름을 표시합니다.")]
    [SerializeField] private bool notifyOnlyWhenUnlocked;
    [Tooltip("플레이어 판별에 사용할 태그입니다.")]
    [SerializeField] private string areaNotificationPlayerTag = PlayerReferenceResolver.DefaultPlayerTag;
    [Tooltip("플레이어 판별에 사용할 레이어 이름입니다.")]
    [SerializeField] private string areaNotificationPlayerLayerName = PlayerReferenceResolver.DefaultPlayerLayerName;

    [Header("Area Entry Gizmo")]
    [SerializeField] private bool showAreaNotificationGizmo = true;
    [SerializeField] private bool showAreaNotificationGizmoOnlyWhenSelected = true;
    [SerializeField] private Color areaNotificationGizmoColor = new Color(0.2f, 0.8f, 1f, 0.35f);

    [Header("Interaction")]
    [SerializeField] private string interactionPrompt = "웨이포인트 지도 열기";

    private static readonly Dictionary<TMP_Text, int> ActiveNotificationTokens = new();
    private static int nextNotificationToken;

    private int overlappingPlayerColliderCount;
    private int currentNotificationToken;
    private Coroutine hideNotificationCoroutine;
    private GameObject runtimeAreaNotificationTriggerObject;
    private BoxCollider runtimeAreaNotificationBoxTrigger;

    public WayPointDefinition Definition => definition;
    public string Id => definition != null ? definition.id : string.Empty;
    public bool IsUnlocked => isUnlocked;
    public string InteractionPrompt => interactionPrompt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetAreaNotificationState()
    {
        ActiveNotificationTokens.Clear();
        nextNotificationToken = 0;
    }

    private void Awake()
    {
        ConfigureAreaNotificationTrigger(true);

        // 매니저가 아직 생성되지 않은 실행 순서에서도 초기 표시가 잠깐 노출되지 않게 한다.
        isUnlocked = definition != null && definition.IsUnlockedOnInitialize;
        RefreshUnlockedVisual();

        if (areaNotificationText != null
            && !ActiveNotificationTokens.ContainsKey(areaNotificationText))
        {
            SetAreaNotificationVisible(false);
        }
    }

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
        ConfigureAreaNotificationTrigger(true);
        if (runtimeAreaNotificationTriggerObject != null)
        {
            runtimeAreaNotificationTriggerObject.SetActive(enableAreaNotification);
        }

        if (WayPointManager.Instance != null)
        {
            WayPointManager.Instance.RegisterStone(this);
        }
    }

    private void OnDestroy()
    {
        ReleaseAreaNotification();
        DestroyRuntimeAreaNotificationTrigger();

        if (WayPointManager.Instance != null)
        {
            WayPointManager.Instance.UnregisterStone(this);
        }
    }

    private void OnDisable()
    {
        if (runtimeAreaNotificationTriggerObject != null)
        {
            runtimeAreaNotificationTriggerObject.SetActive(false);
        }

        overlappingPlayerColliderCount = 0;
        ReleaseAreaNotification();
    }

    private void OnTriggerEnter(Collider other)
    {
        NotifyAreaNotificationTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        NotifyAreaNotificationTriggerExit(other);
    }

    internal void NotifyAreaNotificationTriggerEnter(Collider other)
    {
        if (!enableAreaNotification
            || other == null
            || !PlayerReferenceResolver.IsInPlayerHierarchy(
                other.gameObject,
                areaNotificationPlayerTag,
                areaNotificationPlayerLayerName))
        {
            return;
        }

        overlappingPlayerColliderCount++;
        if (overlappingPlayerColliderCount == 1)
        {
            ShowAreaNotification();
        }
    }

    internal void NotifyAreaNotificationTriggerExit(Collider other)
    {
        if (other == null
            || !PlayerReferenceResolver.IsInPlayerHierarchy(
                other.gameObject,
                areaNotificationPlayerTag,
                areaNotificationPlayerLayerName))
        {
            return;
        }

        overlappingPlayerColliderCount = Mathf.Max(0, overlappingPlayerColliderCount - 1);
    }

    internal void NotifyAreaNotificationTriggerDisabled()
    {
        overlappingPlayerColliderCount = 0;
    }

    /// <summary>웨이포인트 정의가 연결된 스톤만 상호작용할 수 있습니다.</summary>
    public bool CanInteract(PlayerInteraction interactor)
    {
        return definition != null;
    }

    /// <summary>KMS 상호작용 요청을 받아 웨이포인트 이동 모드로 지도 UI를 엽니다.</summary>
    public void Interact(PlayerInteraction interactor)
    {
        if (WayPointManager.Instance != null)
        {
            WayPointManager.Instance.OpenMapFromStone(definition);
            return;
        }

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
        RefreshUnlockedVisual();
    }

    // 이전 코드 호환용으로 활성 상태 설정을 유지한다.
    public void SetActive(bool active)
    {
        SetUnlockedState(active);
    }

    /// <summary>웨이포인트가 처음 등록됐을 때 진입 알림과 같은 UI를 표시합니다.</summary>
    public void ShowUnlockedAreaNotification()
    {
        ShowAreaNotification();
    }

    // 런타임에서 웨이포인트 정의를 바꿀 때 사용한다.
    /// <summary>런타임에 이 스톤이 나타낼 웨이포인트 정의를 교체합니다.</summary>
    public void SetDefinition(WayPointDefinition newDefinition)
    {
        definition = newDefinition;

        bool unlocked = definition != null
            && WayPointManager.Instance != null
            && WayPointManager.Instance.IsUnlocked(definition.id);
        SetUnlockedState(unlocked);
    }

    // 등록 여부에 따라 인스펙터에서 지정한 하위 오브젝트만 표시하거나 숨긴다.
    private void RefreshUnlockedVisual()
    {
        if (unlockedVisualObject != null && unlockedVisualObject.activeSelf != isUnlocked)
        {
            unlockedVisualObject.SetActive(isUnlocked);
        }
    }

    private void ShowAreaNotification()
    {
        if (definition == null
            || areaNotificationText == null
            || (notifyOnlyWhenUnlocked && !isUnlocked))
        {
            return;
        }

        string waypointName = string.IsNullOrWhiteSpace(definition.displayName)
            ? (string.IsNullOrWhiteSpace(definition.id) ? definition.name : definition.id)
            : definition.displayName;

        if (hideNotificationCoroutine != null)
        {
            StopCoroutine(hideNotificationCoroutine);
        }

        currentNotificationToken = ++nextNotificationToken;
        ActiveNotificationTokens[areaNotificationText] = currentNotificationToken;
        areaNotificationText.text = waypointName;
        SetAreaNotificationVisible(true);

        KMSAudioService.Play2D(GameSfxId.WaySound);

        hideNotificationCoroutine = StartCoroutine(
            HideAreaNotificationAfterDelay(currentNotificationToken));
    }

    private IEnumerator HideAreaNotificationAfterDelay(int token)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, areaNotificationDuration));
        hideNotificationCoroutine = null;

        if (areaNotificationText == null
            || !ActiveNotificationTokens.TryGetValue(areaNotificationText, out int activeToken)
            || activeToken != token)
        {
            yield break;
        }

        ActiveNotificationTokens.Remove(areaNotificationText);
        SetAreaNotificationVisible(false);
        currentNotificationToken = 0;
    }

    private void ReleaseAreaNotification()
    {
        if (hideNotificationCoroutine != null)
        {
            StopCoroutine(hideNotificationCoroutine);
            hideNotificationCoroutine = null;
        }

        if (areaNotificationText != null
            && currentNotificationToken != 0
            && ActiveNotificationTokens.TryGetValue(areaNotificationText, out int activeToken)
            && activeToken == currentNotificationToken)
        {
            ActiveNotificationTokens.Remove(areaNotificationText);
            SetAreaNotificationVisible(false);
        }

        currentNotificationToken = 0;
    }

    private void SetAreaNotificationVisible(bool visible)
    {
        GameObject target = areaNotificationRoot != null
            ? areaNotificationRoot
            : areaNotificationText != null
                ? areaNotificationText.gameObject
                : null;

        if (target != null && target.activeSelf != visible)
        {
            target.SetActive(visible);
        }
    }

    private void ConfigureAreaNotificationTrigger(bool createIfMissing)
    {
        // 이전 버전이 WayPointStone 루트에 만든 Trigger는 상호작용 Raycast에 잡히므로 사용하지 않는다.
        if (areaNotificationBoxTrigger != null
            && areaNotificationBoxTrigger != runtimeAreaNotificationBoxTrigger)
        {
            areaNotificationBoxTrigger.enabled = false;
        }

        BoxCollider[] legacyRootColliders = GetComponents<BoxCollider>();
        for (int i = 0; i < legacyRootColliders.Length; i++)
        {
            BoxCollider legacyCollider = legacyRootColliders[i];
            if (legacyCollider != null && legacyCollider.isTrigger)
            {
                legacyCollider.enabled = false;
            }
        }

        // 이전 버전에서 사용하던 Sphere Trigger가 함께 동작하면 진입 이벤트가
        // 중복으로 발생하므로 마이그레이션 이후에는 사용하지 않는다.
        if (createIfMissing && areaNotificationTrigger != null)
        {
            areaNotificationTrigger.enabled = false;
        }

        if (!createIfMissing || !Application.isPlaying)
        {
            return;
        }

        if (runtimeAreaNotificationTriggerObject == null)
        {
            CreateRuntimeAreaNotificationTrigger();
        }

        if (runtimeAreaNotificationBoxTrigger == null)
        {
            return;
        }

        runtimeAreaNotificationBoxTrigger.isTrigger = true;
        runtimeAreaNotificationBoxTrigger.center = areaNotificationCenter;
        runtimeAreaNotificationBoxTrigger.size = GetAreaNotificationSize();
        runtimeAreaNotificationBoxTrigger.enabled = enableAreaNotification;
        areaNotificationBoxTrigger = runtimeAreaNotificationBoxTrigger;
    }

    private void CreateRuntimeAreaNotificationTrigger()
    {
        Transform areaParent = transform.parent;
        GameObject triggerObject = new GameObject("AreaNotificationTrigger");
        triggerObject.SetActive(false);

        if (areaParent != null)
        {
            triggerObject.transform.SetParent(areaParent, false);
            // 계층은 청크 부모 아래에 두되 위치는 WayPointStone과 동일하게 맞춘다.
            triggerObject.transform.localPosition = transform.localPosition;
            triggerObject.transform.localRotation = Quaternion.identity;
            triggerObject.transform.localScale = Vector3.one;
        }
        else
        {
            triggerObject.transform.position = transform.position;
            triggerObject.transform.rotation = Quaternion.identity;
        }

        int areaLayer = LayerMask.NameToLayer(areaNotificationLayerName);
        if (areaLayer >= 0)
        {
            triggerObject.layer = areaLayer;
        }
        else
        {
            Debug.LogWarning(
                $"[WayPointStone] '{areaNotificationLayerName}' 레이어가 없어 " +
                "AreaNotificationTrigger가 Default 레이어를 사용합니다.",
                this);
        }

        runtimeAreaNotificationBoxTrigger = triggerObject.AddComponent<BoxCollider>();
        runtimeAreaNotificationBoxTrigger.isTrigger = true;
        runtimeAreaNotificationBoxTrigger.center = areaNotificationCenter;
        runtimeAreaNotificationBoxTrigger.size = GetAreaNotificationSize();

        WayPointAreaNotificationTrigger relay =
            triggerObject.AddComponent<WayPointAreaNotificationTrigger>();
        relay.Initialize(this);

        runtimeAreaNotificationTriggerObject = triggerObject;
        areaNotificationBoxTrigger = runtimeAreaNotificationBoxTrigger;
        triggerObject.SetActive(enableAreaNotification && isActiveAndEnabled);
    }

    private void DestroyRuntimeAreaNotificationTrigger()
    {
        if (runtimeAreaNotificationTriggerObject != null)
        {
            Destroy(runtimeAreaNotificationTriggerObject);
        }

        runtimeAreaNotificationTriggerObject = null;
        runtimeAreaNotificationBoxTrigger = null;
        areaNotificationBoxTrigger = null;
    }

    // 0인 축은 기존 Radius의 지름을 사용해 씬/프리팹별 오버라이드 크기를 보존한다.
    private Vector3 GetAreaNotificationSize()
    {
        float legacyDiameter = Mathf.Max(0.1f, areaNotificationRadius) * 2f;
        return new Vector3(
            areaNotificationSize.x > 0f ? Mathf.Max(0.1f, areaNotificationSize.x) : legacyDiameter,
            areaNotificationSize.y > 0f ? Mathf.Max(0.1f, areaNotificationSize.y) : legacyDiameter,
            areaNotificationSize.z > 0f ? Mathf.Max(0.1f, areaNotificationSize.z) : legacyDiameter);
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

        if (showAreaNotificationGizmo && !showAreaNotificationGizmoOnlyWhenSelected)
        {
            DrawAreaNotificationGizmo();
        }
    }

    // 기즈모를 선택 시에만 표시하도록 설정한 경우 여기서 그린다.
    private void OnDrawGizmosSelected()
    {
        if (showSpawnGizmo && showSpawnGizmoOnlyWhenSelected)
        {
            DrawSpawnGizmo();
        }

        if (showAreaNotificationGizmo && showAreaNotificationGizmoOnlyWhenSelected)
        {
            DrawAreaNotificationGizmo();
        }
    }

    private void OnValidate()
    {
        areaNotificationDuration = Mathf.Max(0.1f, areaNotificationDuration);
    }

    private void DrawAreaNotificationGizmo()
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Transform areaParent = transform.parent;
        Gizmos.matrix = areaParent != null
            ? areaParent.localToWorldMatrix * Matrix4x4.Translate(transform.localPosition)
            : transform.localToWorldMatrix;
        Gizmos.color = areaNotificationGizmoColor;
        Gizmos.DrawWireCube(areaNotificationCenter, GetAreaNotificationSize());

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
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

/// <summary>
/// WayPointStone과 분리된 런타임 Area Trigger의 진입/이탈 이벤트만 소유 스톤에 전달합니다.
/// IInteractable을 구현하지 않으므로 지도 상호작용 대상으로 선택되지 않습니다.
/// </summary>
[DisallowMultipleComponent]
internal sealed class WayPointAreaNotificationTrigger : MonoBehaviour
{
    private WayPointStone owner;

    public void Initialize(WayPointStone newOwner)
    {
        owner = newOwner;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null)
        {
            owner.NotifyAreaNotificationTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (owner != null)
        {
            owner.NotifyAreaNotificationTriggerExit(other);
        }
    }

    private void OnDisable()
    {
        if (owner != null)
        {
            owner.NotifyAreaNotificationTriggerDisabled();
        }
    }
}

