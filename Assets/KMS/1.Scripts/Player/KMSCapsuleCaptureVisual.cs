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
        [SerializeField, Min(0f)] private float hoverHeight = 0.035f;
        [SerializeField, Min(0f)] private float hoverSpeed = 4f;

        [Header("Result")]
        [SerializeField, Min(0.01f)] private float successDuration = 0.32f;
        [SerializeField, Min(0.01f)] private float failureDuration = 0.48f;
        [SerializeField] private Color successColor = new Color(1f, 0.9f, 0.25f, 1f);
        [SerializeField] private Color failureColor = new Color(1f, 0.25f, 0.2f, 1f);

        [Header("Success Sparkle")]
        [SerializeField] private ParticleSystem successSparklePrefab;
        [SerializeField] private Material successSparkleMaterial;
        [SerializeField, Min(1)] private int successFlashCount = 3;
        [SerializeField, Min(0f)] private float successEmissionIntensity = 4f;
        [SerializeField, Min(1)] private int successSparkleCount = 42;
        [SerializeField, Min(0f)] private float successSparkleRadius = 0.38f;
        [SerializeField, Min(0.01f)] private float successSparkleLifetime = 0.7f;
        [SerializeField, Min(0f)] private float successSparkleSpeed = 0.95f;
        [SerializeField, Min(0.001f)] private float successSparkleSize = 0.09f;

        private MaterialPropertyBlock propertyBlock;
        private Mem targetMem;
        private Coroutine activeRoutine;
        private Vector3 impactPosition;
        private Quaternion impactRotation;
        private Vector3 impactScale;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();

            if (capsuleRenderers == null || capsuleRenderers.Length == 0)
            {
                capsuleRenderers = GetComponentsInChildren<Renderer>(true);
            }

            impactPosition = transform.position;
            impactRotation = transform.rotation;
            impactScale = transform.localScale;
            EnableEmissionOnMaterials();
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
            impactPosition = transform.position;
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
            impactPosition = capsulePosition;
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
                float hoverOffset = Mathf.Sin(elapsed * hoverSpeed) * hoverHeight;
                transform.position = impactPosition + Vector3.up * hoverOffset;
                transform.rotation = impactRotation * Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }
        }

        private IEnumerator SuccessRoutine()
        {
            transform.rotation = impactRotation;
            PlaySuccessSparkles();
            float elapsed = 0f;

            while (elapsed < successDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / successDuration);
                float flash = Mathf.Pow(
                    Mathf.Abs(Mathf.Sin(progress * successFlashCount * Mathf.PI)),
                    0.35f);
                float pulse = 1f + flash * 0.24f;
                float shrink = 1f - progress;
                transform.localScale = impactScale * pulse * shrink;
                Color tint = Color.Lerp(Color.white, successColor, flash);
                SetTintAndEmission(tint, successColor * (flash * successEmissionIntensity));
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
                SetTintAndEmission(Color.Lerp(Color.white, failureColor, progress), Color.black);
                yield return null;
            }

            transform.localScale = Vector3.zero;
            activeRoutine = null;
        }

        private void EnableEmissionOnMaterials()
        {
            if (capsuleRenderers == null)
            {
                return;
            }

            for (int i = 0; i < capsuleRenderers.Length; i++)
            {
                Renderer capsuleRenderer = capsuleRenderers[i];
                if (capsuleRenderer == null)
                {
                    continue;
                }

                Material[] materials = capsuleRenderer.materials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material != null && material.HasProperty(EmissionColorId))
                    {
                        material.EnableKeyword("_EMISSION");
                    }
                }
            }
        }

        private void PlaySuccessSparkles()
        {
            if (successSparklePrefab != null)
            {
                ParticleSystem sparkle = Instantiate(
                    successSparklePrefab,
                    transform.position,
                    Quaternion.identity);
                sparkle.Play(true);

                ParticleSystem.MainModule prefabMain = sparkle.main;
                float prefabLifetime = prefabMain.duration + prefabMain.startLifetime.constantMax + 0.1f;
                Destroy(sparkle.gameObject, prefabLifetime);
                return;
            }

            GameObject sparkleObject = new GameObject($"{name}_SuccessSparkles");
            sparkleObject.transform.position = transform.position;

            ParticleSystem sparkleSystem = sparkleObject.AddComponent<ParticleSystem>();
            sparkleSystem.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = sparkleSystem.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.1f;
            main.startLifetime = successSparkleLifetime;
            main.startSpeed = successSparkleSpeed;
            main.startSize = successSparkleSize;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, successColor);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = successSparkleCount * 2;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = sparkleSystem.emission;
            emission.rateOverTime = 0f;
            short firstBurstCount = (short)Mathf.Clamp(successSparkleCount, 1, short.MaxValue);
            short secondBurstCount = (short)Mathf.Clamp(
                Mathf.CeilToInt(successSparkleCount * 0.65f),
                1,
                short.MaxValue);
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, firstBurstCount),
                new ParticleSystem.Burst(0.12f, secondBurstCount)
            });

            ParticleSystem.ShapeModule shape = sparkleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = successSparkleRadius;
            shape.radiusThickness = 1f;
            shape.randomDirectionAmount = 0.2f;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = sparkleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.25f),
                    new Keyframe(0.12f, 1.35f),
                    new Keyframe(0.55f, 0.9f),
                    new Keyframe(1f, 0f)));

            Gradient sparkleGradient = new Gradient();
            sparkleGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(successColor, 0.3f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.08f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = sparkleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = sparkleGradient;

            ParticleSystem.NoiseModule noise = sparkleSystem.noise;
            noise.enabled = true;
            noise.strength = 0.16f;
            noise.frequency = 0.8f;
            noise.scrollSpeed = 0.25f;

            ParticleSystemRenderer particleRenderer = sparkleObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = ResolveSuccessSparkleMaterial(out bool isRuntimeMaterial);

            if (isRuntimeMaterial && particleRenderer.sharedMaterial != null)
            {
                Destroy(
                    particleRenderer.sharedMaterial,
                    successSparkleLifetime + 0.5f);
            }

            sparkleSystem.Play(true);
        }

        private Material ResolveSuccessSparkleMaterial(out bool isRuntimeMaterial)
        {
            isRuntimeMaterial = false;
            if (successSparkleMaterial != null)
            {
                return successSparkleMaterial;
            }

            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null)
            {
                Debug.LogWarning(
                    "[KMSCapsuleCaptureVisual] URP particle shader was not found.",
                    this);
                return null;
            }

            isRuntimeMaterial = true;
            Material material = new Material(particleShader)
            {
                name = "KMS_CapsuleSuccessSparkle_Runtime",
                hideFlags = HideFlags.DontSave
            };

            ConfigureAdditiveParticleMaterial(material);
            return material;
        }

        private static void ConfigureAdditiveParticleMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetColor("_BaseColor", Color.white);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void SetTintAndEmission(Color color, Color emissionColor)
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

                if (capsuleRenderer.sharedMaterial.HasProperty(EmissionColorId))
                {
                    propertyBlock.SetColor(EmissionColorId, emissionColor);
                }

                capsuleRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
