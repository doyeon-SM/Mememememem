using KMS.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KMS.EditorTools
{
    public static class KMSForgeTestSceneSetup
    {
        private const string ScenePath = "Assets/KMS/0.Scenes/TestScene_KMS.unity";
        private const string ForgeManagerPrefabPath = "Assets/2.Prefabs/Manager/ForgeManager.prefab";

        [MenuItem("KMS/Setup Forge Test Axe")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EnsureForgeManager(scene);
            EnsureTestSeeder(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[KMSForgeTestSceneSetup] TestScene_KMS에 허름한 도끼 +5 지급 환경을 구성했습니다.");
        }

        private static void EnsureForgeManager(Scene scene)
        {
            if (Object.FindFirstObjectByType<HDY.Forge.ForgeManager>() != null) return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ForgeManagerPrefabPath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException($"ForgeManager 프리팹을 찾을 수 없습니다: {ForgeManagerPrefabPath}");
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                throw new System.InvalidOperationException("ForgeManager 프리팹 인스턴스 생성에 실패했습니다.");
            }

            instance.name = "ForgeManager";
        }

        private static void EnsureTestSeeder(Scene scene)
        {
            KMSForgeTestInventorySeeder seeder = Object.FindFirstObjectByType<KMSForgeTestInventorySeeder>();
            if (seeder == null)
            {
                GameObject root = new GameObject("KMS_ForgeTestInventorySeeder");
                SceneManager.MoveGameObjectToScene(root, scene);
                seeder = root.AddComponent<KMSForgeTestInventorySeeder>();
            }

            seeder.Configure("tool_shabby_axe", 5);
            EditorUtility.SetDirty(seeder);
        }
    }
}
