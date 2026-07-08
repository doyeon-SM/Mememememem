using UnityEngine;

namespace KMS
{
    public class PlayerAnimationEvents : MonoBehaviour
    {
        public void OnFootstepWalk(AnimationEvent animationEvent)
        {
            // TODO: 발소리 붙일 때 여기서 처리
        }

        public void OnFootstepRun(AnimationEvent animationEvent)
        {
            // TODO: 달리기 발소리 붙일 때 여기서 처리
        }

        public void OnLand(AnimationEvent animationEvent)
        {
            // TODO: 착지 소리 붙일 때 여기서 처리
        }
    }
}