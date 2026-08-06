using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PotaToon.Editor
{
    public class PotaToonEditorUtility : EditorWindow
    {
        private static readonly string k_PotaToonFeatureName = "PotaToonFeature";

        internal static void PotaToonLog(object msg, bool isError = false)
        {
            if (isError)
            {
                Debug.LogError("<color=red>[PotaToon] " + msg + "</color>");
            }
            else
            {
                Debug.Log("<color=cyan>[PotaToon] " + msg +"</color>");
            }
        }
        
        [MenuItem("PotaToon/Auto Setup for current scene", false, 1)]
        private static void AutoSetupCurrentScene()
        {
            var currentPipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (currentPipeline == null)
            {
                PotaToonLog("Current render pipeline is not using URP.", true);
                return;
            }

#if UNITY_6000_0_OR_NEWER
            var rendererDataList = currentPipeline.rendererDataList;
            if (rendererDataList.Length == 0)
            {
                PotaToonLog("Couldn't find URP RendererData.", true);
                return;
            }
#else
            var rendererDataList = GetRendererDataList(currentPipeline);
            if (rendererDataList == null || rendererDataList.Length == 0)
            {
                PotaToonLog("Couldn't find URP RendererData.", true);
                return;
            }
#endif

            bool allowToAddRendererFeature = EditorUtility.DisplayDialog("PotaToon Auto Setup",
                $"This will add 'PotaToon' renderer feature to all linked renderers in [{currentPipeline.name}] asset.\n If you want to add it to a specific renderer, please add it manually.",
                "OK", "Cancel");

            if (allowToAddRendererFeature)
            {
                bool succeed = false;
                foreach (var renderer in rendererDataList)
                {
                    if (renderer is UniversalRendererData urpRendererData)
                    {
                        AddRendererFeature(urpRendererData);
                        succeed = true;
                    }
                }

                if (!succeed)
                    PotaToonLog("Couldn't find URP RendererData.", true);
            }

            AddPotaToonVolume();
            
            PotaToonLog($"Auto setup for editor is completed. Add [PotaToonCharacter] component to your characters. Please note that you need to add [PotaToonFeature] to other UniversalRendererData assets manually if you use a different URP settings in the build.");
        }
        
#if !UNITY_6000_0_OR_NEWER
        private static ScriptableRendererData[] GetRendererDataList(UniversalRenderPipelineAsset urp)
        {
            var so = new SerializedObject(urp);
            var prop = so.FindProperty("m_RendererDataList") ?? so.FindProperty("rendererDataList");
            if (prop == null || !prop.isArray)
                return null;

            var list = new ScriptableRendererData[prop.arraySize];
            for (int i = 0; i < prop.arraySize; i++)
                list[i] = prop.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
            
            return list;
        }
#endif

        private static void AddRendererFeature(UniversalRendererData rendererData)
        {
            // Return if already setup
            if (rendererData.rendererFeatures.Any(f => f.GetType().Name == k_PotaToonFeatureName))
            {
                PotaToonLog($"{k_PotaToonFeatureName} is already added in your renderer data.");
                return;
            }

            // Create PotaToon Renderer Feature
            ScriptableRendererFeature newFeature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(k_PotaToonFeatureName);
            if (newFeature == null)
            {
                PotaToonLog($"Failed to create instance of {k_PotaToonFeatureName}.", true);
                return;
            }

            newFeature.name = k_PotaToonFeatureName;
            rendererData.rendererFeatures.Add(newFeature);
            rendererData.SetDirty();

            AssetDatabase.AddObjectToAsset(newFeature, rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        private static void AddPotaToonVolume()
        {
            // Find volume
#if UNITY_6000_4_OR_NEWER
            Volume globalVolume = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include).FirstOrDefault(v => v.isGlobal);
#else
            Volume globalVolume = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(v => v.isGlobal);
#endif

            // Create if needed
            if (globalVolume == null)
            {
                GameObject volumeGO = new GameObject("Global Volume");
                globalVolume = volumeGO.AddComponent<Volume>();
                globalVolume.isGlobal = true;

                Undo.RegisterCreatedObjectUndo(volumeGO, "Create Global Volume");

                PotaToonLog("New Global Volume created.");
            }

            // Add PotaToon Volume Component
            if (globalVolume.sharedProfile == null)
            {
                globalVolume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                globalVolume.sharedProfile.name = "PotaToon Global Volume";
                Undo.RegisterCompleteObjectUndo(globalVolume, "Create Volume Profile");
                if (EditorUtility.IsPersistent(globalVolume))
                {
                    AssetDatabase.AddObjectToAsset(globalVolume.sharedProfile, globalVolume);
                    EditorUtility.SetDirty(globalVolume);
                    AssetDatabase.SaveAssets();
                }
            }

            var potaToonType = typeof(PotaToon);
            if (!globalVolume.sharedProfile.TryGet(potaToonType, out VolumeComponent existingComponent))
            {
                var asset = globalVolume.sharedProfile;
                var component = globalVolume.sharedProfile.Add(potaToonType);
                
                if (EditorUtility.IsPersistent(asset))
                    AssetDatabase.AddObjectToAsset(component, asset);
                
                if (EditorUtility.IsPersistent(asset))
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssets();
                }
            }
            else
            {
                PotaToonLog($"PotaToon Volume is already added to Global Volume.");
            }
        }
        
        [MenuItem("PotaToon/Duplicate selected character to PotaToon character prefab", false, 2)]
        private static void DuplicateSelectedPrefab()
        {
            GameObject selectedObject = Selection.activeGameObject;

            if (selectedObject == null)
            {
                PotaToonLog("No object selected! Please select a object or prefab in the Hierarchy/Project window.", true);
                return;
            }
            
            string selectedPath = EditorUtility.SaveFolderPanel("Select Save Folder", Application.dataPath, "");
            if (string.IsNullOrEmpty(selectedPath))
            {
                PotaToonLog("No folder selected. Operation cancelled.", true);
                return;
            }
            
            if (!selectedPath.StartsWith(Application.dataPath))
            {
                PotaToonLog("Please select a folder inside the 'Assets' directory!", true);
                return;
            }

            string savePath = "Assets" + selectedPath.Substring(Application.dataPath.Length);

            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }
            
            string newPrefabPath = $"{savePath}/{selectedObject.name}_PotaToon.prefab";

            GameObject newPrefabInstance = Object.Instantiate(selectedObject);
            newPrefabInstance.name = selectedObject.name + "_PotaToon";
            
            // Remove existing component
            var prevPotaToonCharacter = newPrefabInstance.GetComponent<PotaToonCharacter>();
            if (prevPotaToonCharacter != null)
            {
                DestroyImmediate(prevPotaToonCharacter);
            }

            // Duplicate materials
            var potaToonShader = Shader.Find(PotaToonGUIUtility.k_Paths[0]);
            var potaToonEyeShader = Shader.Find(PotaToonGUIUtility.k_Paths[2]);
            var materialMap = new Dictionary<Material, Material>();
            
            var renderers = newPrefabInstance.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                Material[] originalMaterials = renderer.sharedMaterials;
                var newMaterials = new Material[originalMaterials.Length];
                for (int i = 0; i < originalMaterials.Length; i++)
                {
                    Material originalMaterial = renderer.sharedMaterials[i];
                    if (originalMaterial == null) continue;
                    
                    if (materialMap.TryGetValue(originalMaterial, out Material existingMaterial))
                    {
                        newMaterials[i] = existingMaterial;
                    }
                    else
                    {
                        string materialPath = AssetDatabase.GetAssetPath(originalMaterial);
                        string materialName = Path.GetFileNameWithoutExtension(materialPath);
                        string newMaterialPath = $"{savePath}/{materialName}_PotaToon.mat";

                        var name = materialName.ToLower();
                        Material newMaterial = name.Equals("eye") ? new Material(potaToonEyeShader) : new Material(potaToonShader);
                        CopySettings(originalMaterial, newMaterial, IsSkin(name));
                        AssetDatabase.CreateAsset(newMaterial, newMaterialPath);

                        materialMap[originalMaterial] = newMaterial;
                        newMaterials[i] = newMaterial;
                    }

                }
                renderer.sharedMaterials = newMaterials;
            }
            
            // Add PotaToonCharacter Component
            var potaToonCharacter = newPrefabInstance.AddComponent<PotaToonCharacter>();
            potaToonCharacter.UpdateMaterials();
            potaToonCharacter.LoadPropertyHistoryData();

            var savedObject = PrefabUtility.SaveAsPrefabAsset(newPrefabInstance, newPrefabPath);
            Object.DestroyImmediate(newPrefabInstance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (savedObject != null)
            {
                PotaToonLog($"Prefab duplicated successfully! Saved at: {newPrefabPath}");
            }
            else
            {
                PotaToonLog($"Couldn't duplicate the selected object. This could be caused because of the missing script in the prefab. Please Use the 'Convert selected character materials to PotaToon materials' instead after duplicating the object manually.", true);
            }
        }

        [MenuItem("PotaToon/Convert selected character materials to PotaToon materials", false, 3)]
        private static void ConvertSelectedCharacterMaterials()
        {
            GameObject selectedObject = Selection.activeGameObject;

            if (selectedObject == null)
            {
                PotaToonLog("No object selected! Please select a object or prefab in the Hierarchy/Project window.", true);
                return;
            }
            
            string selectedPath = EditorUtility.SaveFolderPanel("Select Save Folder", Application.dataPath, "");
            if (string.IsNullOrEmpty(selectedPath))
            {
                PotaToonLog("No folder selected. Operation cancelled.", true);
                return;
            }
            
            if (!selectedPath.StartsWith(Application.dataPath))
            {
                PotaToonLog("Please select a folder inside the 'Assets' directory!", true);
                return;
            }

            string savePath = "Assets" + selectedPath.Substring(Application.dataPath.Length);

            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }
            
            // Remove existing component
            var prevPotaToonCharacter = selectedObject.GetComponent<PotaToonCharacter>();
            if (prevPotaToonCharacter != null)
            {
                DestroyImmediate(prevPotaToonCharacter);
            }
            
            // Convert materials
            var potaToonShader = Shader.Find(PotaToonGUIUtility.k_Paths[0]);
            var potaToonEyeShader = Shader.Find(PotaToonGUIUtility.k_Paths[2]);
            
            var renderers = selectedObject.GetComponentsInChildren<Renderer>();
            Dictionary<Material, Material> materialMap = new Dictionary<Material, Material>();
            int undoGroup = Undo.GetCurrentGroup();
            
            foreach (Renderer renderer in renderers)
            {
                Material[] originalMaterials = renderer.sharedMaterials;
                Material[] newMaterials = new Material[originalMaterials.Length];

                for (int i = 0; i < originalMaterials.Length; i++)
                {
                    Material originalMaterial = originalMaterials[i];
                    if (originalMaterial == null) continue;

                    var name = originalMaterial.name.ToLower();
                    var targetShader = name.Equals("eye") ? potaToonEyeShader : potaToonShader;
                    if (materialMap.TryGetValue(originalMaterial, out Material existingMaterial))
                    {
                        newMaterials[i] = existingMaterial;
                    }
                    else
                    {
                        Material newMaterial = new Material(originalMaterial);
                        newMaterial.shader = targetShader;

                        Undo.RegisterCompleteObjectUndo(newMaterial, "Change Material Shader");

                        materialMap[originalMaterial] = newMaterial;
                        newMaterials[i] = newMaterial;
                        
                        CopySettings(originalMaterial, newMaterial, IsSkin(name));

                        string materialPath = AssetDatabase.GetAssetPath(originalMaterial);
                        string materialName = Path.GetFileNameWithoutExtension(materialPath);
                        string newMaterialPath = $"{savePath}/{materialName}_PotaToon.mat";
                        AssetDatabase.CreateAsset(newMaterial, newMaterialPath);
                    }
                }
                
                Undo.RecordObject(renderer, "Assign New Materials");
                renderer.sharedMaterials = newMaterials;
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Undo.CollapseUndoOperations(undoGroup);
            
            // Add PotaToon
            var potaToonCharacter = selectedObject.AddComponent<PotaToonCharacter>();
            potaToonCharacter.UpdateMaterials();
            potaToonCharacter.LoadPropertyHistoryData();

            PotaToonLog("Saved all converted materials into " + savePath);
        }

        private static bool IsSkin(string name)
        {
            return name.Contains("skin") || name.Contains("body") || name.Contains("face");
        }

        private static void CopySettings(Material original, Material copy, bool isSkin)
        {
            CopyTextures(original, copy);
            CopyColors(original, copy, isSkin);
            CopyRenderQueue(original, copy);
            CopyNumericData(original, copy);
            CopyAlphaMask(original, copy);
            CopyBlendingMode(original, copy);
        }
        
        private static void CopyTextures(Material original, Material copy)
        {
            Shader shader = original.shader;
            int propertyCount = shader.GetPropertyCount();

            for (int i = 0; i < propertyCount; i++)
            {
                if (shader.GetPropertyType(i).Equals(ShaderPropertyType.Texture))
                {
                    string propertyName = shader.GetPropertyName(i);
                    if (original.HasProperty(propertyName) && copy.HasProperty(propertyName))
                    {
                        Texture texture = original.GetTexture(propertyName);
                        if (texture != null)
                            copy.SetTexture(propertyName, texture);
                    }
                }
            }
            
            // Set BaseMap to MainTex
            if (original.HasProperty("_BaseMap"))
            {
                Texture texture = original.GetTexture("_BaseMap");
                if (texture != null)
                    copy.SetTexture("_MainTex", texture);
            }
            
            // Set BumpMap to NormalMap
            if (original.HasProperty("_BumpMap"))
            {
                Texture texture = original.GetTexture("_BumpMap");
                if (texture != null)
                    copy.SetTexture("_NormalMap", texture);
            }
            
            // Set ShadowColorTex to ShadeMap
            if (original.HasProperty("_ShadowColorTex"))
            {
                Texture texture = original.GetTexture("_ShadowColorTex");
                if (texture != null)
                    copy.SetTexture("_ShadeMap", texture);
            }

            // Set UseShadeMap
            if (copy.HasProperty("_ShadeMap") && copy.GetTexture("_ShadeMap") != null)
            {
                copy.SetInt("_UseShadeMap", 1);
            }
            
            // If normal map exists, use the normal map
            if (copy.HasProperty("_NormalMap") && copy.GetTexture("_NormalMap") != null)
            {
                copy.SetInt("_UseNormalMap", 1);
            }

            // Set EmissionBlendMask to EmissionMask
            if (original.HasProperty("_EmissionBlendMask"))
            {
                Texture texture = original.GetTexture("_EmissionBlendMask");
                if (texture != null)
                    copy.SetTexture("_EmissionMask", texture);
            }
            
            // Set MatCapBlendMask to MatCapMask
            if (original.HasProperty("_MatCapBlendMask"))
            {
                Texture texture = original.GetTexture("_MatCapBlendMask");
                if (texture != null)
                    copy.SetTexture("_MatCapMask", texture);
            }
        }

        private static AlphaMaskMode ConvertLilToonAlphaMaskMode(int mode)
        {
            switch (mode)
            {
                case 1:
                    return AlphaMaskMode.Replace;
                case 2:
                    return AlphaMaskMode.Multiply;
                case 3:
                    return AlphaMaskMode.Add;
                case 4:
                    return AlphaMaskMode.Subtract;
                case 0:
                default:
                    return AlphaMaskMode.None;
            }
        }

        private static void CopyAlphaMask(Material original, Material copy)
        {
            if (!copy.HasProperty("_ClippingMask"))
                return;

            bool hasAlphaMask = original.HasProperty("_AlphaMask");
            if (hasAlphaMask)
            {
                Texture texture = original.GetTexture("_AlphaMask");
                if (texture != null)
                    copy.SetTexture("_ClippingMask", texture);

                copy.SetTextureScale("_ClippingMask", original.GetTextureScale("_AlphaMask"));
                copy.SetTextureOffset("_ClippingMask", original.GetTextureOffset("_AlphaMask"));

                if (copy.HasProperty("_ClippingMaskCH"))
                    copy.SetInt("_ClippingMaskCH", (int)MaskChannel.R);
            }

            if (copy.HasProperty("_AlphaMaskMode"))
            {
                if (hasAlphaMask && original.HasProperty("_AlphaMaskMode"))
                    copy.SetInt("_AlphaMaskMode", (int)ConvertLilToonAlphaMaskMode(original.GetInt("_AlphaMaskMode")));
                else if (hasAlphaMask)
                    copy.SetInt("_AlphaMaskMode", (int)AlphaMaskMode.None);
            }

            if (original.HasProperty("_AlphaMaskScale") && copy.HasProperty("_AlphaMaskScale"))
                copy.SetFloat("_AlphaMaskScale", original.GetFloat("_AlphaMaskScale"));

            if (original.HasProperty("_AlphaMaskValue") && copy.HasProperty("_AlphaMaskValue"))
                copy.SetFloat("_AlphaMaskValue", original.GetFloat("_AlphaMaskValue"));
        }
        
        private static void CopyColors(Material original, Material copy, bool isSkin)
        {
            Shader shader = original.shader;
            int propertyCount = shader.GetPropertyCount();
            
            // Set default shadow color for skin type
            if (isSkin)
            {
                copy.SetColor("_ShadeColor", new Color(1f, 0.75f, 0.75f));
            }

            // Copy colors
            for (int i = 0; i < propertyCount; i++)
            {
                if (shader.GetPropertyType(i).Equals(ShaderPropertyType.Color))
                {
                    string propertyName = shader.GetPropertyName(i);
                    if (original.HasProperty(propertyName) && copy.HasProperty(propertyName))
                    {
                        var color = original.GetColor(propertyName);
                        copy.SetColor(propertyName, color);
                    }
                }
            }
            
            // Set Color to BaseColor
            if (original.HasProperty("_Color"))
            {
                var color = original.GetColor("_Color");
                copy.SetColor("_BaseColor", color);
            }
            
            // Set ShadowColor to ShadeColor
            if (original.HasProperty("_ShadowColor"))
            {
                var color = original.GetColor("_ShadowColor");
                copy.SetColor("_ShadeColor", color);
            }
        }
        
        private static void CopyRenderQueue(Material original, Material copy)
        {
            copy.renderQueue = original.renderQueue;
            if (copy.HasProperty("_AutoRenderQueue"))
            {
                copy.SetInt("_AutoRenderQueue", 0);
            }

            if (copy.renderQueue < 2450)
                copy.SetInt("_SurfaceType", (int)SurfaceType.Opaque);
            else if (copy.renderQueue < 2501)
                copy.SetInt("_SurfaceType", (int)SurfaceType.Cutout);
            else
                copy.SetInt("_SurfaceType", (int)SurfaceType.Transparent);
        }
        
        private static void CopyNumericData(Material original, Material copy)
        {
            Shader shader = original.shader;
            int propertyCount = shader.GetPropertyCount();

            for (int i = 0; i < propertyCount; i++)
            {
                var propertyType = shader.GetPropertyType(i);
                if (propertyType.Equals(ShaderPropertyType.Float) || propertyType.Equals(ShaderPropertyType.Range))
                {
                    string propertyName = shader.GetPropertyName(i);
                    if (original.HasProperty(propertyName) && copy.HasProperty(propertyName))
                    {
                        var data = original.GetFloat(propertyName);
                        copy.SetFloat(propertyName, data);
                    }
                }
                
                if (propertyType.Equals(ShaderPropertyType.Int))
                {
                    string propertyName = shader.GetPropertyName(i);
                    if (original.HasProperty(propertyName) && copy.HasProperty(propertyName))
                    {
                        var data = original.GetInt(propertyName);
                        copy.SetInt(propertyName, data);
                    }
                }
            }
        }

        private static void CopyBlendingMode(Material original, Material copy)
        {
            if (copy.renderQueue >= 2501)
                copy.SetInt("_Blend", (int)AlphaMode.Custom);
        }

        [MenuItem("PotaToon/Toggle Guide Warning", false, 10)]
        private static void TogglePotaToonGuideWarning()
        {
            PotaToon.guideWarningEnabled = !PotaToon.guideWarningEnabled;
        }
    }
}
