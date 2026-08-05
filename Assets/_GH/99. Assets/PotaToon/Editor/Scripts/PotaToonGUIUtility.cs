using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PotaToon.Editor
{
    internal static class PotaToonGUIUtility
    {
        internal const string k_Version = "1.4.3";
        internal const string k_FullGeneralPath = "PotaToon/Toon";
        internal const string k_SimpleGeneralPath = "PotaToon/SimpleToon";
        internal static readonly string[] k_Types = new string[] { "General", "Face", "Eye", "Gem" };
        internal static readonly string[] k_Paths = new string[] { k_FullGeneralPath, k_FullGeneralPath, "PotaToon/Eye", "PotaToon/Gem" };

        public static bool advancedSettingsUnlocked => !s_AdvancedSettingsUnlockedInitialized ? LoadAdvancedSettingUnlocked() : s_AdvancedSettingsUnlocked;
        public static bool materialPresetShown => !s_MaterialPresetShownInitialized ? LoadMaterialPresetShown() : s_MaterialPresetShown;
        public static bool volumePresetShown => !s_VolumePresetShownInitialized ? LoadVolumePresetShown() : s_VolumePresetShown;
        private static bool s_AdvancedSettingsUnlocked;
        private static bool s_MaterialPresetShown;
        private static bool s_VolumePresetShown;
        private static bool s_AdvancedSettingsUnlockedInitialized;
        private static bool s_MaterialPresetShownInitialized;
        private static bool s_VolumePresetShownInitialized;
        private const string k_AdvancedSettingsUnlockedString = "PotaToonAdvancedSettingsUnlocked";
        private const string k_MaterialPresetShownString = "PotaToonMaterialPresetShown";
        private const string k_VolumePresetShownString = "PotaToonVolumePresetShown";

        internal static bool SupportsPerformanceMode(int toonType)
        {
            return toonType == (int)ToonType.General || toonType == (int)ToonType.Face;
        }

        internal static int NormalizeShaderMode(int toonType, int shaderMode)
        {
            return SupportsPerformanceMode(toonType) ? shaderMode : 0;
        }

        internal static bool IsSimpleShader(Shader shader)
        {
            return shader != null && shader.name == k_SimpleGeneralPath;
        }

        internal static bool IsSimpleShader(Material material)
        {
            return material != null && IsSimpleShader(material.shader);
        }

        internal static int GetShaderMode(Material material)
        {
            if (material == null)
                return 0;

            if (material.HasProperty("_PotaToonShaderMode"))
                return NormalizeShaderMode(material.GetInt("_ToonType"), material.GetInt("_PotaToonShaderMode"));

            return IsSimpleShader(material) ? 1 : 0;
        }

        internal static string ResolveShaderPath(int toonType, int shaderMode)
        {
            int normalizedMode = NormalizeShaderMode(toonType, shaderMode);
            if (normalizedMode == 1)
                return k_SimpleGeneralPath;

            if (toonType < 0 || toonType >= k_Paths.Length)
                return string.Empty;

            return k_Paths[toonType];
        }

        internal static bool ChangeShader(Material material, int index, int renderQueue, bool showNotification = true)
        {
            return ChangeShader(material, index, renderQueue, GetShaderMode(material), showNotification);
        }

        internal static bool ChangeShader(Material material, int index, int renderQueue, int shaderMode, bool showNotification = true)
        {
            var shaderPath = ResolveShaderPath(index, shaderMode);
            if (string.IsNullOrEmpty(shaderPath))
                return false;

            var newShader = Shader.Find(shaderPath);
            if (newShader == null)
                return false;

            int previousToonType = material.GetInt("_ToonType");
            int normalizedMode = NormalizeShaderMode(index, shaderMode);
            bool shaderModeChanged = material.HasProperty("_PotaToonShaderMode") && material.GetInt("_PotaToonShaderMode") != normalizedMode;
            bool toonTypeChanged = previousToonType != index;
            if (!toonTypeChanged && material.shader == newShader && !shaderModeChanged)
                return false;

            material.shader = newShader;
            material.renderQueue = renderQueue;
            material.SetInt("_ToonType", index);
            if (material.HasProperty("_PotaToonShaderMode"))
                material.SetInt("_PotaToonShaderMode", normalizedMode);

            if (toonTypeChanged)
            {
                if (material.HasProperty("_CharShadowType"))
                    material.SetInt("_CharShadowType", index == (int)ToonType.Face ? 1 : 0);
                if (material.HasProperty("_ReceiveLightShadow"))
                    material.SetInt("_ReceiveLightShadow", index == (int)ToonType.Face ? 0 : 1);
            }

            if (showNotification)
            {
                var modeLabel = normalizedMode == 1 ? "Simple" : "Full";
                ShowNotification($"Changed to {k_Types[index]} ({modeLabel}).");
            }
            return true;
        }

        internal static void ShowNotification(string text)
        {
            var win = EditorWindow.focusedWindow;
            if (win != null)
                win.ShowNotification(new GUIContent(text));
        }

        internal static void SaveAdvancedSettingUnlocked()
        {
            EditorPrefs.SetBool(k_AdvancedSettingsUnlockedString, s_AdvancedSettingsUnlocked);
        }

        internal static bool LoadAdvancedSettingUnlocked()
        {
            s_AdvancedSettingsUnlockedInitialized = true;
            s_AdvancedSettingsUnlocked = EditorPrefs.GetBool(k_AdvancedSettingsUnlockedString);
            return s_AdvancedSettingsUnlocked;
        }

        internal static void SaveMaterialPresetShown()
        {
            EditorPrefs.SetBool(k_MaterialPresetShownString, s_MaterialPresetShown);
        }

        internal static bool LoadMaterialPresetShown()
        {
            s_MaterialPresetShownInitialized = true;
            s_MaterialPresetShown = EditorPrefs.GetBool(k_MaterialPresetShownString, true);
            return s_MaterialPresetShown;
        }

        internal static void SetMaterialPresetShown(bool shown)
        {
            s_MaterialPresetShown = shown;
            s_MaterialPresetShownInitialized = true;
            SaveMaterialPresetShown();
        }

        internal static void SaveVolumePresetShown()
        {
            EditorPrefs.SetBool(k_VolumePresetShownString, s_VolumePresetShown);
        }

        internal static bool LoadVolumePresetShown()
        {
            s_VolumePresetShownInitialized = true;
            s_VolumePresetShown = EditorPrefs.GetBool(k_VolumePresetShownString, true);
            return s_VolumePresetShown;
        }

        internal static void SetVolumePresetShown(bool shown)
        {
            s_VolumePresetShown = shown;
            s_VolumePresetShownInitialized = true;
            SaveVolumePresetShown();
        }

        internal static void DrawAdvancedSettingsButton()
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fixedHeight = 20
            };

            var buttonText = PotaToonGUIUtility.s_AdvancedSettingsUnlocked ? "Lock Advanced Settings" : "Unlock Advanced Settings";
            GUIContent buttonContent = new GUIContent(buttonText, EditorGUIUtility.IconContent(PotaToonGUIUtility.s_AdvancedSettingsUnlocked ? "LockIcon" : "LockIcon-On").image);

            if (GUILayout.Button(buttonContent, buttonStyle))
            {
                if (!PotaToonGUIUtility.s_AdvancedSettingsUnlocked)
                {
                    if (EditorUtility.DisplayDialog(
                            "[PotaToon] Unlock Advanced Settings?",
                            "Are you sure you want to unlock advanced settings?\nThese settings could cause ugly/unexpected look if you are not familiar with each feature. They require a dedicated texture or a setting to use correctly.",
                            "Yes", "No"))
                    {
                        PotaToonGUIUtility.s_AdvancedSettingsUnlocked = true;
                    }
                }
                else
                {
                    PotaToonGUIUtility.s_AdvancedSettingsUnlocked = false;
                }
            }
        }

        internal static void DrawOpenDocsButton(string url)
        {
            if (GUILayout.Button("[Open docs]", new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 }))
            {
                Application.OpenURL(url);
            }
        }
    }
}