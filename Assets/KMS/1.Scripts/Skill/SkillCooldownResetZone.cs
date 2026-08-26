using UnityEngine;

namespace KMS.Combat
{
    /// <summary>
    /// [멤] 보스씬의 특정 영역에 배치해서, 플레이어가 들어오면 스킬 쿨타임을 전부 초기화해주는
    /// 트리거 존. Collider를 Is Trigger로 설정하고 이 컴포넌트를 붙이면 된다(Reset에서 자동으로
    /// isTrigger를 켜준다). 저장/불러오기와는 무관한 순수 런타임(씬 내) 리셋 기능이다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SkillCooldownResetZone : MonoBehaviour
    {
        [Tooltip("이 태그를 가진 오브젝트만 반응한다.")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("체크 해제 시 한 번 초기화한 뒤로는 다시 반응하지 않는 1회성 존이 된다.")]
        [SerializeField] private bool reusable = true;

        private bool consumed;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!reusable && consumed) return;
            if (!other.CompareTag(playerTag)) return;

            var controller = other.GetComponentInParent<PlayerWeaponSkillController>();
            if (controller == null) return;

            controller.ClearAllCooldowns();
            consumed = true;
        }
    }
}
