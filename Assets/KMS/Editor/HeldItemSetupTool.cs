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
                "Assets/HDY/3.Assets/3DAsset/3DToolAsset/shabby_axe/tripo_convert_083c8462-ff45-47ed-9b44-5832fd025a99.fbx",
                new Vector3(0f, 0.025f, 0f),
                new Vector3(0f, 135f, -8f),
                0.54f,
                true,
                0.22f),
            new HeldItemDefinition(
                "tool_axe",
                "Held_Axe",
                "Assets/HDY/3.Assets/3DAsset/3DToolAsset/axe/tripo_convert_54fe84f2-f7bb-4d37-bb03-79b605bde459.fbx",
                new Vector3(0f, 0.025f, 0f),
                new Vector3(0f, 135f, -8f),
                0.54f,
                true,
                0.22f),
            new HeldItemDefinition(
                "tool_decent_axe",
                "Held_DecentAxe",
                "Assets/HDY/3.Assets/3DAsset/3DToolAsset/decent_axe/tripo_convert_af681362-80b9-489d-ad3e-c2987c89edd3.fbx",
                new Vector3(0f, 0.025f, 0f),
                new Vector3(0f, 135f, -8f),
                0.54f,
                true,
                0.22f),
            new HeldItemDefinition(
                "tool_shabby_club",
                "Held_ShabbyClub",
                "Assets/HDY/3.Assets/3DAsset/3DToolAsset/shabby_club/tripo_convert_d6cefd9e-f42d-46fa-85e0-6720db1e6c93.fbx",
                new Vector3(0f, 0.025f, 0f),
                new Vector3(0f, 145f, -5f),
                0.44f,
                true,
                0.25f),
            new HeldItemDefinition(
                "tool_club",
                "Held_Club",
                "Assets/HDY/3.Assets/3DAsset/3DToolAsset/club/tripo_convert_8fe5a779-5058-4d23-92ac-1cc284977b10.fbx",
                new Vector3(0f, 0.025f, 0f),
                new Vector3(0f, 145f, -5f),
                0.44f,
                true,
                0.25f),
            new HeldItemDefinition(
                "tool_decent_club",
                "Held_DecentClub",
                "Assets/HDY/3.Assets/3DAsset/3DToolAsset/decent_club/tripo_convert_97f45183-bfd8-4254-af43-0cc53ccb6ee9.fbx",
                new Vector3(0f, 0.025f, 0f),
                new Vector3(0f, 145f, -5f),
                0.44f,
                true,
                0.25f),
            new HeldItemDefinition(
                "tool_shabby_hoe",
                "Held_ShabbyHoe",
                "Assets/HDY/3.Assets/3DAsset/3DToolAsset/shabby_hoe/tripo_convert_b47b3872-a7cc-4234-96b2-29d3722d5e7b.fbx",
                new Vector3(0f, 0.025f, 0f),
                new Vector3(0f, 135f, -8f),
                0.52f,
                true,
                0.22f),
            new HeldItemDefinition(
                "tool_hoe",
                "Held_Hoe",
                "Assets/HDY/3.Assets/3DAsset/3DToolAsset/hoe/tripo_convert_97fbf3eb-6687-4470-8317-f5f7e1f52343.fbx",
                new Vector3(0f, 0.025f, 0f),
                new Vector3(0f, 135f, -8f),
                0.52f,
                true,
                0.22f),
            new HeldItemDefinition(
                "tool_decent_hoe",
                "Held_DecentHoe",
                "Assets/HDY/3.Assets/3DAsset/3DToolAsset/decent_hoe/tripo_convert_1ab85e81-bdfc-4c6d-bc64-30f28c21f6b9.fbx",
                new Vector3(0f, 0.025f, 0f),
                new Vector3(0f, 135f, -8f),
                0.52f,
                true,
                0.22f),
            new HeldItemDefinition(
                "tool_shabby_pickax",
                "Held_ShabbyPickaxe",
                "Assets/HDY/3.Assets/3DAsset/3DToolAsset/shabby_pickaxe/tripo_convert_e601b982-f23f-4a8c-814c-9db89d927e4a.fbx",
                new Vector3(0f, 0.04f, 0f),
                new Vector3(0f, 150f, -10f),
                0.5f,
                true,
                0.22f),
            new HeldItemDefinition(
                "tool_pickax",
                "Held_Pickaxe",
                "Assets/HDY/3.Assets/3DAsset/3DToolAsset/pickaxe/tripo_convert_f7eb0f89-75f7-460c-af1b-0b0c254e34e5.fbx",
                new Vector3(0f, 0.04f, 0f),
                new Vector3(0f, 150f, -10f),
                0.5f,
                true,
                0.22f),
            new HeldItemDefinition(
                "tool_decent_pickax",
                "Held_DecentPickaxe",
                "Assets/HDY/3.Assets/3DAsset/3DToolAsset/decent_pickaxe/tripo_convert_18e0e17b-eebf-4d4e-9021-517257291088.fbx",
                new Vector3(0f, 0.04f, 0f),
                new Vector3(0f, 150f, -10f),
                0.5f,
                true,
                0.22f,
                new Vector3(0.13f, 0.1f, 0.27f)),
            new HeldItemDefinition(
                "tool_shabby_capsule",
                "Held_ShabbyCapsule",
                "Assets/HDY/3.Assets/3DAsset/Capsule/tripo_convert_939fec23-d3f6-4750-ae2d-7ff01f60ceca.fbx",
                Vector3.zero,
                Vector3.zero,
                0.28f,
                false,
                0f)
        };

        private static readonly string[] PlayerPrefabPaths =
        {
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

                AddCapsuleTierAlias(entries, definition.ItemId, heldPrefab);
            }

            HeldItemPrefabTable table = CreateOrUpdateTable(entries);
            UpdateThrownCapsulePrefab();
            foreach (string playerPrefabPath in PlayerPrefabPaths)
            {
                UpdatePlayerPrefab(playerPrefabPath, table);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[HeldItemSetup] 장착용 프리팹 {Definitions.Length}개, 프리팹 테이블, " +
                "플레이어 연결을 갱신했습니다.");
        }

        [MenuItem("KMS/Setup Axe Blade Contact Geometry")]
        public static void SetupAxeBladeContactGeometry()
        {
            string[] axePrefabNames =
            {
                "Held_ShabbyAxe",
                "Held_Axe",
                "Held_DecentAxe"
            };

            foreach (string prefabName in axePrefabNames)
            {
                string prefabPath = $"{HeldPrefabFolder}/{prefabName}.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    List<Vector3> vertices = CollectMeshVertices(root.transform);
                    if (vertices.Count == 0)
                    {
                        throw new System.InvalidOperationException(
                            $"[HeldItemSetup] No axe mesh vertices found: {prefabPath}");
                    }

                    CalculateAxeContactGeometry(
                        vertices,
                        out Vector3 shaftDirection,
                        out Vector3 contactPoint,
                        out Vector3 bladeNormal);
                    HeldToolContactGeometry geometry =
                        root.GetComponent<HeldToolContactGeometry>();
                    if (geometry == null)
                    {
                        geometry = root.AddComponent<HeldToolContactGeometry>();
                    }

                    geometry.SetGeometry(shaftDirection, contactPoint, bladeNormal);
                    EditorUtility.SetDirty(geometry);
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Debug.Log(
                        $"[HeldItemSetup] {prefabName} axe geometry: " +
                        $"shaft={shaftDirection}, contact={contactPoint}, normal={bladeNormal}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static List<Vector3> CollectMeshVertices(Transform root)
        {
            var vertices = new List<Vector3>();
            foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null) continue;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    vertices.Add(root.InverseTransformPoint(
                        meshFilter.transform.TransformPoint(vertex)));
                }
            }

            return vertices;
        }

        private static void CalculateAxeContactGeometry(
            List<Vector3> vertices,
            out Vector3 shaftDirection,
            out Vector3 contactPoint,
            out Vector3 bladeNormal)
        {
            Vector3 center = Vector3.zero;
            foreach (Vector3 vertex in vertices) center += vertex;
            center /= vertices.Count;
            shaftDirection = center.sqrMagnitude > 0.0001f
                ? center.normalized
                : Vector3.up;

            float minShaft = float.PositiveInfinity;
            float maxShaft = float.NegativeInfinity;
            foreach (Vector3 vertex in vertices)
            {
                float projection = Vector3.Dot(vertex, shaftDirection);
                minShaft = Mathf.Min(minShaft, projection);
                maxShaft = Mathf.Max(maxShaft, projection);
            }

            float headStart = Mathf.Lerp(minShaft, maxShaft, 0.70f);
            var headVertices = new List<Vector3>();
            Vector3 headCenter = Vector3.zero;
            foreach (Vector3 vertex in vertices)
            {
                if (Vector3.Dot(vertex, shaftDirection) < headStart) continue;
                headVertices.Add(vertex);
                headCenter += vertex;
            }

            headCenter /= Mathf.Max(1, headVertices.Count);
            Vector3 bladeAxis = FindLargestPlanarAxis(
                headVertices,
                headCenter,
                shaftDirection);
            float negativeExtent = 0f;
            float positiveExtent = 0f;
            foreach (Vector3 vertex in headVertices)
            {
                float extent = Vector3.Dot(vertex - headCenter, bladeAxis);
                negativeExtent = Mathf.Min(negativeExtent, extent);
                positiveExtent = Mathf.Max(positiveExtent, extent);
            }

            if (-negativeExtent > positiveExtent)
            {
                bladeAxis = -bladeAxis;
                positiveExtent = -negativeExtent;
            }

            float contactStart = positiveExtent * 0.88f;
            contactPoint = Vector3.zero;
            int contactCount = 0;
            foreach (Vector3 vertex in headVertices)
            {
                if (Vector3.Dot(vertex - headCenter, bladeAxis) < contactStart) continue;
                contactPoint += vertex;
                contactCount++;
            }

            contactPoint = contactCount > 0
                ? contactPoint / contactCount
                : headCenter + bladeAxis * positiveExtent;
            bladeNormal = Vector3.Cross(shaftDirection, bladeAxis).normalized;
        }

        private static Vector3 FindLargestPlanarAxis(
            List<Vector3> vertices,
            Vector3 center,
            Vector3 planeNormal)
        {
            Vector3 axis = Vector3.ProjectOnPlane(Vector3.right, planeNormal).normalized;
            if (axis.sqrMagnitude < 0.0001f)
            {
                axis = Vector3.ProjectOnPlane(Vector3.forward, planeNormal).normalized;
            }

            for (int iteration = 0; iteration < 20; iteration++)
            {
                Vector3 next = Vector3.zero;
                foreach (Vector3 vertex in vertices)
                {
                    Vector3 delta = Vector3.ProjectOnPlane(vertex - center, planeNormal);
                    next += delta * Vector3.Dot(delta, axis);
                }

                next = Vector3.ProjectOnPlane(next, planeNormal);
                if (next.sqrMagnitude < 0.0001f) break;
                axis = next.normalized;
            }

            return axis;
        }

        private static void AddCapsuleTierAlias(
            List<HeldItemPrefabTable.Entry> entries,
            string shabbyItemId,
            GameObject heldPrefab)
        {
            string[] aliases = shabbyItemId switch
            {
                "tool_shabby_capsule" => new[] { "tool_decent_capsule" },
                _ => System.Array.Empty<string>()
            };

            foreach (string alias in aliases)
            {
                entries.Add(new HeldItemPrefabTable.Entry
                {
                    itemId = alias,
                    prefab = heldPrefab
                });
            }
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
                    Vector3 gripPoint = definition.GripPointOverride
                        ?? CalculateHandleGripPoint(visual, definition.GripInset);
                    localPosition += -(
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
                controllerObject.FindProperty("heldCapsuleScaleCompensation").floatValue = 2f / 3f;
                controllerObject.FindProperty("longToolCarryDirection").vector3Value =
                    new Vector3(0.12f, 0.22f, 1f);
                controllerObject.FindProperty("clubCarryDirection").vector3Value =
                    new Vector3(0.16f, 0.08f, 1f);
                controllerObject.ApplyModifiedPropertiesWithoutUndo();

                PlayerToolAnimationController toolAnimation =
                    root.GetComponent<PlayerToolAnimationController>();
                if (toolAnimation != null)
                {
                    var animationObject = new SerializedObject(toolAnimation);
                    SetStringArray(
                        animationObject.FindProperty("clubItemIds"),
                        "tool_shabby_club",
                        "tool_club",
                        "tool_decent_club");
                    animationObject.ApplyModifiedPropertiesWithoutUndo();
                }

                KMS.Combat.PlayerMeleeAttackController meleeAttack =
                    root.GetComponent<KMS.Combat.PlayerMeleeAttackController>();
                if (meleeAttack != null)
                {
                    var meleeObject = new SerializedObject(meleeAttack);
                    SetStringArray(
                        meleeObject.FindProperty("catalogMeleeItemIds"),
                        "tool_shabby_club",
                        "tool_club",
                        "tool_decent_club");
                    meleeObject.ApplyModifiedPropertiesWithoutUndo();
                }

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

        private static void SetStringArray(SerializedProperty property, params string[] values)
        {
            if (property == null) return;

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        private static void UpdateThrownCapsulePrefab()
        {
            const string prefabPath = "Assets/KMS/2.Prefabs/KMS_ShabbyCaptureCapsule.prefab";
            const string capsuleModelPath =
                "Assets/HDY/3.Assets/3DAsset/Capsule/tripo_convert_939fec23-d3f6-4750-ae2d-7ff01f60ceca.fbx";

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

        private static Vector3 CalculateHandleGripPoint(
            GameObject visual,
            float gripInset)
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

            // A diagonal between two blade tips can be longer than the handle and
            // made the previous heuristic place the hand on an axe/pickaxe head.
            // The dominant covariance axis follows the long shaft much more
            // reliably across the shabby, normal, and decent model variants.
            Vector3 axis = CalculatePrincipalAxis(vertices);

            float minProjection = float.PositiveInfinity;
            float maxProjection = float.NegativeInfinity;
            for (int i = 0; i < vertices.Count; i++)
            {
                float projection = Vector3.Dot(vertices[i], axis);
                minProjection = Mathf.Min(minProjection, projection);
                maxProjection = Mathf.Max(maxProjection, projection);
            }

            float endRegion = (maxProjection - minProjection) * 0.18f;
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

            return Vector3.Lerp(
                handleEnd,
                otherEnd,
                Mathf.Clamp01(gripInset));
        }

        private static Vector3 CalculatePrincipalAxis(List<Vector3> vertices)
        {
            Vector3 mean = Vector3.zero;
            for (int i = 0; i < vertices.Count; i++)
            {
                mean += vertices[i];
            }
            mean /= vertices.Count;

            float xx = 0f;
            float xy = 0f;
            float xz = 0f;
            float yy = 0f;
            float yz = 0f;
            float zz = 0f;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 delta = vertices[i] - mean;
                xx += delta.x * delta.x;
                xy += delta.x * delta.y;
                xz += delta.x * delta.z;
                yy += delta.y * delta.y;
                yz += delta.y * delta.z;
                zz += delta.z * delta.z;
            }

            Vector3 axis = Vector3.forward;
            if (xx >= yy && xx >= zz) axis = Vector3.right;
            else if (yy >= zz) axis = Vector3.up;

            for (int iteration = 0; iteration < 16; iteration++)
            {
                Vector3 next = new Vector3(
                    xx * axis.x + xy * axis.y + xz * axis.z,
                    xy * axis.x + yy * axis.y + yz * axis.z,
                    xz * axis.x + yz * axis.y + zz * axis.z);
                if (next.sqrMagnitude <= Mathf.Epsilon) break;
                axis = next.normalized;
            }

            return axis.sqrMagnitude > Mathf.Epsilon ? axis.normalized : Vector3.forward;
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
                bool autoAlignGrip,
                float gripInset,
                Vector3? gripPointOverride = null)
            {
                ItemId = itemId;
                PrefabName = prefabName;
                ModelPath = modelPath;
                LocalPosition = localPosition;
                LocalEulerAngles = localEulerAngles;
                UniformScale = uniformScale;
                AutoAlignGrip = autoAlignGrip;
                GripInset = gripInset;
                GripPointOverride = gripPointOverride;
            }

            public string ItemId { get; }
            public string PrefabName { get; }
            public string ModelPath { get; }
            public Vector3 LocalPosition { get; }
            public Vector3 LocalEulerAngles { get; }
            public float UniformScale { get; }
            public bool AutoAlignGrip { get; }
            public float GripInset { get; }
            public Vector3? GripPointOverride { get; }
        }
    }
}
