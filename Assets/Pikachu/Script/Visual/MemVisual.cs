// ============================================================================
// MemVisual.cs
// 멤 외형 및 애니메이션 처리 (Animator 기반)
//
// [담당자 안내]
// - Mem2_Rig.fbx에 내장된 애니메이션 클립을 Animator Controller로 제어합니다.
// - PlayXXX() 메서드가 Animator 파라미터를 설정합니다.
// - 피격 플래시(PlayHit)는 Animator와 무관하게 머티리얼 색상으로 처리합니다.
//
// [Animator Controller 설정 안내]
// Assets/Pikachu/Resource/Mem_AnimatorController 기준:
//   파라미터:
//     - AnimState (Int)  : 루프 상태 구분 (아래 AnimStateId 상수 참고)
//     - Attack   (Trigger): 공격 트리거 (Any State → Attack → Idle 로 설정)
//   상태 매핑:
//     AnimState == 0 → Ani_Mem_Idle
//     AnimState == 1 → Ani_Mem_Walk
//     AnimState == 2 → Ani_Mem_Run
//     AnimState == 3 → Ani_Mem_Interact
//     AnimState == 4 → Ani_Mem_Hungry
//     AnimState == 5 → Ani_Mem_Happy
//     Attack Trigger → Ani_Mem_Attack  (Has Exit Time: true → 완료 후 Idle 복귀)
// ============================================================================
using UnityEngine;
using System.Collections;

namespace MemSystem.Visual
{
    /// <summary>
    /// 멤의 비주얼을 담당하는 컴포넌트.
    /// Animator Controller와 연동하여 애니메이션 클립을 재생하고,
    /// 피격 플래시 등 코드 기반 시각 효과를 처리합니다.
    /// </summary>
    public class MemVisual : MonoBehaviour
    {
        // =================================================================
        // Animator 파라미터 이름 (Inspector에서 수정 가능)
        // =================================================================

        [Header("Animator 파라미터 이름")]
        [Tooltip("루프 상태를 구분하는 Int 파라미터 이름 (Animator Controller와 일치해야 함)")]
        [SerializeField] private string paramAnimState = "AnimState";

        [Tooltip("공격 트리거 파라미터 이름 (Animator Controller와 일치해야 함)")]
        [SerializeField] private string paramAttack = "Attack";

        [Tooltip("이동 속도 Float 파라미터 이름. Animator Controller의 Walk/Run State Speed Multiplier에 연결합니다.")]
        [SerializeField] private string paramSpeed  = "Speed";

        // =================================================================
        // AnimState 파라미터 값 상수
        // Animator Controller의 전환 조건 숫자와 반드시 일치해야 합니다.
        // =================================================================

        public static class AnimStateId
        {
            public const int Idle     = 0;
            public const int Walk     = 1;
            public const int Run      = 2;
            public const int Interact = 3;
            public const int Hungry   = 4;
            public const int Happy    = 5;
            // Attack 중 Any State -> Idle 강제 전환을 막기 위한 방어용 임시 값
            public const int Attack   = 99;
        }

        // =================================================================
        // 피격 플래시 설정
        // =================================================================

        [Header("피격 플래시 설정")]
        [Tooltip("피격 시 플래시 색상")]
        [SerializeField] private Color hitColor = Color.red;

        [Tooltip("피격 효과 지속 시간 (초)")]
        [SerializeField] private float hitDuration = 0.2f;

        // =================================================================
        // 애니메이션 속도 동기화 설정
        // =================================================================

        [Header("애니메이션 속도 동기화")]
        [Tooltip("Walk 클립이 설계된 이동 속도 (m/s). \n" +
                  "NavMesh walkSpeed와 직접 측정해서 입력하거나, \n" +
                  "Play Mode에서 미끼러짐이 없어질 때까지 조정하세요.")]
        [SerializeField] private float walkClipSpeed = 0.5f;

        [Tooltip("Run 클립이 설계된 이동 속도 (m/s).")]
        [SerializeField] private float runClipSpeed  = 1.5f;

        // =================================================================
        // 현재 애니메이션 상태 (외부 참조용)
        // =================================================================

        /// <summary>
        /// 현재 요청된 애니메이션 상태.
        /// Animator 내부 전환 상태와 다를 수 있습니다 (예: Attack 중 Idle 요청 무시).
        /// </summary>
        public enum AnimState
        {
            None,
            Idle,
            Walk,
            Run,
            Attack,
            Interact,
            Hungry,
            Happy
        }

        public AnimState CurrentAnimState { get; private set; } = AnimState.None;

        // =================================================================
        // 내부 참조
        // =================================================================

        private Animator animator;
        private GameObject currentModel;
        private Renderer[] modelRenderers;
        private Color[] originalColors;

        private Coroutine hitFlashCoroutine;

        // Animator 파라미터 해시 (성능 최적화: 문자열 → int 해시)
        private int hashAnimState;
        private int hashAttack;
        private int hashSpeed;

        // =================================================================
        // Unity Lifecycle
        // =================================================================

        private void Awake()
        {
            // 파라미터 해시 캐싱 (Animator.StringToHash는 매 프레임 호출 비용 절감)
            // Animator 참조는 SetupModel()에서 자식 모델 인스턴스화 후 획득합니다.
            hashAnimState = Animator.StringToHash(paramAnimState);
            hashAttack    = Animator.StringToHash(paramAttack);
            hashSpeed     = Animator.StringToHash(paramSpeed);
        }

        // =================================================================
        // 모델 설정
        // =================================================================

        /// <summary>
        /// MemData에 지정된 모델 프리팹을 생성하여 자식으로 붙입니다.
        /// Mem.Initialize()에서 호출됩니다.
        /// </summary>
        public void SetupModel(GameObject modelPrefab)
        {
            // 기존 모델 제거
            if (currentModel != null)
            {
                Destroy(currentModel);
            }

            if (modelPrefab == null) return;

            // 새 모델 생성
            currentModel = Instantiate(modelPrefab, transform);

            // 원본 프리팹의 회전값만 보존 (FBX 누움 방지)
            // 위치는 0으로 고정합니다 — 높이 오프셋은 NavMeshAgent.baseOffset으로 조정하세요.
            currentModel.transform.localPosition = Vector3.zero;
            currentModel.transform.localRotation = modelPrefab.transform.localRotation;

            // Animator는 인스턴스화된 자식 모델에서 찾습니다.
            // Mem2_Rig 프리팹에 Animator 컴포넌트 + Controller가 직접 연결되어 있어야 합니다.
            animator = currentModel.GetComponentInChildren<Animator>(includeInactive: true);
            if (animator == null)
            {
                Debug.LogWarning($"[MemVisual] Animator를 찾을 수 없습니다! " +
                                  $"모델 프리팹({modelPrefab.name})에 Animator 컴포넌트가 있는지 확인하세요.");
            }

            // 렌더러와 원래 색상 캐싱 (피격 플래시 용도)
            modelRenderers = currentModel.GetComponentsInChildren<Renderer>();

            if (modelRenderers != null && modelRenderers.Length > 0)
            {
                originalColors = new Color[modelRenderers.Length];
                for (int i = 0; i < modelRenderers.Length; i++)
                {
                    if (modelRenderers[i].material.HasProperty("_Color"))
                        originalColors[i] = modelRenderers[i].material.color;
                    else
                        originalColors[i] = Color.white;
                }
            }
        }

        /// <summary>
        /// 풀 반환 시 시각 효과를 리셋합니다.
        /// </summary>
        public void ResetVisual()
        {
            CurrentAnimState = AnimState.None;

            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
            }

            RestoreColors();

            // Animator를 Idle 상태로 리셋
            if (animator != null)
            {
                animator.SetInteger(hashAnimState, AnimStateId.Idle);
                animator.ResetTrigger(hashAttack);
            }
        }

        // =================================================================
        // 애니메이션 재생 API (MemAI 상태 클래스에서 호출)
        // =================================================================

        /// <summary>
        /// Idle — 기본 대기 (Ani_Mem_Idle 클립 재생).
        /// </summary>
        public void PlayIdle()
        {
            if (CurrentAnimState == AnimState.Attack) return;
            CurrentAnimState = AnimState.Idle;
            SetAnimState(AnimStateId.Idle);
        }

        /// <summary>
        /// Walk — 걷기 (Ani_Mem_Walk 클립 재생).
        /// Wander 상태에서 사용합니다.
        /// </summary>
        public void PlayWalk()
        {
            if (CurrentAnimState == AnimState.Attack) return;
            CurrentAnimState = AnimState.Walk;
            SetAnimState(AnimStateId.Walk);
        }

        /// <summary>
        /// Run — 뛰기 (Ani_Mem_Run 클립 재생).
        /// 추적(Combat) 또는 도주(Flee) 상태에서 사용합니다.
        /// </summary>
        public void PlayRun()
        {
            if (CurrentAnimState == AnimState.Attack) return;
            CurrentAnimState = AnimState.Run;
            SetAnimState(AnimStateId.Run);
        }

        /// <summary>
        /// Attack — 박치기 공격 (Ani_Mem_Attack 클립 재생).
        /// Trigger 파라미터를 사용합니다.
        /// Animator Controller에서 Has Exit Time: true로 설정하면 클립 완료 후 자동 복귀합니다.
        /// </summary>
        public void PlayAttack()
        {
            CurrentAnimState = AnimState.Attack;

            if (animator != null)
            {
                animator.SetTrigger(hashAttack);
                // [핵심 방어 코드] 
                // Any State -> Idle 전환 조건(AnimState == 0)이 Attack 도중 만족되어 
                // 강제로 모션이 뚝 끊기고 Idle로 넘어가는 현상(유니티 고질적 버그) 방지
                SetAnimState(AnimStateId.Attack);
            }

            // 공격 클립이 끝나면 Animator가 자동으로 Idle로 복귀합니다.
            // (Animator Controller: Attack State → Has Exit Time true → Idle 전환)
            StartCoroutine(WaitForAttackEnd());
        }

        /// <summary>
        /// 공격 모션 중 상태가 강제로 변경될 때(예: 피격, 도주, 포획) Attack 락을 해제합니다.
        /// </summary>
        public void CancelAttack()
        {
            if (CurrentAnimState == AnimState.Attack)
            {
                CurrentAnimState = AnimState.None;
            }
        }

        /// <summary>
        /// Interact — 상호작용 (Ani_Mem_Interact 클립 재생).
        /// </summary>
        public void PlayInteract()
        {
            if (CurrentAnimState == AnimState.Attack) return;
            CurrentAnimState = AnimState.Interact;
            SetAnimState(AnimStateId.Interact);
        }

        /// <summary>
        /// Hungry — 허기 고갈 (Ani_Mem_Hungry 클립 재생).
        /// MemStats.IsStarving == true 일 때 HungryState에서 호출됩니다.
        /// </summary>
        public void PlayHungry()
        {
            CurrentAnimState = AnimState.Hungry;
            SetAnimState(AnimStateId.Hungry);
        }

        /// <summary>
        /// Happy — 행복 모션 (Ani_Mem_Happy 클립 재생).
        /// TriggerHappy() 또는 HappyState에서 호출됩니다.
        /// </summary>
        public void PlayHappy()
        {
            if (CurrentAnimState == AnimState.Attack) return;
            CurrentAnimState = AnimState.Happy;
            SetAnimState(AnimStateId.Happy);
        }

        /// <summary>
        /// 피격 연출 — 잠시 빨갛게 변함.
        /// 애니메이션 상태와 무관하게 독립적으로 동작합니다.
        /// </summary>
        public void PlayHit()
        {
            if (hitFlashCoroutine != null) StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        // =================================================================
        // 내부 구현
        // =================================================================

        /// <summary>
        /// 실제 이동 속도를 Animator의 Speed Float 파라미터에 전달합니다.
        /// MemAI.Update()에서 매 프레임 호출합니다.
        ///
        /// [Animator Controller 설정]
        /// Walk State / Run State → Speed 체크박스 활성화 → Multiplier: Speed 파라미터 선택
        /// → 실제 속도에 비례하여 애니메이션 재생 속도가 자동 조정됩니다.
        /// </summary>
        /// <param name="speed">NavMeshAgent.velocity.magnitude 값</param>
        public void UpdateMovementSpeed(float speed)
        {
            if (animator == null) return;

            // 클립 설계 속도 기준으로 정규화
            // Walk/Run 상태일 때 클립 속도를 실제 이동 속도에 맞춰 보정합니다.
            float clipRef = (CurrentAnimState == AnimState.Run) ? runClipSpeed : walkClipSpeed;

            if (clipRef > 0f && speed > 0.05f)
            {
                // 실제 속도 / 클립 설계 속도 = 애니메이션 폰시 폐사 배율
                float normalizedSpeed = speed / clipRef;
                animator.SetFloat(hashSpeed, normalizedSpeed);
            }
            else
            {
                // 정지 상태: 속도 1.0 (클립은 실행되나 State가 Idle이므로 상관없음)
                animator.SetFloat(hashSpeed, 1.0f);
            }
        }

        /// <summary>
        /// Animator의 AnimState Int 파라미터를 설정합니다.
        /// </summary>
        private void SetAnimState(int stateId)
        {
            if (animator == null) return;
            animator.SetInteger(hashAnimState, stateId);
        }

        /// <summary>
        /// Attack Trigger 발동 후 클립이 끝나길 기다렸다가 CurrentAnimState를 Idle로 복귀.
        /// </summary>
        private IEnumerator WaitForAttackEnd()
        {
            // 트리거가 Animator에 반영될 때까지 1프레임 대기
            yield return null;

            if (animator == null) yield break;

            float attackLength = 1.2f; // 클립 길이를 알아내지 못했을 때의 기본 대기 시간

            // 1. 애니메이터가 다음 상태(Attack)로 전환을 시작했는지 감지하여 클립의 진짜 길이를 알아냄
            float transitionWait = 0f;
            while (animator != null && transitionWait < 0.5f)
            {
                // 트리거 발동 후 전환(Transition, 블렌딩)이 시작되었다면
                if (animator.IsInTransition(0))
                {
                    AnimatorStateInfo nextInfo = animator.GetNextAnimatorStateInfo(0);
                    if (nextInfo.length > 0f)
                    {
                        attackLength = nextInfo.length; // 다음 재생될 클립(공격)의 정확한 길이를 가져옴
                        break;
                    }
                }
                else
                {
                    // 만약 Transition(블렌딩) 없이 즉시 상태가 바뀌었다면
                    AnimatorStateInfo currentInfo = animator.GetCurrentAnimatorStateInfo(0);
                    if (currentInfo.IsTag("Attack") || currentInfo.IsName("Attack") || currentInfo.IsName("Ani_Mem_Attack"))
                    {
                        attackLength = currentInfo.length;
                        break;
                    }
                }

                transitionWait += Time.deltaTime;
                yield return null;
            }

            // 2. 알아낸 클립의 길이(또는 기본값 1.2초)만큼 여유롭게 대기
            // 여유를 위해 클립 길이의 95% 정도만 대기 (전환 블렌딩 고려)
            yield return new WaitForSeconds(attackLength * 0.95f);

            // Attack이 끝나면 CurrentAnimState 복귀
            if (CurrentAnimState == AnimState.Attack)
            {
                CurrentAnimState = AnimState.Idle;
                // 공격이 무사히 끝났으므로 다시 정상적인 Idle 상태로 파라미터 복구
                SetAnimState(AnimStateId.Idle);
            }
        }

        private IEnumerator HitFlashRoutine()
        {
            SetColors(hitColor);

            yield return new WaitForSeconds(hitDuration);

            RestoreColors();
        }

        private void SetColors(Color color)
        {
            if (modelRenderers == null) return;

            foreach (var r in modelRenderers)
            {
                if (r != null && r.material.HasProperty("_Color"))
                    r.material.color = color;
            }
        }

        private void RestoreColors()
        {
            if (modelRenderers == null || originalColors == null) return;

            for (int i = 0; i < modelRenderers.Length; i++)
            {
                if (modelRenderers[i] != null && modelRenderers[i].material.HasProperty("_Color"))
                    modelRenderers[i].material.color = originalColors[i];
            }
        }
    }
}
