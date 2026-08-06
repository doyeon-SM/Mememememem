using KMS;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace KMS.Editor
{
    public static class KMSCapsuleSparkleMaterialSetup
    {
        private const string MaterialFolder = "Assets/KMS/3.Materials";
        private const string MaterialPath =
            MaterialFolder + "/KMS_CapsuleSuccessSparkle.mat";
        private const string TexturePath =
            MaterialFolder + "/KMS_CapsuleSuccessSparkleTexture.asset";
        private const string CapsulePrefabPath =
            "Assets/KMS/2.Prefabs/KMS_ShabbyCaptureCapsule.prefab";

        [MenuItem("KMS/Setup Capsule Success Sparkle Material")]
        public static void Apply()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                throw new System.InvalidOperationException(
                    "Universal Render Pipeline/Particles/Unlit shader was not found.");
            }

            EnsureFolder("Assets/KMS", "3.Materials");
            Texture2D sparkleTexture = CreateOrUpdateSparkleTexture();

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.name = "KMS_CapsuleSuccessSparkle";
            ConfigureMaterial(material, sparkleTexture);
            EditorUtility.SetDirty(material);

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CapsulePrefabPath);
            try
            {
                KMSCapsuleCaptureVisual visual =
                    prefabRoot.GetComponent<KMSCapsuleCaptureVisual>();
                if (visual == null)
                {
                    throw new MissingComponentException(
                        $"{CapsulePrefabPath} has no KMSCapsuleCaptureVisual.");
                }

                SerializedObject serializedVisual = new SerializedObject(visual);
                SerializedProperty sparkleMaterial =
                    serializedVisual.FindProperty("successSparkleMaterial");
                sparkleMaterial.objectReferenceValue = material;
                serializedVisual.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, CapsulePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[KMSCapsuleSparkleMaterialSetup] Created and assigned {MaterialPath}.");
        }

        private static Texture2D CreateOrUpdateSparkleTexture()
        {
            const int textureSize = 64;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null)
            {
                texture = new Texture2D(
                    textureSize,
                    textureSize,
                    TextureFormat.RGBA32,
                    false,
                    true)
                {
                    name = "KMS_CapsuleSuccessSparkleTexture",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                AssetDatabase.CreateAsset(texture, TexturePath);
            }

            Color[] pixels = new Color[textureSize * textureSize];
            Vector2 center = Vector2.one * ((textureSize - 1) * 0.5f);
            float radius = textureSize * 0.5f;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float normalizedDistance =
                        Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Pow(
                        Mathf.Clamp01(1f - normalizedDistance),
                        2.2f);
                    pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static void ConfigureMaterial(Material material, Texture2D sparkleTexture)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", sparkleTexture);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void EnsureFolder(string parentFolder, string childFolder)
        {
            string path = $"{parentFolder}/{childFolder}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parentFolder, childFolder);
            }
        }
    }
}
