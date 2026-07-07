using UnityEngine;

namespace KMS.InventoryDuped
{
    [DisallowMultipleComponent]
    public class InventoryPickup : MonoBehaviour, KMS.IInteractable
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField] private int amount = 1;
        [SerializeField] private string promptPrefix = "Pick up";
        [SerializeField] private bool destroyWhenFullyPickedUp = true;

        public string InteractionPrompt
        {
            get
            {
                if (item == null || string.IsNullOrEmpty(item.displayName)) return promptPrefix;

                return $"{promptPrefix} {item.displayName}";
            }
        }

        public bool CanInteract(KMS.PlayerInteraction interactor)
        {
            return item != null
                   && amount > 0
                   && interactor != null
                   && interactor.GetComponent<PlayerInventory>() != null;
        }

        public void Interact(KMS.PlayerInteraction interactor)
        {
            if (interactor == null || item == null || amount <= 0) return;

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            int remaining = inventory.AddItem(item, amount);
            int pickedUp = amount - remaining;

            if (pickedUp <= 0) return;

            amount = remaining;

            if (amount <= 0 && destroyWhenFullyPickedUp)
            {
                Destroy(gameObject);
            }
        }
    }
}
