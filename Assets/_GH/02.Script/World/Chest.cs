using KGH.Data;
using KMS;
using KMS.InventoryDuped;
using System;
using System.Collections.Generic;
using UnityEngine;


public class Chest : MonoBehaviour, KMS.IInteractable
{
    [Header("Setting")]
    [Tooltip("정보 UI에 표시할 이름입니다. 비워 두면 GameObject 이름을 사용합니다.")]
    [SerializeField] private string displayName;
    [SerializeField] private string interactionPrompt = "상자 열기";
    [SerializeField] private string chestId;
    [Tooltip("현재는 다중 드랍으로 구조 작성")][SerializeField] private ChestItem[] dropItem;
    [Tooltip("False일 경우 0번 인덱스만 드랍")][SerializeField] private bool isOverlap;

    [Header("Presentation")]
    [SerializeField] private GHChestPresentation presentation;

    [Header("World Item Drop Spawn")]
    [SerializeField] private Transform dropSpawnPoint;
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float dropSpawnHeight = 0.02f;
    [Tooltip("Drop Spawn Point 기준 로컬 위치 보정값입니다. X/Z로 중심을 옮기고 Y로 높이를 조절합니다.")]
    [SerializeField] private Vector3 dropAreaOffset;
    [Tooltip("드롭 타원 전체 크기입니다. X는 월드 가로축, Y는 월드 Z축 크기로 사용합니다.")]
    [SerializeField] private Vector2 dropAreaSize = new Vector2(2.2f, 2.2f);
    [Tooltip("유효한 바닥 위치를 찾기 위해 시도할 횟수입니다.")]
    [Min(1)] [SerializeField] private int dropPositionAttempts = 12;
    [Tooltip("드롭 위치 주변에 다른 오브젝트가 없어야 하는 반경입니다.")]
    [Min(0.01f)] [SerializeField] private float dropClearanceRadius = 0.25f;
    [Tooltip("바닥부터 이 높이까지 다른 오브젝트가 있으면 해당 위치를 사용하지 않습니다.")]
    [Min(0.01f)] [SerializeField] private float dropClearanceHeight = 0.9f;
    [Tooltip("드롭을 놓을 수 있는 바닥의 최대 경사각입니다.")]
    [Range(0f, 89f)] [SerializeField] private float maxGroundSlope = 50f;

    [Header("Drop Spawn Gizmo")]
    [Tooltip("상자를 선택했을 때 실제 드롭 타원과 중심 위치를 Scene 뷰에 표시합니다.")]
    [SerializeField] private bool showDropSpawnGizmo = true;
    [SerializeField] private Color dropSpawnGizmoColor = new Color(1f, 0.72f, 0.1f, 0.9f);

    [Header("Drop Pool")]
    [Min(0)] [SerializeField] private int poolPrewarmCount;
    [Min(0f)] [SerializeField] private float autoReturnToPoolSeconds = 10f;

    private bool isOpened;
    private bool isDropSpawnComplete;
    private int pendingDropLandings;

    /// <summary>정보 UI에 표시할 상자 이름입니다.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;

    public string InteractionPrompt => interactionPrompt;
    public event Action OpenChest;
    public event Action<string> OpenChestId;

    private void Awake()
    {
        if (presentation == null)
        {
            presentation = GetComponent<GHChestPresentation>();
        }

        WorldDropPool.Prewarm(poolPrewarmCount);
    }

    public bool CanInteract(PlayerInteraction interactor)
    {
        if (isOpened || interactor == null)
        {
            return false;
        }

        PlayerInventory inventory = ResolvePlayerInventory(interactor);
        return inventory != null;
    }

    public void Interact(PlayerInteraction interactor)
    {
        if (isOpened)
        {
            return;
        }

        PlayerInventory inventory = ResolvePlayerInventory(interactor);
        if (inventory == null) return;
        OpenAndDropItems();
    }

    private static PlayerInventory ResolvePlayerInventory(PlayerInteraction interactor)
    {
        if (interactor == null)
        {
            return null;
        }

        PlayerInventory inventory = PlayerReferenceResolver.FindComponentInPlayerHierarchy<PlayerInventory>(
            interactor.gameObject);
        return inventory != null
            ? inventory
            : PlayerReferenceResolver.FindPlayerComponent<PlayerInventory>();
    }

    private void OpenAndDropItems()
    {
        isOpened = true;

        if (presentation != null
            && presentation.PlayOpenSequence(SpawnDropItemsAndNotify))
        {
            return;
        }

        SpawnDropItemsAndNotify();
    }

    private void SpawnDropItemsAndNotify()
    {
        pendingDropLandings = 0;
        isDropSpawnComplete = false;

        if (dropItem == null || dropItem.Length == 0)
        {
            NotifyOpened();
            isDropSpawnComplete = true;
            TryFinishAfterDropLandings();
            return;
        }

        Collider[] chestColliders = GetComponentsInChildren<Collider>(true);
        int dropEntryCount = isOverlap ? dropItem.Length : 1;
        Dictionary<string, int> amountsByItemId = new Dictionary<string, int>();

        for (int i = 0; i < dropEntryCount; i++)
        {
            ChestItem item = dropItem[i];
            if (string.IsNullOrWhiteSpace(item.itemId))
            {
                Debug.LogWarning($"[{name}] Drop Item의 {i}번 Item Id가 비어 있어 생성을 건너뜁니다.", this);
                continue;
            }

            // [HDY 요청] Min~Max 랜덤 드랍 대신 고정 개수(dropCount)를 그대로 사용하도록 변경.
            int count = Mathf.Max(0, item.dropCount);

            if (count <= 0)
            {
                continue;
            }

            string normalizedItemId = item.itemId.Trim();
            amountsByItemId.TryGetValue(normalizedItemId, out int currentAmount);
            amountsByItemId[normalizedItemId] = currentAmount + count;
        }

        WorldItemDropLaunchSettings launchSettings = presentation != null
            ? presentation.CreateDropLaunchSettings()
            : default;

        foreach (KeyValuePair<string, int> drop in amountsByItemId)
        {
            int spawnedAmount = WorldItemDropSpawner.SpawnIndividualItems(
                drop.Key,
                drop.Value,
                transform,
                dropSpawnPoint,
                dropAreaOffset,
                dropAreaSize,
                groundLayer,
                dropSpawnHeight,
                dropPositionAttempts,
                dropClearanceRadius,
                dropClearanceHeight,
                maxGroundSlope,
                autoReturnToPoolSeconds,
                chestColliders,
                launchSettings,
                OnDropLanded);

            if (spawnedAmount > 0 && launchSettings.enabled)
            {
                pendingDropLandings += spawnedAmount;
            }
        }

        NotifyOpened();
        isDropSpawnComplete = true;
        TryFinishAfterDropLandings();
    }

    private void NotifyOpened()
    {
        OpenChest?.Invoke();
        OpenChestId?.Invoke(chestId);
    }

    private void OnDropLanded()
    {
        pendingDropLandings = Mathf.Max(0, pendingDropLandings - 1);
        TryFinishAfterDropLandings();
    }

    private void TryFinishAfterDropLandings()
    {
        if (isDropSpawnComplete && pendingDropLandings == 0)
        {
            Destroy(gameObject);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showDropSpawnGizmo)
        {
            return;
        }

        WorldItemDropSpawner.DrawDropAreaGizmo(
            dropSpawnPoint,
            transform,
            dropAreaOffset,
            dropAreaSize,
            dropSpawnHeight,
            dropSpawnGizmoColor);
    }
#endif
}
