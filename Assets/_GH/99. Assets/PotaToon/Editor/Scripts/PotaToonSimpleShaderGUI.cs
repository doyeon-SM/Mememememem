using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static PotaToon.Editor.PotaToonShaderGUISearchHelper;

namespace PotaToon.Editor
{
    public class PotaToonSimpleShaderGUI : PotaToonShaderGUIBase
    {
        private static bool[] s_FoldoutMatcaps = new bool[2];
        private static bool[] s_ShowPassHelps = new bool[3];

        private static readonly string[] k_SimpleSurfaceLabels = { "Opaque", "Cutout", "Transparent" };
        private static readonly int[] k_SimpleSurfaceValues = { (int)SurfaceType.Opaque, (int)SurfaceType.Cutout, (int)SurfaceType.Transparent };

        private static class GUIContents
        {
            public static readonly GUIContent MatCapMode = new GUIContent("Mode");
            public static readonly GUIContent MatCapMap = new GUIContent("MatCap Map");
            public static readonly GUIContent MatCapMask = new GUIContent("MatCap Mask");
            public static readonly GUIContent MatCapWeight = new GUIContent("Weight", "Controls the weight of the MatCap.");
            public static readonly GUIContent MatCapLightingDimmer = new GUIContent("Lighting Dimmer", "Controls the lighting contribution to the MatCap if 'Add' mode.");
        }

        private static int NormalizeSimpleSurface(int surfaceType)
        {
            return surfaceType >= (int)SurfaceType.Transparent ? (int)SurfaceType.Transparent : surfaceType;
        }

        private static bool HasMixedSimpleSurface(Material[] materials, int value)
        {
            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null)
                    continue;

                if (NormalizeSimpleSurface(mat.GetInt("_SurfaceType")) != value)
                    return true;
            }

            return false;
        }

        private static int ToSimpleSurfaceIndex(int surfaceType)
        {
            int normalized = NormalizeSimpleSurface(surfaceType);
            for (int i = 0; i < k_SimpleSurfaceValues.Length; i++)
            {
                if (k_SimpleSurfaceValues[i] == normalized)
                    return i;
            }

            return 0;
        }

        private static void ApplySimpleSurface(Material[] materials, int surfaceValue)
        {
            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null || mat.GetInt("_SurfaceType") == surfaceValue)
                    continue;

                Undo.RecordObject(mat, "Change Surface Type");
                mat.SetInt("_SurfaceType", surfaceValue);
                EditorUtility.SetDirty(mat);
            }
        }

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
            MaterialProperty _Cutoff = FindProperty("_Cutoff", properties);
            MaterialProperty _ZWriteMode = FindProperty("_ZWriteMode", properties);
            MaterialProperty _ZTest = FindProperty("_ZTest", properties);
            MaterialProperty _Blend = FindProperty("_Blend", properties);
            MaterialProperty _SrcBlend = FindProperty("_SrcBlend", properties);
            MaterialProperty _DstBlend = FindProperty("_DstBlend", properties);
            MaterialProperty _SrcBlendAlpha = FindProperty("_SrcBlendAlpha", properties);
            MaterialProperty _DstBlendAlpha = FindProperty("_DstBlendAlpha", properties);
            MaterialProperty _StencilComp = FindProperty("_StencilComp", properties);
            MaterialProperty _StencilRef = FindProperty("_StencilRef", properties);
            MaterialProperty _StencilPass = FindProperty("_StencilPass", properties);
            MaterialProperty _StencilFail = FindProperty("_StencilFail", properties);
            MaterialProperty _StencilZFail = FindProperty("_StencilZFail", properties);
            MaterialProperty _ClippingMask = FindProperty("_ClippingMask", properties);
            MaterialProperty _AlphaMaskMode = FindProperty("_AlphaMaskMode", properties);
            MaterialProperty _ClippingMaskCutoff = FindProperty("_ClippingMaskCutoff", properties);
            MaterialProperty _AlphaMaskScale = FindProperty("_AlphaMaskScale", properties);
            MaterialProperty _AlphaMaskValue = FindProperty("_AlphaMaskValue", properties);
            MaterialProperty _MainTex = FindProperty("_MainTex", properties);
            MaterialProperty _ShadeMap = FindProperty("_ShadeMap", properties);
            MaterialProperty _ShadowBorderMask = FindProperty("_ShadowBorderMask", properties);
            MaterialProperty _BaseColor = FindProperty("_BaseColor", properties);
            MaterialProperty _ShadeColor = FindProperty("_ShadeColor", properties);
            MaterialProperty _BaseMapHue = FindProperty("_BaseMapHue", properties);
            MaterialProperty _BaseMapSaturation = FindProperty("_BaseMapSaturation", properties);
            MaterialProperty _BaseMapContrast = FindProperty("_BaseMapContrast", properties);
            MaterialProperty _ShadeMapHue = FindProperty("_ShadeMapHue", properties);
            MaterialProperty _ShadeMapSaturation = FindProperty("_ShadeMapSaturation", properties);
            MaterialProperty _ShadeMapContrast = FindProperty("_ShadeMapContrast", properties);
            MaterialProperty _BaseStep = FindProperty("_BaseStep", properties);
            MaterialProperty _StepSmoothness = FindProperty("_StepSmoothness", properties);
            MaterialProperty _UseMidTone = FindProperty("_UseMidTone", properties);
            MaterialProperty _MidColor = FindProperty("_MidColor", properties);
            MaterialProperty _MidWidth = FindProperty("_MidWidth", properties);
            MaterialProperty _ReceiveLightShadow = FindProperty("_ReceiveLightShadow", properties);
            MaterialProperty _UseVertexColor = FindProperty("_UseVertexColor", properties);
            MaterialProperty _UseDarknessMode = FindProperty("_UseDarknessMode", properties);
            MaterialProperty _IndirectDimmer = FindProperty("_IndirectDimmer", properties);
            MaterialProperty _NormalMap = FindProperty("_NormalMap", properties);
            MaterialProperty _BumpScale = FindProperty("_BumpScale", properties);
            MaterialProperty _UseNormalMap = FindProperty("_UseNormalMap", properties);
            MaterialProperty _SpecularColor = FindProperty("_SpecularColor", properties);
            MaterialProperty _SpecularMap = FindProperty("_SpecularMap", properties);
            MaterialProperty _SpecularMask = FindProperty("_SpecularMask", properties);
            MaterialProperty _SpecularPower = FindProperty("_SpecularPower", properties);
            MaterialProperty _SpecularSmoothness = FindProperty("_SpecularSmoothness", properties);
            MaterialProperty _RimColor = FindProperty("_RimColor", properties);
            MaterialProperty _RimMask = FindProperty("_RimMask", properties);
            MaterialProperty _RimPower = FindProperty("_RimPower", properties);
            MaterialProperty _RimSmoothness = FindProperty("_RimSmoothness", properties);
            MaterialProperty _ScreenRimTint = FindProperty("_ScreenRimTint", properties);
            MaterialProperty _ScreenRimTintMode = FindProperty("_ScreenRimTintMode", properties);
            MaterialProperty _ScreenRimWidthMultiplier = FindProperty("_ScreenRimWidthMultiplier", properties);
            MaterialProperty _ScreenRimLightingDimmer = FindProperty("_ScreenRimLightingDimmer", properties);
            MaterialProperty _ScreenRimShadowFade = FindProperty("_ScreenRimShadowFade", properties);
            MaterialProperty _EmissionColor = FindProperty("_EmissionColor", properties);
            MaterialProperty _EmissionMap = FindProperty("_EmissionMap", properties);
            MaterialProperty _BlendOutlineMainTex = FindProperty("_BlendOutlineMainTex", properties);
            MaterialProperty _OutlineMode = FindProperty("_OutlineMode", properties);
            MaterialProperty _UseOutlineNormalMap = FindProperty("_UseOutlineNormalMap", properties);
            MaterialProperty _OutlineNormalMap = FindProperty("_OutlineNormalMap", properties);
            MaterialProperty _OutlineColor = FindProperty("_OutlineColor", properties);
            MaterialProperty _OutlineWidthMask = FindProperty("_OutlineWidthMask", properties);
            MaterialProperty _OutlineWidth = FindProperty("_OutlineWidth", properties);
            MaterialProperty _OutlineOffsetZ = FindProperty("_OutlineOffsetZ", properties);
            MaterialProperty _OutlineLightingDimmer = FindProperty("_OutlineLightingDimmer", properties);
            MaterialProperty _DisableCharShadow = FindProperty("_DisableCharShadow", properties);
            MaterialProperty _CharShadowType = FindProperty("_CharShadowType", properties);
            MaterialProperty _2DFaceShadowWidth = FindProperty("_2DFaceShadowWidth", properties);
            MaterialProperty _DepthBias = FindProperty("_DepthBias", properties);
            MaterialProperty _NormalBias = FindProperty("_NormalBias", properties);
            MaterialProperty _CharShadowSmoothnessOffset = FindProperty("_CharShadowSmoothnessOffset", properties);
            MaterialProperty _MatCapMode = FindProperty("_MatCapMode", properties);
            MaterialProperty _MatCapColor = FindProperty("_MatCapColor", properties);
            MaterialProperty _MatCapTex = FindProperty("_MatCapTex", properties);
            MaterialProperty _MatCapMask = FindProperty("_MatCapMask", properties);
            MaterialProperty _MatCapWeight = FindProperty("_MatCapWeight", properties);
            MaterialProperty _MatCapLightingDimmer = FindProperty("_MatCapLightingDimmer", properties);
            MaterialProperty _MatCapMode2 = FindProperty("_MatCapMode2", properties);
            MaterialProperty _MatCapColor2 = FindProperty("_MatCapColor2", properties);
            MaterialProperty _MatCapTex2 = FindProperty("_MatCapTex2", properties);
            MaterialProperty _MatCapMask2 = FindProperty("_MatCapMask2", properties);
            MaterialProperty _MatCapWeight2 = FindProperty("_MatCapWeight2", properties);
            MaterialProperty _MatCapLightingDimmer2 = FindProperty("_MatCapLightingDimmer2", properties);
            MaterialProperty _ClippingMaskCH = FindProperty("_ClippingMaskCH", properties);
            MaterialProperty _SpecularMaskCH = FindProperty("_SpecularMaskCH", properties);
            MaterialProperty _RimMaskCH = FindProperty("_RimMaskCH", properties);
            MaterialProperty _OutlineMaskCH = FindProperty("_OutlineMaskCH", properties);
            MaterialProperty _MatCapMaskCH1 = FindProperty("_MatCapMaskCH1", properties);
            MaterialProperty _MatCapMaskCH2 = FindProperty("_MatCapMaskCH2", properties);
            MaterialProperty _AOMapCH = FindProperty("_AOMapCH", properties);

            Material material = materialEditor.target as Material;
            var materials = System.Array.ConvertAll(materialEditor.targets, t => t as Material);
            if (materials == null || materials.Length == 0)
                return;

            m_ShaderType = material.GetInt("_ToonType");
            if (!PotaToonGUIUtility.SupportsPerformanceMode(m_ShaderType))
            {
                bool changedAny = false;
                foreach (var mat in materials)
                {
                    if (mat == null)
                        continue;

                    Undo.RecordObject(mat, "Disable Performance Mode");
                    changedAny |= PotaToonGUIUtility.ChangeShader(mat, mat.GetInt("_ToonType"), mat.renderQueue, 0, false);
                    SyncMaterialStateAfterPresetApply(mat);
                    EditorUtility.SetDirty(mat);
                }

                if (changedAny)
                {
                    PotaToonGUIUtility.ShowNotification("Performance Mode is only available for General and Face.");
                    return;
                }
            }

            DrawTitle(m_ShaderType, false, material, materials);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            m_ShaderType = GUILayout.Toolbar(m_ShaderType, GetToonTypeContents(), GUILayout.Width(EditorGUIUtility.currentViewWidth - 80f), GUILayout.Height(20f));
            if (GUILayout.Button("?", new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                fixedWidth = 25f,
                fixedHeight = 20f
            }))
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
                    changedAny |= PotaToonGUIUtility.ChangeShader(mat, m_ShaderType, m_RenderQueue, 1, false);
                    SyncMaterialStateAfterPresetApply(mat);
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

            foreach (var mat in materials)
            {
                if (mat == null) continue;
                mat.SetInt("_UseShadeMap", mat.GetTexture("_ShadeMap") != null ? 1 : 0);
            }

            int simpleSurface = NormalizeSimpleSurface(material.GetInt("_SurfaceType"));

            bool performanceModeChanged = false;
            PropertyGroup("Global Settings", (Property, shouldRender) =>
            {
                Property("Performance Mode", (s) =>
                {
                    performanceModeChanged = DrawPerformanceModeToggle(material, materials);
                });
                if (performanceModeChanged)
                    return;

                Property("Cull Mode", (s) => materialEditor.ShaderProperty(_Cull, new GUIContent(s)));

                var prevAuto = material.GetInt("_AutoRenderQueue") > 0;
                var prevRQ = material.renderQueue;
                var prevSurface = simpleSurface;

                Property("Surface Type", (s) =>
                {
                    int currentIndex = ToSimpleSurfaceIndex(simpleSurface);
                    EditorGUI.showMixedValue = HasMixedSimpleSurface(materials, simpleSurface);
                    EditorGUI.BeginChangeCheck();
                    int newIndex = EditorGUILayout.Popup(s, currentIndex, k_SimpleSurfaceLabels);
                    if (EditorGUI.EndChangeCheck())
                    {
                        simpleSurface = k_SimpleSurfaceValues[newIndex];
                        ApplySimpleSurface(materials, simpleSurface);
                    }
                    EditorGUI.showMixedValue = false;
                });

                if (simpleSurface == (int)SurfaceType.Transparent)
                    Property("Blending Mode", (s) => materialEditor.ShaderProperty(_Blend, new GUIContent(s)));

                if (simpleSurface == (int)SurfaceType.Transparent && material.GetInt(PotaToonShaderGUIBase.Property.BlendMode) == (int)AlphaMode.Custom)
                {
                    EditorGUI.indentLevel++;
                    Property("Src Color", (s) => materialEditor.ShaderProperty(_SrcBlend, new GUIContent(s)));
                    Property("Dst Color", (s) => materialEditor.ShaderProperty(_DstBlend, new GUIContent(s)));
                    Property("Src Alpha", (s) => materialEditor.ShaderProperty(_SrcBlendAlpha, new GUIContent(s)));
                    Property("Dst Alpha", (s) => materialEditor.ShaderProperty(_DstBlendAlpha, new GUIContent(s)));
                    EditorGUI.indentLevel--;
                }

                SetBlendingMode(materials);

                if (shouldRender) EditorGUI.BeginDisabledGroup(simpleSurface != (int)SurfaceType.Cutout);
                Property("Cut Off", (s) => materialEditor.RangeProperty(_Cutoff, s));
                if (shouldRender) EditorGUI.EndDisabledGroup();

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

                bool autoChanged = m_AutoRenderQueue != prevAuto;
                bool rqChanged = !m_AutoRenderQueue && (m_RenderQueue != prevRQ);
                bool surfaceChanged = simpleSurface != prevSurface;
                if (surfaceChanged || autoChanged || rqChanged)
                {
                    SetRenderQueueAndKeywords(materials, rqChanged, m_AutoRenderQueue, surfaceChanged, m_RenderQueue);
                }
            });

            if (performanceModeChanged)
                return;

            PropertyGroupBox("Main Settings", (Property) =>
            {
                Property("Main Tex", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s), _MainTex, _BaseColor));
                Property("Shade Tex", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s), _ShadeMap, _ShadeColor));
                Property("AO Map", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s, "Defines regions that are forced into shadow. AO Map affects both the simple toon attenuation and MidTone."), _ShadowBorderMask, _AOMapCH));
                Property("Base Step", (s) => materialEditor.RangeProperty(_BaseStep, s));
                Property("Step Smoothness", (s) => materialEditor.RangeProperty(_StepSmoothness, s));
                Property("Receive Light Shadow", (s) => materialEditor.ShaderProperty(_ReceiveLightShadow, new GUIContent(s, "If enabled, the material receives the default light shadow from the brightest light in the scene(MainLight or SpotLight). This does not affect the other shadows(base step + self character shadow).")));
                Property("$_MainSettings_HSlider", (s) => EditorGUILayout.LabelField("", GUI.skin.horizontalSlider));
                Property("MidTone", (s) => materialEditor.ShaderProperty(_UseMidTone, new GUIContent(s, "MidTone is computed from the main light, character shadow, and AO Map in Performance Mode.")));
                EditorGUI.BeginDisabledGroup(material.GetInt("_UseMidTone") == 0);
                EditorGUI.indentLevel++;
                Property("MidTone Color", (s) => materialEditor.ColorProperty(_MidColor, s));
                Property("MidTone Thickness", (s) => materialEditor.RangeProperty(_MidWidth, s));
                EditorGUI.indentLevel--;
                EditorGUI.EndDisabledGroup();
                Property("Indirect Dimmer", (s) => materialEditor.ShaderProperty(_IndirectDimmer, new GUIContent(s)));
                Property("Vertex Color", (s) => materialEditor.ShaderProperty(_UseVertexColor, new GUIContent(s)));
                Property("Backlight Mode", (s) => materialEditor.ShaderProperty(_UseDarknessMode, new GUIContent(s)));
            });

            PropertyGroupBox("Alpha Mask", (Property) =>
            {
                DrawAlphaMaskSettings(Property, materialEditor, simpleSurface, _AlphaMaskMode, _ClippingMask, null, _ClippingMaskCH, _ClippingMaskCutoff, _AlphaMaskScale, _AlphaMaskValue);
            });

            PropertyGroupBox("Color Grading", (Property) =>
            {
                Property("Base", (s) => EditorGUILayout.LabelField(s, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 }));
                Property("Hue", (s) => materialEditor.RangeProperty(_BaseMapHue, s));
                Property("Saturation", (s) => materialEditor.RangeProperty(_BaseMapSaturation, s));
                Property("Contrast", (s) => materialEditor.RangeProperty(_BaseMapContrast, s));
                Property("$_ColorGrading_HSlider", (s) => EditorGUILayout.LabelField("", GUI.skin.horizontalSlider));
                Property("Shadow", (s) => EditorGUILayout.LabelField(s, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 }));
                Property("Hue", (s) => materialEditor.RangeProperty(_ShadeMapHue, s));
                Property("Saturation", (s) => materialEditor.RangeProperty(_ShadeMapSaturation, s));
                Property("Contrast", (s) => materialEditor.RangeProperty(_ShadeMapContrast, s));
            });

            PropertyGroupBox("Normal Map", (Property) =>
            {
                Property("Use Normal Map", (s) => materialEditor.ShaderProperty(_UseNormalMap, new GUIContent(s)));
                EditorGUI.BeginDisabledGroup(material.GetInt("_UseNormalMap") == 0);
                Property("Normal Map", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s), _NormalMap));
                EditorGUI.indentLevel++;
                Property("Normal Map", (s) => materialEditor.TextureScaleOffsetProperty(_NormalMap));
                EditorGUI.indentLevel--;
                Property("Bump Scale", (s) => materialEditor.RangeProperty(_BumpScale, s));
                EditorGUI.EndDisabledGroup();
            });

            PropertyGroupBox("Rim Light", (Property) =>
            {
                Property("Fresnel (3D)", (s) => EditorGUILayout.LabelField(s, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 }));
                Property("Mask", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s, "Fresnel rim mask. UV0 is fixed in Performance Mode."), _RimMask, _RimMaskCH));
                Property("Color", (s) => materialEditor.ColorProperty(_RimColor, s));
                Property("Power", (s) => materialEditor.RangeProperty(_RimPower, s));
                Property("Smoothness", (s) => materialEditor.RangeProperty(_RimSmoothness, s));
                Property("$_RimLightSettings_HSlider", (s) => EditorGUILayout.LabelField("", GUI.skin.horizontalSlider));
                Property("Screen (2D)", (s) => EditorGUILayout.LabelField(s, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 }));
                Property("$_RimLightSettings_Space", (s) => EditorGUILayout.Space(5));
                Property("Tint", (s) => materialEditor.ColorProperty(_ScreenRimTint, s));
                EditorGUI.indentLevel++;
                Property("Tint Mode", (s) => materialEditor.ShaderProperty(_ScreenRimTintMode, new GUIContent(s, "Determines whether to multiply by or override the Screen Rim Color from the Volume settings.")));
                EditorGUI.indentLevel--;
                Property("Width Multiplier", (s) => materialEditor.RangeProperty(_ScreenRimWidthMultiplier, s));
                Property("Lighting Dimmer", (s) => materialEditor.ShaderProperty(_ScreenRimLightingDimmer, new GUIContent(s, "Controls how much scene lighting affects the Screen Rim. 0 = constant, 1 = fully lit.")));
                Property("Shadow Fade", (s) => materialEditor.ShaderProperty(_ScreenRimShadowFade, new GUIContent(s, "If enabled, the screen rim is attenuated by a shadow.")));
            });

            PropertyGroupBox("MatCap", (Property) =>
            {
                Property("MatCap 1", (s) => CustomFoldout(ref s_FoldoutMatcaps[0], "MatCap 1", () =>
                {
                    materialEditor.ShaderProperty(_MatCapMode, GUIContents.MatCapMode);
                    materialEditor.ShaderProperty(_MatCapWeight, GUIContents.MatCapWeight);
                    EditorGUI.BeginDisabledGroup(material.GetInt("_MatCapMode") != 1);
                    materialEditor.ShaderProperty(_MatCapLightingDimmer, GUIContents.MatCapLightingDimmer);
                    EditorGUI.EndDisabledGroup();
                    materialEditor.TexturePropertySingleLine(GUIContents.MatCapMap, _MatCapTex, _MatCapColor);
                    materialEditor.TexturePropertySingleLine(GUIContents.MatCapMask, _MatCapMask, _MatCapMaskCH1);
                }));

                Property("MatCap 2", (s) => CustomFoldout(ref s_FoldoutMatcaps[1], "MatCap 2", () =>
                {
                    materialEditor.ShaderProperty(_MatCapMode2, GUIContents.MatCapMode);
                    materialEditor.ShaderProperty(_MatCapWeight2, GUIContents.MatCapWeight);
                    EditorGUI.BeginDisabledGroup(material.GetInt("_MatCapMode2") != 1);
                    materialEditor.ShaderProperty(_MatCapLightingDimmer2, GUIContents.MatCapLightingDimmer);
                    EditorGUI.EndDisabledGroup();
                    materialEditor.TexturePropertySingleLine(GUIContents.MatCapMap, _MatCapTex2, _MatCapColor2);
                    materialEditor.TexturePropertySingleLine(GUIContents.MatCapMask, _MatCapMask2, _MatCapMaskCH2);
                }));
            });

            PropertyGroupBox("Outline", (Property, shouldRender) =>
            {
                if (shouldRender)
                    EditorGUI.BeginDisabledGroup(simpleSurface == (int)SurfaceType.Transparent);

                Property("Mode", (s) => materialEditor.ShaderProperty(_OutlineMode, new GUIContent(s)));
                Property("Use Normal Map", (s) => materialEditor.ShaderProperty(_UseOutlineNormalMap, new GUIContent(s)));
                Property("Normal Map", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s), _OutlineNormalMap));
                Property("Width Mask", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s), _OutlineWidthMask, _OutlineMaskCH));
                Property("Blend Main Tex", (s) => materialEditor.ShaderProperty(_BlendOutlineMainTex, new GUIContent(s)));
                Property("Outline Color", (s) => materialEditor.ColorProperty(_OutlineColor, s));
                Property("Outline Width", (s) => materialEditor.RangeProperty(_OutlineWidth, s));
                Property("Depth Offset", (s) => materialEditor.FloatProperty(_OutlineOffsetZ, s));
                Property("Lighting Dimmer", (s) => materialEditor.RangeProperty(_OutlineLightingDimmer, s));

                if (shouldRender)
                    EditorGUI.EndDisabledGroup();
            });

            var originalAdvancedSettingsUnlocked = PotaToonGUIUtility.advancedSettingsUnlocked;
            if (IsAdvancedSettingsMatched())
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.LabelField("Advanced Settings", advancedSettingsStyle);
                PotaToonGUIUtility.DrawAdvancedSettingsButton();
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            }

            EditorGUI.BeginDisabledGroup(!PotaToonGUIUtility.advancedSettingsUnlocked);

            PropertyGroupBox("Character Shadow", (Property, shouldRender) =>
            {
                Property("Disable Char Shadow", (s) => materialEditor.ShaderProperty(_DisableCharShadow, new GUIContent(s, "Toggles character self-shadowing. In some cases (e.g., bangs), disabling self-shadow can create a cleaner look.")));
                if (shouldRender) EditorGUI.BeginDisabledGroup(material.GetInt("_DisableCharShadow") == 1);
                Property("Shadow Type", (s) => materialEditor.ShaderProperty(_CharShadowType, new GUIContent(s, "We recommend using the 2D face shadow mode if face type material. However, if you prefer a more realistic shadow (i.e. physically correct), 3D shadow mode is also available.")));
                if (material.GetInt("_CharShadowType") == 0)
                {
                    Property("Depth Bias", (s) => materialEditor.RangeProperty(_DepthBias, s));
                    Property("Normal Bias", (s) => materialEditor.RangeProperty(_NormalBias, s));
                    Property("Smoothness", (s) => materialEditor.RangeProperty(_CharShadowSmoothnessOffset, s));
                }
                else
                {
                    Property("2D Shadow Width", (s) => materialEditor.RangeProperty(_2DFaceShadowWidth, s));
                }
                if (shouldRender) EditorGUI.EndDisabledGroup();
            });

            foreach (var mat in materials)
            {
                if (mat == null) continue;
                mat.SetKeyword(new LocalKeyword(mat.shader, "_USE_2D_FACE_SHADOW"), mat.GetInt("_CharShadowType") > 0);
            }

            PropertyGroupBox("High Light (Specular)", (Property) =>
            {
                Property("HighLight Map", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s), _SpecularMap, _SpecularColor));
                Property("HighLight Mask", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s), _SpecularMask, _SpecularMaskCH));
                Property("Power", (s) => materialEditor.RangeProperty(_SpecularPower, s));
                Property("Smoothness", (s) => materialEditor.RangeProperty(_SpecularSmoothness, s));

            });

            PropertyGroupBox("Emission", (Property) =>
            {
                Property("Emission Map", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s), _EmissionMap, _EmissionColor));
            });

            PropertyGroupBox("Stencil / ZTest", (Property) =>
            {
                Property("Stencil", (s) => EditorGUILayout.LabelField(s, sectionHeaderStyle));
                Property("Comp", (s) => materialEditor.ShaderProperty(_StencilComp, s));
                Property("Ref", (s) => materialEditor.RangeProperty(_StencilRef, s));
                Property("Pass", (s) => materialEditor.ShaderProperty(_StencilPass, s));
                Property("Fail", (s) => materialEditor.ShaderProperty(_StencilFail, s));
                Property("ZFail", (s) => materialEditor.ShaderProperty(_StencilZFail, s));
                Property("$_StencilZTest_HSlider", (s) => EditorGUILayout.LabelField("", GUI.skin.horizontalSlider));
                Property("ZTest", (s) => EditorGUILayout.LabelField(s, sectionHeaderStyle));
                Property("$_StencilZTest_Space", (s) => EditorGUILayout.Space(5));
                Property("ZTest Value", (s) => materialEditor.ShaderProperty(_ZTest, new GUIContent("ZTest", "Depth comparison rule for visible color passes only.")));
                Property("ZWrite", (s) => materialEditor.ShaderProperty(_ZWriteMode, new GUIContent(s, "Depth write toggle for the main visible color passes.")));
            });

            PropertyGroupBox("Pass Control", (Property, shouldRender) =>
            {
                if (shouldRender)
                    DrawInfoBox("You can Enable/Disable individual shader passes for this material.");

                Property("Shadow Caster", (s) =>
                {
                    if (!TryGetShaderPassEnabled(materials, Pass.ShadowCaster, out bool enabled, out bool hasMixed))
                        return;

                    EditorGUI.showMixedValue = hasMixed;
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.BeginHorizontal();
                    bool newEnabled = EditorGUILayout.Toggle(s, enabled);
                    if (GUILayout.Button(new GUIContent("?", "Show description"), helpButtonStyle))
                        s_ShowPassHelps[0] = !s_ShowPassHelps[0];
                    EditorGUILayout.EndHorizontal();
                    if (EditorGUI.EndChangeCheck())
                        SetShaderPassEnabled(materials, Pass.ShadowCaster, newEnabled, "Toggle Shadow Caster Pass");
                    EditorGUI.showMixedValue = false;
                    if (s_ShowPassHelps[0])
                        DrawInfoBox("Casts regular scene shadows from this material.");
                });

                Property("Character Shadow", (s) =>
                {
                    if (!TryGetShaderPassEnabled(materials, Pass.CharacterDepth, out bool enabled, out bool hasMixed))
                        return;

                    EditorGUI.showMixedValue = hasMixed;
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.BeginHorizontal();
                    bool newEnabled = EditorGUILayout.Toggle(s, enabled);
                    if (GUILayout.Button(new GUIContent("?", "Show description"), helpButtonStyle))
                        s_ShowPassHelps[1] = !s_ShowPassHelps[1];
                    EditorGUILayout.EndHorizontal();
                    if (EditorGUI.EndChangeCheck())
                        SetShaderPassEnabled(materials, Pass.CharacterDepth, newEnabled, "Toggle Character Depth Pass");
                    EditorGUI.showMixedValue = false;
                    if (s_ShowPassHelps[1])
                        DrawInfoBox("Casts PotaToon character shadow from this material.");
                });

                Property("Character Area", (s) =>
                {
                    if (!TryGetShaderPassEnabled(materials, Pass.PotaToonCharacterMask, out bool enabled, out bool hasMixed))
                        return;

                    EditorGUI.showMixedValue = hasMixed;
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.BeginHorizontal();
                    bool newEnabled = EditorGUILayout.Toggle(s, enabled);
                    if (GUILayout.Button(new GUIContent("?", "Show description"), helpButtonStyle))
                        s_ShowPassHelps[2] = !s_ShowPassHelps[2];
                    EditorGUILayout.EndHorizontal();
                    if (EditorGUI.EndChangeCheck())
                    {
                        SetShaderPassEnabled(materials, Pass.PotaToonCharacterMask, newEnabled, "Toggle Character Mask Pass");
                        if (!newEnabled)
                        {
                            bool changedScreenRimWidth = false;
                            for (int i = 0; i < materials.Length; i++)
                            {
                                var mat = materials[i];
                                if (mat == null || !mat.HasProperty("_ScreenRimWidthMultiplier"))
                                    continue;

                                if (Mathf.Approximately(mat.GetFloat("_ScreenRimWidthMultiplier"), 0.0f))
                                    continue;

                                Undo.RecordObject(mat, "Set Screen Rim Width Multiplier");
                                mat.SetFloat("_ScreenRimWidthMultiplier", 0.0f);
                                EditorUtility.SetDirty(mat);
                                changedScreenRimWidth = true;
                            }

                            if (changedScreenRimWidth)
                                PotaToonGUIUtility.ShowNotification("Character Area pass disabled.\nScreen Rim Width Multiplier set to 0.");
                        }
                    }
                    EditorGUI.showMixedValue = false;
                    if (s_ShowPassHelps[2])
                        DrawInfoBox("Marks character area. If disabled, this material is excluded from character area and will not receive character shadow or character-only post processing.");
                });
            });

            EditorGUI.EndDisabledGroup();

            if (PotaToonGUIUtility.advancedSettingsUnlocked != originalAdvancedSettingsUnlocked)
                PotaToonGUIUtility.SaveAdvancedSettingUnlocked();
        }
    }
}
