using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PotaToon.Editor
{
    static class PotaToonSettingsProvider
    {
        private const string k_EnableDitherFadePassesHelp =
            "When disabled, player builds strip the OpaqueDitherFade shader passes. Enable this before building characters that use Dither Fade.";
        private const string k_EnableDitherFadePassesTooltip =
            "Includes the OpaqueDitherFade shader passes in player builds. Disabled by default to strip unused dither fade variants.";

        private static readonly GUIContent k_EnableDitherFadePassesContent = new GUIContent(
            "Enable Dither Fade",
            k_EnableDitherFadePassesTooltip);

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Project/PotaToon", SettingsScope.Project)
            {
                label = "PotaToon",
                keywords = new HashSet<string>
                {
                    "PotaToon",
                    "OpaqueDitherFade",
                    "Dither",
                    "Shader",
                    "Strip",
                },
                guiHandler = _ =>
                {
                    EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    float previousLabelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = Mathf.Min(160f, EditorGUIUtility.currentViewWidth * 0.45f);
                    bool enableDitherFadePasses = EditorGUILayout.Toggle(k_EnableDitherFadePassesContent, PotaToonProjectSettings.EnableDitherFadePasses);
                    EditorGUIUtility.labelWidth = previousLabelWidth;
                    GUILayout.Label(k_EnableDitherFadePassesHelp, EditorStyles.wordWrappedMiniLabel);

                    if (EditorGUI.EndChangeCheck())
                    {
                        PotaToonProjectSettings.SetDitherFadePassesEnabled(enableDitherFadePasses);
                    }
                }
            };

            return provider;
        }

        internal static bool EnableDitherFadePasses => PotaToonProjectSettings.EnableDitherFadePasses;

        internal static void SetDitherFadePassesEnabled(bool enabled)
        {
            PotaToonProjectSettings.SetDitherFadePassesEnabled(enabled);
        }
    }
}
