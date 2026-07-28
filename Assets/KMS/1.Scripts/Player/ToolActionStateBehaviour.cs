using UnityEngine;

namespace KMS
{
    public sealed class ToolActionStateBehaviour : StateMachineBehaviour
    {
        [SerializeField] private ToolMotionType motionType;

        private PlayerToolAnimationController controller;

        public void SetMotionType(ToolMotionType value)
        {
            motionType = value;
        }

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            ResolveController(animator);
            controller?.NotifyToolActionEntered(motionType);
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            ResolveController(animator);
            controller?.NotifyToolActionExited(motionType);
        }

        private void ResolveController(Animator animator)
        {
            if (controller == null && animator != null)
            {
                controller = animator.GetComponentInParent<PlayerToolAnimationController>();
            }
        }
    }
}
