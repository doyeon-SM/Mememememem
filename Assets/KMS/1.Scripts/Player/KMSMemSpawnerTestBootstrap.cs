using MemSystem.Spawn;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace KMS.Player
{
    /// <summary>
    /// Supplies the KMS test scene with a runtime NavMesh for the shared Mem spawner.
    /// The production spawner and Pikachu assets remain untouched.
    /// </summary>
    public static class KMSMemSpawnerTestBootstrap
    {
        private const string TestSceneName = "TestScene_KMS";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeTestScene()
        {
            if (SceneManager.GetActiveScene().name != TestSceneName)
            {
                return;
            }

            var spawner = Object.FindFirstObjectByType<MemSpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("[KMS Mem Test] MemSpawner를 찾지 못해 NavMesh 생성을 건너뜁니다.");
                return;
            }

            var setupObject = new GameObject("KMS_MemSpawner_NavMesh_Runtime");
            setupObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var surface = setupObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = 1 << 0;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
            surface.BuildNavMesh();

            if (NavMesh.SamplePosition(spawner.transform.position, out var hit, 5f, NavMesh.AllAreas))
            {
                Debug.Log($"[KMS Mem Test] 런타임 NavMesh 준비 완료. 스포너 기준점: {hit.position}");
            }
            else
            {
                Debug.LogError("[KMS Mem Test] 스포너 주변에 NavMesh가 생성되지 않았습니다. Layer 0 바닥 콜라이더를 확인하세요.");
            }
        }
    }
}
