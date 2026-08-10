using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// KMS 코드를 수정하거나 참조하지 않고 월드 오브젝트 피해 숫자를 표시합니다.
/// 숫자는 플레이어 가슴 옆에 배치되며 대상 크기의 영향을 받지 않습니다.
/// </summary>
public sealed class GHWorldObjectDamagePopupService : MonoBehaviour
{
    private const string ServiceName = "[GH] World Object Damage Popup Service";
    private const float ReferenceScreenHeight = 1080f;
    private const float PlayerSideScreenOffset = 104f;
    private const float PlayerChestScreenLift = 12f;
    private const float ScreenEdgeMargin = 96f;
    private const float CameraPull = 0.2f;
    private const float PlayerChestHeightRatio = 0.68f;
    private const float FallbackPlayerChestHeight = 1.25f;
    private const int RetainedPoolSize = 32;

    private static GHWorldObjectDamagePopupService instance;
    private readonly Queue<GHWorldObjectDamagePopup> pool =
        new Queue<GHWorldObjectDamagePopup>();
    private Transform poolRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    public static void ShowDamage(
        Component target,
        Vector3 hitPoint,
        int damage)
    {
        if (target == null || damage <= 0)
        {
            return;
        }

        EnsureInstance();
        if (instance == null)
        {
            return;
        }

        Camera camera = Camera.main;
        Vector3 popupPosition = ResolvePopupPosition(
            target,
            hitPoint,
            camera);
        GHWorldObjectDamagePopup popup = instance.pool.Count > 0
            ? instance.pool.Dequeue()
            : instance.CreatePopup();
        popup.Play(popupPosition, damage, camera, instance.ReleasePopup);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        instance = FindFirstObjectByType<GHWorldObjectDamagePopupService>();
        if (instance != null)
        {
            return;
        }

        GameObject serviceObject = new GameObject(ServiceName);
        instance = serviceObject.AddComponent<GHWorldObjectDamagePopupService>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        GameObject rootObject = new GameObject("Popup Pool");
        poolRoot = rootObject.transform;
        poolRoot.SetParent(transform, false);
    }

    private GHWorldObjectDamagePopup CreatePopup()
    {
        GameObject popupObject = new GameObject(
            "GH World Object Damage Popup",
            typeof(GHWorldObjectDamagePopup));
        popupObject.transform.SetParent(poolRoot, false);
        return popupObject.GetComponent<GHWorldObjectDamagePopup>();
    }

    private void ReleasePopup(GHWorldObjectDamagePopup popup)
    {
        if (popup == null)
        {
            return;
        }

        popup.transform.SetParent(poolRoot, false);
        if (pool.Count < RetainedPoolSize)
        {
            pool.Enqueue(popup);
        }
        else
        {
            Destroy(popup.gameObject);
        }
    }

    private static Vector3 ResolvePopupPosition(
        Component target,
        Vector3 hitPoint,
        Camera camera)
    {
        if (camera == null
            || !TryResolvePlayerChestPosition(
                hitPoint.y,
                out Vector3 playerChestPosition))
        {
            return hitPoint + Vector3.up * 0.55f;
        }

        Vector3 playerScreen = camera.WorldToScreenPoint(playerChestPosition);
        if (playerScreen.z <= camera.nearClipPlane)
        {
            return hitPoint + Vector3.up * 0.55f;
        }

        float resolutionScale = Mathf.Max(
            0.65f,
            Screen.height / ReferenceScreenHeight);
        float sideOffset = PlayerSideScreenOffset * resolutionScale;
        float screenLift = PlayerChestScreenLift * resolutionScale;
        float edgeMargin = ScreenEdgeMargin * resolutionScale;

        Vector3 targetScreen = camera.WorldToScreenPoint(target.transform.position);
        Vector3 hitScreen = camera.WorldToScreenPoint(hitPoint);
        float comparisonX = targetScreen.z > camera.nearClipPlane
            ? targetScreen.x
            : hitScreen.x;
        float side = comparisonX >= playerScreen.x ? -1f : 1f;
        float selectedX = playerScreen.x + side * sideOffset;

        if (selectedX < edgeMargin || selectedX > Screen.width - edgeMargin)
        {
            selectedX = playerScreen.x - side * sideOffset;
        }

        Vector3 selectedScreen = new Vector3(
            Mathf.Clamp(selectedX, edgeMargin, Screen.width - edgeMargin),
            Mathf.Clamp(
                playerScreen.y + screenLift,
                edgeMargin,
                Screen.height - edgeMargin),
            playerScreen.z);
        return camera.ScreenToWorldPoint(selectedScreen)
            - camera.transform.forward * CameraPull;
    }

    private static bool TryResolvePlayerChestPosition(
        float fallbackGroundY,
        out Vector3 chestPosition)
    {
        GameObject playerObject = PlayerReferenceResolver.FindPlayerObject();
        if (playerObject == null)
        {
            chestPosition = default;
            return false;
        }

        CharacterController controller = PlayerReferenceResolver
            .FindComponentInPlayerHierarchy<CharacterController>(playerObject);
        if (controller != null && controller.bounds.size.y > 0.1f)
        {
            chestPosition = controller.bounds.center;
            chestPosition.y = controller.bounds.min.y
                + controller.bounds.size.y * PlayerChestHeightRatio;
            return true;
        }

        chestPosition = playerObject.transform.position;
        chestPosition.y = Mathf.Max(
            chestPosition.y + FallbackPlayerChestHeight,
            fallbackGroundY + 0.55f);
        return true;
    }
}

[DisallowMultipleComponent]
public sealed class GHWorldObjectDamagePopup : MonoBehaviour
{
    private const float Lifetime = 1.05f;
    private const float RiseDistance = 0.75f;
    private const float FadeStart = 0.55f;
    private const float SpawnScale = 0.7f;
    private const float OvershootScale = 1.18f;
    private const float ReferencePerspectiveDistance = 8f;
    private const float ReferenceOrthographicSize = 5f;
    private const float MinimumCameraScale = 0.8f;
    private const float MaximumCameraScale = 5f;
    private const int LargeDamageThreshold = 10;

    private static readonly Color NormalColor =
        new Color(1f, 0.72f, 0.22f, 1f);
    private static readonly Color LargeDamageColor =
        new Color(1f, 0.2f, 0.12f, 1f);
    private static readonly Color OutlineColor =
        new Color(0.08f, 0.025f, 0.015f, 1f);

    private TextMeshPro textMesh;
    private Camera targetCamera;
    private Action<GHWorldObjectDamagePopup> releaseCallback;
    private Vector3 startPosition;
    private Color baseColor;
    private float damageScale;
    private float age;
    private bool isPlaying;

    public void Play(
        Vector3 worldPosition,
        int damage,
        Camera camera,
        Action<GHWorldObjectDamagePopup> onReleased)
    {
        EnsureVisual();
        targetCamera = camera;
        releaseCallback = onReleased;
        startPosition = worldPosition;
        age = 0f;
        isPlaying = true;

        float thresholdRatio = Mathf.Clamp01(
            (damage - LargeDamageThreshold) / (float)LargeDamageThreshold);
        damageScale = Mathf.Lerp(1f, 1.45f, thresholdRatio);
        baseColor = Color.Lerp(
            NormalColor,
            LargeDamageColor,
            thresholdRatio);

        textMesh.text = damage.ToString();
        textMesh.fontSize = 5f;
        textMesh.color = baseColor;
        textMesh.outlineColor = OutlineColor;
        textMesh.outlineWidth = 0.22f;

        transform.position = startPosition;
        transform.localScale = Vector3.one
            * SpawnScale
            * damageScale
            * ResolveCameraScale();
        gameObject.SetActive(true);
        FaceCamera();
    }

    private void LateUpdate()
    {
        if (!isPlaying)
        {
            return;
        }

        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / Lifetime);
        float easedRise = 1f - Mathf.Pow(1f - progress, 2f);
        transform.position = startPosition + Vector3.up * RiseDistance * easedRise;
        transform.localScale = Vector3.one
            * damageScale
            * EvaluatePopScale(progress)
            * ResolveCameraScale();

        float alpha = progress <= FadeStart
            ? 1f
            : 1f - Mathf.InverseLerp(FadeStart, 1f, progress);
        Color color = baseColor;
        color.a = Mathf.SmoothStep(0f, 1f, alpha);
        textMesh.color = color;
        FaceCamera();

        if (age >= Lifetime)
        {
            isPlaying = false;
            gameObject.SetActive(false);
            releaseCallback?.Invoke(this);
        }
    }

    private float EvaluatePopScale(float progress)
    {
        if (progress < 0.12f)
        {
            return Mathf.Lerp(
                SpawnScale,
                OvershootScale,
                progress / 0.12f);
        }

        if (progress < 0.28f)
        {
            return Mathf.Lerp(
                OvershootScale,
                1f,
                (progress - 0.12f) / 0.16f);
        }

        return 1f;
    }

    private float ResolveCameraScale()
    {
        Camera camera = targetCamera != null ? targetCamera : Camera.main;
        if (camera == null)
        {
            return 1f;
        }

        if (camera.orthographic)
        {
            return Mathf.Max(
                0.01f,
                camera.orthographicSize / ReferenceOrthographicSize);
        }

        float distance = Vector3.Distance(
            camera.transform.position,
            transform.position);
        return Mathf.Clamp(
            distance / ReferencePerspectiveDistance,
            MinimumCameraScale,
            MaximumCameraScale);
    }

    private void FaceCamera()
    {
        Camera camera = targetCamera != null ? targetCamera : Camera.main;
        if (camera != null)
        {
            transform.forward = camera.transform.forward;
        }
    }

    private void EnsureVisual()
    {
        if (textMesh == null)
        {
            textMesh = GetComponent<TextMeshPro>();
            if (textMesh == null)
            {
                textMesh = gameObject.AddComponent<TextMeshPro>();
            }

            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.textWrappingMode = TextWrappingModes.NoWrap;
            textMesh.richText = false;
            textMesh.raycastTarget = false;
            textMesh.sortingOrder = 250;
        }
    }
}
