using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace KMS.EditorTools
{
    public static class KMSHungerHealthRecoveryValidation
    {
        private const string PlayerPrefabPath =
            "Assets/KMS/2.Prefabs/0720_Player_KMS.prefab";

        [MenuItem("KMS/Validation/Validate Hunger Health Recovery")]
        public static void Validate()
        {
            ValidateExactHungerConsumption();
            ValidatePlayerPrefabSettings();
            Debug.Log("[KMS Hunger Recovery] Exact consumption and prefab settings validation passed.");
        }

        private static void ValidateExactHungerConsumption()
        {
            var testObject = new GameObject("KMSHungerRecoveryValidation");

            try
            {
                PlayerStats stats = testObject.AddComponent<PlayerStats>();
                SetSerializedFloat(stats, "startingHealth", 50f);
                SetSerializedFloat(stats, "startingHunger", 0f);
                InvokePrivateAwake(stats);

                stats.RestoreHunger(5f);
                if (stats.TryConsumeHungerExact(10f))
                {
                    throw new InvalidOperationException(
                        "Exact hunger consumption must fail when only part of the cost is available.");
                }

                RequireApproximately(stats.CurrentHunger, 5f, "failed exact cost preserves hunger");

                stats.RestoreHunger(5f);
                if (!stats.TryConsumeHungerExact(10f))
                {
                    throw new InvalidOperationException(
                        "Exact hunger consumption must succeed when the full cost is available.");
                }

                RequireApproximately(stats.CurrentHunger, 0f, "successful exact cost consumes full hunger");

                stats.RestoreHunger(5f);
                if (!stats.ConsumeHunger(10f))
                {
                    throw new InvalidOperationException(
                        "Legacy partial hunger consumption behavior unexpectedly changed.");
                }

                RequireApproximately(stats.CurrentHunger, 0f, "legacy consumption still allows partial drain");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testObject);
            }
        }

        private static void ValidatePlayerPrefabSettings()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (root == null)
            {
                throw new InvalidOperationException($"Player prefab could not be loaded: {PlayerPrefabPath}");
            }

            try
            {
                PlayerHungerHealthRecovery recovery =
                    root.GetComponent<PlayerHungerHealthRecovery>();
                if (recovery == null)
                {
                    throw new InvalidOperationException(
                        "Player prefab is missing PlayerHungerHealthRecovery.");
                }

                var serializedRecovery = new SerializedObject(recovery);
                RequireSerializedFloat(
                    serializedRecovery,
                    "minimumHungerReserve",
                    10f);
                RequireSerializedFloat(
                    serializedRecovery,
                    "recoveryDelayAfterEating",
                    2f);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetSerializedFloat(
            UnityEngine.Object target,
            string propertyName,
            float value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name}.{propertyName} was not found.");
            }

            property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void InvokePrivateAwake(PlayerStats stats)
        {
            MethodInfo awake = typeof(PlayerStats).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (awake == null)
            {
                throw new InvalidOperationException("PlayerStats.Awake could not be resolved.");
            }

            awake.Invoke(stats, null);
        }

        private static void RequireSerializedFloat(
            SerializedObject serializedObject,
            string propertyName,
            float expected)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"PlayerHungerHealthRecovery.{propertyName} was not found.");
            }

            RequireApproximately(property.floatValue, expected, propertyName);
        }

        private static void RequireApproximately(float actual, float expected, string label)
        {
            if (Mathf.Abs(actual - expected) <= 0.001f) return;

            throw new InvalidOperationException(
                $"{label}: expected {expected}, actual {actual}.");
        }
    }
}
