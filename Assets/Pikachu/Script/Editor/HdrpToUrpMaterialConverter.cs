// ============================================================================
// HdrpToUrpMaterialConverter.cs
// HDRP 전용 머티리얼을 URP/Lit 으로 변환하는 에디터 창
//
// [여는 법]  상단 메뉴 → Tools → Mem → HDRP→URP 머티리얼 변환기
//
// [왜 필요한가]
// 이 프로젝트는 URP(com.unity.render-pipelines.universal)를 씁니다.
// HDRP 전용으로 만들어진 에셋팩을 임포트하면, 머티리얼이 참조하는 HDRP 셰이더가
// 프로젝트에 존재하지 않아 유니티가 에러 셰이더로 대체합니다. 그게 핫핑크입니다.
// 유니티 공식 Render Pipeline Converter 는 Built-in→URP 만 지원하고
// HDRP→URP 경로는 제공하지 않기 때문에, 이 창으로 직접 옮깁니다.
//
// [작업 흐름]
// 1. 대상 폴더를 지정합니다. (예: Assets/Hawaii Beach House (PBR, HDRP))
// 2. [검사] — 폴더 안 머티리얼을 훑어서 무엇이 변환 대상인지 미리 보여줍니다.
//    이 단계는 파일을 건드리지 않습니다.
// 3. [변환 실행] — 셰이더를 URP/Lit 으로 바꾸고 텍스처를 재연결합니다.
//
// [중요]
// 변환은 .mat 파일을 덮어씁니다. Undo 로 되돌릴 수 없으니
// 실행 전에 반드시 버전관리 커밋 또는 폴더 백업을 해두세요.
//
// [슬롯 대응표]
//   HDRP                                   →  URP/Lit
//   _BaseColorMap / _MainTex               →  _BaseMap
//   _NormalMap / _BumpMap                  →  _BumpMap
//   _MaskMap                               →  _MetallicGlossMap + _OcclusionMap
//   _EmissiveColorMap                      →  _EmissionMap
//   _BaseColor / _Color                    →  _BaseColor
//
// HDRP MaskMap 채널은 R:Metallic G:Occlusion B:Detail A:Smoothness 이고,
// URP 는 _MetallicGlossMap 의 R:Metallic A:Smoothness, _OcclusionMap 의 G:Occlusion
// 을 읽으므로 같은 텍스처를 두 슬롯에 물려도 채널이 그대로 맞습니다.
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MemSystem.EditorTools
{
    /// <summary>
    /// HDRP 머티리얼을 URP/Lit 으로 일괄 변환하는 에디터 창.
    /// 셰이더가 유실된(핫핑크) 상태에서도 동작하도록, 프로퍼티를 Material API가 아니라
    /// SerializedObject 로 직접 읽는다.
    /// </summary>
    public class HdrpToUrpMaterialConverter : EditorWindow
    {
        // ---------------------------------------------------------------
        // 상수
        // ---------------------------------------------------------------

        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";

        /// <summary>_BaseMap 후보를 우선순위 순으로. 앞에 있는 게 먼저 채택된다.</summary>
        private static readonly string[] BaseMapCandidates =
        {
            "_BaseColorMap", "_MainTex", "_BASE_COLOR_MAP", "_BaseMap"
        };

        private static readonly string[] NormalMapCandidates =
        {
            "_NormalMap", "_BumpMap", "_NORMAL_MAP"
        };

        private static readonly string[] MaskMapCandidates =
        {
            "_MaskMap", "_METALNESS_MAP", "_SPECULAR_ROUGHNESS_MAP"
        };

        private static readonly string[] EmissionMapCandidates =
        {
            "_EmissiveColorMap", "_EmissionMap"
        };

        // ---------------------------------------------------------------
        // 창 상태
        // ---------------------------------------------------------------

        [SerializeField] private DefaultAsset targetFolder;
        [SerializeField] private bool onlyBrokenShaders = true;
        [SerializeField] private Vector2 scroll;

        private string report;

        [MenuItem("Tools/Mem/HDRP→URP 머티리얼 변환기")]
        private static void Open()
        {
            var window = GetWindow<HdrpToUrpMaterialConverter>();
            window.titleContent = new GUIContent("HDRP→URP 변환기");
            window.minSize = new Vector2(460f, 360f);
            window.Show();
        }

        // ---------------------------------------------------------------
        // GUI
        // ---------------------------------------------------------------

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "HDRP 전용 머티리얼을 URP/Lit 으로 옮깁니다.\n" +
                "변환은 .mat 파일을 덮어쓰며 Undo 가 되지 않습니다. " +
                "실행 전에 커밋하거나 폴더를 백업하세요.",
                MessageType.Warning);

            EditorGUILayout.Space();

            targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "대상 폴더", targetFolder, typeof(DefaultAsset), false);

            onlyBrokenShaders = EditorGUILayout.ToggleLeft(
                "셰이더가 깨진 머티리얼만 (권장)", onlyBrokenShaders);

            EditorGUILayout.Space();

            string folderPath = GetFolderPath();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(folderPath)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("검사", GUILayout.Height(28f)))
                        report = Scan(folderPath);

                    if (GUILayout.Button("변환 실행", GUILayout.Height(28f)))
                        TryConvert(folderPath);
                }
            }

            if (string.IsNullOrEmpty(folderPath))
                EditorGUILayout.HelpBox("폴더 에셋을 넣어주세요.", MessageType.Info);

            EditorGUILayout.Space();

            if (!string.IsNullOrEmpty(report))
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        private string GetFolderPath()
        {
            if (targetFolder == null) return null;

            string path = AssetDatabase.GetAssetPath(targetFolder);
            return AssetDatabase.IsValidFolder(path) ? path : null;
        }

        // ---------------------------------------------------------------
        // 검사 — 파일을 건드리지 않고 무엇이 바뀔지만 보여준다
        // ---------------------------------------------------------------

        private string Scan(string folderPath)
        {
            var materials = LoadMaterials(folderPath);
            var sb = new StringBuilder();

            sb.AppendLine($"[검사] {folderPath}");
            sb.AppendLine($"머티리얼 {materials.Count}개");
            sb.AppendLine();

            int targets = 0;

            foreach (var mat in materials)
            {
                bool broken = IsShaderBroken(mat);
                if (onlyBrokenShaders && !broken) continue;

                targets++;

                var textures = ReadSavedTextures(mat);
                string shaderName = mat.shader != null ? mat.shader.name : "(null)";

                sb.AppendLine($"● {mat.name}");
                sb.AppendLine($"   현재 셰이더 : {shaderName}{(broken ? "   ← 깨짐" : "")}");

                if (mat.parent != null)
                {
                    sb.AppendLine($"   배리언트    : 부모 '{mat.parent.name}' 에서 셰이더 상속 " +
                                  "(셰이더는 부모만 바꾸고, 여기엔 텍스처만 재연결)");
                }

                sb.AppendLine($"   _BaseMap    ← {DescribePick(textures, BaseMapCandidates)}");
                sb.AppendLine($"   _BumpMap    ← {DescribePick(textures, NormalMapCandidates)}");
                sb.AppendLine($"   _MaskMap계열 ← {DescribePick(textures, MaskMapCandidates)}");
                sb.AppendLine();
            }

            sb.AppendLine($"변환 대상 {targets}개");

            if (Shader.Find(UrpLitShaderName) == null)
                sb.AppendLine($"\n경고: '{UrpLitShaderName}' 셰이더를 찾을 수 없습니다. URP 패키지를 확인하세요.");

            return sb.ToString();
        }

        private static string DescribePick(Dictionary<string, Texture> textures, string[] candidates)
        {
            foreach (string key in candidates)
            {
                if (textures.TryGetValue(key, out var tex) && tex != null)
                    return $"{key} ({tex.name})";
            }
            return "없음";
        }

        // ---------------------------------------------------------------
        // 변환
        // ---------------------------------------------------------------

        private void TryConvert(string folderPath)
        {
            var urpLit = Shader.Find(UrpLitShaderName);
            if (urpLit == null)
            {
                EditorUtility.DisplayDialog(
                    "변환 불가",
                    $"'{UrpLitShaderName}' 셰이더를 찾을 수 없습니다.\n" +
                    "URP 패키지가 설치되어 있는지 확인하세요.",
                    "확인");
                return;
            }

            var materials = LoadMaterials(folderPath);

            // 배리언트는 부모의 셰이더를 물려받으므로 부모부터 변환해야 한다.
            // 상속 깊이 오름차순 = 부모가 항상 자식보다 먼저.
            var targets = materials
                .Where(m => !onlyBrokenShaders || IsShaderBroken(m))
                .OrderBy(GetVariantDepth)
                .ToList();

            if (targets.Count == 0)
            {
                report = "변환할 머티리얼이 없습니다.";
                return;
            }

            bool ok = EditorUtility.DisplayDialog(
                "변환 실행",
                $"{folderPath}\n\n머티리얼 {targets.Count}개를 URP/Lit 으로 변환합니다.\n" +
                "이 작업은 Undo 로 되돌릴 수 없습니다. 백업했나요?",
                "실행", "취소");

            if (!ok) return;

            var sb = new StringBuilder();
            sb.AppendLine($"[변환 완료] {folderPath}");
            sb.AppendLine();

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < targets.Count; i++)
                {
                    var mat = targets[i];
                    EditorUtility.DisplayProgressBar(
                        "HDRP→URP 변환", mat.name, (float)i / targets.Count);

                    sb.AppendLine(Convert(mat, urpLit));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            sb.AppendLine();
            sb.AppendLine($"총 {targets.Count}개 변환.");
            sb.AppendLine("씬뷰에서 확인하고, 잎사귀/유리처럼 투명이 필요한 머티리얼은");
            sb.AppendLine("Surface Type 을 직접 Transparent 또는 Alpha Clipping 으로 바꿔주세요.");

            report = sb.ToString();
            Debug.Log(report);
        }

        /// <summary>
        /// 머티리얼 하나를 URP/Lit 으로 변환한다. 반환값은 리포트 한 줄.
        /// </summary>
        private static string Convert(Material mat, Shader urpLit)
        {
            // 셰이더를 바꾸면 기존 프로퍼티 접근이 불가능해지므로 먼저 다 읽어둔다.
            var textures = ReadSavedTextures(mat);
            var floats = ReadSavedFloats(mat);
            var colors = ReadSavedColors(mat);

            var baseMap = Pick(textures, BaseMapCandidates);
            var normalMap = Pick(textures, NormalMapCandidates);
            var maskMap = Pick(textures, MaskMapCandidates);
            var emissionMap = Pick(textures, EmissionMapCandidates);

            // 머티리얼 배리언트는 셰이더를 부모에게서 물려받는다.
            // 배리언트에 직접 대입하면 유니티가 무시하면서
            // "Trying to set shader on a Material Variant" 경고를 낸다.
            // TryConvert 에서 부모를 먼저 변환하도록 정렬하므로 여기선 건너뛰면 된다.
            bool isVariant = mat.parent != null;
            if (!isVariant)
            {
                mat.shader = urpLit;
            }
            else if (mat.shader != urpLit)
            {
                return $"● {mat.name}  (배리언트 — 부모 '{mat.parent.name}' 의 셰이더가 아직 " +
                       "URP/Lit 이 아닙니다. 부모를 먼저 변환한 뒤 다시 실행하세요)";
            }

            // --- Base ---
            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);

            if (TryPickColor(colors, out var baseColor, "_BaseColor", "_Color"))
                mat.SetColor("_BaseColor", baseColor);
            else
                mat.SetColor("_BaseColor", Color.white);

            // --- Normal ---
            if (normalMap != null)
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.SetFloat("_BumpScale", PickFloat(floats, 1f, "_NormalScale", "_BumpScale"));
                mat.EnableKeyword("_NORMALMAP");
            }
            else
            {
                mat.DisableKeyword("_NORMALMAP");
            }

            // --- Metallic / Smoothness / Occlusion ---
            // HDRP MaskMap = R:Metallic G:Occlusion B:Detail A:Smoothness
            // URP  _MetallicGlossMap = R:Metallic A:Smoothness / _OcclusionMap = G:Occlusion
            // 채널 배치가 같으므로 동일 텍스처를 두 슬롯에 물린다.
            mat.SetFloat("_WorkflowMode", 1f); // Metallic

            if (maskMap != null)
            {
                mat.SetTexture("_MetallicGlossMap", maskMap);
                mat.SetTexture("_OcclusionMap", maskMap);
                mat.SetFloat("_SmoothnessTextureChannel", 0f); // Metallic Alpha
                mat.SetFloat("_Smoothness", PickFloat(floats, 1f, "_SmoothnessRemapMax", "_Smoothness"));
                mat.SetFloat("_OcclusionStrength", 1f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.EnableKeyword("_OCCLUSIONMAP");
            }
            else
            {
                mat.SetFloat("_Metallic", PickFloat(floats, 0f, "_Metallic"));
                mat.SetFloat("_Smoothness", PickFloat(floats, 0.5f, "_Smoothness"));
                mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                mat.DisableKeyword("_OCCLUSIONMAP");
            }

            // --- Emission ---
            if (emissionMap != null)
            {
                mat.SetTexture("_EmissionMap", emissionMap);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

                if (TryPickColor(colors, out var emissive, "_EmissiveColor", "_EmissionColor"))
                    mat.SetColor("_EmissionColor", emissive);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            // --- Surface ---
            // HDRP _SurfaceType: 0=Opaque 1=Transparent / URP _Surface: 0=Opaque 1=Transparent
            float hdrpSurface = PickFloat(floats, 0f, "_SurfaceType");
            bool transparent = hdrpSurface > 0.5f;
            bool alphaClip = PickFloat(floats, 0f, "_AlphaCutoffEnable") > 0.5f;

            mat.SetFloat("_Surface", transparent ? 1f : 0f);
            mat.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            mat.SetFloat("_Cutoff", PickFloat(floats, 0.5f, "_AlphaCutoff", "_Cutoff"));

            if (alphaClip) mat.EnableKeyword("_ALPHATEST_ON");
            else mat.DisableKeyword("_ALPHATEST_ON");

            if (transparent)
            {
                mat.SetFloat("_Blend", 0f); // Alpha
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
            }
            else
            {
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetFloat("_ZWrite", 1f);
                mat.renderQueue = alphaClip
                    ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest
                    : (int)UnityEngine.Rendering.RenderQueue.Geometry;
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
            }

            // HDRP _CullMode / _DoubleSidedEnable → URP _Cull (0=Off 1=Front 2=Back)
            bool doubleSided = PickFloat(floats, 0f, "_DoubleSidedEnable") > 0.5f;
            mat.SetFloat("_Cull", doubleSided ? 0f : 2f);
            mat.doubleSidedGI = doubleSided;

            EditorUtility.SetDirty(mat);

            var missing = new List<string>();
            if (baseMap == null) missing.Add("BaseMap");
            if (normalMap == null) missing.Add("Normal");
            if (maskMap == null) missing.Add("Mask");

            string note = missing.Count > 0 ? $"  (텍스처 없음: {string.Join(", ", missing)})" : "";
            string variantNote = isVariant ? $"  [배리언트 ← {mat.parent.name}]" : "";
            return $"● {mat.name}{variantNote}{note}";
        }

        // ---------------------------------------------------------------
        // 직렬화 데이터 직접 읽기
        //
        // 셰이더가 유실되면 Material.GetTexture 등이 동작하지 않는다.
        // (현재 셰이더에 없는 프로퍼티는 조회되지 않기 때문)
        // .mat 파일에는 값이 그대로 남아 있으므로 SerializedObject 로 꺼낸다.
        // ---------------------------------------------------------------

        private static Dictionary<string, Texture> ReadSavedTextures(Material mat)
        {
            var result = new Dictionary<string, Texture>();
            var so = new SerializedObject(mat);
            var array = so.FindProperty("m_SavedProperties.m_TexEnvs");
            if (array == null) return result;

            for (int i = 0; i < array.arraySize; i++)
            {
                var entry = array.GetArrayElementAtIndex(i);
                string key = entry.FindPropertyRelative("first").stringValue;
                var texProp = entry.FindPropertyRelative("second.m_Texture");
                if (string.IsNullOrEmpty(key) || texProp == null) continue;

                result[key] = texProp.objectReferenceValue as Texture;
            }

            return result;
        }

        private static Dictionary<string, float> ReadSavedFloats(Material mat)
        {
            var result = new Dictionary<string, float>();
            var so = new SerializedObject(mat);
            var array = so.FindProperty("m_SavedProperties.m_Floats");
            if (array == null) return result;

            for (int i = 0; i < array.arraySize; i++)
            {
                var entry = array.GetArrayElementAtIndex(i);
                string key = entry.FindPropertyRelative("first").stringValue;
                if (string.IsNullOrEmpty(key)) continue;

                result[key] = entry.FindPropertyRelative("second").floatValue;
            }

            return result;
        }

        private static Dictionary<string, Color> ReadSavedColors(Material mat)
        {
            var result = new Dictionary<string, Color>();
            var so = new SerializedObject(mat);
            var array = so.FindProperty("m_SavedProperties.m_Colors");
            if (array == null) return result;

            for (int i = 0; i < array.arraySize; i++)
            {
                var entry = array.GetArrayElementAtIndex(i);
                string key = entry.FindPropertyRelative("first").stringValue;
                if (string.IsNullOrEmpty(key)) continue;

                result[key] = entry.FindPropertyRelative("second").colorValue;
            }

            return result;
        }

        // ---------------------------------------------------------------
        // 유틸
        // ---------------------------------------------------------------

        private static Texture Pick(Dictionary<string, Texture> source, string[] candidates)
        {
            foreach (string key in candidates)
            {
                if (source.TryGetValue(key, out var tex) && tex != null)
                    return tex;
            }
            return null;
        }

        private static float PickFloat(Dictionary<string, float> source, float fallback, params string[] candidates)
        {
            foreach (string key in candidates)
            {
                if (source.TryGetValue(key, out float value))
                    return value;
            }
            return fallback;
        }

        private static bool TryPickColor(Dictionary<string, Color> source, out Color result, params string[] candidates)
        {
            foreach (string key in candidates)
            {
                if (source.TryGetValue(key, out result))
                    return true;
            }

            result = Color.white;
            return false;
        }

        /// <summary>
        /// 셰이더가 유실되어 유니티가 에러 셰이더로 대체한 상태인지.
        /// 이 상태가 씬에서 핫핑크로 보인다.
        /// </summary>
        /// <summary>
        /// 머티리얼 배리언트의 상속 깊이. 일반 머티리얼은 0.
        /// 부모를 먼저 변환하도록 정렬하는 데 쓴다.
        /// 순환 참조가 있어도 멈추도록 상한을 둔다.
        /// </summary>
        private static int GetVariantDepth(Material mat)
        {
            int depth = 0;
            var current = mat.parent;

            while (current != null && depth < 16)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

        private static bool IsShaderBroken(Material mat)
        {
            if (mat.shader == null) return true;

            string name = mat.shader.name;
            return name == "Hidden/InternalErrorShader"
                || name.StartsWith("Hidden/Core/FallbackError")
                || name.Contains("InternalErrorShader");
        }

        private static List<Material> LoadMaterials(string folderPath)
        {
            return AssetDatabase
                .FindAssets("t:Material", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Select(AssetDatabase.LoadAssetAtPath<Material>)
                .Where(m => m != null)
                .OrderBy(m => m.name)
                .ToList();
        }
    }
}
