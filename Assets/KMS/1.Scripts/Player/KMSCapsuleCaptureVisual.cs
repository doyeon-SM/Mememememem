using System.Collections;
using MemSystem.Core;
using MemSystem.Data;
using MemSystem.Events;
using KMS.Audio;
using UnityEngine;

namespace KMS
{
    /// <summary>
    /// Pikachu 포획 이벤트를 현재 캡슐에 한정해 수신하고 흔들림, 성공, 실패 연출을 재생합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KMSCapsuleCaptureVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Renderer[] capsuleRenderers;

        [Header("Shake")]
        [SerializeField, Min(0f)] private float shakeAngle = 14f;
        [SerializeField, Min(0f)] private float shakeSpeed = 15f;

        [Header("Result")]
        [SerializeField, Min(0.01f)] private float successDuration = 0.32f;
        [SerializeField, Min(0.01f)] private float failureDuration = 0.48f;
        [SerializeField] private Color successColor = new Color(1f, 0.9f, 0.25f, 1f);
        [SerializeField] private Color failureColor = new Color(1f, 0.25f, 0.2f, 1f);

        private MaterialPropertyBlock propertyBlock;
        private Mem targetMem;
        private Coroutine activeRoutine;
        private Quaternion impactRotation;
        private Vector3 impactScale;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();

            if (capsuleRenderers == null || capsuleRenderers.Length == 0)
            {
                capsuleRenderers = GetComponentsInChildren<Renderer>(true);
            }

            impactRotation = transform.rotation;
            impactScale = transform.localScale;
        }

        private void OnEnable()
        {
            MemEvents.OnMemCaptureStarted += HandleCaptureStarted;
            MemEvents.OnMemCaptured += HandleCaptured;
            MemEvents.OnMemCaptureFailed += HandleCaptureFailed;
        }

        private void OnDisable()
        {
            MemEvents.OnMemCaptureStarted -= HandleCaptureStarted;
            MemEvents.OnMemCaptured -= HandleCaptured;
            MemEvents.OnMemCaptureFailed -= HandleCaptureFailed;

            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }
        }

        public void BindTarget(Mem mem)
        {
            targetMem = mem;
            impactRotation = transform.rotation;
            impactScale = transform.localScale;
        }

        private void HandleCaptureStarted(Mem mem, Vector3 capsulePosition)
        {
            if (mem != targetMem)
            {
                return;
            }

            transform.position = capsulePosition;
            StartVisualRoutine(ShakeRoutine());
        }

        private void HandleCaptured(Mem mem, MemSnapshot snapshot)
        {
            if (mem != targetMem)
            {
                return;
            }

            KMSAudioService.PlayAt(GameSfxId.CaptureSuccess, transform.position);
            StartVisualRoutine(SuccessRoutine());
        }

        private void HandleCaptureFailed(Mem mem)
        {
            if (mem != targetMem)
            {
                return;
            }

            KMSAudioService.PlayAt(GameSfxId.CaptureFailure, transform.position);
            StartVisualRoutine(FailureRoutine());
        }

        private void StartVisualRoutine(IEnumerator routine)
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
            }

            activeRoutine = StartCoroutine(routine);
        }

        private IEnumerator ShakeRoutine()
        {
            float elapsed = 0f;
            while (true)
            {
                elapsed += Time.deltaTime;
                float angle = Mathf.Sin(elapsed * shakeSpeed) * shakeAngle;
                transform.rotation = impactRotation * Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }
        }

        private IEnumerator SuccessRoutine()
        {
            transform.rotation = impactRotation;
            float elapsed = 0f;

            while (elapsed < successDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / successDuration);
                float pulse = 1f + Mathf.Sin(progress * Mathf.PI) * 0.3f;
                float shrink = 1f - progress;
                transform.localScale = impactScale * pulse * shrink;
                SetTint(Color.Lerp(Color.white, successColor, progress));
                yield return null;
            }

            transform.localScale = Vector3.zero;
            activeRoutine = null;
        }

        private IEnumerator FailureRoutine()
        {
            float elapsed = 0f;
            while (elapsed < failureDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / failureDuration);
                float angle = Mathf.Sin(elapsed * shakeSpeed * 2.2f) * shakeAngle * (1f + progress);
                float pulse = 1f + Mathf.Sin(progress * Mathf.PI) * 0.45f;
                transform.rotation = impactRotation * Quaternion.Euler(0f, 0f, angle);
                transform.localScale = impactScale * pulse * (1f - progress);
                SetTint(Color.Lerp(Color.white, failureColor, progress));
                yield return null;
            }

            transform.localScale = Vector3.zero;
            activeRoutine = null;
        }

        private void SetTint(Color color)
        {
            if (capsuleRenderers == null)
            {
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            for (int i = 0; i < capsuleRenderers.Length; i++)
            {
                Renderer capsuleRenderer = capsuleRenderers[i];
                if (capsuleRenderer == null || capsuleRenderer.sharedMaterial == null)
                {
                    continue;
                }

                capsuleRenderer.GetPropertyBlock(propertyBlock);
                if (capsuleRenderer.sharedMaterial.HasProperty(BaseColorId))
                {
                    propertyBlock.SetColor(BaseColorId, color);
                }
                else if (capsuleRenderer.sharedMaterial.HasProperty(ColorId))
                {
                    propertyBlock.SetColor(ColorId, color);
                }

                capsuleRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
