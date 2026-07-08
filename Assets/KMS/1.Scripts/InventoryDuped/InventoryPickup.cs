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
        [SerializeField] private bool autoPickupWhenNear = true;
        [SerializeField] private float pickupRadius = 1.5f;
        [SerializeField] private LayerMask playerLayers = ~0;

        private readonly Collider[] pickupHits = new Collider[12];

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

            TryPickup(interactor.GetComponent<PlayerInventory>(), interactor.name);
        }

        private void Update()
        {
            if (!autoPickupWhenNear || itemData == null || amount <= 0) return;

            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                pickupRadius,
                pickupHits,
                playerLayers,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = pickupHits[i];
                if (hit == null) continue;

                PlayerInventory inventory = hit.GetComponentInParent<PlayerInventory>();
                if (inventory == null) continue;

                TryPickup(inventory, inventory.name);
                if (amount <= 0) return;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryAutoPickup(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryAutoPickup(other);
        }

        private void TryAutoPickup(Collider other)
        {
            if (!autoPickupWhenNear || other == null || itemData == null || amount <= 0) return;

            PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
            if (inventory == null) return;

            TryPickup(inventory, inventory.name);
        }

        private void TryPickup(PlayerInventory inventory, string pickerName)
        {
            if (inventory == null || itemData == null || amount <= 0) return;

            int remaining = inventory.AddItem(itemData, amount);
            int pickedUp = amount - remaining;

            if (pickedUp <= 0) return;

            Debug.Log($"[InventoryPickup] Added {pickedUp} x {itemData.ItemName} to {pickerName}'s inventory.");

            amount = remaining;

            if (amount <= 0 && destroyWhenFullyPickedUp)
            {
                Destroy(gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!autoPickupWhenNear) return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
    }
}
