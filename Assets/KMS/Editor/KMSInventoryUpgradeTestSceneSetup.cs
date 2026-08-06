using System;
using HDY.Inventory;
using KMS.InventoryDuped;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KMS.EditorTools
{
    public static class KMSInventoryUpgradeTestSceneSetup
    {
        private const string ScenePath = "Assets/KMS/0.Scenes/TestScene_KMS.unity";
        private const string CostTablePath = "Assets/KMS/3.UI/Settings/KMSTestInventoryUpgradeCostTable.asset";
        private const string SupplyObjectName = "KMS Inventory Upgrade Test Supplies";

        [MenuItem("KMS/Setup/Add Inventory Upgrade Supplies To Test Scene")]
        public static void Run()
        {
            InventoryUpgradeCostTable costTable = BuildCostTable();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            InventoryUpgrade upgrade = UnityEngine.Object.FindFirstObjectByType<InventoryUpgrade>(FindObjectsInactive.Include);
            Require(upgrade != null, "InventoryUpgrade is missing from TestScene_KMS.");
            SerializedObject serializedUpgrade = new SerializedObject(upgrade);
            serializedUpgrade.FindProperty("costTable").objectReferenceValue = costTable;
            serializedUpgrade.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(upgrade);

            GameObject supplyObject = GameObject.Find(SupplyObjectName);
            if (supplyObject == null) supplyObject = new GameObject(SupplyObjectName);
            if (supplyObject.GetComponent<KMSInventoryUpgradeTestSupplies>() == null)
                supplyObject.AddComponent<KMSInventoryUpgradeTestSupplies>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[KMSInventoryUpgradeTestSceneSetup] TestScene_KMS now supplies 100 item_iron and ten upgrade steps.");
        }

        public static void RunFromCommandLine() => Run();

        public static void RunHudAndTestSetupFromCommandLine()
        {
            KMSLongTermPlayerHudMigration.Run();
            Run();
        }

        private static InventoryUpgradeCostTable BuildCostTable()
        {
            InventoryUpgradeCostTable table =
                AssetDatabase.LoadAssetAtPath<InventoryUpgradeCostTable>(CostTablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<InventoryUpgradeCostTable>();
                AssetDatabase.CreateAsset(table, CostTablePath);
            }

            table.Steps.Clear();
            for (int i = 0; i < 10; i++)
            {
                InventoryUpgradeCostTable.Step step = new InventoryUpgradeCostTable.Step
                {
                    GoldCost = 0
                };
                step.MaterialCosts.Add(new HDY.Recipe.Recipe_Requset_Item_Data
                {
                    Item_ID = "item_iron",
                    Amount = 10
                });
                table.Steps.Add(step);
            }

            EditorUtility.SetDirty(table);
            return table;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
