using UnityEngine;

public class WayPointObject : MonoBehaviour, TestInteractable
{
    [Header("Ref")]
    [SerializeField] private WayPointDefinition targetWayPoint;
    [SerializeField] private GameObject lockedVisual;
    [SerializeField] private GameObject activeVisual;

    private bool isActiveObj;
    private bool subscribed;

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
        if (targetWayPoint == null || WayPointManager.Instance == null)
        {
            return;
        }

        if (isActiveObj)
        {
            return;
        }

        bool unlocked = WayPointManager.Instance.Unlock(targetWayPoint.id);
        if (unlocked)
        {
            SetActiveVisual(true);
        }
    }

    // 매니저에 저장된 현재 해금 상태를 오브젝트 비주얼에 반영한다.
    private void RefreshStateFromManager()
    {
        bool unlocked = targetWayPoint != null
            && WayPointManager.Instance != null
            && WayPointManager.Instance.IsUnlocked(targetWayPoint.id);

        SetActiveVisual(unlocked);
    }

    // 같은 웨이포인트 상태가 바뀌면 잠금/활성 비주얼을 교체한다.
    private void HandleWayPointStateChanged(WayPointRunTime state)
    {
        if (state == null || targetWayPoint == null || state.Definition != targetWayPoint)
        {
            return;
        }

        SetActiveVisual(state.IsActive);
    }

    // 잠금 오브젝트와 활성 오브젝트를 현재 상태에 맞춰 켜고 끈다.
    private void SetActiveVisual(bool active)
    {
        isActiveObj = active;

        if (lockedVisual != null)
        {
            lockedVisual.SetActive(!active);
        }

        if (activeVisual != null)
        {
            activeVisual.SetActive(active);
        }
    }
}
