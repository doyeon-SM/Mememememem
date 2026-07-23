using System.Collections;
using HDY.Item;
using MemSystem.Core;
using MemSystem.Interface;
using UnityEngine;

namespace KMS
{
    /// <summary>
    /// KMS 플레이어가 던진 캡슐의 명중, 흡수 대기, 성공/실패 판정을 순서대로 실행합니다.
    /// 멤 연출과 최종 처리는 Pikachu의 ICapturable 구현에 위임합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider), typeof(Rigidbody))]
    public sealed class KMSCapsuleCaptureSequence : MonoBehaviour
    {
        public enum JudgmentMode
        {
            Random,
            ForceSuccess,
            ForceFailure
        }

        [Header("Capture")]
        [SerializeField] private ItemData capsuleItemData;
        [SerializeField, Min(0f)] private float judgmentDelay = 0.65f;
        [SerializeField] private JudgmentMode judgmentMode = JudgmentMode.Random;

        [Header("Cleanup")]
        [SerializeField, Min(0f)] private float successCleanupDelay = 0.4f;
        [SerializeField, Min(0f)] private float failureCleanupDelay = 0.6f;
        [SerializeField, Min(0.1f)] private float missLifetime = 8f;

        [Header("References")]
        [SerializeField] private KMSCapsuleCaptureVisual captureVisual;

        private Collider capsuleCollider;
        private Rigidbody capsuleBody;
        private Mem targetMem;
        private ICapturable targetCapturable;
        private bool consumed;

        public ItemData CapsuleItemData => capsuleItemData;
        public float LastCaptureRate { get; private set; }

        private void Awake()
        {
            capsuleCollider = GetComponent<Collider>();
            capsuleBody = GetComponent<Rigidbody>();
            if (captureVisual == null)
            {
                captureVisual = GetComponent<KMSCapsuleCaptureVisual>();
            }

            if (missLifetime > 0f)
            {
                Destroy(gameObject, missLifetime);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (consumed || other == null)
            {
                return;
            }

            Mem mem = other.GetComponentInParent<Mem>();
            ICapturable capturable = mem;
            if (mem == null || capturable == null || !mem.IsActive)
            {
                return;
            }

            consumed = true;
            targetMem = mem;
            targetCapturable = capturable;
            LastCaptureRate = capturable.GetCaptureRate(ResolveCapsuleTier());

            FreezeAtImpact();
            captureVisual?.BindTarget(targetMem);

            Vector3 capturePosition = transform.position;
            capturable.NotifyCaptureBallHit(capturePosition);
            StartCoroutine(JudgeCaptureRoutine());
        }

        private IEnumerator JudgeCaptureRoutine()
        {
            if (judgmentDelay > 0f)
            {
                yield return new WaitForSeconds(judgmentDelay);
            }

            if (targetMem == null || targetCapturable == null || !targetMem.IsActive)
            {
                Destroy(gameObject);
                yield break;
            }

            bool success = ResolveSuccess();
            if (success)
            {
                targetCapturable.OnCaptureSuccess();
            }
            else
            {
                targetCapturable.OnCaptureFail();
            }

            float cleanupDelay = success ? successCleanupDelay : failureCleanupDelay;
            if (cleanupDelay > 0f)
            {
                yield return new WaitForSeconds(cleanupDelay);
            }

            Destroy(gameObject);
        }

        private int ResolveCapsuleTier()
        {
            return capsuleItemData != null ? (int)capsuleItemData.ItemClass : 0;
        }

        private bool ResolveSuccess()
        {
            switch (judgmentMode)
            {
                case JudgmentMode.ForceSuccess:
                    return true;
                case JudgmentMode.ForceFailure:
                    return false;
                default:
                    return Random.value <= LastCaptureRate;
            }
        }

        private void FreezeAtImpact()
        {
            if (capsuleCollider != null)
            {
                capsuleCollider.enabled = false;
            }

            if (capsuleBody != null)
            {
                capsuleBody.linearVelocity = Vector3.zero;
                capsuleBody.angularVelocity = Vector3.zero;
                capsuleBody.isKinematic = true;
                capsuleBody.useGravity = false;
            }
        }
    }
}
