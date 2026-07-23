using System;
using HDY.Item;
using KMS.InventoryDuped;
using UnityEngine;

/// <summary>게임 시작 시 보유 여부를 확인할 기본 아이템 한 종류입니다.</summary>
[Serializable]
public class PlayerDefaultItemEntry
{
    [SerializeField] private string item;
    [Min(1)] [SerializeField] private int amount = 1;

    public string Item => item;
    public int Amount => Mathf.Max(1, amount);
}

/// <summary>
/// 테스트용 기본 아이템 지급 컴포넌트입니다.
/// Start 시점에 플레이어가 전혀 보유하지 않은 아이템만 지급합니다.
/// PlayerInventory.AddItem은 새 스택을 빈 퀵슬롯부터 생성합니다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerDefaultItemTest : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private string playerTag = PlayerReferenceResolver.DefaultPlayerTag;
    [SerializeField] private string playerLayerName = PlayerReferenceResolver.DefaultPlayerLayerName;

    [Header("Default Items")]
    [Tooltip("게임 시작 시 보유하고 있지 않으면 퀵슬롯부터 지급할 아이템과 수량입니다.")]
    [SerializeField] private PlayerDefaultItemEntry[] defaultItems = Array.Empty<PlayerDefaultItemEntry>();

    [Header("Debug")]
    [SerializeField] private bool logResult = true;

    private void Start()
    {
        ResolvePlayerInventory();
        GrantMissingDefaultItems();
    }

    private void GrantMissingDefaultItems()
    {
        ResolvePlayerInventory();
        if (playerInventory == null)
        {
            Debug.LogWarning("[PlayerDefaultItemTest] PlayerInventory를 찾을 수 없어 기본 아이템을 지급하지 못했습니다.", this);
            return;
        }

        if (defaultItems == null || defaultItems.Length == 0)
        {
            return;
        }

        for (int i = 0; i < defaultItems.Length; i++)
        {
            PlayerDefaultItemEntry entry = defaultItems[i];
            if (entry == null || entry.Item == null || string.IsNullOrWhiteSpace(entry.Item))
            {
                continue;
            }

            string itemId = entry.Item;
            if (playerInventory.GetItemAmount(itemId) > 0)
            {
                continue;
            }

            int requestedAmount = entry.Amount;
            int remainingAmount = playerInventory.AddItem(entry.Item, requestedAmount);
            int grantedAmount = requestedAmount - remainingAmount;

            if (!logResult)
            {
                continue;
            }

            if (grantedAmount > 0)
            {
                Debug.Log($"[PlayerDefaultItemTest] 기본 아이템 지급: {itemId} x{grantedAmount}", this);
            }

            if (remainingAmount > 0)
            {
                Debug.LogWarning($"[PlayerDefaultItemTest] 공간 부족으로 {itemId} x{remainingAmount}개를 지급하지 못했습니다.", this);
            }
        }
    }

    private void ResolvePlayerInventory()
    {
        if (playerInventory != null)
        {
            return;
        }

        playerInventory = GetComponentInParent<PlayerInventory>(true);
        if (playerInventory == null)
        {
            playerInventory = GetComponentInChildren<PlayerInventory>(true);
        }

        if (playerInventory == null)
        {
            playerInventory = PlayerReferenceResolver.FindPlayerComponent<PlayerInventory>(
                null,
                playerTag,
                playerLayerName);
        }
    }
}
