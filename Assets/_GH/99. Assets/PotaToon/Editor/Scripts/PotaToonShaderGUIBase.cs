using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static PotaToon.Editor.PotaToonShaderGUISearchHelper;
using static PotaToon.Editor.PotaToonEditorUtility;

namespace PotaToon.Editor
{

    public class PotaToonShaderGUIBase : ShaderGUI
    {
        internal static class Property
        {
            public static readonly string BlendMode = "_Blend";
            public static readonly string SrcBlend = "_SrcBlend";
            public static readonly string DstBlend = "_DstBlend";
            public static readonly string SrcBlendAlpha = "_SrcBlendAlpha";
            public static readonly string DstBlendAlpha = "_DstBlendAlpha";
        }
        internal static class Pass
        {
            public const string OITDepth = "OITDepth";
            public const string ShadowCaster = "ShadowCaster";
            public const string CharacterDepth = "CharacterDepth";
            public const string TransparentShadow = "TransparentShadow";
            public const string TransparentAlphaSum = "TransparentAlphaSum";
            public const string PotaToonCharacterMask = "PotaToonCharacterMask";
            public static readonly string[] UserControlled =
            {
                ShadowCaster,
                CharacterDepth,
                TransparentShadow,
                TransparentAlphaSum,
                PotaToonCharacterMask,
            };
        }
        internal static class OIT
        {
            public const string DisableProperty = "_DisableOIT";
            public const string SurfaceTypeProperty = "_SurfaceType";
        }
        private static int[]  s_AutoRenderQueues = new int[] { 2000, 2450, 2900, 3000 };

        protected static bool s_ShowMaininfo;
        protected static readonly string k_MainInfoString = "1. General: The default type, used for most parts of the character such as the body, clothing, and hair.\n2. Face: Recommended for facial surfaces and eyeballs.\n3. Eye: Designed specifically for pupil-only meshes. If the eyeball and pupil are not separated into different submeshes or materials, use the Face type instead.\n4. Gem: Designed for Gem type materials.";
        protected int m_ShaderType;
        protected bool m_AutoRenderQueue = true;
        protected int  m_RenderQueue = 2000;

        // Icons
        private static readonly string[] k_TypeIconNames = new string[]
        {
            "d_Avatar Icon", "HeadZoomSilhouette", "d_animationvisibilitytoggleon@2x", "sv_icon_dot8_pix16_gizmo",
        };
        private static List<GUIContent> s_TypeIconContents = new List<GUIContent>();

        // Presets
        internal static Dictionary<int, List<PotaToonMaterialPresetBase>> s_MaterialPresets = new Dictionary<int, List<PotaToonMaterialPresetBase>>();
        private static Material s_CopyBuffer;
        protected static Texture2D s_PresetButtonIcon;
        protected bool m_PrestIconInitialized;
        protected Vector2 m_ScrollPos = Vector2.zero;
        protected static readonly GUIContent k_PerformanceModeContent = new GUIContent(
            "Performance Mode",
            "Much lighter and suitable for objects that do not need fine detail, or for game-oriented use.");
        protected static readonly string k_PerformanceModeHelpString =
            "Much lighter and suitable for objects that do not need fine detail, or for game-oriented use.\n" +
            "Excluded: Refraction, OIT, Face SDF, Glitter, Hair High Light, MatCap slots beyond 2, and several advanced mask/UV controls.\n" +
            "Supported: General and Face materials only.";
        private static readonly GUIContent k_HelpButtonContent = new GUIContent("?", "Show description");
        private static bool s_ShowPerformanceModeHelp;

        protected bool DrawPerformanceModeToggle(Material material, Material[] materials)
        {
            if (!PotaToonGUIUtility.SupportsPerformanceMode(material.GetInt("_ToonType")))
                return false;

            GUIStyle helpButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                fixedWidth = 25f,
                fixedHeight = 20f
            };

            int shaderMode = PotaToonGUIUtility.GetShaderMode(material);
            bool hasMixed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null)
                    continue;

                if (PotaToonGUIUtility.GetShaderMode(mat) != shaderMode)
                {
                    hasMixed = true;
                    break;
                }
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUI.showMixedValue = hasMixed;
            EditorGUI.BeginChangeCheck();
            bool useSimpleMode = EditorGUILayout.Toggle(k_PerformanceModeContent, shaderMode == 1);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;
            if (GUILayout.Button(k_HelpButtonContent, helpButtonStyle))
                s_ShowPerformanceModeHelp = !s_ShowPerformanceModeHelp;
            EditorGUILayout.EndHorizontal();

            if (s_ShowPerformanceModeHelp)
                DrawInfoBox(k_PerformanceModeHelpString);

            if (!changed)
                return false;

            int newMode = useSimpleMode ? 1 : 0;
            bool changedAny = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null || !PotaToonGUIUtility.SupportsPerformanceMode(mat.GetInt("_ToonType")))
                    continue;

                Undo.RecordObject(mat, "Change Shader Mode");
                if (newMode == 1 && mat.GetInt("_SurfaceType") == (int)SurfaceType.Refraction)
                    mat.SetInt("_SurfaceType", (int)SurfaceType.Transparent);

                changedAny |= PotaToonGUIUtility.ChangeShader(mat, mat.GetInt("_ToonType"), mat.renderQueue, newMode, false);
                SyncMaterialStateAfterPresetApply(mat);
                EditorUtility.SetDirty(mat);
            }

            if (changedAny)
            {
                PotaToonGUIUtility.ShowNotification($"Performance Mode {(newMode == 1 ? "On" : "Off")}");
                return true;
            }

            return false;
        }

        protected void DrawTitle(int shaderType, bool showType, Material target, Material[] targets = null)
        {
            bool showPreset = PotaToonGUIUtility.materialPresetShown;

            const float titleHeight = 35f;
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft
            };
            GUIStyle versionStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
            };
            GUIStyle presetButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            var typeText = PotaToonGUIUtility.k_Types[shaderType];
            var text = showType ? $"PotaToon ({typeText})" : "PotaToon";
            var width = showType ? 100f + typeText.Length * 16f : 100f;

            EditorGUILayout.LabelField(text, titleStyle, GUILayout.Width(width), GUILayout.Height(titleHeight));
            EditorGUILayout.LabelField("v" + PotaToonGUIUtility.k_Version, versionStyle, GUILayout.Width(40), GUILayout.Height(titleHeight));
            GUILayout.FlexibleSpace();


            if (GUILayout.Button(EditorGUIUtility.IconContent("Clipboard", "Copy settings from the active material"), GUILayout.Width(titleHeight), GUILayout.Height(titleHeight)))
            {
                CopyComponent(target);
            }
            if (GUILayout.Button(EditorGUIUtility.IconContent("d_SaveAs", "Paste settings. When multiple materials are selected, applies to all selected materials using the same shader."), GUILayout.Width(titleHeight), GUILayout.Height(titleHeight)))
            {
                if (targets != null && targets.Length > 1)
                    PasteComponent(targets);
                else
                    PasteComponent(target);
            }

            var bgColor = GUI.backgroundColor;
            GUI.backgroundColor = showPreset ? new Color(0.8f, 0.8f, 1f) : bgColor;
            var presetIconConcent = s_PresetButtonIcon != null ? new GUIContent(s_PresetButtonIcon, "Preset") : EditorGUIUtility.IconContent("d_Preset.Context@2x", "|Preset");
            if (GUILayout.Button(presetIconConcent, presetButtonStyle, GUILayout.Width(titleHeight), GUILayout.Height(titleHeight)))
            {
                PotaToonGUIUtility.SetMaterialPresetShown(!showPreset);
            }
            GUI.backgroundColor = bgColor;
            EditorGUILayout.EndHorizontal();

            searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);

            EditorGUILayout.Space(4);
        }

        protected GUIContent[] GetToonTypeContents()
        {
            var types = PotaToonGUIUtility.k_Types;
            GUIContent[] toonTypeContents = new GUIContent[types.Length];

            for (int i = 0; i < types.Length; i++)
                toonTypeContents[i] = new GUIContent(types[i]);

            if (s_TypeIconContents.Count > 0)
            {
                for (int i = 0; i < types.Length; i++)
                    toonTypeContents[i].image = s_TypeIconContents[i].image;
            }

            return toonTypeContents;
        }

        protected void DrawInfoBox(string message)
        {
            GUIStyle boxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(5, 5, 5, 5),
                normal = { textColor = EditorStyles.label.normal.textColor }
            };

            GUIStyle iconStyle = new GUIStyle(EditorStyles.label)
            {
                fixedWidth = 20,
                alignment = TextAnchor.MiddleLeft
            };

            GUIStyle textStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft
            };

            EditorGUILayout.BeginHorizontal(boxStyle);
            GUILayout.Label(EditorGUIUtility.IconContent("console.infoicon"), iconStyle);
            GUILayout.Label(message, textStyle, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndHorizontal();
        }

        protected void DrawAlphaMaskSettings(System.Action<string, System.Action<string>> drawProperty, MaterialEditor materialEditor, int surfaceType,
            MaterialProperty alphaMaskMode, MaterialProperty clippingMask, MaterialProperty clippingMaskUV, MaterialProperty clippingMaskCH,
            MaterialProperty clippingMaskCutoff, MaterialProperty alphaMaskScale, MaterialProperty alphaMaskValue)
        {
            drawProperty("Mode", (s) => materialEditor.ShaderProperty(alphaMaskMode, new GUIContent(s)));

            if (alphaMaskMode.hasMixedValue)
                return;

            var mode = (AlphaMaskMode)Mathf.RoundToInt(alphaMaskMode.floatValue);
            if (mode == AlphaMaskMode.None)
                return;

            drawProperty("Mask", (s) =>
            {
                var content = new GUIContent(s, "Texture used by Alpha Mask. The selected channel is sampled.");
                if (clippingMaskUV != null)
                    materialEditor.TexturePropertySingleLine(content, clippingMask, clippingMaskUV, clippingMaskCH);
                else
                    materialEditor.TexturePropertySingleLine(content, clippingMask, clippingMaskCH);
            });

            if (mode == AlphaMaskMode.Clipping)
            {
                drawProperty("Clipping Cutoff", (s) => materialEditor.RangeProperty(clippingMaskCutoff, s));
                return;
            }

            if (surfaceType == (int)SurfaceType.Opaque)
            {
                drawProperty("Alpha Mask Notice", (s) =>
                {
                    EditorGUILayout.HelpBox("Alpha Mask alpha modes become visible on Cutout, Refraction, or Transparent surfaces. Opaque output remains opaque.", MessageType.Info);
                });
            }

            drawProperty("Invert", (s) =>
            {
                bool oldInvert = alphaMaskScale.floatValue < 0f;
                bool invert = oldInvert;
                EditorGUI.showMixedValue = alphaMaskScale.hasMixedValue;
                EditorGUI.BeginChangeCheck();
                invert = EditorGUILayout.Toggle(s, invert);
                if (EditorGUI.EndChangeCheck())
                {
                    float transparency = oldInvert ? alphaMaskValue.floatValue - 1f : alphaMaskValue.floatValue;
                    float scaleMagnitude = Mathf.Abs(alphaMaskScale.floatValue);
                    if (scaleMagnitude == 0f)
                        scaleMagnitude = 1f;
                    alphaMaskScale.floatValue = invert ? -scaleMagnitude : scaleMagnitude;
                    alphaMaskValue.floatValue = invert ? transparency + 1f : transparency;
                }
                EditorGUI.showMixedValue = false;
            });

            drawProperty("Transparency", (s) =>
            {
                bool invert = alphaMaskScale.floatValue < 0f;
                float transparency = invert ? alphaMaskValue.floatValue - 1f : alphaMaskValue.floatValue;
                EditorGUI.showMixedValue = alphaMaskScale.hasMixedValue || alphaMaskValue.hasMixedValue;
                EditorGUI.BeginChangeCheck();
                transparency = EditorGUILayout.Slider(s, transparency, -1f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    alphaMaskValue.floatValue = invert ? transparency + 1f : transparency;
                }
                EditorGUI.showMixedValue = false;
            });

            drawProperty("Scale", (s) => materialEditor.ShaderProperty(alphaMaskScale, new GUIContent(s)));
        }

        private void CopyComponent(Material mat)
        {
            if (mat == null)
                return;

            s_CopyBuffer = new Material(mat);
            PotaToonGUIUtility.ShowNotification("Copied!");
        }

        private void PasteComponent(Material mat)
        {
            if (mat == null || s_CopyBuffer == null)
                return;

            if (mat.shader != s_CopyBuffer.shader)
            {
                Debug.LogWarning("[PotaToon] Paste component shader mismatch");
                return;
            }

            var originalName = mat.name;
            Undo.RecordObject(mat, "Paste Material Properties");
            EditorUtility.CopySerialized(s_CopyBuffer, mat);
            mat.name = originalName;
            EditorUtility.SetDirty(mat);
            PotaToonGUIUtility.ShowNotification("Pasted!");
        }

        private void PasteComponent(Material[] mats)
        {
            if (mats == null || mats.Length == 0 || s_CopyBuffer == null)
                return;

            int applied = 0;
            foreach (var mat in mats)
            {
                if (mat == null)
                    continue;

                if (mat.shader != s_CopyBuffer.shader)
                {
                    Debug.LogWarning("[PotaToon] Paste component shader mismatch: " + mat.name);
                    continue;
                }

                var originalName = mat.name;
                Undo.RecordObject(mat, "Paste Material Properties");
                EditorUtility.CopySerialized(s_CopyBuffer, mat);
                mat.name = originalName;
                EditorUtility.SetDirty(mat);
                applied++;
            }

            if (applied > 1)
                PotaToonGUIUtility.ShowNotification($"Pasted to {applied} materials!");
            else if (applied == 1)
                PotaToonGUIUtility.ShowNotification("Pasted!");
        }

        protected void DrawPresetField(Material mat, Material[] mats = null)
        {
            if (!PotaToonGUIUtility.materialPresetShown)
                return;

            const float scrollHeight = 270f;
            const float itemWidth = 60f;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.HelpBox("Right-click to edit preset. Note that presets do not contain textures except for 'MatCap Map'.", MessageType.Info);

            int cols = Mathf.Max(1, Mathf.FloorToInt(EditorGUIUtility.currentViewWidth / itemWidth) - 1);

            m_ScrollPos = EditorGUILayout.BeginScrollView(  m_ScrollPos, false, true,
                                                            GUIStyle.none, GUI.skin.verticalScrollbar, GUI.skin.box,
                                                            GUILayout.Height(scrollHeight), GUILayout.ExpandWidth(true));

            var iconButtonStyle = new GUIStyle(GUI.skin.button)
            {
                imagePosition = ImagePosition.ImageAbove,
                alignment     = TextAnchor.LowerCenter,
                padding       = new RectOffset(4,4,4,4),
                wordWrap      = true,
                fontSize      = 10
            };
            var evt = Event.current;
            foreach (var materialPresets in s_MaterialPresets)
            {
                var presets = materialPresets.Value;
                if (!materialPresets.Key.Equals(m_ShaderType))
                    continue;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus", "|Create preset"), GUILayout.Height(30f)))
                {
                    if (CreateAndAddPreset(presets, mat))
                    {
                        evt.Use();
                        PopupWindow.Show(new Rect(0, 0, 0, 0), new MaterialPresetContextMenu(presets, presets.Count - 1, mat));
                    }
                }
                if (GUILayout.Button(EditorGUIUtility.IconContent("Import", "|Import preset"), GUILayout.Height(30f)))
                {
                    ImportPreset();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();

                // Display groups
                var groupedPresets = PotaToonMaterialPresetBase.SplitByDisplayIndex(presets);
                int idx = 0;
                for (int i = 0; i < groupedPresets.Count; i++)
                {
                    var currPresets = groupedPresets[i];
                    var presetCount = currPresets.Count;

                    if (presetCount == 0)
                        continue;

                    EditorGUILayout.LabelField(currPresets[0].displayGroup.ToString(), EditorStyles.boldLabel);

                    int groupedIdx = 0;
                    var rows = Mathf.CeilToInt((float)presetCount / cols);
                    for (int y = 0; y < rows; y++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        for (int x = 0; x < cols; x++)
                        {
                            if (groupedIdx < presetCount)
                            {
                                if (GUILayout.Button(presets[idx].GetIconContent(presets[idx].name), iconButtonStyle, GUILayout.Width(itemWidth), GUILayout.Height(itemWidth)))
                                {
                                    if (evt.button == 0)
                                    {
                                        var selectedPreset = presets[idx];
                                        int appliedCount = 0;

                                        if (mats != null && mats.Length > 0)
                                        {
                                            foreach (var m in mats)
                                            {
                                                if (m == null) continue;
                                                Undo.RecordObject(m, "Apply PotaToon Preset");
                                                // Ensure shader type matches preset
                                                PotaToonGUIUtility.ChangeShader(m, (int)selectedPreset._ToonType, m_RenderQueue, selectedPreset._PotaToonShaderMode, false);
                                                selectedPreset.ApplyTo(m);
                                                SyncMaterialStateAfterPresetApply(m);
                                                EditorUtility.SetDirty(m);
                                                appliedCount++;
                                            }
                                        }
                                        else if (mat != null)
                                        {
                                            Undo.RecordObject(mat, "Apply PotaToon Preset");
                                            PotaToonGUIUtility.ChangeShader(mat, (int)selectedPreset._ToonType, m_RenderQueue, selectedPreset._PotaToonShaderMode, false);
                                            selectedPreset.ApplyTo(mat);
                                            SyncMaterialStateAfterPresetApply(mat);
                                            EditorUtility.SetDirty(mat);
                                            appliedCount = 1;
                                        }

                                        if (appliedCount > 1)
                                            PotaToonGUIUtility.ShowNotification($"Applied preset: [{selectedPreset.name}] to {appliedCount} materials");
                                        else if (appliedCount == 1)
                                            PotaToonGUIUtility.ShowNotification($"Applied preset: [{selectedPreset.name}]");
                                    }
                                    else if (evt.button == 1)
                                    {
                                        evt.Use();
                                        PopupWindow.Show(new Rect(0, 0, 0, 0), new MaterialPresetContextMenu(presets, idx, mat));
                                    }
                                }
                                idx++;
                                groupedIdx++;
                            }
                            else
                            {
                                GUILayout.Space(itemWidth);
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    // Add divider if not a last group
                    if (i < groupedPresets.Count - 1)
                        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static PotaToonMaterialPresetBase CreatePresetInstance(int shaderType)
        {
            switch ((ToonType)shaderType)
            {
                case ToonType.Eye:
                    return ScriptableObject.CreateInstance<PotaToonEyeMaterialPreset>();
                case ToonType.Gem:
                    return ScriptableObject.CreateInstance<PotaToonGemMaterialPreset>();
                case ToonType.General:
                case ToonType.Face:
                default:
                    return ScriptableObject.CreateInstance<PotaToonMaterialPreset>();
            }
        }

        private bool CreateAndAddPreset(List<PotaToonMaterialPresetBase> presets, Material sourceMaterial)
        {
            var typeName = GetType().Name;
            var guids = AssetDatabase.FindAssets($"{typeName} t:MonoScript");
            if (guids == null || guids.Length == 0)
                return false;

            var scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var editorDir = Path.GetDirectoryName(scriptPath).Replace("\\Scripts", "/");
            var presetsBase = $"{editorDir}/Presets";
            var materialBase = $"{presetsBase}/Material";
            var typeString = PotaToonGUIUtility.k_Types[m_ShaderType];
            var presetsDir = $"{materialBase}/{typeString}";

            if (!AssetDatabase.IsValidFolder(presetsBase))
                AssetDatabase.CreateFolder(editorDir, "Presets");

            if (!AssetDatabase.IsValidFolder(materialBase))
                AssetDatabase.CreateFolder(presetsBase, "Material");

            if (!AssetDatabase.IsValidFolder(presetsDir))
                AssetDatabase.CreateFolder(materialBase, typeString);

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{presetsDir}/New {typeString}.asset");
            PotaToonMaterialPresetBase newPreset = CreatePresetInstance(m_ShaderType);
            if (newPreset == null)
                return false;
            newPreset._ToonType = (ToonType)m_ShaderType;
            newPreset._PotaToonShaderMode = PotaToonGUIUtility.GetShaderMode(sourceMaterial);
            AssetDatabase.CreateAsset(newPreset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            presets.Add(newPreset);
            return true;
        }

        private void ImportPreset()
        {
            var absPath = EditorUtility.OpenFilePanel("Import PotaToonMaterialPreset", "", "asset");
            if (string.IsNullOrEmpty(absPath))
                return;

            var typeName = GetType().Name;
            var guids = AssetDatabase.FindAssets($"{typeName} t:MonoScript");
            if (guids == null || guids.Length == 0)
                return;

            var scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var editorDir = Path.GetDirectoryName(scriptPath).Replace("\\Scripts", "/");
            var presetsBase = $"{editorDir}/Presets";
            var materialBase = $"{presetsBase}/Material";
            var typeString = PotaToonGUIUtility.k_Types[m_ShaderType];
            var presetsDir = $"{materialBase}/{typeString}";

            if (!AssetDatabase.IsValidFolder(presetsBase))
                AssetDatabase.CreateFolder(editorDir, "Presets");

            if (!AssetDatabase.IsValidFolder(materialBase))
                AssetDatabase.CreateFolder(presetsBase, "Material");

            if (!AssetDatabase.IsValidFolder(presetsDir))
                AssetDatabase.CreateFolder(materialBase, typeString);

            var fileName = Path.GetFileName(absPath);
            var destPath = AssetDatabase.GenerateUniqueAssetPath($"{presetsDir}/{fileName}");

            File.Copy(absPath, destPath, overwrite: false);
            AssetDatabase.ImportAsset(destPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var imported = AssetDatabase.LoadAssetAtPath<PotaToonMaterialPresetBase>(destPath);
            if (imported == null)
            {
                EditorUtility.DisplayDialog("Invalid Preset",
                    "The selected file is not a PotaToonMaterialPreset asset.", "OK");
                AssetDatabase.DeleteAsset(destPath);
                AssetDatabase.SaveAssets();
                return;
            }

            // Move preset folder based on type
            int importedType = (int)imported._ToonType;
            if (importedType != m_ShaderType)
            {
                typeString = PotaToonGUIUtility.k_Types[importedType];
                presetsDir = $"{materialBase}/{typeString}";
                var oldPath = destPath;
                destPath = AssetDatabase.GenerateUniqueAssetPath($"{presetsDir}/{fileName}");

                if (!AssetDatabase.IsValidFolder(presetsDir))
                    AssetDatabase.CreateFolder(materialBase, typeString);

                AssetDatabase.MoveAsset(oldPath, destPath);
                AssetDatabase.SaveAssets();
            }

            foreach (var materialPresets in s_MaterialPresets)
            {
                if (materialPresets.Key.Equals(importedType))
                {
                    materialPresets.Value.Add(imported);
                    PotaToonGUIUtility.ShowNotification($"Imported {imported.name} into {imported._ToonType}!");
                    return;
                }
            }
        }

        private static void LoadTypeIconsIfNeeded()
        {
            if (s_TypeIconContents.Count == 0)
            {
                for (int i = 0; i < k_TypeIconNames.Length; i++)
                    s_TypeIconContents.Add(EditorGUIUtility.IconContent(k_TypeIconNames[i]));
            }
        }

        protected void InitializePresetsAndIcons()
        {
            // Load default editor icons first if needed
            LoadTypeIconsIfNeeded();
            PotaToonMaterialPresetBase.LoadPresetIconsIfNeeded();

            var typeName = GetType().Name;
            var guids = AssetDatabase.FindAssets($"{typeName} t:MonoScript");

            if (guids == null || guids.Length == 0)
                return;

            var scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var editorDir  = Path.GetDirectoryName(scriptPath).Replace("\\Scripts", "/");

            var iconPath = $"{editorDir}/Textures/potatoon_icon.png";
            s_PresetButtonIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);

            for (int i = 0; i < PotaToonGUIUtility.k_Types.Length; i++)
                s_MaterialPresets[i] = new List<PotaToonMaterialPresetBase>();

            var presetDir = $"{editorDir}/Presets/Material";
            if (AssetDatabase.IsValidFolder(presetDir))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:PotaToonMaterialPreset", new[] { presetDir }))
                {
                    var preset = AssetDatabase.LoadAssetAtPath<PotaToonMaterialPresetBase>(AssetDatabase.GUIDToAssetPath(guid));
                    if (preset != null)
                        s_MaterialPresets[(int)preset._ToonType].Add(preset);
                }
                foreach (var guid in AssetDatabase.FindAssets("t:PotaToonEyeMaterialPreset", new[] { presetDir }))
                {
                    var preset = AssetDatabase.LoadAssetAtPath<PotaToonMaterialPresetBase>(AssetDatabase.GUIDToAssetPath(guid));
                    if (preset != null)
                        s_MaterialPresets[(int)preset._ToonType].Add(preset);
                }
                foreach (var guid in AssetDatabase.FindAssets("t:PotaToonGemMaterialPreset", new[] { presetDir }))
                {
                    var preset = AssetDatabase.LoadAssetAtPath<PotaToonMaterialPresetBase>(AssetDatabase.GUIDToAssetPath(guid));
                    if (preset != null)
                        s_MaterialPresets[(int)preset._ToonType].Add(preset);
                }
            }
        }

        private static bool IsSimpleModeMaterial(Material material)
        {
            return material != null && PotaToonGUIUtility.IsSimpleShader(material);
        }

        private static void SyncSimpleSurfacePasses(Material material)
        {
            if (!IsSimpleModeMaterial(material))
                return;

            bool isTransparent = material.HasProperty("_SurfaceType") && material.GetInt("_SurfaceType") == (int)SurfaceType.Transparent;
            material.SetShaderPassEnabled("SRPDefaultUnlit", !isTransparent);
            material.SetShaderPassEnabled("DepthOnly", !isTransparent);
            material.SetShaderPassEnabled("DepthNormals", !isTransparent);
            material.SetShaderPassEnabled("OpaqueDitherFade", !isTransparent);
            material.SetShaderPassEnabled("OpaqueDitherFadeOutline", !isTransparent);
        }

        private static void SyncCharacterShadowKeywords(Material material)
        {
            if (material == null || material.shader == null || !material.HasProperty("_CharShadowType"))
                return;

            if (material.shader.name != PotaToonGUIUtility.k_FullGeneralPath && !PotaToonGUIUtility.IsSimpleShader(material))
                return;

            material.SetKeyword(new LocalKeyword(material.shader, "_USE_2D_FACE_SHADOW"), material.GetInt("_CharShadowType") > 0);
        }

        private static Dictionary<string, bool> CaptureUserControlledPassStates(Material material)
        {
            var passStates = new Dictionary<string, bool>(Pass.UserControlled.Length);
            if (material == null)
                return passStates;

            for (int i = 0; i < Pass.UserControlled.Length; i++)
            {
                string passName = Pass.UserControlled[i];
                passStates[passName] = material.GetShaderPassEnabled(passName);
            }

            return passStates;
        }

        private static void RestoreUserControlledPassStates(Material material, Dictionary<string, bool> passStates)
        {
            if (material == null || material.shader == null || passStates == null)
                return;

            foreach (var passState in passStates)
            {
                if (material.FindPass(passState.Key) < 0)
                    continue;

                material.SetShaderPassEnabled(passState.Key, passState.Value);
            }
        }

        internal static void SyncMaterialStateAfterPresetApply(Material material)
        {
            if (material == null)
                return;

            var preservedPassStates = CaptureUserControlledPassStates(material);

            bool autoRenderQueue = material.HasProperty("_AutoRenderQueue") && material.GetInt("_AutoRenderQueue") > 0;
            SetRenderQueueAndKeywords(new[] { material }, true, autoRenderQueue, false, material.renderQueue);

            bool useCustomBlend = !IsSimpleModeMaterial(material)
                                  && material.HasProperty(Property.BlendMode)
                                  && material.GetInt(OIT.SurfaceTypeProperty) >= (int)SurfaceType.Refraction
                                  && material.GetInt(Property.BlendMode) == (int)AlphaMode.Custom;
            if (!useCustomBlend)
                SetBlendingMode(new[] { material });

            SyncOITDepthPass(material);
            SyncSimpleSurfacePasses(material);
            SyncCharacterShadowKeywords(material);
            RestoreUserControlledPassStates(material, preservedPassStates);
        }

        internal static bool ShouldEnableOITDepthPass(Material material)
        {
            if (material == null || !material.HasProperty(OIT.SurfaceTypeProperty) || IsSimpleModeMaterial(material))
                return false;

            int surfaceType = material.GetInt(OIT.SurfaceTypeProperty);
            int disableOIT = material.HasProperty(OIT.DisableProperty) ? material.GetInt(OIT.DisableProperty) : 0;
            return surfaceType >= (int)SurfaceType.Refraction && disableOIT == 0;
        }

        internal static void SyncOITDepthPass(Material material)
        {
            if (material == null)
                return;

            material.SetShaderPassEnabled(Pass.OITDepth, ShouldEnableOITDepthPass(material));
        }

        internal static void SyncOITDepthPass(Material[] materials)
        {
            if (materials == null || materials.Length == 0)
                return;

            for (int i = 0; i < materials.Length; i++)
                SyncOITDepthPass(materials[i]);
        }

        internal static bool TryGetShaderPassEnabled(Material[] materials, string passName, out bool enabled, out bool hasMixedValue)
        {
            enabled = false;
            hasMixedValue = false;
            if (materials == null || materials.Length == 0)
                return false;

            bool hasValue = false;
            bool firstValue = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null)
                    continue;

                bool passEnabled = mat.GetShaderPassEnabled(passName);
                if (!hasValue)
                {
                    hasValue = true;
                    firstValue = passEnabled;
                }
                else if (firstValue != passEnabled)
                {
                    hasMixedValue = true;
                }
            }

            enabled = firstValue;
            return hasValue;
        }

        internal static bool TryGetTransparentShadowPassEnabled(Material[] materials, out bool enabled, out bool hasMixedValue)
        {
            bool hasShadow = TryGetShaderPassEnabled(materials, Pass.TransparentShadow, out bool shadowEnabled, out bool shadowMixed);
            bool hasAlpha = TryGetShaderPassEnabled(materials, Pass.TransparentAlphaSum, out bool alphaEnabled, out bool alphaMixed);

            if (!hasShadow && !hasAlpha)
            {
                enabled = false;
                hasMixedValue = false;
                return false;
            }

            enabled = hasShadow ? shadowEnabled : alphaEnabled;
            hasMixedValue = shadowMixed || alphaMixed || (hasShadow && hasAlpha && shadowEnabled != alphaEnabled);
            return true;
        }

        internal static void SetShaderPassEnabled(Material[] materials, string passName, bool enabled, string undoName)
        {
            if (materials == null || materials.Length == 0)
                return;

            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null || mat.GetShaderPassEnabled(passName) == enabled)
                    continue;

                Undo.RecordObject(mat, undoName);
                mat.SetShaderPassEnabled(passName, enabled);
                EditorUtility.SetDirty(mat);
            }
        }

        internal static void SetTransparentShadowPassEnabled(Material[] materials, bool enabled, string undoName)
        {
            SetShaderPassEnabled(materials, Pass.TransparentShadow, enabled, undoName);
            SetShaderPassEnabled(materials, Pass.TransparentAlphaSum, enabled, undoName);
        }

        internal static void SetRenderQueueAndKeywords(Material[] materials, bool renderQueueChanged, bool autoRenderQueue, bool surfaceChanged, int renderQueue)
        {
            if (materials == null || materials.Length == 0)
                return;

            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null) continue;

                bool isSimple = IsSimpleModeMaterial(mat);
                int surfaceType = mat.GetInt("_SurfaceType");
                if (isSimple && surfaceType == (int)SurfaceType.Refraction)
                {
                    surfaceType = (int)SurfaceType.Transparent;
                    mat.SetInt("_SurfaceType", surfaceType);
                }

                if (!autoRenderQueue)
                {
                    renderQueue = renderQueueChanged ? renderQueue : mat.renderQueue;
                    switch ((SurfaceType)surfaceType)
                    {
                        case SurfaceType.Opaque:
                            if (renderQueue > 2400) renderQueue = 2400;
                            break;
                        case SurfaceType.Cutout:
                            renderQueue = Mathf.Clamp(renderQueue, 2450, 2500);
                            break;
                        case SurfaceType.Refraction:
                            renderQueue = Mathf.Clamp(renderQueue, 2501, 2900);
                            break;
                        case SurfaceType.Transparent:
                            renderQueue = Mathf.Clamp(renderQueue, 2901, 5000);
                            break;
                    }
                }

                int finalRenderQueue = renderQueue;
                if (autoRenderQueue)
                    finalRenderQueue = s_AutoRenderQueues[Mathf.Clamp(surfaceType, 0, s_AutoRenderQueues.Length - 1)];

                if (surfaceChanged)
                    mat.SetInt("_ZWriteMode", surfaceType < (int)SurfaceType.Refraction ? 1 : 0);
                mat.SetInt("_AutoRenderQueue", autoRenderQueue ? 1 : 0);
                mat.renderQueue = finalRenderQueue;

                bool alphaTest = isSimple ? surfaceType != (int)SurfaceType.Opaque : mat.renderQueue >= 2450;
                bool transparent = isSimple ? surfaceType == (int)SurfaceType.Transparent : mat.renderQueue > 2500;
                CoreUtils.SetKeyword(mat, ShaderKeywordStrings._ALPHATEST_ON, alphaTest);
                CoreUtils.SetKeyword(mat, ShaderKeywordStrings._SURFACE_TYPE_TRANSPARENT, transparent);
                SyncOITDepthPass(mat);
                SyncSimpleSurfacePasses(mat);
            }
        }

        internal static void SetBlendingMode(Material[] materials)
        {
            if (materials == null || materials.Length == 0)
                return;

            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null) continue;

                bool isSimple = IsSimpleModeMaterial(mat);
                int surfaceType = mat.GetInt("_SurfaceType");
                if (isSimple && surfaceType == (int)SurfaceType.Refraction)
                {
                    surfaceType = (int)SurfaceType.Transparent;
                    mat.SetInt("_SurfaceType", surfaceType);
                }

                if (isSimple)
                {
                    if (surfaceType < (int)SurfaceType.Transparent)
                    {
                        mat.SetInt(Property.SrcBlend, (int)BlendMode.One);
                        mat.SetInt(Property.DstBlend, (int)BlendMode.Zero);
                        mat.SetInt(Property.SrcBlendAlpha, (int)BlendMode.One);
                        mat.SetInt(Property.DstBlendAlpha, (int)BlendMode.Zero);
                    }
                    else
                    {
                        var alphaMode = (AlphaMode)mat.GetInt(Property.BlendMode);
                        switch (alphaMode)
                        {
                            case AlphaMode.Alpha:
                                mat.SetInt(Property.SrcBlend, (int)BlendMode.SrcAlpha);
                                mat.SetInt(Property.DstBlend, (int)BlendMode.OneMinusSrcAlpha);
                                mat.SetInt(Property.SrcBlendAlpha, (int)BlendMode.One);
                                mat.SetInt(Property.DstBlendAlpha, (int)BlendMode.OneMinusSrcAlpha);
                                break;
                            case AlphaMode.Premultiply:
                                mat.SetInt(Property.SrcBlend, (int)BlendMode.One);
                                mat.SetInt(Property.DstBlend, (int)BlendMode.OneMinusSrcAlpha);
                                mat.SetInt(Property.SrcBlendAlpha, (int)BlendMode.One);
                                mat.SetInt(Property.DstBlendAlpha, (int)BlendMode.OneMinusSrcAlpha);
                                break;
                            case AlphaMode.Additive:
                                mat.SetInt(Property.SrcBlend, (int)BlendMode.SrcAlpha);
                                mat.SetInt(Property.DstBlend, (int)BlendMode.One);
                                mat.SetInt(Property.SrcBlendAlpha, (int)BlendMode.One);
                                mat.SetInt(Property.DstBlendAlpha, (int)BlendMode.One);
                                break;
                            case AlphaMode.Multiply:
                                mat.SetInt(Property.SrcBlend, (int)BlendMode.DstColor);
                                mat.SetInt(Property.DstBlend, (int)BlendMode.Zero);
                                mat.SetInt(Property.SrcBlendAlpha, (int)BlendMode.Zero);
                                mat.SetInt(Property.DstBlendAlpha, (int)BlendMode.One);
                                break;
                        }
                    }

                    SyncSimpleSurfacePasses(mat);
                    continue;
                }
                if (surfaceType < (int)SurfaceType.Refraction)
                {
                    mat.SetInt(Property.SrcBlend, (int)BlendMode.One);
                    mat.SetInt(Property.DstBlend, (int)BlendMode.Zero);
                    mat.SetInt(Property.SrcBlendAlpha, (int)BlendMode.One);
                    mat.SetInt(Property.DstBlendAlpha, (int)BlendMode.Zero);
                }
                else
                {
                    var alphaMode = (AlphaMode)mat.GetInt(Property.BlendMode);
                    switch (alphaMode)
                    {
                        case AlphaMode.Alpha:
                            mat.SetInt(Property.SrcBlend, (int)BlendMode.SrcAlpha);
                            mat.SetInt(Property.DstBlend, (int)BlendMode.OneMinusSrcAlpha);
                            mat.SetInt(Property.SrcBlendAlpha, (int)BlendMode.One);
                            mat.SetInt(Property.DstBlendAlpha, (int)BlendMode.OneMinusSrcAlpha);
                            break;
                        case AlphaMode.Premultiply:
                            mat.SetInt(Property.SrcBlend, (int)BlendMode.One);
                            mat.SetInt(Property.DstBlend, (int)BlendMode.OneMinusSrcAlpha);
                            mat.SetInt(Property.SrcBlendAlpha, (int)BlendMode.One);
                            mat.SetInt(Property.DstBlendAlpha, (int)BlendMode.OneMinusSrcAlpha);
                            break;
                        case AlphaMode.Additive:
                            mat.SetInt(Property.SrcBlend, (int)BlendMode.SrcAlpha);
                            mat.SetInt(Property.DstBlend, (int)BlendMode.One);
                            mat.SetInt(Property.SrcBlendAlpha, (int)BlendMode.One);
                            mat.SetInt(Property.DstBlendAlpha, (int)BlendMode.One);
                            break;
                        case AlphaMode.Multiply:
                            mat.SetInt(Property.SrcBlend, (int)BlendMode.DstColor);
                            mat.SetInt(Property.DstBlend, (int)BlendMode.Zero);
                            mat.SetInt(Property.SrcBlendAlpha, (int)BlendMode.Zero);
                            mat.SetInt(Property.DstBlendAlpha, (int)BlendMode.One);
                            break;
                    }
                }
            }
        }
    }

    internal class MaterialPresetContextMenu : PopupWindowContent
    {
        private List<PotaToonMaterialPresetBase> m_Presets;
        private string m_TempName;
        private int m_Index;
        private Material m_Material;

        public MaterialPresetContextMenu(List<PotaToonMaterialPresetBase> presets, int idx, Material mat)
        {
            m_Presets = presets;
            m_TempName = m_Presets[idx].name;
            m_Index = idx;
            m_Material = mat;
        }

        public override Vector2 GetWindowSize() => new Vector2(250, 270);

        public override void OnGUI(Rect rect)
        {
            var preset = m_Presets[m_Index];
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Edit Preset", EditorStyles.boldLabel, GUILayout.Height(20f));
            if (GUILayout.Button("X", GUILayout.Width(20f)))
            {
                editorWindow.Close();
            }
            EditorGUILayout.EndHorizontal();
            m_TempName = GUILayout.TextField(m_TempName);

            if (GUILayout.Button("Rename", GUILayout.Height(20f)))
            {
                if (!preset.name.Equals(m_TempName, StringComparison.Ordinal))
                {
                    var oldPath = AssetDatabase.GetAssetPath(preset);
                    var newNameNoExt = Path.GetFileNameWithoutExtension(m_TempName);
                    AssetDatabase.RenameAsset(oldPath, newNameNoExt);
                    AssetDatabase.SaveAssets();
                    PotaToonGUIUtility.ShowNotification($"Renamed to {m_TempName}.");
                }
            }

            if (GUILayout.Button("Find Preset in Project", GUILayout.Height(20f)))
            {
                EditorUtility.FocusProjectWindow();
                EditorGUIUtility.PingObject(preset);
            }

            if (GUILayout.Button("Export Preset", GUILayout.Height(20f)))
            {
                ExportPreset(preset);
            }

            // Icons
            EditorGUILayout.BeginHorizontal();

            var iconPreviewStyle = new GUIStyle()
            {
                alignment = TextAnchor.MiddleCenter,
            };

            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(preset.GetIconContent(""), iconPreviewStyle, GUILayout.Width(100f), GUILayout.Height(100f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            var presetIconCount = PotaToonMaterialPresetBase.presetIconContents.Count;
            const float iconBtnSize = 25f;
            const int cols = 5;
            var rows = Mathf.CeilToInt(presetIconCount / (float)cols);

            EditorGUILayout.BeginVertical();
            for (int row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int col = 0; col < cols; col++)
                {
                    int idx = row * cols + col;
                    if (idx < presetIconCount)
                    {
                        if (GUILayout.Button(PotaToonMaterialPresetBase.presetIconContents[idx], GUILayout.Width(iconBtnSize), GUILayout.Height(iconBtnSize)))
                        {
                            Undo.RecordObject(preset, "Change PotaToonMaterialPreset Icon");
                            preset.presetIconIndex = idx;
                            EditorUtility.SetDirty(preset);
                            AssetDatabase.SaveAssets();
                            PotaToonGUIUtility.ShowNotification("Icon changed.");
                        }
                    }
                    else
                    {
                        GUILayout.Space(iconBtnSize);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            var bottomStyle = new GUIStyle() {
                padding = new RectOffset(2, 2, 0, 0)
            };

            EditorGUILayout.BeginHorizontal(bottomStyle, GUILayout.Height(20f));
            if (GUILayout.Button("Save (Override)", GUILayout.ExpandHeight(true)))
            {
                // Rename if needed
                if (!preset.name.Equals(m_TempName, StringComparison.Ordinal))
                {
                    var oldPath = AssetDatabase.GetAssetPath(preset);
                    var newNameNoExt = Path.GetFileNameWithoutExtension(m_TempName);
                    AssetDatabase.RenameAsset(oldPath, newNameNoExt);
                }
                preset.SaveFrom(m_Material);
                Undo.RecordObject(preset, "Save PotaToonMaterialPreset");
                EditorUtility.SetDirty(preset);
                AssetDatabase.SaveAssets();
                PotaToonGUIUtility.ShowNotification($"Saved {preset.name}.");
            }

            if (GUILayout.Button("Delete", GUILayout.ExpandHeight(true)))
            {
                var path = AssetDatabase.GetAssetPath(preset);
                if (EditorUtility.DisplayDialog("Delete Preset", $"Are you sure you want to delete '{preset.name}'? This operation can't be undone.", "Delete", "Cancel"))
                {
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.SaveAssets();
                    m_Presets.RemoveAt(m_Index);
                }
                editorWindow.Close();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ExportPreset(PotaToonMaterialPresetBase preset)
        {
            // Get source asset path
            var sourcePath = AssetDatabase.GetAssetPath(preset);
            if (string.IsNullOrEmpty(sourcePath))
            {
                EditorUtility.DisplayDialog("Export Failed", "Could not find the preset asset path.", "OK");
                return;
            }

            // Ask user for target save path (anywhere)
            var defaultName = preset.name + ".asset";
            var absTarget = EditorUtility.SaveFilePanel(
                "Export Material Preset",
                "", // default folder
                defaultName,
                "asset"
            );

            if (string.IsNullOrEmpty(absTarget))
                return;

            // Convert source to absolute path
            var absSource = Path.GetFullPath(sourcePath).Replace("\\", "/");

            // Copy file
            try
            {
                // Notify and refresh
                System.IO.File.Copy(absSource, absTarget, overwrite: true);
                EditorUtility.RevealInFinder(absTarget);
                var win = EditorWindow.focusedWindow;
                if (win != null)
                    win.ShowNotification(new GUIContent("Preset exported!"));
            }
            catch (System.Exception ex)
            {
                PotaToonLog($"Error exporting preset: {ex.Message}", true);
                EditorUtility.DisplayDialog("Export Failed", ex.Message, "OK");
            }
        }
    }

}
