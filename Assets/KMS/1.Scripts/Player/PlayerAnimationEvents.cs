using KMS.Audio;
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
            if (ShouldProcess(animationEvent))
            {
                KMSAudioService.PlayAt(GameSfxId.FootstepWalk, transform.position);
            }
        }

        public void OnFootstepRun(AnimationEvent animationEvent)
        {
            if (ShouldProcess(animationEvent))
            {
                KMSAudioService.PlayAt(GameSfxId.FootstepRun, transform.position);
            }
        }

        public void OnLand(AnimationEvent animationEvent)
        {
            if (ShouldProcess(animationEvent))
            {
                KMSAudioService.PlayAt(GameSfxId.Land, transform.position);
            }
        }

        private static bool ShouldProcess(AnimationEvent animationEvent)
        {
            return animationEvent == null
                || animationEvent.animatorClipInfo.weight >= 0.5f;
        }
    }
}
