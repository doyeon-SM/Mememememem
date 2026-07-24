using System.Collections.Generic;
using System.IO;
using KMS.InventoryDuped;
using UnityEditor;
using UnityEngine;

namespace KMS.EditorTools
{
    public static class HeldItemSetupTool
    {
        private const string HeldPrefabFolder = "Assets/KMS/2.Prefabs/HeldItems";
        private const string TableFolder = "Assets/KMS/3.SO/HeldItems";
        private const string TablePath = TableFolder + "/HeldItemPrefabTable.asset";

        private static readonly HeldItemDefinition[] Definitions =
        {
            new HeldItemDefinition(
                "tool_shabby_axe",
                "Held_ShabbyAxe",
                "Assets/HDY/3DAsset/shabby_axe/tripo_convert_5e62ca64-ddfa-4b56-bbd1-8f49bf812b18.fbx",
                Vector3.zero,
                Vector3.zero,
                0.72f,
                true),
            new HeldItemDefinition(
                "tool_shabby_club",
                "Held_ShabbyClub",
                "Assets/HDY/3DAsset/shabby_club/tripo_convert_61f8eac2-afe4-4828-99e0-59b09dc1e3b6.fbx",
                Vector3.zero,
                Vector3.zero,
                0.65f,
                true),
            new HeldItemDefinition(
                "tool_shabby_hoe",
                "Held_ShabbyHoe",
                "Assets/HDY/3DAsset/shabby_hoe/tripo_convert_6112994e-fd15-4945-bdbb-cb3eef050f7a.fbx",
                Vector3.zero,
                Vector3.zero,
                0.65f,
                true),
            new HeldItemDefinition(
                "tool_shabby_pickax",
                "Held_ShabbyPickaxe",
                "Assets/HDY/3DAsset/shabby_pickaxe/tripo_convert_49403417-b522-4c4e-8b66-e025003710fb.fbx",
                Vector3.zero,
                Vector3.zero,
                0.65f,
                true),
            new HeldItemDefinition(
                "tool_shabby_capsule",
                "Held_ShabbyCapsule",
                "Assets/HDY/3DAsset/Capsule/tripo_convert_939fec23-d3f6-4750-ae2d-7ff01f60ceca.fbx",
                Vector3.zero,
                Vector3.zero,
                0.32f,
                false)
        };

        private static readonly string[] PlayerPrefabPaths =
        {
            "Assets/KMS/2.Prefabs/0714_Player_KMS.prefab",
            "Assets/KMS/2.Prefabs/0720_Player_KMS.prefab"
        };

        [MenuItem("KMS/Setup Held Item Models")]
        public static void Run()
        {
            EnsureFolder("Assets/KMS/2.Prefabs", "HeldItems");
            EnsureFolder("Assets/KMS/3.SO", "HeldItems");

            var entries = new List<HeldItemPrefabTable.Entry>();
            foreach (HeldItemDefinition definition in Definitions)
            {
                GameObject heldPrefab = CreateHeldPrefab(definition);
                entries.Add(new HeldItemPrefabTable.Entry
                {
                    itemId = definition.ItemId,
                    prefab = heldPrefab
                });
            }

            HeldItemPrefabTable table = CreateOrUpdateTable(entries);
            UpdateThrownCapsulePrefab();
            foreach (string playerPrefabPath in PlayerPrefabPaths)
            {
                UpdatePlayerPrefab(playerPrefabPath, table);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HeldItemSetup] 장착용 프리팹 5개, 프리팹 테이블, 플레이어 연결을 갱신했습니다.");
        }

        [MenuItem("KMS/Render Held Item Model Previews")]
        public static void RenderModelPreviews()
        {
            const int previewSize = 512;
            string outputFolder = Path.GetFullPath("Logs/HeldItemPreviews");
            Directory.CreateDirectory(outputFolder);

            foreach (HeldItemDefinition definition in Definitions)
            {
                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(definition.ModelPath);
                if (modelAsset == null) continue;

                GameObject instance = Object.Instantiate(modelAsset);
                try
                {
                    Bounds bounds = CalculateRendererBounds(instance);
                    Texture2D front = RenderPreview(instance, bounds, Vector3.back, previewSize);
                    Texture2D side = RenderPreview(instance, bounds, Vector3.right, previewSize);
                    var combined = new Texture2D(previewSize * 2, previewSize, TextureFormat.RGBA32, false);
                    combined.SetPixels(0, 0, previewSize, previewSize, front.GetPixels());
                    combined.SetPixels(previewSize, 0, previewSize, previewSize, side.GetPixels());
                    combined.Apply();

                    File.WriteAllBytes(
                        Path.Combine(outputFolder, $"{definition.PrefabName}.png"),
                        combined.EncodeToPNG());

                    Debug.Log(
                        $"[HeldItemPreview] {definition.PrefabName} bounds center={bounds.center}, " +
                        $"size={bounds.size}");

                    Object.DestroyImmediate(front);
                    Object.DestroyImmediate(side);
                    Object.DestroyImmediate(combined);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        [MenuItem("KMS/Render Player Held Item Previews")]
        public static void RenderPlayerHeldPreviews()
        {
            const int previewSize = 512;
            const string playerPrefabPath = "Assets/KMS/2.Prefabs/0720_Player_KMS.prefab";
            string outputFolder = Path.GetFullPath("Logs/PlayerHeldItemPreviews");
            Directory.CreateDirectory(outputFolder);

            GameObject playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            if (playerAsset == null) return;

            foreach (HeldItemDefinition definition in Definitions)
            {
                GameObject heldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{HeldPrefabFolder}/{definition.PrefabName}.prefab");
                if (heldPrefab == null) continue;

                GameObject player = Object.Instantiate(playerAsset);
                try
                {
                    PlayerMovement movement = player.GetComponent<PlayerMovement>();
                    Animator animator = movement != null ? movement.Animator : player.GetComponentInChildren<Animator>(true);
                    Transform rightHand = animator != null && animator.isHuman
                        ? animator.GetBoneTransform(HumanBodyBones.RightHand)
                        : null;
                    if (rightHand == null) continue;

                    Object.Instantiate(heldPrefab, rightHand, false);
                    CreateAxisMarker(rightHand, Vector3.zero, Color.magenta, 0.055f);
                    CreateAxisMarker(rightHand, Vector3.right * 0.15f, Color.red, 0.04f);
                    CreateAxisMarker(rightHand, Vector3.up * 0.15f, Color.green, 0.04f);
                    CreateAxisMarker(rightHand, Vector3.forward * 0.15f, Color.blue, 0.04f);
                    Bounds bounds = CalculateRendererBounds(player);
                    Texture2D front = RenderPreview(player, bounds, Vector3.back, previewSize);
                    Texture2D side = RenderPreview(player, bounds, Vector3.right, previewSize);
                    var combined = new Texture2D(previewSize * 2, previewSize, TextureFormat.RGBA32, false);
                    combined.SetPixels(0, 0, previewSize, previewSize, front.GetPixels());
                    combined.SetPixels(previewSize, 0, previewSize, previewSize, side.GetPixels());
                    combined.Apply();

                    File.WriteAllBytes(
                        Path.Combine(outputFolder, $"{definition.PrefabName}.png"),
                        combined.EncodeToPNG());

                    Object.DestroyImmediate(front);
                    Object.DestroyImmediate(side);
                    Object.DestroyImmediate(combined);
                }
                finally
                {
                    Object.DestroyImmediate(player);
                }
            }
        }

        private static GameObject CreateHeldPrefab(HeldItemDefinition definition)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(definition.ModelPath);
            if (modelAsset == null)
            {
                throw new System.InvalidOperationException(
                    $"[HeldItemSetup] FBX 모델을 찾을 수 없습니다: {definition.ModelPath}");
            }

            var root = new GameObject(definition.PrefabName);
            try
            {
                GameObject visual = PrefabUtility.InstantiatePrefab(modelAsset, root.transform) as GameObject;
                if (visual == null)
                {
                    visual = Object.Instantiate(modelAsset, root.transform);
                }

                visual.name = "Visual";
                visual.transform.localRotation = Quaternion.Euler(definition.LocalEulerAngles);
                visual.transform.localScale = Vector3.one * definition.UniformScale;
                Vector3 localPosition = definition.LocalPosition;
                if (definition.AutoAlignGrip)
                {
                    Vector3 gripPoint = CalculateHandleGripPoint(visual);
                    localPosition = -(
                        visual.transform.localRotation *
                        (gripPoint * definition.UniformScale));
                    Debug.Log(
                        $"[HeldItemSetup] {definition.PrefabName} grip={gripPoint}, " +
                        $"position={localPosition}");
                }

                visual.transform.localPosition = localPosition;

                string prefabPath = $"{HeldPrefabFolder}/{definition.PrefabName}.prefab";
                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static HeldItemPrefabTable CreateOrUpdateTable(List<HeldItemPrefabTable.Entry> entries)
        {
            HeldItemPrefabTable table = AssetDatabase.LoadAssetAtPath<HeldItemPrefabTable>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<HeldItemPrefabTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            table.EditorSetEntries(entries);
            EditorUtility.SetDirty(table);
            return table;
        }

        private static void UpdatePlayerPrefab(string prefabPath, HeldItemPrefabTable table)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                PlayerHeldItemModelController modelController =
                    root.GetComponent<PlayerHeldItemModelController>();
                if (modelController == null)
                {
                    modelController = root.AddComponent<PlayerHeldItemModelController>();
                }

                var controllerObject = new SerializedObject(modelController);
                controllerObject.FindProperty("inventory").objectReferenceValue =
                    root.GetComponent<PlayerInventory>();
                PlayerMovement movement = root.GetComponent<PlayerMovement>();
                controllerObject.FindProperty("movement").objectReferenceValue = movement;
                controllerObject.FindProperty("animator").objectReferenceValue =
                    movement != null && movement.Animator != null
                        ? movement.Animator
                        : root.GetComponentInChildren<Animator>(true);
                controllerObject.FindProperty("prefabTable").objectReferenceValue = table;
                controllerObject.ApplyModifiedPropertiesWithoutUndo();

                PlayerCapsuleThrowController capsuleThrow =
                    root.GetComponent<PlayerCapsuleThrowController>();
                if (capsuleThrow != null)
                {
                    var capsuleObject = new SerializedObject(capsuleThrow);
                    capsuleObject.FindProperty("heldItemModel").objectReferenceValue = modelController;
                    capsuleObject.FindProperty("capsulePrefab").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<GameObject>(
                            "Assets/KMS/2.Prefabs/KMS_ShabbyCaptureCapsule.prefab");
                    capsuleObject.ApplyModifiedPropertiesWithoutUndo();
                }

                PlayerHeldItemSpriteController oldController =
                    root.GetComponent<PlayerHeldItemSpriteController>();
                if (oldController != null)
                {
                    Object.DestroyImmediate(oldController);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void UpdateThrownCapsulePrefab()
        {
            const string prefabPath = "Assets/KMS/2.Prefabs/KMS_ShabbyCaptureCapsule.prefab";
            const string capsuleModelPath =
                "Assets/HDY/3DAsset/Capsule/tripo_convert_939fec23-d3f6-4750-ae2d-7ff01f60ceca.fbx";

            GameObject capsuleModel = AssetDatabase.LoadAssetAtPath<GameObject>(capsuleModelPath);
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                MeshRenderer primitiveRenderer = root.GetComponent<MeshRenderer>();
                if (primitiveRenderer != null) Object.DestroyImmediate(primitiveRenderer);

                MeshFilter primitiveFilter = root.GetComponent<MeshFilter>();
                if (primitiveFilter != null) Object.DestroyImmediate(primitiveFilter);

                Transform oldVisual = root.transform.Find("Visual");
                if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);

                GameObject visual = PrefabUtility.InstantiatePrefab(capsuleModel, root.transform) as GameObject;
                if (visual == null)
                {
                    visual = Object.Instantiate(capsuleModel, root.transform);
                }

                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visual.transform.localScale = Vector3.one * 0.4f;

                SphereCollider sphereCollider = root.GetComponent<SphereCollider>();
                if (sphereCollider != null)
                {
                    sphereCollider.center = Vector3.zero;
                    sphereCollider.radius = 0.25f;
                }

                KMSCapsuleCaptureVisual captureVisual = root.GetComponent<KMSCapsuleCaptureVisual>();
                if (captureVisual != null)
                {
                    var captureVisualObject = new SerializedObject(captureVisual);
                    SerializedProperty renderers = captureVisualObject.FindProperty("capsuleRenderers");
                    Renderer[] modelRenderers = visual.GetComponentsInChildren<Renderer>(true);
                    renderers.arraySize = modelRenderers.Length;
                    for (int i = 0; i < modelRenderers.Length; i++)
                    {
                        renderers.GetArrayElementAtIndex(i).objectReferenceValue = modelRenderers[i];
                    }

                    captureVisualObject.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureFolder(string parentFolder, string childFolder)
        {
            string path = $"{parentFolder}/{childFolder}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parentFolder, childFolder);
            }
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static Vector3 CalculateHandleGripPoint(GameObject visual)
        {
            var vertices = new List<Vector3>();
            Matrix4x4 toVisualLocal = visual.transform.worldToLocalMatrix;
            MeshFilter[] meshFilters = visual.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null) continue;

                Matrix4x4 meshToVisual =
                    toVisualLocal * meshFilter.transform.localToWorldMatrix;
                Vector3[] meshVertices = mesh.vertices;
                for (int i = 0; i < meshVertices.Length; i++)
                {
                    vertices.Add(meshToVisual.MultiplyPoint3x4(meshVertices[i]));
                }
            }

            if (vertices.Count == 0) return Vector3.zero;

            Vector3 pointA = vertices[0];
            pointA = FindFarthestVertex(vertices, pointA);
            Vector3 pointB = FindFarthestVertex(vertices, pointA);
            Vector3 axis = (pointB - pointA).normalized;

            float minProjection = float.PositiveInfinity;
            float maxProjection = float.NegativeInfinity;
            for (int i = 0; i < vertices.Count; i++)
            {
                float projection = Vector3.Dot(vertices[i], axis);
                minProjection = Mathf.Min(minProjection, projection);
                maxProjection = Mathf.Max(maxProjection, projection);
            }

            float endRegion = (maxProjection - minProjection) * 0.14f;
            Vector3 minCenter = AverageEndVertices(
                vertices, axis, minProjection, minProjection + endRegion);
            Vector3 maxCenter = AverageEndVertices(
                vertices, axis, maxProjection - endRegion, maxProjection);
            float minSpread = CalculateEndSpread(
                vertices, axis, minProjection, minProjection + endRegion, minCenter);
            float maxSpread = CalculateEndSpread(
                vertices, axis, maxProjection - endRegion, maxProjection, maxCenter);

            Vector3 handleEnd = minSpread <= maxSpread ? minCenter : maxCenter;
            Vector3 otherEnd = minSpread <= maxSpread ? maxCenter : minCenter;

            // 절대 끝점보다 손잡이 안쪽 8% 지점을 잡아 손이 모델 밖으로 빠져나가지 않게 한다.
            return Vector3.Lerp(handleEnd, otherEnd, 0.08f);
        }

        private static Vector3 FindFarthestVertex(List<Vector3> vertices, Vector3 origin)
        {
            Vector3 farthest = origin;
            float greatestDistance = -1f;
            for (int i = 0; i < vertices.Count; i++)
            {
                float distance = (vertices[i] - origin).sqrMagnitude;
                if (distance > greatestDistance)
                {
                    greatestDistance = distance;
                    farthest = vertices[i];
                }
            }

            return farthest;
        }

        private static Vector3 AverageEndVertices(
            List<Vector3> vertices,
            Vector3 axis,
            float projectionMin,
            float projectionMax)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                float projection = Vector3.Dot(vertices[i], axis);
                if (projection < projectionMin || projection > projectionMax) continue;
                sum += vertices[i];
                count++;
            }

            return count > 0 ? sum / count : Vector3.zero;
        }

        private static float CalculateEndSpread(
            List<Vector3> vertices,
            Vector3 axis,
            float projectionMin,
            float projectionMax,
            Vector3 center)
        {
            float spread = 0f;
            int count = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                float projection = Vector3.Dot(vertices[i], axis);
                if (projection < projectionMin || projection > projectionMax) continue;

                Vector3 fromCenter = vertices[i] - center;
                Vector3 radial = fromCenter - Vector3.Project(fromCenter, axis);
                spread += radial.magnitude;
                count++;
            }

            return count > 0 ? spread / count : float.PositiveInfinity;
        }

        private static void CreateAxisMarker(
            Transform parent,
            Vector3 localPosition,
            Color color,
            float scale)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "HeldItemPreviewAxisMarker";
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            marker.transform.localScale = Vector3.one * scale;

            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null) Object.DestroyImmediate(markerCollider);

            Renderer markerRenderer = marker.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (markerRenderer != null && shader != null)
            {
                var material = new Material(shader);
                material.color = color;
                markerRenderer.sharedMaterial = material;
            }
        }

        private static Texture2D RenderPreview(
            GameObject target,
            Bounds bounds,
            Vector3 viewDirection,
            int previewSize)
        {
            var cameraObject = new GameObject("HeldItemPreviewCamera");
            var lightObject = new GameObject("HeldItemPreviewLight");
            var renderTexture = new RenderTexture(previewSize, previewSize, 24, RenderTextureFormat.ARGB32);

            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.13f, 0.15f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 1.25f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = Mathf.Max(10f, bounds.size.magnitude * 8f);
                camera.targetTexture = renderTexture;

                float distance = Mathf.Max(2f, bounds.size.magnitude * 2f);
                camera.transform.position = bounds.center + viewDirection.normalized * distance;
                camera.transform.LookAt(bounds.center, Vector3.up);

                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.5f;
                light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                var texture = new Texture2D(previewSize, previewSize, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, previewSize, previewSize), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                camera.targetTexture = null;
                return texture;
            }
            finally
            {
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lightObject);
            }
        }

        private readonly struct HeldItemDefinition
        {
            public HeldItemDefinition(
                string itemId,
                string prefabName,
                string modelPath,
                Vector3 localPosition,
                Vector3 localEulerAngles,
                float uniformScale,
                bool autoAlignGrip)
            {
                ItemId = itemId;
                PrefabName = prefabName;
                ModelPath = modelPath;
                LocalPosition = localPosition;
                LocalEulerAngles = localEulerAngles;
                UniformScale = uniformScale;
                AutoAlignGrip = autoAlignGrip;
            }

            public string ItemId { get; }
            public string PrefabName { get; }
            public string ModelPath { get; }
            public Vector3 LocalPosition { get; }
            public Vector3 LocalEulerAngles { get; }
            public float UniformScale { get; }
            public bool AutoAlignGrip { get; }
        }
    }
}
