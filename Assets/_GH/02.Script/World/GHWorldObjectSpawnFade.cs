using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Fades every renderer and material slot with the same quantized progress.
/// Temporary transparent variants keep LODs, bark, leaves, and vegetation
/// shaders synchronized without relying on externally managed property blocks.
/// </summary>
internal sealed class GHWorldObjectSpawnFade
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
    private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int CullId = Shader.PropertyToID("_Cull");
    private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
    private static readonly int BlendId = Shader.PropertyToID("_Blend");
    private static readonly int ModeId = Shader.PropertyToID("_Mode");
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int SrcBlendAlphaId = Shader.PropertyToID("_SrcBlendAlpha");
    private static readonly int DstBlendAlphaId = Shader.PropertyToID("_DstBlendAlpha");
    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

    private static readonly Dictionary<FadeMaterialKey, Material> FadeMaterialCache =
        new Dictionary<FadeMaterialKey, Material>();

    private readonly Renderer[] renderers;
    private RendererFadeState[] cachedStates;
    private bool isActive;
    private int lastFadeStep = -1;

    public bool IsActive => isActive;

    public GHWorldObjectSpawnFade(Renderer[] renderers)
    {
        this.renderers = renderers;
    }

    public bool Begin(int fadeSteps)
    {
        Restore();

        if (cachedStates == null)
        {
            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            List<RendererFadeState> states = new List<RendererFadeState>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                RendererFadeState state = RendererFadeState.TryCreate(renderer);
                if (state != null)
                {
                    states.Add(state);
                }
            }

            cachedStates = states.ToArray();
        }
        if (cachedStates != null)
        {
            for (int i = 0; i < cachedStates.Length; i++)
            {
                cachedStates[i].Activate();
            }
        }

        isActive = cachedStates.Length > 0;
        lastFadeStep = -1;
        Apply(0f, fadeSteps, true);
        return isActive;
    }

    public void Apply(float alpha, int fadeSteps, bool force = false)
    {
        if (!isActive || cachedStates == null)
        {
            return;
        }

        int steps = Mathf.Clamp(fadeSteps, 8, 60);
        int step = Mathf.RoundToInt(Mathf.Clamp01(alpha) * steps);
        if (!force && step == lastFadeStep)
        {
            return;
        }

        lastFadeStep = step;
        for (int i = 0; i < cachedStates.Length; i++)
        {
            cachedStates[i].ApplyStep(step, steps);
        }
    }

    public void Restore()
    {
        if (!isActive || cachedStates == null)
        {
            return;
        }

        for (int i = 0; i < cachedStates.Length; i++)
        {
            cachedStates[i].Restore();
        }

        isActive = false;
        lastFadeStep = -1;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetMaterialCache()
    {
        foreach (Material material in FadeMaterialCache.Values)
        {
            if (material == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(material);
            }
            else
            {
                Object.DestroyImmediate(material);
            }
        }

        FadeMaterialCache.Clear();
    }

    private static Material GetFadeMaterial(Material source, int fadeStep, int fadeSteps)
    {
        if (source == null)
        {
            return null;
        }

        int steps = Mathf.Clamp(fadeSteps, 8, 60);
        int step = Mathf.Clamp(fadeStep, 0, steps);
        FadeMaterialKey key = new FadeMaterialKey(source, step, steps);
        if (FadeMaterialCache.TryGetValue(key, out Material cached)
            && cached != null)
        {
            return cached;
        }

        Material fadeMaterial = CreateFadeMaterial(source);
        if (fadeMaterial == null)
        {
            return null;
        }

        fadeMaterial.name = $"{source.name} (GH Spawn Fade {step}/{steps})";
        fadeMaterial.hideFlags = HideFlags.HideAndDontSave;

        ConfigureTransparentFadeMaterial(fadeMaterial);
        int colorPropertyId = ResolveColorProperty(fadeMaterial);
        if (colorPropertyId < 0)
        {
            Object.Destroy(fadeMaterial);
            return null;
        }

        Color fadedColor = fadeMaterial.GetColor(colorPropertyId);
        fadedColor.a *= step / (float)steps;
        fadeMaterial.SetColor(colorPropertyId, fadedColor);

        FadeMaterialCache[key] = fadeMaterial;
        return fadeMaterial;
    }

    private static bool CanCreateFadeMaterial(Material source)
    {
        if (source == null)
        {
            return false;
        }

        if (!RequiresCompatibleFadeShader(source))
        {
            return ResolveColorProperty(source) >= 0;
        }

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        return litShader != null || ResolveColorProperty(source) >= 0;
    }

    private static Material CreateFadeMaterial(Material source)
    {
        if (!RequiresCompatibleFadeShader(source))
        {
            return new Material(source);
        }

        // Idyllic Fantasy Nature's Surface/Vegetation Shader Graphs expose the
        // usual URP material properties, but their fragment Alpha output does
        // not use _BaseColor.a. Reusing those shaders therefore changes the
        // property value without changing the pixels. During the short spawn
        // fade, render an equivalent URP/Lit material and restore the exact
        // original material when the tween completes.
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null)
        {
            return new Material(source);
        }

        Material compatible = new Material(litShader);
        CopyCompatibleSurfaceProperties(source, compatible);
        return compatible;
    }

    private static bool RequiresCompatibleFadeShader(Material source)
    {
        string shaderName = source != null && source.shader != null
            ? source.shader.name
            : string.Empty;

        bool isNamedIdyllicShader = shaderName.IndexOf(
                                        "Idyllic Fantasy Nature/",
                                        System.StringComparison.Ordinal)
                                    >= 0
                                    && (shaderName.EndsWith(
                                            "/Vegetation",
                                            System.StringComparison.Ordinal)
                                        || shaderName.EndsWith(
                                            "/Surface",
                                            System.StringComparison.Ordinal));

        // Keep the fallback working even if Shader Graph changes the generated
        // shader category/name while retaining the same material interface.
        bool hasVegetationSignature = source != null
                                      && source.HasProperty("_Texture")
                                      && source.HasProperty("_Alpha_Cutoff")
                                      && source.HasProperty("_Wind_Strength");
        bool hasSurfaceSignature = source != null
                                   && source.HasProperty("_Base_Map")
                                   && source.HasProperty("_Coverage_Base_Map")
                                   && source.HasProperty("_Fade");

        return isNamedIdyllicShader
               || hasVegetationSignature
               || hasSurfaceSignature;
    }

    private static void CopyCompatibleSurfaceProperties(
        Material source,
        Material destination)
    {
        int sourceColorId = ResolveColorProperty(source);
        if (sourceColorId >= 0 && destination.HasProperty(BaseColorId))
        {
            destination.SetColor(BaseColorId, source.GetColor(sourceColorId));
        }

        CopyFirstTexture(
            source,
            destination,
            BaseMapId,
            "_Base_Map",
            "_BaseMap",
            "_MainTex",
            "_Texture");
        CopyFirstTexture(
            source,
            destination,
            BumpMapId,
            "_Normal_Map",
            "_BumpMap");

        if (destination.GetTexture(BumpMapId) != null)
        {
            destination.EnableKeyword("_NORMALMAP");
        }

        CopyFloat(source, destination, BumpScaleId, "_Normal_Strength", "_BumpScale");
        CopyFloat(source, destination, MetallicId, "_Metallic");
        CopyFloat(source, destination, SmoothnessId, "_Smoothness");
        CopyFloat(source, destination, CullId, "_Cull");
    }

    private static void CopyFirstTexture(
        Material source,
        Material destination,
        int destinationPropertyId,
        params string[] sourcePropertyNames)
    {
        if (!destination.HasProperty(destinationPropertyId))
        {
            return;
        }

        for (int i = 0; i < sourcePropertyNames.Length; i++)
        {
            string sourcePropertyName = sourcePropertyNames[i];
            if (!source.HasProperty(sourcePropertyName))
            {
                continue;
            }

            Texture texture = source.GetTexture(sourcePropertyName);
            if (texture == null)
            {
                continue;
            }

            destination.SetTexture(destinationPropertyId, texture);
            destination.SetTextureScale(
                destinationPropertyId,
                source.GetTextureScale(sourcePropertyName));
            destination.SetTextureOffset(
                destinationPropertyId,
                source.GetTextureOffset(sourcePropertyName));
            return;
        }
    }

    private static void CopyFloat(
        Material source,
        Material destination,
        int destinationPropertyId,
        params string[] sourcePropertyNames)
    {
        if (!destination.HasProperty(destinationPropertyId))
        {
            return;
        }

        for (int i = 0; i < sourcePropertyNames.Length; i++)
        {
            string sourcePropertyName = sourcePropertyNames[i];
            if (!source.HasProperty(sourcePropertyName))
            {
                continue;
            }

            destination.SetFloat(
                destinationPropertyId,
                source.GetFloat(sourcePropertyName));
            return;
        }
    }

    private static void ConfigureTransparentFadeMaterial(Material fadeMaterial)
    {
        // Alpha clipping fights a smooth opacity fade: once the multiplied
        // texture alpha drops below the cutoff, foliage disappears in a hard
        // pop. Transparent blending already preserves the leaf texture alpha.
        if (fadeMaterial.HasProperty(AlphaClipId))
        {
            fadeMaterial.SetFloat(AlphaClipId, 0f);
        }

        fadeMaterial.DisableKeyword("_ALPHATEST_ON");

        if (fadeMaterial.HasProperty(SurfaceId))
        {
            fadeMaterial.SetFloat(SurfaceId, 1f);
        }

        if (fadeMaterial.HasProperty(BlendId))
        {
            fadeMaterial.SetFloat(BlendId, 0f);
        }

        if (fadeMaterial.HasProperty(ModeId))
        {
            fadeMaterial.SetFloat(ModeId, 2f);
        }

        if (fadeMaterial.HasProperty(SrcBlendId))
        {
            fadeMaterial.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
        }

        if (fadeMaterial.HasProperty(DstBlendId))
        {
            fadeMaterial.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
        }

        if (fadeMaterial.HasProperty(SrcBlendAlphaId))
        {
            fadeMaterial.SetFloat(SrcBlendAlphaId, (float)BlendMode.One);
        }

        if (fadeMaterial.HasProperty(DstBlendAlphaId))
        {
            fadeMaterial.SetFloat(DstBlendAlphaId, (float)BlendMode.OneMinusSrcAlpha);
        }

        if (fadeMaterial.HasProperty(ZWriteId))
        {
            fadeMaterial.SetFloat(ZWriteId, 0f);
        }

        fadeMaterial.SetOverrideTag("RenderType", "Transparent");
        fadeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        fadeMaterial.EnableKeyword("_ALPHABLEND_ON");
        fadeMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        fadeMaterial.renderQueue = Mathf.Max(
            fadeMaterial.renderQueue,
            (int)RenderQueue.Transparent);
        fadeMaterial.SetShaderPassEnabled("ShadowCaster", false);
    }

    private static int ResolveColorProperty(Material material)
    {
        if (material == null)
        {
            return -1;
        }

        if (material.HasProperty(BaseColorId))
        {
            return BaseColorId;
        }

        if (material.HasProperty(ColorId))
        {
            return ColorId;
        }

        return material.HasProperty(TintColorId) ? TintColorId : -1;
    }

    private sealed class RendererFadeState
    {
        private readonly Renderer renderer;
        private readonly Material[] originalMaterials;
        private readonly Material[] workingMaterials;
        private readonly MaterialPropertyBlock originalRendererBlock;
        private readonly MaterialPropertyBlock[] originalBlocks;
        private bool propertyBlocksCaptured;

        private RendererFadeState(
            Renderer renderer,
            Material[] originalMaterials)
        {
            this.renderer = renderer;
            this.originalMaterials = originalMaterials;
            workingMaterials = new Material[originalMaterials.Length];
            originalRendererBlock = new MaterialPropertyBlock();
            originalBlocks = new MaterialPropertyBlock[originalMaterials.Length];
        }

        public static RendererFadeState TryCreate(Renderer renderer)
        {
            Material[] originals = renderer.sharedMaterials;
            if (originals == null || originals.Length == 0)
            {
                return null;
            }

            bool hasFadableSlot = false;

            for (int i = 0; i < originals.Length; i++)
            {
                hasFadableSlot |= CanCreateFadeMaterial(originals[i]);
            }

            if (!hasFadableSlot)
            {
                return null;
            }

            return new RendererFadeState(renderer, originals);
        }

        public void Activate()
        {
            if (renderer == null)
            {
                return;
            }

            // LOD renderers and vegetation systems may write their own property blocks.
            // Capture them on every spawn, then clear them temporarily so a stale color
            // override cannot force bark/leaves/bushes back to alpha 1 during the fade.
            originalRendererBlock.Clear();
            renderer.GetPropertyBlock(originalRendererBlock);
            renderer.SetPropertyBlock(null);

            for (int i = 0; i < originalBlocks.Length; i++)
            {
                MaterialPropertyBlock block = originalBlocks[i]
                    ?? new MaterialPropertyBlock();
                block.Clear();
                renderer.GetPropertyBlock(block, i);
                originalBlocks[i] = block;
                renderer.SetPropertyBlock(null, i);
            }

            propertyBlocksCaptured = true;
        }

        public void ApplyStep(int fadeStep, int fadeSteps)
        {
            if (renderer == null)
            {
                return;
            }

            for (int i = 0; i < originalMaterials.Length; i++)
            {
                Material original = originalMaterials[i];
                workingMaterials[i] = GetFadeMaterial(original, fadeStep, fadeSteps)
                                      ?? original;
            }

            // Alpha is stored in the temporary material itself instead of a property
            // block. This keeps every LOD and every material slot (tree bark + leaves)
            // on the exact same quantized fade step.
            renderer.sharedMaterials = workingMaterials;
        }

        public void Restore()
        {
            if (renderer == null)
            {
                return;
            }

            renderer.sharedMaterials = originalMaterials;
            if (!propertyBlocksCaptured)
            {
                return;
            }

            renderer.SetPropertyBlock(originalRendererBlock);
            for (int i = 0; i < originalBlocks.Length; i++)
            {
                renderer.SetPropertyBlock(originalBlocks[i], i);
            }

            propertyBlocksCaptured = false;
        }
    }

    private readonly struct FadeMaterialKey : System.IEquatable<FadeMaterialKey>
    {
        private readonly Material source;
        private readonly int fadeStep;
        private readonly int fadeSteps;

        public FadeMaterialKey(Material source, int fadeStep, int fadeSteps)
        {
            this.source = source;
            this.fadeStep = fadeStep;
            this.fadeSteps = fadeSteps;
        }

        public bool Equals(FadeMaterialKey other)
        {
            return source == other.source
                   && fadeStep == other.fadeStep
                   && fadeSteps == other.fadeSteps;
        }

        public override bool Equals(object obj)
        {
            return obj is FadeMaterialKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = source != null ? source.GetInstanceID() : 0;
                hash = (hash * 397) ^ fadeStep;
                hash = (hash * 397) ^ fadeSteps;
                return hash;
            }
        }
    }
}
