using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 프리팹이 교체되어 Inspector 참조가 비어 있더라도 Player 태그 또는 레이어를 기준으로
/// 현재 플레이어 오브젝트와 그 계층의 컴포넌트를 찾아줍니다.
/// </summary>
public static class PlayerReferenceResolver
{
    public const string DefaultPlayerTag = "Player";
    public const string DefaultPlayerLayerName = "Player";

    /// <summary>기존 참조가 유효하면 유지하고, 비어 있으면 현재 플레이어 Transform을 찾습니다.</summary>
    public static Transform ResolveTransform(
        Transform current,
        string playerTag = DefaultPlayerTag,
        string playerLayerName = DefaultPlayerLayerName)
    {
        if (current != null)
        {
            return current;
        }

        GameObject playerObject = FindPlayerObject(playerTag, playerLayerName);
        return playerObject != null ? playerObject.transform : null;
    }

    /// <summary>기존 참조가 유효하면 유지하고, 비어 있으면 플레이어 계층에서 컴포넌트를 찾습니다.</summary>
    public static T ResolveComponent<T>(
        T current,
        GameObject playerObject = null,
        string playerTag = DefaultPlayerTag,
        string playerLayerName = DefaultPlayerLayerName) where T : Component
    {
        if (current != null)
        {
            return current;
        }

        return FindPlayerComponent<T>(playerObject, playerTag, playerLayerName);
    }

    /// <summary>활성 Player 태그를 우선 사용하고, 찾지 못하면 Player 레이어를 사용합니다.</summary>
    public static GameObject FindPlayerObject(
        string playerTag = DefaultPlayerTag,
        string playerLayerName = DefaultPlayerLayerName)
    {
        GameObject taggedPlayer = FindByTag(playerTag);
        if (taggedPlayer != null)
        {
            return taggedPlayer;
        }

        int playerLayer = string.IsNullOrWhiteSpace(playerLayerName)
            ? -1
            : LayerMask.NameToLayer(playerLayerName);

        if (string.IsNullOrWhiteSpace(playerTag) && playerLayer < 0)
        {
            return null;
        }

        Transform[] candidates = UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        Transform bestCandidate = null;
        int bestScore = int.MinValue;
        Scene activeScene = SceneManager.GetActiveScene();

        foreach (Transform candidate in candidates)
        {
            if (candidate == null
                || !IsPlayerMarker(candidate.gameObject, playerTag, playerLayer))
            {
                continue;
            }

            int score = 0;
            if (candidate.gameObject.activeInHierarchy)
            {
                score += 4;
            }

            if (candidate.parent == null
                || !IsPlayerMarker(candidate.parent.gameObject, playerTag, playerLayer))
            {
                score += 2;
            }

            if (candidate.gameObject.scene == activeScene)
            {
                score += 1;
            }

            if (bestCandidate == null || score > bestScore)
            {
                bestCandidate = candidate;
                bestScore = score;
            }
        }

        return bestCandidate != null ? bestCandidate.gameObject : null;
    }

    /// <summary>플레이어 루트와 자식, 부모에서 필요한 컴포넌트를 찾습니다.</summary>
    public static T FindPlayerComponent<T>(
        GameObject playerObject = null,
        string playerTag = DefaultPlayerTag,
        string playerLayerName = DefaultPlayerLayerName) where T : Component
    {
        GameObject target = playerObject != null
            ? playerObject
            : FindPlayerObject(playerTag, playerLayerName);

        return FindComponentInPlayerHierarchy<T>(target, playerTag, playerLayerName);
    }

    /// <summary>플레이어 루트 아래에 있는 동일 타입 컴포넌트를 모두 가져옵니다.</summary>
    public static T[] FindPlayerComponents<T>(
        string playerTag = DefaultPlayerTag,
        string playerLayerName = DefaultPlayerLayerName) where T : Component
    {
        GameObject playerObject = FindPlayerObject(playerTag, playerLayerName);
        if (playerObject == null)
        {
            return Array.Empty<T>();
        }

        T[] components = playerObject.GetComponentsInChildren<T>(true);
        if (components.Length > 0)
        {
            return components;
        }

        T parentComponent = playerObject.GetComponentInParent<T>(true);
        return parentComponent != null ? new[] { parentComponent } : Array.Empty<T>();
    }

    /// <summary>
    /// 전달된 오브젝트에서 부모 방향으로 플레이어 루트를 찾은 뒤 자식까지 포함해 컴포넌트를 찾습니다.
    /// PlayerInteraction이나 충돌 Collider가 플레이어의 자식에 있어도 같은 프리팹의 컴포넌트를 연결할 수 있습니다.
    /// </summary>
    public static T FindComponentInPlayerHierarchy<T>(
        GameObject source,
        string playerTag = DefaultPlayerTag,
        string playerLayerName = DefaultPlayerLayerName) where T : Component
    {
        if (source == null)
        {
            return null;
        }

        int playerLayer = string.IsNullOrWhiteSpace(playerLayerName)
            ? -1
            : LayerMask.NameToLayer(playerLayerName);

        Transform current = source.transform;
        while (current != null)
        {
            T component = current.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            if (IsPlayerMarker(current.gameObject, playerTag, playerLayer))
            {
                component = current.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            current = current.parent;
        }

        return source.GetComponentInChildren<T>(true);
    }

    /// <summary>오브젝트 자신이나 부모 중 하나가 Player 태그 또는 레이어인지 확인합니다.</summary>
    public static bool IsInPlayerHierarchy(
        GameObject source,
        string playerTag = DefaultPlayerTag,
        string playerLayerName = DefaultPlayerLayerName)
    {
        if (source == null)
        {
            return false;
        }

        int playerLayer = string.IsNullOrWhiteSpace(playerLayerName)
            ? -1
            : LayerMask.NameToLayer(playerLayerName);

        Transform current = source.transform;
        while (current != null)
        {
            if (IsPlayerMarker(current.gameObject, playerTag, playerLayer))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static GameObject FindByTag(string playerTag)
    {
        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return null;
        }

        try
        {
            return GameObject.FindGameObjectWithTag(playerTag);
        }
        catch (UnityException)
        {
            return null;
        }
    }

    private static bool IsPlayerMarker(GameObject target, string playerTag, int playerLayer)
    {
        bool tagMatches = !string.IsNullOrWhiteSpace(playerTag)
            && string.Equals(target.tag, playerTag, StringComparison.Ordinal);
        bool layerMatches = playerLayer >= 0 && target.layer == playerLayer;
        return tagMatches || layerMatches;
    }
}
