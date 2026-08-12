#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KMS.EditorTools
{
    public static class KMSWaterDeathVolumePrefabSetup
    {
        private const string PrefabFolder = "Assets/KMS/2.Prefabs/World/Hazards";
        private const string PrefabPath = PrefabFolder + "/KMS_WaterDeathVolume.prefab";

        [MenuItem("Tools/KMS/Create Water Death Volume Prefab")]
        public static void CreatePrefab()
        {
            EnsureFolder(PrefabFolder);

            GameObject root = new GameObject("KMS_WaterDeathVolume");
            try
            {
                BoxCollider trigger = root.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = new Vector3(0f, -0.5f, 0f);
                trigger.size = new Vector3(10f, 1f, 10f);

                root.AddComponent<KMSWaterDeathVolume>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[KMSWaterDeathVolumePrefabSetup] Created prefab at '{PrefabPath}'.");
        }

        private static void EnsureFolder(string folderPath)
        {
            string current = "Assets";
            string[] parts = folderPath.Split('/');
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
#endif
