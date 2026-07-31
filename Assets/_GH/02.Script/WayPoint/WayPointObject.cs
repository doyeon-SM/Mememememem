using KMS;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 플레이어 상호작용으로 연결된 웨이포인트를 최초 해금하는 등록 오브젝트입니다.
/// 해금 상태의 원본은 <see cref="WayPointManager"/>이며 이 컴포넌트는 상호작용 가능 상태만 반영합니다.
/// </summary>
public class WayPointObject : MonoBehaviour, IInteractable
{
    [Header("Ref")]
    [SerializeField] private WayPointDefinition targetWayPoint;

    [Header("Registered Material")]
    [Tooltip("등록 상태 머티리얼을 적용할 렌더러입니다. 비워 두면 현재 오브젝트와 하위 오브젝트에서 자동으로 찾습니다.")]
    [SerializeField] private Renderer targetRenderer;
    [Tooltip("연결된 웨이포인트가 등록된 상태일 때 적용할 머티리얼입니다.")]
    [SerializeField] private Material unlockedMaterial;

    [Header("Interaction")]
    [SerializeField] private string interactionPrompt = "웨이포인트 등록";

    private bool isActiveObj;
    private bool subscribed;
    private bool hasCachedLockedMaterial;
    private Material lockedMaterial;

    public string InteractionPrompt => interactionPrompt;

    private void Awake()
    {
        CacheLockedMaterial();
        isActiveObj = targetWayPoint != null && targetWayPoint.IsUnlockedOnInitialize;
        RefreshRegisteredMaterial();
    }

    private void Start()
    {
        TrySubscribe();
        RefreshStateFromManager();
    }

    private void OnEnable()
    {
        TrySubscribe();
        RefreshStateFromManager();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    // WayPointManager의 상태 변경 이벤트를 구독한다.
    private void TrySubscribe()
    {
        if (subscribed || WayPointManager.Instance == null)
        {
            return;
        }

        WayPointManager.Instance.OnWayPointStateChanged += HandleWayPointStateChanged;
        subscribed = true;
    }

    // 오브젝트가 꺼질 때 이벤트 구독을 해제한다.
    private void Unsubscribe()
    {
        if (!subscribed || WayPointManager.Instance == null)
        {
            subscribed = false;
            return;
        }

        WayPointManager.Instance.OnWayPointStateChanged -= HandleWayPointStateChanged;
        subscribed = false;
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    /// <summary>현재 해금 상태와 UI 입력 상태를 기준으로 등록 가능 여부를 반환합니다.</summary>
    public bool CanInteract(PlayerInteraction interactor)
    {
        return CanRegisterWayPoint();
    }

    /// <summary>KMS 상호작용 요청을 받아 연결된 웨이포인트를 해금합니다.</summary>
    public void Interact(PlayerInteraction interactor)
    {
        if (!CanRegisterWayPoint())
        {
            return;
        }

        WayPointManager.Instance.Unlock(targetWayPoint.id);
    }

    // 지도 UI나 다른 UI를 클릭하는 중에는 플레이어 입력이 등록 오브젝트로 전달되지 않게 막는다.
    private bool CanRegisterWayPoint()
    {
        if (targetWayPoint == null || WayPointManager.Instance == null || isActiveObj)
        {
            return false;
        }

        if (!WayPointManager.Instance.CanUnlockByInteraction(targetWayPoint))
        {
            return false;
        }

        if (WayPointMapUI.Instance != null && WayPointMapUI.Instance.IsVisible)
        {
            return false;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return false;
        }

        return true;
    }

    // 매니저에 저장된 현재 해금 상태를 상호작용 가능 여부에 반영한다.
    private void RefreshStateFromManager()
    {
        if (targetWayPoint == null)
        {
            isActiveObj = false;
        }
        else if (WayPointManager.Instance != null)
        {
            isActiveObj = WayPointManager.Instance.IsUnlocked(targetWayPoint.id);
        }

        RefreshRegisteredMaterial();
    }

    // 같은 웨이포인트 상태가 바뀌면 상호작용 상태만 갱신한다.
    // 지도 UI는 WayPointManager의 동일 이벤트를 구독해 별도로 갱신한다.
    private void HandleWayPointStateChanged(WayPointRunTime state)
    {
        if (state == null || targetWayPoint == null || state.Definition != targetWayPoint)
        {
            return;
        }

        isActiveObj = state.IsActive;
        RefreshRegisteredMaterial();
    }

    // 잠금 상태의 원래 머티리얼을 보관해 세이브 데이터에서 미등록 상태가 복원될 때 되돌린다.
    private void CacheLockedMaterial()
    {
        if (hasCachedLockedMaterial)
        {
            return;
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>(true);
        }

        if (targetRenderer == null)
        {
            return;
        }

        lockedMaterial = targetRenderer.sharedMaterial;
        hasCachedLockedMaterial = true;
    }

    // 공유 머티리얼을 사용해 오브젝트마다 불필요한 런타임 머티리얼 인스턴스가 생성되지 않게 한다.
    private void RefreshRegisteredMaterial()
    {
        CacheLockedMaterial();
        if (!hasCachedLockedMaterial)
        {
            return;
        }

        Material targetMaterial = isActiveObj && unlockedMaterial != null
            ? unlockedMaterial
            : lockedMaterial;

        if (targetRenderer.sharedMaterial != targetMaterial)
        {
            targetRenderer.sharedMaterial = targetMaterial;
        }
    }
}

