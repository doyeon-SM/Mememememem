using System;
using TMPro;
using UnityEngine;

namespace KMS.Effects.DamageNumbers
{
    [DisallowMultipleComponent]
    public sealed class KMSFallbackDamagePopup : MonoBehaviour
    {
        private TextMeshPro textMesh;
        private KMSDamagePopupSettings settings;
        private Action<KMSFallbackDamagePopup> releaseCallback;
        private Camera targetCamera;
        private Vector3 startPosition;
        private Vector3 sideDirection;
        private Color baseColor;
        private float age;
        private float damageScale = 1f;
        private bool isPlaying;

        public bool IsPlaying => isPlaying;

        public void Configure(
            KMSDamagePopupSettings popupSettings,
            Action<KMSFallbackDamagePopup> onReleased)
        {
            settings = popupSettings;
            releaseCallback = onReleased;
            EnsureVisual();
            gameObject.SetActive(false);
        }

        public void Play(Vector3 worldPosition, int damage, Camera camera)
        {
            EnsureVisual();

            targetCamera = camera;
            startPosition = worldPosition;
            age = 0f;
            isPlaying = true;

            float thresholdRatio = settings.largeDamageThreshold > 0
                ? Mathf.Clamp01((damage - settings.largeDamageThreshold) / (float)settings.largeDamageThreshold)
                : 0f;

            damageScale = Mathf.Lerp(1f, settings.maximumDamageScale, thresholdRatio);
            baseColor = Color.Lerp(settings.normalColor, settings.largeDamageColor, thresholdRatio);
            sideDirection = ResolveScreenRight(camera) * UnityEngine.Random.Range(-settings.sideDrift, settings.sideDrift);

            textMesh.text = damage.ToString();
            textMesh.fontSize = settings.fontSize;
            textMesh.color = baseColor;
            textMesh.outlineColor = settings.outlineColor;
            textMesh.outlineWidth = settings.outlineWidth;

            transform.position = startPosition;
            transform.localScale = Vector3.one * settings.baseScale * settings.spawnScale * damageScale;
            gameObject.SetActive(true);
            FaceCamera();
        }

        private void LateUpdate()
        {
            if (!isPlaying || settings == null)
            {
                return;
            }

            age += Time.deltaTime;
            float lifetime = Mathf.Max(0.1f, settings.lifetime);
            float progress = Mathf.Clamp01(age / lifetime);

            float easedRise = 1f - Mathf.Pow(1f - progress, 2f);
            transform.position = startPosition
                + Vector3.up * settings.riseDistance * easedRise
                + sideDirection * easedRise;

            float animationScale = EvaluatePopScale(progress);
            float cameraScale = ResolveCameraScale();
            transform.localScale = Vector3.one
                * settings.baseScale
                * damageScale
                * animationScale
                * cameraScale;

            float alpha = progress <= settings.fadeStart
                ? 1f
                : 1f - Mathf.InverseLerp(settings.fadeStart, 1f, progress);

            Color color = baseColor;
            color.a = Mathf.SmoothStep(0f, 1f, alpha);
            textMesh.color = color;

            FaceCamera();

            if (age >= lifetime)
            {
                StopAndRelease();
            }
        }

        private float EvaluatePopScale(float progress)
        {
            if (progress < 0.12f)
            {
                return Mathf.Lerp(
                    settings.spawnScale,
                    settings.overshootScale,
                    progress / 0.12f);
            }

            if (progress < 0.28f)
            {
                return Mathf.Lerp(
                    settings.overshootScale,
                    1f,
                    (progress - 0.12f) / 0.16f);
            }

            return 1f;
        }

        private float ResolveCameraScale()
        {
            Camera camera = ResolveCamera();
            if (camera == null || !camera.orthographic)
            {
                return 1f;
            }

            float referenceSize = Mathf.Max(0.01f, settings.referenceOrthographicSize);
            return Mathf.Max(0.01f, camera.orthographicSize / referenceSize);
        }

        private void FaceCamera()
        {
            Camera camera = ResolveCamera();
            if (camera != null)
            {
                transform.forward = camera.transform.forward;
            }
        }

        private Camera ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            return targetCamera;
        }

        private static Vector3 ResolveScreenRight(Camera camera)
        {
            if (camera == null)
            {
                return Vector3.right;
            }

            Vector3 right = camera.transform.right;
            return right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
        }

        private void EnsureVisual()
        {
            if (textMesh != null)
            {
                return;
            }

            textMesh = GetComponent<TextMeshPro>();
            if (textMesh == null)
            {
                textMesh = gameObject.AddComponent<TextMeshPro>();
            }

            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.textWrappingMode = TextWrappingModes.NoWrap;
            textMesh.richText = false;
            textMesh.raycastTarget = false;
            textMesh.sortingOrder = 200;
        }

        private void StopAndRelease()
        {
            if (!isPlaying)
            {
                return;
            }

            isPlaying = false;
            gameObject.SetActive(false);
            releaseCallback?.Invoke(this);
        }

        private void OnDisable()
        {
            if (isPlaying && !gameObject.scene.isLoaded)
            {
                isPlaying = false;
            }
        }
    }
}
