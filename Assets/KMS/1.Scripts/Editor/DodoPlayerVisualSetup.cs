using System.Linq;
using KMS;
using UnityEditor;
using UnityEngine;

public static class DodoPlayerVisualSetup
{
    private const string PlayerPrefabPath = "Assets/KMS/2.Prefabs/0708_Player_KMS.prefab";
    private const string DodoModelPath = "Assets/KMS/4.Animation/Dodo/Models/Dodo_T-Pose.fbx";
    private const string DodoControllerPath = "Assets/KMS/4.Animation/Dodo/Controllers/KMS_DodoAnimator.controller";
    private const string DodoMaterialPath = "Assets/KMS/4.Animation/Dodo/Materials/M_Dodo.mat";
    private const string VisualRootName = "PlayerVisual_Dodo";
    private const string LegacyVisualName = "Armature_Core";

    [MenuItem("KMS/Setup Dodo Player Visual")]
    public static void Setup()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        try
        {
            DisableLegacyVisual(prefabRoot.transform);
            Animator dodoAnimator = CreateDodoVisual(prefabRoot.transform);
            AssignPlayerMovementAnimator(prefabRoot, dodoAnimator);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            Debug.Log($"Dodo player visual setup complete: {PlayerPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void DisableLegacyVisual(Transform root)
    {
        Transform legacyVisual = FindDeepChild(root, LegacyVisualName);
        if (legacyVisual != null)
        {
            legacyVisual.gameObject.SetActive(false);
        }
    }

    private static Animator CreateDodoVisual(Transform root)
    {
        Transform existingVisual = FindDeepChild(root, VisualRootName);
        if (existingVisual != null)
        {
            Object.DestroyImmediate(existingVisual.gameObject);
        }

        GameObject visualRoot = new GameObject(VisualRootName);
        visualRoot.transform.SetParent(root, false);
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;
        visualRoot.transform.localScale = Vector3.one;

        GameObject dodoModel = AssetDatabase.LoadAssetAtPath<GameObject>(DodoModelPath);
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DodoControllerPath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(DodoMaterialPath);
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(DodoModelPath).OfType<Avatar>().FirstOrDefault();

        if (dodoModel == null)
        {
            throw new System.InvalidOperationException($"Missing dodo model: {DodoModelPath}");
        }

        if (controller == null)
        {
            throw new System.InvalidOperationException($"Missing dodo animator controller: {DodoControllerPath}");
        }

        GameObject dodoInstance = (GameObject)PrefabUtility.InstantiatePrefab(dodoModel, visualRoot.transform);
        dodoInstance.name = "Dodo_T-Pose";
        dodoInstance.transform.localPosition = Vector3.zero;
        dodoInstance.transform.localRotation = Quaternion.identity;
        dodoInstance.transform.localScale = Vector3.one;

        Animator animator = dodoInstance.GetComponent<Animator>();
        if (animator == null)
        {
            animator = dodoInstance.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.avatar = avatar;

        if (material != null)
        {
            foreach (Renderer renderer in dodoInstance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
            }
        }

        return animator;
    }

    private static void AssignPlayerMovementAnimator(GameObject prefabRoot, Animator animator)
    {
        PlayerMovement movement = prefabRoot.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            throw new System.InvalidOperationException("0708_Player_KMS is missing PlayerMovement.");
        }

        SerializedObject serializedMovement = new SerializedObject(movement);
        SerializedProperty animatorProperty = serializedMovement.FindProperty("animator");
        if (animatorProperty == null)
        {
            throw new System.InvalidOperationException("PlayerMovement.animator serialized field was not found.");
        }

        animatorProperty.objectReferenceValue = animator;
        serializedMovement.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform result = FindDeepChild(child, childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
