using KMS;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 플레이어 상호작용으로 연결된 웨이포인트를 최초 해금하는 등록 오브젝트입니다.
/// 해금 상태의 원본은 <see cref="WayPointManager"/>이며 이 컴포넌트는 상호작용 가능 상태만 반영합니다.
/// </summary>
public class WayPointObject : MonoBehaviour, TestInteractable, IInteractable
{
    [Header("Ref")]
    [SerializeField] private WayPointDefinition targetWayPoint;

    [Header("Interaction")]
    [SerializeField] private string interactionPrompt = "웨이포인트 등록";

    private bool isActiveObj;
    private bool subscribed;

    public string InteractionPrompt => interactionPrompt;

    private void Awake()
    {
        isActiveObj = targetWayPoint != null && targetWayPoint.IsUnlockedOnInitialize;
    }

    private void Start()
    {
        TrySubscribe();
        RefreshStateFromManager();
    }

    private void OnEnable()
    {
        TrySubscribe();
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

    // 플레이어가 오브젝트와 상호작용하면 연결된 웨이포인트를 해금한다.
    public void Interact()
    {
        if (!CanRegisterWayPoint())
        {
            return;
        }

        WayPointManager.Instance.Unlock(targetWayPoint.id);
    }

    // KMS 플레이어 상호작용 시스템에서 이 오브젝트를 사용할 수 있는지 확인한다.
    public bool CanInteract(PlayerInteraction interactor)
    {
        return CanRegisterWayPoint();
    }

    // KMS 플레이어 상호작용 시스템에서 호출될 때 기존 웨이포인트 해금 로직을 실행한다.
    public void Interact(PlayerInteraction interactor)
    {
        Interact();
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
        isActiveObj = targetWayPoint != null
            && WayPointManager.Instance != null
            && WayPointManager.Instance.IsUnlocked(targetWayPoint.id);
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
    }
}

