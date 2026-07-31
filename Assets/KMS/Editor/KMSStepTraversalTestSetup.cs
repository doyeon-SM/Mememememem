using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KMS.EditorTools
{
    public static class KMSStepTraversalTestSetup
    {
        private const string ScenePath = "Assets/KMS/0.Scenes/TestScene_KMS.unity";
        private const string PlayerPrefabPath = "Assets/KMS/2.Prefabs/0720_Player_KMS.prefab";
        private const string CourseRootName = "StepTraversalTestCourse";
        private const string MaterialFolder = "Assets/KMS/3.Materials/StepTraversalTest";

        private static readonly float[] StepHeights =
        {
            0.05f,
            0.10f,
            0.15f,
            0.20f,
            0.25f,
            0.30f,
            0.35f,
            0.40f
        };

        [MenuItem("KMS/Setup/Build Step Traversal Test Course")]
        public static void Run()
        {
            ConfigurePlayerPrefab();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject existingRoot = GameObject.Find(CourseRootName);
            if (existingRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(existingRoot);
            }

            Material passMaterial = GetOrCreateMaterial(
                "Step_Pass.mat",
                new Color(0.20f, 0.72f, 0.38f));
            Material limitMaterial = GetOrCreateMaterial(
                "Step_Limit.mat",
                new Color(1.00f, 0.70f, 0.12f));
            Material blockedMaterial = GetOrCreateMaterial(
                "Step_Blocked.mat",
                new Color(0.88f, 0.24f, 0.20f));

            GameObject courseRoot = new GameObject(CourseRootName);
            courseRoot.transform.position = new Vector3(0f, 0f, -12f);

            CreateApproachGuide(courseRoot.transform);

            for (int i = 0; i < StepHeights.Length; i++)
            {
                float height = StepHeights[i];
                float x = (i - (StepHeights.Length - 1) * 0.5f) * 3.2f;
                Material material = Mathf.Approximately(height, 0.25f)
                    ? limitMaterial
                    : height < 0.25f
                        ? passMaterial
                        : blockedMaterial;

                CreateStep(courseRoot.transform, x, height, material);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Failed to save TestScene_KMS.");
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[KMSStepTraversalTestSetup] Built 0.05m-0.40m step course and configured the player for 0.25m traversal.");
        }

        public static void RunFromCommandLine() => Run();

        private static void ConfigurePlayerPrefab()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                PlayerMovement movement = prefabRoot.GetComponent<PlayerMovement>();
                Require(movement != null, "PlayerMovement is missing from 0720_Player_KMS.");

                Transform visualRoot = prefabRoot.transform.Find("PlayerVisual_Dodo");
                Require(visualRoot != null, "PlayerVisual_Dodo is missing from 0720_Player_KMS.");

                SerializedObject serializedMovement = new SerializedObject(movement);
                serializedMovement.FindProperty("maxStepHeight").floatValue = 0.25f;
                serializedMovement.FindProperty("stepSmoothTime").floatValue = 0.08f;
                serializedMovement.FindProperty("stepVisualRoot").objectReferenceValue = visualRoot;
                serializedMovement.ApplyModifiedPropertiesWithoutUndo();

                CharacterController controller = prefabRoot.GetComponent<CharacterController>();
                Require(controller != null, "CharacterController is missing from 0720_Player_KMS.");
                controller.stepOffset = 0.25f;

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void CreateApproachGuide(Transform parent)
        {
            GameObject guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guide.name = "Approach_From_This_Side";
            guide.transform.SetParent(parent, false);
            guide.transform.localPosition = new Vector3(0f, 0.01f, -4.5f);
            guide.transform.localScale = new Vector3(25f, 0.02f, 0.12f);

            Collider collider = guide.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        }

        private static void CreateStep(
            Transform parent,
            float x,
            float height,
            Material material)
        {
            string heightLabel = height.ToString("0.00");
            GameObject laneRoot = new GameObject($"Step_{heightLabel}m");
            laneRoot.transform.SetParent(parent, false);
            laneRoot.transform.localPosition = new Vector3(x, 0f, 0f);

            GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
            step.name = $"Cube_{heightLabel}m";
            step.transform.SetParent(laneRoot.transform, false);
            step.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            step.transform.localScale = new Vector3(2.5f, height, 2.5f);
            step.GetComponent<MeshRenderer>().sharedMaterial = material;

            GameObject labelObject = new GameObject($"Label_{heightLabel}m");
            labelObject.transform.SetParent(laneRoot.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.04f, -2.0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = $"{heightLabel} m";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.12f;
            label.fontSize = 48;
            label.color = Color.white;
        }

        private static Material GetOrCreateMaterial(string fileName, Color color)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{fileName}";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                Require(shader != null, "No supported Lit shader was found.");

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
