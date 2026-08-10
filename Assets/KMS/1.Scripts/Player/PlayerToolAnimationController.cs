using System;
using HDY.Item;
using KGH.Data;
using UnityEngine;

namespace KMS
{
    [DisallowMultipleComponent]
    public sealed class PlayerToolAnimationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private Animator animator;

        [Header("Club Item IDs")]
        [SerializeField] private string[] clubItemIds =
        {
            "tool_shabby_club",
            "tool_club",
            "tool_decent_club"
        };

        [Header("Action Request")]
        [SerializeField, Min(0.1f)] private float stateEntryTimeout = 0.75f;
        [SerializeField, Min(0f)] private float stationarySpeedThreshold = 0.1f;

        public bool IsToolActionPlaying => actionRequested || actionStateActive;
        public bool IsToolActionStateActive => actionStateActive;
        public ToolMotionType CurrentMotionType { get; private set; }
        public event Action ToolActionStarted;
        public event Action ToolActionEnded;

        private static readonly int LocomotionStateHash = Animator.StringToHash("Locomotion");
        private static readonly int ToolActionHash = Animator.StringToHash("ToolAction");
        private static readonly int ToolMotionTypeHash = Animator.StringToHash("ToolMotionType");
        private static readonly int ToolActionPlaybackRateHash =
            Animator.StringToHash("ToolActionPlaybackRate");

        private bool actionRequested;
        private bool actionStateActive;
        private bool movementLocked;
        private float requestTime;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (!actionRequested || actionStateActive) return;
            if (Time.unscaledTime - requestTime <= stateEntryTimeout) return;

            CancelPendingAction();
        }

        private void OnDisable()
        {
            CancelToolAction();
        }

        public bool TryPlay(ItemData itemData, float actionDuration)
        {
            ToolMotionType motionType = ResolveMotionType(itemData);
            if (motionType == ToolMotionType.None
                || animator == null
                || IsToolActionPlaying
                || actionDuration <= 0f
                || !CanStartToolAction())
            {
                return false;
            }

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0) || currentState.shortNameHash != LocomotionStateHash)
            {
                return false;
            }

            CurrentMotionType = motionType;
            actionRequested = true;
            requestTime = Time.unscaledTime;
            SetMovementLocked(true);
            ToolActionStarted?.Invoke();

            animator.ResetTrigger(ToolActionHash);
            animator.SetFloat(ToolActionPlaybackRateHash, 1f / actionDuration);
            animator.SetInteger(ToolMotionTypeHash, (int)motionType);
            animator.SetTrigger(ToolActionHash);
            return true;
        }

        public ToolMotionType ResolveMotionType(ItemData itemData)
        {
            if (itemData == null || itemData.Category != ItemCategory.Tool)
            {
                return ToolMotionType.None;
            }

            // Club tiers use different catalog ObjectType values for harvesting,
            // but they must all keep the club grip and swing animation.
            if (IsClub(itemData.Item_ID))
            {
                return ToolMotionType.Club;
            }

            switch (itemData.ObjectType)
            {
                case ObjectType.Tree:
                    return ToolMotionType.Axe;
                case ObjectType.Bush:
                    return ToolMotionType.Hoe;
                case ObjectType.Stone:
                    return ToolMotionType.Pickaxe;
                case ObjectType.None:
                default:
                    return ToolMotionType.None;
            }
        }

        public void NotifyToolActionEntered(ToolMotionType motionType)
        {
            CurrentMotionType = motionType;
            actionRequested = false;
            actionStateActive = true;
        }

        public void NotifyToolActionExited(ToolMotionType motionType)
        {
            if (CurrentMotionType != motionType) return;

            actionRequested = false;
            actionStateActive = false;
            CurrentMotionType = ToolMotionType.None;
            SetMovementLocked(false);
            ToolActionEnded?.Invoke();
        }

        public void CancelToolAction()
        {
            bool hadAction = IsToolActionPlaying;
            if (animator != null)
            {
                animator.ResetTrigger(ToolActionHash);
            }

            actionRequested = false;
            actionStateActive = false;
            CurrentMotionType = ToolMotionType.None;
            SetMovementLocked(false);
            if (hadAction) ToolActionEnded?.Invoke();
        }

        private void CancelPendingAction()
        {
            bool hadAction = actionRequested;
            if (animator != null)
            {
                animator.ResetTrigger(ToolActionHash);
            }

            actionRequested = false;
            CurrentMotionType = ToolMotionType.None;
            SetMovementLocked(false);
            if (hadAction) ToolActionEnded?.Invoke();
        }

        public float GetCurrentActionNormalizedTime()
        {
            if (!actionStateActive || animator == null) return 0f;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsTag("ToolAction")) state = next;
            }

            return Mathf.Clamp01(state.normalizedTime);
        }

        private bool CanStartToolAction()
        {
            if (movement == null) return true;

            return movement.IsMovementEnabled
                && !movement.IsDead
                && !movement.IsOnLadder
                && !movement.IsSprinting
                && movement.CurrentSpeed <= stationarySpeedThreshold;
        }

        private void SetMovementLocked(bool locked)
        {
            if (movementLocked == locked) return;

            movementLocked = locked;
            if (movement != null)
            {
                movement.SetMovementBlocked(this, locked);
            }
        }

        private bool IsClub(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || clubItemIds == null)
            {
                return false;
            }

            for (int i = 0; i < clubItemIds.Length; i++)
            {
                if (string.Equals(itemId, clubItemIds[i], System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveReferences()
        {
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (movement != null && movement.Animator != null) animator = movement.Animator;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
        }

        private void OnValidate()
        {
            stateEntryTimeout = Mathf.Max(0.1f, stateEntryTimeout);
            stationarySpeedThreshold = Mathf.Max(0f, stationarySpeedThreshold);
        }
    }
}
