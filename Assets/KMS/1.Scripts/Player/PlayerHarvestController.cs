using HDY.Item;
using UnityEngine;

using HdyItemCategory = HDY.Item.ItemCategory;
using KmsItemStack = KMS.InventoryDuped.ItemStack;
using KmsPlayerInventory = KMS.InventoryDuped.PlayerInventory;

namespace KMS.Harvesting
{
    [DisallowMultipleComponent]
    public class PlayerHarvestController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private KMS.PlayerInput input;
        [SerializeField] private KmsPlayerInventory inventory;
        [SerializeField] private Transform cameraTransform;

        // [HDY 요청] 선택된 퀵슬롯(ItemStack)에는 itemId만 있으므로, 실제 ItemData(Category/Value/ObjectType 등)를
        // 조회하기 위한 참조.
        [Header("아이템 카탈로그 (Item_ID로 조회할 때 사용)")]
        [SerializeField] private ItemCatalogManager catalogManager;

        [Header("Harvest")]
        [SerializeField] private LayerMask harvestLayer = ~0;
        [SerializeField] private float harvestDistance = 3f;
        [SerializeField] private float harvestCooldown = 0.35f;
        [SerializeField] private int fallbackToolDamage = 1;

        [Header("Debug")]
        [SerializeField] private bool drawDebugRay = true;
        [SerializeField] private Color debugMissColor = Color.red;
        [SerializeField] private Color debugHitColor = Color.green;
        [SerializeField] private bool logHitTarget = true;

        private float cooldownTimer;

        private void Reset()
        {
            input = GetComponent<KMS.PlayerInput>();
            inventory = GetComponent<KmsPlayerInventory>();

            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Awake()
        {
            if (input == null) input = GetComponent<KMS.PlayerInput>();
            if (inventory == null) inventory = GetComponent<KmsPlayerInventory>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

            catalogManager = ItemCatalogManager.Resolve(catalogManager);
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.PrimaryActionPressed += TryHarvest;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.PrimaryActionPressed -= TryHarvest;
            }
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }
        }

        private void TryHarvest()
        {
            if (cooldownTimer > 0f) return;
            if (inventory == null || cameraTransform == null) return;

            KmsItemStack selectedSlot = inventory.GetSelectedQuickSlot();
            if (selectedSlot == null || selectedSlot.IsEmpty) return;

            // [HDY 요청] 슬롯에는 itemId(string)만 있으므로 카탈로그에서 실제 ItemData를 조회한다.
            ItemData selectedItem = catalogManager != null ? catalogManager.FindItemData(selectedSlot.itemId) : null;
            if (selectedItem == null || selectedItem.Category != HdyItemCategory.Tool) return;

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

            bool hasHit = Physics.Raycast(
                ray,
                out RaycastHit hit,
                harvestDistance,
                harvestLayer,
                QueryTriggerInteraction.Collide);

            if (drawDebugRay)
            {
                Debug.DrawRay(
                    ray.origin,
                    ray.direction * harvestDistance,
                    hasHit ? debugHitColor : debugMissColor,
                    0.5f);
            }

            if (!hasHit)
            {
                return;
            }

            if (logHitTarget)
            {
                Debug.Log($"[Harvest] Hit: {hit.collider.name}", hit.collider);
            }
            if(WorldObjectHarvest(hit, selectedItem))
            {
                return;
            }
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.IsDead) return;

            int damage = Mathf.Max(1, selectedItem.Value);
            if (damage <= 0) damage = fallbackToolDamage;

            damageable.TakeDamage(damage);
            cooldownTimer = harvestCooldown;

            if (!damageable.IsDead) return;

            HarvestableResource resource = hit.collider.GetComponentInParent<HarvestableResource>();
            if (resource != null)
            {
                resource.TryCollectReward(inventory);
            }
        }

        private bool WorldObjectHarvest(RaycastHit hitObj, ItemData selectedItem)
        {
            if (hitObj.collider == null) return false;
            WorldObject harvestable = hitObj.collider.GetComponent<WorldObject>();
            if (harvestable == null) return false;
            harvestable.ObjectInteract(inventory, selectedItem);
            return true;
        }
    }
}
