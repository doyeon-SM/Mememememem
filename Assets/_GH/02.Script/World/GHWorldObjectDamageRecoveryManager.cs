using System;
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

        [Tooltip("균열 마스크가 오브젝트 UV 위에서 반복되는 횟수입니다. 값이 클수록 더 촘촘한 균열이 보입니다.")]
        [Range(0.25f, 4f)]
        [SerializeField] private float crackTiling = 1.15f;

        [Tooltip(
            "원본 표면과 균열 표면이 겹쳐 깜박이지 않도록 균열 메시를 아주 조금 확대하는 비율입니다. " +
            "너무 높이면 균열이 표면에서 떠 보일 수 있습니다.")]
        [Range(1.0001f, 1.01f)]
        [SerializeField] private float overlayScale = 1.002f;

        [Header("Discovery")]
        [Tooltip(
            "청크 활성화나 런타임 배치로 새로 나타난 WorldObject를 다시 찾는 간격입니다. " +
            "매 프레임 검색하지 않아 성능 부담을 제한합니다.")]
        [Min(0.1f)]
        [SerializeField] private float discoveryIntervalSeconds = 1f;

        private readonly List<GHWorldObjectDamageRecovery> adapters = new();
        private Material runtimeCrackMaterial;
        private Texture2D runtimeCrackTexture;
        private float nextDiscoveryTime;

        private void OnEnable()
        {
            CreateRuntimeResources();
            DiscoverWorldObjects();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextDiscoveryTime)
            {
                return;
            }

            DiscoverWorldObjects();
        }

        private void OnDisable()
        {
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

        private void DiscoverWorldObjects()
        {
            nextDiscoveryTime =
                Time.unscaledTime + Mathf.Max(0.1f, discoveryIntervalSeconds);
            CreateRuntimeResources();

            if (runtimeCrackMaterial == null)
            {
                return;
            }

            WorldObject[] worldObjects = FindObjectsByType<WorldObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < worldObjects.Length; i++)
            {
                WorldObject worldObject = worldObjects[i];
                if (worldObject == null)
                {
                    continue;
                }

                GHWorldObjectDamageRecovery adapter =
                    worldObject.GetComponent<GHWorldObjectDamageRecovery>();
                if (adapter == null)
                {
                    adapter = worldObject.gameObject.AddComponent<GHWorldObjectDamageRecovery>();
                }

                adapter.Configure(
                    runtimeCrackMaterial,
                    recoveryDelaySeconds,
                    crackColor,
                    crackTiling,
                    overlayScale);

                if (!adapters.Contains(adapter))
                {
                    adapters.Add(adapter);
                }
            }

            adapters.RemoveAll(adapter => adapter == null);
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

            for (int root = 0; root < 11; root++)
            {
                int x = random.Next(18, Size - 18);
                int y = random.Next(18, Size - 18);
                float angle = (float)(random.NextDouble() * Math.PI * 2.0);

                for (int segment = 0; segment < 7; segment++)
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
                    DrawLine(pixels, Size, x, y, nextX, nextY, 255, 1);

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
                        byte branchStage = (byte)(segment < 3 ? 205 : segment < 5 ? 165 : 125);
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
            overlayScale = Mathf.Clamp(overlayScale, 1.0001f, 1.01f);
            discoveryIntervalSeconds = Mathf.Max(0.1f, discoveryIntervalSeconds);
        }
    }

    internal sealed class GHWorldObjectDamageRecovery : MonoBehaviour
    {
        private static readonly int CrackColorId = Shader.PropertyToID("_CrackColor");
        private static readonly int SeverityId = Shader.PropertyToID("_Severity");
        private static readonly int TilingId = Shader.PropertyToID("_Tiling");
        private const string OverlayName = "__GH_CrackOverlay";

        private readonly List<Renderer> overlayRenderers = new();
        private readonly List<Renderer> sourceRenderers = new();
        private WorldObject worldObject;
        private Material crackMaterial;
        private MaterialPropertyBlock propertyBlock;
        private float recoveryDelaySeconds;
        private Color crackColor;
        private float crackTiling;
        private float overlayScale;
        private float lastDamageTime = float.PositiveInfinity;
        private int previousHp;
        private bool configured;

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
            float recoveryDelay,
            Color color,
            float tiling,
            float scale)
        {
            crackMaterial = material;
            recoveryDelaySeconds = Mathf.Max(0.1f, recoveryDelay);
            crackColor = color;
            crackTiling = Mathf.Max(0.01f, tiling);
            overlayScale = Mathf.Max(1.0001f, scale);

            if (!configured)
            {
                CreateOverlays();
                configured = true;
            }

            RefreshVisual();
        }

        private void Update()
        {
            if (!configured || worldObject == null)
            {
                return;
            }

            int currentHp = worldObject.CurrentHp;
            if (currentHp < previousHp)
            {
                lastDamageTime = Time.time;
            }

            if (!worldObject.IsDepleted
                && currentHp < worldObject.MaxHp
                && Time.time - lastDamageTime >= recoveryDelaySeconds)
            {
                worldObject.RestoreHealthToMaximum();
                currentHp = worldObject.CurrentHp;
            }

            if (currentHp != previousHp)
            {
                previousHp = currentHp;
                RefreshVisual();
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
            }

            previousHp = changedObject.CurrentHp;
            RefreshVisual();
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

            float severity = worldObject == null || worldObject.MaxHp <= 0
                ? 0f
                : 1f - Mathf.Clamp01((float)worldObject.CurrentHp / worldObject.MaxHp);

            propertyBlock.Clear();
            propertyBlock.SetColor(CrackColorId, crackColor);
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
                overlay.enabled = severity > 0.001f && sourceVisible;
                overlay.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
