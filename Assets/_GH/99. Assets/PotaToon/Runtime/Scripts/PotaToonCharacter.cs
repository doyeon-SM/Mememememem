using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotaToon
{
    [ExecuteAlways]
    public class PotaToonCharacter : MonoBehaviour
    {
        private static class PropertyIDs
        {
            public static readonly int _BaseColor = Shader.PropertyToID("_BaseColor");
            public static readonly int _ShadeColor = Shader.PropertyToID("_ShadeColor");
            public static readonly int _BaseStep = Shader.PropertyToID("_BaseStep");
            public static readonly int _StepSmoothness = Shader.PropertyToID("_StepSmoothness");
            public static readonly int _ReceiveLightShadow = Shader.PropertyToID("_ReceiveLightShadow");
            public static readonly int _UseMidTone = Shader.PropertyToID("_UseMidTone");
            public static readonly int _MidColor = Shader.PropertyToID("_MidColor");
            public static readonly int _MidWidth = Shader.PropertyToID("_MidWidth");
            public static readonly int _IndirectDimmer = Shader.PropertyToID("_IndirectDimmer");
            public static readonly int _RimColor = Shader.PropertyToID("_RimColor");
            public static readonly int _RimPower = Shader.PropertyToID("_RimPower");
            public static readonly int _RimSmoothness = Shader.PropertyToID("_RimSmoothness");
            public static readonly int _OutlineWidth = Shader.PropertyToID("_OutlineWidth");
            public static readonly int _OutlineColor = Shader.PropertyToID("_OutlineColor");
            public static readonly int _HiLightColor = Shader.PropertyToID("_SpecularColor");
            public static readonly int _EmissionColor = Shader.PropertyToID("_EmissionColor");
            
            // Runtime Properties
            public static readonly int _FaceForward = Shader.PropertyToID("_FaceForward");
            public static readonly int _FaceUp = Shader.PropertyToID("_FaceUp");
            public static readonly int _HeadWorldPos = Shader.PropertyToID("_HeadWorldPos");
            public static readonly int _UseDitherFade = Shader.PropertyToID("_UseDitherFade");
            public static readonly int _DitherFadeMultiplier = Shader.PropertyToID("_DitherFadeMultiplier");
            public static readonly int _DitherFadeMinZ = Shader.PropertyToID("_DitherFadeMinZ");
            public static readonly int _DitherFadeMaxZ = Shader.PropertyToID("_DitherFadeMaxZ");
        }

        private static class ShaderPassStrings
        {
            public const string OpaqueOutline = "SRPDefaultUnlit";
            public const string TransparentOutline = "TransparentOutline";
            public const string UniversalForward = "UniversalForward";
            public const string OpaqueDitherFade = "OpaqueDitherFade";
            public const string OpaqueDitherFadeOutline = "OpaqueDitherFadeOutline";
        }
        
#if UNITY_EDITOR
        private class PropertyHistoryData
        {
            public Color baseColor = Color.white;
            public Color shadeColor = Color.white;
            public float baseStep = 0.5f;
            public float stepSmoothness = 0.01f;
            public bool  receiveLightShadow = true;
            public bool  useMidTone = true;
            public Color midTone = Color.black;
            public float midThickness;
            public float indirectDimmer;
            public Color rimLightColor = Color.black;
            public float rimPower = 0.5f;
            public float rimSmoothness = 0.25f;
            public float outlineWidth = 0.1f;
            public Color outlineColor = Color.black;
            public Color hiLightColor = Color.black;
            public Color emissionColor = Color.black;
        }
        
        private PropertyHistoryData[] m_PropertyHistoryDatas;
        private PropertyHistoryData[] m_EditorHistoryDatas;
#endif
        
        public static int activeCharacters = 0;
        public static HashSet<Renderer> activeRenderers = new HashSet<Renderer>();
        public static Dictionary<Renderer, Transform> headFromActiveRenderers = new Dictionary<Renderer, Transform>();
        public static bool requireDitherFade => s_ActiveDitherFadeCharacters.Count > 0;
        private static HashSet<PotaToonCharacter> s_ActiveDitherFadeCharacters = new HashSet<PotaToonCharacter>();
        private HashSet<Material> m_AllMaterials = new HashSet<Material>();
        private Renderer[] m_Renderers;
        private PotaToonCharacter m_Owner;
        private bool m_OwnerDirty;
        private bool m_IsOwnerRegistered;
        private bool m_DitherFadeActive;
        // Owner is updated by hierarchy events.

        public List<Material> allMaterials = new List<Material>();
        public Transform head;
        public bool useDitherFade = false;
        public float ditherFadeMinZ = 0.1f;
        public float ditherFadeMaxZ = 0.5f;
        private bool m_PrevDitherFadeActive = false;
        
        [Header("[Editor Only] All Materials Control")]
        [ColorUsage(true, true)] public Color baseColor = Color.white;
        [ColorUsage(true, false)] public Color shadeColor = Color.white;
        [Range(0f, 1f)] public float baseStep = 0.5f;
        [Range(0f, 0.1f)] public float stepSmoothness = 0.01f;
        public bool receiveLightShadow = true;
        public bool useMidTone = true;
        [ColorUsage(true, true)] public Color midTone = Color.black;
        [Range(0f, 1f)] public float midThickness = 1f;
        [Range(0f, 10f)] public float indirectDimmer = 1f;
        [ColorUsage(true, true)] public Color rimLightColor = Color.black;
        [Range(0f, 1f)] public float rimPower = 0.5f;
        [Range(0f, 0.5f)] public float rimSmoothness = 0.25f;
        [Range(0f, 10f)] public float outlineWidth = 0.1f;
        [ColorUsage(false)] public Color outlineColor = Color.black;
        [ColorUsage(true, true)] public Color hiLightColor = Color.black;
        [ColorUsage(true, true)] public Color emissionColor = Color.black;


        void Awake()
        {
            ResolveOwner();
            if (IsOwner)
            {
                m_Renderers = GetComponentsInChildren<Renderer>();
                UpdateMaterials();
                FindHead();
#if UNITY_EDITOR
                LoadPropertyHistoryData();
#endif
            }
        }

        void OnEnable()
        {
            EnsureOwner();
            if (IsOwner)
            {
                if (!m_IsOwnerRegistered)
                {
                    UpdateMaterials();
                    FindHead();
                    RegisterOwner();
                }
                else
                {
                    m_OwnerDirty = true;
                }
                
                ResetDitherFadeProperties();
            }
            else
            {
                RequestOwnerRebuild();
            }
        }
        
        void OnDisable()
        {
            if (IsOwner)
            {
                UnregisterOwner();
                ResetDitherFadeProperties();
            }
            else
            {
                RequestOwnerRebuild();
            }
        }

#if UNITY_EDITOR
        private bool IsFloatDirty(float lhs, float rhs)
        {
            return Math.Abs(lhs - rhs) > float.Epsilon;
        }

        private void OnValidate()
        {
            if (m_Owner == null)
                ResolveOwner();

            if (!IsOwner)
                return;

            UpdateMaterials();
        }

        public void UpdateMaterialProperties()
        {
            if (!IsOwner)
                return;

            if (m_PropertyHistoryDatas == null)
            {
                LoadPropertyHistoryData();
                return;
            }
            
            for (int i = 0; i < m_PropertyHistoryDatas.Length; i++)
            {
                var historyData = m_PropertyHistoryDatas[i];
                if (historyData == null || i >= allMaterials.Count)
                    continue;
                
                var material = allMaterials[i];
                if (material == null)
                    continue;
                
                if (baseColor != m_EditorHistoryDatas[i].baseColor && historyData.baseColor != baseColor)
                {
                    m_EditorHistoryDatas[i].baseColor = baseColor;
                    historyData.baseColor = baseColor;
                    material.SetColor(PropertyIDs._BaseColor, baseColor);
                }
                
                if (shadeColor != m_EditorHistoryDatas[i].shadeColor && historyData.shadeColor != shadeColor)
                {
                    m_EditorHistoryDatas[i].shadeColor = shadeColor;
                    historyData.shadeColor = shadeColor;
                    material.SetColor(PropertyIDs._ShadeColor, shadeColor);
                }
                
                if (IsFloatDirty(m_EditorHistoryDatas[i].baseStep,baseStep) && IsFloatDirty(historyData.baseStep,baseStep))
                {
                    m_EditorHistoryDatas[i].baseStep = baseStep;
                    historyData.baseStep = baseStep;
                    material.SetFloat(PropertyIDs._BaseStep, baseStep);
                }
                
                if (IsFloatDirty(m_EditorHistoryDatas[i].stepSmoothness,stepSmoothness) && IsFloatDirty(historyData.stepSmoothness,stepSmoothness))
                {
                    m_EditorHistoryDatas[i].stepSmoothness = stepSmoothness;
                    historyData.stepSmoothness = stepSmoothness;
                    material.SetFloat(PropertyIDs._StepSmoothness, stepSmoothness);
                }
                
                if (receiveLightShadow != m_EditorHistoryDatas[i].receiveLightShadow && historyData.receiveLightShadow != receiveLightShadow)
                {
                    m_EditorHistoryDatas[i].receiveLightShadow = receiveLightShadow;
                    historyData.receiveLightShadow = receiveLightShadow;
                    material.SetInt(PropertyIDs._ReceiveLightShadow, receiveLightShadow ? 1 : 0);
                }
                
                if (useMidTone != m_EditorHistoryDatas[i].useMidTone && historyData.useMidTone != useMidTone)
                {
                    m_EditorHistoryDatas[i].useMidTone = useMidTone;
                    historyData.useMidTone = useMidTone;
                    material.SetInt(PropertyIDs._UseMidTone, useMidTone ? 1 : 0);
                }
                
                if (midTone != m_EditorHistoryDatas[i].midTone && historyData.midTone != midTone)
                {
                    m_EditorHistoryDatas[i].midTone = midTone;
                    historyData.midTone = midTone;
                    material.SetColor(PropertyIDs._MidColor, midTone);
                }
                
                if (IsFloatDirty(m_EditorHistoryDatas[i].midThickness,midThickness) && IsFloatDirty(historyData.midThickness, midThickness))
                {
                    m_EditorHistoryDatas[i].midThickness = midThickness;
                    historyData.midThickness = midThickness;
                    material.SetFloat(PropertyIDs._MidWidth, midThickness);
                }
                
                if (IsFloatDirty(m_EditorHistoryDatas[i].indirectDimmer,indirectDimmer) && IsFloatDirty(historyData.indirectDimmer, indirectDimmer))
                {
                    m_EditorHistoryDatas[i].indirectDimmer = indirectDimmer;
                    historyData.midThickness = indirectDimmer;
                    material.SetFloat(PropertyIDs._IndirectDimmer, indirectDimmer);
                }
                
                if (rimLightColor != m_EditorHistoryDatas[i].rimLightColor && historyData.rimLightColor != rimLightColor)
                {
                    m_EditorHistoryDatas[i].rimLightColor = rimLightColor;
                    historyData.rimLightColor = rimLightColor;
                    material.SetColor(PropertyIDs._RimColor, rimLightColor);
                }
                
                if (IsFloatDirty(m_EditorHistoryDatas[i].rimPower,rimPower) && IsFloatDirty(historyData.rimPower, rimPower))
                {
                    m_EditorHistoryDatas[i].rimPower = rimPower;
                    historyData.rimPower = rimPower;
                    material.SetFloat(PropertyIDs._RimPower, rimPower);
                }
                
                if (IsFloatDirty(m_EditorHistoryDatas[i].rimSmoothness,rimSmoothness) && IsFloatDirty(historyData.rimSmoothness, rimSmoothness))
                {
                    m_EditorHistoryDatas[i].rimSmoothness = rimSmoothness;
                    historyData.rimSmoothness = rimSmoothness;
                    material.SetFloat(PropertyIDs._RimSmoothness, rimSmoothness);
                }
                
                if (IsFloatDirty(m_EditorHistoryDatas[i].outlineWidth,outlineWidth) && IsFloatDirty(historyData.outlineWidth, outlineWidth))
                {
                    m_EditorHistoryDatas[i].outlineWidth = outlineWidth;
                    historyData.outlineWidth = outlineWidth;
                    material.SetFloat(PropertyIDs._OutlineWidth, outlineWidth);
                }
                
                if (outlineColor != m_EditorHistoryDatas[i].outlineColor && historyData.outlineColor != outlineColor)
                {
                    m_EditorHistoryDatas[i].outlineColor = outlineColor;
                    historyData.outlineColor = outlineColor;
                    material.SetColor(PropertyIDs._OutlineColor, outlineColor);
                }
                
                if (hiLightColor != m_EditorHistoryDatas[i].hiLightColor && historyData.hiLightColor != hiLightColor)
                {
                    m_EditorHistoryDatas[i].hiLightColor = hiLightColor;
                    historyData.hiLightColor = hiLightColor;
                    material.SetColor(PropertyIDs._HiLightColor, hiLightColor);
                }
                
                if (emissionColor != m_EditorHistoryDatas[i].emissionColor && historyData.emissionColor != emissionColor)
                {
                    m_EditorHistoryDatas[i].emissionColor = emissionColor;
                    historyData.emissionColor = emissionColor;
                    material.SetColor(PropertyIDs._EmissionColor, emissionColor);
                }
            }
        }
#endif

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        }
        
        // Update is called once per frame
        void Update()
        {
            if (m_OwnerDirty && IsOwner)
                RebuildOwnerData();

            if (!IsOwner)
                return;

            bool isHeadValid = head != null;
            UpdateFaceVectors(isHeadValid);
            UpdateDitherFadeProperties(isHeadValid);
            UpdateOutlinePassActive();
        }
        
        private void UpdateFaceVectors(bool isHeadValid)
        {
            if (!isHeadValid)
                return;

            Vector3 faceForward = head.forward;
            Vector3 faceUp = head.up;
            Vector3 headPos = head.position;

            foreach (var material in allMaterials)
            {
                if (material != null)
                {
                    if (material.HasProperty(PropertyIDs._FaceForward))
                    {
                        material.SetVector(PropertyIDs._FaceForward, faceForward);
                        material.SetVector(PropertyIDs._FaceUp, faceUp);
                        material.SetVector(PropertyIDs._HeadWorldPos, headPos);
                    }
                }
            }
        }

        private void UpdateDitherFadeProperties(bool isHeadValid)
        {
            var targetCamera = Camera.current ?? Camera.main;
            if (targetCamera == null)
            {
                foreach (var cam in Camera.allCameras)
                {
                    if (cam != null && (cam.cameraType == CameraType.SceneView || cam.cameraType == CameraType.Game))
                    {
                        targetCamera = cam;
                        break;
                    }
                }
            }
            var objectPosition = isHeadValid ?  head.position : transform.position;
            var ditherFadeActive = Mathf.Abs(Vector3.Distance(targetCamera?.transform.position ?? Vector3.zero, objectPosition)) <= ditherFadeMaxZ;
            ditherFadeActive &= useDitherFade;
            var isDitherFadeValid = ditherFadeActive != m_PrevDitherFadeActive;
            m_PrevDitherFadeActive = ditherFadeActive;
            m_DitherFadeActive = ditherFadeActive;

            if (!isDitherFadeValid)
            {
                // Update properties only if no need change other settings
                if (ditherFadeActive)
                {
                    foreach (var material in allMaterials)
                    {
                        if (material != null && material.HasProperty(PropertyIDs._UseDitherFade))
                        {
                            material.SetFloat(PropertyIDs._DitherFadeMinZ, ditherFadeMinZ);
                            material.SetFloat(PropertyIDs._DitherFadeMaxZ, ditherFadeMaxZ);
                            if (!isHeadValid)
                                material.SetVector(PropertyIDs._HeadWorldPos, transform.position);
                        }
                    }
                }
                return;
            }
                
            if (ditherFadeActive)
            {
                s_ActiveDitherFadeCharacters.Add(this);
                foreach (var material in allMaterials)
                {
                    if (material != null && material.HasProperty(PropertyIDs._UseDitherFade))
                    {
                        material.SetInt(PropertyIDs._UseDitherFade, 1);
                        material.SetFloat(PropertyIDs._DitherFadeMinZ, ditherFadeMinZ);
                        material.SetFloat(PropertyIDs._DitherFadeMaxZ, ditherFadeMaxZ);
                        if (!isHeadValid)
                            material.SetVector(PropertyIDs._HeadWorldPos, transform.position);
                        material.SetShaderPassEnabled(ShaderPassStrings.OpaqueOutline, false);
                        material.SetShaderPassEnabled(ShaderPassStrings.OpaqueDitherFade, true);
                        material.SetShaderPassEnabled(ShaderPassStrings.OpaqueDitherFadeOutline, true);
                        if (material.renderQueue < 2501)
                            material.SetShaderPassEnabled(ShaderPassStrings.UniversalForward, false);
                    }
                }
            }
            else
            {
                s_ActiveDitherFadeCharacters.Remove(this);
                foreach (var material in allMaterials)
                {
                    if (material != null && material.HasProperty(PropertyIDs._UseDitherFade))
                    {
                        material.SetInt(PropertyIDs._UseDitherFade, 0);
                        material.SetShaderPassEnabled(ShaderPassStrings.OpaqueOutline, true);
                        material.SetShaderPassEnabled(ShaderPassStrings.OpaqueDitherFade, false);
                        material.SetShaderPassEnabled(ShaderPassStrings.OpaqueDitherFadeOutline, false);
                        if (material.renderQueue < 2501)
                            material.SetShaderPassEnabled(ShaderPassStrings.UniversalForward, true);
                    }
                }
            }
        }

        private void ResetDitherFadeProperties()
        {
            m_PrevDitherFadeActive = false;
            m_DitherFadeActive = false;
            s_ActiveDitherFadeCharacters.Remove(this);
            foreach (var material in allMaterials)
            {
                if (material != null && material.HasProperty(PropertyIDs._UseDitherFade))
                {
                    material.SetInt(PropertyIDs._UseDitherFade, 0);
                    material.SetShaderPassEnabled(ShaderPassStrings.OpaqueDitherFade, false);
                    material.SetShaderPassEnabled(ShaderPassStrings.OpaqueDitherFadeOutline, false);
                }
            }
        }
        
        private void ResetRenderers()
        {
            m_Renderers = GetComponentsInChildren<Renderer>();
            m_AllMaterials.Clear();
            allMaterials.Clear();
        }
        
        public void UpdateMaterials()
        {
            if (!IsOwner)
                return;

            ResetRenderers();
            if (m_Renderers != null)
            {
                foreach (var renderer in m_Renderers)
                {
                    if (renderer != null)
                    {
                        foreach (var mat in renderer.sharedMaterials)
                        {
                            m_AllMaterials.Add(mat);
                        }
                    }
                }
            }

            allMaterials.Capacity = m_AllMaterials.Count;
            foreach (var mat in m_AllMaterials)
            {
                allMaterials.Add(mat);
            }
        }

        private void FindHead()
        {
            if (!IsOwner)
                return;

            foreach (var child in transform.GetComponentsInChildren<Transform>())
            {
                if (child.name.ToLower().Equals("head"))
                {
                    head = child;
                    break;
                }
            }
        }

        private void UpdateOutlinePassActive()
        {
            if (m_PrevDitherFadeActive)
                return;
            
            foreach (var material in allMaterials)
            {
                if (material != null && material.HasProperty(PropertyIDs._OutlineWidth))
                {
                    var outlineActive = material.GetFloat(PropertyIDs._OutlineWidth) > 0f;
                    material.SetShaderPassEnabled(ShaderPassStrings.OpaqueOutline, outlineActive && material.renderQueue <= 2500);
                    material.SetShaderPassEnabled(ShaderPassStrings.TransparentOutline, outlineActive);
                }
            }
        }

        private void OnTransformParentChanged()
        {
            EnsureOwner();
            if (IsOwner)
                m_OwnerDirty = true;
            else
                RequestOwnerRebuild();
        }

        private void OnTransformChildrenChanged()
        {
            if (IsOwner)
                m_OwnerDirty = true;
        }

        public bool IsOwner => m_Owner == this;

        private void ResolveOwner()
        {
            var parent = transform.parent;
            var parentOwner = parent != null ? parent.GetComponentInParent<PotaToonCharacter>(true) : null;
            m_Owner = parentOwner != null ? parentOwner : this;
        }

        private void EnsureOwner()
        {
            var prevOwner = m_Owner;
            ResolveOwner();
            if (prevOwner != m_Owner)
                OnOwnerChanged(prevOwner, m_Owner);
        }

        private void OnOwnerChanged(PotaToonCharacter prevOwner, PotaToonCharacter newOwner)
        {
            if (prevOwner == this)
            {
                UnregisterOwner();
                ResetDitherFadeProperties();
            }
            if (prevOwner != null && prevOwner != this)
            {
                prevOwner.m_OwnerDirty = true;
            }

            if (newOwner == this)
            {
                UpdateMaterials();
                FindHead();
                RegisterOwner();
                m_DitherFadeActive = false;
                m_PrevDitherFadeActive = false;
            }
            else if (newOwner != null)
            {
                newOwner.m_OwnerDirty = true;
            }
        }

        private void RequestOwnerRebuild()
        {
            if (m_Owner != null)
                m_Owner.m_OwnerDirty = true;
        }

        private void RebuildOwnerData()
        {
            if (!IsOwner)
                return;

            m_OwnerDirty = false;

            if (m_IsOwnerRegistered)
                UnregisterRenderers();

            UpdateMaterials();
            FindHead();
            
            if (!m_IsOwnerRegistered)
                RegisterOwner();
            else
                RegisterRenderers();

            ApplyDitherFadeState(m_DitherFadeActive);
            if (!m_DitherFadeActive)
                UpdateOutlinePassActive();
        }

        private void RegisterOwner()
        {
            if (m_IsOwnerRegistered)
                return;

            if (m_Renderers == null)
                m_Renderers = GetComponentsInChildren<Renderer>();

            activeCharacters++;
            RegisterRenderers();
            m_IsOwnerRegistered = true;
        }

        private void UnregisterOwner()
        {
            if (!m_IsOwnerRegistered)
                return;

            UnregisterRenderers();
            activeCharacters = Mathf.Max(0, activeCharacters - 1);
            m_IsOwnerRegistered = false;
        }

        private void RegisterRenderers()
        {
            if (m_Renderers == null)
                return;

            foreach (var renderer in m_Renderers)
            {
                if (renderer != null)
                {
                    activeRenderers.Add(renderer);
                    headFromActiveRenderers[renderer] = head;
                }
            }
        }

        private void UnregisterRenderers()
        {
            if (m_Renderers == null)
                return;

            foreach (var renderer in m_Renderers)
            {
                if (renderer != null)
                {
                    activeRenderers.Remove(renderer);
                    headFromActiveRenderers.Remove(renderer);
                }
            }
        }

        private void ApplyDitherFadeState(bool active)
        {
            if (active)
            {
                s_ActiveDitherFadeCharacters.Add(this);
                foreach (var material in allMaterials)
                {
                    if (material != null && material.HasProperty(PropertyIDs._UseDitherFade))
                    {
                        material.SetInt(PropertyIDs._UseDitherFade, 1);
                        material.SetFloat(PropertyIDs._DitherFadeMinZ, ditherFadeMinZ);
                        material.SetFloat(PropertyIDs._DitherFadeMaxZ, ditherFadeMaxZ);
                        material.SetShaderPassEnabled(ShaderPassStrings.OpaqueOutline, false);
                        material.SetShaderPassEnabled(ShaderPassStrings.OpaqueDitherFade, true);
                        material.SetShaderPassEnabled(ShaderPassStrings.OpaqueDitherFadeOutline, true);
                        if (material.renderQueue < 2501)
                            material.SetShaderPassEnabled(ShaderPassStrings.UniversalForward, false);
                    }
                }
            }
            else
            {
                s_ActiveDitherFadeCharacters.Remove(this);
                foreach (var material in allMaterials)
                {
                    if (material != null && material.HasProperty(PropertyIDs._UseDitherFade))
                    {
                        material.SetInt(PropertyIDs._UseDitherFade, 0);
                        material.SetShaderPassEnabled(ShaderPassStrings.OpaqueDitherFade, false);
                        material.SetShaderPassEnabled(ShaderPassStrings.OpaqueDitherFadeOutline, false);
                        if (material.renderQueue < 2501)
                            material.SetShaderPassEnabled(ShaderPassStrings.UniversalForward, true);
                    }
                }
            }
        }

#if UNITY_EDITOR
        public void LoadPropertyHistoryData()
        {
            if (allMaterials.Count == 0)
                return;
            
            m_PropertyHistoryDatas = new PropertyHistoryData[allMaterials.Count];
            m_EditorHistoryDatas = new PropertyHistoryData[allMaterials.Count];
            for (int i = 0; i< allMaterials.Count; i++)
            {
                m_PropertyHistoryDatas[i] = new PropertyHistoryData();
                m_EditorHistoryDatas[i] = new PropertyHistoryData();
                var material = allMaterials[i];
                if (material == null)
                    continue;
                
                if (material.HasColor(PropertyIDs._BaseColor))
                    m_PropertyHistoryDatas[i].baseColor = material.GetColor(PropertyIDs._BaseColor);
                if (material.HasColor(PropertyIDs._ShadeColor))
                    m_PropertyHistoryDatas[i].shadeColor = material.GetColor(PropertyIDs._ShadeColor);
                if (material.HasFloat(PropertyIDs._BaseStep))
                    m_PropertyHistoryDatas[i].baseStep = material.GetFloat(PropertyIDs._BaseStep);
                if (material.HasFloat(PropertyIDs._StepSmoothness))
                    m_PropertyHistoryDatas[i].stepSmoothness = material.GetFloat(PropertyIDs._StepSmoothness);
                if (material.HasInt(PropertyIDs._ReceiveLightShadow))
                    m_PropertyHistoryDatas[i].receiveLightShadow = material.GetInt(PropertyIDs._ReceiveLightShadow) > 0;
                if (material.HasInt(PropertyIDs._UseMidTone))
                    m_PropertyHistoryDatas[i].useMidTone = material.GetInt(PropertyIDs._UseMidTone) > 0;
                if (material.HasColor(PropertyIDs._MidColor))
                    m_PropertyHistoryDatas[i].midTone = material.GetColor(PropertyIDs._MidColor);
                if (material.HasFloat(PropertyIDs._MidWidth))
                    m_PropertyHistoryDatas[i].midThickness = material.GetFloat(PropertyIDs._MidWidth);
                if (material.HasFloat(PropertyIDs._IndirectDimmer))
                    m_PropertyHistoryDatas[i].indirectDimmer = material.GetFloat(PropertyIDs._IndirectDimmer);
                if (material.HasColor(PropertyIDs._RimColor))
                    m_PropertyHistoryDatas[i].rimLightColor = material.GetColor(PropertyIDs._RimColor);
                if (material.HasFloat(PropertyIDs._RimPower))
                    m_PropertyHistoryDatas[i].rimPower = material.GetFloat(PropertyIDs._RimPower);
                if (material.HasFloat(PropertyIDs._RimSmoothness))
                    m_PropertyHistoryDatas[i].rimSmoothness = material.GetFloat(PropertyIDs._RimSmoothness);
                if (material.HasFloat(PropertyIDs._OutlineWidth))
                    m_PropertyHistoryDatas[i].outlineWidth = material.GetFloat(PropertyIDs._OutlineWidth);
                if (material.HasColor(PropertyIDs._OutlineColor))
                    m_PropertyHistoryDatas[i].outlineColor = material.GetColor(PropertyIDs._OutlineColor);
                if (material.HasColor(PropertyIDs._HiLightColor))
                    m_PropertyHistoryDatas[i].hiLightColor = material.GetColor(PropertyIDs._HiLightColor);
                if (material.HasColor(PropertyIDs._EmissionColor))
                    m_PropertyHistoryDatas[i].emissionColor = material.GetColor(PropertyIDs._EmissionColor);

                m_EditorHistoryDatas[i].baseColor = baseColor;
                m_EditorHistoryDatas[i].shadeColor = shadeColor;
                m_EditorHistoryDatas[i].baseStep = baseStep;
                m_EditorHistoryDatas[i].stepSmoothness = stepSmoothness;
                m_EditorHistoryDatas[i].receiveLightShadow = receiveLightShadow;
                m_EditorHistoryDatas[i].useMidTone = useMidTone;
                m_EditorHistoryDatas[i].midTone = midTone;
                m_EditorHistoryDatas[i].midThickness = midThickness;
                m_EditorHistoryDatas[i].indirectDimmer = indirectDimmer;
                m_EditorHistoryDatas[i].rimLightColor = rimLightColor;
                m_EditorHistoryDatas[i].rimPower = rimPower;
                m_EditorHistoryDatas[i].rimSmoothness = rimSmoothness;
                m_EditorHistoryDatas[i].outlineWidth = outlineWidth;
                m_EditorHistoryDatas[i].outlineColor = outlineColor;
                m_EditorHistoryDatas[i].hiLightColor = hiLightColor;
                m_EditorHistoryDatas[i].emissionColor = emissionColor;
            }
        }
#endif
    }
}
