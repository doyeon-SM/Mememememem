using UnityEngine;

/// <summary>
/// 월드 오브젝트와 상자가 같은 규칙으로 월드 아이템을 배치하도록 공통 드롭 생성을 담당합니다.
/// </summary>
public static class WorldItemDropSpawner
{
    private const float GroundRaycastHeight = 3f;
    private const float GroundRaycastDistance = 8f;
    private const int MaxClearanceHits = 32;

    private static readonly Collider[] ClearanceHits = new Collider[MaxClearanceHits];

    /// <summary>
    /// 한 아이템 종류의 전체 수량을 하나의 월드 아이템 스택으로 생성합니다.
    /// </summary>
    /// <returns>실제로 월드에 생성된 총 아이템 수량입니다.</returns>
    public static int SpawnStack(
        string itemId,
        int amount,
        Transform owner,
        Transform dropPoint,
        Vector3 dropAreaOffset,
        Vector2 dropAreaSize,
        LayerMask groundLayer,
        float spawnHeight,
        int positionAttempts,
        float clearanceRadius,
        float clearanceHeight,
        float maxGroundSlope,
        float autoReturnToPoolSeconds,
        Collider[] ignoredColliders = null)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
        {
            return 0;
        }

        if (!TryGetSpawnPosition(
                owner,
                dropPoint,
                dropAreaOffset,
                dropAreaSize,
                groundLayer,
                spawnHeight,
                positionAttempts,
                clearanceRadius,
                clearanceHeight,
                maxGroundSlope,
                ignoredColliders,
                out Vector3 spawnPosition))
        {
            Debug.LogWarning(
                $"[{GetOwnerName(owner)}] 주변에서 안전한 바닥 드롭 위치를 찾지 못해 '{itemId}' 생성을 건너뜁니다.",
                owner);
            return 0;
        }

        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject spawnedObject = WorldDropPool.Spawn(
            itemId,
            amount,
            spawnPosition,
            rotation,
            autoReturnToPoolSeconds);

        return spawnedObject != null ? amount : 0;
    }

    private static bool TryGetSpawnPosition(
        Transform owner,
        Transform dropPoint,
        Vector3 dropAreaOffset,
        Vector2 dropAreaSize,
        LayerMask groundLayer,
        float spawnHeight,
        int positionAttempts,
        float clearanceRadius,
        float clearanceHeight,
        float maxGroundSlope,
        Collider[] ignoredColliders,
        out Vector3 spawnPosition)
    {
        int attempts = Mathf.Max(1, positionAttempts);
        Transform anchor = dropPoint != null ? dropPoint : owner;
        Vector3 origin = GetDropAreaCenter(anchor, dropAreaOffset);
        Vector2 halfSize = new Vector2(
            Mathf.Max(0f, dropAreaSize.x) * 0.5f,
            Mathf.Max(0f, dropAreaSize.y) * 0.5f);
        Vector3 areaRight = GetHorizontalAxis(anchor != null ? anchor.right : Vector3.right, Vector3.right);
        Vector3 areaForward = GetHorizontalAxis(anchor != null ? anchor.forward : Vector3.forward, Vector3.forward);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector2 randomCircle = Random.insideUnitCircle;
            Vector3 samplePosition = origin
                + areaRight * (randomCircle.x * halfSize.x)
                + areaForward * (randomCircle.y * halfSize.y);
            Vector3 rayStart = samplePosition + Vector3.up * GroundRaycastHeight;

            if (!TryFindGround(
                    rayStart,
                    owner,
                    groundLayer,
                    maxGroundSlope,
                    ignoredColliders,
                    out RaycastHit groundHit))
            {
                continue;
            }

            if (!IsDropSpaceClear(
                    groundHit.point,
                    groundHit.collider,
                    owner,
                    clearanceRadius,
                    clearanceHeight,
                    ignoredColliders))
            {
                continue;
            }

            spawnPosition = groundHit.point + groundHit.normal * Mathf.Max(0f, spawnHeight);
            return true;
        }

        spawnPosition = default;
        return false;
    }

    private static Vector3 GetDropAreaCenter(Transform anchor, Vector3 localOffset)
    {
        if (anchor == null)
        {
            return localOffset;
        }

        return anchor.position + anchor.rotation * localOffset;
    }

    private static Vector3 GetHorizontalAxis(Vector3 axis, Vector3 fallback)
    {
        Vector3 horizontalAxis = Vector3.ProjectOnPlane(axis, Vector3.up);
        return horizontalAxis.sqrMagnitude > 0.0001f
            ? horizontalAxis.normalized
            : fallback;
    }

    private static bool TryFindGround(
        Vector3 rayStart,
        Transform owner,
        LayerMask groundLayer,
        float maxGroundSlope,
        Collider[] ignoredColliders,
        out RaycastHit groundHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            rayStart,
            Vector3.down,
            GroundRaycastDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore);

        groundHit = default;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidGroundHit(hit, owner, maxGroundSlope, ignoredColliders))
            {
                continue;
            }

            if (!found || hit.distance < groundHit.distance)
            {
                groundHit = hit;
                found = true;
            }
        }

        return found;
    }

    private static bool IsValidGroundHit(
        RaycastHit hit,
        Transform owner,
        float maxGroundSlope,
        Collider[] ignoredColliders)
    {
        Collider hitCollider = hit.collider;
        if (hitCollider == null
            || Vector3.Angle(hit.normal, Vector3.up) > Mathf.Clamp(maxGroundSlope, 0f, 89f)
            || IsOwnedOrIgnored(hitCollider, owner, ignoredColliders)
            || PlayerReferenceResolver.IsInPlayerHierarchy(hitCollider.gameObject)
            || hitCollider.GetComponentInParent<WorldItem>() != null
            || hitCollider.GetComponentInParent<WorldObject>() != null
            || hitCollider.GetComponentInParent<Chest>() != null)
        {
            return false;
        }

        Rigidbody attachedBody = hitCollider.attachedRigidbody;
        return attachedBody == null || attachedBody.isKinematic;
    }

    private static bool IsDropSpaceClear(
        Vector3 groundPosition,
        Collider groundCollider,
        Transform owner,
        float clearanceRadius,
        float clearanceHeight,
        Collider[] ignoredColliders)
    {
        float radius = Mathf.Max(0.01f, clearanceRadius);
        float height = Mathf.Max(radius * 2f, clearanceHeight);
        Vector3 bottom = groundPosition + Vector3.up * (radius + 0.01f);
        Vector3 top = groundPosition + Vector3.up * Mathf.Max(radius + 0.01f, height - radius);
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            radius,
            ClearanceHits,
            ~0,
            QueryTriggerInteraction.Ignore);

        bool isClear = true;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = ClearanceHits[i];
            ClearanceHits[i] = null;
            if (hitCollider == null
                || hitCollider == groundCollider
                || IsOwnedOrIgnored(hitCollider, owner, ignoredColliders))
            {
                continue;
            }

            isClear = false;
        }

        return isClear;
    }

    private static bool IsOwnedOrIgnored(
        Collider candidate,
        Transform owner,
        Collider[] ignoredColliders)
    {
        if (candidate == null)
        {
            return false;
        }

        if (owner != null && candidate.transform.IsChildOf(owner))
        {
            return true;
        }

        if (ignoredColliders == null)
        {
            return false;
        }

        for (int i = 0; i < ignoredColliders.Length; i++)
        {
            if (ignoredColliders[i] == candidate)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetOwnerName(Transform owner)
    {
        return owner != null ? owner.name : nameof(WorldItemDropSpawner);
    }

#if UNITY_EDITOR
    /// <summary>실제 무작위 배치와 같은 X/Z 타원 영역을 Scene 뷰에 표시합니다.</summary>
    public static void DrawDropAreaGizmo(
        Transform dropPoint,
        Transform owner,
        Vector3 dropAreaOffset,
        Vector2 dropAreaSize,
        float spawnHeight,
        Color color)
    {
        Transform anchor = dropPoint != null ? dropPoint : owner;
        Vector3 anchorPosition = anchor != null ? anchor.position : Vector3.zero;
        Vector3 center = GetDropAreaCenter(anchor, dropAreaOffset) + Vector3.up * Mathf.Max(0f, spawnHeight);
        Vector2 halfSize = new Vector2(
            Mathf.Max(0f, dropAreaSize.x) * 0.5f,
            Mathf.Max(0f, dropAreaSize.y) * 0.5f);
        Vector2 clampedSize = halfSize * 2f;
        Vector3 areaRight = GetHorizontalAxis(anchor != null ? anchor.right : Vector3.right, Vector3.right);
        Vector3 areaForward = GetHorizontalAxis(anchor != null ? anchor.forward : Vector3.forward, Vector3.forward);

        const int segmentCount = 48;
        UnityEditor.Handles.color = color;
        Vector3 previousPoint = center + areaRight * halfSize.x;

        for (int segment = 1; segment <= segmentCount; segment++)
        {
            float angle = segment * Mathf.PI * 2f / segmentCount;
            Vector3 nextPoint = center
                + areaRight * (Mathf.Cos(angle) * halfSize.x)
                + areaForward * (Mathf.Sin(angle) * halfSize.y);
            UnityEditor.Handles.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }

        UnityEditor.Handles.DrawLine(center - areaRight * halfSize.x, center + areaRight * halfSize.x);
        UnityEditor.Handles.DrawLine(center - areaForward * halfSize.y, center + areaForward * halfSize.y);
        UnityEditor.Handles.DrawDottedLine(anchorPosition, center, 4f);
        UnityEditor.Handles.Label(
            center,
            $"Drop Area  X {clampedSize.x:0.##} / Z {clampedSize.y:0.##}");

        Gizmos.color = color;
        float centerMarkerSize = Mathf.Max(0.04f, Mathf.Max(halfSize.x, halfSize.y) * 0.025f);
        Gizmos.DrawSphere(center, centerMarkerSize);
    }
#endif
}
