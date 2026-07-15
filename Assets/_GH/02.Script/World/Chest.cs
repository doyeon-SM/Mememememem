using HDY.Item;
using KGH.Data;
using KMS;
using KMS.InventoryDuped;
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

    /// <summary>정보 UI에 표시할 상자 이름입니다.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;

    public string InteractionPrompt => interactionPrompt;

    public bool CanInteract(PlayerInteraction interactor)
    {
        if (interactor == null)
        {
            return false;
        }

        PlayerInventory inventory = interactor.GetComponentInParent<PlayerInventory>();
        if (inventory == null) return false;
        else return true;
    }

    public void Interact(PlayerInteraction interactor)
    {
        PlayerInventory inventory = interactor.GetComponentInParent<PlayerInventory>();
        if (inventory == null) return;
        ChestItem(inventory);
    }

    private void ChestItem(PlayerInventory inventory)
    {
        if(!isOverlap)
        {
            int count = UnityEngine.Random.Range(dropItem[0].minDrop, dropItem[0].maxDrop +1);
            inventory.AddItem(dropItem[0].itemData.Item_ID, count);
        }
        else
        {
            for(int i = 0; i < dropItem.Length; i++)
            {
                int count = UnityEngine.Random.Range(dropItem[i].minDrop, dropItem[i].maxDrop + 1);
                inventory.AddItem(dropItem[i].itemData.Item_ID, count);
            }
        }
        Destroy(this.gameObject);
    }
}
