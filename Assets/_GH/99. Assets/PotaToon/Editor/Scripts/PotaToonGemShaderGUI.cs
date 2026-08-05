using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static PotaToon.Editor.PotaToonShaderGUISearchHelper;

namespace PotaToon.Editor
{
    public class PotaToonGemShaderGUI : PotaToonShaderGUIBase
    {
        private static bool[] s_FoldoutMatcaps = new bool[4];
        private static bool[] s_ShowPassHelps = new bool[2];
        private static readonly string[] s_GemTypeDisplayNames =
        {
            "Glass",
            "Crystal",
            "Diamond",
            "Ruby",
            "Emerald",
            "Sapphire",
            "Opal",
            "Custom"
        };

        private enum GemType
        {
            Glass = 0,
            Crystal = 1,
            Diamond = 2,
            Ruby = 3,
            Emerald = 4,
            Sapphire = 5,
            Opal = 6,
            Custom = 7
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

            MaterialProperty _MainTex = FindProperty("_MainTex", properties);
            MaterialProperty _BaseColor = FindProperty("_BaseColor", properties);
            MaterialProperty _BaseMapHue = FindProperty("_BaseMapHue", properties);
            MaterialProperty _BaseMapSaturation = FindProperty("_BaseMapSaturation", properties);
            MaterialProperty _BaseMapContrast = FindProperty("_BaseMapContrast", properties);
            MaterialProperty _ClippingMask = FindProperty("_ClippingMask", properties);
            MaterialProperty _AlphaMaskMode = FindProperty("_AlphaMaskMode", properties);
            MaterialProperty _ClippingMaskCutoff = FindProperty("_ClippingMaskCutoff", properties);
            MaterialProperty _AlphaMaskScale = FindProperty("_AlphaMaskScale", properties);
            MaterialProperty _AlphaMaskValue = FindProperty("_AlphaMaskValue", properties);
            MaterialProperty _ClippingMaskCH = FindProperty("_ClippingMaskCH", properties);
            MaterialProperty _NormalMap = FindProperty("_NormalMap", properties);
            MaterialProperty _BumpScale = FindProperty("_BumpScale", properties);
            MaterialProperty _UseNormalMap = FindProperty("_UseNormalMap", properties);
            MaterialProperty _Roughness = FindProperty("_Roughness", properties);
            MaterialProperty _IndirectDimmer = FindProperty("_IndirectDimmer", properties);
            MaterialProperty _ReceiveLightShadow = FindProperty("_ReceiveLightShadow", properties);

            MaterialProperty _GemType = FindProperty("_GemType", properties);
            MaterialProperty _GemShine = FindProperty("_GemShine", properties);

            MaterialProperty _ClearcoatIntensity = FindProperty("_ClearcoatIntensity", properties);
            MaterialProperty _ClearcoatRoughness = FindProperty("_ClearcoatRoughness", properties);

            MaterialProperty _TransmissionStrength = FindProperty("_TransmissionStrength", properties);
            MaterialProperty _AbsorptionColor = FindProperty("_AbsorptionColor", properties);
            MaterialProperty _Thickness = FindProperty("_Thickness", properties);
            MaterialProperty _BaseStrength = FindProperty("_BaseStrength", properties);
            MaterialProperty _ChromaticAberration = FindProperty("_ChromaticAberration", properties);
            MaterialProperty _RefractionStrength = FindProperty("_RefractionStrength", properties);
            MaterialProperty _RefractionBlurWeight = FindProperty("_RefractionBlurWeight", properties);
            MaterialProperty _RefractionFresnelPower = FindProperty("_RefractionFresnelPower", properties);
            MaterialProperty _ParticleIntensity = FindProperty("_ParticleIntensity", properties);
            MaterialProperty _ParticleLightingDimmer = FindProperty("_ParticleLightingDimmer", properties);
            MaterialProperty _ParticleLoop = FindProperty("_ParticleLoop", properties);
            MaterialProperty _ParticleColor = FindProperty("_ParticleColor", properties);
            MaterialProperty _UseGlitter = FindProperty("_UseGlitter", properties);
            MaterialProperty _GlitterColor = FindProperty("_GlitterColor", properties);
            MaterialProperty _GlitterColorTex = FindProperty("_GlitterColorTex", properties);
            MaterialProperty _GlitterMainStrength = FindProperty("_GlitterMainStrength", properties);
            MaterialProperty _GlitterEnableLighting = FindProperty("_GlitterEnableLighting", properties);
            MaterialProperty _GlitterBackfaceMask = FindProperty("_GlitterBackfaceMask", properties);
            MaterialProperty _GlitterApplyTransparency = FindProperty("_GlitterApplyTransparency", properties);
            MaterialProperty _GlitterShadowMask = FindProperty("_GlitterShadowMask", properties);
            MaterialProperty _GlitterParticleSize = FindProperty("_GlitterParticleSize", properties);
            MaterialProperty _GlitterScaleRandomize = FindProperty("_GlitterScaleRandomize", properties);
            MaterialProperty _GlitterContrast = FindProperty("_GlitterContrast", properties);
            MaterialProperty _GlitterSensitivity = FindProperty("_GlitterSensitivity", properties);
            MaterialProperty _GlitterBlinkSpeed = FindProperty("_GlitterBlinkSpeed", properties);
            MaterialProperty _GlitterAngleLimit = FindProperty("_GlitterAngleLimit", properties);
            MaterialProperty _GlitterLightDirection = FindProperty("_GlitterLightDirection", properties);
            MaterialProperty _GlitterColorRandomness = FindProperty("_GlitterColorRandomness", properties);
            MaterialProperty _GlitterNormalStrength = FindProperty("_GlitterNormalStrength", properties);
            MaterialProperty _GlitterPostContrast = FindProperty("_GlitterPostContrast", properties);

            MaterialProperty _UseRim = FindProperty("_UseRim", properties);
            MaterialProperty _RimColor = FindProperty("_RimColor", properties);
            MaterialProperty _RimPower = FindProperty("_RimPower", properties);
            MaterialProperty _RimSmoothness = FindProperty("_RimSmoothness", properties);
            MaterialProperty _RimMask = FindProperty("_RimMask", properties);
            MaterialProperty _RimMaskCH = FindProperty("_RimMaskCH", properties);
            MaterialProperty _ScreenRimTint = FindProperty("_ScreenRimTint", properties);
            MaterialProperty _ScreenRimTintMode = FindProperty("_ScreenRimTintMode", properties);
            MaterialProperty _ScreenRimWidthMultiplier = FindProperty("_ScreenRimWidthMultiplier", properties);
            MaterialProperty _ScreenRimLightingDimmer = FindProperty("_ScreenRimLightingDimmer", properties);
            MaterialProperty _ScreenRimShadowFade = FindProperty("_ScreenRimShadowFade", properties);

            MaterialProperty _MatCapMode = FindProperty("_MatCapMode", properties);
            MaterialProperty _MatCapTex = FindProperty("_MatCapTex", properties);
            MaterialProperty _MatCapMask = FindProperty("_MatCapMask", properties);
            MaterialProperty _MatCapMaskCH = FindProperty("_MatCapMaskCH", properties);
            MaterialProperty _MatCapColor = FindProperty("_MatCapColor", properties);
            MaterialProperty _MatCapWeight = FindProperty("_MatCapWeight", properties);
            MaterialProperty _MatCapLightingDimmer = FindProperty("_MatCapLightingDimmer", properties);
            MaterialProperty _MatCapMode2 = FindProperty("_MatCapMode2", properties);
            MaterialProperty _MatCapTex2 = FindProperty("_MatCapTex2", properties);
            MaterialProperty _MatCapMask2 = FindProperty("_MatCapMask2", properties);
            MaterialProperty _MatCapMaskCH2 = FindProperty("_MatCapMaskCH2", properties);
            MaterialProperty _MatCapColor2 = FindProperty("_MatCapColor2", properties);
            MaterialProperty _MatCapWeight2 = FindProperty("_MatCapWeight2", properties);
            MaterialProperty _MatCapLightingDimmer2 = FindProperty("_MatCapLightingDimmer2", properties);
            MaterialProperty _MatCapMode3 = FindProperty("_MatCapMode3", properties);
            MaterialProperty _MatCapTex3 = FindProperty("_MatCapTex3", properties);
            MaterialProperty _MatCapMask3 = FindProperty("_MatCapMask3", properties);
            MaterialProperty _MatCapMaskCH3 = FindProperty("_MatCapMaskCH3", properties);
            MaterialProperty _MatCapColor3 = FindProperty("_MatCapColor3", properties);
            MaterialProperty _MatCapWeight3 = FindProperty("_MatCapWeight3", properties);
            MaterialProperty _MatCapLightingDimmer3 = FindProperty("_MatCapLightingDimmer3", properties);
            MaterialProperty _MatCapMode4 = FindProperty("_MatCapMode4", properties);
            MaterialProperty _MatCapTex4 = FindProperty("_MatCapTex4", properties);
            MaterialProperty _MatCapMask4 = FindProperty("_MatCapMask4", properties);
            MaterialProperty _MatCapMaskCH4 = FindProperty("_MatCapMaskCH4", properties);
            MaterialProperty _MatCapColor4 = FindProperty("_MatCapColor4", properties);
            MaterialProperty _MatCapWeight4 = FindProperty("_MatCapWeight4", properties);
            MaterialProperty _MatCapLightingDimmer4 = FindProperty("_MatCapLightingDimmer4", properties);

            MaterialProperty _DisableCharShadow = FindProperty("_DisableCharShadow", properties);
            MaterialProperty _DisableOIT = FindProperty("_DisableOIT", properties);
            MaterialProperty _CharShadowSmoothnessOffset = FindProperty("_CharShadowSmoothnessOffset", properties);

            MaterialProperty _BaseMapUV = FindProperty("_BaseMapUV", properties);
            MaterialProperty _NormalMapUV = FindProperty("_NormalMapUV", properties);
            MaterialProperty _ClippingMaskUV = FindProperty("_ClippingMaskUV", properties);
            MaterialProperty _RimMaskUV = FindProperty("_RimMaskUV", properties);
            MaterialProperty _GlitterMapUV = FindProperty("_GlitterMapUV", properties);
            MaterialProperty _MatCapUV1 = FindProperty("_MatCapUV1", properties);
            MaterialProperty _MatCapUV2 = FindProperty("_MatCapUV2", properties);
            MaterialProperty _MatCapUV3 = FindProperty("_MatCapUV3", properties);
            MaterialProperty _MatCapUV4 = FindProperty("_MatCapUV4", properties);

            MaterialProperty _StencilComp = FindProperty("_StencilComp", properties);
            MaterialProperty _StencilRef = FindProperty("_StencilRef", properties);
            MaterialProperty _StencilPass = FindProperty("_StencilPass", properties);
            MaterialProperty _StencilFail = FindProperty("_StencilFail", properties);
            MaterialProperty _StencilZFail = FindProperty("_StencilZFail", properties);

            Material material = materialEditor.target as Material;
            var materials = System.Array.ConvertAll(materialEditor.targets, t => t as Material);
            if (materials == null || materials.Length == 0)
                return;

            bool surfaceForced = false;
            bool blendForced = false;
            const int gemToonType = (int)ToonType.Gem;
            const int refractionSurface = (int)SurfaceType.Refraction;
            const int premultiplyBlend = (int)AlphaMode.Premultiply;

            foreach (var mat in materials)
            {
                if (mat == null) continue;

                bool shaderFixed = false;
                if (mat.GetInt("_ToonType") != gemToonType)
                {
                    Undo.RecordObject(mat, "Fix Toon Type");
                    PotaToonGUIUtility.ChangeShader(mat, gemToonType, mat.renderQueue, false);
                    EditorUtility.SetDirty(mat);
                    shaderFixed = true;
                }

                bool surfaceFixed = false;
                if (mat.GetInt("_SurfaceType") != refractionSurface)
                {
                    Undo.RecordObject(mat, "Fix Surface Type");
                    mat.SetInt("_SurfaceType", refractionSurface);
                    EditorUtility.SetDirty(mat);
                    surfaceForced = true;
                    surfaceFixed = true;
                }

                if (shaderFixed || surfaceFixed)
                {
                    if (mat.GetInt(PotaToonShaderGUIBase.Property.BlendMode) != premultiplyBlend)
                    {
                        Undo.RecordObject(mat, "Set Blend Mode");
                        mat.SetInt(PotaToonShaderGUIBase.Property.BlendMode, premultiplyBlend);
                        EditorUtility.SetDirty(mat);
                        blendForced = true;
                    }
                }
            }

            if (blendForced)
            {
                SetBlendingMode(materials);
            }

            m_ShaderType = (int)ToonType.Gem;
            DrawTitle(m_ShaderType, false, material, materials);

            // Toon type selection
            m_ShaderType = material.GetInt("_ToonType");
            
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
                Property("Cull Mode", (s) => materialEditor.ShaderProperty(_Cull, new GUIContent(s, "Choose which side of polygons is rendered. Back is usually best.")));
                if (shouldRender) EditorGUI.BeginDisabledGroup(true);
                Property("Surface Type", (s) => materialEditor.ShaderProperty(_SurfaceType, new GUIContent(s, "Defines how the material blends with the scene.")));
                if (shouldRender) EditorGUI.EndDisabledGroup();

                if (surfaceType == (int)SurfaceType.Cutout)
                    Property("Alpha Cutoff", (s) => materialEditor.ShaderProperty(_Cutoff, new GUIContent(s, "Pixels below this alpha are fully cut out.")));

                if (surfaceType >= (int)SurfaceType.Refraction)
                    Property("Blending Mode", (s) => materialEditor.ShaderProperty(_Blend, new GUIContent(s, "Controls how gem color mixes with the background.")));

                if (material.GetInt(PotaToonShaderGUIBase.Property.BlendMode) == (int)AlphaMode.Custom)
                {
                    if (shouldRender) EditorGUI.indentLevel++;
                    Property("Src Color", (s) => materialEditor.ShaderProperty(_SrcBlend, new GUIContent(s, "Source color blend factor (advanced).")));
                    Property("Dst Color", (s) => materialEditor.ShaderProperty(_DstBlend, new GUIContent(s, "Destination color blend factor (advanced).")));
                    Property("Src Alpha", (s) => materialEditor.ShaderProperty(_SrcBlendAlpha, new GUIContent(s, "Source alpha blend factor (advanced).")));
                    Property("Dst Alpha", (s) => materialEditor.ShaderProperty(_DstBlendAlpha, new GUIContent(s, "Destination alpha blend factor (advanced).")));
                    if (shouldRender) EditorGUI.indentLevel--;
                }

                SetBlendingMode(materials);

                var prevAuto = material.GetInt("_AutoRenderQueue") > 0;
                var prevRQ = material.renderQueue;
                var prevSurface = surfaceType;

                m_AutoRenderQueue = prevAuto;
                Property("Auto Render Queue", (s) =>
                {
                    m_AutoRenderQueue = EditorGUILayout.Toggle(new GUIContent(s, "Automatically sets render queue from the current surface type."), m_AutoRenderQueue);
                });

                if (shouldRender)
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginDisabledGroup(m_AutoRenderQueue);
                }
                m_RenderQueue = material.renderQueue;
                Property("Render Queue", (s) =>
                {
                    m_RenderQueue = EditorGUILayout.IntField(new GUIContent(s, "Manual render order. Lower draws earlier, higher draws later."), m_RenderQueue);
                });
                if (shouldRender)
                {
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }

                var newSurface = material.GetInt("_SurfaceType");
                bool autoChanged = m_AutoRenderQueue != prevAuto;
                bool rqChanged = !m_AutoRenderQueue && (m_RenderQueue != prevRQ);
                bool surfaceChanged = newSurface != prevSurface || surfaceForced;

                if (surfaceChanged || autoChanged || rqChanged)
                {
                    SetRenderQueueAndKeywords(materials, rqChanged, m_AutoRenderQueue, surfaceChanged, m_RenderQueue);
                }
            });

            PropertyGroupBox("Main Settings", (Property, shouldRender) =>
            {
                Property("Base", (s) => EditorGUILayout.LabelField(s, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 }));
                Property("Base Map", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s, "Main color texture. Alpha controls transparency."), _MainTex, _BaseColor, _BaseMapUV));
                Property("Indirect Dimmer", (s) => materialEditor.ShaderProperty(_IndirectDimmer, new GUIContent(s, "Controls the intensity of indirect lighting. (LightMap, Light Probe, Adaptive Probe Volume, Reflection Probe, Skybox)")));
                Property("Receive Light Shadow", (s) => materialEditor.ShaderProperty(_ReceiveLightShadow, new GUIContent(s, "If enabled, the material receives the default light shadow from the brightest light in the scene(MainLight or SpotLight). This does not affect the other shadows(base step + self character shadow).")));

                Property("$_MainSettings_HSlider", (s) => EditorGUILayout.LabelField("", GUI.skin.horizontalSlider));
                Property("Gem", (s) => EditorGUILayout.LabelField(s, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 }));
                bool gemTypeChanged = false;
                Property("Gem Type", (s) =>
                {
                    int currentGemType = Mathf.RoundToInt(_GemType.floatValue);
                    currentGemType = Mathf.Clamp(currentGemType, 0, s_GemTypeDisplayNames.Length - 1);
                    EditorGUI.showMixedValue = _GemType.hasMixedValue;
                    EditorGUI.BeginChangeCheck();
                    int newGemType = EditorGUILayout.Popup(
                        new GUIContent(s, "Select a gem preset. Custom lets you tune Shine and Base Strength freely."),
                        currentGemType,
                        s_GemTypeDisplayNames);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _GemType.floatValue = newGemType;
                        gemTypeChanged = true;
                    }
                    EditorGUI.showMixedValue = false;
                });

                var gemTypeValue = _GemType.hasMixedValue ? -1 : (int)_GemType.floatValue;
                bool isCustomGem = gemTypeValue == (int)GemType.Custom;
                if (gemTypeChanged && !_GemType.hasMixedValue && !isCustomGem)
                {
                    if (TryGetGemPreset((GemType)gemTypeValue, out GemPreset preset))
                    {
                        float presetShine = ComputeShineFromIOR(preset.Ior);
                        foreach (var mat in materials)
                        {
                            if (mat == null) continue;
                            Undo.RecordObject(mat, "Apply Gem Preset");
                            mat.SetFloat("_GemShine", presetShine);
                            mat.SetFloat("_Roughness", preset.Roughness);
                            mat.SetFloat("_ClearcoatIntensity", preset.ClearcoatIntensity);
                            mat.SetFloat("_ClearcoatRoughness", preset.ClearcoatRoughness);
                            mat.SetFloat("_ParticleIntensity", preset.ParticleIntensity);
                            mat.SetFloat("_ParticleLoop", preset.ParticleLoop);
                            mat.SetFloat("_TransmissionStrength", preset.TransmissionStrength);
                            mat.SetColor("_AbsorptionColor", preset.AbsorptionColor);
                            mat.SetFloat("_Thickness", preset.Thickness);
                            mat.SetFloat("_BaseStrength", preset.BaseStrength);
                            mat.SetFloat("_ChromaticAberration", preset.ChromaticAberration);
                            mat.SetFloat("_RefractionStrength", preset.RefractionStrength);
                            mat.SetFloat("_RefractionBlurWeight", preset.RefractionBlurWeight);
                            mat.SetFloat("_RefractionFresnelPower", preset.RefractionFresnelPower);
                            EditorUtility.SetDirty(mat);
                        }
                        _GemShine.floatValue = presetShine;
                        _Roughness.floatValue = preset.Roughness;
                        _ClearcoatIntensity.floatValue = preset.ClearcoatIntensity;
                        _ClearcoatRoughness.floatValue = preset.ClearcoatRoughness;
                        _ParticleIntensity.floatValue = preset.ParticleIntensity;
                        _ParticleLoop.floatValue = preset.ParticleLoop;
                        _TransmissionStrength.floatValue = preset.TransmissionStrength;
                        _AbsorptionColor.colorValue = preset.AbsorptionColor;
                        _Thickness.floatValue = preset.Thickness;
                        _BaseStrength.floatValue = preset.BaseStrength;
                        _ChromaticAberration.floatValue = preset.ChromaticAberration;
                        _RefractionStrength.floatValue = preset.RefractionStrength;
                        _RefractionBlurWeight.floatValue = preset.RefractionBlurWeight;
                        _RefractionFresnelPower.floatValue = preset.RefractionFresnelPower;
                    }
                }

                if (shouldRender)
                {
                    EditorGUI.BeginDisabledGroup(_GemType.hasMixedValue || !isCustomGem);
                    EditorGUI.indentLevel++;
                }
                Property("Base Strength", (s) => materialEditor.ShaderProperty(_BaseStrength, new GUIContent(s, "Controls how strongly the gem body color shows up. Lower for clearer gems, higher for milkier looks.")));
                Property("Shine", (s) => materialEditor.ShaderProperty(_GemShine, new GUIContent(s, "Main sparkle control. Higher values make reflections stronger and sharper.")));
                if (shouldRender)
                {
                    var shineValue = _GemShine.floatValue;
                    var computedIor = ComputeGemIOR(shineValue);
                    string computedIorText = _GemType.hasMixedValue || _GemShine.hasMixedValue
                        ? "Computed IOR: (mixed values)"
                        : $"Computed IOR: {computedIor:0.00}";
                    EditorGUI.EndDisabledGroup();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.HelpBox(computedIorText, MessageType.None);
                    if (GUILayout.Button("See IOR List", GUILayout.Width(100)))
                        Application.OpenURL("https://pixelandpoly.com/ior.html");
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.indentLevel--;
                }
                Property("Roughness", (s) => materialEditor.ShaderProperty(_Roughness, new GUIContent(s, "Surface roughness. Lower = smooth/sharp reflections, higher = rough/blurred reflections.")));

                bool enableChromatic = _ChromaticAberration.hasMixedValue
                    || (_TransmissionStrength.hasMixedValue || _TransmissionStrength.floatValue > 0.0f)
                    || _IndirectDimmer.hasMixedValue || _IndirectDimmer.floatValue > 0.0f
                    || _RefractionStrength.hasMixedValue || Mathf.Abs(_RefractionStrength.floatValue) > 0.0001f;
                if (shouldRender) EditorGUI.BeginDisabledGroup(!enableChromatic);
                Property("Chromatic Aberration", (s) => materialEditor.ShaderProperty(_ChromaticAberration, new GUIContent(s, "Prism-like RGB split near edges. Use subtle values for natural gems.")));
                if (shouldRender) EditorGUI.EndDisabledGroup();

                Property("Particle Intensity", (s) => materialEditor.ShaderProperty(_ParticleIntensity, new GUIContent(s, "Overall strength of internal sparkle particles.")));
                EditorGUI.indentLevel++;
                bool enableGemParticle = _ParticleIntensity.hasMixedValue || _ParticleIntensity.floatValue > 0.0f;
                if (shouldRender) EditorGUI.BeginDisabledGroup(!enableGemParticle);
                Property("Lighting Dimmer", (s) => materialEditor.ShaderProperty(_ParticleLightingDimmer, new GUIContent(s, "Controls how much scene lighting affects sparkle. 0 = fixed color, 1 = fully lit.")));
                Property("Loop", (s) => materialEditor.ShaderProperty(_ParticleLoop, new GUIContent(s, "Sparkle pattern density. Higher values create denser sparkle.")));
                Property("Color", (s) => materialEditor.ShaderProperty(_ParticleColor, new GUIContent(s, "Tint color of sparkle particles.")));
                if (shouldRender) EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;

                Property("Transmission Strength", (s) => materialEditor.ShaderProperty(_TransmissionStrength, new GUIContent(s, "Controls how much light passes through the gem. Higher values make it feel more see-through and strengthen refracted color.")));
                EditorGUI.indentLevel++;
                bool enableTransmission = _TransmissionStrength.hasMixedValue || _TransmissionStrength.floatValue > 0.0f;
                if (shouldRender) EditorGUI.BeginDisabledGroup(!enableTransmission);
                Property("Absorption Color", (s) => materialEditor.ShaderProperty(_AbsorptionColor, new GUIContent(s, "Color absorbed inside the gem volume. This tints transmitted light.")));
                if (shouldRender) EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            });

            PropertyGroupBox("Alpha Mask", (Property) =>
            {
                DrawAlphaMaskSettings(Property, materialEditor, surfaceType, _AlphaMaskMode, _ClippingMask, _ClippingMaskUV, _ClippingMaskCH, _ClippingMaskCutoff, _AlphaMaskScale, _AlphaMaskValue);
            });

            PropertyGroupBox("Color Grading", (Property) =>
            {
                Property("Hue", (s) => materialEditor.RangeProperty(_BaseMapHue, s));
                Property("Saturation", (s) => materialEditor.RangeProperty(_BaseMapSaturation, s));
                Property("Contrast", (s) => materialEditor.RangeProperty(_BaseMapContrast, s));
            });

            PropertyGroupBox("Normal Map", (Property) =>
            {
                Property("Use Normal Map", (s) => materialEditor.ShaderProperty(_UseNormalMap, new GUIContent(s, "Adds small surface detail from a normal map.")));
                EditorGUI.BeginDisabledGroup(material.GetInt("_UseNormalMap") == 0);
                Property("Normal Map", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s, "Texture that adds fake bumps and detail."), _NormalMap, _NormalMapUV));
                EditorGUI.indentLevel++;
                Property("Normal Map", (s) => materialEditor.TextureScaleOffsetProperty(_NormalMap));
                EditorGUI.indentLevel--;
                Property("Bump Scale", (s) => materialEditor.ShaderProperty(_BumpScale, new GUIContent(s, "Strength of the normal map effect.")));
                EditorGUI.EndDisabledGroup();
            });

            PropertyGroupBox("Refraction", (Property) =>
            {
                Property("Refraction Strength", (s) => materialEditor.ShaderProperty(_RefractionStrength, new GUIContent(s, "Distortion amount of the background seen through the gem. Affects both center and edge response.")));
                bool enableRefraction = _RefractionStrength.hasMixedValue || Mathf.Abs(_RefractionStrength.floatValue) > 0.0001f;
                EditorGUI.BeginDisabledGroup(!enableRefraction);
                Property("Refraction Blur Weight", (s) => materialEditor.ShaderProperty(_RefractionBlurWeight, new GUIContent(s, "Softens refracted background color using a 5-tap blur. Higher values make refraction blurrier.")));
                Property("Refraction Fresnel Power", (s) => materialEditor.ShaderProperty(_RefractionFresnelPower, new GUIContent(s, "Moves refraction emphasis toward edges as the value increases.")));
                EditorGUI.EndDisabledGroup();
            });

            PropertyGroupBox("Rim Light", (Property, shouldRender) =>
            {
                Property("Use Rim", (s) => materialEditor.ShaderProperty(_UseRim, new GUIContent(s, "Turns edge rim lighting on or off.")));
                Property("Fresnel (3D)", (s) => EditorGUILayout.LabelField(s, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 }));
                bool enableRim = material.GetInt("_UseRim") != 0;
                if (shouldRender)
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginDisabledGroup(!enableRim);
                }
                Property("Mask", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s, "Mask and channel controlling where rim appears."), _RimMask, _RimMaskUV, _RimMaskCH));
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
                if (shouldRender)
                {
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }
            });

            PropertyGroupBox("MatCap", (Property) =>
            {
                Property("MatCap 1", (s) => CustomFoldout(ref s_FoldoutMatcaps[0], "MatCap 1", () =>
                {
                    materialEditor.ShaderProperty(_MatCapMode, new GUIContent("MatCap Mode 1", "How this MatCap layer blends: None, Add, or Multiply."));
                    EditorGUI.BeginDisabledGroup(_MatCapMode.hasMixedValue == false && _MatCapMode.floatValue == 0);
                    materialEditor.TexturePropertySingleLine(new GUIContent("MatCap 1", "MatCap texture and tint color."), _MatCapTex, _MatCapColor);
                    materialEditor.TexturePropertySingleLine(new GUIContent("MatCap Mask 1", "Mask texture and channel for MatCap."), _MatCapMask, _MatCapUV1, _MatCapMaskCH);
                    materialEditor.ShaderProperty(_MatCapWeight, new GUIContent("MatCap Weight 1", "Controls the weight of the MatCap."));
                    materialEditor.ShaderProperty(_MatCapLightingDimmer, new GUIContent("MatCap Lighting Dimmer 1", "Controls the lighting contribution to the MatCap if 'Add' mode. If this value is 0, the MatCap result will always be constant because it ignores lighting."));
                    EditorGUI.EndDisabledGroup();
                }));

                Property("MatCap 2", (s) => CustomFoldout(ref s_FoldoutMatcaps[1], "MatCap 2", () =>
                {
                    materialEditor.ShaderProperty(_MatCapMode2, new GUIContent("MatCap Mode 2", "How this MatCap layer blends: None, Add, or Multiply."));
                    EditorGUI.BeginDisabledGroup(_MatCapMode2.hasMixedValue == false && _MatCapMode2.floatValue == 0);
                    materialEditor.TexturePropertySingleLine(new GUIContent("MatCap 2", "MatCap texture and tint color."), _MatCapTex2, _MatCapColor2);
                    materialEditor.TexturePropertySingleLine(new GUIContent("MatCap Mask 2", "Mask texture and channel for MatCap."), _MatCapMask2, _MatCapUV2, _MatCapMaskCH2);
                    materialEditor.ShaderProperty(_MatCapWeight2, new GUIContent("MatCap Weight 2", "Controls the weight of the MatCap."));
                    materialEditor.ShaderProperty(_MatCapLightingDimmer2, new GUIContent("MatCap Lighting Dimmer 2", "Controls the lighting contribution to the MatCap if 'Add' mode. If this value is 0, the MatCap result will always be constant because it ignores lighting."));
                    EditorGUI.EndDisabledGroup();
                }));

                Property("MatCap 3", (s) => CustomFoldout(ref s_FoldoutMatcaps[2], "MatCap 3", () =>
                {
                    materialEditor.ShaderProperty(_MatCapMode3, new GUIContent("MatCap Mode 3", "How this MatCap layer blends: None, Add, or Multiply."));
                    EditorGUI.BeginDisabledGroup(_MatCapMode3.hasMixedValue == false && _MatCapMode3.floatValue == 0);
                    materialEditor.TexturePropertySingleLine(new GUIContent("MatCap 3", "MatCap texture and tint color."), _MatCapTex3, _MatCapColor3);
                    materialEditor.TexturePropertySingleLine(new GUIContent("MatCap Mask 3", "Mask texture and channel for MatCap."), _MatCapMask3, _MatCapUV3, _MatCapMaskCH3);
                    materialEditor.ShaderProperty(_MatCapWeight3, new GUIContent("MatCap Weight 3", "Controls the weight of the MatCap."));
                    materialEditor.ShaderProperty(_MatCapLightingDimmer3, new GUIContent("MatCap Lighting Dimmer 3", "Controls the lighting contribution to the MatCap if 'Add' mode. If this value is 0, the MatCap result will always be constant because it ignores lighting."));
                    EditorGUI.EndDisabledGroup();
                }));

                Property("MatCap 4", (s) => CustomFoldout(ref s_FoldoutMatcaps[3], "MatCap 4", () =>
                {
                    materialEditor.ShaderProperty(_MatCapMode4, new GUIContent("MatCap Mode 4", "How this MatCap layer blends: None, Add, or Multiply."));
                    EditorGUI.BeginDisabledGroup(_MatCapMode4.hasMixedValue == false && _MatCapMode4.floatValue == 0);
                    materialEditor.TexturePropertySingleLine(new GUIContent("MatCap 4", "MatCap texture and tint color."), _MatCapTex4, _MatCapColor4);
                    materialEditor.TexturePropertySingleLine(new GUIContent("MatCap Mask 4", "Mask texture and channel for MatCap."), _MatCapMask4, _MatCapUV4, _MatCapMaskCH4);
                    materialEditor.ShaderProperty(_MatCapWeight4, new GUIContent("MatCap Weight 4", "Controls the weight of the MatCap."));
                    materialEditor.ShaderProperty(_MatCapLightingDimmer4, new GUIContent("MatCap Lighting Dimmer 4", "Controls the lighting contribution to the MatCap if 'Add' mode. If this value is 0, the MatCap result will always be constant because it ignores lighting."));
                    EditorGUI.EndDisabledGroup();
                }));
            });

            PropertyGroupBox("Glitter", (Property, shouldRender) =>
            {
                Property("Use Glitter", (s) => materialEditor.ShaderProperty(_UseGlitter, new GUIContent(s, "Turns glitter highlights on or off.")));
                if (shouldRender)
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginDisabledGroup(material.GetInt("_UseGlitter") == 0);
                }
                Property("Color / Mask", (s) => materialEditor.TexturePropertySingleLine(new GUIContent(s, "Glitter texture tint and mask control."), _GlitterColorTex, _GlitterColor, _GlitterMapUV));
                Property("Main Color Strength", (s) => materialEditor.ShaderProperty(_GlitterMainStrength, new GUIContent(s, "How much glitter is tinted by the base color.")));
                Property("Enable Lighting", (s) => materialEditor.ShaderProperty(_GlitterEnableLighting, new GUIContent(s, "Makes glitter react to scene lighting.")));
                Property("Backface Mask", (s) => materialEditor.ShaderProperty(_GlitterBackfaceMask, new GUIContent(s, "Reduces glitter on back-facing polygons.")));
                Property("Apply Transparency", (s) => materialEditor.ShaderProperty(_GlitterApplyTransparency, new GUIContent(s, "Makes glitter follow material transparency.")));
                Property("Shadow Mask", (s) => materialEditor.ShaderProperty(_GlitterShadowMask, new GUIContent(s, "Reduces glitter where shadows are stronger.")));
                Property("Particle Size", (s) => materialEditor.FloatProperty(_GlitterParticleSize, s));
                Property("Scale Randomize", (s) => materialEditor.ShaderProperty(_GlitterScaleRandomize, new GUIContent(s, "Adds random size variation to glitter particles.")));
                Property("Contrast", (s) => materialEditor.FloatProperty(_GlitterContrast, s));
                Property("Sensitivity", (s) => materialEditor.FloatProperty(_GlitterSensitivity, s));
                Property("Blink Speed", (s) => materialEditor.FloatProperty(_GlitterBlinkSpeed, s));
                Property("Angle Limit", (s) => materialEditor.FloatProperty(_GlitterAngleLimit, s));
                Property("Light Direction Strength", (s) => materialEditor.FloatProperty(_GlitterLightDirection, s));
                Property("Color Randomness", (s) => materialEditor.ShaderProperty(_GlitterColorRandomness, new GUIContent(s, "Adds random color variation per glitter particle.")));
                Property("Normal Strength", (s) => materialEditor.ShaderProperty(_GlitterNormalStrength, new GUIContent(s, "How much normal details affect glitter direction.")));
                Property("Post Contrast", (s) => materialEditor.FloatProperty(_GlitterPostContrast, s));
                if (shouldRender)
                {
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }
            });

            foreach (var mat in materials)
            {
                if (mat == null) continue;
                mat.SetKeyword(new LocalKeyword(mat.shader, "_USE_GLITTER"), mat.GetInt("_UseGlitter") > 0);
            }

            var originalAdvancedSettingsUnlocked = PotaToonGUIUtility.advancedSettingsUnlocked;
            if (IsAdvancedSettingsMatched())
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.LabelField("Advanced Settings", advancedSettingsStyle);
                PotaToonGUIUtility.DrawAdvancedSettingsButton();
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            }

            EditorGUI.BeginDisabledGroup(!PotaToonGUIUtility.advancedSettingsUnlocked);

            PropertyGroupBox("Clearcoat", (Property, shouldRender) =>
            {
                Property("Clearcoat Intensity", (s) => materialEditor.ShaderProperty(_ClearcoatIntensity, new GUIContent(s, "Adds a glossy top coat highlight.")));
                bool enableClearcoat = _ClearcoatIntensity.hasMixedValue || _ClearcoatIntensity.floatValue > 0.0f;
                if (shouldRender) EditorGUI.BeginDisabledGroup(!enableClearcoat);
                Property("Clearcoat Roughness", (s) => materialEditor.ShaderProperty(_ClearcoatRoughness, new GUIContent(s, "Roughness of the top coat highlight. Lower = sharper, higher = softer.")));
                if (shouldRender) EditorGUI.EndDisabledGroup();
            });

            PropertyGroupBox("Character Shadow", (Property, shouldRender) =>
            {
                Property("Disable Char Shadow", (s) => materialEditor.ShaderProperty(_DisableCharShadow, new GUIContent(s, "Toggles character self-shadowing. In some cases (e.g., bangs), disabling self-shadow can create a cleaner look.")));
                if (shouldRender) EditorGUI.BeginDisabledGroup(material.GetInt("_DisableCharShadow") == 1);
                Property("Smoothness", (s) => materialEditor.ShaderProperty(_CharShadowSmoothnessOffset, new GUIContent(s, "Controls how soft the character shadow edge appears.")));
                if (shouldRender) EditorGUI.EndDisabledGroup();
            });
            
            PropertyGroupBox("OIT", (Property) =>
            {
                Property("Disable OIT", (s) => materialEditor.ShaderProperty(_DisableOIT, new GUIContent(s, "Ignore OIT for this material. Transparent/Refraction will render without OIT contribution.")));
            });

            PropertyGroupBox("Stencil / ZTest", (Property) =>
            {
                Property("Stencil", (s) => EditorGUILayout.LabelField(s, sectionHeaderStyle));
                Property("Comp", (s) => materialEditor.ShaderProperty(_StencilComp, new GUIContent(s, "Stencil comparison rule (advanced).")));
                Property("Ref", (s) => materialEditor.ShaderProperty(_StencilRef, new GUIContent(s, "Stencil reference value used for comparison.")));
                Property("Pass", (s) => materialEditor.ShaderProperty(_StencilPass, new GUIContent(s, "Action when stencil and depth tests pass.")));
                Property("Fail", (s) => materialEditor.ShaderProperty(_StencilFail, new GUIContent(s, "Action when stencil test fails.")));
                Property("ZFail", (s) => materialEditor.ShaderProperty(_StencilZFail, new GUIContent(s, "Action when stencil passes but depth test fails.")));
                Property("$_StencilZTest_HSlider", (s) => EditorGUILayout.LabelField("", GUI.skin.horizontalSlider));
                Property("ZTest", (s) => EditorGUILayout.LabelField(s, sectionHeaderStyle));
                Property("$_StencilZTest_Space", (s) => EditorGUILayout.Space(5));
                Property("ZTest Value", (s) => materialEditor.ShaderProperty(_ZTest, new GUIContent("ZTest", "Depth comparison rule for visible color passes only.")));
                Property("ZWrite", (s) => materialEditor.ShaderProperty(_ZWriteMode, new GUIContent(s, "Depth write toggle for the main visible color pass.")));
            });

            PropertyGroupBox("Pass Control", (Property, shouldRender) =>
            {
                if (shouldRender)
                    DrawInfoBox("You can Enable/Disable individual shader passes for this material.");

                Property("Transparent Shadow", (s) =>
                {
                    if (!TryGetTransparentShadowPassEnabled(materials, out bool enabled, out bool hasMixed))
                        return;

                    EditorGUI.showMixedValue = hasMixed;
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.BeginHorizontal();
                    bool newEnabled = EditorGUILayout.Toggle(s, enabled);
                    if (GUILayout.Button(new GUIContent("?", "Show description"), helpButtonStyle))
                        s_ShowPassHelps[0] = !s_ShowPassHelps[0];
                    EditorGUILayout.EndHorizontal();
                    if (EditorGUI.EndChangeCheck())
                        SetTransparentShadowPassEnabled(materials, newEnabled, "Toggle Transparent Shadow Pass");
                    EditorGUI.showMixedValue = false;
                    if (s_ShowPassHelps[0])
                        DrawInfoBox("Casts transparent shadow from this material.");
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
                        s_ShowPassHelps[1] = !s_ShowPassHelps[1];
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
                    if (s_ShowPassHelps[1])
                        DrawInfoBox("Marks character area. If disabled, this material is excluded from character area and will not receive character shadow or character-only post processing.");
                });
            });

            SyncOITDepthPass(materials);
            EditorGUI.EndDisabledGroup();

            if (PotaToonGUIUtility.advancedSettingsUnlocked != originalAdvancedSettingsUnlocked)
                PotaToonGUIUtility.SaveAdvancedSettingUnlocked();
        }

        private static float ComputeGemIOR(float shine)
        {
            return Mathf.Lerp(1.02f, 2.42f, Mathf.Clamp01(shine));
        }

        private static float ComputeShineFromIOR(float ior)
        {
            return Mathf.InverseLerp(1.02f, 2.42f, ior);
        }

        private struct GemPreset
        {
            public float Ior;
            public float Roughness;
            public float ClearcoatIntensity;
            public float ClearcoatRoughness;
            public float ParticleIntensity;
            public float ParticleLoop;
            public float TransmissionStrength;
            public Color AbsorptionColor;
            public float Thickness;
            public float BaseStrength;
            public float ChromaticAberration;
            public float RefractionStrength;
            public float RefractionBlurWeight;
            public float RefractionFresnelPower;
        }

        private static bool TryGetGemPreset(GemType gemType, out GemPreset preset)
        {
            switch (gemType)
            {
                case GemType.Glass:
                    preset = new GemPreset
                    {
                        Ior = 1.47f,
                        Roughness = 0.18f,
                        ClearcoatIntensity = 0.22f,
                        ClearcoatRoughness = 0.16f,
                        ParticleIntensity = 0.02f,
                        ParticleLoop = 2.0f,
                        TransmissionStrength = 0.68f,
                        AbsorptionColor = new Color(0.015f, 0.015f, 0.015f, 1f),
                        Thickness = 0.70f,
                        BaseStrength = 0.03f,
                        ChromaticAberration = 0.03f,
                        RefractionStrength = 0.14f,
                        RefractionBlurWeight = 0.14f,
                        RefractionFresnelPower = 1.05f
                    };
                    return true;
                case GemType.Crystal:
                    preset = new GemPreset
                    {
                        Ior = 1.70f,
                        Roughness = 0.12f,
                        ClearcoatIntensity = 0.40f,
                        ClearcoatRoughness = 0.08f,
                        ParticleIntensity = 0.34f,
                        ParticleLoop = 9.0f,
                        TransmissionStrength = 0.60f,
                        AbsorptionColor = new Color(0.030f, 0.030f, 0.040f, 1f),
                        Thickness = 0.90f,
                        BaseStrength = 0.05f,
                        ChromaticAberration = 0.14f,
                        RefractionStrength = 0.19f,
                        RefractionBlurWeight = 0.16f,
                        RefractionFresnelPower = 1.30f
                    };
                    return true;
                case GemType.Diamond:
                    preset = new GemPreset
                    {
                        Ior = 2.42f,
                        Roughness = 0.03f,
                        ClearcoatIntensity = 0.95f,
                        ClearcoatRoughness = 0.015f,
                        ParticleIntensity = 0.72f,
                        ParticleLoop = 9.0f,
                        TransmissionStrength = 0.38f,
                        AbsorptionColor = new Color(0.0f, 0.0f, 0.0f, 1f),
                        Thickness = 0.70f,
                        BaseStrength = 0.00f,
                        ChromaticAberration = 0.34f,
                        RefractionStrength = 0.25f,
                        RefractionBlurWeight = 0.08f,
                        RefractionFresnelPower = 2.40f
                    };
                    return true;
                case GemType.Ruby:
                    preset = new GemPreset
                    {
                        Ior = 1.77f,
                        Roughness = 0.10f,
                        ClearcoatIntensity = 0.48f,
                        ClearcoatRoughness = 0.06f,
                        ParticleIntensity = 0.38f,
                        ParticleLoop = 8.0f,
                        TransmissionStrength = 0.50f,
                        AbsorptionColor = new Color(0.14f, 0.92f, 0.92f, 1f),
                        Thickness = 1.70f,
                        BaseStrength = 0.08f,
                        ChromaticAberration = 0.12f,
                        RefractionStrength = 0.17f,
                        RefractionBlurWeight = 0.18f,
                        RefractionFresnelPower = 1.60f
                    };
                    return true;
                case GemType.Emerald:
                    preset = new GemPreset
                    {
                        Ior = 1.58f,
                        Roughness = 0.14f,
                        ClearcoatIntensity = 0.42f,
                        ClearcoatRoughness = 0.08f,
                        ParticleIntensity = 0.30f,
                        ParticleLoop = 8.0f,
                        TransmissionStrength = 0.58f,
                        AbsorptionColor = new Color(0.92f, 0.16f, 0.92f, 1f),
                        Thickness = 1.80f,
                        BaseStrength = 0.10f,
                        ChromaticAberration = 0.11f,
                        RefractionStrength = 0.16f,
                        RefractionBlurWeight = 0.17f,
                        RefractionFresnelPower = 1.45f
                    };
                    return true;
                case GemType.Sapphire:
                    preset = new GemPreset
                    {
                        Ior = 1.77f,
                        Roughness = 0.10f,
                        ClearcoatIntensity = 0.50f,
                        ClearcoatRoughness = 0.06f,
                        ParticleIntensity = 0.44f,
                        ParticleLoop = 10.0f,
                        TransmissionStrength = 0.52f,
                        AbsorptionColor = new Color(0.92f, 0.92f, 0.14f, 1f),
                        Thickness = 1.65f,
                        BaseStrength = 0.07f,
                        ChromaticAberration = 0.10f,
                        RefractionStrength = 0.18f,
                        RefractionBlurWeight = 0.15f,
                        RefractionFresnelPower = 1.65f
                    };
                    return true;
                case GemType.Opal:
                    preset = new GemPreset
                    {
                        Ior = 1.45f,
                        Roughness = 0.28f,
                        ClearcoatIntensity = 0.22f,
                        ClearcoatRoughness = 0.24f,
                        ParticleIntensity = 0.18f,
                        ParticleLoop = 5.0f,
                        TransmissionStrength = 0.35f,
                        AbsorptionColor = new Color(0.08f, 0.08f, 0.09f, 1f),
                        Thickness = 2.00f,
                        BaseStrength = 0.72f,
                        ChromaticAberration = 0.05f,
                        RefractionStrength = 0.10f,
                        RefractionBlurWeight = 0.22f,
                        RefractionFresnelPower = 1.10f
                    };
                    return true;
                default:
                    preset = default;
                    return false;
            }
        }

    }
}
