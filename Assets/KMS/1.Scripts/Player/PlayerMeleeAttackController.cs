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
            if (inventory == null || cameraTransform == null || catalogManager == null) return;

            KmsItemStack selectedSlot = inventory.GetSelectedQuickSlot();
            if (selectedSlot == null || selectedSlot.IsEmpty) return;

            WeaponItemData weapon = catalogManager.FindItemData(selectedSlot.itemId) as WeaponItemData;
            if (weapon == null) return;

            cooldownTimer = Mathf.Max(0f, weapon.AttackCooldown);

            if (animator != null)
            {
                animator.SetTrigger(SlashHash);
            }

            float attackDistance = Mathf.Max(0.1f, weapon.AttackDistance);
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

            damageable.TakeDamage(Mathf.Max(1, weapon.Value));
        }
    }
}
