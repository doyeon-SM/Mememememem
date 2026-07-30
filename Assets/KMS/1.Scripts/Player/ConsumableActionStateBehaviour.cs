using UnityEngine;

namespace KMS
{
    public sealed class ConsumableActionStateBehaviour : StateMachineBehaviour
    {
        [SerializeField, Min(0.01f)] private float completionNormalizedTime = 1f;

        private PlayerConsumableController controller;
        private bool completionSent;

        public void SetCompletionNormalizedTime(float value)
        {
            completionNormalizedTime = Mathf.Max(0.01f, value);
        }

        public override void OnStateEnter(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            completionSent = false;
            ResolveController(animator);
            controller?.NotifyConsumeActionEntered();
        }

        public override void OnStateUpdate(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            TryComplete(animator, stateInfo);
        }

        public override void OnStateExit(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            TryComplete(animator, stateInfo);
            ResolveController(animator);
            controller?.NotifyConsumeActionExited();
            completionSent = false;
        }

        private void TryComplete(Animator animator, AnimatorStateInfo stateInfo)
        {
            if (completionSent || stateInfo.normalizedTime < completionNormalizedTime) return;

            completionSent = true;
            ResolveController(animator);
            controller?.NotifyConsumeActionCompleted();
        }

        private void ResolveController(Animator animator)
        {
            if (controller == null && animator != null)
            {
                controller = animator.GetComponentInParent<PlayerConsumableController>();
            }
        }
    }
}
