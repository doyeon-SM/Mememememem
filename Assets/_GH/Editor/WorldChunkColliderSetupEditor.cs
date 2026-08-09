using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class WorldChunkColliderSetupEditor
{
    private const string TargetScenePath = "Assets/0.Scene/Main_World_3.unity";
    private const string SessionKey = "WorldChunkColliderSetupEditor.MainWorld3Applied.v1";

    static WorldChunkColliderSetupEditor()
    {
        EditorApplication.delayCall += ApplyToTargetSceneOnce;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    [MenuItem("Tools/World Chunks/Add Missing Colliders To Loaded Chunks")]
    private static void ApplyToLoadedChunksFromMenu()
    {
        WorldChunkColliderBuildResult result = ApplyToLoadedScenes(default, false);
        Debug.Log($"[WorldChunk Colliders] Added {result.TotalCount} collider(s): " +
                  $"Mesh {result.MeshColliderCount}, Box {result.BoxColliderCount}, " +
                  $"Capsule {result.CapsuleColliderCount}.");
    }

    private static void ApplyToTargetSceneOnce()
    {
        if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        Scene targetScene = default;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.path == TargetScenePath)
            {
                targetScene = scene;
                break;
            }
        }

        if (!targetScene.IsValid())
        {
            return;
        }

        SessionState.SetBool(SessionKey, true);
        WorldChunkColliderBuildResult result = ApplyToLoadedScenes(targetScene, true);
        if (result.TotalCount > 0)
        {
            Debug.Log($"[WorldChunk Colliders] Main_World_3 received {result.TotalCount} missing collider(s): " +
                      $"Mesh {result.MeshColliderCount}, Box {result.BoxColliderCount}, " +
                      $"Capsule {result.CapsuleColliderCount}. Existing collider hierarchies were unchanged.");
        }
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == TargetScenePath)
        {
            EditorApplication.delayCall += ApplyToTargetSceneOnce;
        }
    }

    private static WorldChunkColliderBuildResult ApplyToLoadedScenes(Scene onlyScene, bool restrictToOneScene)
    {
        WorldChunkColliderBuildResult total = default;
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add Missing World Chunk Colliders");

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || (restrictToOneScene && scene != onlyScene))
            {
                continue;
            }

            WorldChunkColliderBuildResult sceneTotal = default;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                WorldChunk[] chunks = root.GetComponentsInChildren<WorldChunk>(true);
                foreach (WorldChunk chunk in chunks)
                {
                    sceneTotal += chunk.AddMissingColliders(true);
                }
            }

            if (sceneTotal.TotalCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                total += sceneTotal;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        return total;
    }
}
