using KMS.InventoryDuped;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WorldItem))]
[AddComponentMenu("GH/World/World Item Pickup Attractor")]
public sealed class WorldItemPickupAttractor : MonoBehaviour
{
    [Header("Detection")]
    [Min(0.1f)] [SerializeField] private float attractionRadius = 2.5f;
    [Min(0.02f)] [SerializeField] private float playerSearchInterval = 0.15f;
    [Min(0f)] [SerializeField] private float retryDelayWhenInventoryIsFull = 0.8f;

    [Header("Flight To Player")]
    [Min(0.1f)] [SerializeField] private float initialSpeed = 4f;
    [Min(0f)] [SerializeField] private float acceleration = 18f;
    [Min(0.1f)] [SerializeField] private float maxSpeed = 18f;
    [Min(0.01f)] [SerializeField] private float arrivalDistance = 0.16f;
    [SerializeField] private float playerTargetHeight = 1f;

    [Header("Visual Fade")]
    [Range(0f, 0.95f)] [SerializeField] private float fadeStartProgress = 0.2f;
    [Range(0f, 1f)] [SerializeField] private float endVisualScale = 0.15f;

    private WorldItem worldItem;
    private WorldItemDropMotion dropMotion;
    private PlayerInventory targetInventory;
    private readonly List<Collider> colliders = new List<Collider>(4);
    private readonly List<bool> colliderStates = new List<bool>(4);
    private readonly List<Renderer> visualRenderers = new List<Renderer>(8);
    private readonly List<Color> rendererBaseColors = new List<Color>(8);
    private readonly List<ParticleSystem> particleSystems = new List<ParticleSystem>(4);
    private readonly List<Color> particleBaseColors = new List<Color>(4);
    private readonly List<float> particleEmissionRates = new List<float>(4);
    private readonly List<Light> visualLights = new List<Light>(2);
    private readonly List<float> lightBaseIntensities = new List<float>(2);
    private readonly List<float> lightBaseRanges = new List<float>(2);
    // MaterialPropertyBlock은 UnityEngine.Object가 아닌 네이티브 리소스 래퍼라서
    // 에디터 핫 리로드/도메인 리로드 설정에 따라 기존 컴포넌트의 필드가 null로
    // 복원될 수 있습니다. 사용 시점에 다시 만들 수 있도록 readonly로 두지 않습니다.
    private MaterialPropertyBlock propertyBlock;
    private Vector3 pickupStartPosition;
    private Vector3 pickupStartScale;
    private float pickupStartDistance;
    private float currentSpeed;
    private float nextPlayerSearchTime;
    private float nextPickupAllowedTime;
    private bool isAttracting;

    public bool IsAttracting => isAttracting;

    public void Bind(WorldItem item)
    {
        worldItem = item != null ? item : GetComponent<WorldItem>();
        dropMotion = GetComponent<WorldItemDropMotion>();
    }

    public bool TryBegin(Collider playerCollider)
    {
        if (playerCollider == null || !PlayerReferenceResolver.IsInPlayerHierarchy(playerCollider.gameObject))
        {
            return false;
        }

        PlayerInventory inventory =
            PlayerReferenceResolver.FindComponentInPlayerHierarchy<PlayerInventory>(playerCollider.gameObject);
        return TryBegin(inventory);
    }

    private void Awake()
    {
        EnsurePropertyBlock();
        Bind(GetComponent<WorldItem>());
    }

    private void OnEnable()
    {
        isAttracting = false;
        targetInventory = null;
        nextPlayerSearchTime = 0f;
        nextPickupAllowedTime = 0f;
        pickupStartScale = transform.localScale;
        RestoreVisuals();
        RestoreColliders();
        CacheColliders();
        CacheVisuals();
    }

    private void Update()
    {
        if (worldItem == null)
        {
            Bind(GetComponent<WorldItem>());
        }

        if (isAttracting)
        {
            UpdateAttraction();
            return;
        }

        if (Time.time < nextPickupAllowedTime
            || worldItem == null
            || !worldItem.CanBePickedUp
            || (dropMotion != null && dropMotion.IsInFlight)
            || Time.time < nextPlayerSearchTime)
        {
            return;
        }

        nextPlayerSearchTime = Time.time + Mathf.Max(0.02f, playerSearchInterval);
        PlayerInventory inventory = PlayerReferenceResolver.FindPlayerComponent<PlayerInventory>();
        if (inventory == null)
        {
            return;
        }

        float radius = Mathf.Max(0.1f, attractionRadius);
        if ((GetTargetPosition(inventory) - transform.position).sqrMagnitude <= radius * radius)
        {
            TryBegin(inventory);
        }
    }

    private bool TryBegin(PlayerInventory inventory)
    {
        if (isAttracting
            || inventory == null
            || worldItem == null
            || !worldItem.CanBePickedUp
            || Time.time < nextPickupAllowedTime
            || (dropMotion != null && dropMotion.IsInFlight))
        {
            return false;
        }

        targetInventory = inventory;
        pickupStartPosition = transform.position;
        pickupStartScale = transform.localScale;
        pickupStartDistance = Mathf.Max(
            arrivalDistance,
            Vector3.Distance(transform.position, GetTargetPosition(inventory)));
        currentSpeed = Mathf.Max(0.1f, initialSpeed);
        isAttracting = true;

        CacheColliders();
        for (int i = 0; i < colliders.Count; i++)
        {
            if (colliders[i] != null)
            {
                colliderStates[i] = colliders[i].enabled;
                colliders[i].enabled = false;
            }
        }

        CacheVisuals();
        return true;
    }

    private void UpdateAttraction()
    {
        if (worldItem != null && worldItem.IsCollectionPending)
        {
            return;
        }

        if (targetInventory == null || worldItem == null || !worldItem.CanBePickedUp)
        {
            CancelAttraction(true);
            return;
        }

        Vector3 targetPosition = GetTargetPosition(targetInventory);
        float distance = Vector3.Distance(transform.position, targetPosition);
        if (distance <= Mathf.Max(0.01f, arrivalDistance))
        {
            CompletePickup();
            return;
        }

        currentSpeed = Mathf.Min(
            Mathf.Max(0.1f, maxSpeed),
            currentSpeed + Mathf.Max(0f, acceleration) * Time.deltaTime);
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            currentSpeed * Time.deltaTime);

        float progress = 1f - Mathf.Clamp01(distance / pickupStartDistance);
        float fadeProgress = Mathf.InverseLerp(
            Mathf.Clamp01(fadeStartProgress),
            1f,
            progress);
        SetVisualStrength(1f - fadeProgress);
    }

    private void CompletePickup()
    {
        PlayerInventory inventory = targetInventory;
        transform.position = GetTargetPosition(inventory);
        SetVisualStrength(0f);
        if (WorldItemPickupBatcher.Queue(worldItem, this, inventory))
        {
            return;
        }

        bool fullyCollected = worldItem.TryCollect(inventory);
        if (fullyCollected || !gameObject.activeInHierarchy)
        {
            return;
        }

        ResolveQueuedPickup(false);
    }

    internal void ResolveQueuedPickup(bool fullyCollected)
    {
        if (fullyCollected || !gameObject.activeInHierarchy)
        {
            return;
        }

        CancelAttraction(true);
        transform.position = pickupStartPosition;
        nextPickupAllowedTime = Time.time + Mathf.Max(0f, retryDelayWhenInventoryIsFull);
    }

    private void CancelAttraction(bool restoreVisuals)
    {
        isAttracting = false;
        targetInventory = null;
        currentSpeed = 0f;
        RestoreColliders();

        if (restoreVisuals)
        {
            RestoreVisuals();
        }
    }

    private Vector3 GetTargetPosition(PlayerInventory inventory)
    {
        return inventory.transform.position + Vector3.up * playerTargetHeight;
    }

    private void CacheColliders()
    {
        colliders.Clear();
        GetComponentsInChildren(true, colliders);
        colliderStates.Clear();
        for (int i = 0; i < colliders.Count; i++)
        {
            colliderStates.Add(colliders[i] != null && colliders[i].enabled);
        }
    }

    private void RestoreColliders()
    {
        if (colliderStates.Count != colliders.Count)
        {
            return;
        }

        for (int i = 0; i < colliders.Count; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = colliderStates[i];
            }
        }
    }

    private void CacheVisuals()
    {
        visualRenderers.Clear();
        GetComponentsInChildren(true, visualRenderers);
        rendererBaseColors.Clear();
        for (int i = 0; i < visualRenderers.Count; i++)
        {
            rendererBaseColors.Add(GetRendererColor(visualRenderers[i]));
        }

        particleSystems.Clear();
        GetComponentsInChildren(true, particleSystems);
        particleBaseColors.Clear();
        particleEmissionRates.Clear();
        for (int i = 0; i < particleSystems.Count; i++)
        {
            ParticleSystem.MainModule main = particleSystems[i].main;
            ParticleSystem.EmissionModule emission = particleSystems[i].emission;
            particleBaseColors.Add(main.startColor.color);
            particleEmissionRates.Add(emission.rateOverTimeMultiplier);
        }

        visualLights.Clear();
        GetComponentsInChildren(true, visualLights);
        lightBaseIntensities.Clear();
        lightBaseRanges.Clear();
        for (int i = 0; i < visualLights.Count; i++)
        {
            Light visualLight = visualLights[i];
            if (visualLight == null)
            {
                lightBaseIntensities.Add(0f);
                lightBaseRanges.Add(0f);
                continue;
            }

            lightBaseIntensities.Add(visualLight.intensity);
            lightBaseRanges.Add(visualLight.range);
        }
    }

    private void SetVisualStrength(float strength)
    {
        strength = Mathf.Clamp01(strength);
        transform.localScale = pickupStartScale * Mathf.Lerp(endVisualScale, 1f, strength);

        MaterialPropertyBlock block = EnsurePropertyBlock();

        if (visualRenderers.Count > 0)
        {
            for (int i = 0; i < visualRenderers.Count; i++)
            {
                Renderer renderer = visualRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Color baseColor = rendererBaseColors[i];
                Color fadedColor = new Color(
                    baseColor.r * strength,
                    baseColor.g * strength,
                    baseColor.b * strength,
                    baseColor.a * strength);
                block.Clear();
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", fadedColor);
                block.SetColor("_Color", fadedColor);
                renderer.SetPropertyBlock(block);
            }
        }

        if (particleSystems.Count == 0)
        {
            FadeLights(strength);
            return;
        }

        for (int i = 0; i < particleSystems.Count; i++)
        {
            ParticleSystem particles = particleSystems[i];
            if (particles == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particles.main;
            Color baseColor = particleBaseColors[i];
            main.startColor = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * strength);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTimeMultiplier = particleEmissionRates[i] * strength;
        }

        FadeLights(strength);
    }

    private MaterialPropertyBlock EnsurePropertyBlock()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        return propertyBlock;
    }

    private void FadeLights(float strength)
    {
        if (lightBaseIntensities.Count != visualLights.Count
            || lightBaseRanges.Count != visualLights.Count)
        {
            return;
        }

        float lightStrength = strength * strength;
        for (int i = 0; i < visualLights.Count; i++)
        {
            Light visualLight = visualLights[i];
            if (visualLight == null)
            {
                continue;
            }

            visualLight.intensity = lightBaseIntensities[i] * lightStrength;
            visualLight.range = lightBaseRanges[i] * Mathf.Lerp(0.35f, 1f, strength);
        }
    }

    private void RestoreVisuals()
    {
        transform.localScale = pickupStartScale == Vector3.zero ? Vector3.one : pickupStartScale;

        if (visualRenderers.Count > 0)
        {
            for (int i = 0; i < visualRenderers.Count; i++)
            {
                if (visualRenderers[i] != null)
                {
                    visualRenderers[i].SetPropertyBlock(null);
                }
            }
        }

        if (particleBaseColors.Count == particleSystems.Count
            && particleEmissionRates.Count == particleSystems.Count)
        {
            for (int i = 0; i < particleSystems.Count; i++)
            {
                ParticleSystem particles = particleSystems[i];
                if (particles == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = particles.main;
                main.startColor = particleBaseColors[i];

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTimeMultiplier = particleEmissionRates[i];
            }
        }

        if (lightBaseIntensities.Count == visualLights.Count
            && lightBaseRanges.Count == visualLights.Count)
        {
            for (int i = 0; i < visualLights.Count; i++)
            {
                Light visualLight = visualLights[i];
                if (visualLight == null)
                {
                    continue;
                }

                visualLight.intensity = lightBaseIntensities[i];
                visualLight.range = lightBaseRanges[i];
            }
        }
    }

    private static Color GetRendererColor(Renderer renderer)
    {
        if (renderer == null || renderer.sharedMaterial == null)
        {
            return Color.white;
        }

        Material material = renderer.sharedMaterial;
        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        return material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
    }

    private void OnDisable()
    {
        CancelAttraction(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        attractionRadius = Mathf.Max(0.1f, attractionRadius);
        playerSearchInterval = Mathf.Max(0.02f, playerSearchInterval);
        retryDelayWhenInventoryIsFull = Mathf.Max(0f, retryDelayWhenInventoryIsFull);
        initialSpeed = Mathf.Max(0.1f, initialSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        maxSpeed = Mathf.Max(initialSpeed, maxSpeed);
        arrivalDistance = Mathf.Max(0.01f, arrivalDistance);
        fadeStartProgress = Mathf.Clamp(fadeStartProgress, 0f, 0.95f);
        endVisualScale = Mathf.Clamp01(endVisualScale);
    }
#endif
}
