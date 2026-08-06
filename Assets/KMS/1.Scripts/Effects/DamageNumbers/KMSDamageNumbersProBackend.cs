using System;
using System.Reflection;
using UnityEngine;

namespace KMS.Effects.DamageNumbers
{
    internal sealed class KMSDamageNumbersProBackend
    {
        private const string DamageNumberTypeName =
            "DamageNumbersPro.DamageNumber, DamageNumbersPro";

        private readonly KMSDamagePopupSettings settings;
        private bool initialized;
        private bool available;
        private bool warnedAboutFailure;
        private Component prefabComponent;
        private MethodInfo spawnMethod;
        private MethodInfo setColorMethod;
        private MethodInfo setScaleMethod;

        public KMSDamageNumbersProBackend(KMSDamagePopupSettings popupSettings)
        {
            settings = popupSettings;
        }

        public bool TrySpawn(Vector3 position, int damage)
        {
            if (!settings.preferDamageNumbersPro)
            {
                return false;
            }

            EnsureInitialized();
            if (!available)
            {
                return false;
            }

            try
            {
                object spawnedPopup = spawnMethod.Invoke(
                    prefabComponent,
                    new object[] { position, (float)damage });

                ApplyDamageStyle(spawnedPopup, damage);
                return true;
            }
            catch (Exception exception)
            {
                available = false;
                WarnOnce(
                    "Damage Numbers Pro 팝업 생성에 실패하여 KMS 기본 팝업으로 전환합니다.",
                    Unwrap(exception));
                return false;
            }
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            Type damageNumberType = Type.GetType(DamageNumberTypeName, throwOnError: false);
            if (damageNumberType == null)
            {
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(settings.damageNumbersProResourcesPath);
            if (prefab == null)
            {
                return;
            }

            prefabComponent = prefab.GetComponent(damageNumberType);
            if (prefabComponent == null)
            {
                WarnOnce("로컬 프리팹에서 Damage Numbers Pro 컴포넌트를 찾지 못했습니다.");
                return;
            }

            spawnMethod = damageNumberType.GetMethod(
                "Spawn",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(Vector3), typeof(float) },
                modifiers: null);

            if (spawnMethod == null)
            {
                WarnOnce("Damage Numbers Pro의 Spawn(Vector3, float) API를 찾지 못했습니다.");
                return;
            }

            setColorMethod = damageNumberType.GetMethod(
                "SetColor",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(Color) },
                modifiers: null);

            setScaleMethod = damageNumberType.GetMethod(
                "SetScale",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(float) },
                modifiers: null);

            available = true;
            Debug.Log("[KMSDamagePopup] Damage Numbers Pro 로컬 백엔드를 사용합니다.");
        }

        private void ApplyDamageStyle(object spawnedPopup, int damage)
        {
            if (spawnedPopup == null)
            {
                return;
            }

            float thresholdRatio = settings.largeDamageThreshold > 0
                ? Mathf.Clamp01(
                    (damage - settings.largeDamageThreshold)
                    / (float)settings.largeDamageThreshold)
                : 0f;

            if (setColorMethod != null)
            {
                Color color = Color.Lerp(
                    settings.normalColor,
                    settings.largeDamageColor,
                    thresholdRatio);
                setColorMethod.Invoke(spawnedPopup, new object[] { color });
            }

            if (setScaleMethod != null)
            {
                float scale = Mathf.Lerp(1f, settings.maximumDamageScale, thresholdRatio);
                setScaleMethod.Invoke(spawnedPopup, new object[] { scale });
            }
        }

        private void WarnOnce(string message, Exception exception = null)
        {
            if (warnedAboutFailure)
            {
                return;
            }

            warnedAboutFailure = true;
            if (exception == null)
            {
                Debug.LogWarning($"[KMSDamagePopup] {message}");
            }
            else
            {
                Debug.LogWarning($"[KMSDamagePopup] {message}\n{exception.Message}");
            }
        }

        private static Exception Unwrap(Exception exception)
        {
            return exception is TargetInvocationException invocationException
                && invocationException.InnerException != null
                    ? invocationException.InnerException
                    : exception;
        }
    }
}
