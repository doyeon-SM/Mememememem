#if UNITY_EDITOR
using System.IO;
using KMS.Effects;
using KMS.Harvesting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace KMS.EditorTools
{
    public static class KMSMemHitDustSetupTool
    {
        private const string EffectPrefabPath =
            "Assets/KMS/2.Prefabs/Effects/P_MemHitDust.prefab";

        private const string TexturePath =
            "Assets/KMS/4.Materials/Effects/T_MemHitDustSoft.asset";

        private const string MaterialPath =
            "Assets/KMS/4.Materials/Effects/M_MemHitDust.mat";

        private const string PlayerPrefabPath =
            "Assets/KMS/2.Prefabs/0720_Player_KMS.prefab";

        [MenuItem("KMS/Setup/Mem Hit Dust")]
        public static void Run()
        {
            EnsureFolder("Assets/KMS/2.Prefabs/Effects");
            EnsureFolder("Assets/KMS/4.Materials/Effects");

            Texture2D texture = CreateOrUpdateSoftParticleTexture();
            Material material = CreateOrUpdateMaterial(texture);
            ParticleSystem effectPrefab = CreateOrUpdateEffectPrefab(material);
            ConfigurePlayerPrefab(effectPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KMSMemHitDustSetupTool] Mem hit dust setup complete.");
        }

        private static Texture2D CreateOrUpdateSoftParticleTexture()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null)
            {
                texture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
                {
                    name = "T_MemHitDustSoft",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                AssetDatabase.CreateAsset(texture, TexturePath);
            }

            const int size = 64;
            var pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalizedDistance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.SmoothStep(1f, 0f, normalizedDistance);
                    alpha *= alpha;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static Material CreateOrUpdateMaterial(Texture2D texture)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader =
                    Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                    Shader.Find("Particles/Standard Unlit") ??
                    Shader.Find("Sprites/Default");

                if (shader == null)
                {
                    throw new System.InvalidOperationException(
                        "[KMSMemHitDustSetupTool] A compatible particle shader was not found.");
                }

                material = new Material(shader)
                {
                    name = "M_MemHitDust"
                };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.mainTexture = texture;
            material.color = Color.white;

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;

            EditorUtility.SetDirty(material);
            return material;
        }

        private static ParticleSystem CreateOrUpdateEffectPrefab(Material material)
        {
            GameObject root = new GameObject("P_MemHitDust");
            try
            {
                ParticleSystem particles = root.AddComponent<ParticleSystem>();
                ConfigureParticles(particles, material);

                PrefabUtility.SaveAsPrefabAsset(root, EffectPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            ParticleSystem prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(EffectPrefabPath)
                    ?.GetComponent<ParticleSystem>();

            if (prefab == null)
            {
                throw new System.InvalidOperationException(
                    "[KMSMemHitDustSetupTool] Failed to create the effect prefab.");
            }

            return prefab;
        }

        private static void ConfigureParticles(ParticleSystem particles, Material material)
        {
            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.52f, 0.43f, 0.32f, 0.72f),
                new Color(0.76f, 0.69f, 0.56f, 0.62f));
            main.gravityModifier = 0.08f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 16;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(
                    0f,
                    new ParticleSystem.MinMaxCurve(6f, 9f))
            });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.035f;
            shape.radiusThickness = 1f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var colorGradient = new Gradient();
            colorGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.9f, 0.86f, 0.78f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = colorGradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.55f),
                    new Keyframe(0.25f, 1f),
                    new Keyframe(1f, 1.35f)));

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.75f;
            noise.scrollSpeed = 0.2f;
            noise.damping = true;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = material;
            renderer.sortingFudge = 1f;
        }

        private static void ConfigurePlayerPrefab(ParticleSystem effectPrefab)
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                PlayerHarvestController controller =
                    playerRoot.GetComponentInChildren<PlayerHarvestController>(true);

                if (controller == null)
                {
                    throw new System.InvalidOperationException(
                        "[KMSMemHitDustSetupTool] PlayerHarvestController was not found.");
                }

                GameObject controllerObject = controller.gameObject;
                KMSMemHitDustPool pool =
                    controllerObject.GetComponent<KMSMemHitDustPool>() ??
                    controllerObject.AddComponent<KMSMemHitDustPool>();

                var poolObject = new SerializedObject(pool);
                poolObject.FindProperty("effectPrefab").objectReferenceValue = effectPrefab;
                poolObject.FindProperty("capacity").intValue = 6;
                poolObject.FindProperty("surfaceOffset").floatValue = 0.02f;
                poolObject.ApplyModifiedPropertiesWithoutUndo();

                var controllerObjectSerialized = new SerializedObject(controller);
                controllerObjectSerialized.FindProperty("memHitDustPool").objectReferenceValue = pool;
                controllerObjectSerialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            {
                throw new System.ArgumentException($"Invalid asset folder path: {path}");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
#endif
