using UnityEngine;
using HDY.Item;

namespace KMS.InventoryDuped
{
    [DisallowMultipleComponent]
    public class InventoryPickup : MonoBehaviour, KMS.IInteractable
    {
        [SerializeField] private ItemData itemData;
        [SerializeField] private int amount = 1;
        [SerializeField] private string promptPrefix = "Pick up";
        [SerializeField] private bool destroyWhenFullyPickedUp = true;

        public string InteractionPrompt
        {
            get
            {
                if (itemData == null || string.IsNullOrEmpty(itemData.ItemName)) return promptPrefix;

                return $"{promptPrefix} {itemData.ItemName}";
            }
        }

        public bool CanInteract(KMS.PlayerInteraction interactor)
        {
            return itemData != null
                   && amount > 0
                   && interactor != null
                   && interactor.GetComponent<PlayerInventory>() != null;
        }

        public void Interact(KMS.PlayerInteraction interactor)
        {
            if (interactor == null || itemData == null || amount <= 0) return;

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            int remaining = inventory.AddItem(itemData, amount);
            int pickedUp = amount - remaining;

            if (pickedUp <= 0) return;

            Debug.Log($"[InventoryPickup] Added {pickedUp} x {itemData.ItemName} to {interactor.name}'s inventory.");

            amount = remaining;

            if (amount <= 0 && destroyWhenFullyPickedUp)
            {
                Destroy(gameObject);
            }
        }
    }
}
