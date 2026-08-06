using System;
using System.Collections.Generic;
using HDY.Item;
using UnityEditor;
using UnityEngine;

namespace KMS.EditorTools
{
    public static class KMSFoodEffectSetup
    {
        private static readonly string[] PlayerPrefabPaths =
        {
            "Assets/KMS/2.Prefabs/0720_Player_KMS.prefab"
        };

        [MenuItem("KMS/Setup/Apply Food Effect Structure")]
        public static void Apply()
        {
            foreach (string prefabPath in PlayerPrefabPaths)
            {
                ConfigurePlayerPrefab(prefabPath);
            }

            ValidateLedgerModel();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KMS Food Effects] Player prefabs configured and ledger validation passed.");
        }

        private static void ConfigurePlayerPrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) return;

            try
            {
                PlayerStats stats = root.GetComponent<PlayerStats>();
                if (stats == null) return;

                KMSFoodEffectController foodEffects =
                    root.GetComponent<KMSFoodEffectController>();
                if (foodEffects == null)
                {
                    foodEffects = root.AddComponent<KMSFoodEffectController>();
                }

                SetReference(foodEffects, "stats", stats);
                SetReference(stats, "foodEffects", foodEffects);

                PlayerMovement movement = root.GetComponent<PlayerMovement>();
                if (movement != null) SetReference(movement, "foodEffects", foodEffects);

                PlayerHUD hud = root.GetComponent<PlayerHUD>();
                if (hud != null) SetReference(hud, "foodEffects", foodEffects);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name}.{propertyName} serialized field was not found.");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateLedgerModel()
        {
            var testObject = new GameObject("KMSFoodEffectValidation");
            testObject.AddComponent<PlayerStats>();
            var controller = testObject.GetComponent<KMSFoodEffectController>();
            if (controller == null) controller = testObject.AddComponent<KMSFoodEffectController>();

            ItemData sandwich = CreateFood(
                "item_sandwich",
                new ItemEffect { Effect = EffectType.Satiety, Value = 30f },
                new ItemEffect { Effect = EffectType.Speed, Value = 60f });
            ItemData pizza = CreateFood(
                "item_pizza",
                new ItemEffect { Effect = EffectType.Satiety, Value = 20f },
                new ItemEffect { Effect = EffectType.Fulling, Value = 20f });
            ItemData oatmeal = CreateFood(
                "item_oatmeal",
                new ItemEffect { Effect = EffectType.Satiety, Value = 20f });

            try
            {
                controller.InitializeAsNormal(20f, false);
                RequireApply(controller, pizza, 20f, 100f, 20f, out float currentHunger);
                RequireApproximately(currentHunger, 40f, "pizza resulting hunger");
                RequireApproximately(controller.NormalSatiety, 20f, "pizza keeps normal on right");
                RequireApproximately(
                    controller.EffectSegments[0].RemainingSatiety,
                    20f,
                    "pizza left segment");

                RequireApply(controller, oatmeal, 20f, 100f, currentHunger, out currentHunger);
                RequireApproximately(controller.NormalSatiety, 40f, "oatmeal merges on right");
                RequireApproximately(
                    controller.EffectSegments[0].RemainingSatiety,
                    20f,
                    "oatmeal must not resize pizza");

                RequireApply(controller, sandwich, 30f, 100f, currentHunger, out currentHunger);
                RequireApproximately(currentHunger, 90f, "sandwich resulting hunger");
                RequireSegment(controller, 0, "item_sandwich", 30f, "new sandwich left");
                RequireSegment(controller, 1, "item_pizza", 20f, "older pizza shifted right");
                RequireApproximately(controller.NormalSatiety, 40f, "normal remains rightmost");

                RequireApply(controller, pizza, 20f, 100f, currentHunger, out currentHunger);
                RequireApproximately(currentHunger, 100f, "full hunger after replacement");
                RequireSegment(controller, 0, "item_pizza", 20f, "latest pizza left");
                RequireSegment(controller, 1, "item_sandwich", 30f, "sandwich middle");
                RequireSegment(controller, 2, "item_pizza", 20f, "oldest pizza right");
                RequireApproximately(controller.NormalSatiety, 30f, "overflow trims normal first");
                RequireApproximately(controller.MoveSpeedMultiplier, 1.6f, "sandwich speed");
                RequireApproximately(
                    controller.GetActiveEffectTotal(EffectType.Fulling),
                    40f,
                    "fulling stacking");

                if (controller.CanApplyFood(oatmeal, 20f, 100f, currentHunger))
                    throw new InvalidOperationException("Normal food must be blocked at full hunger.");
                if (!controller.CanApplyFood(pizza, 20f, 100f, currentHunger))
                    throw new InvalidOperationException("Effect food must be allowed at full hunger.");

                controller.InitializeAsNormal(10f, false);
                RequireApply(controller, pizza, 20f, 100f, 10f, out currentHunger);
                RequireApply(controller, sandwich, 70f, 100f, currentHunger, out currentHunger);
                RequireApply(controller, pizza, 20f, 100f, currentHunger, out currentHunger);
                RequireSegment(controller, 0, "item_pizza", 20f, "new blue segment");
                RequireSegment(controller, 1, "item_sandwich", 70f, "orange middle segment");
                RequireSegment(controller, 2, "item_pizza", 10f, "old blue trimmed on right");
                RequireApproximately(controller.NormalSatiety, 0f, "normal trimmed before old effect");

                controller.ConsumeSatiety(15f);
                RequireSegment(controller, 0, "item_pizza", 20f, "newest effect survives");
                RequireApproximately(
                    controller.EffectSegments[1].RemainingSatiety,
                    65f,
                    "rightmost old effect drains first");

                var savedState = controller.CaptureSaveData();
                controller.InitializeAsNormal(0f, false);
                controller.RestoreSaveData(savedState, 85f);
                RequireSegment(controller, 0, "item_pizza", 20f, "restored newest segment");
                RequireSegment(controller, 1, "item_sandwich", 65f, "restored older segment");
                RequireApproximately(controller.NormalSatiety, 0f, "restored normal satiety");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sandwich);
                UnityEngine.Object.DestroyImmediate(pizza);
                UnityEngine.Object.DestroyImmediate(oatmeal);
                UnityEngine.Object.DestroyImmediate(testObject);
            }
        }

        private static void RequireApply(
            KMSFoodEffectController controller,
            ItemData item,
            float satiety,
            float maxHunger,
            float currentHunger,
            out float resultingHunger)
        {
            if (controller.ApplyFood(
                    item,
                    satiety,
                    maxHunger,
                    currentHunger,
                    out resultingHunger))
            {
                return;
            }

            throw new InvalidOperationException($"Failed to apply test food: {item.Item_ID}");
        }

        private static void RequireSegment(
            KMSFoodEffectController controller,
            int index,
            string itemId,
            float satiety,
            string label)
        {
            if (index < 0 || index >= controller.EffectSegments.Count)
                throw new InvalidOperationException($"{label}: segment index {index} is missing.");

            KMSFoodEffectSegment segment = controller.EffectSegments[index];
            if (segment.ItemId != itemId)
                throw new InvalidOperationException(
                    $"{label}: expected item {itemId}, actual {segment.ItemId}");

            RequireApproximately(segment.RemainingSatiety, satiety, label);
        }

        private static ItemData CreateFood(string itemId, params ItemEffect[] effects)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.Item_ID = itemId;
            item.Category = ItemCategory.Food;
            item.UseAction = UseAction.Eat;
            item.EatEffects = new List<ItemEffect>(effects);
            return item;
        }

        private static void RequireApproximately(float actual, float expected, string label)
        {
            if (Mathf.Abs(actual - expected) <= 0.001f) return;
            throw new InvalidOperationException(
                $"{label}: expected {expected}, actual {actual}");
        }
    }
}
