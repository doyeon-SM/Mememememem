using KMS.InventoryDuped;
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
    private Collider[] colliders;
    private bool[] colliderStates;
    private Renderer[] visualRenderers;
    private Color[] rendererBaseColors;
    private ParticleSystem[] particleSystems;
    private Color[] particleBaseColors;
    private float[] particleEmissionRates;
    private Light[] visualLights;
    private float[] lightBaseIntensities;
    private float[] lightBaseRanges;
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
        for (int i = 0; i < colliders.Length; i++)
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
        bool fullyCollected = worldItem.TryCollect(inventory);
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
        colliders = GetComponentsInChildren<Collider>(true);
        colliderStates = new bool[colliders.Length];
    }

    private void RestoreColliders()
    {
        if (colliders == null || colliderStates == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = colliderStates[i];
            }
        }
    }

    private void CacheVisuals()
    {
        visualRenderers = GetComponentsInChildren<Renderer>(true);
        rendererBaseColors = new Color[visualRenderers.Length];
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            rendererBaseColors[i] = GetRendererColor(visualRenderers[i]);
        }

        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        particleBaseColors = new Color[particleSystems.Length];
        particleEmissionRates = new float[particleSystems.Length];
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem.MainModule main = particleSystems[i].main;
            ParticleSystem.EmissionModule emission = particleSystems[i].emission;
            particleBaseColors[i] = main.startColor.color;
            particleEmissionRates[i] = emission.rateOverTimeMultiplier;
        }

        visualLights = GetComponentsInChildren<Light>(true);
        lightBaseIntensities = new float[visualLights.Length];
        lightBaseRanges = new float[visualLights.Length];
        for (int i = 0; i < visualLights.Length; i++)
        {
            Light visualLight = visualLights[i];
            if (visualLight == null)
            {
                continue;
            }

            lightBaseIntensities[i] = visualLight.intensity;
            lightBaseRanges[i] = visualLight.range;
        }
    }

    private void SetVisualStrength(float strength)
    {
        strength = Mathf.Clamp01(strength);
        transform.localScale = pickupStartScale * Mathf.Lerp(endVisualScale, 1f, strength);

        if (visualRenderers != null)
        {
            for (int i = 0; i < visualRenderers.Length; i++)
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
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", fadedColor);
                block.SetColor("_Color", fadedColor);
                renderer.SetPropertyBlock(block);
            }
        }

        if (particleSystems == null)
        {
            FadeLights(strength);
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
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

    private void FadeLights(float strength)
    {
        if (visualLights == null || lightBaseIntensities == null || lightBaseRanges == null)
        {
            return;
        }

        float lightStrength = strength * strength;
        for (int i = 0; i < visualLights.Length; i++)
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

        if (visualRenderers != null)
        {
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                if (visualRenderers[i] != null)
                {
                    visualRenderers[i].SetPropertyBlock(null);
                }
            }
        }

        if (particleSystems != null)
        {
            for (int i = 0; i < particleSystems.Length; i++)
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

        if (visualLights != null && lightBaseIntensities != null && lightBaseRanges != null)
        {
            for (int i = 0; i < visualLights.Length; i++)
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
