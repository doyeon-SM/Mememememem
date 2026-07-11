using KMS;
using UnityEngine;
using UnityEngine.EventSystems;

public class WayPointObject : MonoBehaviour, TestInteractable, IInteractable
{
    [Header("Ref")]
    [SerializeField] private WayPointDefinition targetWayPoint;
    [SerializeField] private GameObject lockedVisual;
    [SerializeField] private GameObject activeVisual;
    [SerializeField] private bool instantiatePrefabVisualReferences = true;

    [Header("Interaction")]
    [SerializeField] private string interactionPrompt = "웨이포인트 등록";

    private bool isActiveObj;
    private bool subscribed;

    public string InteractionPrompt => interactionPrompt;

    private void Awake()
    {
        bool unlockedOnStart = targetWayPoint != null && targetWayPoint.IsUnlockedOnInitialize;
        SetActiveVisual(unlockedOnStart);
    }

    private void Start()
    {
        ResolveVisualReferences();
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

        bool unlocked = WayPointManager.Instance.Unlock(targetWayPoint.id);
        if (unlocked)
        {
            SetActiveVisual(true);
        }
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
        ResolveVisualReferences();
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

    // 인스펙터에 프리팹 에셋을 넣은 경우 실제 씬 자식 오브젝트로 생성해서 SetActive가 동작하게 한다.
    private void ResolveVisualReferences()
    {
        if (!instantiatePrefabVisualReferences)
        {
            return;
        }

        lockedVisual = ResolveVisualReference(lockedVisual, "LockedVisual");
        activeVisual = ResolveVisualReference(activeVisual, "ActiveVisual");
    }

    // 씬 오브젝트가 아닌 프리팹 참조라면 자식으로 인스턴스화한다.
    private GameObject ResolveVisualReference(GameObject visual, string instanceName)
    {
        if (visual == null || visual.scene.IsValid())
        {
            return visual;
        }

        GameObject instance = Instantiate(visual, transform);
        instance.name = instanceName;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        return instance;
    }
}

