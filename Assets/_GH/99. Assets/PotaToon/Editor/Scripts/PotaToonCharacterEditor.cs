using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PotaToon.Editor
{
    [CustomEditor(typeof(PotaToonCharacter))]
    public class PotaToonCharacterEditor : UnityEditor.Editor
    {
        private static bool s_FoldoutChecks = true;
        private static bool s_FoldoutMaterials = true;
        private static bool s_FoldoutController = true;
        private static StringBuilder s_BoundsMismatchesString = new StringBuilder(512);
        private double m_LastUpdateTime = 0;
        
        // Validation
        private bool m_HasSettingsValidated = false;
        private bool m_IsBoundsCheckPassed = false;
        private bool m_IsSizeCheckPassed = false;
        private List<PotaToonMeshBoundsUtils.RendererBoundsComparison> m_BoundsMismatches = new List<PotaToonMeshBoundsUtils.RendererBoundsComparison>();
        private bool m_FoldoutBoundsMismatches = false;
        private static readonly GUIContent k_PerformanceModeContent = new GUIContent(
            "Performance Mode",
            "Much lighter and suitable for objects that do not need fine detail, or for game-oriented use. General and Face materials only.");

        private static bool IsPerformanceModeMaterial(Material material)
        {
            if (material == null || !material.HasProperty("_ToonType"))
                return false;

            int toonType = material.GetInt("_ToonType");
            return toonType == (int)ToonType.General || toonType == (int)ToonType.Face;
        }

        private static void GetPerformanceModeState(List<Material> materials, out bool hasSupportedMaterials, out bool hasMixedValue, out bool useSimpleMode)
        {
            hasSupportedMaterials = false;
            hasMixedValue = false;
            useSimpleMode = false;

            if (materials == null)
                return;

            for (int i = 0; i < materials.Count; i++)
            {
                var material = materials[i];
                if (!IsPerformanceModeMaterial(material))
                    continue;

                bool currentMode = PotaToonGUIUtility.GetShaderMode(material) == 1;
                if (!hasSupportedMaterials)
                {
                    hasSupportedMaterials = true;
                    useSimpleMode = currentMode;
                    continue;
                }

                if (useSimpleMode != currentMode)
                {
                    hasMixedValue = true;
                    return;
                }
            }
        }

        private static bool ApplyPerformanceMode(PotaToonCharacter character, bool useSimpleMode)
        {
            if (character == null || character.allMaterials == null)
                return false;

            bool changedAny = false;
            int shaderMode = useSimpleMode ? 1 : 0;
            for (int i = 0; i < character.allMaterials.Count; i++)
            {
                var material = character.allMaterials[i];
                if (!IsPerformanceModeMaterial(material))
                    continue;

                Undo.RecordObject(material, "Change Performance Mode");
                if (useSimpleMode && material.HasProperty("_SurfaceType") && material.GetInt("_SurfaceType") == (int)SurfaceType.Refraction)
                    material.SetInt("_SurfaceType", (int)SurfaceType.Transparent);

                changedAny |= PotaToonGUIUtility.ChangeShader(material, material.GetInt("_ToonType"), material.renderQueue, shaderMode, false);
                PotaToonShaderGUIBase.SyncMaterialStateAfterPresetApply(material);
                EditorUtility.SetDirty(material);
            }

            if (!changedAny)
                return false;

            character.UpdateMaterials();
            PotaToonGUIUtility.ShowNotification($"Performance Mode {(useSimpleMode ? "On" : "Off")}");
            return true;
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var character = target as PotaToonCharacter;
            if (character == null)
                return;

            if (!character.IsOwner)
            {
                PotaToonInfoField("This PotaToonCharacter is not the owner. Only the root PotaToonCharacter is valid for settings, materials, and checks. See the document below for more information.");
                PotaToonGUIUtility.DrawOpenDocsButton("https://potatoon.dev/features/character-component");
                serializedObject.ApplyModifiedProperties();
                return;
            }
            
            GUIStyle headerStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 30,
                normal = { textColor = Color.white }
            };
            
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 5, 5),
                margin = new RectOffset(5, 5, 5, 5)
            };
            
            GUIStyle borderStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(1, 1, 1, 1),
                margin = new RectOffset(5, 5, 5, 5),
                normal = { background = Texture2D.grayTexture }
            };

            // Update materials periodically.
            var currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - m_LastUpdateTime >= 10.0)
            {
                character.UpdateMaterials();
                m_LastUpdateTime = currentTime;
            }

            EditorGUILayout.BeginVertical(borderStyle);
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("Character Settings", new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.Space();
            character.head = (Transform)EditorGUILayout.ObjectField(new GUIContent("Head", "The head transform of this character."), character.head, typeof(Transform), true);
            
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            
            character.useDitherFade = EditorGUILayout.Toggle(new GUIContent("Dither Fade", "The dither fade feature makes characters translucent when they are close to the camera. This is especially useful in games where you want to see background objects through a character."), character.useDitherFade);
            if (character.useDitherFade)
            {
                if (!PotaToonSettingsProvider.EnableDitherFadePasses)
                {
                    DitherFadeProjectSettingsInfoField();
                }

                PotaToonInfoField("Dither Fade modifies the original material asset directly. If the material is shared by other Mesh Renderers, duplicate it and assign a separate material per mesh.");
                EditorGUI.indentLevel++;
                Undo.RecordObject(character, "Update Dither fade property");
                character.ditherFadeMinZ = Mathf.Max(0.01f, EditorGUILayout.FloatField(new GUIContent("Min Z", "Sets the nearest distance where dither fade peaks. Commonly the same as the camera near-clip."), character.ditherFadeMinZ));
                character.ditherFadeMaxZ = Mathf.Max(0.01f, EditorGUILayout.FloatField(new GUIContent("Max Z", "Sets the distance at which dither fade starts."), character.ditherFadeMaxZ));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            
            // Validation field
            if (GUILayout.Button((s_FoldoutChecks ? "▼ " : "► ") + "Character Checking", headerStyle))
                s_FoldoutChecks = !s_FoldoutChecks;
            if (s_FoldoutChecks)
            {
                PotaToonInfoField("[NOTE] Run check only if a character is in the default pose (T-pose).");
                
                // Check Settings
                if (!m_HasSettingsValidated) // Do auto check if not validated
                    CheckAllSettings(character.gameObject);
                
                if (GUILayout.Button("Check Settings", new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontSize = 14 }))
                    CheckAllSettings(character.gameObject);
                
                EditorGUILayout.BeginVertical(borderStyle);
                EditorGUILayout.BeginVertical(boxStyle);

                if (IsValidationPassed())
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(EditorGUIUtility.IconContent("d_greenLight"), GUILayout.Width(20f));
                    EditorGUILayout.LabelField("Looks Good!");
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    ValidationField(character.gameObject);
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            
            if (GUILayout.Button((s_FoldoutMaterials ? "▼ " : "► ") + "[Read Only] All Materials", headerStyle))
                s_FoldoutMaterials = !s_FoldoutMaterials;
            if (s_FoldoutMaterials)
            {
                if (GUILayout.Button( "Refresh Materials", new GUIStyle(GUI.skin.button) { fontSize = 14 }))
                {
                    character.UpdateMaterials();
                }
                EditorGUILayout.PropertyField(serializedObject.FindProperty("allMaterials"), true);
            }

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            
            if (GUILayout.Button((s_FoldoutController ? "▼ " : "► ") + "[Editor Only] All Materials Control", headerStyle))
                s_FoldoutController = !s_FoldoutController;
            if (s_FoldoutController)
            {
                PotaToonInfoField("[NOTE] This changes all materials directly. If you share materials for other characters, please duplicate materials first.");
                if (GUILayout.Button( "Duplicate Materials", new GUIStyle(GUI.skin.button) {  fontSize = 14 }))
                {
                    DuplicateMaterials(character);
                }
                
                EditorGUILayout.BeginVertical(borderStyle);
                EditorGUILayout.BeginVertical(boxStyle);

                GetPerformanceModeState(character.allMaterials, out bool hasSupportedMaterials, out bool hasMixedSimpleMode, out bool useSimpleMode);
                EditorGUI.BeginDisabledGroup(!hasSupportedMaterials);
                EditorGUI.showMixedValue = hasMixedSimpleMode;
                EditorGUI.BeginChangeCheck();
                bool newSimpleMode = EditorGUILayout.Toggle(k_PerformanceModeContent, useSimpleMode);
                bool simpleModeChanged = EditorGUI.EndChangeCheck();
                EditorGUI.showMixedValue = false;
                EditorGUI.EndDisabledGroup();

                if (simpleModeChanged && ApplyPerformanceMode(character, newSimpleMode))
                {
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(character);
                    return;
                }

                EditorGUI.BeginChangeCheck();
                character.baseColor = EditorGUILayout.ColorField("Base Color", character.baseColor);
                character.shadeColor = EditorGUILayout.ColorField("Shade Color", character.shadeColor);
                character.baseStep = EditorGUILayout.Slider("Base Step", character.baseStep, 0f, 1f);
                character.stepSmoothness = EditorGUILayout.Slider("Step Smoothness", character.stepSmoothness, 0f, 0.1f);

                character.receiveLightShadow = EditorGUILayout.Toggle("Receive Light Shadow", character.receiveLightShadow);
                character.useMidTone = EditorGUILayout.Toggle("Use Mid Tone", character.useMidTone);
                character.midTone = EditorGUILayout.ColorField("Mid Tone", character.midTone);
                character.midThickness = EditorGUILayout.Slider("Mid Thickness", character.midThickness, 0f, 1f);
                character.indirectDimmer = EditorGUILayout.Slider("Indirect Dimmer", character.indirectDimmer, 0f, 10f);

                character.rimLightColor = EditorGUILayout.ColorField("Rim Light Color", character.rimLightColor);
                character.rimPower = EditorGUILayout.Slider("Rim Power", character.rimPower, 0f, 1f);
                character.rimSmoothness = EditorGUILayout.Slider("Rim Smoothness", character.rimSmoothness, 0f, 0.5f);

                character.outlineWidth = EditorGUILayout.Slider("Outline Width", character.outlineWidth, 0f, 10f);
                character.outlineColor = EditorGUILayout.ColorField("Outline Color", character.outlineColor);

                character.hiLightColor = EditorGUILayout.ColorField("Hi-Light Color", character.hiLightColor);
                character.emissionColor = EditorGUILayout.ColorField("Emission Color", character.emissionColor);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();
            }

            if (EditorGUI.EndChangeCheck())
            {
                foreach (var mat in character.allMaterials)
                {
                    if (mat != null)
                        Undo.RecordObject(mat, "Update PotaToon Material Properties");
                }
                character.UpdateMaterialProperties();
                foreach (var mat in character.allMaterials)
                {
                    if (mat != null)
                        EditorUtility.SetDirty(mat);
                }
            }
            
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(character);
        }
        
        private void DuplicateMaterials(PotaToonCharacter target)
        {
            // Choose folder to save duplicated materials
            string folderPath = EditorUtility.OpenFolderPanel(
                "Select Folder to Save Materials",
                Application.dataPath,
                ""
            );
            if (string.IsNullOrEmpty(folderPath))
                return;

            if (!folderPath.StartsWith(Application.dataPath))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Folder",
                    "Please select a folder inside the project's Assets directory.",
                    "OK"
                );
                return;
            }

            string assetFolder = "Assets" + folderPath.Substring(Application.dataPath.Length);
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

            var materialMap = new Dictionary<Material, Material>();

            // Duplicate each unique material and create it as an asset
            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null || materialMap.ContainsKey(mat))
                        continue;

                    Material duplicated = new Material(mat)
                    {
                        name = mat.name
                    };

                    string newPath = AssetDatabase.GenerateUniqueAssetPath(
                        $"{assetFolder}/{duplicated.name}.mat"
                    );

                    AssetDatabase.CreateAsset(duplicated, newPath);
                    materialMap.Add(mat, duplicated);
                }
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Duplicate Materials");
            
            // Replace each renderer's materials with the duplicated versions
            foreach (var renderer in renderers)
            {
                var mats = renderer.sharedMaterials;
                bool replaced = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && materialMap.ContainsKey(mats[i]))
                    {
                        replaced = true;
                        break;
                    }
                }
                
                if (!replaced)
                    continue;
                
                Undo.RecordObject(renderer, "Duplicate Materials");
                
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && materialMap.TryGetValue(mats[i], out var newMat))
                    {
                        mats[i] = newMat;
                    }
                }
                
                renderer.sharedMaterials = mats;
                EditorUtility.SetDirty(renderer);
            }
            
            Undo.CollapseUndoOperations(undoGroup);

            // Save assets, refresh database, mark scene dirty
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(target.gameObject.scene);

            EditorUtility.DisplayDialog(
                "Done",
                "Materials have been duplicated and applied.",
                "OK"
            );
        }

        private void PotaToonInfoField(string msg)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(EditorGUIUtility.IconContent("console.infoicon"), GUILayout.Width(30f), GUILayout.ExpandHeight(true));
            GUI.contentColor = Color.yellow;
            EditorGUILayout.LabelField(msg, new GUIStyle(EditorStyles.textArea) { fontSize = 13 });
            GUI.contentColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DitherFadeProjectSettingsInfoField()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(EditorGUIUtility.IconContent("console.warnicon"), GUILayout.Width(30f), GUILayout.ExpandHeight(true));
            GUI.contentColor = Color.yellow;
            EditorGUILayout.LabelField("Dither Fade Passes is disabled in Project Settings. Player builds will strip the required shader passes.", new GUIStyle(EditorStyles.textArea) { fontSize = 13 });
            GUI.contentColor = Color.white;

            if (GUILayout.Button("Enable", new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontSize = 13 }, GUILayout.Width(60f), GUILayout.ExpandHeight(true)))
            {
                PotaToonSettingsProvider.SetDitherFadePassesEnabled(true);
                Repaint();
            }

            if (GUILayout.Button("Settings", new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontSize = 13 }, GUILayout.Width(80f), GUILayout.ExpandHeight(true)))
            {
                SettingsService.OpenProjectSettings("Project/PotaToon");
            }

            EditorGUILayout.EndHorizontal();
        }
        
#region Validation
        private void CheckAllSettings(GameObject root)
        {
            m_HasSettingsValidated = true;
            CheckBoundsMismatches(root);
            CheckCharacterSize(root, false);
        }

        private bool IsValidationPassed()
        {
            return m_IsBoundsCheckPassed && m_IsSizeCheckPassed;
        }

        private void ValidationField(GameObject root)
        {
            GUI.contentColor = Color.yellow;
            BoundsMismatchCheckField(root);
            CharacterSizeCheckField(root);
            GUI.contentColor = Color.white;
        }
        
        // Check functions
        private static float GetCharacterMaxSize(GameObject root, bool includeInactive)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive);
            if (renderers == null || renderers.Length == 0)
                return float.MaxValue;

            var combined = new Bounds(renderers[0].bounds.center, Vector3.zero);
            foreach (var r in renderers)
            {
                if (r == null)
                    continue;
                combined.Encapsulate(r.bounds);
            }

            return Mathf.Max(combined.size.x, combined.size.y, combined.size.z);
        }
        
        private void CheckCharacterSize(GameObject root, bool includeInactive)
        {
            m_IsSizeCheckPassed = GetCharacterMaxSize(root, includeInactive) < 3.0f;
        }

        private void UpdateBoundsMismatchesString()
        {
            s_BoundsMismatchesString.Clear();
            for (int i = 0; i < m_BoundsMismatches.Count; i++)
            {
                s_BoundsMismatchesString.Append(m_BoundsMismatches[i].renderer.gameObject.name);
                if (i < m_BoundsMismatches.Count - 1)
                    s_BoundsMismatchesString.Append(", ");
            }
        }
        
        private void CheckBoundsMismatches(GameObject root)
        {
            m_BoundsMismatches = PotaToonMeshBoundsUtils.FindBoundsMismatches(root, 0.25f, true, true);
            m_IsBoundsCheckPassed = m_BoundsMismatches.Count == 0;
            UpdateBoundsMismatchesString();
        }

        // Fields
        private static void CheckIconContent(bool passed)
        {
            GUILayout.Label(passed ? EditorGUIUtility.IconContent("d_greenLight") : EditorGUIUtility.IconContent("d_orangeLight"), new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(25));
        }

        private void BoundsMismatchCheckField(GameObject root)
        {
            if (m_IsBoundsCheckPassed)
                return;
            
            EditorGUILayout.BeginHorizontal();
            CheckIconContent(m_IsBoundsCheckPassed);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Bounds Size", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft, fontSize = 14 });
            EditorGUILayout.LabelField($"- {m_BoundsMismatches.Count} renderer bounds are bigger than mesh size.", EditorStyles.textArea);
            m_FoldoutBoundsMismatches = EditorGUILayout.Foldout(m_FoldoutBoundsMismatches, "See Details");
            if (m_FoldoutBoundsMismatches)
            {
                EditorGUILayout.LabelField($"{s_BoundsMismatchesString}", EditorStyles.textArea);
            }
            EditorGUILayout.EndVertical();
            
            // Fix button for bounds mismatches
            if (GUILayout.Button("Fix", new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontSize = 13 }, GUILayout.Width(60f), GUILayout.ExpandHeight(true)))
            {
                if (EditorUtility.DisplayDialog("Change character bounds", $"All renderer bounds will be changed in {root.name} object.", "Proceed",  "Cancel"))
                {
                    int group = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Apply Computed Mesh Bounds");
                    PotaToonMeshBoundsUtils.ApplyComputedBoundsToMeshes(root, true, true);
                    Undo.CollapseUndoOperations(group);
                    m_BoundsMismatches.Clear();
                    m_IsBoundsCheckPassed = true;
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
                    CheckCharacterSize(root, false);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void CharacterSizeCheckField(GameObject root)
        {
            if (m_IsSizeCheckPassed)
                return;
            
            EditorGUILayout.BeginHorizontal();
            CheckIconContent(m_IsSizeCheckPassed);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Character Size", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft, fontSize = 14 });
            EditorGUILayout.LabelField("- The character size is too big. Fix the bounds size check first if needed.", EditorStyles.textArea);
            EditorGUILayout.EndVertical();
            
            // Fix button for character size
            if (GUILayout.Button("Fix", new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontSize = 13 }, GUILayout.Width(60f), GUILayout.ExpandHeight(true)))
            {
                if (EditorUtility.DisplayDialog("Change character size", $"{root.name} object will be scaled.", "Proceed", "Cancel"))
                {
                    Undo.RecordObject(root.transform, "Fix Character Size");
                    var maxSize = GetCharacterMaxSize(root, true);
                    var scale = maxSize > 3.0f ? 3.0f / maxSize : 1.0f;
                    root.transform.localScale = root.transform.localScale * scale;
                    m_IsSizeCheckPassed = true;
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
#endregion
    }
}
