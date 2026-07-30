using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GH.World.Editor
{
    /// <summary>
    /// Adds Terrain Tree collider shapes to AI Navigation bakes without converting
    /// Terrain Tree instances into persistent GameObjects.
    /// </summary>
    public static class TerrainTreeNavMeshBaker
    {
        private const string MenuRoot = "Tools/GH/NavMesh/";
        private const string ProxyRootName = "__TerrainTreeNavMeshBakeProxies";
        private const int NotWalkableArea = 1;
        private const float HorizontalPadding = 0.05f;
        private const float VerticalPadding = 0.4f;
        private const float GroundOverlap = 0.15f;

        private static readonly List<BakeJob> BakeJobs = new List<BakeJob>();
        private static GameObject proxyRoot;
        private static bool isBaking;
        private static int proxyCount;
        private static int treeCount;
        private static int skippedTreeCount;

        [MenuItem(MenuRoot + "Bake Active Scene With Terrain Trees", priority = 100)]
        public static void BakeActiveSceneWithTerrainTrees()
        {
            if (isBaking)
            {
                Debug.LogWarning("[Terrain Tree NavMesh] A bake is already in progress.");
                return;
            }

            if (Application.isPlaying)
            {
                Debug.LogWarning("[Terrain Tree NavMesh] Exit Play Mode before baking.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Terrain Tree NavMesh] No valid active scene is loaded.");
                return;
            }

            NavMeshSurface[] surfaces = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NavMeshSurface>(true))
                .Where(surface => surface.enabled && surface.navMeshData != null)
                .ToArray();

            if (surfaces.Length == 0)
            {
                Debug.LogError("[Terrain Tree NavMesh] No enabled NavMeshSurface with baked data was found.");
                return;
            }

            try
            {
                CreateTreeModifierVolumes(scene);
                if (proxyCount == 0)
                {
                    throw new InvalidOperationException(
                        "No Terrain Tree collider proxies were created. " +
                        "Check that the Tree Prototype prefabs contain enabled colliders.");
                }

                BakeJobs.Clear();
                foreach (NavMeshSurface surface in surfaces)
                {
                    AsyncOperation operation = surface.UpdateNavMesh(surface.navMeshData);
                    BakeJobs.Add(new BakeJob(surface, operation));
                }

                isBaking = true;
                EditorApplication.update += PollBakeOperations;
                Debug.Log(
                    $"[Terrain Tree NavMesh] Bake started. surfaces={surfaces.Length}, " +
                    $"trees={treeCount}, modifierVolumes={proxyCount}, skippedTrees={skippedTreeCount}");
            }
            catch (Exception exception)
            {
                Cleanup();
                Debug.LogException(exception);
            }
        }

        [MenuItem(MenuRoot + "Report Active Scene Terrain Trees", priority = 101)]
        public static void ReportActiveSceneTerrainTrees()
        {
            Scene scene = SceneManager.GetActiveScene();
            Terrain[] terrains = GetSceneTerrains(scene);
            int totalTrees = terrains.Sum(terrain => terrain.terrainData?.treeInstanceCount ?? 0);
            int prototypesWithColliders = 0;
            int prototypesWithoutColliders = 0;

            foreach (Terrain terrain in terrains)
            {
                if (terrain.terrainData == null)
                    continue;

                foreach (TreePrototype prototype in terrain.terrainData.treePrototypes)
                {
                    if (GetSupportedColliders(prototype.prefab).Length > 0)
                        prototypesWithColliders++;
                    else
                        prototypesWithoutColliders++;
                }
            }

            Debug.Log(
                $"[Terrain Tree NavMesh] Scene report: terrains={terrains.Length}, trees={totalTrees}, " +
                $"prototypesWithColliders={prototypesWithColliders}, " +
                $"prototypesWithoutColliders={prototypesWithoutColliders}");
        }

        [MenuItem(MenuRoot + "Bake Active Scene With Terrain Trees", true)]
        private static bool ValidateBakeMenu()
        {
            return !Application.isPlaying && !isBaking;
        }

        private static void CreateTreeModifierVolumes(Scene scene)
        {
            CleanupProxyRoot();

            proxyRoot = new GameObject(ProxyRootName)
            {
                hideFlags = HideFlags.HideAndDontSave,
                tag = "EditorOnly"
            };
            SceneManager.MoveGameObjectToScene(proxyRoot, scene);

            proxyCount = 0;
            treeCount = 0;
            skippedTreeCount = 0;

            Terrain[] terrains = GetSceneTerrains(scene);
            foreach (Terrain terrain in terrains)
            {
                TerrainData terrainData = terrain.terrainData;
                if (terrainData == null)
                    continue;

                TreePrototype[] prototypes = terrainData.treePrototypes;
                TreeInstance[] instances = terrainData.treeInstances;
                Dictionary<int, Collider[]> colliderCache = new Dictionary<int, Collider[]>();

                for (int treeIndex = 0; treeIndex < instances.Length; treeIndex++)
                {
                    TreeInstance instance = instances[treeIndex];
                    treeCount++;

                    if (instance.prototypeIndex < 0 || instance.prototypeIndex >= prototypes.Length)
                    {
                        skippedTreeCount++;
                        continue;
                    }

                    if (!colliderCache.TryGetValue(instance.prototypeIndex, out Collider[] colliders))
                    {
                        colliders = GetSupportedColliders(prototypes[instance.prototypeIndex].prefab);
                        colliderCache.Add(instance.prototypeIndex, colliders);
                    }

                    if (colliders.Length == 0)
                    {
                        skippedTreeCount++;
                        continue;
                    }

                    GameObject prototypeRoot = prototypes[instance.prototypeIndex].prefab;
                    Matrix4x4 treeMatrix = GetTreeWorldMatrix(terrain, instance);
                    Matrix4x4 prototypeWorldToLocal = prototypeRoot.transform.worldToLocalMatrix;

                    for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                    {
                        Collider collider = colliders[colliderIndex];
                        Matrix4x4 colliderMatrix =
                            treeMatrix * prototypeWorldToLocal * collider.transform.localToWorldMatrix;

                        if (!TryGetColliderVolume(collider, colliderMatrix, out Vector3 center, out Quaternion rotation,
                                out Vector3 size))
                        {
                            continue;
                        }

                        GameObject proxy = new GameObject(
                            $"Tree_{terrain.GetInstanceID()}_{treeIndex}_{colliderIndex}")
                        {
                            hideFlags = HideFlags.HideAndDontSave,
                            layer = terrain.gameObject.layer,
                            tag = "EditorOnly"
                        };

                        proxy.transform.SetParent(proxyRoot.transform, false);
                        proxy.transform.SetPositionAndRotation(center + Vector3.down * GroundOverlap, rotation);
                        proxy.transform.localScale = Vector3.one;

                        NavMeshModifierVolume volume = proxy.AddComponent<NavMeshModifierVolume>();
                        volume.center = Vector3.zero;
                        volume.size = new Vector3(
                            Mathf.Max(0.05f, size.x + HorizontalPadding * 2f),
                            Mathf.Max(0.05f, size.y + VerticalPadding),
                            Mathf.Max(0.05f, size.z + HorizontalPadding * 2f));
                        volume.area = NotWalkableArea;
                        proxyCount++;
                    }
                }
            }
        }

        private static Terrain[] GetSceneTerrains(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return Array.Empty<Terrain>();

            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Terrain>(true))
                .Where(terrain => terrain.enabled && terrain.gameObject.activeInHierarchy)
                .ToArray();
        }

        private static Collider[] GetSupportedColliders(GameObject prototype)
        {
            if (prototype == null)
                return Array.Empty<Collider>();

            return prototype.GetComponentsInChildren<Collider>(true)
                .Where(collider =>
                    collider != null &&
                    collider.enabled &&
                    !collider.isTrigger &&
                    IsActiveInPrefabHierarchy(collider.transform, prototype.transform) &&
                    (collider is BoxCollider ||
                     collider is CapsuleCollider ||
                     collider is SphereCollider ||
                     collider is MeshCollider))
                .ToArray();
        }

        private static bool IsActiveInPrefabHierarchy(Transform current, Transform root)
        {
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                    return false;

                if (current == root)
                    return true;

                current = current.parent;
            }

            return false;
        }

        private static Matrix4x4 GetTreeWorldMatrix(Terrain terrain, TreeInstance instance)
        {
            Vector3 localPosition = Vector3.Scale(instance.position, terrain.terrainData.size);
            Quaternion localRotation = Quaternion.Euler(0f, instance.rotation * Mathf.Rad2Deg, 0f);
            Vector3 localScale = new Vector3(instance.widthScale, instance.heightScale, instance.widthScale);
            return terrain.transform.localToWorldMatrix * Matrix4x4.TRS(localPosition, localRotation, localScale);
        }

        private static bool TryGetColliderVolume(
            Collider collider,
            Matrix4x4 colliderMatrix,
            out Vector3 worldCenter,
            out Quaternion worldRotation,
            out Vector3 worldSize)
        {
            Vector3 localCenter;
            Vector3 localSize;

            switch (collider)
            {
                case BoxCollider box:
                    localCenter = box.center;
                    localSize = box.size;
                    break;

                case SphereCollider sphere:
                    localCenter = sphere.center;
                    localSize = Vector3.one * (sphere.radius * 2f);
                    break;

                case CapsuleCollider capsule:
                    localCenter = capsule.center;
                    float diameter = capsule.radius * 2f;
                    localSize = Vector3.one * diameter;
                    localSize[capsule.direction] = Mathf.Max(capsule.height, diameter);
                    break;

                case MeshCollider meshCollider when meshCollider.sharedMesh != null:
                    localCenter = meshCollider.sharedMesh.bounds.center;
                    localSize = meshCollider.sharedMesh.bounds.size;
                    break;

                default:
                    worldCenter = default;
                    worldRotation = default;
                    worldSize = default;
                    return false;
            }

            Vector3 axisX = colliderMatrix.MultiplyVector(Vector3.right);
            Vector3 axisY = colliderMatrix.MultiplyVector(Vector3.up);
            Vector3 axisZ = colliderMatrix.MultiplyVector(Vector3.forward);
            Vector3 scale = new Vector3(axisX.magnitude, axisY.magnitude, axisZ.magnitude);

            if (scale.x <= Mathf.Epsilon || scale.y <= Mathf.Epsilon || scale.z <= Mathf.Epsilon)
            {
                worldCenter = default;
                worldRotation = default;
                worldSize = default;
                return false;
            }

            worldCenter = colliderMatrix.MultiplyPoint3x4(localCenter);
            worldRotation = Quaternion.LookRotation(axisZ / scale.z, axisY / scale.y);
            worldSize = Vector3.Scale(localSize, scale);
            return true;
        }

        private static void PollBakeOperations()
        {
            if (!isBaking)
                return;

            if (BakeJobs.Count == 0)
            {
                FinishBake();
                return;
            }

            float progress = BakeJobs.Average(job => job.Operation == null ? 1f : job.Operation.progress);
            EditorUtility.DisplayProgressBar(
                "Terrain Tree NavMesh Bake",
                $"Baking {BakeJobs.Count} NavMesh surfaces with {proxyCount} tree obstacle volumes...",
                progress);

            if (BakeJobs.Any(job => job.Operation != null && !job.Operation.isDone))
                return;

            FinishBake();
        }

        private static void FinishBake()
        {
            EditorApplication.update -= PollBakeOperations;

            foreach (BakeJob job in BakeJobs)
            {
                if (job.Surface != null && job.Surface.navMeshData != null)
                    EditorUtility.SetDirty(job.Surface.navMeshData);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[Terrain Tree NavMesh] Bake complete. surfaces={BakeJobs.Count}, " +
                $"trees={treeCount}, modifierVolumes={proxyCount}, skippedTrees={skippedTreeCount}. " +
                "Temporary proxies were removed.");

            Cleanup();
            SceneView.RepaintAll();
        }

        private static void Cleanup()
        {
            EditorApplication.update -= PollBakeOperations;
            EditorUtility.ClearProgressBar();
            BakeJobs.Clear();
            CleanupProxyRoot();
            isBaking = false;
        }

        private static void CleanupProxyRoot()
        {
            if (proxyRoot != null)
                Object.DestroyImmediate(proxyRoot);

            proxyRoot = null;
        }

        private readonly struct BakeJob
        {
            public readonly NavMeshSurface Surface;
            public readonly AsyncOperation Operation;

            public BakeJob(NavMeshSurface surface, AsyncOperation operation)
            {
                Surface = surface;
                Operation = operation;
            }
        }
    }
}
