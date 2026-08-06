using KMS.Audio;
using UnityEngine;

namespace KMS
{
    /// <summary>
    /// PlayerStats의 피격 이벤트를 플레이어 전용 애니메이션/사운드 피드백으로 연결한다.
    /// 체력 계산과 사망 처리는 각각 PlayerStats와 PlayerDeathController가 계속 담당한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerHitFeedbackController : MonoBehaviour
    {
        [SerializeField] private PlayerStats stats;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private Animator animator;

        private static readonly int HitHash = Animator.StringToHash("Hit");

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            if (stats != null) stats.DamageReceived += HandleDamageReceived;
        }

        private void OnDisable()
        {
            if (stats != null) stats.DamageReceived -= HandleDamageReceived;
        }

        private void ResolveReferences()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (movement == null) movement = GetComponent<PlayerMovement>();

            if (movement != null && movement.Animator != null)
                animator = movement.Animator;
            else if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
        }

        private void HandleDamageReceived(float amount, PlayerDamageType damageType)
        {
            if (amount <= 0f || stats == null) return;

            // 현재 일반 피격 피드백은 멤의 공격과 낙하 충격에만 사용한다.
            // 굶주림과 출처 미지정 피해는 체력만 감소시키고 모션/사운드를 재생하지 않는다.
            if (damageType != PlayerDamageType.MemAttack
                && damageType != PlayerDamageType.Fall)
            {
                return;
            }

            // PlayerStats는 CurrentHealth를 먼저 갱신한 뒤 DamageReceived를 보낸다.
            // 치명타에서 Hit과 Death가 같은 프레임에 경쟁하지 않도록 일반 피격 연출을 생략한다.
            if (stats.CurrentHealth <= 0f) return;

            if (animator != null)
            {
                animator.ResetTrigger(HitHash);
                animator.SetTrigger(HitHash);
            }

            KMSAudioService.Play2D(GameSfxId.PlayerDamaged);
        }
    }
}
