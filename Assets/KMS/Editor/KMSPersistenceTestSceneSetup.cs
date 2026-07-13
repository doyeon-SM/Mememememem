using System.Collections.Generic;
using System.Linq;
using KMS.Persistence;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class KMSPersistenceTestSceneSetup
{
    private const string TestScenePath = "Assets/KMS/0.Scenes/TestScene_KMS.unity";
    private const string Test2ScenePath = "Assets/KMS/0.Scenes/Test2Scene_KMS.unity";
    private const string PlayerPrefabPath = "Assets/KMS/2.Prefabs/0708_Player_KMS.prefab";

    public static void SetupAndValidate()
    {
        SetupTestScene();
        SetupTest2Scene();
        EnsureBuildScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[KMSPersistenceTestSceneSetup] 테스트 씬 구성 완료");
    }

    private static void SetupTestScene()
    {
        Scene scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
        CreateOrUpdatePortal(scene, "Portal_To_Test2Scene_KMS", new Vector3(0f, 1f, 8f), "Test2Scene_KMS");
        EditorSceneManager.SaveScene(scene);
    }

    private static void SetupTest2Scene()
    {
        Scene scene = EditorSceneManager.OpenScene(Test2ScenePath, OpenSceneMode.Single);

        GameObject ground = FindRoot(scene, "Test2_Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Test2_Ground";
            SceneManager.MoveGameObjectToScene(ground, scene);
        }
        ground.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        ground.transform.localScale = new Vector3(4f, 1f, 4f);

        GameObject player = FindRoot(scene, "0708_Player_KMS");
        if (player == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.name = "0708_Player_KMS";
        }
        player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, 0f), Quaternion.identity);

        if (player.GetComponent<PlayerPersistenceDebugHUD>() == null)
        {
            player.AddComponent<PlayerPersistenceDebugHUD>();
        }

        Camera mainCamera = Object.FindFirstObjectByType<Camera>();
        if (mainCamera != null)
        {
            mainCamera.tag = "MainCamera";
            AssignCameraReferences(player, mainCamera.transform);
        }

        CreateOrUpdatePortal(scene, "Portal_Back_To_TestScene_KMS", new Vector3(0f, 1f, 8f), "TestScene_KMS");
        EditorSceneManager.SaveScene(scene);
    }

    private static void AssignCameraReferences(GameObject player, Transform cameraTransform)
    {
        foreach (MonoBehaviour component in player.GetComponents<MonoBehaviour>())
        {
            if (component == null) continue;

            var serialized = new SerializedObject(component);
            SerializedProperty cameraProperty = serialized.FindProperty("cameraTransform");
            if (cameraProperty == null || cameraProperty.propertyType != SerializedPropertyType.ObjectReference) continue;

            cameraProperty.objectReferenceValue = cameraTransform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void CreateOrUpdatePortal(Scene scene, string objectName, Vector3 position, string targetScene)
    {
        GameObject portal = FindRoot(scene, objectName);
        if (portal == null)
        {
            portal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            portal.name = objectName;
            SceneManager.MoveGameObjectToScene(portal, scene);
        }

        portal.transform.SetPositionAndRotation(position, Quaternion.identity);
        portal.transform.localScale = new Vector3(3f, 2f, 1f);

        BoxCollider trigger = portal.GetComponent<BoxCollider>();
        trigger.isTrigger = true;

        Rigidbody body = portal.GetComponent<Rigidbody>();
        if (body == null) body = portal.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        PlayerScenePortal portalComponent = portal.GetComponent<PlayerScenePortal>();
        if (portalComponent == null) portalComponent = portal.AddComponent<PlayerScenePortal>();

        var serializedPortal = new SerializedObject(portalComponent);
        serializedPortal.FindProperty("targetSceneName").stringValue = targetScene;
        serializedPortal.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject FindRoot(Scene scene, string objectName)
    {
        return scene.GetRootGameObjects().FirstOrDefault(root => root.name == objectName);
    }

    private static void EnsureBuildScenes()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        AddEnabledSceneIfMissing(scenes, TestScenePath);
        AddEnabledSceneIfMissing(scenes, Test2ScenePath);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void AddEnabledSceneIfMissing(List<EditorBuildSettingsScene> scenes, string path)
    {
        EditorBuildSettingsScene existing = scenes.FirstOrDefault(scene => scene.path == path);
        if (existing != null)
        {
            existing.enabled = true;
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(path, true));
    }
}
