using System.IO;
using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using KMS.Effects.DamageNumbers;

namespace KMS.EditorTools
{
    public static class KMSDamagePopupSetup
    {
        private const string SettingsDirectory = "Assets/KMS/Resources/KMS";
        private const string SettingsAssetPath =
            SettingsDirectory + "/DamagePopupSettings.asset";

        private const string LocalResourcesDirectory =
            "Assets/KMS/99.Assets/DamageNumbersPro/Resources/KMSLocal";

        private const string SourcePrefabPath =
            "Assets/KMS/99.Assets/DamageNumbersPro/Demo/Prefabs/3D/Clear.prefab";

        private const string LocalPrefabPath =
            LocalResourcesDirectory + "/MemDamageNumber.prefab";

        [MenuItem("Tools/KMS/Damage Popup/Setup Local Prototype")]
        public static void Apply()
        {
            EnsureSettingsAsset();
            bool dnpReady = EnsureLocalDamageNumbersProPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (dnpReady)
            {
                Debug.Log(
                    "[KMSDamagePopupSetup] 공용 설정과 Damage Numbers Pro 로컬 프리팹 준비가 완료되었습니다.");
            }
            else
            {
                Debug.LogWarning(
                    "[KMSDamagePopupSetup] 공용 Fallback 설정은 준비됐지만 Damage Numbers Pro 원본 프리팹을 찾지 못했습니다.");
            }
        }

        public static void ApplyFromCommandLine()
        {
            Apply();
        }

        [MenuItem("Tools/KMS/Damage Popup/Validate Installation")]
        public static void Validate()
        {
            KMSDamagePopupSettings settings =
                AssetDatabase.LoadAssetAtPath<KMSDamagePopupSettings>(SettingsAssetPath);

            if (settings == null)
            {
                throw new InvalidOperationException("공용 DamagePopupSettings 에셋이 없습니다.");
            }

            ValidateFallback(settings);
            ValidateDamageNumbersPro(settings);
            Debug.Log("[KMSDamagePopupValidation] Fallback 및 로컬 DNP 연동 검증을 통과했습니다.");
        }

        public static void ValidateFromCommandLine()
        {
            Validate();
        }

        private static void EnsureSettingsAsset()
        {
            EnsureDirectory(SettingsDirectory);

            KMSDamagePopupSettings settings =
                AssetDatabase.LoadAssetAtPath<KMSDamagePopupSettings>(SettingsAssetPath);

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<KMSDamagePopupSettings>();
                AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            }

            EditorUtility.SetDirty(settings);
        }

        private static void ValidateFallback(KMSDamagePopupSettings settings)
        {
            GameObject popupObject = new GameObject("KMS Damage Popup Validation");
            try
            {
                KMSFallbackDamagePopup popup =
                    popupObject.AddComponent<KMSFallbackDamagePopup>();
                popup.Configure(settings, _ => { });
                popup.Play(Vector3.zero, 7, null);

                TextMeshPro textMesh = popupObject.GetComponent<TextMeshPro>();
                if (!popup.IsPlaying || !popupObject.activeSelf
                    || textMesh == null || textMesh.text != "7")
                {
                    throw new InvalidOperationException("KMS Fallback 팝업 초기화에 실패했습니다.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popupObject);
            }
        }

        private static void ValidateDamageNumbersPro(KMSDamagePopupSettings settings)
        {
            Type damageNumberType = Type.GetType(
                "DamageNumbersPro.DamageNumber, DamageNumbersPro",
                throwOnError: false);

            if (damageNumberType == null)
            {
                throw new InvalidOperationException("Damage Numbers Pro 타입을 찾지 못했습니다.");
            }

            GameObject prefab = Resources.Load<GameObject>(
                settings.damageNumbersProResourcesPath);
            if (prefab == null || prefab.GetComponent(damageNumberType) == null)
            {
                throw new InvalidOperationException("Damage Numbers Pro 로컬 프리팹을 찾지 못했습니다.");
            }

            MethodInfo spawnMethod = damageNumberType.GetMethod(
                "Spawn",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(Vector3), typeof(float) },
                modifiers: null);

            if (spawnMethod == null)
            {
                throw new InvalidOperationException("Damage Numbers Pro Spawn API를 찾지 못했습니다.");
            }
        }

        private static bool EnsureLocalDamageNumbersProPrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (source == null)
            {
                return false;
            }

            EnsureDirectory(LocalResourcesDirectory);

            GameObject localPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LocalPrefabPath);
            if (localPrefab == null)
            {
                if (!AssetDatabase.CopyAsset(SourcePrefabPath, LocalPrefabPath))
                {
                    Debug.LogError(
                        $"[KMSDamagePopupSetup] 로컬 프리팹 복제에 실패했습니다: {LocalPrefabPath}");
                    return false;
                }

                AssetDatabase.ImportAsset(LocalPrefabPath, ImportAssetOptions.ForceSynchronousImport);
                localPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LocalPrefabPath);
            }

            if (localPrefab == null)
            {
                return false;
            }

            Component damageNumberComponent = FindDamageNumberComponent(localPrefab);
            if (damageNumberComponent == null)
            {
                Debug.LogError(
                    "[KMSDamagePopupSetup] 복제된 프리팹에 Damage Numbers Pro 컴포넌트가 없습니다.");
                return false;
            }

            SerializedObject serialized = new SerializedObject(damageNumberComponent);
            SetFloat(serialized, "lifetime", 1.05f);
            SetBool(serialized, "enable3DGame", true);
            SetBool(serialized, "faceCameraView", true);
            SetBool(serialized, "renderThroughWalls", true);
            SetBool(serialized, "enableOrthographicScaling", true);
            SetBool(serialized, "enableLerp", true);
            SetBool(serialized, "enableCollision", true);
            SetBool(serialized, "enablePooling", true);
            SetInt(serialized, "poolSize", 40);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(localPrefab);
            PrefabUtility.SavePrefabAsset(localPrefab);
            return true;
        }

        private static Component FindDamageNumberComponent(GameObject prefab)
        {
            Component[] components = prefab.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null)
                {
                    continue;
                }

                string fullName = component.GetType().FullName;
                if (fullName == "DamageNumbersPro.DamageNumberMesh"
                    || fullName == "DamageNumbersPro.DamageNumber")
                {
                    return component;
                }
            }

            return null;
        }

        private static void EnsureDirectory(string assetPath)
        {
            string absolutePath = Path.GetFullPath(assetPath);
            if (!Directory.Exists(absolutePath))
            {
                Directory.CreateDirectory(absolutePath);
            }
        }

        private static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetInt(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }
    }
}
