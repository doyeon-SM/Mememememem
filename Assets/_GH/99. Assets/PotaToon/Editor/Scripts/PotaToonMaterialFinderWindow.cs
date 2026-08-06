using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace PotaToon.Editor
{
    public class PotaToonMaterialFinderWindow : EditorWindow
    {
        private static readonly string[] k_GeneralShaderNames = { PotaToonGUIUtility.k_FullGeneralPath, PotaToonGUIUtility.k_SimpleGeneralPath };
        private const string k_EyeShaderName = "PotaToon/Eye";
        private static List<Material> s_FoundMaterials = new List<Material>();
        private static List<Material> s_FoundEyeMaterials = new List<Material>();
        private Vector2 m_ScrollPosition;

        [MenuItem("PotaToon/View all materials Using PotaToon Shader in this scene")]
        public static void ShowWindow()
        {
            PotaToonMaterialFinderWindow window = GetWindow<PotaToonMaterialFinderWindow>("PotaToon Shader Material Finder");
            window.SearchMaterials(s_FoundMaterials, k_GeneralShaderNames);
            window.SearchMaterials(s_FoundEyeMaterials, new[] { k_EyeShaderName });
        }

        private void OnGUI()
        {
            GUILayout.Label($"Searching for Shader: {string.Join(", ", k_GeneralShaderNames)}", EditorStyles.boldLabel);

            if (GUILayout.Button("Refresh List"))
            {
                SearchMaterials(s_FoundMaterials, k_GeneralShaderNames);
                SearchMaterials(s_FoundEyeMaterials, new[] { k_EyeShaderName });
            }

            GUILayout.Space(10);

            if (s_FoundMaterials.Count == 0)
            {
                EditorGUILayout.HelpBox("No materials using the general PotaToon shaders were found in the scene.", MessageType.Info);
            }

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);

            foreach (var mat in s_FoundMaterials)
            {
                DrawMaterialRow(mat);
            }

            GUILayout.Label($"🔍 Searching for Shader: {k_EyeShaderName}", EditorStyles.boldLabel);
            GUILayout.Space(10);

            foreach (var mat in s_FoundEyeMaterials)
            {
                DrawMaterialRow(mat);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawMaterialRow(Material mat)
        {
            EditorGUILayout.BeginHorizontal("box");

            const float previewSize = 35f;
            EditorGUILayout.BeginVertical(GUILayout.Width(previewSize), GUILayout.Height(previewSize));
            Texture previewTexture = AssetPreview.GetAssetPreview(mat);
            if (previewTexture)
            {
                Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.ExpandWidth(false));
                GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUILayout.Label("No Preview", GUILayout.Width(previewSize), GUILayout.Height(previewSize));
            }
            EditorGUILayout.EndVertical();

            GUILayout.Label(mat.name, GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginHorizontal(GUILayout.Width(180));
            if (GUILayout.Button("Select", GUILayout.Width(70)))
            {
                Selection.activeObject = mat;
                EditorGUIUtility.PingObject(mat);
            }

            if (GUILayout.Button("Find in Project", GUILayout.Width(100)))
            {
                EditorUtility.FocusProjectWindow();
                EditorGUIUtility.PingObject(mat);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndHorizontal();
        }

        private void SearchMaterials(List<Material> targetMaterials, string[] targetShaderNames)
        {
            targetMaterials.Clear();
            var shaders = new List<Shader>();
            foreach (var shaderName in targetShaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                    shaders.Add(shader);
            }

            if (shaders.Count == 0)
            {
                Debug.LogError($"[PotaToon] Shaders not found: {string.Join(", ", targetShaderNames)}");
                return;
            }

#if UNITY_6000_4_OR_NEWER
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
#else
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#endif
            foreach (Renderer renderer in renderers)
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null && shaders.Contains(mat.shader) && !targetMaterials.Contains(mat))
                        targetMaterials.Add(mat);
                }
            }

            Debug.Log($"<color=cyan>[PotaToon] Found {targetMaterials.Count} materials using shaders '{string.Join(", ", targetShaderNames)}'</color>");
        }
    }
}