using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GH.World
{
    /// <summary>
    /// Main world의 WorldObject를 찾아 체력 비율에 따른 균열 오버레이와 비전투 체력 회복을 연결합니다.
    /// 기존 WorldObject 프리팹에는 컴포넌트를 저장하지 않고 런타임에만 보조 컴포넌트를 붙입니다.
    /// </summary>
    [DefaultExecutionOrder(650)]
    [DisallowMultipleComponent]
    public sealed class GHWorldObjectDamageRecoveryManager : MonoBehaviour
    {
        private const string CrackShaderName = "GH/World/Crack Overlay";

        private static readonly HashSet<GHWorldObjectDamageRecoveryManager> ActiveManagers =
            new HashSet<GHWorldObjectDamageRecoveryManager>();

        [Header("Scene World Object Rules")]
        [Tooltip("이 매니저와 같은 씬의 WorldObject가 타입별 파괴 방식을 사용합니다. 나무는 쓰러진 뒤 드롭하고, 부쉬는 체력이 1로 고정됩니다.")]
        [SerializeField] private bool enableTypeSpecificDepletion;

        [Tooltip("이 매니저와 같은 씬의 WorldObject 피격 충격광만 제거합니다. 균열과 체력 복구는 유지됩니다.")]
        [SerializeField] private bool disableImpactFlash;

        [Header("Damage Recovery")]
        [Tooltip(
            "월드 오브젝트가 마지막 피해를 받은 뒤 최대 체력으로 돌아가기까지 기다리는 시간입니다. " +
            "고갈되어 드롭이 발생한 오브젝트는 기존 Respawn Time을 그대로 사용하고 이 복구 대상에서 제외됩니다.")]
        [Min(0.1f)]
        [SerializeField] private float recoveryDelaySeconds = 10f;

        [Header("Crack Visual")]
        [Tooltip(
            "균열 오버레이 전용 셰이더입니다. 비워 두면 GH/World/Crack Overlay를 자동으로 찾습니다. " +
            "빌드 셰이더 스트리핑을 막기 위해 프리팹에서는 직접 연결해 두는 것을 권장합니다.")]
        [SerializeField] private Shader crackOverlayShader;

        [Tooltip("균열의 색과 불투명도입니다. 알파가 높을수록 균열이 선명하게 보입니다.")]
        [SerializeField] private Color crackColor = new Color(0.07f, 0.045f, 0.025f, 0.9f);

        [Tooltip(
            "어두운 나무와 광물에서도 균열이 보이도록 중심선 주변에 표시하는 밝은 테두리 색상입니다.")]
        [SerializeField] private Color crackHighlightColor =
            new Color(1f, 0.58f, 0.16f, 0.72f);

        [Tooltip("균열 하이라이트의 픽셀 폭입니다. 나무처럼 어두운 오브젝트에서 잘 안 보이면 값을 올리세요.")]
        [Range(0.5f, 4f)]
        [SerializeField] private float crackHighlightWidth = 2f;

        [Tooltip("균열 주변 하이라이트의 표시 강도입니다. 0이면 테두리를 표시하지 않습니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float crackHighlightStrength = 0.72f;

        [Header("Impact Feedback")]
        [Tooltip(
            "나무와 돌이 실제 피해를 받았을 때 전체 표면에 짧게 번지는 통일된 충격광 색상입니다.")]
        [SerializeField] private Color impactFlashColor =
            new Color(1f, 0.72f, 0.34f, 0.42f);

        [Tooltip("피격 충격광이 사라질 때까지 걸리는 시간입니다.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float impactFlashDuration = 0.18f;

        [Tooltip("피격 충격광의 최대 강도입니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float impactFlashIntensity = 0.9f;

        [Tooltip("균열 마스크가 오브젝트 UV 위에서 반복되는 횟수입니다. 값이 클수록 더 촘촘한 균열이 보입니다.")]
        [Range(0.25f, 4f)]
        [SerializeField] private float crackTiling = 1.15f;

        [Tooltip(
            "체력 감소량을 실제 균열 진행도로 변환하는 지수입니다. " +
            "1보다 높을수록 첫 피해의 균열은 적게 보이고, 체력이 낮아질수록 균열이 더 확실하게 증가합니다.")]
        [Range(1f, 3f)]
        [SerializeField] private float crackGrowthExponent = 1.45f;

        [Tooltip(
            "원본 표면과 균열 표면이 겹쳐 깜박이지 않도록 균열 메시를 아주 조금 확대하는 비율입니다. " +
            "너무 높이면 균열이 표면에서 떠 보일 수 있습니다.")]
        [Range(1.0001f, 1.01f)]
        [SerializeField] private float overlayScale = 1.002f;

        private readonly List<GHWorldObjectDamageRecovery> adapters = new();
        private Material runtimeCrackMaterial;
        private Texture2D runtimeCrackTexture;

        private void OnEnable()
        {
            ActiveManagers.Add(this);
            CreateRuntimeResources();
            WorldObject.InstanceEnabled -= HandleWorldObjectEnabled;
            WorldObject.InstanceEnabled += HandleWorldObjectEnabled;
            foreach (WorldObject worldObject in WorldObject.ActiveInstances)
            {
                ConfigureWorldObject(worldObject);
            }
        }

        private void OnDisable()
        {
            ActiveManagers.Remove(this);
            WorldObject.InstanceEnabled -= HandleWorldObjectEnabled;
            for (int i = adapters.Count - 1; i >= 0; i--)
            {
                if (adapters[i] != null)
                {
                    Destroy(adapters[i]);
                }
            }

            adapters.Clear();
            DestroyRuntimeResources();
        }

        private void HandleWorldObjectEnabled(WorldObject worldObject)
        {
            ConfigureWorldObject(worldObject);
        }

        private void ConfigureWorldObject(WorldObject worldObject)
        {
            if (worldObject == null)
            {
                return;
            }

            if (worldObject.gameObject.scene != gameObject.scene)
            {
                return;
            }

            if (enableTypeSpecificDepletion)
            {
                worldObject.ApplyTypeSpecificRules();
            }

            CreateRuntimeResources();
            if (runtimeCrackMaterial == null)
            {
                return;
            }

            GHWorldObjectDamageRecovery adapter =
                worldObject.GetComponent<GHWorldObjectDamageRecovery>();
            if (adapter == null)
            {
                adapter = worldObject.gameObject.AddComponent<GHWorldObjectDamageRecovery>();
            }

            bool useCrackVisual = !enableTypeSpecificDepletion
                || worldObject.RequiredToolType == KGH.Data.ObjectType.Stone;

            adapter.Configure(
                runtimeCrackMaterial,
                useCrackVisual,
                recoveryDelaySeconds,
                crackColor,
                crackHighlightColor,
                crackHighlightWidth,
                crackHighlightStrength,
                impactFlashColor,
                impactFlashDuration,
                disableImpactFlash ? 0f : impactFlashIntensity,
                crackTiling,
                crackGrowthExponent,
                overlayScale);

            if (!adapters.Contains(adapter))
            {
                adapters.Add(adapter);
            }
        }

        /// <summary>해당 WorldObject가 속한 씬에서 타입별 파괴 규칙을 켰는지 확인합니다.</summary>
        public static bool IsTypeSpecificDepletionEnabledFor(WorldObject worldObject)
        {
            if (worldObject == null)
            {
                return false;
            }

            foreach (GHWorldObjectDamageRecoveryManager manager in ActiveManagers)
            {
                if (manager != null
                    && manager.isActiveAndEnabled
                    && manager.enableTypeSpecificDepletion
                    && manager.gameObject.scene == worldObject.gameObject.scene)
                {
                    return true;
                }
            }

            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveManagers()
        {
            ActiveManagers.Clear();
        }

        private void CreateRuntimeResources()
        {
            if (runtimeCrackMaterial != null)
            {
                return;
            }

            if (crackOverlayShader == null)
            {
                crackOverlayShader = Shader.Find(CrackShaderName);
            }

            if (crackOverlayShader == null)
            {
                Debug.LogWarning(
                    "[GHWorldObjectDamageRecoveryManager] Crack Overlay 셰이더를 찾지 못했습니다.",
                    this);
                return;
            }

            runtimeCrackTexture = CreateCrackTexture();
            runtimeCrackMaterial = new Material(crackOverlayShader)
            {
                name = "GH Runtime World Object Crack",
                hideFlags = HideFlags.HideAndDontSave
            };
            runtimeCrackMaterial.SetTexture("_CrackTex", runtimeCrackTexture);
        }

        private void DestroyRuntimeResources()
        {
            if (runtimeCrackMaterial != null)
            {
                Destroy(runtimeCrackMaterial);
                runtimeCrackMaterial = null;
            }

            if (runtimeCrackTexture != null)
            {
                Destroy(runtimeCrackTexture);
                runtimeCrackTexture = null;
            }
        }

        private static Texture2D CreateCrackTexture()
        {
            const int Size = 256;
            Color32[] pixels = new Color32[Size * Size];
            System.Random random = new System.Random(42719);

            const int RootCount = 11;
            const int SegmentCount = 7;
            int totalMainSegments = RootCount * SegmentCount;

            for (int root = 0; root < RootCount; root++)
            {
                int x = random.Next(18, Size - 18);
                int y = random.Next(18, Size - 18);
                float angle = (float)(random.NextDouble() * Math.PI * 2.0);

                for (int segment = 0; segment < SegmentCount; segment++)
                {
                    int length = random.Next(12, 26);
                    int nextX = Mathf.Clamp(
                        x + Mathf.RoundToInt(Mathf.Cos(angle) * length),
                        2,
                        Size - 3);
                    int nextY = Mathf.Clamp(
                        y + Mathf.RoundToInt(Mathf.Sin(angle) * length),
                        2,
                        Size - 3);
                    // 선분마다 서로 다른 등장 시점을 저장해 첫 피해에서
                    // 모든 균열 뿌리가 한꺼번에 드러나지 않도록 한다.
                    int segmentIndex = root * SegmentCount + segment;
                    float revealStage = Mathf.Lerp(
                        0.02f,
                        0.92f,
                        segmentIndex / Mathf.Max(1f, totalMainSegments - 1f));
                    byte mainStage = EncodeRevealStage(revealStage);
                    DrawLine(pixels, Size, x, y, nextX, nextY, mainStage, 1);

                    if (segment > 0)
                    {
                        float branchAngle = angle
                            + (random.Next(0, 2) == 0 ? -1f : 1f)
                            * Mathf.Lerp(0.55f, 1.05f, (float)random.NextDouble());
                        int branchLength = random.Next(8, 19);
                        int branchX = Mathf.Clamp(
                            x + Mathf.RoundToInt(Mathf.Cos(branchAngle) * branchLength),
                            1,
                            Size - 2);
                        int branchY = Mathf.Clamp(
                            y + Mathf.RoundToInt(Mathf.Sin(branchAngle) * branchLength),
                            1,
                            Size - 2);
                        byte branchStage = EncodeRevealStage(
                            Mathf.Min(0.98f, revealStage + 0.055f));
                        DrawLine(pixels, Size, x, y, branchX, branchY, branchStage, 1);
                    }

                    x = nextX;
                    y = nextY;
                    angle += Mathf.Lerp(-0.55f, 0.55f, (float)random.NextDouble());
                }
            }

            Texture2D texture = new Texture2D(
                Size,
                Size,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "GH Runtime Crack Mask",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // 셰이더의 threshold 변화 범위와 동일하게 균열 등장 시점을 마스크 값으로 변환한다.
        private static byte EncodeRevealStage(float revealStage)
        {
            float maskValue = Mathf.Lerp(0.96f, 0.18f, Mathf.Clamp01(revealStage));
            return (byte)Mathf.Clamp(Mathf.RoundToInt(maskValue * 255f), 0, 255);
        }

        private static void DrawLine(
            Color32[] pixels,
            int size,
            int x0,
            int y0,
            int x1,
            int y1,
            byte value,
            int radius)
        {
            int deltaX = Mathf.Abs(x1 - x0);
            int stepX = x0 < x1 ? 1 : -1;
            int deltaY = -Mathf.Abs(y1 - y0);
            int stepY = y0 < y1 ? 1 : -1;
            int error = deltaX + deltaY;

            while (true)
            {
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    for (int offsetX = -radius; offsetX <= radius; offsetX++)
                    {
                        int drawX = x0 + offsetX;
                        int drawY = y0 + offsetY;
                        if (drawX < 0 || drawX >= size || drawY < 0 || drawY >= size)
                        {
                            continue;
                        }

                        int index = drawY * size + drawX;
                        byte existing = pixels[index].r;
                        byte finalValue = Math.Max(existing, value);
                        pixels[index] = new Color32(finalValue, finalValue, finalValue, 255);
                    }
                }

                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int doubledError = error * 2;
                if (doubledError >= deltaY)
                {
                    error += deltaY;
                    x0 += stepX;
                }

                if (doubledError <= deltaX)
                {
                    error += deltaX;
                    y0 += stepY;
                }
            }
        }

        private void OnValidate()
        {
            recoveryDelaySeconds = Mathf.Max(0.1f, recoveryDelaySeconds);
            crackTiling = Mathf.Clamp(crackTiling, 0.25f, 4f);
            crackHighlightWidth = Mathf.Clamp(crackHighlightWidth, 0.5f, 4f);
            crackHighlightStrength = Mathf.Clamp01(crackHighlightStrength);
            impactFlashDuration = Mathf.Clamp(impactFlashDuration, 0.05f, 0.5f);
            impactFlashIntensity = Mathf.Clamp01(impactFlashIntensity);
            crackGrowthExponent = Mathf.Clamp(crackGrowthExponent, 1f, 3f);
            overlayScale = Mathf.Clamp(overlayScale, 1.0001f, 1.01f);
        }
    }

    internal sealed class GHWorldObjectDamageRecovery : MonoBehaviour
    {
        private static readonly int CrackColorId = Shader.PropertyToID("_CrackColor");
        private static readonly int CrackHighlightColorId =
            Shader.PropertyToID("_CrackHighlightColor");
        private static readonly int CrackHighlightWidthId =
            Shader.PropertyToID("_CrackHighlightWidth");
        private static readonly int CrackHighlightStrengthId =
            Shader.PropertyToID("_CrackHighlightStrength");
        private static readonly int ImpactFlashColorId =
            Shader.PropertyToID("_ImpactFlashColor");
        private static readonly int ImpactFlashId =
            Shader.PropertyToID("_ImpactFlash");
        private static readonly int SeverityId = Shader.PropertyToID("_Severity");
        private static readonly int TilingId = Shader.PropertyToID("_Tiling");
        private const string OverlayName = "__GH_CrackOverlay";

        private readonly List<Renderer> overlayRenderers = new();
        private readonly List<Renderer> sourceRenderers = new();
        private WorldObject worldObject;
        private Material crackMaterial;
        private bool crackVisualEnabled = true;
        private MaterialPropertyBlock propertyBlock;
        private float recoveryDelaySeconds;
        private Color crackColor;
        private Color crackHighlightColor;
        private float crackHighlightWidth;
        private float crackHighlightStrength;
        private Color impactFlashColor;
        private float impactFlashDuration;
        private float impactFlashIntensity;
        private float crackTiling;
        private float crackGrowthExponent;
        private float overlayScale;
        private float lastDamageTime = float.PositiveInfinity;
        private float impactFlashStartTime = float.NegativeInfinity;
        private int previousHp;
        private bool configured;
        private bool impactFlashActive;
        private Coroutine activeFeedbackRoutine;

        private void Awake()
        {
            worldObject = GetComponent<WorldObject>();
            previousHp = worldObject != null ? worldObject.CurrentHp : 0;
        }

        private void OnEnable()
        {
            if (worldObject == null)
            {
                worldObject = GetComponent<WorldObject>();
            }

            if (worldObject != null)
            {
                worldObject.StateChanged -= HandleStateChanged;
                worldObject.StateChanged += HandleStateChanged;
                previousHp = worldObject.CurrentHp;
            }
        }

        private void OnDisable()
        {
            if (worldObject != null)
            {
                worldObject.StateChanged -= HandleStateChanged;
            }

            impactFlashActive = false;
            activeFeedbackRoutine = null;
        }

        private void OnDestroy()
        {
            for (int i = overlayRenderers.Count - 1; i >= 0; i--)
            {
                if (overlayRenderers[i] != null)
                {
                    Destroy(overlayRenderers[i].gameObject);
                }
            }

            overlayRenderers.Clear();
            sourceRenderers.Clear();
        }

        internal void Configure(
            Material material,
            bool enableCrackVisual,
            float recoveryDelay,
            Color color,
            Color highlightColor,
            float highlightWidth,
            float highlightStrength,
            Color flashColor,
            float flashDuration,
            float flashIntensity,
            float tiling,
            float growthExponent,
            float scale)
        {
            crackMaterial = material;
            crackVisualEnabled = enableCrackVisual;
            recoveryDelaySeconds = Mathf.Max(0.1f, recoveryDelay);
            crackColor = color;
            crackHighlightColor = highlightColor;
            crackHighlightWidth = Mathf.Clamp(highlightWidth, 0.5f, 4f);
            crackHighlightStrength = Mathf.Clamp01(highlightStrength);
            impactFlashColor = flashColor;
            impactFlashDuration = Mathf.Clamp(flashDuration, 0.05f, 0.5f);
            impactFlashIntensity = Mathf.Clamp01(flashIntensity);
            crackTiling = Mathf.Max(0.01f, tiling);
            crackGrowthExponent = Mathf.Max(1f, growthExponent);
            overlayScale = Mathf.Max(1.0001f, scale);

            configured = true;

            RefreshVisual();
            if (worldObject != null
                && !worldObject.IsDepleted
                && worldObject.CurrentHp < worldObject.MaxHp)
            {
                if (float.IsPositiveInfinity(lastDamageTime))
                {
                    lastDamageTime = Time.time;
                }

                StartActiveFeedbackRoutine();
            }
        }

        private void HandleStateChanged(WorldObject changedObject)
        {
            if (changedObject == null)
            {
                return;
            }

            if (changedObject.CurrentHp < previousHp)
            {
                lastDamageTime = Time.time;
                TriggerImpactFlash();
                StartActiveFeedbackRoutine();
            }

            previousHp = changedObject.CurrentHp;
            RefreshVisual();
        }

        private void StartActiveFeedbackRoutine()
        {
            if (activeFeedbackRoutine != null)
            {
                StopCoroutine(activeFeedbackRoutine);
            }

            activeFeedbackRoutine = StartCoroutine(RunActiveFeedbackAndRecovery());
        }

        private IEnumerator RunActiveFeedbackAndRecovery()
        {
            while (configured && worldObject != null)
            {
                bool needsRecovery = !worldObject.IsDepleted
                    && worldObject.CurrentHp < worldObject.MaxHp;
                if (needsRecovery && Time.time - lastDamageTime >= recoveryDelaySeconds)
                {
                    worldObject.RestoreHealthToMaximum();
                    previousHp = worldObject.CurrentHp;
                    needsRecovery = false;
                }

                if (impactFlashActive)
                {
                    RefreshVisual();
                }

                if (!needsRecovery && !impactFlashActive)
                {
                    break;
                }

                yield return null;
            }

            RefreshVisual();
            activeFeedbackRoutine = null;
        }

        private void TriggerImpactFlash()
        {
            impactFlashStartTime = Time.time;
            impactFlashActive = impactFlashIntensity > 0.001f;
        }

        private float EvaluateImpactFlash()
        {
            if (!impactFlashActive)
            {
                return 0f;
            }

            float normalizedTime =
                (Time.time - impactFlashStartTime) / Mathf.Max(0.05f, impactFlashDuration);
            if (normalizedTime >= 1f)
            {
                impactFlashActive = false;
                return 0f;
            }

            float remaining = 1f - Mathf.Clamp01(normalizedTime);
            return impactFlashIntensity * remaining * remaining;
        }

        private void CreateOverlays()
        {
            if (worldObject == null || crackMaterial == null)
            {
                return;
            }

            MeshRenderer[] meshRenderers =
                worldObject.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                MeshRenderer source = meshRenderers[i];
                if (source == null || source.gameObject.name.StartsWith(OverlayName))
                {
                    continue;
                }

                MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
                if (sourceFilter == null || sourceFilter.sharedMesh == null)
                {
                    continue;
                }

                GameObject overlayObject = new GameObject($"{OverlayName}_{i}");
                overlayObject.layer = source.gameObject.layer;
                Transform overlayTransform = overlayObject.transform;
                overlayTransform.SetParent(source.transform, false);
                overlayTransform.localScale = Vector3.one * overlayScale;

                MeshFilter overlayFilter = overlayObject.AddComponent<MeshFilter>();
                overlayFilter.sharedMesh = sourceFilter.sharedMesh;

                MeshRenderer overlay = overlayObject.AddComponent<MeshRenderer>();
                CopyRendererSettings(source, overlay);
                overlay.sharedMaterials =
                    CreateOverlayMaterials(source.sharedMaterials.Length, crackMaterial);

                sourceRenderers.Add(source);
                overlayRenderers.Add(overlay);
            }

            SkinnedMeshRenderer[] skinnedRenderers =
                worldObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                SkinnedMeshRenderer source = skinnedRenderers[i];
                if (source == null
                    || source.sharedMesh == null
                    || source.gameObject.name.StartsWith(OverlayName))
                {
                    continue;
                }

                GameObject overlayObject = new GameObject($"{OverlayName}_Skinned_{i}");
                overlayObject.layer = source.gameObject.layer;
                Transform overlayTransform = overlayObject.transform;
                overlayTransform.SetParent(source.transform, false);
                overlayTransform.localScale = Vector3.one * overlayScale;

                SkinnedMeshRenderer overlay =
                    overlayObject.AddComponent<SkinnedMeshRenderer>();
                overlay.sharedMesh = source.sharedMesh;
                overlay.bones = source.bones;
                overlay.rootBone = source.rootBone;
                overlay.localBounds = source.localBounds;
                overlay.updateWhenOffscreen = source.updateWhenOffscreen;
                CopyRendererSettings(source, overlay);
                overlay.sharedMaterials =
                    CreateOverlayMaterials(source.sharedMaterials.Length, crackMaterial);

                sourceRenderers.Add(source);
                overlayRenderers.Add(overlay);
            }
        }

        private static Material[] CreateOverlayMaterials(int count, Material material)
        {
            Material[] materials = new Material[Mathf.Max(1, count)];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            return materials;
        }

        private static void CopyRendererSettings(Renderer source, Renderer target)
        {
            target.shadowCastingMode = ShadowCastingMode.Off;
            target.receiveShadows = false;
            target.lightProbeUsage = LightProbeUsage.Off;
            target.reflectionProbeUsage = ReflectionProbeUsage.Off;
            target.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            target.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            target.renderingLayerMask = source.renderingLayerMask;
        }

        private void RefreshVisual()
        {
            if (worldObject == null || propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            float damageRatio = !crackVisualEnabled
                || worldObject == null
                || worldObject.MaxHp <= 0
                ? 0f
                : 1f - Mathf.Clamp01((float)worldObject.CurrentHp / worldObject.MaxHp);
            float severity = Mathf.Pow(damageRatio, crackGrowthExponent);
            float impactFlash = EvaluateImpactFlash();

            if (overlayRenderers.Count == 0
                && (severity > 0.001f || impactFlash > 0.001f))
            {
                CreateOverlays();
            }

            propertyBlock.Clear();
            propertyBlock.SetColor(CrackColorId, crackColor);
            propertyBlock.SetColor(CrackHighlightColorId, crackHighlightColor);
            propertyBlock.SetFloat(CrackHighlightWidthId, crackHighlightWidth);
            propertyBlock.SetFloat(CrackHighlightStrengthId, crackHighlightStrength);
            propertyBlock.SetColor(ImpactFlashColorId, impactFlashColor);
            propertyBlock.SetFloat(ImpactFlashId, impactFlash);
            propertyBlock.SetFloat(SeverityId, severity);
            propertyBlock.SetFloat(TilingId, crackTiling);

            for (int i = 0; i < overlayRenderers.Count; i++)
            {
                Renderer overlay = overlayRenderers[i];
                if (overlay == null)
                {
                    continue;
                }

                bool sourceVisible =
                    i < sourceRenderers.Count
                    && sourceRenderers[i] != null
                    && sourceRenderers[i].enabled;
                overlay.enabled =
                    (severity > 0.001f || impactFlash > 0.001f) && sourceVisible;
                overlay.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
