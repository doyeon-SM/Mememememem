using HDY.Item;
using KMS.Harvesting;
using UnityEngine;

using KmsItemStack = KMS.InventoryDuped.ItemStack;
using KmsPlayerInventory = KMS.InventoryDuped.PlayerInventory;

namespace KMS.Combat
{
    [DisallowMultipleComponent]
    public class PlayerMeleeAttackController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private KMS.PlayerInput input;
        [SerializeField] private KMS.PlayerMovement movement;
        [SerializeField] private KmsPlayerInventory inventory;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Animator animator;
        [SerializeField] private ItemCatalogManager catalogManager;

        [Header("Melee Attack")]
        [SerializeField] private LayerMask attackLayer = ~0;
        [SerializeField, Min(0f)] private float attackOriginHeight = 1.2f;
        [SerializeField] private string[] catalogMeleeItemIds =
        {
            "tool_shabby_club",
            "tool_decent_club"
        };
        [SerializeField, Min(0.1f)] private float catalogWeaponAttackDistance = 3f;
        [SerializeField, Min(0f)] private float catalogWeaponAttackCooldown = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool drawDebugRay = true;
        [SerializeField] private Color debugMissColor = Color.red;
        [SerializeField] private Color debugHitColor = Color.yellow;
        [SerializeField] private bool logHitTarget = true;

        private static readonly int SlashHash = Animator.StringToHash("Slash");

        private float cooldownTimer;

        private void Reset()
        {
            input = GetComponent<KMS.PlayerInput>();
            movement = GetComponent<KMS.PlayerMovement>();
            inventory = GetComponent<KmsPlayerInventory>();

            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Awake()
        {
            if (input == null) input = GetComponent<KMS.PlayerInput>();
            if (movement == null) movement = GetComponent<KMS.PlayerMovement>();
            if (inventory == null) inventory = GetComponent<KmsPlayerInventory>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (movement != null && movement.Animator != null) animator = movement.Animator;

            catalogManager = ItemCatalogManager.Resolve(catalogManager);
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.PrimaryActionPressed += TryAttack;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.PrimaryActionPressed -= TryAttack;
            }
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }
        }

        private void TryAttack()
        {
            if (cooldownTimer > 0f) return;
            if (catalogManager == null)
            {
                catalogManager = ItemCatalogManager.Resolve(null);
            }

            if (inventory == null || cameraTransform == null || catalogManager == null) return;

            KmsItemStack selectedSlot = inventory.GetSelectedQuickSlot();
            if (selectedSlot == null || selectedSlot.IsEmpty) return;

            ItemData selectedItem = catalogManager.FindItemData(selectedSlot.itemId);
            if (!TryGetAttackProfile(
                    selectedSlot.itemId,
                    selectedItem,
                    out float attackDistance,
                    out float attackCooldown,
                    out int attackDamage))
            {
                return;
            }

            cooldownTimer = attackCooldown;

            if (animator != null)
            {
                animator.SetTrigger(SlashHash);
            }

            Vector3 attackOrigin = transform.position + transform.up * attackOriginHeight;
            Ray ray = new Ray(attackOrigin, cameraTransform.forward);

            bool hasHit = Physics.Raycast(
                ray,
                out RaycastHit hit,
                attackDistance,
                attackLayer,
                QueryTriggerInteraction.Collide);

            if (drawDebugRay)
            {
                Debug.DrawRay(
                    ray.origin,
                    ray.direction * attackDistance,
                    hasHit ? debugHitColor : debugMissColor,
                    0.5f);
            }

            if (!hasHit) return;

            if (logHitTarget)
            {
                Debug.Log($"[MeleeAttack] Hit: {hit.collider.name}", hit.collider);
            }

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.IsDead) return;

            damageable.TakeDamage(attackDamage);
        }

        private bool TryGetAttackProfile(
            string itemId,
            ItemData itemData,
            out float attackDistance,
            out float attackCooldown,
            out int attackDamage)
        {
            attackDistance = 0f;
            attackCooldown = 0f;
            attackDamage = 0;

            if (itemData is WeaponItemData weapon)
            {
                attackDistance = Mathf.Max(0.1f, weapon.AttackDistance);
                attackCooldown = Mathf.Max(0f, weapon.AttackCooldown);
                attackDamage = Mathf.Max(1, weapon.Value);
                return true;
            }

            if (itemData == null ||
                itemData.Category != ItemCategory.Tool ||
                !IsCatalogMeleeItem(itemId))
            {
                return false;
            }

            attackDistance = Mathf.Max(0.1f, catalogWeaponAttackDistance);
            attackCooldown = Mathf.Max(0f, catalogWeaponAttackCooldown);
            attackDamage = Mathf.Max(1, itemData.Value);
            return true;
        }

        private bool IsCatalogMeleeItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || catalogMeleeItemIds == null)
            {
                return false;
            }

            for (int i = 0; i < catalogMeleeItemIds.Length; i++)
            {
                if (string.Equals(itemId, catalogMeleeItemIds[i], System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
