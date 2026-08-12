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
        private Vector3 heldModelSecondaryAxis = Vector3.right;
        private float stableLongToolRollOffset;
        private HeldToolContactGeometry heldToolContactGeometry;
        private HeldItemCarryType requestedCarryType;
        private float requestedCarryWeight;
        private bool wasToolActionActive;
        private Quaternion actionEntryLocalRotation = Quaternion.identity;
        private Quaternion actionExitLocalRotation = Quaternion.identity;
        private float actionRecoveryBlend = 1f;
        private ToolMotionType lastActionMotionType = ToolMotionType.None;
        private Vector3 previousAxeSwingPlaneNormal;
        private bool hasPreviousAxeSwingPlaneNormal;

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
            heldToolContactGeometry = heldModelInstance.GetComponent<HeldToolContactGeometry>();
            // Contact geometry is action-only. Carry/idle keeps using the same
            // model axis it used before blade alignment was introduced.
            heldModelAxis = ResolveHeldModelAxis(heldModelInstance.transform);
            heldModelSecondaryAxis = ResolveHeldModelSecondaryAxis(heldModelAxis);
            CacheStableLongToolRollOffset();
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
                    hasPreviousAxeSwingPlaneNormal = false;
                    wasToolActionActive = true;
                }

                float normalizedTime = toolAnimationController.GetCurrentActionNormalizedTime();
                if (ToolActionGripPose.TryEvaluate(
                        toolAnimationController.CurrentMotionType,
                        normalizedTime,
                        out Vector3 direction,
                        out float roll))
                {
                    if (toolAnimationController.CurrentMotionType == ToolMotionType.Hoe)
                    {
                        roll += ToolActionGripPose.EvaluateHoeInwardRoll(normalizedTime);
                    }

                    bool stabilizeRoll = toolAnimationController.CurrentMotionType
                        == ToolMotionType.Axe
                        || toolAnimationController.CurrentMotionType == ToolMotionType.Hoe;
                    Quaternion target = stabilizeRoll
                        ? ResolveAxeActionLocalRotation(direction, roll, normalizedTime)
                        : ResolveDirectionLocalRotation(direction, roll);
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
            if (requestedCarryType == HeldItemCarryType.LongTool
                && requestedCarryWeight >= 0.999f)
            {
                CacheStableLongToolRollOffset();
            }
        }

        private static bool UsesSeamlessCarryHandoff(ToolMotionType motionType)
        {
            return motionType == ToolMotionType.Axe
                || motionType == ToolMotionType.Pickaxe
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

        private Quaternion ResolveDirectionLocalRotation(
            Vector3 localDirection,
            float roll,
            bool stabilizeRoll = false)
        {
            Quaternion baseWorldRotation = handAnchor.rotation * heldModelBaseLocalRotation;
            Vector3 currentAxis = baseWorldRotation * heldModelAxis;
            Vector3 targetAxis = transform.TransformDirection(localDirection.normalized);
            Quaternion targetWorldRotation =
                Quaternion.FromToRotation(currentAxis, targetAxis) * baseWorldRotation;
            if (stabilizeRoll)
            {
                targetWorldRotation = StabilizeToolRoll(
                    targetWorldRotation,
                    targetAxis,
                    roll);
            }
            else if (!Mathf.Approximately(roll, 0f))
            {
                targetWorldRotation = Quaternion.AngleAxis(roll, targetAxis) * targetWorldRotation;
            }

            Quaternion targetLocalRotation =
                Quaternion.Inverse(handAnchor.rotation) * targetWorldRotation;
            return targetLocalRotation;
        }

        private Quaternion StabilizeToolRoll(
            Quaternion alignedWorldRotation,
            Vector3 targetAxis,
            float profileRoll)
        {
            Vector3 desiredReference = ResolveSwingPlaneReference(targetAxis);
            Vector3 desiredSecondary = Quaternion.AngleAxis(
                stableLongToolRollOffset + profileRoll,
                targetAxis) * desiredReference;
            Vector3 alignedSecondary = Vector3.ProjectOnPlane(
                alignedWorldRotation * heldModelSecondaryAxis,
                targetAxis).normalized;
            float correction = Vector3.SignedAngle(
                alignedSecondary,
                desiredSecondary,
                targetAxis);
            return Quaternion.AngleAxis(correction, targetAxis) * alignedWorldRotation;
        }

        private Quaternion ResolveAxeActionLocalRotation(
            Vector3 localDirection,
            float profileRoll,
            float normalizedTime)
        {
            Quaternion shaftRotation = ResolveDirectionLocalRotation(
                localDirection,
                profileRoll,
                true);
            if (heldToolContactGeometry == null)
            {
                return shaftRotation;
            }

            float bladeWeight = ToolActionGripPose.EvaluateAxeBladeAlignmentWeight(normalizedTime);
            if (bladeWeight <= 0f)
            {
                return shaftRotation;
            }

            Quaternion baseWorldRotation = handAnchor.rotation * heldModelBaseLocalRotation;
            Vector3 contactAxis = baseWorldRotation
                * heldToolContactGeometry.BladeContactDirectionLocal;
            Vector3 targetAxis = transform.TransformDirection(localDirection.normalized);
            Quaternion contactWorldRotation =
                Quaternion.FromToRotation(contactAxis, targetAxis) * baseWorldRotation;

            Vector3 alignedBladeNormal = Vector3.ProjectOnPlane(
                contactWorldRotation * heldToolContactGeometry.BladeNormalLocal,
                targetAxis).normalized;
            Vector3 swingPlaneNormal = ResolveAxeSwingPlaneNormal(normalizedTime, targetAxis);
            if (!hasPreviousAxeSwingPlaneNormal)
            {
                if (Vector3.Dot(alignedBladeNormal, swingPlaneNormal) < 0f)
                {
                    swingPlaneNormal = -swingPlaneNormal;
                }

                hasPreviousAxeSwingPlaneNormal = true;
            }
            else if (Vector3.Dot(previousAxeSwingPlaneNormal, swingPlaneNormal) < 0f)
            {
                // A plane normal has two equivalent signs. Keep the sign chosen at
                // action entry so an asymmetric axe head cannot turn 180 degrees
                // when the closest-sign test crosses its midpoint.
                swingPlaneNormal = -swingPlaneNormal;
            }

            previousAxeSwingPlaneNormal = swingPlaneNormal;

            swingPlaneNormal = Quaternion.AngleAxis(
                profileRoll,
                targetAxis) * swingPlaneNormal;
            float correction = Vector3.SignedAngle(
                alignedBladeNormal,
                swingPlaneNormal,
                targetAxis);
            contactWorldRotation =
                Quaternion.AngleAxis(correction, targetAxis) * contactWorldRotation;
            Quaternion contactLocalRotation =
                Quaternion.Inverse(handAnchor.rotation) * contactWorldRotation;
            return Quaternion.Slerp(shaftRotation, contactLocalRotation, bladeWeight);
        }

        private Vector3 ResolveAxeSwingPlaneNormal(float normalizedTime, Vector3 targetAxis)
        {
            const float sampleOffset = 0.015f;
            ToolActionGripPose.TryEvaluate(
                ToolMotionType.Axe,
                Mathf.Clamp01(normalizedTime - sampleOffset),
                out Vector3 before,
                out _);
            ToolActionGripPose.TryEvaluate(
                ToolMotionType.Axe,
                Mathf.Clamp01(normalizedTime + sampleOffset),
                out Vector3 after,
                out _);

            Vector3 tangent = transform.TransformDirection(after - before);
            Vector3 normal = Vector3.Cross(targetAxis, tangent);
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector3.ProjectOnPlane(transform.forward, targetAxis);
            }

            return normal.normalized;
        }

        private void CacheStableLongToolRollOffset()
        {
            if (heldModelInstance == null) return;

            Quaternion worldRotation = heldModelInstance.transform.rotation;
            Vector3 worldAxis = worldRotation * heldModelAxis;
            Vector3 reference = ResolveSwingPlaneReference(worldAxis);
            Vector3 secondary = Vector3.ProjectOnPlane(
                worldRotation * heldModelSecondaryAxis,
                worldAxis).normalized;
            if (secondary.sqrMagnitude < 0.0001f) return;

            stableLongToolRollOffset = Vector3.SignedAngle(
                reference,
                secondary,
                worldAxis);
        }

        private Vector3 ResolveSwingPlaneReference(Vector3 toolAxis)
        {
            Vector3 reference = Vector3.ProjectOnPlane(transform.right, toolAxis);
            if (reference.sqrMagnitude < 0.0001f)
            {
                reference = Vector3.ProjectOnPlane(transform.up, toolAxis);
            }

            return reference.normalized;
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
            heldModelSecondaryAxis = Vector3.right;
            stableLongToolRollOffset = 0f;
            heldToolContactGeometry = null;
            previousAxeSwingPlaneNormal = Vector3.zero;
            hasPreviousAxeSwingPlaneNormal = false;
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

        private static Vector3 ResolveHeldModelSecondaryAxis(Vector3 primaryAxis)
        {
            Vector3 candidate = Mathf.Abs(Vector3.Dot(primaryAxis, Vector3.right)) < 0.85f
                ? Vector3.right
                : Vector3.forward;
            return Vector3.ProjectOnPlane(candidate, primaryAxis).normalized;
        }
    }
}
