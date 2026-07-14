using UnityEngine;

namespace KMS
{
    public class PlayerAnimationEvents : MonoBehaviour
    {
        private PlayerCapsuleThrowController capsuleThrowController;

        private void Awake()
        {
            capsuleThrowController = GetComponentInParent<PlayerCapsuleThrowController>();
        }

        public void OnCapsuleRelease()
        {
            if (capsuleThrowController == null)
            {
                capsuleThrowController = GetComponentInParent<PlayerCapsuleThrowController>();
            }

            capsuleThrowController?.ReleaseCapsuleFromAnimationEvent();
        }

        public void OnCapsuleThrowFinished()
        {
            if (capsuleThrowController == null)
            {
                capsuleThrowController = GetComponentInParent<PlayerCapsuleThrowController>();
            }

            capsuleThrowController?.FinishThrowFromAnimationEvent();
        }

        public void OnFootstepWalk(AnimationEvent animationEvent)
        {
            // TODO: �߼Ҹ� ���� �� ���⼭ ó��
        }

        public void OnFootstepRun(AnimationEvent animationEvent)
        {
            // TODO: �޸��� �߼Ҹ� ���� �� ���⼭ ó��
        }

        public void OnLand(AnimationEvent animationEvent)
        {
            // TODO: ���� �Ҹ� ���� �� ���⼭ ó��
        }
    }
}
