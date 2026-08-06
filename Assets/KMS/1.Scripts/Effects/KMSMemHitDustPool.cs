using UnityEngine;

namespace KMS.Effects
{
    [DisallowMultipleComponent]
    public sealed class KMSMemHitDustPool : MonoBehaviour
    {
        [Header("Effect")]
        [SerializeField] private ParticleSystem effectPrefab;
        [SerializeField, Min(1)] private int capacity = 6;
        [SerializeField, Min(0f)] private float surfaceOffset = 0.02f;

        private ParticleSystem[] effects;
        private int nextIndex;

        private void Awake()
        {
            WarmUp();
        }

        public void Play(Vector3 position, Vector3 surfaceNormal)
        {
            if (effectPrefab == null) return;

            WarmUp();
            if (effects == null || effects.Length == 0) return;

            ParticleSystem effect = GetAvailableEffect();
            if (effect == null) return;

            Vector3 normal = surfaceNormal.sqrMagnitude > 0.0001f
                ? surfaceNormal.normalized
                : Vector3.up;

            Transform effectTransform = effect.transform;
            effectTransform.SetPositionAndRotation(
                position + normal * surfaceOffset,
                Quaternion.FromToRotation(Vector3.up, normal));

            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.Play(true);
        }

        private void WarmUp()
        {
            if (effectPrefab == null || effects != null) return;

            int poolSize = Mathf.Max(1, capacity);
            effects = new ParticleSystem[poolSize];

            for (int i = 0; i < poolSize; i++)
            {
                ParticleSystem instance = Instantiate(effectPrefab, transform);
                instance.name = $"{effectPrefab.name}_Pooled_{i + 1}";
                instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                effects[i] = instance;
            }
        }

        private ParticleSystem GetAvailableEffect()
        {
            for (int offset = 0; offset < effects.Length; offset++)
            {
                int index = (nextIndex + offset) % effects.Length;
                ParticleSystem candidate = effects[index];
                if (candidate == null || candidate.IsAlive(true)) continue;

                nextIndex = (index + 1) % effects.Length;
                return candidate;
            }

            // 풀이 모두 재생 중이면 가장 오래된 순서의 이펙트를 즉시 재사용한다.
            ParticleSystem recycled = effects[nextIndex];
            nextIndex = (nextIndex + 1) % effects.Length;
            return recycled;
        }
    }
}
