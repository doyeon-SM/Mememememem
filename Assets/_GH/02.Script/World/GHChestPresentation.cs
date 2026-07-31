using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[AddComponentMenu("GH/World/GH Chest Presentation")]
public sealed class GHChestPresentation : MonoBehaviour
{
    [Header("Chest Animation")]
    [SerializeField] private Animator chestAnimator;
    [SerializeField] private string closedStateName = "Base Layer.Animated PBR Chest _Idle";
    [SerializeField] private string openingStateName = "Base Layer.Animated PBR Chest _Opening_UnCommon";
    [Min(0.05f)] [SerializeField] private float openingSpeed = 1f;
    [Min(0.01f)] [SerializeField] private float fallbackOpeningDuration = 1.34f;

    [Header("Item Ejection")]
    [SerializeField] private Transform itemEjectPoint;
    [SerializeField] private Vector3 itemEjectLocalOffset = new Vector3(0f, 1.2f, 0.45f);
    [Min(0.01f)] [SerializeField] private float itemFlightDuration = 0.55f;
    [Min(0f)] [SerializeField] private float itemFlightArcHeight = 0.85f;
    [Min(0f)] [SerializeField] private float itemSpinSpeed = 320f;
    [Min(0f)] [SerializeField] private float itemStartJitterRadius = 0.12f;

    [Header("Chest Effect Scale")]
    [Tooltip("Particle System의 Hierarchy 스케일을 사용해 상자 부모 스케일을 실시간으로 반영합니다.")]
    [SerializeField] private bool scaleEffectsWithChest = true;
    [SerializeField] private Transform effectScaleRoot;
    [SerializeField] private Vector3 effectBaseLocalScale = Vector3.one;
    [Min(0.01f)] [SerializeField] private float effectScaleMultiplier = 1f;
    [SerializeField] private ParticleSystem[] scaledParticleSystems;

    [Header("Affected Components")]
    [Tooltip("비워 두면 자식 Collider를 자동으로 수집하고 열기 시작 시 비활성화합니다.")]
    [SerializeField] private Collider[] colliders;

    private Coroutine sequenceRoutine;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        ResolveReferences();
        ApplyEffectScaleSettings();
        SetClosedPose();
    }

    public void SetEffectScaleMultiplier(float multiplier)
    {
        effectScaleMultiplier = Mathf.Max(0.01f, multiplier);
        ApplyEffectScaleSettings();
    }

    public bool PlayOpenSequence(Action onOpeningFinished)
    {
        if (!isActiveAndEnabled || isPlaying)
        {
            return false;
        }

        sequenceRoutine = StartCoroutine(OpenSequence(onOpeningFinished));
        return true;
    }

    public WorldItemDropLaunchSettings CreateDropLaunchSettings()
    {
        Vector3 startPosition = itemEjectPoint != null
            ? itemEjectPoint.position
            : transform.TransformPoint(itemEjectLocalOffset);

        return new WorldItemDropLaunchSettings
        {
            enabled = true,
            startPosition = startPosition,
            duration = Mathf.Max(0.01f, itemFlightDuration),
            arcHeight = Mathf.Max(0f, itemFlightArcHeight),
            spinSpeed = Mathf.Max(0f, itemSpinSpeed),
            startJitterRadius = Mathf.Max(0f, itemStartJitterRadius)
        };
    }

    private IEnumerator OpenSequence(Action onOpeningFinished)
    {
        isPlaying = true;
        ResolveReferences();
        DisableColliders();

        float openingDuration = PlayOpeningAnimation();
        if (openingDuration > 0f)
        {
            yield return new WaitForSeconds(openingDuration);
        }

        if (chestAnimator != null)
        {
            chestAnimator.speed = 0f;
        }

        onOpeningFinished?.Invoke();

        isPlaying = false;
        sequenceRoutine = null;
    }

    private float PlayOpeningAnimation()
    {
        float speed = Mathf.Max(0.05f, openingSpeed);
        if (chestAnimator == null || chestAnimator.runtimeAnimatorController == null)
        {
            return fallbackOpeningDuration / speed;
        }

        int stateHash = Animator.StringToHash(openingStateName);
        if (!chestAnimator.HasState(0, stateHash))
        {
            Debug.LogWarning(
                $"[{name}] Animator에서 열기 상태 '{openingStateName}'를 찾지 못했습니다. 기본 시간으로 진행합니다.",
                this);
            return fallbackOpeningDuration / speed;
        }

        chestAnimator.speed = speed;
        chestAnimator.Play(stateHash, 0, 0f);
        chestAnimator.Update(0f);
        AnimatorStateInfo stateInfo = chestAnimator.GetCurrentAnimatorStateInfo(0);
        float clipDuration = stateInfo.length > 0.01f
            ? stateInfo.length
            : fallbackOpeningDuration;
        return clipDuration / speed;
    }

    private void SetClosedPose()
    {
        if (chestAnimator == null || chestAnimator.runtimeAnimatorController == null)
        {
            return;
        }

        int stateHash = Animator.StringToHash(closedStateName);
        if (!chestAnimator.HasState(0, stateHash))
        {
            return;
        }

        chestAnimator.Play(stateHash, 0, 0f);
        chestAnimator.Update(0f);
        chestAnimator.speed = 0f;
    }

    private void ResolveReferences()
    {
        if (chestAnimator == null)
        {
            chestAnimator = GetComponent<Animator>();
        }

        if (colliders == null || colliders.Length == 0)
        {
            colliders = GetComponentsInChildren<Collider>(true);
        }

        if (scaledParticleSystems == null || scaledParticleSystems.Length == 0)
        {
            scaledParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }

        if (effectScaleRoot == null
            && scaledParticleSystems != null
            && scaledParticleSystems.Length > 0)
        {
            effectScaleRoot = scaledParticleSystems[0].transform;
        }
    }

    private void ApplyEffectScaleSettings()
    {
        if (!scaleEffectsWithChest)
        {
            return;
        }

        if (effectScaleRoot != null)
        {
            effectScaleRoot.localScale = effectBaseLocalScale * Mathf.Max(0.01f, effectScaleMultiplier);
        }

        if (scaledParticleSystems == null)
        {
            return;
        }

        for (int i = 0; i < scaledParticleSystems.Length; i++)
        {
            ParticleSystem particles = scaledParticleSystems[i];
            if (particles == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particles.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    private void DisableColliders()
    {
        if (colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }
    }

    private void OnDestroy()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
        }

    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        openingSpeed = Mathf.Max(0.05f, openingSpeed);
        fallbackOpeningDuration = Mathf.Max(0.01f, fallbackOpeningDuration);
        itemFlightDuration = Mathf.Max(0.01f, itemFlightDuration);
        itemFlightArcHeight = Mathf.Max(0f, itemFlightArcHeight);
        itemSpinSpeed = Mathf.Max(0f, itemSpinSpeed);
        itemStartJitterRadius = Mathf.Max(0f, itemStartJitterRadius);
        effectScaleMultiplier = Mathf.Max(0.01f, effectScaleMultiplier);

        ResolveReferences();
        ApplyEffectScaleSettings();
    }
#endif
}
