using System;
using HDY;
using HDY.Forge;
using HDY.Item;
using KMS.InventoryDuped;
using UnityEngine;

/// <summary>게임 시작 시 보유 여부를 확인할 기본 아이템 한 종류입니다.</summary>
[Serializable]
public class PlayerDefaultItemEntry
{
    [SerializeField] private string item;
    [Min(1)] [SerializeField] private int amount = 1;

    [Tooltip("체크하면 지급 직후 이 아이템에 고정 연마(Rare/DamageIncrease/1)를 강제로 적용합니다. " +
             "대장간 대상이 아닌 아이템(예: 몽둥이)이면 체크해도 조용히 무시됩니다.")]
    [SerializeField] private bool applyFixedToolRefinement;

    public string Item => item;
    public int Amount => Mathf.Max(1, amount);
    public bool ApplyFixedToolRefinement => applyFixedToolRefinement;
}

/// <summary>
/// 테스트용 기본 아이템 지급 컴포넌트입니다.
/// Start 시점에 플레이어가 전혀 보유하지 않은 아이템만 지급합니다.
/// PlayerInventory.AddItem은 새 스택을 빈 퀵슬롯부터 생성합니다.
///
/// [HDY 요청 - 초반 도구 고정 연마] applyFixedToolRefinement가 켜진 항목은 지급 직후 곧바로
/// ForgeManager.TryAssignFixedRefinement로 Rare 등급 + DamageIncrease(데미지) 옵션 + 수치 1을
/// 정확히 1칸만 강제로 채운다(무작위 판정 없음). 씬에 이미 있는 ForgeRefinementAutoAssigner가
/// 인벤토리 변경을 감지해서 랜덤 연마를 채우는 것보다 먼저 확정지어서, 랜덤 값으로 덮이지 않게 한다.
/// ForgeManager가 없는 씬(테스트 씬 등)에서는 경고만 남기고 아이템 지급 자체는 그대로 진행한다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerDefaultItemTest : MonoBehaviour
{
    // [HDY 요청 - 초반 도구 고정 연마 값] "고정"으로 요청받은 값이라 상수로 고정한다.
    private const CommonClass FixedRefinementGrade = CommonClass.Rare;
    private const string FixedRefinementOptionType = "DamageIncrease";
    private const string FixedRefinementDisplayName = "데미지";
    private const float FixedRefinementValue = 1f;

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
            if (OwnsItemIncludingForged(itemId))
            {
                continue;
            }

            int requestedAmount = entry.Amount;
            int remainingAmount = playerInventory.AddItem(entry.Item, requestedAmount);
            int grantedAmount = requestedAmount - remainingAmount;

            if (grantedAmount > 0 && entry.ApplyFixedToolRefinement)
            {
                ApplyFixedToolRefinement(itemId);
            }

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

    /// <summary>
    /// [HDY 요청 - 재지급 버그 수정] PlayerInventory.GetItemAmount는 stack.itemId와 완전히 똑같은
    /// 문자열인 경우만 센다. 그런데 이 아이템에 대장간 강화/승급/연마(이 클래스가 호출하는
    /// ApplyFixedToolRefinement 포함)가 한 번이라도 적용되면, ForgeManager.ApplyInstanceToSlot이
    /// stack.itemId를 "{BaseItemId}@{InstanceId}" 형태의 합성 ID로 바꿔버린다. 그 결과 GetItemAmount만
    /// 으로는 이미 강화까지 적용된 도구를 "보유 중"으로 인식하지 못해서, 맵을 나갔다가 돌아와 Start()가
    /// 다시 실행될 때마다 같은 기본 아이템이 계속 재지급되는 문제가 있었다.
    /// 여기서는 공용 PlayerInventory.GetItemAmount 로직은 그대로 두고(다른 시스템에 영향 없도록),
    /// 이 컴포넌트 안에서만 인벤토리/퀵슬롯을 직접 훑어 합성 ID의 BaseItemId까지 비교해서 판정한다.
    /// </summary>
    private bool OwnsItemIncludingForged(string itemId)
    {
        if (playerInventory.GetItemAmount(itemId) > 0)
        {
            return true;
        }

        return ContainerHasBaseItem(playerInventory.inventory, itemId)
            || ContainerHasBaseItem(playerInventory.quickSlots, itemId);
    }

    /// <summary>[HDY 요청 - 재지급 버그 수정] 컨테이너 한 곳을 훑어 합성 ID든 아니든 BaseItemId가 itemId와 일치하는 스택이 있는지 확인한다.</summary>
    private static bool ContainerHasBaseItem(InventoryContainer container, string itemId)
    {
        if (container == null || container.slots == null)
        {
            return false;
        }

        foreach (ItemStack stack in container.slots)
        {
            if (stack == null || stack.IsEmpty)
            {
                continue;
            }

            if (stack.itemId == itemId)
            {
                return true;
            }

            if (ForgeInstanceRegistry.TryParseCompositeId(stack.itemId, out string baseItemId, out _) && baseItemId == itemId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// [HDY 요청 - 초반 도구 고정 연마] 방금 지급되어 인벤토리/퀵슬롯 어딘가에 놓인 이 itemId의 스택을
    /// 찾아서, ForgeManager를 통해 Rare/DamageIncrease/1 연마를 정확히 1칸만 강제로 채운다.
    /// </summary>
    private void ApplyFixedToolRefinement(string itemId)
    {
        if (ForgeManager.Instance == null)
        {
            Debug.LogWarning($"[PlayerDefaultItemTest] ForgeManager를 찾을 수 없어 {itemId}에 고정 연마를 적용하지 못했습니다.", this);
            return;
        }

        ItemStack stack = FindLiveStackByItemId(itemId);
        if (stack == null)
        {
            Debug.LogWarning($"[PlayerDefaultItemTest] 지급 직후 {itemId} 스택을 인벤토리에서 찾지 못해 고정 연마를 적용하지 못했습니다.", this);
            return;
        }

        bool applied = ForgeManager.Instance.TryAssignFixedRefinement(
            stack, FixedRefinementGrade, FixedRefinementOptionType, FixedRefinementDisplayName, FixedRefinementValue);

        if (!logResult)
        {
            return;
        }

        if (applied)
        {
            Debug.Log($"[PlayerDefaultItemTest] 고정 연마 적용: {itemId} -> {FixedRefinementGrade}/{FixedRefinementOptionType}({FixedRefinementDisplayName}) {FixedRefinementValue}", this);
        }
        else
        {
            // 대장간 대상이 아닌 아이템(예: 몽둥이)이면 정상적으로 여기로 온다 - 오류가 아니라 그냥 스킵된 것.
            Debug.Log($"[PlayerDefaultItemTest] {itemId}은(는) 대장간 대상이 아니라 고정 연마를 건너뛰었습니다.", this);
        }
    }

    /// <summary>
    /// inventory.slots / quickSlots.slots(둘 다 라이브 ItemStack[] 참조, ForgeUI가 도구를 찾을 때 쓰는 것과
    /// 동일한 방식)를 훑어서 itemId가 일치하는 첫 스택을 찾는다. ItemStack은 참조 타입이라, 여기서 얻은
    /// 스택의 itemId를 바꾸면 실제 인벤토리 슬롯도 그대로 갱신된다.
    /// </summary>
    private ItemStack FindLiveStackByItemId(string itemId)
    {
        ItemStack found = FindLiveStackInContainer(playerInventory.inventory, itemId);
        if (found != null) return found;

        return FindLiveStackInContainer(playerInventory.quickSlots, itemId);
    }

    private static ItemStack FindLiveStackInContainer(InventoryContainer container, string itemId)
    {
        if (container == null || container.slots == null) return null;

        foreach (ItemStack stack in container.slots)
        {
            if (stack != null && !stack.IsEmpty && stack.itemId == itemId)
            {
                return stack;
            }
        }

        return null;
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
