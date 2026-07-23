using HDY.Inventory;
using KMS.InventoryDuped;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KMS.Editor
{
    /// <summary>
    /// 창고의 ID/C 버튼 외형을 복제해 KMS 플레이어 인벤토리용 공용 프리팹으로 만들고,
    /// 현재 사용 중인 인벤토리 Canvas 프리팹에 배치한다.
    /// </summary>
    public static class KMSInventorySortSetup
    {
        private const string WarehousePrefabPath = "Assets/HDY/2.Prefabs/UI/P_WarehouseRoot.prefab";
        private const string InventoryCanvasPath = "Assets/KMS/2.Prefabs/0714_InventoryCanvas_Root.prefab";
        private const string SortControlsPrefabPath = "Assets/KMS/2.Prefabs/P_InventorySortControls.prefab";
        private const string SortControlsName = "InventorySortControls";

        [MenuItem("KMS/Inventory/Install Player Inventory Sort Buttons")]
        public static void Install()
        {
            GameObject controlsPrefab = BuildControlsPrefab();
            if (controlsPrefab == null) return;

            GameObject inventoryRoot = PrefabUtility.LoadPrefabContents(InventoryCanvasPath);
            try
            {
                Transform inventoryPanel = FindDescendant(inventoryRoot.transform, "InventoryPanel");
                if (inventoryPanel == null)
                {
                    Debug.LogError($"[KMSInventorySortSetup] '{InventoryCanvasPath}'에서 InventoryPanel을 찾지 못했습니다.");
                    return;
                }

                Transform existing = FindDescendant(inventoryPanel, SortControlsName);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(controlsPrefab, inventoryPanel);
                instance.name = SortControlsName;
                instance.transform.SetAsLastSibling();

                RectTransform rect = instance.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.one;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-10f, 10f);
                rect.sizeDelta = new Vector2(110f, 50f);

                InventoryUI inventoryUI = inventoryRoot.GetComponentInChildren<InventoryUI>(true);
                InventorySortUI installedSortUI = instance.GetComponent<InventorySortUI>();
                if (inventoryUI == null || installedSortUI == null)
                {
                    Debug.LogError("[KMSInventorySortSetup] InventoryUI 또는 설치된 InventorySortUI를 찾지 못했습니다.");
                    return;
                }

                SerializedObject serializedInventoryUI = new SerializedObject(inventoryUI);
                serializedInventoryUI.FindProperty("sortUI").objectReferenceValue = installedSortUI;
                serializedInventoryUI.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(inventoryRoot, InventoryCanvasPath);
                Debug.Log($"[KMSInventorySortSetup] 플레이어 인벤토리 정렬 버튼 설치 완료: {InventoryCanvasPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(inventoryRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject BuildControlsPrefab()
        {
            GameObject warehouseRoot = PrefabUtility.LoadPrefabContents(WarehousePrefabPath);
            try
            {
                Transform source = FindDescendant(warehouseRoot.transform, "P_sort");
                if (source == null)
                {
                    Debug.LogError($"[KMSInventorySortSetup] '{WarehousePrefabPath}'에서 P_sort를 찾지 못했습니다.");
                    return null;
                }

                GameObject clone = Object.Instantiate(source.gameObject);
                clone.name = SortControlsName;
                clone.transform.SetParent(null, false);

                WarehouseSortUI warehouseSort = clone.GetComponent<WarehouseSortUI>();
                if (warehouseSort != null)
                {
                    Object.DestroyImmediate(warehouseSort);
                }

                RectTransform rect = clone.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(110f, 50f);

                Button idButton = FindDescendant(clone.transform, "B_id")?.GetComponent<Button>();
                Button categoryButton = FindDescendant(clone.transform, "B_category")?.GetComponent<Button>();
                if (idButton == null || categoryButton == null)
                {
                    Debug.LogError("[KMSInventorySortSetup] 창고의 B_id/B_category 버튼을 찾지 못했습니다.");
                    Object.DestroyImmediate(clone);
                    return null;
                }

                InventorySortUI sortUI = clone.AddComponent<InventorySortUI>();
                sortUI.Configure(idButton, categoryButton);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(clone, SortControlsPrefabPath);
                Object.DestroyImmediate(clone);
                return saved;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(warehouseRoot);
            }
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(root.GetChild(i), objectName);
                if (found != null) return found;
            }

            return null;
        }
    }
}
