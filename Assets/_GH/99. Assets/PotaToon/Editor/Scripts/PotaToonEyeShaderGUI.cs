using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static PotaToon.Editor.PotaToonShaderGUISearchHelper;

namespace PotaToon.Editor
{
    public class PotaToonEyeShaderGUI : PotaToonShaderGUIBase
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (!m_PrestIconInitialized)
            {
                m_PrestIconInitialized = true;
                InitializePresetsAndIcons();
            }

            GUIStyle advancedSettingsStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };

            GUIStyle helpButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                fixedWidth = 25f,
                fixedHeight = 20f
            };

            GUIStyle sectionHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 13
            };
            
            EditorGUIUtility.labelWidth = 0f;
            EditorGUIUtility.fieldWidth = 0f;
            
            MaterialProperty _SurfaceType = FindProperty("_SurfaceType", properties);
            MaterialProperty _Cull = FindProperty("_Cull", properties);
            MaterialProperty _ZWriteMode = FindProperty("_ZWriteMode", properties);
            MaterialProperty _ZTest = FindProperty("_ZTest", properties);
            MaterialProperty _Blend = FindProperty("_Blend", properties);
            MaterialProperty _SrcBlend = FindProperty("_SrcBlend", properties);
            MaterialProperty _DstBlend = FindProperty("_DstBlend", properties);
            MaterialProperty _SrcBlendAlpha = FindProperty("_SrcBlendAlpha", properties);
            MaterialProperty _DstBlendAlpha = FindProperty("_DstBlendAlpha", properties);
            MaterialProperty _MainTex = FindProperty("_MainTex", properties);
            MaterialProperty _ClippingMask = FindProperty("_ClippingMask", properties);
            MaterialProperty _AlphaMaskMode = FindProperty("_AlphaMaskMode", properties);
            MaterialProperty _ClippingMaskCutoff = FindProperty("_ClippingMaskCutoff", properties);
            MaterialProperty _AlphaMaskScale = FindProperty("_AlphaMaskScale", properties);
            MaterialProperty _AlphaMaskValue = FindProperty("_AlphaMaskValue", properties);
            MaterialProperty _BaseColor = FindProperty("_BaseColor", properties);
            MaterialProperty _BaseMapHue = FindProperty("_BaseMapHue", properties);
            MaterialProperty _BaseMapSaturation = FindProperty("_BaseMapSaturation", properties);
            MaterialProperty _BaseMapContrast = FindProperty("_BaseMapContrast", properties);
            MaterialProperty _Exposure = FindProperty("_Exposure", properties);
            MaterialProperty _IndirectDimmer = FindProperty("_IndirectDimmer", properties);
            MaterialProperty _MinIntensity = FindProperty("_MinIntensity", properties);
            MaterialProperty _UseRefraction = FindProperty("_UseRefraction", properties);
            MaterialProperty _RefractionWeight = FindProperty("_RefractionWeight", properties);
            MaterialProperty _UseHiLight = FindProperty("_UseHiLight", properties);
            MaterialProperty _UseHiLightJitter = FindProperty("_UseHiLightJitter", properties);
            MaterialProperty _HiLightTex = FindProperty("_HiLightTex", properties);
            MaterialProperty _HiLightColor = FindProperty("_HiLightColor", properties);
            MaterialProperty _HiLightPowerR = FindProperty("_HiLightPowerR", properties);
            MaterialProperty _HiLightPowerG = FindProperty("_HiLightPowerG", properties);
            MaterialProperty _HiLightPowerB = FindProperty("_HiLightPowerB", properties);
            MaterialProperty _HiLightIntensityR = FindProperty("_HiLightIntensityR", properties);
            MaterialProperty _HiLightIntensityG = FindProperty("_HiLightIntensityG", properties);
            MaterialProperty _HiLightIntensityB = FindProperty("_HiLightIntensityB", properties);
            MaterialProperty _ClippingMaskCH = FindProperty("_ClippingMaskCH", properties);
            MaterialProperty _StencilComp = FindProperty("_StencilComp", properties);
            MaterialProperty _StencilRef = FindProperty("_StencilRef", properties);
            MaterialProperty _StencilPass = FindProperty("_StencilPass", properties);
            MaterialProperty _StencilFail = FindProperty("_StencilFail", properties);
            MaterialProperty _StencilZFail = FindProperty("_StencilZFail", properties);
            Material material = materialEditor.target as Material;
            var materials = System.Array.ConvertAll(materialEditor.targets, t => t as Material);
            if (materials == null || materials.Length == 0)
                return;

            m_ShaderType = 2;
            DrawTitle(m_ShaderType, true, material, materials);
            
            // Base Settings
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            m_ShaderType = GUILayout.Toolbar(m_ShaderType, GetToonTypeContents(), GUILayout.Width(EditorGUIUtility.currentViewWidth - 80f), GUILayout.Height(20f));
            if (GUILayout.Button("?", helpButtonStyle))
            {
                s_ShowMaininfo = !s_ShowMaininfo;
            }
            EditorGUILayout.EndHorizontal();
            
            if (EditorGUI.EndChangeCheck())
            {
                bool changedAny = false;
                foreach (var mat in materials)
                {
                    if (mat == null) continue;
                    Undo.RecordObject(mat, "Change Shader");
                    changedAny |= PotaToonGUIUtility.ChangeShader(mat, m_ShaderType, m_RenderQueue);
                    EditorUtility.SetDirty(mat);
                }
                if (changedAny)
                    return;
            }
            
            DrawPresetField(material, materials);
            
            if (s_ShowMaininfo)
            {
                DrawInfoBox(k_MainInfoString);
                PotaToonGUIUtility.DrawOpenDocsButton("https://potatoon.dev/features/material-settings");
            }
            
            var surfaceType = material.GetInt("_SurfaceType");
            
            PropertyGroup("Global Settings", (Property, shouldRender) =>
            {
                Property("Cull Mode", (s) => materialEditor.ShaderProperty(_Cull, new GUIContent(s)));
                Property("Surface Type", (s) => materialEditor.ShaderProperty(_SurfaceType, new GUIContent(s)));
                if (surfaceType >= (int)SurfaceType.Refraction)
                    Property("Blending Mode", (s) => materialEditor.ShaderProperty(_Blend, new GUIContent(s)));

                if (material.GetInt(PotaToonShaderGUI.Property.BlendMode) == (int)AlphaMode.Custom)
                {
                    if (shouldRender) EditorGUI.indentLevel++;
                    Property("Src Color", (s) => materialEditor.ShaderProperty(_SrcBlend, new GUIContent(s)));
                    Property("Dst Color", (s) => materialEditor.ShaderProperty(_DstBlend, new GUIContent(s)));
                    Property("Src Alpha", (s) => materialEditor.ShaderProperty(_SrcBlendAlpha, new GUIContent(s)));
                    Property("Dst Alpha", (s) => materialEditor.ShaderProperty(_DstBlendAlpha, new GUIContent(s)));
                    if (shouldRender) EditorGUI.indentLevel--;
                }
                if (shouldRender)
                    SetBlendingMode(materials);

                // Snapshot values to detect changes
                var prevAuto = material.GetInt("_AutoRenderQueue") > 0;
                var prevRQ = material.renderQueue;
                var prevSurface = surfaceType;

                m_AutoRenderQueue = prevAuto;
                Property("Auto Render Queue", (s) =>
                {
                    m_AutoRenderQueue = EditorGUILayout.Toggle(s, m_AutoRenderQueue);
                });

                if (shouldRender)
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginDisabledGroup(m_AutoRenderQueue);
                }

                m_RenderQueue = material.renderQueue;
                Property("Render Queue", (s) =>
                {
                    m_RenderQueue = EditorGUILayout.IntField(s, m_RenderQueue);
                });

                if (shouldRender)
                {
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }

                // Compute changes and apply only when modified
                var newSurface = material.GetInt("_SurfaceType");
                bool autoChanged = m_AutoRenderQueue != prevAuto;
                bool rqChanged = !m_AutoRenderQueue && (m_RenderQueue != prevRQ);
                bool surfaceChanged = newSurface != prevSurface;

                if (shouldRender && (surfaceChanged || autoChanged || rqChanged))
                {
                    SetRenderQueueAndKeywords(materials, rqChanged, m_AutoRenderQueue, surfaceChanged, m_RenderQueue);
                }
            });
            
            // Main
            PropertyGroupBox("Main Settings", (Property) =>
            {
                Property("Main Tex", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s), _MainTex, _BaseColor));
                Property("Exposure", (s) => materialEditor.RangeProperty(_Exposure, s));
                Property("Min Intensity", (s) => materialEditor.RangeProperty(_MinIntensity, s));
                Property("Indirect Dimmer", (s) => materialEditor.RangeProperty(_IndirectDimmer, s));
            });

            PropertyGroupBox("Alpha Mask", (Property) =>
            {
                DrawAlphaMaskSettings(Property, materialEditor, surfaceType, _AlphaMaskMode, _ClippingMask, null, _ClippingMaskCH, _ClippingMaskCutoff, _AlphaMaskScale, _AlphaMaskValue);
            });

            // Color Grading
            PropertyGroupBox("Color Grading", (Property) =>
            {
                Property("Hue", (s) => materialEditor.RangeProperty(_BaseMapHue, s));
                Property("Saturation", (s) => materialEditor.RangeProperty(_BaseMapSaturation, s));
                Property("Contrast", (s) => materialEditor.RangeProperty(_BaseMapContrast, s));
            });

            // Refraction
            PropertyGroupBox("Refraction", (Property, shouldRender) =>
            {
                Property("Use Refraction", (s) => materialEditor.ShaderProperty(_UseRefraction, s));
                if (shouldRender)
                {
                    EditorGUI.BeginDisabledGroup(material.GetInt("_UseRefraction") == 0);
                    EditorGUI.indentLevel++;
                }
                Property("Weight", (s) => materialEditor.RangeProperty(_RefractionWeight, s));
                if (shouldRender)
                {
                    EditorGUI.indentLevel--;
                    EditorGUI.EndDisabledGroup();
                }
            });
            
            // Advanced Settings
            var originalAdvancedSettingsUnlocked = PotaToonGUIUtility.advancedSettingsUnlocked;
            if (IsAdvancedSettingsMatched())
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.LabelField("Advanced Settings", advancedSettingsStyle);
                PotaToonGUIUtility.DrawAdvancedSettingsButton();
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            }

            EditorGUI.BeginDisabledGroup(!PotaToonGUIUtility.advancedSettingsUnlocked);

            // High Light
            PropertyGroupBox("High Light", (Property, shouldRender) =>
            {
                Property("Use High Light", (s) => materialEditor.ShaderProperty(_UseHiLight, s));
                if (shouldRender)
                {
                    EditorGUI.BeginDisabledGroup(material.GetInt("_UseHiLight") == 0);
                    EditorGUI.indentLevel++;
                }
                Property("Jitter", (s) => materialEditor.ShaderProperty(_UseHiLightJitter, s));
                Property("Hi Tex", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s), _HiLightTex, _HiLightColor));
                Property("Power R", (s) => materialEditor.RangeProperty(_HiLightPowerR, s));
                Property("Power G", (s) => materialEditor.RangeProperty(_HiLightPowerG, s));
                Property("Power B", (s) => materialEditor.RangeProperty(_HiLightPowerB, s));
                Property("Intensity R", (s) => materialEditor.RangeProperty(_HiLightIntensityR, s));
                Property("Intensity G", (s) => materialEditor.RangeProperty(_HiLightIntensityG, s));
                Property("Intensity B", (s) => materialEditor.RangeProperty(_HiLightIntensityB, s));
                if (shouldRender)
                {
                    EditorGUI.indentLevel--;
                    EditorGUI.EndDisabledGroup();
                }
            });

            foreach (var mat in materials)
            {
                if (mat == null) continue;
                mat.SetKeyword(new LocalKeyword(mat.shader, "_USE_EYE_HI_LIGHT"), mat.GetInt("_UseHiLight") > 0);
            }
            
            // Stencil / ZTest
            PropertyGroupBox("Stencil / ZTest", (Property) =>
            {
                Property("Stencil", (s) => EditorGUILayout.LabelField(s, sectionHeaderStyle));
                Property("Comp", (s) => materialEditor.ShaderProperty(_StencilComp, s));
                Property("Ref", (s) => materialEditor.RangeProperty(_StencilRef, s));
                Property("Pass", (s) => materialEditor.ShaderProperty(_StencilPass, s));
                Property("Fail", (s) => materialEditor.ShaderProperty(_StencilFail, s));
                Property("ZFail", (s) => materialEditor.ShaderProperty(_StencilZFail, s));
                Property("$_StencilZTest_HSlider", (s) => EditorGUILayout.LabelField("", GUI.skin.horizontalSlider));
                Property("$_StencilZTest_Space", (s) => EditorGUILayout.Space(5));
                Property("ZTest", (s) => EditorGUILayout.LabelField(s, sectionHeaderStyle));
                Property("ZTest Value", (s) => materialEditor.ShaderProperty(_ZTest, new GUIContent("ZTest", "Depth comparison rule for visible color passes only.")));
                Property("ZWrite", (s) => materialEditor.ShaderProperty(_ZWriteMode, new GUIContent(s, "Depth write toggle for the main visible color passes.")));
            });
            
            EditorGUI.EndDisabledGroup();

            if (PotaToonGUIUtility.advancedSettingsUnlocked != originalAdvancedSettingsUnlocked)
                PotaToonGUIUtility.SaveAdvancedSettingUnlocked();
        }
    }
}

