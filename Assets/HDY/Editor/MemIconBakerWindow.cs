using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HDY.Mem.Editor
{
    /// <summary>
    /// [에디터 전용] 멤 아이콘 일괄/부분 굽기 도구 창.
    /// 메뉴: HDY > Mem > Mem Icon Baker.
    ///
    /// - Appearance Table(MemAppearanceTable)에서 굽기 대상 목록(memId + 모델 프리팹)을 가져온다.
    /// - 체크박스로 전체/부분 선택 후 "굽기"하면 선택한 항목만 다시 촬영해 PNG를 덮어쓰고
    ///   Icon Table(MemIconTable)에 반영한다(기존 리스트 수정 = 덮어쓰기).
    /// - "미리보기"는 디스크에 저장하지 않고 화면에서만 결과를 확인한다.
    /// </summary>
    public class MemIconBakerWindow : EditorWindow
    {
        // [HDY 요청 - 편의 개선] 프로젝트 표준 경로를 기본값으로 자동 로드한다.
        // 도연님이 매번 직접 드래그하지 않아도 되도록, OnEnable에서 비어있으면 채워 넣는다.
        private const string DefaultAppearanceTablePath = "Assets/3.SO/Mems/MemAppearanceTable.asset";
        private const string DefaultIconTablePath = "Assets/3.SO/Mems/MemIconTable.asset";

        [SerializeField] private MemAppearanceTable appearanceTable;
        [SerializeField] private MemIconTable iconTable;

        [Header("촬영 설정 (기존 MemIconRenderer와 동일한 의미)")]
        [SerializeField] private float cameraFitPadding = 1.2f;
        [SerializeField] private float verticalFocusRatio = 1f;
        [SerializeField] private float cameraAzimuthDegrees = 0f;
        [SerializeField] private float cameraElevationDegrees = 5f;
        [SerializeField] private float iconLightIntensity = 8f;
        [SerializeField] private Color iconLightColor = Color.white;

        private readonly Dictionary<string, bool> checkedState = new Dictionary<string, bool>();
        private Vector2 scroll;
        private Texture2D previewTexture;
        private string previewMemId;

        [MenuItem("HDY/Mem/Mem Icon Baker")]
        public static void Open()
        {
            GetWindow<MemIconBakerWindow>("Mem Icon Baker");
        }

        private void OnEnable()
        {
            if (appearanceTable == null)
            {
                appearanceTable = AssetDatabase.LoadAssetAtPath<MemAppearanceTable>(DefaultAppearanceTablePath);
            }

            if (iconTable == null)
            {
                iconTable = AssetDatabase.LoadAssetAtPath<MemIconTable>(DefaultIconTablePath);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("멤 아이콘 굽기 도구", EditorStyles.boldLabel);

            appearanceTable = (MemAppearanceTable)EditorGUILayout.ObjectField(
                "Appearance Table", appearanceTable, typeof(MemAppearanceTable), false);
            iconTable = (MemIconTable)EditorGUILayout.ObjectField(
                "Icon Table", iconTable, typeof(MemIconTable), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("촬영 설정", EditorStyles.boldLabel);
            cameraFitPadding = EditorGUILayout.FloatField("Camera Fit Padding", cameraFitPadding);
            verticalFocusRatio = EditorGUILayout.Slider("Vertical Focus Ratio", verticalFocusRatio, 0f, 1f);
            cameraAzimuthDegrees = EditorGUILayout.FloatField("Camera Azimuth", cameraAzimuthDegrees);
            cameraElevationDegrees = EditorGUILayout.FloatField("Camera Elevation", cameraElevationDegrees);
            iconLightIntensity = EditorGUILayout.FloatField("Icon Light Intensity", iconLightIntensity);
            iconLightColor = EditorGUILayout.ColorField("Icon Light Color", iconLightColor);

            EditorGUILayout.Space();

            if (appearanceTable == null)
            {
                EditorGUILayout.HelpBox("Appearance Table을 지정해주세요.", MessageType.Warning);
                return;
            }

            var entries = appearanceTable.EditorEntries;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 선택"))
            {
                foreach (var e in entries) checkedState[e.Mem_ID] = true;
            }
            if (GUILayout.Button("전체 해제"))
            {
                foreach (var e in entries) checkedState[e.Mem_ID] = false;
            }
            EditorGUILayout.EndHorizontal();

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(300));
            foreach (var e in entries)
            {
                if (!checkedState.ContainsKey(e.Mem_ID)) checkedState[e.Mem_ID] = false;

                EditorGUILayout.BeginHorizontal();
                checkedState[e.Mem_ID] = EditorGUILayout.ToggleLeft(e.Mem_ID, checkedState[e.Mem_ID], GUILayout.Width(220));

                bool hasIcon = iconTable != null && iconTable.HasDedicatedIcon(e.Mem_ID);
                GUILayout.Label(hasIcon ? "구워짐" : "미굽음", GUILayout.Width(60));

                GUI.enabled = e.Prefab != null;
                if (GUILayout.Button("미리보기", GUILayout.Width(70)))
                {
                    PreviewOne(e.Mem_ID, e.Prefab);
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            if (previewTexture != null)
            {
                EditorGUILayout.LabelField("미리보기: " + previewMemId);
                var rect = GUILayoutUtility.GetRect(128, 128, GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(rect, previewTexture);
            }

            EditorGUILayout.Space();

            int checkedCount = checkedState.Values.Count(v => v);
            using (new EditorGUI.DisabledScope(iconTable == null || checkedCount == 0))
            {
                if (GUILayout.Button($"선택한 {checkedCount}개 굽기 (덮어쓰기)"))
                {
                    BakeChecked(entries);
                }
            }
        }

        private MemIconBaker.Settings BuildSettings()
        {
            return new MemIconBaker.Settings
            {
                cameraFitPadding = cameraFitPadding,
                verticalFocusRatio = verticalFocusRatio,
                cameraAzimuthDegrees = cameraAzimuthDegrees,
                cameraElevationDegrees = cameraElevationDegrees,
                iconLightIntensity = iconLightIntensity,
                iconLightColor = iconLightColor,
            };
        }

        private void PreviewOne(string memId, GameObject prefab)
        {
            if (prefab == null) return;

            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
                previewTexture = null;
            }

            previewTexture = MemIconBaker.CapturePreview(prefab, BuildSettings(), 128);
            previewMemId = memId;
            Repaint();
        }

        private void BakeChecked(IReadOnlyList<MemAppearanceTable.Entry> entries)
        {
            var settings = BuildSettings();
            int done = 0;

            var targets = new List<MemAppearanceTable.Entry>();
            foreach (var e in entries)
            {
                if (checkedState.TryGetValue(e.Mem_ID, out var isChecked) && isChecked)
                {
                    targets.Add(e);
                }
            }

            for (int i = 0; i < targets.Count; i++)
            {
                var e = targets[i];
                if (e.Prefab == null)
                {
                    Debug.LogWarning($"[MemIconBakerWindow] '{e.Mem_ID}'는 modelPrefab이 없어 건너뜁니다.");
                    continue;
                }

                EditorUtility.DisplayProgressBar("멤 아이콘 굽는 중", e.Mem_ID, (float)i / targets.Count);

                var entry = MemIconBaker.BakeAndSave(e.Mem_ID, e.Prefab, settings);
                iconTable.EditorUpsertEntry(entry);
                done++;
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(iconTable);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MemIconBakerWindow] {done}개 아이콘 굽기 완료.");
        }

        private void OnDestroy()
        {
            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
                previewTexture = null;
            }
        }
    }
}
