using System;
using System.Collections.Generic;
using HDY;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 모든 월드 아이템의 등급 색상과 발광 표현을 한 곳에서 관리합니다.
/// 씬에 직접 배치하면 인스펙터에서 설정할 수 있고, 없으면 기본 설정으로 자동 생성됩니다.
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
[AddComponentMenu("GH/World/World Item Rarity VFX Manager")]
public sealed class WorldItemRarityVfxManager : MonoBehaviour
{
    [Serializable]
    public sealed class RarityStyle
    {
        public CommonClass itemClass;

        [ColorUsage(true, true)]
        public Color color = Color.white;

        [Min(0.1f)]
        public float emissionIntensity = 2f;

        [Min(0f)]
        public float sparkRate = 5f;

        [Min(0.005f)]
        public float sparkSize = 0.04f;

        [Min(0.05f)]
        public float ringRadius = 0.38f;

        [Min(0.002f)]
        public float ringWidth = 0.018f;

        public bool useSecondaryRing;
    }

    private const string VisualRootName = "World Item Rarity VFX";
    private const string CoreName = "Glow Core";
    private const string GroundRingName = "Ground Ring";
    private const string SecondaryRingName = "Secondary Ring";
    private const string SparkName = "Spark Particles";
    private const string GroundGlowName = "Ground Glow";
    private const string BeamCoreName = "Loot Beam Core";
    private const string BeamHaloName = "Loot Beam Halo";
    private const string SecondaryBeamName = "Loot Beam Secondary";
    private const string RarityLightName = "Rarity Light";

    private static WorldItemRarityVfxManager instance;

    [Header("전체 월드 아이템 등급 설정")]
    [SerializeField] private RarityStyle[] rarityStyles = CreateDefaultStyles();

    [Header("공통 표현")]
    [Tooltip("아이템 중심에 표시되는 발광 구체의 상대 크기입니다.")]
    [Range(0.05f, 0.6f)]
    [SerializeField] private float coreScale = 0.24f;

    [Tooltip("반짝임이 아이템 주변에서 생성되는 상대 반경입니다.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float particleRadius = 0.34f;

    [Tooltip("반짝임이 위로 흘러가는 속도입니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float particleRiseSpeed = 0.12f;

    [Header("Soft Loot Beam")]
    [Min(0.2f)] [SerializeField] private float beamHeight = 2.7f;
    [Min(0.01f)] [SerializeField] private float beamWidth = 0.055f;
    [Min(0.02f)] [SerializeField] private float beamHaloWidth = 0.24f;
    [Min(0.1f)] [SerializeField] private float groundGlowScale = 1.05f;
    [Min(0f)] [SerializeField] private float pointLightIntensity = 0.8f;
    [Min(0.1f)] [SerializeField] private float pointLightRange = 1.65f;

    private readonly Dictionary<CommonClass, Material> glowMaterials =
        new Dictionary<CommonClass, Material>();

    private Material particleMaterial;
    private Texture2D particleTexture;

    /// <summary>
    /// 씬에 배치된 관리자를 우선 사용하며, 없으면 기본 설정 관리자를 자동 생성합니다.
    /// </summary>
    public static WorldItemRarityVfxManager Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<WorldItemRarityVfxManager>(FindObjectsInactive.Include);
            if (instance != null)
            {
                return instance;
            }

            GameObject managerObject = new GameObject(nameof(WorldItemRarityVfxManager));
            instance = managerObject.AddComponent<WorldItemRarityVfxManager>();
            DontDestroyOnLoad(managerObject);
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    /// <summary>
    /// 지정한 월드 아이템 아래에 등급 VFX를 생성하거나 기존 VFX를 갱신합니다.
    /// </summary>
    public Transform ApplyTo(
        Transform itemRoot,
        CommonClass itemClass,
        float visualSize,
        float groundClearance)
    {
        if (itemRoot == null)
        {
            return null;
        }

        RarityStyle style = GetStyle(itemClass);
        Transform visualRoot = GetOrCreateChild(itemRoot, VisualRootName);
        visualRoot.gameObject.SetActive(true);
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one * Mathf.Max(0.01f, visualSize);
        visualRoot.localPosition = Vector3.up * (Mathf.Max(0f, groundClearance) + 0.015f);

        SetChildActive(visualRoot, CoreName, false);
        SetChildActive(visualRoot, GroundRingName, false);
        SetChildActive(visualRoot, SecondaryRingName, false);

        ConfigureGroundGlow(visualRoot, itemClass, style);
        ConfigureBeam(visualRoot, BeamHaloName, itemClass, style, true, Vector3.zero, 1f);
        ConfigureBeam(visualRoot, BeamCoreName, itemClass, style, false, Vector3.zero, 1f);
        ConfigureBeam(
            visualRoot,
            SecondaryBeamName,
            itemClass,
            style,
            false,
            new Vector3(style.ringRadius * 0.34f, 0f, 0f),
            0.62f,
            style.useSecondaryRing);
        ConfigureRarityLight(visualRoot, style);
        ConfigureParticles(visualRoot, style);

        return visualRoot;
    }

    /// <summary>해당 월드 아이템에 이미 만들어진 등급 VFX를 숨깁니다.</summary>
    public void Hide(Transform itemRoot)
    {
        if (itemRoot == null)
        {
            return;
        }

        Transform visualRoot = itemRoot.Find(VisualRootName);
        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(false);
        }
    }

    private void ConfigureGroundGlow(
        Transform visualRoot,
        CommonClass itemClass,
        RarityStyle style)
    {
        Transform glow = visualRoot.Find(GroundGlowName);
        MeshRenderer renderer;

        if (glow == null)
        {
            GameObject glowObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glowObject.name = GroundGlowName;
            glow = glowObject.transform;
            glow.SetParent(visualRoot, false);

            Collider generatedCollider = glowObject.GetComponent<Collider>();
            if (generatedCollider != null)
            {
                generatedCollider.enabled = false;
                Destroy(generatedCollider);
            }

            renderer = glowObject.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        else
        {
            renderer = glow.GetComponent<MeshRenderer>();
        }

        float diameter = Mathf.Max(0.1f, style.ringRadius * 2f * groundGlowScale);
        glow.localPosition = Vector3.up * 0.018f;
        glow.localRotation = Quaternion.Euler(90f, 0f, 0f);
        glow.localScale = new Vector3(diameter, diameter, 1f);
        glow.gameObject.SetActive(true);

        if (renderer != null)
        {
            renderer.sharedMaterial = GetGlowMaterial(itemClass, style);
        }
    }

    private void ConfigureBeam(
        Transform visualRoot,
        string beamName,
        CommonClass itemClass,
        RarityStyle style,
        bool halo,
        Vector3 offset,
        float heightMultiplier,
        bool visible = true)
    {
        Transform beamTransform = GetOrCreateChild(visualRoot, beamName);
        beamTransform.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        LineRenderer line = beamTransform.GetComponent<LineRenderer>();
        if (line == null)
        {
            line = beamTransform.gameObject.AddComponent<LineRenderer>();
        }

        float rarityFactor = Mathf.InverseLerp(1.8f, 4.3f, style.emissionIntensity);
        float height = beamHeight * Mathf.Lerp(0.88f, 1.18f, rarityFactor) * heightMultiplier;
        float width = halo
            ? beamHaloWidth * Mathf.Lerp(0.9f, 1.18f, rarityFactor)
            : Mathf.Max(beamWidth, style.ringWidth * 2.2f);

        beamTransform.localPosition = offset;
        beamTransform.localRotation = Quaternion.identity;
        beamTransform.localScale = Vector3.one;

        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = 2;
        line.SetPosition(0, new Vector3(0f, 0.025f, 0f));
        line.SetPosition(1, new Vector3(0f, height, 0f));
        line.widthMultiplier = width;
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.08f),
            new Keyframe(0.06f, 1f),
            new Keyframe(0.62f, halo ? 0.54f : 0.72f),
            new Keyframe(1f, 0f));
        line.colorGradient = CreateBeamGradient(halo ? 0.2f : 0.72f);
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        line.sharedMaterial = GetGlowMaterial(itemClass, style);
    }

    private void ConfigureRarityLight(Transform visualRoot, RarityStyle style)
    {
        Transform lightTransform = GetOrCreateChild(visualRoot, RarityLightName);
        Light rarityLight = lightTransform.GetComponent<Light>();
        if (rarityLight == null)
        {
            rarityLight = lightTransform.gameObject.AddComponent<Light>();
        }

        float rarityFactor = Mathf.InverseLerp(1.8f, 4.3f, style.emissionIntensity);
        lightTransform.localPosition = Vector3.up * 0.18f;
        lightTransform.localRotation = Quaternion.identity;
        lightTransform.localScale = Vector3.one;

        rarityLight.type = LightType.Point;
        rarityLight.color = new Color(
            Mathf.Clamp01(style.color.r),
            Mathf.Clamp01(style.color.g),
            Mathf.Clamp01(style.color.b),
            1f);
        rarityLight.intensity = pointLightIntensity * Mathf.Lerp(0.62f, 1.28f, rarityFactor);
        rarityLight.range = pointLightRange * Mathf.Lerp(0.86f, 1.2f, rarityFactor);
        rarityLight.shadows = LightShadows.None;
        rarityLight.bounceIntensity = 0f;
        rarityLight.renderMode = LightRenderMode.Auto;
        rarityLight.gameObject.SetActive(pointLightIntensity > 0f);
    }

    private void ConfigureCore(Transform visualRoot, CommonClass itemClass, RarityStyle style)
    {
        Transform core = visualRoot.Find(CoreName);
        MeshRenderer renderer;

        if (core == null)
        {
            GameObject coreObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coreObject.name = CoreName;
            core = coreObject.transform;
            core.SetParent(visualRoot, false);

            Collider generatedCollider = coreObject.GetComponent<Collider>();
            if (generatedCollider != null)
            {
                generatedCollider.enabled = false;
                Destroy(generatedCollider);
            }

            renderer = coreObject.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        else
        {
            renderer = core.GetComponent<MeshRenderer>();
        }

        core.localPosition = Vector3.zero;
        core.localRotation = Quaternion.identity;
        core.localScale = Vector3.one * coreScale;

        if (renderer != null)
        {
            renderer.sharedMaterial = GetGlowMaterial(itemClass, style);
        }
    }

    private void ConfigureRing(
        Transform visualRoot,
        string ringName,
        RarityStyle style,
        Quaternion localRotation,
        float radiusMultiplier,
        bool visible)
    {
        Transform ringTransform = GetOrCreateChild(visualRoot, ringName);
        ringTransform.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        LineRenderer line = ringTransform.GetComponent<LineRenderer>();
        if (line == null)
        {
            line = ringTransform.gameObject.AddComponent<LineRenderer>();
        }

        ringTransform.localPosition = Vector3.down * 0.28f;
        ringTransform.localRotation = localRotation;
        ringTransform.localScale = Vector3.one;

        const int segmentCount = 48;
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segmentCount;
        line.widthMultiplier = style.ringWidth;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        line.sharedMaterial = GetGlowMaterial(style.itemClass, style);

        float radius = style.ringRadius * radiusMultiplier;
        for (int i = 0; i < segmentCount; i++)
        {
            float angle = i * Mathf.PI * 2f / segmentCount;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    private void ConfigureParticles(Transform visualRoot, RarityStyle style)
    {
        Transform particleTransform = GetOrCreateChild(visualRoot, SparkName);
        ParticleSystem particles = particleTransform.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = particleTransform.gameObject.AddComponent<ParticleSystem>();
        }

        particleTransform.localPosition = Vector3.up * 0.08f;
        particleTransform.localRotation = Quaternion.identity;
        particleTransform.localScale = Vector3.one;

        Color particleColor = MultiplyRgb(style.color, Mathf.Max(1f, style.emissionIntensity * 0.72f));
        particleColor.a = 0.62f;

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = Mathf.Max(6, Mathf.CeilToInt(style.sparkRate * 2f));
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.025f);
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(style.sparkSize * 0.38f, style.sparkSize * 0.75f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(style.sparkSize * 5.5f, style.sparkSize * 11f);
        main.startSizeZ = new ParticleSystem.MinMaxCurve(style.sparkSize * 0.38f, style.sparkSize * 0.75f);
        main.startColor = particleColor;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = style.sparkRate > 0f;
        emission.rateOverTime = Mathf.Max(1.5f, style.sparkRate * 0.45f);

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = particleRadius * 0.72f;
        shape.radiusThickness = 0.82f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = Mathf.Max(0.06f, particleRiseSpeed);

        Gradient alphaGradient = new Gradient();
        alphaGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.45f, 0.12f),
                new GradientAlphaKey(0.28f, 0.62f),
                new GradientAlphaKey(0f, 1f)
            });

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = alphaGradient;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.sharedMaterial = GetParticleMaterial();

        particles.Clear(true);
        particles.Play(true);
    }

    private Material GetGlowMaterial(CommonClass itemClass, RarityStyle style)
    {
        if (!glowMaterials.TryGetValue(itemClass, out Material material) || material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            material = new Material(shader)
            {
                name = $"World Item Glow - {itemClass}",
                hideFlags = HideFlags.HideAndDontSave
            };

            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            Texture2D softTexture = GetParticleTexture();
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", softTexture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", softTexture);
            }

            glowMaterials[itemClass] = material;
        }

        Color hdrColor = MultiplyRgb(style.color, style.emissionIntensity * 0.72f);
        hdrColor.a = 0.82f;
        SetMaterialColor(material, hdrColor);
        return material;
    }

    private Material GetParticleMaterial()
    {
        if (particleMaterial != null)
        {
            return particleMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        particleMaterial = new Material(shader)
        {
            name = "World Item Spark Material",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Transparent
        };

        particleMaterial.SetFloat("_Surface", 1f);
        particleMaterial.SetFloat("_Blend", 1f);
        particleMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        particleMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
        particleMaterial.SetFloat("_ZWrite", 0f);
        particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        Texture2D softParticleTexture = GetParticleTexture();
        if (particleMaterial.HasProperty("_BaseMap"))
        {
            particleMaterial.SetTexture("_BaseMap", softParticleTexture);
        }

        if (particleMaterial.HasProperty("_MainTex"))
        {
            particleMaterial.SetTexture("_MainTex", softParticleTexture);
        }

        SetMaterialColor(particleMaterial, Color.white);
        return particleMaterial;
    }

    private Texture2D GetParticleTexture()
    {
        if (particleTexture != null)
        {
            return particleTexture;
        }

        const int size = 32;
        particleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "World Item Soft Spark",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalizedX = (x + 0.5f) / size * 2f - 1f;
                float normalizedY = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.4f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        particleTexture.SetPixels(pixels);
        particleTexture.Apply(false, true);
        return particleTexture;
    }

    private RarityStyle GetStyle(CommonClass itemClass)
    {
        if (rarityStyles != null)
        {
            for (int i = 0; i < rarityStyles.Length; i++)
            {
                RarityStyle style = rarityStyles[i];
                if (style != null && style.itemClass == itemClass)
                {
                    return style;
                }
            }
        }

        RarityStyle[] defaults = CreateDefaultStyles();
        for (int i = 0; i < defaults.Length; i++)
        {
            if (defaults[i].itemClass == itemClass)
            {
                return defaults[i];
            }
        }

        return defaults[0];
    }

    private static Transform GetOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(childName);
        child = childObject.transform;
        child.SetParent(parent, false);
        return child;
    }

    private static void SetChildActive(Transform parent, string childName, bool active)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            child.gameObject.SetActive(active);
        }
    }

    private static Gradient CreateBeamGradient(float peakAlpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(peakAlpha, 0.08f),
                new GradientAlphaKey(peakAlpha * 0.58f, 0.58f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static Color MultiplyRgb(Color color, float multiplier)
    {
        return new Color(
            color.r * multiplier,
            color.g * multiplier,
            color.b * multiplier,
            color.a);
    }

    private static RarityStyle[] CreateDefaultStyles()
    {
        return new[]
        {
            new RarityStyle
            {
                itemClass = CommonClass.Rare,
                color = new Color(0.18f, 0.55f, 1f, 1f),
                emissionIntensity = 1.8f,
                sparkRate = 4f,
                sparkSize = 0.035f,
                ringRadius = 0.34f,
                ringWidth = 0.014f
            },
            new RarityStyle
            {
                itemClass = CommonClass.Epic,
                color = new Color(0.68f, 0.25f, 1f, 1f),
                emissionIntensity = 2.3f,
                sparkRate = 7f,
                sparkSize = 0.04f,
                ringRadius = 0.36f,
                ringWidth = 0.017f
            },
            new RarityStyle
            {
                itemClass = CommonClass.Unique,
                color = new Color(0.934f, 1f, 0f, 1f),
                emissionIntensity = 2.8f,
                sparkRate = 10f,
                sparkSize = 0.045f,
                ringRadius = 0.38f,
                ringWidth = 0.02f,
                useSecondaryRing = true
            },
            new RarityStyle
            {
                itemClass = CommonClass.Legendary,
                color = new Color(1f, 0.52f, 0.06f, 1f),
                emissionIntensity = 3.5f,
                sparkRate = 14f,
                sparkSize = 0.052f,
                ringRadius = 0.41f,
                ringWidth = 0.024f,
                useSecondaryRing = true
            },
            new RarityStyle
            {
                itemClass = CommonClass.Myth,
                color = new Color(1f, 0.08f, 0.32f, 1f),
                emissionIntensity = 4.3f,
                sparkRate = 20f,
                sparkSize = 0.06f,
                ringRadius = 0.45f,
                ringWidth = 0.03f,
                useSecondaryRing = true
            }
        };
    }

    private void OnValidate()
    {
        coreScale = Mathf.Clamp(coreScale, 0.05f, 0.6f);
        particleRadius = Mathf.Clamp(particleRadius, 0.05f, 1f);
        particleRiseSpeed = Mathf.Clamp01(particleRiseSpeed);
        beamHeight = Mathf.Max(0.2f, beamHeight);
        beamWidth = Mathf.Max(0.01f, beamWidth);
        beamHaloWidth = Mathf.Max(beamWidth, beamHaloWidth);
        groundGlowScale = Mathf.Max(0.1f, groundGlowScale);
        pointLightIntensity = Mathf.Max(0f, pointLightIntensity);
        pointLightRange = Mathf.Max(0.1f, pointLightRange);

        if (rarityStyles == null || rarityStyles.Length == 0)
        {
            rarityStyles = CreateDefaultStyles();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        foreach (Material material in glowMaterials.Values)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        glowMaterials.Clear();

        if (particleMaterial != null)
        {
            Destroy(particleMaterial);
        }

        if (particleTexture != null)
        {
            Destroy(particleTexture);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }
}
