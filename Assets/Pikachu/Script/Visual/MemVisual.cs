// ============================================================================
// MemVisual.cs
// 멤 외형 및 애니메이션 처리 (프로시저럴)
//
// [담당자 안내]
// - 현재 멤 모델에 애니메이션(FBX Animation)이 없으므로, 코드로 애니메이션을 대체합니다.
// - Transform 조작(크기 바운스, 기울기, 돌진, 쉐이더 색상 변경)을 통해 상태를 표현합니다.
// - 추후 아티스트가 애니메이션 클립을 제공하면, 이 클래스 내부 구현을 Animator 제어로 교체하면 됩니다.
// ============================================================================
using UnityEngine;
using System.Collections;

namespace MemSystem.Visual
{
    /// <summary>
    /// 멤의 비주얼을 담당하는 컴포넌트.
    /// 모델 스왑 및 프로시저럴 애니메이션(코드 애니메이션)을 처리합니다.
    /// 
    /// [프로시저럴 애니메이션 종류]
    /// - Idle: 제자리 상하 바운스
    /// - Walk: 이동 방향으로 기울어지며 바운스
    /// - Flee: 빠르고 불규칙한 바운스
    /// - Attack: 전방으로 짧게 돌진(Lunge) 후 복귀
    /// - Hit: 잠시 빨갛게 변하며 흔들림 (피격)
    /// </summary>
    public class MemVisual : MonoBehaviour
    {
        // =================================================================
        // 설정값
        // =================================================================

        [Header("시각 효과 설정")]
        [Tooltip("피격 시 플래시 색상")]
        [SerializeField] private Color hitColor = Color.red;
        
        [Tooltip("피격 효과 지속 시간 (초)")]
        [SerializeField] private float hitDuration = 0.2f;

        [Header("프로시저럴 애니메이션 파라미터")]
        [SerializeField] private float idleBounceSpeed = 2f;
        [SerializeField] private float idleBounceHeight = 0.1f;
        [SerializeField] private float walkTiltAngle = 15f;
        [SerializeField] private float walkBounceSpeed = 5f;
        [SerializeField] private float walkBounceHeight = 0.2f;

        // =================================================================
        // 상태 변수
        // =================================================================

        private GameObject currentModel;
        private Renderer[] modelRenderers;
        private Color[] originalColors;

        private enum AnimState { None, Idle, Walk, Flee, Attack }
        private AnimState currentState = AnimState.None;

        private Vector3 originalLocalPos;
        private Quaternion originalLocalRot;

        private Coroutine hitFlashCoroutine;
        private Coroutine attackCoroutine;

        // =================================================================
        // Unity Lifecycle
        // =================================================================

        private void Awake()
        {
            originalLocalPos = transform.localPosition;
            originalLocalRot = transform.localRotation;
        }

        private void Update()
        {
            // 코루틴으로 처리되지 않는 반복 애니메이션 (Idle, Walk, Flee) 업데이트
            switch (currentState)
            {
                case AnimState.Idle:
                    UpdateIdleAnim();
                    break;
                case AnimState.Walk:
                    UpdateWalkAnim();
                    break;
                case AnimState.Flee:
                    UpdateFleeAnim();
                    break;
            }
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
            
            // 원본 프리팹이 가진 로컬 위치와 회전값을 그대로 보존합니다. (FBX 누움 방지 및 높이 조절 유지)
            currentModel.transform.localPosition = modelPrefab.transform.localPosition;
            currentModel.transform.localRotation = modelPrefab.transform.localRotation;

            // 렌더러와 원래 색상 캐싱 (피격 플래시 용도)
            modelRenderers = currentModel.GetComponentsInChildren<Renderer>();
            
            if (modelRenderers != null && modelRenderers.Length > 0)
            {
                originalColors = new Color[modelRenderers.Length];
                for (int i = 0; i < modelRenderers.Length; i++)
                {
                    // 기본 머티리얼의 색상 저장
                    if (modelRenderers[i].material.HasProperty("_Color"))
                    {
                        originalColors[i] = modelRenderers[i].material.color;
                    }
                    else
                    {
                        originalColors[i] = Color.white; // 기본값
                    }
                }
            }
        }

        /// <summary>
        /// 풀 반환 시 시각 효과를 리셋합니다.
        /// </summary>
        public void ResetVisual()
        {
            currentState = AnimState.None;
            transform.localPosition = originalLocalPos;
            transform.localRotation = originalLocalRot;
            
            if (hitFlashCoroutine != null) StopCoroutine(hitFlashCoroutine);
            if (attackCoroutine != null) StopCoroutine(attackCoroutine);

            RestoreColors();
        }

        // =================================================================
        // 애니메이션 재생 API (MemAI 상태 클래스에서 호출)
        // =================================================================

        public void PlayIdle()
        {
            if (currentState == AnimState.Attack) return; // 공격 중 방해 방지
            currentState = AnimState.Idle;
            transform.localRotation = originalLocalRot; // 기울기 원복
        }

        public void PlayWalk()
        {
            if (currentState == AnimState.Attack) return;
            currentState = AnimState.Walk;
        }

        public void PlayFlee()
        {
            if (currentState == AnimState.Attack) return;
            currentState = AnimState.Flee;
        }

        /// <summary>
        /// 공격 연출 — 짧게 전방으로 돌진(Lunge) 후 복귀.
        /// </summary>
        public void PlayAttack()
        {
            currentState = AnimState.Attack;
            
            if (attackCoroutine != null) StopCoroutine(attackCoroutine);
            attackCoroutine = StartCoroutine(AttackLungeRoutine());
        }

        /// <summary>
        /// 피격 연출 — 잠시 빨갛게 변함.
        /// </summary>
        public void PlayHit()
        {
            if (hitFlashCoroutine != null) StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        // =================================================================
        // 프로시저럴 애니메이션 구현부
        // =================================================================

        private void UpdateIdleAnim()
        {
            // 부드러운 상하 바운스
            float yOffset = Mathf.Sin(Time.time * idleBounceSpeed) * idleBounceHeight;
            transform.localPosition = originalLocalPos + new Vector3(0, yOffset, 0);
        }

        private void UpdateWalkAnim()
        {
            // 통통 튀는 바운스 (절댓값으로 위로만 튐)
            float yOffset = Mathf.Abs(Mathf.Sin(Time.time * walkBounceSpeed)) * walkBounceHeight;
            transform.localPosition = originalLocalPos + new Vector3(0, yOffset, 0);

            // 앞으로 살짝 기울어짐
            transform.localRotation = Quaternion.Euler(walkTiltAngle, 0, 0);
        }

        private void UpdateFleeAnim()
        {
            // 걷기보다 빠르고 급박한 바운스 + 좌우 흔들림
            float yOffset = Mathf.Abs(Mathf.Sin(Time.time * walkBounceSpeed * 1.5f)) * walkBounceHeight;
            float roll = Mathf.Sin(Time.time * walkBounceSpeed * 2f) * 10f; // 좌우 기우뚱
            
            transform.localPosition = originalLocalPos + new Vector3(0, yOffset, 0);
            transform.localRotation = Quaternion.Euler(walkTiltAngle, 0, roll);
        }

        private IEnumerator AttackLungeRoutine()
        {
            Vector3 startPos = originalLocalPos;
            Vector3 lungePos = startPos + Vector3.forward * 1.0f; // 앞으로 1m 돌진

            float time = 0;
            float duration = 0.15f; // 돌진 시간

            // 돌진 (전진)
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;
                transform.localPosition = Vector3.Lerp(startPos, lungePos, t);
                yield return null;
            }

            time = 0;
            duration = 0.2f; // 복귀 시간

            // 복귀 (후진)
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;
                transform.localPosition = Vector3.Lerp(lungePos, startPos, t);
                yield return null;
            }

            transform.localPosition = startPos;
            
            // 상태 원복 (Idle)
            currentState = AnimState.Idle;
        }

        private IEnumerator HitFlashRoutine()
        {
            SetColors(hitColor);
            
            // 피격 흔들림 (선택적)
            transform.localPosition += new Vector3(Random.Range(-0.1f, 0.1f), 0, Random.Range(-0.1f, 0.1f));
            
            yield return new WaitForSeconds(hitDuration);
            
            RestoreColors();
        }

        private void SetColors(Color color)
        {
            if (modelRenderers == null) return;
            
            foreach (var r in modelRenderers)
            {
                if (r != null && r.material.HasProperty("_Color"))
                {
                    r.material.color = color;
                }
            }
        }

        private void RestoreColors()
        {
            if (modelRenderers == null || originalColors == null) return;
            
            for (int i = 0; i < modelRenderers.Length; i++)
            {
                if (modelRenderers[i] != null && modelRenderers[i].material.HasProperty("_Color"))
                {
                    modelRenderers[i].material.color = originalColors[i];
                }
            }
        }
    }
}
