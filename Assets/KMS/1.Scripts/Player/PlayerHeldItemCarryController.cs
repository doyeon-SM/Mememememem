using HDY.Item;
using KMS.InventoryDuped;
using UnityEngine;

namespace KMS
{
    public enum HeldItemCarryType
    {
        None = 0,
        LongTool = 1,
        Club = 2
    }

    [DisallowMultipleComponent]
    public sealed class PlayerHeldItemCarryController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerToolAnimationController toolAnimationController;
        [SerializeField] private PlayerHeldItemModelController heldItemModelController;

        [Header("Animator")]
        [SerializeField] private string carryLayerName = "HeldItemCarry";

        [Header("Long Tool Weights")]
        [SerializeField, Range(0f, 1f)] private float longToolIdleWeight = 0.8f;
        [SerializeField, Range(0f, 1f)] private float longToolWalkWeight = 0.9f;
        [SerializeField, Range(0f, 1f)] private float longToolRunWeight = 1f;

        [Header("Club Weights")]
        [SerializeField, Range(0f, 1f)] private float clubIdleWeight = 0.58f;
        [SerializeField, Range(0f, 1f)] private float clubWalkWeight = 0.75f;
        [SerializeField, Range(0f, 1f)] private float clubRunWeight = 0.9f;

        [Header("Blending")]
        [SerializeField, Min(0.01f)] private float movingSpeedThreshold = 0.1f;
        [SerializeField, Min(0.01f)] private float blendInSpeed = 8f;
        [SerializeField, Min(0.01f)] private float blendOutSpeed = 14f;

        public HeldItemCarryType CurrentCarryType { get; private set; }

        private static readonly int CarryTypeHash = Animator.StringToHash("HeldItemCarryType");
        private static readonly int LocomotionStateHash = Animator.StringToHash("Locomotion");

        private int carryLayerIndex = -1;
        private bool toolActionSuppressed;
        private float carryOrientationWeight;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
            ResolveCarryLayer();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ResolveCarryLayer();
            Subscribe();
            RefreshCarryType();
            SetLayerWeightImmediate(0f);
        }

        private void Start()
        {
            // Other player components finish their Awake initialization before this refresh.
            RefreshCarryType();
        }

        private void OnDisable()
        {
            Unsubscribe();
            toolActionSuppressed = false;
            carryOrientationWeight = 0f;
            SetLayerWeightImmediate(0f);
            if (heldItemModelController != null)
            {
                heldItemModelController.ApplyCarryOrientation(HeldItemCarryType.None, 0f);
            }
        }

        private void Update()
        {
            if (carryLayerIndex < 0)
            {
                ResolveCarryLayer();
                if (carryLayerIndex < 0) return;
            }

            float targetWeight = ResolveTargetWeight();
            float currentWeight = animator.GetLayerWeight(carryLayerIndex);
            float speed = targetWeight > currentWeight ? blendInSpeed : blendOutSpeed;
            float nextWeight = Mathf.MoveTowards(
                currentWeight,
                targetWeight,
                speed * Time.deltaTime);
            animator.SetLayerWeight(carryLayerIndex, nextWeight);

            float orientationTarget = targetWeight > 0f ? 1f : 0f;
            float orientationSpeed = orientationTarget > carryOrientationWeight
                ? blendInSpeed
                : blendOutSpeed;
            carryOrientationWeight = Mathf.MoveTowards(
                carryOrientationWeight,
                orientationTarget,
                orientationSpeed * Time.deltaTime);
            if (heldItemModelController != null)
            {
                heldItemModelController.ApplyCarryOrientation(
                    CurrentCarryType,
                    carryOrientationWeight);
            }
        }

        private void Subscribe()
        {
            if (inventory != null)
            {
                inventory.OnSelectedQuickSlotChanged += HandleSelectedQuickSlotChanged;
                inventory.OnQuickSlotChanged += HandleQuickSlotChanged;
            }

            if (toolAnimationController != null)
            {
                toolAnimationController.ToolActionStarted += HandleToolActionStarted;
                toolAnimationController.ToolActionEnded += HandleToolActionEnded;
            }
        }

        private void Unsubscribe()
        {
            if (inventory != null)
            {
                inventory.OnSelectedQuickSlotChanged -= HandleSelectedQuickSlotChanged;
                inventory.OnQuickSlotChanged -= HandleQuickSlotChanged;
            }

            if (toolAnimationController != null)
            {
                toolAnimationController.ToolActionStarted -= HandleToolActionStarted;
                toolAnimationController.ToolActionEnded -= HandleToolActionEnded;
            }
        }

        private void HandleSelectedQuickSlotChanged(int _)
        {
            RefreshCarryType();
        }

        private void HandleQuickSlotChanged(int changedIndex)
        {
            if (inventory != null && changedIndex == inventory.selectedQuickSlotIndex)
            {
                RefreshCarryType();
            }
        }

        private void HandleToolActionStarted()
        {
            toolActionSuppressed = true;
        }

        private void HandleToolActionEnded()
        {
            toolActionSuppressed = false;
        }

        private void RefreshCarryType()
        {
            HeldItemCarryType carryType = HeldItemCarryType.None;
            if (inventory != null && toolAnimationController != null)
            {
                ItemStack selectedStack = inventory.GetSelectedQuickSlot();
                if (selectedStack != null && !selectedStack.IsEmpty)
                {
                    ItemData itemData = inventory.FindItemData(selectedStack.itemId);
                    ToolMotionType motionType = toolAnimationController.ResolveMotionType(itemData);
                    switch (motionType)
                    {
                        case ToolMotionType.Axe:
                        case ToolMotionType.Hoe:
                        case ToolMotionType.Pickaxe:
                            carryType = HeldItemCarryType.LongTool;
                            break;
                        case ToolMotionType.Club:
                            carryType = HeldItemCarryType.Club;
                            break;
                    }
                }
            }

            CurrentCarryType = carryType;
            if (animator != null)
            {
                animator.SetInteger(CarryTypeHash, (int)carryType);
            }
        }

        private float ResolveTargetWeight()
        {
            if (animator == null
                || movement == null
                || CurrentCarryType == HeldItemCarryType.None
                || toolActionSuppressed
                || (toolAnimationController != null && toolAnimationController.IsToolActionPlaying)
                || movement.IsDead
                || movement.IsOnLadder
                || !IsStableLocomotion())
            {
                return 0f;
            }

            bool moving = movement.CurrentSpeed > movingSpeedThreshold;
            bool running = moving && movement.IsSprinting;
            if (CurrentCarryType == HeldItemCarryType.Club)
            {
                if (running) return clubRunWeight;
                return moving ? clubWalkWeight : clubIdleWeight;
            }

            if (running) return longToolRunWeight;
            return moving ? longToolWalkWeight : longToolIdleWeight;
        }

        private bool IsStableLocomotion()
        {
            if (animator.IsInTransition(0)) return false;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            return state.shortNameHash == LocomotionStateHash;
        }

        private void ResolveReferences()
        {
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (toolAnimationController == null)
            {
                toolAnimationController = GetComponent<PlayerToolAnimationController>();
            }
            if (heldItemModelController == null)
            {
                heldItemModelController = GetComponent<PlayerHeldItemModelController>();
            }
            if (movement != null && movement.Animator != null) animator = movement.Animator;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
        }

        private void ResolveCarryLayer()
        {
            carryLayerIndex = animator != null
                ? animator.GetLayerIndex(carryLayerName)
                : -1;
        }

        private void SetLayerWeightImmediate(float weight)
        {
            if (animator != null && carryLayerIndex >= 0)
            {
                animator.SetLayerWeight(carryLayerIndex, Mathf.Clamp01(weight));
            }
        }

        private void OnValidate()
        {
            movingSpeedThreshold = Mathf.Max(0.01f, movingSpeedThreshold);
            blendInSpeed = Mathf.Max(0.01f, blendInSpeed);
            blendOutSpeed = Mathf.Max(0.01f, blendOutSpeed);
        }
    }
}
