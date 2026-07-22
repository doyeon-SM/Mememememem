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
            public const int Chop     = 6;  // 벌목 (axe)
            public const int Craft    = 7;  // 제작 (hammer)
            public const int Farm     = 8;  // 밭 (handsickle)
            public const int Cook     = 9;  // 요리 (pan)
            public const int Generate = 10; // 발전기 (windmill)
            public const int Mine     = 11; // 채굴 (pickaxe)
            // Attack 중 Any State -> Idle 강제 전환을 막기 위한 방어용 임시 값
            public const int Attack   = 99;
        }

        // =================================================================
        // 피격 연출 설정
        // =================================================================

        [Header("피격 연출 설정")]
        [Tooltip("피격 시 플래시 색상")]
        [SerializeField] private Color hitColor = Color.red;

        [Tooltip("피격 효과 지속 시간 (초)")]
        [SerializeField] private float hitDuration = 0.2f;

        [Tooltip("피격 시 뒤로 밀리는 거리")]
        [SerializeField] private float hitPushbackDistance = 0.1f;

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
        // 도구 프랍(Prop) 설정
        // =================================================================

        [System.Serializable]
        public class PropSetting
        {
            [Tooltip("장착할 3D 모델 (FBX 또는 프리팹)")]
            public GameObject prefab;
            
            [Tooltip("손 기준 위치 오프셋")]
            public Vector3 positionOffset = Vector3.zero;
            
            [Tooltip("손 기준 회전 오프셋 (Euler Angles)")]
            public Vector3 rotationOffset = Vector3.zero;
        }

        [Header("도구 프랍 설정 (작업 시 장착)")]
        [Tooltip("벌목 도끼 설정")]
        [SerializeField] private PropSetting propAxe = new PropSetting();

        [Tooltip("제작 망치 설정")]
        [SerializeField] private PropSetting propHammer = new PropSetting();

        [Tooltip("밭 낫 설정")]
        [SerializeField] private PropSetting propHandSickle = new PropSetting();

        [Tooltip("요리 팬 설정")]
        [SerializeField] private PropSetting propPan = new PropSetting();

        [Tooltip("채굴 곡괭이 설정")]
        [SerializeField] private PropSetting propPickaxe = new PropSetting();

        [Tooltip("발전기 바람개비 설정")]
        [SerializeField] private PropSetting propWindmill = new PropSetting();

        [Tooltip("도구를 부착할 뼈 이름 (기본: LowerArm.L)")]
        [SerializeField] private string mountBoneName = "LowerArm.L";

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
            Happy,
            Chop,
            Craft,
            Farm,
            Cook,
            Generate,
            Mine
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

        /// <summary>PlayCaptureAbsorb 실행 중인 코루틴 핸들 (ResetVisual에서 중단용)</summary>
        private Coroutine captureAbsorbCoroutine;

        /// <summary>PlayCaptureEject 실행 중인 코루틴 핸들 (ResetVisual에서 중단용)</summary>
        private Coroutine captureEjectCoroutine;

        private Transform propMountPoint;
        private GameObject currentPropInstance;

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

            // 도구 장착점 찾기
            propMountPoint = FindMountPoint(currentModel.transform, mountBoneName);
            if (propMountPoint == null)
            {
                Debug.LogWarning($"[MemVisual] 도구 장착점 '{mountBoneName}'을(를) 찾을 수 없습니다.");
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

            // 진행 중인 피격 플래시 중단
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
                
                // 피격 연출 중단 시 모델 위치 원상 복구
                if (currentModel != null)
                {
                    currentModel.transform.localPosition = Vector3.zero;
                }
            }

            // 진행 중인 포획 흡수 연출 중단
            if (captureAbsorbCoroutine != null)
            {
                StopCoroutine(captureAbsorbCoroutine);
                captureAbsorbCoroutine = null;
            }

            // 진행 중인 포획 실패 탈출 연출 중단
            if (captureEjectCoroutine != null)
            {
                StopCoroutine(captureEjectCoroutine);
                captureEjectCoroutine = null;
            }

            RestoreColors();
            UnequipProp();

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
            UnequipProp();
            CurrentAnimState = AnimState.Idle;
            SetAnimState(AnimStateId.Idle);
        }

        public void PlayWalk()
        {
            if (CurrentAnimState == AnimState.Attack) return;
            UnequipProp();
            CurrentAnimState = AnimState.Walk;
            SetAnimState(AnimStateId.Walk);
        }

        public void PlayRun()
        {
            if (CurrentAnimState == AnimState.Attack) return;
            UnequipProp();
            CurrentAnimState = AnimState.Run;
            SetAnimState(AnimStateId.Run);
        }

        public void PlayAttack()
        {
            CurrentAnimState = AnimState.Attack;
            UnequipProp();

            if (animator != null)
            {
                animator.SetTrigger(hashAttack);
                SetAnimState(AnimStateId.Attack);
            }

            StartCoroutine(WaitForAttackEnd());
        }

        public void CancelAttack()
        {
            if (CurrentAnimState == AnimState.Attack)
            {
                CurrentAnimState = AnimState.None;
            }
        }

        public void PlayInteract()
        {
            if (CurrentAnimState == AnimState.Attack) return;
            UnequipProp();
            CurrentAnimState = AnimState.Interact;
            SetAnimState(AnimStateId.Interact);
        }

        public void PlayHungry()
        {
            UnequipProp();
            CurrentAnimState = AnimState.Hungry;
            SetAnimState(AnimStateId.Hungry);
        }

        public void PlayHappy()
        {
            if (CurrentAnimState == AnimState.Attack) return;
            UnequipProp();
            CurrentAnimState = AnimState.Happy;
            SetAnimState(AnimStateId.Happy);
        }

        public void PlayChop()
        {
            if (CurrentAnimState == AnimState.Attack) return;
            EquipProp(propAxe);
            CurrentAnimState = AnimState.Chop;
            SetAnimState(AnimStateId.Chop);
        }

        public void PlayCraft()
        {
            if (CurrentAnimState == AnimState.Attack) return;
            EquipProp(propHammer);
            CurrentAnimState = AnimState.Craft;
            SetAnimState(AnimStateId.Craft);
        }

        public void PlayFarm()
        {
            if (CurrentAnimState == AnimState.Attack) return;
            EquipProp(propHandSickle);
            CurrentAnimState = AnimState.Farm;
            SetAnimState(AnimStateId.Farm);
        }

        public void PlayCook()
        {
            if (CurrentAnimState == AnimState.Attack) return;
            EquipProp(propPan);
            CurrentAnimState = AnimState.Cook;
            SetAnimState(AnimStateId.Cook);
        }

        public void PlayGenerate()
        {
            if (CurrentAnimState == AnimState.Attack) return;
            EquipProp(propWindmill);
            CurrentAnimState = AnimState.Generate;
            SetAnimState(AnimStateId.Generate);
        }

        public void PlayMine()
        {
            if (CurrentAnimState == AnimState.Attack) return;
            EquipProp(propPickaxe);
            CurrentAnimState = AnimState.Mine;
            SetAnimState(AnimStateId.Mine);
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

        /// <summary>
        /// 포획 흡수 연출 — 캡슐에 빨려들어가는 연출.
        ///
        /// 연출 내용:
        /// 1. 빛남 효과: 머티리얼 색상을 흰색으로 플래시
        /// 2. 이동: targetPosition(캡슐 위치) 방향으로 서서히 이동
        /// 3. 축소: EaseInBack 커브로 스케일을 0으로 수렴
        ///
        /// [CapturedState에서 호출됩니다. 외부에서 직접 호출하지 마세요.]
        /// Mem.NotifyCaptureBallHit() → CapturedState.Enter() → 이 메서드 순으로 호출됩니다.
        /// </summary>
        /// <param name="targetPosition">빨려들어갈 목표 위치 (캡슐의 월드 좌표)</param>
        /// <param name="duration">연출 전체 시간 (초)</param>
        public void PlayCaptureAbsorb(Vector3 targetPosition, float duration = 0.6f)
        {
            // 진행 중인 연출이 있으면 중단하고 새로 시작
            if (captureAbsorbCoroutine != null) StopCoroutine(captureAbsorbCoroutine);
            if (captureEjectCoroutine  != null) StopCoroutine(captureEjectCoroutine);
            captureAbsorbCoroutine = StartCoroutine(CaptureAbsorbRoutine(targetPosition, duration));
        }

        /// <summary>
        /// 포획 실패 탈출 연출 — 캡슐에서 멤이 다시 튀어나오는 연출.
        ///
        /// 연출 내용:
        /// 1. 스케일을 0에서 EaseOutBack 커브로 빠르게 팽창
        /// 2. 빛남 효과: 흰색에서 원색으로 복원되며 등장
        /// 3. 착지 바운스: Y축 소폭 점프 후 착지 (캡슐에서 튀어나오는 느낌)
        ///
        /// [Mem.OnCaptureFail()에서 호출됩니다. 외부에서 직접 호출하지 마세요.]
        /// </summary>
        /// <param name="fromPosition">탈출 시작 위치 (캡슐의 월드 좌표)</param>
        /// <param name="duration">연출 전체 시간 (초)</param>
        public void PlayCaptureEject(Vector3 fromPosition, float duration = 0.5f)
        {
            // 진행 중인 연출이 있으면 중단하고 새로 시작
            if (captureAbsorbCoroutine != null) StopCoroutine(captureAbsorbCoroutine);
            if (captureEjectCoroutine  != null) StopCoroutine(captureEjectCoroutine);
            captureEjectCoroutine = StartCoroutine(CaptureEjectRoutine(fromPosition, duration));
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
                // 실제 속도 / 클립 설계 속도 = 애니메이션 재생 배속
                float normalizedSpeed = speed / clipRef;
                animator.SetFloat(hashSpeed, normalizedSpeed);
            }
            else
            {
                // 정지 상태: 속도 1.0 (클립은 실행되나 State가 Idle이므로 상관없음)
                animator.SetFloat(hashSpeed, 1.0f);
            }
        }

        // =================================================================
        // 도구 프랍(Prop) 제어
        // =================================================================

        private void EquipProp(PropSetting setting)
        {
            UnequipProp();

            if (setting == null || setting.prefab == null || propMountPoint == null) return;

            currentPropInstance = Instantiate(setting.prefab, propMountPoint);
            currentPropInstance.transform.localPosition = setting.positionOffset;
            currentPropInstance.transform.localRotation = Quaternion.Euler(setting.rotationOffset);
        }

        private void UnequipProp()
        {
            if (currentPropInstance != null)
            {
                Destroy(currentPropInstance);
                currentPropInstance = null;
            }
        }

        private Transform FindMountPoint(Transform root, string boneName)
        {
            if (root.name.Contains(boneName)) return root;

            foreach (Transform child in root)
            {
                Transform found = FindMountPoint(child, boneName);
                if (found != null) return found;
            }
            return null;
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
            float timer = 0f;
            Vector3 originalLocalPos = Vector3.zero;

            // 모델 위치 백업
            Transform targetTransform = currentModel != null ? currentModel.transform : null;
            if (targetTransform != null)
            {
                originalLocalPos = targetTransform.localPosition;
            }

            while (timer < hitDuration)
            {
                timer += Time.deltaTime;
                float progress = Mathf.Clamp01(timer / hitDuration);

                // 1. 색상 처리: 원래색상 -> 빨간색 -> 원래색상 (PingPong)
                // 강도를 0.5f로 줄여서 너무 쨍한 빨간색이 되지 않게 부드럽게 섞습니다.
                float colorProgress = Mathf.PingPong(progress * 2f, 1f) * 0.5f;
                LerpColorsToHit(colorProgress);

                // 2. 밀림 처리
                if (targetTransform != null)
                {
                    // Sin 궤적: 0 -> 1 -> 0 (시작 시 뒤로 밀렸다가 원위치)
                    float pushProgress = Mathf.Sin(progress * Mathf.PI);

                    // 로컬 Z축(뒤쪽)으로 살짝 밀림
                    Vector3 pushbackOffset = Vector3.back * (hitPushbackDistance * pushProgress);

                    targetTransform.localPosition = originalLocalPos + pushbackOffset;
                }

                yield return null;
            }

            // 효과 종료 후 원상태로 복구
            if (targetTransform != null)
            {
                targetTransform.localPosition = originalLocalPos;
            }
            RestoreColors();
            hitFlashCoroutine = null;
        }

        private void LerpColorsToHit(float t)
        {
            if (modelRenderers == null || originalColors == null) return;

            for (int i = 0; i < modelRenderers.Length; i++)
            {
                if (modelRenderers[i] != null && modelRenderers[i].material.HasProperty("_Color"))
                {
                    modelRenderers[i].material.color = Color.Lerp(originalColors[i], hitColor, t);
                }
            }
        }

        // -----------------------------------------------------------------
        // 포획 연출 코루틴 — 내부 구현
        // -----------------------------------------------------------------

        /// <summary>
        /// 포획 흡수 코루틴 내부 구현.
        ///
        /// [페이즈 구성]
        /// Phase A (40%): 빛남 플래시 — 흰색으로 빠르게 변함
        /// Phase B (60%): 이동+축소 — targetPosition으로 이동하며 스케일 0으로 수렴 (EaseInBack)
        /// 완료 후: 스케일·위치·색상 복원 (Object Pool 재사용 대비)
        /// </summary>
        private IEnumerator CaptureAbsorbRoutine(Vector3 targetPosition, float duration)
        {
            Vector3 originalScale    = transform.localScale;
            Vector3 originalPosition = transform.position;

            // ----------------------------------------------------------
            // Phase A: 빛남 플래시 (전체 시간의 40%)
            // 점진적으로 밝은 무지개색(Glow)으로 포화되며 임팩트를 표현
            // ----------------------------------------------------------
            float flashDuration = duration * 0.4f;
            float flashTimer    = 0f;

            while (flashTimer < flashDuration)
            {
                flashTimer += Time.deltaTime;

                // 0→1로 플래시 강도 증가
                float t = Mathf.Clamp01(flashTimer / flashDuration);
                
                // 알록달록한 무지개색 생성 (시간에 따라 Hue 변경)
                float hue = (Time.time * 2f) % 1f;
                Color rainbowGlow = Color.HSVToRGB(hue, 0.5f, 1f) * 3f; // 채도를 낮춰서 파스텔톤 느낌
                
                if (modelRenderers != null && originalColors != null)
                {
                    for (int i = 0; i < modelRenderers.Length; i++)
                    {
                        if (modelRenderers[i] != null && modelRenderers[i].material.HasProperty("_Color"))
                            modelRenderers[i].material.color = Color.Lerp(originalColors[i], rainbowGlow, t);
                    }
                }

                yield return null;
            }

            // ----------------------------------------------------------
            // Phase B: 이동 + 축소 (전체 시간의 60%)
            // EaseInBack: 살짝 반대 방향으로 당긴 후 빠르게 수축
            // ----------------------------------------------------------
            float absorbDuration = duration * 0.6f;
            float absorbTimer    = 0f;

            while (absorbTimer < absorbDuration)
            {
                absorbTimer += Time.deltaTime;

                float progress = Mathf.Clamp01(absorbTimer / absorbDuration);
                float eased    = EaseInBack(progress);

                // 캡슐 방향으로 서서히 이동
                transform.position = Vector3.Lerp(originalPosition, targetPosition, eased);

                // 스케일을 0으로 축소
                float scale = Mathf.Lerp(1f, 0f, eased);
                transform.localScale = originalScale * Mathf.Max(scale, 0.001f);

                // 축소 중에도 계속 알록달록하게 빛나도록 유지
                float hue = (Time.time * 2f) % 1f;
                Color rainbowGlow = Color.HSVToRGB(hue, 0.5f, 1f) * 3f;
                SetColors(rainbowGlow);

                yield return null;
            }

            // ----------------------------------------------------------
            // 완료: 위치·스케일·색상 복원 (Object Pool 반환 후 재사용 대비)
            // ----------------------------------------------------------
            transform.position   = originalPosition;
            transform.localScale = originalScale;
            RestoreColors();

            captureAbsorbCoroutine = null;
        }

        /// <summary>
        /// 포획 실패 탈출 코루틴 내부 구현.
        ///
        /// [페이즈 구성]
        /// Phase A (40%): 팝업 — 스케일 0에서 EaseOutBack으로 빠르게 확장 + 흰색→원색 복원
        /// Phase B (30%): 바운스 상승 — 약간 위로 점프 (캡슐에서 튀어나오는 느낌)
        /// Phase C (30%): 바운스 하강 — 원위치로 착지
        /// </summary>
        private IEnumerator CaptureEjectRoutine(Vector3 fromPosition, float duration)
        {
            Vector3 originalScale    = transform.localScale;
            Vector3 originalPosition = transform.position;

            // 시작 시 스케일을 0으로, 위치를 캡슐 위치로 순간 이동
            transform.localScale = Vector3.zero;
            transform.position   = fromPosition;

            // ----------------------------------------------------------
            // Phase A: 팝업 확장 (전체 시간의 40%)
            // EaseOutBack: 목표를 살짝 초과한 후 바운스로 정착
            // ----------------------------------------------------------
            float popDuration = duration * 0.4f;
            float popTimer    = 0f;

            while (popTimer < popDuration)
            {
                popTimer += Time.deltaTime;

                float progress = Mathf.Clamp01(popTimer / popDuration);
                float eased    = EaseOutBack(progress);

                // 스케일 0 → 원본 크기로 확장
                transform.localScale = originalScale * eased;

                // 위치를 캡슐 위치 → 원위치로 이동
                transform.position = Vector3.Lerp(fromPosition, originalPosition, progress);

                // 알록달록한 무지개색에서 원래 색상으로 복원
                float hue = (Time.time * 2f) % 1f;
                Color rainbowGlow = Color.HSVToRGB(hue, 0.5f, 1f) * 3f;

                if (modelRenderers != null && originalColors != null)
                {
                    for (int i = 0; i < modelRenderers.Length; i++)
                    {
                        if (modelRenderers[i] != null && modelRenderers[i].material.HasProperty("_Color"))
                            modelRenderers[i].material.color = Color.Lerp(rainbowGlow, originalColors[i], progress);
                    }
                }

                yield return null;
            }

            // 팝업 완료 후 정확한 원위치·원색 고정
            transform.localScale = originalScale;
            transform.position   = originalPosition;
            RestoreColors();

            // ----------------------------------------------------------
            // Phase B: 바운스 상승 (전체 시간의 30%)
            // 캡슐에서 튀어나와 공중으로 솟아오르는 느낌
            // ----------------------------------------------------------
            float bounceHeight   = 0.4f;         // 점프 최고 높이 (m)
            float bounceDuration = duration * 0.3f;
            float bounceTimer    = 0f;

            while (bounceTimer < bounceDuration)
            {
                bounceTimer += Time.deltaTime;

                float t = Mathf.Clamp01(bounceTimer / bounceDuration);

                // 상승: Sin 커브로 부드럽게 위로 이동
                float yOffset = Mathf.Sin(t * Mathf.PI * 0.5f) * bounceHeight;
                transform.position = originalPosition + Vector3.up * yOffset;

                yield return null;
            }

            // ----------------------------------------------------------
            // Phase C: 바운스 하강 (전체 시간의 30%)
            // 최고점에서 원위치로 착지
            // ----------------------------------------------------------
            float landDuration = duration * 0.3f;
            float landTimer    = 0f;
            Vector3 peakPosition = transform.position; // 최고점 위치

            while (landTimer < landDuration)
            {
                landTimer += Time.deltaTime;

                float t = Mathf.Clamp01(landTimer / landDuration);

                // 하강: 최고점 → 원위치로 이동
                transform.position = Vector3.Lerp(peakPosition, originalPosition, t);

                yield return null;
            }

            // 최종 정착
            transform.position = originalPosition;

            captureEjectCoroutine = null;
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

        /// <summary>
        /// 현재 색상을 원색(originalColors)으로 선형 보간합니다.
        /// t=0: 현재 색상 유지, t=1: 완전히 원색으로 복원.
        /// </summary>
        /// <param name="t">보간 계수 (0~1)</param>
        private void LerpColorsToOriginal(float t)
        {
            if (modelRenderers == null || originalColors == null) return;

            for (int i = 0; i < modelRenderers.Length; i++)
            {
                if (modelRenderers[i] != null && modelRenderers[i].material.HasProperty("_Color"))
                {
                    Color current = modelRenderers[i].material.color;
                    modelRenderers[i].material.color = Color.Lerp(current, originalColors[i], t);
                }
            }
        }

        // -----------------------------------------------------------------
        // 이징 함수
        // -----------------------------------------------------------------

        /// <summary>
        /// EaseInBack 커브 — 살짝 뒤로 당긴 후 빠르게 수축.
        /// 포획 흡수 연출(빨려들어가는 느낌)에 사용됩니다.
        /// </summary>
        private float EaseInBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }

        /// <summary>
        /// EaseOutBack 커브 — 목표를 살짝 초과한 후 바운스로 정착.
        /// 포획 실패 팝업 연출(캡슐에서 튀어나오는 느낌)에 사용됩니다.
        /// </summary>
        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
