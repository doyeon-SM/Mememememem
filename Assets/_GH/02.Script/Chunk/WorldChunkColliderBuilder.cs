using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public readonly struct WorldChunkColliderBuildResult
{
    public WorldChunkColliderBuildResult(int meshColliderCount, int boxColliderCount, int capsuleColliderCount)
    {
        MeshColliderCount = meshColliderCount;
        BoxColliderCount = boxColliderCount;
        CapsuleColliderCount = capsuleColliderCount;
    }

    public int MeshColliderCount { get; }
    public int BoxColliderCount { get; }
    public int CapsuleColliderCount { get; }
    public int TotalCount => MeshColliderCount + BoxColliderCount + CapsuleColliderCount;

    public static WorldChunkColliderBuildResult operator +(
        WorldChunkColliderBuildResult left,
        WorldChunkColliderBuildResult right)
    {
        return new WorldChunkColliderBuildResult(
            left.MeshColliderCount + right.MeshColliderCount,
            left.BoxColliderCount + right.BoxColliderCount,
            left.CapsuleColliderCount + right.CapsuleColliderCount);
    }
}

/// <summary>
/// Completes collision for static renderer objects placed directly below a WorldChunk.
/// A placed hierarchy that already contains any Collider is never modified.
/// </summary>
public static class WorldChunkColliderBuilder
{
    private static readonly string[] EnterableBuildingKeywords =
    {
        "house", "building", "cabin", "hut", "shed", "warehouse", "shop",
        "inn", "tavern", "temple", "church", "chapel", "interior", "outbuilding"
    };

    private static readonly string[] CapsuleKeywords =
    {
        "tree", "trunk", "stump", "log", "pillar", "column", "post", "pole",
        "barrel", "vase", "pot", "boulder", "rock", "stone"
    };

    // These renderers are intentionally non-solid world dressing.
    private static readonly string[] NonSolidKeywords =
    {
        "grass", "flower", "foliage", "bush", "shrub", "weed", "fern", "ivy",
        "leaf", "leaves", "moss", "water", "river", "ocean", "cloud", "sky",
        "decal", "particle", "vfx", "visualeffect", "billboard", "shadow"
    };

    public static WorldChunkColliderBuildResult AddMissingColliders(
        WorldChunk chunk,
        bool registerUndo = false)
    {
        if (chunk == null)
        {
            return default;
        }

        WorldChunkColliderBuildResult result = default;
        Transform chunkTransform = chunk.transform;

        // Chunk children are the placed object roots in the world scenes. Treating the
        // whole child hierarchy as one unit avoids adding duplicates to compound prefabs.
        for (int i = 0; i < chunkTransform.childCount; i++)
        {
            Transform placedRoot = chunkTransform.GetChild(i);
            if (placedRoot == null || placedRoot.GetComponentInChildren<Collider>(true) != null)
            {
                continue;
            }

            string hierarchyName = BuildHierarchyName(placedRoot);
            if (ContainsKeyword(placedRoot.name, NonSolidKeywords))
            {
                continue;
            }

            List<RendererMesh> rendererMeshes = CollectPrimaryRendererMeshes(placedRoot);
            if (rendererMeshes.Count == 0)
            {
                continue;
            }

            if (ContainsKeyword(hierarchyName, EnterableBuildingKeywords))
            {
                result += AddBuildingMeshColliders(rendererMeshes, registerUndo);
                continue;
            }

            if (!TryCalculateRootLocalBounds(placedRoot, rendererMeshes, out Bounds bounds))
            {
                continue;
            }

            bool useCapsule = ShouldUseCapsule(hierarchyName, bounds.size);
            if (useCapsule)
            {
                CapsuleCollider capsule = AddComponent<CapsuleCollider>(placedRoot.gameObject, registerUndo);
                ConfigureCapsule(capsule, bounds, hierarchyName);
                result += new WorldChunkColliderBuildResult(0, 0, 1);
            }
            else
            {
                BoxCollider box = AddComponent<BoxCollider>(placedRoot.gameObject, registerUndo);
                box.center = bounds.center;
                box.size = ClampSize(bounds.size);
                result += new WorldChunkColliderBuildResult(0, 1, 0);
            }
        }

        return result;
    }

    private static WorldChunkColliderBuildResult AddBuildingMeshColliders(
        List<RendererMesh> rendererMeshes,
        bool registerUndo)
    {
        int added = 0;

        foreach (RendererMesh rendererMesh in rendererMeshes)
        {
            GameObject target = rendererMesh.Renderer.gameObject;
            if (target.GetComponent<Collider>() != null)
            {
                continue;
            }

            MeshCollider collider = AddComponent<MeshCollider>(target, registerUndo);
            collider.sharedMesh = rendererMesh.Mesh;
            collider.convex = false;
            added++;
        }

        return new WorldChunkColliderBuildResult(added, 0, 0);
    }

    private static List<RendererMesh> CollectPrimaryRendererMeshes(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        List<RendererMesh> results = new List<RendererMesh>(renderers.Length);

        foreach (Renderer renderer in renderers)
        {
            if (!TryGetSharedMesh(renderer, out Mesh mesh) || mesh == null || !IsPrimaryLodRenderer(renderer, root))
            {
                continue;
            }

            Vector3 size = mesh.bounds.size;
            if (size.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            results.Add(new RendererMesh(renderer, mesh));
        }

        return results;
    }

    private static bool TryGetSharedMesh(Renderer renderer, out Mesh mesh)
    {
        if (renderer is MeshRenderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            mesh = filter != null ? filter.sharedMesh : null;
            return mesh != null;
        }

        if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
        {
            mesh = skinnedMeshRenderer.sharedMesh;
            return mesh != null;
        }

        mesh = null;
        return false;
    }

    private static bool IsPrimaryLodRenderer(Renderer renderer, Transform placedRoot)
    {
        LODGroup lodGroup = renderer.GetComponentInParent<LODGroup>();
        if (lodGroup == null ||
            (lodGroup.transform != placedRoot && !lodGroup.transform.IsChildOf(placedRoot)))
        {
            return true;
        }

        LOD[] lods = lodGroup.GetLODs();
        if (lods.Length == 0)
        {
            return true;
        }

        foreach (Renderer primaryRenderer in lods[0].renderers)
        {
            if (primaryRenderer == renderer)
            {
                return true;
            }
        }

        // A renderer not registered in the LODGroup is an always-visible detail.
        foreach (LOD lod in lods)
        {
            foreach (Renderer lodRenderer in lod.renderers)
            {
                if (lodRenderer == renderer)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryCalculateRootLocalBounds(
        Transform root,
        List<RendererMesh> rendererMeshes,
        out Bounds bounds)
    {
        bounds = default;
        bool hasPoint = false;

        foreach (RendererMesh rendererMesh in rendererMeshes)
        {
            Bounds meshBounds = rendererMesh.Mesh.bounds;
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 meshPoint = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 worldPoint = rendererMesh.Renderer.transform.TransformPoint(meshPoint);
                Vector3 rootPoint = root.InverseTransformPoint(worldPoint);

                if (!hasPoint)
                {
                    bounds = new Bounds(rootPoint, Vector3.zero);
                    hasPoint = true;
                }
                else
                {
                    bounds.Encapsulate(rootPoint);
                }
            }
        }

        return hasPoint;
    }

    private static void ConfigureCapsule(CapsuleCollider capsule, Bounds bounds, string hierarchyName)
    {
        Vector3 size = ClampSize(bounds.size);
        bool isTree = hierarchyName.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      hierarchyName.IndexOf("trunk", StringComparison.OrdinalIgnoreCase) >= 0;

        int direction = isTree ? 1 : LargestAxis(size);
        float height = GetAxis(size, direction);
        float radius = Mathf.Max(GetOtherAxis(size, direction, 0), GetOtherAxis(size, direction, 1)) * 0.5f;

        // For trees the visible canopy is much wider than the solid trunk.
        if (isTree)
        {
            radius = Mathf.Max(0.12f, Mathf.Min(size.x, size.z) * 0.16f);
        }

        capsule.center = bounds.center;
        capsule.direction = direction;
        capsule.radius = Mathf.Max(0.01f, radius);
        capsule.height = Mathf.Max(height, capsule.radius * 2f);
    }

    private static bool ShouldUseCapsule(string hierarchyName, Vector3 size)
    {
        if (ContainsKeyword(hierarchyName, CapsuleKeywords))
        {
            return true;
        }

        int largestAxis = LargestAxis(size);
        float primary = GetAxis(size, largestAxis);
        float secondaryA = GetOtherAxis(size, largestAxis, 0);
        float secondaryB = GetOtherAxis(size, largestAxis, 1);
        return primary >= Mathf.Max(secondaryA, secondaryB) * 1.75f &&
               Mathf.Abs(secondaryA - secondaryB) <= Mathf.Max(secondaryA, secondaryB) * 0.45f;
    }

    private static string BuildHierarchyName(Transform root)
    {
        string result = root.name;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            result += " " + renderer.name;
        }

        return result;
    }

    private static bool ContainsKeyword(string value, string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            if (value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static int LargestAxis(Vector3 value)
    {
        if (value.x >= value.y && value.x >= value.z)
        {
            return 0;
        }

        return value.y >= value.z ? 1 : 2;
    }

    private static float GetAxis(Vector3 value, int axis)
    {
        return axis == 0 ? value.x : axis == 1 ? value.y : value.z;
    }

    private static float GetOtherAxis(Vector3 value, int primaryAxis, int otherIndex)
    {
        if (primaryAxis == 0)
        {
            return otherIndex == 0 ? value.y : value.z;
        }

        if (primaryAxis == 1)
        {
            return otherIndex == 0 ? value.x : value.z;
        }

        return otherIndex == 0 ? value.x : value.y;
    }

    private static Vector3 ClampSize(Vector3 size)
    {
        return new Vector3(
            Mathf.Max(0.01f, size.x),
            Mathf.Max(0.01f, size.y),
            Mathf.Max(0.01f, size.z));
    }

    private static T AddComponent<T>(GameObject gameObject, bool registerUndo) where T : Component
    {
#if UNITY_EDITOR
        if (registerUndo && !Application.isPlaying)
        {
            return Undo.AddComponent<T>(gameObject);
        }
#endif
        return gameObject.AddComponent<T>();
    }

    private readonly struct RendererMesh
    {
        public RendererMesh(Renderer renderer, Mesh mesh)
        {
            Renderer = renderer;
            Mesh = mesh;
        }

        public Renderer Renderer { get; }
        public Mesh Mesh { get; }
    }
}
