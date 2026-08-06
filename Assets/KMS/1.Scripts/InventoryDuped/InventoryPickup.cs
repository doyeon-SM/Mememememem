using UnityEngine;
using HDY.Item;

namespace KMS.InventoryDuped
{
    [DisallowMultipleComponent]
    public class InventoryPickup : MonoBehaviour, KMS.IInteractable
    {
        // [HDY 요청] ItemData 직접 참조 대신 Item_ID 문자열로 변경.
        // ItemCatalogManager가 시트 기반으로 바뀌면서 런타임에 매번 새 ItemData 인스턴스를
        // 만들기 때문에, 여기서 특정 ItemData 애셋을 직접 들고 있으면 같은 Item_ID를 가진
        // 두 개의 서로 다른 객체가 메모리에 동시에 존재하게 되어 다른 곳(GridManager 등)의
        // Resources.FindObjectsOfTypeAll<ItemData>() 조회가 꼬일 수 있다. ID 문자열만 들고
        // 있다가 ItemCatalogManager.FindItemData(itemId)/PlayerInventory.AddItem(string,int)로
        // 조회·위임하는 방식으로 통일했다.
        [SerializeField] private string itemId;
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
                ItemData itemData = ResolveItemData();
                if (itemData == null || string.IsNullOrEmpty(itemData.ItemName)) return promptPrefix;

                return $"{promptPrefix} {itemData.ItemName}";
            }
        }

        public bool CanInteract(KMS.PlayerInteraction interactor)
        {
            return !string.IsNullOrEmpty(itemId)
                   && amount > 0
                   && interactor != null
                   && interactor.GetComponent<PlayerInventory>() != null;
        }

        public void Interact(KMS.PlayerInteraction interactor)
        {
            if (interactor == null || string.IsNullOrEmpty(itemId) || amount <= 0) return;

            TryPickup(interactor.GetComponent<PlayerInventory>(), interactor.name);
        }

        private void Update()
        {
            if (!autoPickupWhenNear || string.IsNullOrEmpty(itemId) || amount <= 0) return;

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
            if (!autoPickupWhenNear || other == null || string.IsNullOrEmpty(itemId) || amount <= 0) return;

            PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
            if (inventory == null) return;

            TryPickup(inventory, inventory.name);
        }

        private void TryPickup(PlayerInventory inventory, string pickerName)
        {
            if (inventory == null || string.IsNullOrEmpty(itemId) || amount <= 0) return;

            int remaining = inventory.AddItem(itemId, amount);
            int pickedUp = amount - remaining;

            if (pickedUp <= 0) return;

            Debug.Log($"[InventoryPickup] Added {pickedUp} x {itemId} to {pickerName}'s inventory.");

            amount = remaining;

            if (amount <= 0 && destroyWhenFullyPickedUp)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>itemId로 ItemCatalogManager에서 ItemData를 조회한다. 못 찾으면 null.</summary>
        private ItemData ResolveItemData()
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            ItemCatalogManager catalogManager = ItemCatalogManager.Instance;
            if (catalogManager == null)
            {
                catalogManager = FindFirstObjectByType<ItemCatalogManager>();
            }

            return catalogManager != null ? catalogManager.FindItemData(itemId) : null;
        }

        private void OnDrawGizmosSelected()
        {
            if (!autoPickupWhenNear) return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
    }
}
