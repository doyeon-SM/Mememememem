using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace PotaToon.Editor
{
#if UNITY_2021_3
    [VolumeComponentEditor(typeof(PotaToon))]
#else
    [CustomEditor(typeof(PotaToon))]
#endif
    public class PotaToonEditor : VolumeComponentEditor
    {
        SerializedDataParameter m_Mode;
        SerializedDataParameter m_TransparentShadow;
        SerializedDataParameter m_CharShadowDirOffsetMode;
        SerializedDataParameter m_CharShadowDirOffset;
        SerializedDataParameter m_FollowLayerMask;
        SerializedDataParameter m_MaxToonBrightness;
        SerializedDataParameter m_Bias;
        SerializedDataParameter m_NormalBias;
        SerializedDataParameter m_Quality;
        SerializedDataParameter m_TextureScale;
        SerializedDataParameter m_TransparentTextureScale;
        SerializedDataParameter m_ShadowCullingDistance;
        SerializedDataParameter m_OIT;
        SerializedDataParameter m_OITMode;
        SerializedDataParameter m_CharPostProcess;
        SerializedDataParameter m_CharScreenOutline;
        SerializedDataParameter m_CharScreenOutlineExcludeInnerLines;
        SerializedDataParameter m_CharScreenOutlineColor;
        SerializedDataParameter m_CharScreenOutlineThickness;
        SerializedDataParameter m_CharScreenOutlineEdgeStrength;
        SerializedDataParameter m_CharPostExposure;
        SerializedDataParameter m_CharGammaAdjust;
        SerializedDataParameter m_CharToneMapping;
        SerializedDataParameter m_CharContrast;
        SerializedDataParameter m_CharColorFilter;
        SerializedDataParameter m_CharHueShift;
        SerializedDataParameter m_CharSaturation;
        SerializedDataParameter m_CharWhiteBalanceTemperature;
        SerializedDataParameter m_CharWhiteBalanceTint;
        SerializedDataParameter m_ScreenRimWidth;
        SerializedDataParameter m_ScreenRimColor;
        SerializedDataParameter m_EnvPostProcess;
        SerializedDataParameter m_EnvToneMapping;
        SerializedDataParameter m_EnvPostExposure;
        
        SerializedDataParameter m_CharToeStrength;
        SerializedDataParameter m_CharToeLength;
        SerializedDataParameter m_CharShoulderStrength;
        SerializedDataParameter m_CharShoulderLength;
        SerializedDataParameter m_CharShoulderAngle;
        SerializedDataParameter m_CharGamma;
        SerializedDataParameter m_EnvToeStrength;
        SerializedDataParameter m_EnvToeLength;
        SerializedDataParameter m_EnvShoulderStrength;
        SerializedDataParameter m_EnvShoulderLength;
        SerializedDataParameter m_EnvShoulderAngle;
        SerializedDataParameter m_EnvGamma;
        SerializedDataParameter m_EnvContrast;
        SerializedDataParameter m_EnvColorFilter;
        SerializedDataParameter m_EnvHueShift;
        SerializedDataParameter m_EnvSaturation;
        SerializedDataParameter m_EnvWhiteBalanceTemperature;
        SerializedDataParameter m_EnvWhiteBalanceTint;
        
        SerializedDataParameter m_CharBloom;
        SerializedDataParameter m_CharBloomThreshold;
        SerializedDataParameter m_CharBloomIntensity;
        SerializedDataParameter m_CharBloomScatter;
        SerializedDataParameter m_CharBloomClamp;
        SerializedDataParameter m_CharBloomTint;
        SerializedDataParameter m_CharBloomHQ;
        SerializedDataParameter m_CharBloomDownscale;
        SerializedDataParameter m_CharBloomMaxIterations;
        SerializedDataParameter m_CharBloomDirtTexture;
        SerializedDataParameter m_CharBloomDirtIntensity;
        
        SerializedDataParameter m_EnvBloom;
        SerializedDataParameter m_EnvBloomThreshold;
        SerializedDataParameter m_EnvBloomIntensity;
        SerializedDataParameter m_EnvBloomScatter;
        SerializedDataParameter m_EnvBloomClamp;
        SerializedDataParameter m_EnvBloomTint;
        SerializedDataParameter m_EnvBloomHQ;
        SerializedDataParameter m_EnvBloomDownscale;
        SerializedDataParameter m_EnvBloomMaxIterations;
        SerializedDataParameter m_EnvBloomDirtTexture;
        SerializedDataParameter m_EnvBloomDirtIntensity;
        
        private static bool s_ShadowSettingsFoldout;
        private static bool s_CharScreenOutlineFoldout;
        private static bool s_CharColorGradingFoldout;
        private static bool s_CharScreenRimFoldout;
        private static bool s_CharBloomFoldout;
        private static bool s_EnvColorGradingFoldout;
        private static bool s_EnvBloomFoldout;
        
        // Curve drawing utilities
        private readonly HableCurve m_HableCurve = new HableCurve();
        private Rect m_CurveRect;
        private Material m_CurveMaterial;
        private RenderTexture m_CurveTex;
        
        // Preset
        private static VolumeComponent s_CopyBuffer;
        private Vector2 m_ScrollPos = Vector2.zero;
        private int m_SelectedIndex = -1;
        private List<PotaToonVolumePreset> m_Presets;
        private Color m_SelectedPresetColor = new Color(0.4f, 0.8f, 0.3f) ;
        private Texture2D m_PresetIcon;

        public override void OnEnable()
        {
            PotaToonGUIUtility.LoadAdvancedSettingUnlocked();

            InitializePresetsAndIcons();
            
            var o = new PropertyFetcher<PotaToon>(serializedObject);
            m_Mode = Unpack(o.Find(x => x.mode));
            m_TransparentShadow = Unpack(o.Find(x => x.transparentShadow));
            m_CharShadowDirOffsetMode = Unpack(o.Find(x => x.charShadowDirOffsetMode));
            m_CharShadowDirOffset = Unpack(o.Find(x => x.charShadowDirOffset));
            m_FollowLayerMask = Unpack(o.Find(x => x.followLayerMask));
            m_MaxToonBrightness = Unpack(o.Find(x => x.maxToonBrightness));
            m_Bias = Unpack(o.Find(x => x.bias));
            m_NormalBias = Unpack(o.Find(x => x.normalBias));
            m_Quality = Unpack(o.Find(x => x.quality));
            m_TextureScale = Unpack(o.Find(x => x.textureScale));
            m_TransparentTextureScale = Unpack(o.Find(x => x.transparentTextureScale));
            m_ShadowCullingDistance = Unpack(o.Find(x => x.shadowCullingDistance));
            m_OIT = Unpack(o.Find(x => x.oit));
            m_OITMode = Unpack(o.Find(x => x.oitMode));
            m_CharPostProcess = Unpack(o.Find(x => x.charPostProcessing));
            m_CharScreenOutline = Unpack(o.Find(x => x.charScreenOutline));
            m_CharScreenOutlineExcludeInnerLines = Unpack(o.Find(x => x.charScreenOutlineExcludeInnerLines));
            m_CharScreenOutlineColor = Unpack(o.Find(x => x.charScreenOutlineColor));
            m_CharScreenOutlineThickness = Unpack(o.Find(x => x.charScreenOutlineThickness));
            m_CharScreenOutlineEdgeStrength = Unpack(o.Find(x => x.charScreenOutlineEdgeStrength));
            m_CharPostExposure = Unpack(o.Find(x => x.charPostExposure));
            m_CharGammaAdjust = Unpack(o.Find(x => x.charGammaAdjust));
            m_CharToneMapping = Unpack(o.Find(x => x.charToneMapping));
            m_CharContrast = Unpack(o.Find(x => x.charContrast));
            m_CharColorFilter = Unpack(o.Find(x => x.charColorFilter));
            m_CharHueShift = Unpack(o.Find(x => x.charHueShift));
            m_CharSaturation = Unpack(o.Find(x => x.charSaturation));
            m_CharWhiteBalanceTemperature = Unpack(o.Find(x => x.whiteBalanceTemperature));
            m_CharWhiteBalanceTint = Unpack(o.Find(x => x.whiteBalanceTint));
            m_ScreenRimWidth = Unpack(o.Find(x => x.screenRimWidth));
            m_ScreenRimColor = Unpack(o.Find(x => x.screenRimColor));
            
            m_EnvPostProcess = Unpack(o.Find(x => x.envPostProcessing));
            m_EnvToneMapping = Unpack(o.Find(x => x.envToneMapping));
            m_EnvPostExposure = Unpack(o.Find(x => x.envPostExposure));
            m_EnvContrast = Unpack(o.Find(x => x.envContrast));
            m_EnvColorFilter = Unpack(o.Find(x => x.envColorFilter));
            m_EnvHueShift = Unpack(o.Find(x => x.envHueShift));
            m_EnvSaturation = Unpack(o.Find(x => x.envSaturation));
            m_EnvWhiteBalanceTemperature = Unpack(o.Find(x => x.envWhiteBalanceTemperature));
            m_EnvWhiteBalanceTint = Unpack(o.Find(x => x.envWhiteBalanceTint));
            
            m_CharToeStrength = Unpack(o.Find(x => x.charToeStrength));
            m_CharToeLength = Unpack(o.Find(x => x.charToeLength));
            m_CharShoulderStrength = Unpack(o.Find(x => x.charShoulderStrength));
            m_CharShoulderLength = Unpack(o.Find(x => x.charShoulderLength));
            m_CharShoulderAngle = Unpack(o.Find(x => x.charShoulderAngle));
            m_CharGamma = Unpack(o.Find(x => x.charGamma));
            m_EnvToeStrength = Unpack(o.Find(x => x.envToeStrength));
            m_EnvToeLength = Unpack(o.Find(x => x.envToeLength));
            m_EnvShoulderStrength = Unpack(o.Find(x => x.envShoulderStrength));
            m_EnvShoulderLength = Unpack(o.Find(x => x.envShoulderLength));
            m_EnvShoulderAngle = Unpack(o.Find(x => x.envShoulderAngle));
            m_EnvGamma = Unpack(o.Find(x => x.envGamma));
            
            m_CharBloom = Unpack(o.Find(x => x.charBloom));
            m_CharBloomThreshold = Unpack(o.Find(x => x.threshold));
            m_CharBloomIntensity = Unpack(o.Find(x => x.intensity));
            m_CharBloomScatter = Unpack(o.Find(x => x.scatter));
            m_CharBloomClamp = Unpack(o.Find(x => x.clamp));
            m_CharBloomTint = Unpack(o.Find(x => x.tint));
            m_CharBloomHQ = Unpack(o.Find(x => x.highQualityFiltering));
            m_CharBloomDownscale = Unpack(o.Find(x => x.downscale));
            m_CharBloomMaxIterations = Unpack(o.Find(x => x.maxIterations));
            m_CharBloomDirtTexture = Unpack(o.Find(x => x.dirtTexture));
            m_CharBloomDirtIntensity = Unpack(o.Find(x => x.dirtIntensity));
            
            m_EnvBloom = Unpack(o.Find(x => x.envBloom));
            m_EnvBloomThreshold = Unpack(o.Find(x => x.envBloomThreshold));
            m_EnvBloomIntensity = Unpack(o.Find(x => x.envBloomIntensity));
            m_EnvBloomScatter = Unpack(o.Find(x => x.envBloomScatter));
            m_EnvBloomClamp = Unpack(o.Find(x => x.envBloomClamp));
            m_EnvBloomTint = Unpack(o.Find(x => x.envBloomTint));
            m_EnvBloomHQ = Unpack(o.Find(x => x.envBloomHighQualityFiltering));
            m_EnvBloomDownscale = Unpack(o.Find(x => x.envBloomDownscale));
            m_EnvBloomMaxIterations = Unpack(o.Find(x => x.envBloomMaxIterations));
            m_EnvBloomDirtTexture = Unpack(o.Find(x => x.envBloomDirtTexture));
            m_EnvBloomDirtIntensity = Unpack(o.Find(x => x.envBloomDirtIntensity));
            
            m_CurveMaterial = new Material(Shader.Find("Hidden/PotaToon/Editor/Custom Tonemapper Curve"));
        }
        
        public override void OnDisable()
        {
            CoreUtils.Destroy(m_CurveMaterial);
            m_CurveMaterial = null;

            CoreUtils.Destroy(m_CurveTex);
            m_CurveTex = null;
        }

        public override void OnInspectorGUI()
        {
            var originalColor = GUI.backgroundColor;
            var subGroupBoxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 5, 5), margin = new RectOffset(5, 5, 5, 5) };
            
            // Copy/Paste Buttons
            const float iconSize = 32f;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(EditorGUIUtility.IconContent("Clipboard", "|Copy settings"), GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
                CopyComponent();
            
            if (GUILayout.Button(EditorGUIUtility.IconContent("d_SaveAs", "|Paste settings"), GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
                PasteComponent();

            bool showPreset = PotaToonGUIUtility.volumePresetShown;
            GUI.backgroundColor = showPreset ? new Color(0.8f, 0.8f, 1f)  : originalColor;
            var presetIconContent = m_PresetIcon != null ? new GUIContent(m_PresetIcon, "Preset") : EditorGUIUtility.IconContent("d_Preset.Context", "|Preset");
            if (GUILayout.Button(presetIconContent, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                PotaToonGUIUtility.SetVolumePresetShown(!showPreset);
            }
            GUI.backgroundColor = originalColor;
            EditorGUILayout.EndHorizontal();
            
            DrawPresetField();
            
            EditorGUILayout.LabelField("MAIN SETTINGS", EditorStyles.boldLabel);
            // Mode
            PropertyField(m_Mode);
            bool isConcertMode = m_Mode.value.enumValueIndex == (int)PotaToonMode.Concert;
            
            // Mode Settings
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(subGroupBoxStyle);
                if (!isConcertMode)
                {
                    EditorGUILayout.LabelField("Shadow Direction Control");
                    PropertyField(m_CharShadowDirOffsetMode, new GUIContent("Mode"));
                    var text = m_CharShadowDirOffsetMode.value.enumValueIndex == 0 ? "Offset" : "Fixed Direction";
                    PropertyField(m_CharShadowDirOffset, new GUIContent(text));
                }
                else
                {
                    PropertyField(m_FollowLayerMask);
                }
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            
            // Shadow
            PropertyField(m_TransparentShadow);
            EditorGUILayout.Space();
            
            // Quality
            PropertyField(m_Quality);
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginDisabledGroup(m_Quality.value.enumValueIndex != (int)PotaToonQuality.Custom);
                switch (m_Quality.value.enumValueIndex)
                {
                    case (int)PotaToonQuality.Low:
                        m_TextureScale.value.enumValueIndex = 0;                // X2
                        m_TransparentTextureScale.value.enumValueIndex = 0;     // X2
                        m_ShadowCullingDistance.value.floatValue = 1.0f;
                        break;
                    case (int)PotaToonQuality.Medium:
                        m_TextureScale.value.enumValueIndex = 1;                // X4
                        m_TransparentTextureScale.value.enumValueIndex = 1;     // X4
                        m_ShadowCullingDistance.value.floatValue = 1.2f;
                        break;
                    case (int)PotaToonQuality.High:
                        m_TextureScale.value.enumValueIndex = 2;                // X8
                        m_TransparentTextureScale.value.enumValueIndex = 2;     // X8
                        m_ShadowCullingDistance.value.floatValue = 1.5f;
                        break;
                    case (int)PotaToonQuality.Cinematic:
                        m_TextureScale.value.enumValueIndex = 3;                // X16
                        m_TransparentTextureScale.value.enumValueIndex = 3;     // X16
                        m_ShadowCullingDistance.value.floatValue = 2.0f;
                        break;
                }
                if (m_Quality.value.enumValueIndex == (int)PotaToonQuality.Custom)
                {
                    PropertyField(m_TextureScale);
                    PropertyField(m_TransparentTextureScale);
                    PropertyField(m_ShadowCullingDistance, new GUIContent("Culling Distance"));
                }
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
            
            // Max Brightness
            PropertyField(m_MaxToonBrightness, new GUIContent("Max Material Brightness"));
            
            // Post Processing
            var postProcessLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 };
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField("POST PROCESSING", EditorStyles.boldLabel);
            PropertyField(m_CharPostProcess);
            if (m_CharPostProcess.value.boolValue)
            {
                EditorGUI.indentLevel++;
                
                s_CharScreenOutlineFoldout = EditorGUILayout.Foldout(s_CharScreenOutlineFoldout, "[Screen Outline]", true, EditorStyles.foldoutHeader);
                if (s_CharScreenOutlineFoldout)
                {
                    EditorGUILayout.BeginVertical(subGroupBoxStyle);
                    PropertyField(m_CharScreenOutline, new GUIContent("Screen Outline"));
                    if (m_CharScreenOutline.value.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        PropertyField(m_CharScreenOutlineExcludeInnerLines, new GUIContent("Exclude Inner Outline"));
                        PropertyField(m_CharScreenOutlineColor, new GUIContent("Color"));
                        PropertyField(m_CharScreenOutlineThickness, new GUIContent("Thickness"));
                        PropertyField(m_CharScreenOutlineEdgeStrength, new GUIContent("Edge Strength"));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
                
                s_CharColorGradingFoldout = EditorGUILayout.Foldout(s_CharColorGradingFoldout, "[Color Grading]", true, EditorStyles.foldoutHeader);
                if (s_CharColorGradingFoldout)
                {
                    EditorGUILayout.BeginVertical(subGroupBoxStyle);
                    EditorGUILayout.LabelField("Tone Mapping", postProcessLabelStyle);
                    PropertyField(m_CharToneMapping, new GUIContent("Mode"));

                    if (m_CharToneMapping.value.intValue == (int)PotaToonToneMapping.Custom)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.Space();

                        // Reserve GUI space
                        m_CurveRect = GUILayoutUtility.GetRect(128, 80);
                        m_CurveRect.xMin += EditorGUI.indentLevel * 15f;

                        if (Event.current.type == EventType.Repaint)
                        {
                            // Prepare curve data
                            float toeStrength = m_CharToeStrength.value.floatValue;
                            float toeLength = m_CharToeLength.value.floatValue;
                            float shoulderStrength = m_CharShoulderStrength.value.floatValue;
                            float shoulderLength = m_CharShoulderLength.value.floatValue;
                            float shoulderAngle = m_CharShoulderAngle.value.floatValue;
                            float gamma = m_CharGamma.value.floatValue;

                            m_HableCurve.Init(
                                toeStrength,
                                toeLength,
                                shoulderStrength,
                                shoulderLength,
                                shoulderAngle,
                                gamma
                            );

                            float alpha = GUI.enabled ? 1f : 0.5f;

                            m_CurveMaterial.SetVector(ShaderIDs._CustomToneCurve, m_HableCurve.uniforms.curve);
                            m_CurveMaterial.SetVector(ShaderIDs._ToeSegmentA, m_HableCurve.uniforms.toeSegmentA);
                            m_CurveMaterial.SetVector(ShaderIDs._ToeSegmentB, m_HableCurve.uniforms.toeSegmentB);
                            m_CurveMaterial.SetVector(ShaderIDs._MidSegmentA, m_HableCurve.uniforms.midSegmentA);
                            m_CurveMaterial.SetVector(ShaderIDs._MidSegmentB, m_HableCurve.uniforms.midSegmentB);
                            m_CurveMaterial.SetVector(ShaderIDs._ShoSegmentA, m_HableCurve.uniforms.shoSegmentA);
                            m_CurveMaterial.SetVector(ShaderIDs._ShoSegmentB, m_HableCurve.uniforms.shoSegmentB);
                            m_CurveMaterial.SetVector("_Variants", new Vector4(alpha, m_HableCurve.whitePoint, 0f, 0f));

                            CheckCurveRT((int)m_CurveRect.width, (int)m_CurveRect.height);
                            
                            var oldRt = RenderTexture.active;
                            Graphics.Blit(null, m_CurveTex, m_CurveMaterial, EditorGUIUtility.isProSkin ? 0 : 1);
                            RenderTexture.active = oldRt;
                            
                            GUI.DrawTexture(m_CurveRect, m_CurveTex);
                            
                            Handles.DrawSolidRectangleWithOutline(m_CurveRect, Color.clear, Color.white * 0.4f);
                        }
                        
                        PropertyField(m_CharToeStrength, new GUIContent("Toe Strength"));
                        PropertyField(m_CharToeLength, new GUIContent("Toe Length"));
                        PropertyField(m_CharShoulderStrength, new GUIContent("Shoulder Strength"));
                        PropertyField(m_CharShoulderLength, new GUIContent("Shoulder Length"));
                        PropertyField(m_CharShoulderAngle, new GUIContent("Shoulder Angle"));
                        PropertyField(m_CharGamma, new GUIContent("Gamma"));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                    
                    EditorGUILayout.Space(1f);
                    EditorGUILayout.BeginVertical(subGroupBoxStyle);
                    EditorGUILayout.LabelField("Color Adjustments", postProcessLabelStyle);
                    PropertyField(m_CharPostExposure, new GUIContent("Post Brightness"));
                    PropertyField(m_CharGammaAdjust, new GUIContent("Gamma Adjust"));
                    PropertyField(m_CharContrast, new GUIContent("Contrast"));
                    PropertyField(m_CharColorFilter, new GUIContent("Color Filter"));
                    PropertyField(m_CharHueShift, new GUIContent("Hue Shift"));
                    PropertyField(m_CharSaturation, new GUIContent("Saturation"));
                    EditorGUILayout.EndVertical();
                    
                    EditorGUILayout.Space(1f);
                    EditorGUILayout.BeginVertical(subGroupBoxStyle);
                    EditorGUILayout.LabelField("White Balance", postProcessLabelStyle);
                    PropertyField(m_CharWhiteBalanceTemperature, new GUIContent("Temperature"));
                    PropertyField(m_CharWhiteBalanceTint, new GUIContent("Tint"));
                    EditorGUILayout.EndVertical();
                }
                
                s_CharScreenRimFoldout = EditorGUILayout.Foldout(s_CharScreenRimFoldout, "[Screen Rim]", true, EditorStyles.foldoutHeader);
                if (s_CharScreenRimFoldout)
                {
                    EditorGUILayout.BeginVertical(subGroupBoxStyle);
                    PropertyField(m_ScreenRimWidth);
                    PropertyField(m_ScreenRimColor);
                    EditorGUILayout.EndVertical();
                }
                
                s_CharBloomFoldout = EditorGUILayout.Foldout(s_CharBloomFoldout, "[Bloom]", true, EditorStyles.foldoutHeader);
                if (s_CharBloomFoldout)
                {
                    EditorGUILayout.BeginVertical(subGroupBoxStyle);
                    PropertyField(m_CharBloom);
                    if (m_CharBloom.value.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        PropertyField(m_CharBloomThreshold);
                        PropertyField(m_CharBloomIntensity);
                        PropertyField(m_CharBloomScatter);
                        PropertyField(m_CharBloomClamp);
                        PropertyField(m_CharBloomTint);
                        PropertyField(m_CharBloomHQ);
                        PropertyField(m_CharBloomDownscale);
                        PropertyField(m_CharBloomMaxIterations);
                        PropertyField(m_CharBloomDirtTexture);
                        PropertyField(m_CharBloomDirtIntensity);
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
                
                EditorGUI.indentLevel--;
            }
            
            PropertyField(m_EnvPostProcess);
            if (m_EnvPostProcess.value.boolValue)
            {
                EditorGUI.indentLevel++;
                
                s_EnvColorGradingFoldout = EditorGUILayout.Foldout(s_EnvColorGradingFoldout, "[Color Grading]", true, EditorStyles.foldoutHeader);
                if (s_EnvColorGradingFoldout)
                {
                    EditorGUILayout.BeginVertical(subGroupBoxStyle);
                    EditorGUILayout.LabelField("Tone Mapping", postProcessLabelStyle);
                    PropertyField(m_EnvToneMapping, new GUIContent("Mode"));
                    
                    if (m_EnvToneMapping.value.intValue == (int)PotaToonToneMapping.Custom)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.Space();

                        // Reserve GUI space
                        m_CurveRect = GUILayoutUtility.GetRect(128, 80);
                        m_CurveRect.xMin += EditorGUI.indentLevel * 15f;

                        if (Event.current.type == EventType.Repaint)
                        {
                            // Prepare curve data
                            float toeStrength = m_EnvToeStrength.value.floatValue;
                            float toeLength = m_EnvToeLength.value.floatValue;
                            float shoulderStrength = m_EnvShoulderStrength.value.floatValue;
                            float shoulderLength = m_EnvShoulderLength.value.floatValue;
                            float shoulderAngle = m_EnvShoulderAngle.value.floatValue;
                            float gamma = m_EnvGamma.value.floatValue;

                            m_HableCurve.Init(
                                toeStrength,
                                toeLength,
                                shoulderStrength,
                                shoulderLength,
                                shoulderAngle,
                                gamma
                            );

                            float alpha = GUI.enabled ? 1f : 0.5f;

                            m_CurveMaterial.SetVector(ShaderIDs._CustomToneCurve, m_HableCurve.uniforms.curve);
                            m_CurveMaterial.SetVector(ShaderIDs._ToeSegmentA, m_HableCurve.uniforms.toeSegmentA);
                            m_CurveMaterial.SetVector(ShaderIDs._ToeSegmentB, m_HableCurve.uniforms.toeSegmentB);
                            m_CurveMaterial.SetVector(ShaderIDs._MidSegmentA, m_HableCurve.uniforms.midSegmentA);
                            m_CurveMaterial.SetVector(ShaderIDs._MidSegmentB, m_HableCurve.uniforms.midSegmentB);
                            m_CurveMaterial.SetVector(ShaderIDs._ShoSegmentA, m_HableCurve.uniforms.shoSegmentA);
                            m_CurveMaterial.SetVector(ShaderIDs._ShoSegmentB, m_HableCurve.uniforms.shoSegmentB);
                            m_CurveMaterial.SetVector("_Variants", new Vector4(alpha, m_HableCurve.whitePoint, 0f, 0f));

                            CheckCurveRT((int)m_CurveRect.width, (int)m_CurveRect.height);
                            
                            var oldRt = RenderTexture.active;
                            Graphics.Blit(null, m_CurveTex, m_CurveMaterial, EditorGUIUtility.isProSkin ? 0 : 1);
                            RenderTexture.active = oldRt;
                            
                            GUI.DrawTexture(m_CurveRect, m_CurveTex);
                            
                            Handles.DrawSolidRectangleWithOutline(m_CurveRect, Color.clear, Color.white * 0.4f);
                        }
                            
                        PropertyField(m_EnvToeStrength, new GUIContent("Toe Strength"));
                        PropertyField(m_EnvToeLength, new GUIContent("Toe Length"));
                        PropertyField(m_EnvShoulderStrength, new GUIContent("Shoulder Strength"));
                        PropertyField(m_EnvShoulderLength, new GUIContent("Shoulder Length"));
                        PropertyField(m_EnvShoulderAngle, new GUIContent("Shoulder Angle"));
                        PropertyField(m_EnvGamma, new GUIContent("Gamma"));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                    
                    EditorGUILayout.Space(1f);
                    EditorGUILayout.BeginVertical(subGroupBoxStyle);
                    EditorGUILayout.LabelField("Color Adjustments", postProcessLabelStyle);
                    PropertyField(m_EnvPostExposure, new GUIContent("Post Brightness"));
                    PropertyField(m_EnvContrast, new GUIContent("Contrast"));
                    PropertyField(m_EnvColorFilter, new GUIContent("Color Filter"));
                    PropertyField(m_EnvHueShift, new GUIContent("Hue Shift"));
                    PropertyField(m_EnvSaturation, new GUIContent("Saturation"));
                    EditorGUILayout.EndVertical();
                    
                    EditorGUILayout.Space(1f);
                    EditorGUILayout.BeginVertical(subGroupBoxStyle);
                    EditorGUILayout.LabelField("White Balance", postProcessLabelStyle);
                    PropertyField(m_EnvWhiteBalanceTemperature, new GUIContent("Temperature"));
                    PropertyField(m_EnvWhiteBalanceTint, new GUIContent("Tint"));
                    EditorGUILayout.EndVertical();
                }
                
                s_EnvBloomFoldout = EditorGUILayout.Foldout(s_EnvBloomFoldout, "[Bloom]", true, EditorStyles.foldoutHeader);
                if (s_EnvBloomFoldout)
                {
                    EditorGUILayout.BeginVertical(subGroupBoxStyle);
                    PropertyField(m_EnvBloom);
                    if (m_EnvBloom.value.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        PropertyField(m_EnvBloomThreshold, new GUIContent("Threshold"));
                        PropertyField(m_EnvBloomIntensity, new GUIContent("Intensity"));
                        PropertyField(m_EnvBloomScatter, new GUIContent("Scatter"));
                        PropertyField(m_EnvBloomClamp, new GUIContent("Clamp"));
                        PropertyField(m_EnvBloomTint, new GUIContent("Tint"));
                        PropertyField(m_EnvBloomHQ, new GUIContent("High Quality Filtering"));
                        PropertyField(m_EnvBloomDownscale, new GUIContent("DownScale"));
                        PropertyField(m_EnvBloomMaxIterations, new GUIContent("Max Iterations"));
                        PropertyField(m_EnvBloomDirtTexture, new GUIContent("Dirt Texture"));
                        PropertyField(m_EnvBloomDirtIntensity, new GUIContent("Dirt Intensity"));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
                
                EditorGUI.indentLevel--;
            }
            
            // Advanced Settings
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField("ADVANCED SETTINGS", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("[PLEASE READ THE TOOLTIP]");
            var originalAdvancedSettingsUnlocked = PotaToonGUIUtility.advancedSettingsUnlocked;

            PotaToonGUIUtility.DrawAdvancedSettingsButton();
            
            if (PotaToonGUIUtility.advancedSettingsUnlocked)
            {
                s_ShadowSettingsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(s_ShadowSettingsFoldout, "Global Char Shadow Settings");
                if (s_ShadowSettingsFoldout)
                {
                    PropertyField(m_Bias, new GUIContent("Depth Bias"));
                    PropertyField(m_NormalBias);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("OIT is only available in Windows. (DX 11/12)");
                EditorGUI.indentLevel--;
                PropertyField(m_OIT, new GUIContent("OIT"));
                PropertyField(m_OITMode, new GUIContent("OIT Mode"));
            }

            GUI.backgroundColor = originalColor;

            if (PotaToonGUIUtility.advancedSettingsUnlocked != originalAdvancedSettingsUnlocked)
                PotaToonGUIUtility.SaveAdvancedSettingUnlocked();
        }
        
        private void CopyComponent()
        {
            var comp = target as VolumeComponent;
            if (comp == null)
                return;

            s_CopyBuffer = ScriptableObject.CreateInstance(comp.GetType()) as VolumeComponent;
            EditorUtility.CopySerialized(comp, s_CopyBuffer);
            PotaToonGUIUtility.ShowNotification("Copied!");
        }

        private void PasteComponent()
        {
            var comp = target as VolumeComponent;
            if (comp == null || s_CopyBuffer == null)
                return;

            Undo.RecordObject(comp, "Paste VolumeComponent Settings");
            EditorUtility.CopySerialized(s_CopyBuffer, comp);
            EditorUtility.SetDirty(comp);
            PotaToonGUIUtility.ShowNotification("Pasted!");
        }
        
        private void CheckCurveRT(int width, int height)
        {
            if (m_CurveTex == null || !m_CurveTex.IsCreated() || m_CurveTex.width != width || m_CurveTex.height != height)
            {
                CoreUtils.Destroy(m_CurveTex);
                m_CurveTex = new RenderTexture(width, height, 0, GraphicsFormat.R8G8B8A8_SRGB);
                m_CurveTex.hideFlags = HideFlags.HideAndDontSave;
            }
        }
        
#region Preset
        private void DrawPresetField()
        {
            if (!PotaToonGUIUtility.volumePresetShown)
                return;
            
            const float scrollHeight = 50f;
            const float itemWidth = 70f;
            const float itemHeight = 30f;
            
            GUIStyle presetHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
            };
            GUIStyle itemStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 5, 5),
                margin = new RectOffset(5, 5, 5, 5)
            };
            
            // Header
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("[ PRESET ]", presetHeaderStyle);
            EditorGUILayout.HelpBox("Right-click to edit preset.", MessageType.Info);
            
            var originalBackgroundColor = GUI.backgroundColor;
            
            var evt = Event.current;
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus", "|Create preset"), GUILayout.Height(20f)))
            {
                if (CreateAndAddPreset() != null)
                {
                    evt.Use();
                    PopupWindow.Show(new Rect(30f, 200f, 0, 0),
                        new VolumePresetContextMenu(m_Presets, m_Presets.Count - 1, target as PotaToon,
                            () =>
                            {
                                m_SelectedIndex = Mathf.Clamp(m_SelectedIndex, 0, m_Presets.Count - 1);
                            }));
                }
            }
            if (GUILayout.Button(EditorGUIUtility.IconContent("Import", "|Import preset"), GUILayout.Height(20f)))
            {
                ImportPreset();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
            
            // Items
            HandleKeyboardNavigation(itemWidth);
            
            m_ScrollPos = EditorGUILayout.BeginScrollView(  m_ScrollPos, true, false,
                                                            GUI.skin.horizontalScrollbar, GUIStyle.none, GUI.skin.box,
                                                            GUILayout.Height(scrollHeight), GUILayout.ExpandWidth(true));
            
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < m_Presets.Count; i++)
            {
                GUI.backgroundColor = i == m_SelectedIndex ? m_SelectedPresetColor : originalBackgroundColor;
                
                var assetPath = AssetDatabase.GetAssetPath(m_Presets[i]);
                var fileName = Path.GetFileNameWithoutExtension(assetPath);
                
                if (GUILayout.Button(fileName, itemStyle, GUILayout.Width(itemWidth), GUILayout.Height(itemHeight)))
                {
                    if (evt.button == 0)
                    {
                        m_SelectedIndex = i;
                        ApplyPreset(i);
                    }
                    else if (evt.button == 1)
                    {
                        evt.Use();
                        PopupWindow.Show(new Rect(0, 0, 0, 0),
                            new VolumePresetContextMenu(m_Presets, i, target as PotaToon,
                            () =>
                            {
                                m_SelectedIndex = Mathf.Clamp(m_SelectedIndex, 0, m_Presets.Count - 1);
                            }));
                    }
                }
            }
            GUI.backgroundColor = originalBackgroundColor;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        private void HandleKeyboardNavigation(float itemWidth)
        {
            if (EditorGUIUtility.editingTextField)
                return;
            
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.DownArrow)
                {
                    m_SelectedIndex = Mathf.Min(m_SelectedIndex + 1, m_Presets.Count - 1);
                    m_ScrollPos.x = m_SelectedIndex * itemWidth;
                    Repaint();
                    e.Use();
                    ApplyPreset(m_SelectedIndex);
                }
                else if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.UpArrow)
                {
                    m_SelectedIndex = Mathf.Max(m_SelectedIndex - 1, 0);
                    m_ScrollPos.x = m_SelectedIndex * itemWidth;
                    Repaint();
                    e.Use();
                    ApplyPreset(m_SelectedIndex);
                }
            }
        }
        
        private static MonoScript FindMonoScriptFor<T>()
        {
            var guids = AssetDatabase.FindAssets($"t:MonoScript {typeof(T).Name}");
            for (int i = 0; i < guids.Length; ++i)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms != null && ms.GetClass() == typeof(T))
                    return ms;
            }
            return null;
        }

        private void InitializePresetsAndIcons()
        {
#if UNITY_2021_3
            var monoScript = FindMonoScriptFor<PotaToonEditor>();
#else
            var monoScript = MonoScript.FromScriptableObject(this);
#endif
            var scriptPath = AssetDatabase.GetAssetPath(monoScript);
            var editorDir = Path.GetDirectoryName(scriptPath).Replace("\\Scripts", "/");

            var volumeDir = $"{editorDir}/Presets/Volume";

            m_Presets = new List<PotaToonVolumePreset>();
            m_SelectedIndex = -1;
            m_PresetIcon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{editorDir}/Textures/potatoon_icon.png");

            if (AssetDatabase.IsValidFolder(volumeDir))
            {
                var guids = AssetDatabase.FindAssets("t:PotaToonVolumePreset", new[] { volumeDir });
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var preset = AssetDatabase.LoadAssetAtPath<PotaToonVolumePreset>(path);
                    if (preset != null)
                        m_Presets.Add(preset);
                }
            }
        }
        
        private void ApplyPreset(int index)
        {
            var component = target as PotaToon;
            if (component != null)
            {
                Undo.RecordObject(component, "Apply PotaToonVolumePreset");
                m_Presets[index].ApplyTo(component);
                EditorUtility.SetDirty(component);
                AssetDatabase.SaveAssets();
                PotaToonGUIUtility.ShowNotification($"Applied preset: [{m_Presets[index].name}]");
            }
        }
        
        private PotaToonVolumePreset CreateAndAddPreset()
        {
#if UNITY_2021_3
            var monoScript = FindMonoScriptFor<PotaToonEditor>();
#else
            var monoScript = MonoScript.FromScriptableObject(this);
#endif
            var scriptPath = AssetDatabase.GetAssetPath(monoScript);
            var parentDir = Path.GetDirectoryName(scriptPath).Replace("\\Scripts", "/");
            var presetsBase = $"{parentDir}/Presets";
            var volumeBase = $"{presetsBase}/Volume";
            var presetsDir = $"{parentDir}/Presets/Volume";
            
            if (!AssetDatabase.IsValidFolder(presetsBase))
                AssetDatabase.CreateFolder(parentDir, "Presets");

            if (!AssetDatabase.IsValidFolder(volumeBase))
                AssetDatabase.CreateFolder(presetsBase, "Volume");

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{presetsDir}/New Preset.asset");
            var newPreset = ScriptableObject.CreateInstance<PotaToonVolumePreset>();
            AssetDatabase.CreateAsset(newPreset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            m_Presets.Add(newPreset);
            m_SelectedIndex = m_Presets.Count - 1;
            m_ScrollPos.x = m_SelectedIndex * 20f;
            return newPreset;
        }
        
        private void ImportPreset()
        {
            var absPath = EditorUtility.OpenFilePanel("Import PotaToonVolumePreset", "", "asset");
            if (string.IsNullOrEmpty(absPath))
                return;
            
#if UNITY_2021_3
            var monoScript = FindMonoScriptFor<PotaToonEditor>();
#else
            var monoScript = MonoScript.FromScriptableObject(this);
#endif
            var scriptPath = AssetDatabase.GetAssetPath(monoScript);
            var editorDir = Path.GetDirectoryName(scriptPath).Replace("\\Scripts", "/");

            var presetsBase = $"{editorDir}/Presets";
            var volumeDir = $"{presetsBase}/Volume";
            if (!AssetDatabase.IsValidFolder(presetsBase))
                AssetDatabase.CreateFolder(editorDir, "Presets");
            if (!AssetDatabase.IsValidFolder(volumeDir))
                AssetDatabase.CreateFolder(presetsBase, "Volume");
            
            var fileName = Path.GetFileName(absPath);
            var destPath = AssetDatabase.GenerateUniqueAssetPath($"{volumeDir}/{fileName}");

            File.Copy(absPath, destPath, overwrite: false);
            AssetDatabase.ImportAsset(destPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var imported = AssetDatabase.LoadAssetAtPath<PotaToonVolumePreset>(destPath);
            if (imported == null)
            {
                EditorUtility.DisplayDialog("Invalid Preset",
                    "The selected file is not a PotaToonVolumePreset asset.", "OK");
                AssetDatabase.DeleteAsset(destPath);
                AssetDatabase.SaveAssets();
                return;
            }
            
            PotaToonGUIUtility.ShowNotification($"Imported {imported.name}.");
            
            m_Presets.Add(imported);
        }
        
        internal class VolumePresetContextMenu : PopupWindowContent
        {
            private readonly Action m_OnDelete;
            private List<PotaToonVolumePreset> m_Presets;
            private string m_TempName;
            private int m_Index;
            private PotaToon m_Target;

            public VolumePresetContextMenu(List<PotaToonVolumePreset> presets, int idx, PotaToon volume, Action onDelete)
            {
                m_Presets = presets;
                m_TempName = m_Presets[idx].name;
                m_Index = idx;
                m_Target = volume;
                m_OnDelete = onDelete;
            }

            public override Vector2 GetWindowSize() => new Vector2(200, 205);

            public override void OnGUI(Rect rect)
            {
                var preset = m_Presets[m_Index];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Edit Preset", EditorStyles.boldLabel, GUILayout.Width(100f), GUILayout.Height(20f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("X", GUILayout.Width(20f)))
                {
                    editorWindow.Close();
                }
                EditorGUILayout.EndHorizontal();
                
                var textfieldStyle = new GUIStyle(GUI.skin.textField)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 13
                };
                m_TempName = GUILayout.TextField(m_TempName, textfieldStyle, GUILayout.Height(30f));

                if (GUILayout.Button("Rename", GUILayout.Height(30f)))
                {
                    if (!preset.name.Equals(m_TempName, StringComparison.Ordinal))
                    {
                        var oldPath = AssetDatabase.GetAssetPath(preset);
                        var newNameNoExt = Path.GetFileNameWithoutExtension(m_TempName);
                        AssetDatabase.RenameAsset(oldPath, newNameNoExt);
                        AssetDatabase.SaveAssets();
                        PotaToonGUIUtility.ShowNotification("Renamed.");
                    }
                }
                
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button("Find Preset in Project", GUILayout.Height(25f)))
                {
                    EditorUtility.FocusProjectWindow();
                    EditorGUIUtility.PingObject(preset);
                }
                
                if (GUILayout.Button("Export Preset", GUILayout.Height(25f)))
                {
                    ExportPreset(m_Index);
                }
                
                if (GUILayout.Button("Save (Override)", GUILayout.Height(30f)))
                {
                    SavePreset(m_Index);
                }
                
                if (GUILayout.Button("Delete", GUILayout.Height(30f)))
                {
                    if (RemovePreset(m_Index))
                    {
                        m_OnDelete();
                    }
                    editorWindow.Close();
                }
            }
            
            private bool RemovePreset(int index)
            {
                var preset = m_Presets[index];
                var path = AssetDatabase.GetAssetPath(preset);

                if (!EditorUtility.DisplayDialog("Remove Preset", $"Are you sure you want to remove preset '{preset.name}'? This operation can't be undone.", "Remove", "Cancel"))
                    return false;
            
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();

                m_Presets.RemoveAt(index);
                return true;
            }
        
            private void SavePreset(int index)
            {
                if (m_Target == null)
                    return;
            
                var preset = m_Presets[index];
                // Rename if needed
                if (!preset.name.Equals(m_TempName, StringComparison.Ordinal))
                {
                    var oldPath = AssetDatabase.GetAssetPath(preset);
                    var newNameNoExt = Path.GetFileNameWithoutExtension(m_TempName);
                    AssetDatabase.RenameAsset(oldPath, newNameNoExt);
                }
                preset.SaveFrom(m_Target);
                Undo.RecordObject(preset, "Save PotaToonVolumePreset");
                EditorUtility.SetDirty(preset);
                AssetDatabase.SaveAssets();
                PotaToonGUIUtility.ShowNotification("Saved.");
            }
            
            private void ExportPreset(int index)
            {
                // Get source asset path
                var preset = m_Presets[index];
                var sourcePath = AssetDatabase.GetAssetPath(preset);
                if (string.IsNullOrEmpty(sourcePath))
                {
                    EditorUtility.DisplayDialog("Export Failed", "Could not find the preset asset path.", "OK");
                    return;
                }

                // Ask user for target save path (anywhere)
                var defaultName = preset.name + ".asset";
                var absTarget = EditorUtility.SaveFilePanel(
                    "Export Volume Preset",
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
                    PotaToonEditorUtility.PotaToonLog($"Error exporting preset: {ex.Message}", true);
                    EditorUtility.DisplayDialog("Export Failed", ex.Message, "OK");
                }
            }
        }
#endregion
    }
}
