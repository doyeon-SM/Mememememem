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
        [SerializeField] private PlayerToolAnimationController toolAnimationController;
        [SerializeField] private Transform handAnchor;
        [SerializeField] private HeldItemPrefabTable prefabTable;

        [Header("Scale")]
        [Tooltip("Compensates for the 1.5x player visual scale so held capsules keep their original world size.")]
        [Min(0.01f)]
        [SerializeField] private float heldCapsuleScaleCompensation = 2f / 3f;

        [Header("Carry Orientation")]
        [Tooltip("Direction from the hand toward the tool head while carrying a long tool.")]
        [SerializeField] private Vector3 longToolCarryDirection = new Vector3(0.12f, 0.22f, 1f);
        [Tooltip("Direction from the hand toward the club head while carrying a club.")]
        [SerializeField] private Vector3 clubCarryDirection = new Vector3(0.16f, 0.08f, 1f);

        private GameObject heldModelInstance;
        private string displayedItemId;
        private bool isThrowVisualSuppressed;
        private Quaternion heldModelBaseLocalRotation = Quaternion.identity;
        private Vector3 heldModelAxis = Vector3.up;
        private HeldItemCarryType requestedCarryType;
        private float requestedCarryWeight;
        private bool wasToolActionActive;
        private Quaternion actionEntryLocalRotation = Quaternion.identity;
        private Quaternion actionExitLocalRotation = Quaternion.identity;
        private float actionRecoveryBlend = 1f;
        private ToolMotionType lastActionMotionType = ToolMotionType.None;

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

        private void LateUpdate()
        {
            UpdateHeldOrientation();
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
            heldModelBaseLocalRotation = heldModelInstance.transform.localRotation;
            heldModelAxis = ResolveHeldModelAxis(heldModelInstance.transform);
            if (IsCapsule(selectedStack.itemId))
            {
                heldModelInstance.transform.localScale *= heldCapsuleScaleCompensation;
            }
            displayedItemId = selectedStack.itemId;
        }

        public void ApplyCarryOrientation(HeldItemCarryType carryType, float blendWeight)
        {
            requestedCarryType = carryType;
            requestedCarryWeight = Mathf.Clamp01(blendWeight);
        }

        private void UpdateHeldOrientation()
        {
            if (heldModelInstance == null || handAnchor == null) return;

            bool actionActive = toolAnimationController != null
                && toolAnimationController.IsToolActionStateActive;
            if (actionActive)
            {
                lastActionMotionType = toolAnimationController.CurrentMotionType;
                if (!wasToolActionActive)
                {
                    actionEntryLocalRotation = heldModelInstance.transform.localRotation;
                    wasToolActionActive = true;
                }

                float normalizedTime = toolAnimationController.GetCurrentActionNormalizedTime();
                if (ToolActionGripPose.TryEvaluate(
                        toolAnimationController.CurrentMotionType,
                        normalizedTime,
                        out Vector3 direction,
                        out float roll))
                {
                    Quaternion target = ResolveDirectionLocalRotation(direction, roll);
                    float entryBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.10f, normalizedTime));
                    heldModelInstance.transform.localRotation = Quaternion.Slerp(
                        actionEntryLocalRotation,
                        target,
                        entryBlend);
                    actionExitLocalRotation = heldModelInstance.transform.localRotation;
                    actionRecoveryBlend = 0f;
                    return;
                }
            }
            else if (wasToolActionActive)
            {
                wasToolActionActive = false;
                if (UsesSeamlessCarryHandoff(lastActionMotionType))
                {
                    // These action profiles already finish at the long-tool carry direction.
                    // Recompute against the current hand instead of blending a local rotation
                    // captured under the previous Animator hand pose.
                    actionRecoveryBlend = 1f;
                }
                else
                {
                    actionExitLocalRotation = heldModelInstance.transform.localRotation;
                    actionRecoveryBlend = 0f;
                }

                lastActionMotionType = ToolMotionType.None;
            }

            Quaternion carryRotation = ResolveCarryLocalRotation();
            if (actionRecoveryBlend < 1f)
            {
                actionRecoveryBlend = Mathf.MoveTowards(
                    actionRecoveryBlend,
                    1f,
                    10f * Time.deltaTime);
                heldModelInstance.transform.localRotation = Quaternion.Slerp(
                    actionExitLocalRotation,
                    carryRotation,
                    Mathf.SmoothStep(0f, 1f, actionRecoveryBlend));
                return;
            }

            heldModelInstance.transform.localRotation = carryRotation;
        }

        private static bool UsesSeamlessCarryHandoff(ToolMotionType motionType)
        {
            return motionType == ToolMotionType.Pickaxe
                || motionType == ToolMotionType.Hoe;
        }

        private Quaternion ResolveCarryLocalRotation()
        {
            if (heldModelInstance == null || handAnchor == null)
            {
                return heldModelBaseLocalRotation;
            }

            if (requestedCarryType == HeldItemCarryType.None || requestedCarryWeight <= 0f)
            {
                return heldModelBaseLocalRotation;
            }

            Vector3 localDirection = requestedCarryType == HeldItemCarryType.Club
                ? clubCarryDirection
                : longToolCarryDirection;
            if (localDirection.sqrMagnitude < 0.0001f)
            {
                return heldModelBaseLocalRotation;
            }

            Quaternion targetLocalRotation = ResolveDirectionLocalRotation(localDirection, 0f);
            return Quaternion.Slerp(
                heldModelBaseLocalRotation,
                targetLocalRotation,
                requestedCarryWeight);
        }

        private Quaternion ResolveDirectionLocalRotation(Vector3 localDirection, float roll)
        {
            Quaternion baseWorldRotation = handAnchor.rotation * heldModelBaseLocalRotation;
            Vector3 currentAxis = baseWorldRotation * heldModelAxis;
            Vector3 targetAxis = transform.TransformDirection(localDirection.normalized);
            Quaternion targetWorldRotation =
                Quaternion.FromToRotation(currentAxis, targetAxis) * baseWorldRotation;
            if (!Mathf.Approximately(roll, 0f))
            {
                targetWorldRotation = Quaternion.AngleAxis(roll, targetAxis) * targetWorldRotation;
            }

            Quaternion targetLocalRotation =
                Quaternion.Inverse(handAnchor.rotation) * targetWorldRotation;
            return targetLocalRotation;
        }

        private void OnValidate()
        {
            heldCapsuleScaleCompensation = Mathf.Max(0.01f, heldCapsuleScaleCompensation);
        }

        private bool IsCapsule(string itemId)
        {
            HDY.Item.ItemData itemData = inventory != null ? inventory.FindItemData(itemId) : null;
            if (itemData != null)
            {
                return itemData.Category == HDY.Item.ItemCategory.Capsule;
            }

            return !string.IsNullOrWhiteSpace(itemId)
                && itemId.IndexOf("capsule", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ResolveReferences()
        {
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (movement != null && movement.Animator != null) animator = movement.Animator;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (toolAnimationController == null)
            {
                toolAnimationController = GetComponent<PlayerToolAnimationController>();
            }
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
            requestedCarryType = HeldItemCarryType.None;
            requestedCarryWeight = 0f;
            heldModelBaseLocalRotation = Quaternion.identity;
            heldModelAxis = Vector3.up;
            wasToolActionActive = false;
            actionEntryLocalRotation = Quaternion.identity;
            actionExitLocalRotation = Quaternion.identity;
            actionRecoveryBlend = 1f;
            lastActionMotionType = ToolMotionType.None;
            if (heldModelInstance == null) return;

            Destroy(heldModelInstance);
            heldModelInstance = null;
        }

        private static Vector3 ResolveHeldModelAxis(Transform heldRoot)
        {
            Renderer[] renderers = heldRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return Vector3.up;

            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 localCenter = heldRoot.InverseTransformPoint(combinedBounds.center);
            return localCenter.sqrMagnitude > 0.0001f
                ? localCenter.normalized
                : Vector3.up;
        }
    }
}
