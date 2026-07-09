using KMS;
using UnityEngine;
using UnityEngine.Serialization;

public class WayPointStone : MonoBehaviour, TestInteractable, IInteractable
{
    [Header("WayPoint")]
    [SerializeField] private WayPointDefinition definition;

    [Header("Map UI")]
    [SerializeField] private WayPointMapUI mapUI;

    [Header("Spawn")]
    [Tooltip("If assigned, the player is moved to this transform position.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Used only when Spawn Point is not assigned.")]
    [FormerlySerializedAs("spawnPoistion")]
    [SerializeField] private Vector3 fallbackSpawnPosition;
    [SerializeField] private bool useFallbackSpawnPosition;

    [Header("Visual")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Material lockedMaterial;
    [SerializeField] private Material unlockedMaterial;
    [SerializeField] private GameObject lockedVisual;
    [SerializeField] private GameObject unlockedVisual;

    [Header("State")]
    [SerializeField] private bool isUnlocked;

    [Header("Interaction")]
    [SerializeField] private string interactionPrompt = "웨이포인트 지도 열기";

    public WayPointDefinition Definition => definition;
    public string Id => definition != null ? definition.id : string.Empty;
    public bool IsUnlocked => isUnlocked;
    public string InteractionPrompt => interactionPrompt;

    public Vector3 SpawnPosition
    {
        get
        {
            if (spawnPoint != null)
            {
                return spawnPoint.position;
            }

            if (useFallbackSpawnPosition)
            {
                return fallbackSpawnPosition;
            }

            return transform.position;
        }
    }

    private void Awake()
    {
        RefreshVisual();
    }

    private void OnEnable()
    {
        if (WayPointManager.Instance != null)
        {
            WayPointManager.Instance.RegisterStone(this);
        }
    }

    private void OnDisable()
    {
        if (WayPointManager.Instance != null)
        {
            WayPointManager.Instance.UnregisterStone(this);
        }
    }

    private void OnValidate()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        RefreshVisual();
    }

    // 플레이어가 실제 웨이포인트 스톤과 상호작용하면 이동 가능 모드로 지도 UI를 연다.
    public void Interact()
    {
        WayPointMapUI targetMapUI = ResolveMapUI();
        if (targetMapUI != null)
        {
            targetMapUI.OpenFromStone(definition);
        }
    }

    // KMS 플레이어 상호작용 시스템에서 이 스톤을 사용할 수 있는지 확인한다.
    public bool CanInteract(PlayerInteraction interactor)
    {
        return definition != null;
    }

    // KMS 플레이어 상호작용 시스템에서 호출될 때 기존 지도 열기 로직을 실행한다.
    public void Interact(PlayerInteraction interactor)
    {
        Interact();
    }

    // 매니저가 해금 상태를 반영할 때 스톤 비주얼을 갱신한다.
    public void SetUnlockedVisual(bool unlocked)
    {
        isUnlocked = unlocked;
        RefreshVisual();
    }

    // 이전 코드 호환용으로 활성 상태 설정을 유지한다.
    public void SetActive(bool active)
    {
        SetUnlockedVisual(active);
    }

    // 런타임에서 웨이포인트 정의를 바꿀 때 사용한다.
    public void SetDefinition(WayPointDefinition newDefinition)
    {
        definition = newDefinition;
    }

    // 런타임에서 도착 위치 Transform을 바꿀 때 사용한다.
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

    // 해금 상태에 맞춰 머티리얼과 잠금/해금 오브젝트를 교체한다.
    private void RefreshVisual()
    {
        if (targetRenderer != null)
        {
            Material targetMaterial = isUnlocked ? unlockedMaterial : lockedMaterial;
            if (targetMaterial != null)
            {
                targetRenderer.sharedMaterial = targetMaterial;
            }
        }

        if (lockedVisual != null)
        {
            lockedVisual.SetActive(!isUnlocked);
        }

        if (unlockedVisual != null)
        {
            unlockedVisual.SetActive(isUnlocked);
        }
    }

    // 선택 시 에디터에서 실제 도착 위치를 확인하기 위한 기즈모를 그린다.
    private void OnDrawGizmosSelected()
    {
        Vector3 position = SpawnPosition;

        Gizmos.color = isUnlocked ? Color.cyan : Color.gray;
        Gizmos.DrawWireSphere(position, 0.35f);
        Gizmos.DrawLine(transform.position, position);
    }
}

