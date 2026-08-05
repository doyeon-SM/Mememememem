using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace PotaToon.Editor
{
    /// <summary>
    /// Utility to calculate bounds for each submesh of all meshes under a root object.
    /// </summary>
    public static class PotaToonMeshBoundsUtils
    {
        [Serializable]
        public struct SubmeshBoundsInfo
        {
            public Renderer renderer;
            public Bounds localBounds;
            public Bounds worldBounds;
        }

        /// <summary>
        /// Computes per-submesh bounds for all meshes under <paramref name="root"/>.
        /// </summary>
        /// <param name="root">Root object whose children will be scanned.</param>
        /// <param name="includeInactive">Include inactive children.</param>
        /// <param name="bakeSkinnedMeshes">Bake skinned meshes to capture deformed vertices.</param>
        /// <returns>List of SubmeshBoundsInfo entries.</returns>
        public static List<SubmeshBoundsInfo> ComputeMeshBounds(GameObject root, bool includeInactive = true, bool bakeSkinnedMeshes = false)
        {
            var results = new List<SubmeshBoundsInfo>();
            if (root == null)
                return results;

            // Initialize rotation
            var originalRotation = root.transform.rotation;
            root.transform.rotation = Quaternion.identity;
            
            // MeshFilter + MeshRenderer pairs
            var meshFilters = root.GetComponentsInChildren<MeshFilter>(includeInactive);
            foreach (var mf in meshFilters)
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null)
                    continue;

                var mesh = mf.sharedMesh;
                if (mesh == null)
                    continue;

                ComputeForMesh(results, mr, mesh, mr.transform.localToWorldMatrix);
            }

            // Skinned meshes
            var skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive);
            foreach (var smr in skinned)
            {
                var mesh = smr.sharedMesh;
                if (mesh == null)
                    continue;

                Mesh source = mesh;
                Mesh baked = null;
                
                try
                {
                    if (bakeSkinnedMeshes)
                    {
                        baked = new Mesh();
                        smr.BakeMesh(baked, true);
                        source = baked;
                    }
                    ComputeForMesh(results, smr, source, smr.transform.localToWorldMatrix);
                }
                finally
                {
                    if (baked != null)
                    {
                        UnityEngine.Object.DestroyImmediate(baked);
                    }
                }
            }
            
            // Restore rotation
            root.transform.rotation = originalRotation;

            return results;
        }

        [Serializable]
        public struct RendererBoundsComparison
        {
            public Renderer renderer;               // Target renderer
            public Bounds rendererWorldBounds;      // Current renderer.bounds (world space)
            public Bounds computedWorldBounds;      // Combined computed bounds from all submeshes (world space)
        }

        /// <summary>
        /// Compares current Renderer.bounds (world) against calculated bounds (combined across submeshes, world) for each renderer.
        /// </summary>
        public static List<RendererBoundsComparison> CompareRendererAndComputedBounds(GameObject root, bool includeInactive = true, bool bakeSkinnedMeshes = false)
        {
            var submeshInfos = ComputeMeshBounds(root, includeInactive, bakeSkinnedMeshes);
            var results = new List<RendererBoundsComparison>();
            if (submeshInfos.Count == 0)
                return results;

            // Group by renderer
            var grouped = new Dictionary<Renderer, List<SubmeshBoundsInfo>>();
            foreach (var info in submeshInfos)
            {
                if (!grouped.TryGetValue(info.renderer, out var list))
                {
                    list = new List<SubmeshBoundsInfo>();
                    grouped.Add(info.renderer, list);
                }
                list.Add(info);
            }

            foreach (var kvp in grouped)
            {
                var renderer = kvp.Key;
                var list = kvp.Value;
                if (list.Count == 0)
                    continue;

                // Combine all computed submesh bounds
                Bounds combinedWorld = list[0].worldBounds;
                for (int i = 1; i < list.Count; i++)
                {
                    combinedWorld.Encapsulate(list[i].worldBounds);
                }
                
                results.Add(new RendererBoundsComparison
                {
                    renderer = renderer,
                    rendererWorldBounds = renderer.bounds,
                    computedWorldBounds = combinedWorld,
                });
            }

            return results;
        }

        /// <summary>
        /// Returns a list of renderers whose computed WORLD bounds differ from current Renderer.bounds by
        /// at least <paramref name="thresholdRatio"/> (20% by default) when comparing overall size magnitude.
        /// Uses the magnitude of Bounds.size (diagonal length) for comparison.
        /// </summary>
        /// <param name="root">Root GameObject to analyze.</param>
        /// <param name="thresholdRatio">Relative difference threshold (e.g., 0.2 for 20%).</param>
        /// <param name="includeInactive">Include inactive children.</param>
        /// <param name="bakeSkinnedMeshes">Bake skinned meshes to account for current deformation.</param>
        /// <returns>Subset of <see cref="RendererBoundsComparison"/> with differences >= threshold.</returns>
        public static List<RendererBoundsComparison> FindBoundsMismatches(GameObject root, float thresholdRatio = 0.2f, bool includeInactive = true, bool bakeSkinnedMeshes = false)
        {
            var all = CompareRendererAndComputedBounds(root, includeInactive, bakeSkinnedMeshes);
            var flagged = new List<RendererBoundsComparison>();

            foreach (var item in all)
            {
                if (ExceedsThreshold(item.rendererWorldBounds, item.computedWorldBounds, thresholdRatio))
                {
                    flagged.Add(item);
                }
            }

            return flagged;
        }

        private static bool ExceedsThreshold(in Bounds currentWorld, in Bounds computedWorld, float threshold)
        {
            const float eps = 1e-6f;

            var a = currentWorld.size.magnitude; // diagonal length
            var b = computedWorld.size.magnitude;

            float rel = Mathf.Abs(b - a) / Mathf.Max(Mathf.Abs(a), eps);
            return rel >= threshold;
        }

        /// <summary>
        /// Applies computed bounds to meshes under <paramref name="root"/>.
        /// Combines per-submesh world bounds. (same as CompareRendererAndComputedBounds)
        /// </summary>
        public static void ApplyComputedBoundsToMeshes(GameObject root, bool includeInactive, bool bakeSkinnedMeshes)
        {
            var submeshInfos = ComputeMeshBounds(root, includeInactive, bakeSkinnedMeshes);
            if (submeshInfos.Count == 0)
                return;

            // Group by renderer
            var grouped = new Dictionary<Renderer, List<SubmeshBoundsInfo>>();
            foreach (var info in submeshInfos)
            {
                if (!grouped.TryGetValue(info.renderer, out var list))
                {
                    list = new List<SubmeshBoundsInfo>();
                    grouped.Add(info.renderer, list);
                }
                list.Add(info);
            }
            
            // Initialize rotation
            var originalRotation = root.transform.rotation;
            root.transform.rotation = Quaternion.identity;
            
            foreach (var kvp in grouped)
            {
                var renderer = kvp.Key;
                var list = kvp.Value;
                if (list.Count == 0)
                    continue;

                // Combine WORLD-space bounds across submeshes (match CompareRendererAndComputedBounds)
                Bounds combinedLocal = list[0].localBounds;
                Bounds combinedWorld = list[0].worldBounds;
                for (int i = 1; i < list.Count; i++)
                {
                    combinedLocal.Encapsulate(list[i].localBounds);
                    combinedWorld.Encapsulate(list[i].worldBounds);
                }
                
                // Apply to appropriate target
                if (renderer is SkinnedMeshRenderer smr)
                {
                    Undo.RecordObject(smr, "Apply Computed Bounds (Skinned)");
                    // 1. Set local bounds first to get a position diff since we don't know the position of rigged bone.
                    smr.localBounds = combinedLocal;
                    // 2. Apply offset & Set the extents with world bounds
                    combinedLocal.center += (combinedWorld.center - smr.bounds.center);
                    combinedLocal.extents = combinedWorld.extents;
                    smr.localBounds = combinedLocal;
                    EditorUtility.SetDirty(smr);
                }
                else if (renderer is MeshRenderer mr)
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var mesh = mf.sharedMesh;
                        Undo.RecordObject(mesh, "Apply Computed Bounds (Mesh)");
                        mesh.bounds = combinedWorld;
                        EditorUtility.SetDirty(mesh);
                    }
                }
            }
            
            // Restore rotation
            root.transform.rotation = originalRotation;
        }

        private static void ComputeForMesh(List<SubmeshBoundsInfo> sink, Renderer renderer, Mesh mesh, Matrix4x4 localToWorld)
        {
            var vertices = mesh.vertices;
            int subMeshCount = mesh.subMeshCount;
            if (vertices == null || vertices.Length == 0 || subMeshCount == 0)
                return;

            for (int si = 0; si < subMeshCount; si++)
            {
                // Use indices for the submesh to gather only referenced vertices
                var indices = mesh.GetIndices(si);
                if (indices == null || indices.Length == 0)
                    continue;

                Vector3 minLocal = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                Vector3 maxLocal = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

                Vector3 minWorld = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                Vector3 maxWorld = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

                for (int i = 0; i < indices.Length; i++)
                {
                    int vi = indices[i];
                    if ((uint)vi >= (uint)vertices.Length) // safety against malformed index buffers
                        continue;

                    Vector3 vLocal = vertices[vi];
                    Vector3 vWorld = localToWorld.MultiplyPoint3x4(vLocal);

                    // expand local bounds
                    if (vLocal.x < minLocal.x) minLocal.x = vLocal.x;
                    if (vLocal.y < minLocal.y) minLocal.y = vLocal.y;
                    if (vLocal.z < minLocal.z) minLocal.z = vLocal.z;
                    if (vLocal.x > maxLocal.x) maxLocal.x = vLocal.x;
                    if (vLocal.y > maxLocal.y) maxLocal.y = vLocal.y;
                    if (vLocal.z > maxLocal.z) maxLocal.z = vLocal.z;

                    // expand world bounds
                    if (vWorld.x < minWorld.x) minWorld.x = vWorld.x;
                    if (vWorld.y < minWorld.y) minWorld.y = vWorld.y;
                    if (vWorld.z < minWorld.z) minWorld.z = vWorld.z;
                    if (vWorld.x > maxWorld.x) maxWorld.x = vWorld.x;
                    if (vWorld.y > maxWorld.y) maxWorld.y = vWorld.y;
                    if (vWorld.z > maxWorld.z) maxWorld.z = vWorld.z;
                }

                // If no valid indices, skip
                if (!IsFinite(minLocal) || !IsFinite(maxLocal))
                    continue;

                var localCenter = (minLocal + maxLocal) * 0.5f;
                var localSize = (maxLocal - minLocal);
                var worldCenter = (minWorld + maxWorld) * 0.5f;
                var worldSize = (maxWorld - minWorld);

                sink.Add(new SubmeshBoundsInfo
                {
                    renderer = renderer,
                    localBounds = new Bounds(localCenter, localSize),
                    worldBounds = new Bounds(worldCenter, worldSize),
                });
            }
        }

        private static bool IsFinite(in Vector3 v)
        {
            return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        }
    }
}
