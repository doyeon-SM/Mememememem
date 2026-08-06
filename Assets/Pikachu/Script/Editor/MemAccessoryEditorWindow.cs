// ============================================================================
// MemAccessoryEditorWindow.cs
// 악세서리 부착 위치를 Play Mode 없이 씬뷰에서 직접 잡는 에디터 창
//
// [여는 법]  상단 메뉴 → Tools → Mem → 악세서리 위치 편집기
//
// [작업 흐름]
// 1. accessory 칸에 MemAccessoryData 에셋을 넣습니다.
// 2. [프리뷰 생성] — 씬에 멈춰 있는 멤 모델이 생기고, 악세서리가 슬롯 뼈에 붙은 채
//    자동으로 선택됩니다. 이 상태에서 씬뷰의 이동/회전/스케일 기즈모로 그냥 끌면 됩니다.
//    (Play Mode 아님 / 멤이 움직이지 않음)
// 3. 필요하면 [애니메이션 포즈] 로 도끼질·달리기 포즈를 미리 잡아두고 파고들지 확인합니다.
// 4. [에셋에 저장] — 현재 기즈모 위치가 MemAccessoryData에 기록됩니다.
// 5. [프리뷰 정리] — 임시 오브젝트를 지웁니다. (창을 닫아도 자동으로 정리됩니다)
//
// [프리뷰 오브젝트에 대해]
// HideFlags.DontSave로 생성되므로 씬에 저장되지 않습니다.
// Play를 누르거나 스크립트가 재컴파일되면 사라지니, 그때는 다시 [프리뷰 생성]을 누르세요.
// ============================================================================

using UnityEngine;
using UnityEditor;
using MemSystem.Data;
using MemSystem.Visual;

namespace MemSystem.EditorTools
{
    /// <summary>
    /// 악세서리 오프셋을 Edit Mode에서 씬뷰 기즈모로 조정하는 에디터 창.
    /// </summary>
    public class MemAccessoryEditorWindow : EditorWindow
    {
        // ---------------------------------------------------------------
        // 기본값 경로
        // ---------------------------------------------------------------

        private const string DefaultModelPath =
            "Assets/Pikachu/Resource/Mem/Mem_Rig_White.prefab";

        private const string PreviewRootName = "[악세서리 프리뷰 — 저장되지 않음]";

        // ---------------------------------------------------------------
        // 창 상태 (도메인 리로드에도 유지되도록 SerializeField)
        // ---------------------------------------------------------------

        [SerializeField] private MemAccessoryData accessory;
        [SerializeField] private GameObject modelPrefab;

        [SerializeField] private GameObject previewRoot;
        [SerializeField] private GameObject modelInstance;
        [SerializeField] private GameObject accessoryInstance;

        [SerializeField] private int selectedClipIndex;
        [SerializeField] private float clipTime;

        private AnimationClip[] cachedClips;
        private string[] cachedClipNames;
        private Vector2 scroll;

        // ---------------------------------------------------------------
        // 창 열기
        // ---------------------------------------------------------------

        [MenuItem("Tools/Mem/악세서리 위치 편집기")]
        public static void Open()
        {
            var window = GetWindow<MemAccessoryEditorWindow>("악세서리 편집기");
            window.minSize = new Vector2(320f, 420f);
            window.Show();
        }

        /// <summary>
        /// MemAccessoryData 에셋을 더블클릭하듯 바로 편집할 수 있게,
        /// 에셋 우클릭 메뉴에서도 열 수 있도록 합니다.
        /// </summary>
        [MenuItem("Assets/Mem/이 악세서리 위치 편집", true)]
        private static bool OpenFromAssetValidate()
        {
            return Selection.activeObject is MemAccessoryData;
        }

        [MenuItem("Assets/Mem/이 악세서리 위치 편집")]
        private static void OpenFromAsset()
        {
            var window = GetWindow<MemAccessoryEditorWindow>("악세서리 편집기");
            window.accessory = Selection.activeObject as MemAccessoryData;
            window.Show();
            window.CreatePreview();
        }

        private void OnEnable()
        {
            if (modelPrefab == null)
                modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModelPath);
        }

        private void OnDisable()
        {
            // 창을 닫으면 임시 오브젝트가 씬에 남지 않도록 정리합니다.
            ClearPreview();
        }

        // ---------------------------------------------------------------
        // GUI
        // ---------------------------------------------------------------

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("대상", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            accessory = (MemAccessoryData)EditorGUILayout.ObjectField(
                "악세서리 데이터", accessory, typeof(MemAccessoryData), false);
            bool accessoryChanged = EditorGUI.EndChangeCheck();

            modelPrefab = (GameObject)EditorGUILayout.ObjectField(
                "미리보기 멤 모델", modelPrefab, typeof(GameObject), false);

            if (accessoryChanged && IsPreviewAlive())
            {
                // 다른 악세서리로 바꾸면 프리뷰도 즉시 갈아끼웁니다.
                CreatePreview();
            }

            EditorGUILayout.Space(6);
            DrawSlotInfo();

            EditorGUILayout.Space(10);
            DrawPreviewButtons();

            EditorGUILayout.Space(10);
            DrawOffsetFields();

            EditorGUILayout.Space(10);
            DrawAnimationPosePreview();

            EditorGUILayout.Space(10);
            DrawSaveButtons();

            EditorGUILayout.Space(10);
            DrawHelp();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSlotInfo()
        {
            if (accessory == null) return;

            string bone = MemVisual.GetDefaultBoneName(accessory.slot);
            EditorGUILayout.HelpBox(
                $"슬롯: {accessory.slot}    →    부착 뼈: \"{bone}\"", MessageType.None);

            if (accessory.prefab == null)
            {
                EditorGUILayout.HelpBox(
                    "이 악세서리에 prefab이 지정되지 않았습니다. 3D 모델 프리팹을 먼저 넣어주세요.",
                    MessageType.Warning);
            }
        }

        private void DrawPreviewButtons()
        {
            EditorGUILayout.LabelField("프리뷰", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(accessory == null || accessory.prefab == null || modelPrefab == null))
            {
                if (GUILayout.Button(IsPreviewAlive() ? "프리뷰 다시 생성" : "프리뷰 생성", GUILayout.Height(30)))
                    CreatePreview();
            }

            using (new EditorGUI.DisabledScope(!IsPreviewAlive()))
            {
                if (GUILayout.Button("악세서리 다시 선택 (기즈모 띄우기)"))
                    SelectAccessoryInstance();

                if (GUILayout.Button("프리뷰 정리"))
                    ClearPreview();
            }

            if (!IsPreviewAlive())
            {
                EditorGUILayout.HelpBox(
                    "프리뷰가 없습니다. [프리뷰 생성]을 누르면 씬에 멈춰 있는 멤이 만들어지고 " +
                    "악세서리가 자동 선택됩니다. 그 상태에서 씬뷰 기즈모로 끌어서 맞추세요.",
                    MessageType.Info);
            }
        }

        private void DrawOffsetFields()
        {
            EditorGUILayout.LabelField("현재 값 (수치 직접 입력도 가능)", EditorStyles.boldLabel);

            if (!IsPreviewAlive())
            {
                if (accessory != null)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.Vector3Field("Position", accessory.positionOffset);
                        EditorGUILayout.Vector3Field("Rotation", accessory.rotationOffset);
                        EditorGUILayout.Vector3Field("Scale", accessory.scaleMultiplier);
                    }
                    EditorGUILayout.LabelField("(에셋에 저장된 값)", EditorStyles.miniLabel);
                }
                return;
            }

            Transform t = accessoryInstance.transform;

            EditorGUI.BeginChangeCheck();
            Vector3 pos   = EditorGUILayout.Vector3Field("Position", t.localPosition);
            Vector3 rot   = EditorGUILayout.Vector3Field("Rotation", t.localEulerAngles);
            Vector3 scale = EditorGUILayout.Vector3Field("Scale", CurrentScaleMultiplier());

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(t, "악세서리 오프셋 수정");
                t.localPosition    = pos;
                t.localEulerAngles = rot;
                t.localScale       = Vector3.Scale(PrefabBaseScale(), scale);
                SceneView.RepaintAll();
            }
        }

        private void DrawAnimationPosePreview()
        {
            EditorGUILayout.LabelField("애니메이션 포즈로 확인", EditorStyles.boldLabel);

            if (!IsPreviewAlive())
            {
                EditorGUILayout.LabelField("프리뷰 생성 후 사용할 수 있습니다.", EditorStyles.miniLabel);
                return;
            }

            CacheClips();

            if (cachedClips == null || cachedClips.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "모델의 Animator Controller에서 클립을 찾지 못했습니다. 기본(바인드) 포즈로만 확인됩니다.",
                    MessageType.None);
                return;
            }

            EditorGUI.BeginChangeCheck();
            selectedClipIndex = EditorGUILayout.Popup("클립", selectedClipIndex, cachedClipNames);
            clipTime = EditorGUILayout.Slider("재생 위치", clipTime, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
                SamplePose();

            EditorGUILayout.LabelField(
                "도끼질·달리기 포즈에서 모자가 머리를 뚫지 않는지 확인하세요.",
                EditorStyles.miniLabel);
        }

        private void DrawSaveButtons()
        {
            EditorGUILayout.LabelField("저장", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!IsPreviewAlive() || accessory == null))
            {
                GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                if (GUILayout.Button("에셋에 저장", GUILayout.Height(34)))
                    SaveToAsset();
                GUI.backgroundColor = Color.white;
            }

            using (new EditorGUI.DisabledScope(!IsPreviewAlive() || accessory == null))
            {
                if (GUILayout.Button("에셋 값으로 되돌리기"))
                    ApplyAssetOffsetsToInstance();
            }
        }

        private void DrawHelp()
        {
            EditorGUILayout.HelpBox(
                "프리뷰 오브젝트는 씬에 저장되지 않습니다.\n" +
                "Play를 누르거나 스크립트가 재컴파일되면 사라지므로, 저장은 그 전에 하세요.",
                MessageType.Info);
        }

        // ---------------------------------------------------------------
        // 프리뷰 생성/정리
        // ---------------------------------------------------------------

        private void CreatePreview()
        {
            ClearPreview();

            if (accessory == null || accessory.prefab == null || modelPrefab == null) return;

            // 씬에 저장되지 않는 임시 루트
            previewRoot = new GameObject(PreviewRootName);
            previewRoot.hideFlags = HideFlags.DontSave;

            // 멤 모델 — 씬뷰에서 보기 좋게 원점에 세웁니다.
            modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            if (modelInstance == null)
            {
                Debug.LogError("[악세서리 편집기] 멤 모델 프리팹을 생성하지 못했습니다.");
                ClearPreview();
                return;
            }

            modelInstance.transform.SetParent(previewRoot.transform, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = modelPrefab.transform.localRotation;
            SetHideFlagsRecursive(modelInstance, HideFlags.DontSave);

            // 슬롯에 해당하는 뼈 찾기 — 런타임(MemVisual)과 완전히 같은 규칙
            string boneName = MemVisual.GetDefaultBoneName(accessory.slot);
            Transform bone = MemVisual.FindBone(modelInstance.transform, boneName);

            if (bone == null)
            {
                Debug.LogError($"[악세서리 편집기] 뼈 '{boneName}'을(를) 모델에서 찾지 못했습니다. " +
                               $"모델: {modelPrefab.name}");
                ClearPreview();
                return;
            }

            // 악세서리 부착 — 런타임 EquipAccessory와 동일하게 적용
            accessoryInstance = (GameObject)PrefabUtility.InstantiatePrefab(accessory.prefab);
            if (accessoryInstance == null)
            {
                Debug.LogError("[악세서리 편집기] 악세서리 프리팹을 생성하지 못했습니다.");
                ClearPreview();
                return;
            }

            accessoryInstance.transform.SetParent(bone, false);
            SetHideFlagsRecursive(accessoryInstance, HideFlags.DontSave);
            ApplyAssetOffsetsToInstance();

            cachedClips = null; // 모델이 바뀌었을 수 있으므로 클립 캐시 무효화
            selectedClipIndex = 0;
            clipTime = 0f;

            SelectAccessoryInstance();

            Debug.Log($"[악세서리 편집기] '{accessory.name}' 프리뷰 생성 — 뼈 '{bone.name}'에 부착. " +
                      $"씬뷰 기즈모로 위치를 잡고 [에셋에 저장]을 누르세요.");
        }

        private void ClearPreview()
        {
            if (previewRoot != null)
                DestroyImmediate(previewRoot);

            previewRoot       = null;
            modelInstance     = null;
            accessoryInstance = null;
            cachedClips       = null;
            cachedClipNames   = null;
        }

        private bool IsPreviewAlive()
        {
            return previewRoot != null && accessoryInstance != null;
        }

        /// <summary>악세서리를 선택 상태로 만들어 씬뷰에 이동/회전/스케일 기즈모가 뜨게 합니다.</summary>
        private void SelectAccessoryInstance()
        {
            if (accessoryInstance == null) return;

            Selection.activeGameObject = accessoryInstance;

            SceneView sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                sv.Frame(new Bounds(accessoryInstance.transform.position, Vector3.one * 1.5f), false);
                sv.Repaint();
            }
        }

        private static void SetHideFlagsRecursive(GameObject go, HideFlags flags)
        {
            go.hideFlags = flags;
            foreach (Transform child in go.transform)
                SetHideFlagsRecursive(child.gameObject, flags);
        }

        // ---------------------------------------------------------------
        // 값 적용/저장
        // ---------------------------------------------------------------

        /// <summary>프리팹 원본 스케일 (scaleMultiplier의 기준값).</summary>
        private Vector3 PrefabBaseScale()
        {
            if (accessory == null || accessory.prefab == null) return Vector3.one;
            return accessory.prefab.transform.localScale;
        }

        /// <summary>현재 인스턴스의 localScale을 scaleMultiplier 값으로 환산합니다.</summary>
        private Vector3 CurrentScaleMultiplier()
        {
            if (accessoryInstance == null) return Vector3.one;

            Vector3 baseScale = PrefabBaseScale();
            Vector3 local     = accessoryInstance.transform.localScale;

            return new Vector3(
                Mathf.Approximately(baseScale.x, 0f) ? local.x : local.x / baseScale.x,
                Mathf.Approximately(baseScale.y, 0f) ? local.y : local.y / baseScale.y,
                Mathf.Approximately(baseScale.z, 0f) ? local.z : local.z / baseScale.z);
        }

        /// <summary>에셋에 저장된 오프셋을 프리뷰 인스턴스에 적용합니다.</summary>
        private void ApplyAssetOffsetsToInstance()
        {
            if (accessoryInstance == null || accessory == null) return;

            Undo.RecordObject(accessoryInstance.transform, "에셋 값으로 되돌리기");

            accessoryInstance.transform.localPosition = accessory.positionOffset;
            accessoryInstance.transform.localRotation = Quaternion.Euler(accessory.rotationOffset);
            accessoryInstance.transform.localScale    =
                Vector3.Scale(PrefabBaseScale(), accessory.scaleMultiplier);

            SceneView.RepaintAll();
        }

        /// <summary>현재 기즈모 위치를 MemAccessoryData 에셋에 기록합니다.</summary>
        private void SaveToAsset()
        {
            if (accessoryInstance == null || accessory == null) return;

            Undo.RecordObject(accessory, "악세서리 오프셋 저장");

            Transform t = accessoryInstance.transform;
            accessory.positionOffset  = t.localPosition;
            accessory.rotationOffset  = t.localEulerAngles;
            accessory.scaleMultiplier = CurrentScaleMultiplier();

            EditorUtility.SetDirty(accessory);
            AssetDatabase.SaveAssetIfDirty(accessory);

            Debug.Log($"[악세서리 편집기] '{accessory.name}' 저장 완료. " +
                      $"pos={accessory.positionOffset} rot={accessory.rotationOffset} " +
                      $"scale={accessory.scaleMultiplier}");
        }

        // ---------------------------------------------------------------
        // 애니메이션 포즈 샘플링
        // ---------------------------------------------------------------

        private void CacheClips()
        {
            if (cachedClips != null) return;
            if (modelInstance == null) return;

            Animator animator = modelInstance.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                cachedClips = new AnimationClip[0];
                cachedClipNames = new string[0];
                return;
            }

            cachedClips = animator.runtimeAnimatorController.animationClips;
            cachedClipNames = new string[cachedClips.Length];

            for (int i = 0; i < cachedClips.Length; i++)
                cachedClipNames[i] = cachedClips[i] != null ? cachedClips[i].name : "(null)";
        }

        /// <summary>선택한 클립의 특정 시점 포즈를 프리뷰 모델에 적용합니다.</summary>
        private void SamplePose()
        {
            if (cachedClips == null || cachedClips.Length == 0) return;
            if (selectedClipIndex < 0 || selectedClipIndex >= cachedClips.Length) return;

            AnimationClip clip = cachedClips[selectedClipIndex];
            if (clip == null || modelInstance == null) return;

            Animator animator = modelInstance.GetComponentInChildren<Animator>(true);
            if (animator == null) return;

            clip.SampleAnimation(animator.gameObject, clipTime * clip.length);
            SceneView.RepaintAll();
        }
    }
}
