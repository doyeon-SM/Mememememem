using System;
using UnityEngine;

[Serializable]
public struct WorldItemDropLaunchSettings
{
    public bool enabled;
    public Vector3 startPosition;
    [Min(0.01f)] public float duration;
    [Min(0f)] public float arcHeight;
    [Min(0f)] public float spinSpeed;
    [Min(0f)] public float startJitterRadius;
}

[DisallowMultipleComponent]
[AddComponentMenu("GH/World/World Item Drop Motion")]
public sealed class WorldItemDropMotion : MonoBehaviour
{
    private Action onCompleted;
    private Collider[] colliders;
    private bool[] colliderStates;
    private Vector3 startPosition;
    private Vector3 endPosition;
    private float duration;
    private float arcHeight;
    private float spinSpeed;
    private float elapsed;

    public bool IsInFlight { get; private set; }

    public void Begin(
        Vector3 start,
        Vector3 end,
        float flightDuration,
        float height,
        float rotationSpeed,
        Action completionCallback = null)
    {
        if (IsInFlight)
        {
            IsInFlight = false;
            RestoreColliders();
            NotifyCompleted();
        }

        CacheColliders();

        startPosition = start;
        endPosition = end;
        duration = Mathf.Max(0.01f, flightDuration);
        arcHeight = Mathf.Max(0f, height);
        spinSpeed = Mathf.Max(0f, rotationSpeed);
        elapsed = 0f;
        onCompleted = completionCallback;
        IsInFlight = true;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliderStates[i] = colliders[i].enabled;
                colliders[i].enabled = false;
            }
        }

        transform.position = startPosition;
    }

    private void Update()
    {
        if (!IsInFlight)
        {
            return;
        }

        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        float easedProgress = progress * progress * (3f - 2f * progress);
        Vector3 position = Vector3.LerpUnclamped(startPosition, endPosition, easedProgress);
        position.y += Mathf.Sin(progress * Mathf.PI) * arcHeight;
        transform.position = position;

        if (spinSpeed > 0f)
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        if (progress >= 1f)
        {
            Complete();
        }
    }

    private void Complete()
    {
        transform.position = endPosition;
        IsInFlight = false;
        RestoreColliders();
        NotifyCompleted();
    }

    private void CacheColliders()
    {
        Collider[] currentColliders = GetComponentsInChildren<Collider>(true);
        colliders = currentColliders;
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

    private void OnDisable()
    {
        bool wasInFlight = IsInFlight;
        IsInFlight = false;
        RestoreColliders();

        if (wasInFlight)
        {
            NotifyCompleted();
        }
        else
        {
            onCompleted = null;
        }
    }

    private void NotifyCompleted()
    {
        Action callback = onCompleted;
        onCompleted = null;
        callback?.Invoke();
    }
}
