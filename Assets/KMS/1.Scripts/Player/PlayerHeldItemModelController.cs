using KMS.InventoryDuped;
using UnityEngine;

namespace KMS
{
    [DisallowMultipleComponent]
    public sealed class PlayerHeldItemModelController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform handAnchor;
        [SerializeField] private HeldItemPrefabTable prefabTable;

        private GameObject heldModelInstance;
        private string displayedItemId;
        private bool isThrowVisualSuppressed;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (inventory != null)
            {
                inventory.OnSelectedQuickSlotChanged += HandleSelectedQuickSlotChanged;
                inventory.OnQuickSlotChanged += HandleQuickSlotChanged;
            }

            RefreshHeldModel();
        }

        private void Start()
        {
            // 씬 복원과 다른 컴포넌트의 Awake가 끝난 상태를 한 번 더 반영한다.
            ResolveReferences();
            RefreshHeldModel();
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.OnSelectedQuickSlotChanged -= HandleSelectedQuickSlotChanged;
                inventory.OnQuickSlotChanged -= HandleQuickSlotChanged;
            }

            ClearHeldModel();
        }

        private void HandleSelectedQuickSlotChanged(int _)
        {
            RefreshHeldModel();
        }

        private void HandleQuickSlotChanged(int changedIndex)
        {
            if (inventory != null && changedIndex == inventory.selectedQuickSlotIndex)
            {
                RefreshHeldModel();
            }
        }

        public void SetThrowVisualSuppressed(bool suppressed)
        {
            if (isThrowVisualSuppressed == suppressed) return;

            isThrowVisualSuppressed = suppressed;
            RefreshHeldModel();
        }

        private void RefreshHeldModel()
        {
            if (isThrowVisualSuppressed || inventory == null || prefabTable == null)
            {
                ClearHeldModel();
                return;
            }

            ItemStack selectedStack = inventory.GetSelectedQuickSlot();
            if (selectedStack == null || selectedStack.IsEmpty)
            {
                ClearHeldModel();
                return;
            }

            GameObject heldPrefab = prefabTable.GetPrefab(selectedStack.itemId);
            if (heldPrefab == null)
            {
                ClearHeldModel();
                return;
            }

            if (heldModelInstance != null && displayedItemId == selectedStack.itemId)
            {
                return;
            }

            ResolveHandAnchor();
            if (handAnchor == null)
            {
                ClearHeldModel();
                return;
            }

            ClearHeldModel();
            heldModelInstance = Instantiate(heldPrefab, handAnchor, false);
            heldModelInstance.name = $"{heldPrefab.name}_Instance";
            displayedItemId = selectedStack.itemId;
        }

        private void ResolveReferences()
        {
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (movement != null && movement.Animator != null) animator = movement.Animator;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            ResolveHandAnchor();
        }

        private void ResolveHandAnchor()
        {
            if (handAnchor == null && animator != null && animator.isHuman)
            {
                handAnchor = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
        }

        private void ClearHeldModel()
        {
            displayedItemId = null;
            if (heldModelInstance == null) return;

            Destroy(heldModelInstance);
            heldModelInstance = null;
        }
    }
}
