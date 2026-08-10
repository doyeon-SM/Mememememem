using System;
using System.Collections.Generic;
using HDY.Item;
using KMS.Persistence;
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

        /// <summary>
        /// [HDY 요청 - KMS 승인 - 음식 큐 통합] 효과 없는(포만감만) 음식과 효과 있는 음식이 하나의 큐로
        /// 통합된 뒤에도 실제 취식 순서(선입선출)가 정확히 지켜지는지 검증한다. 특히 "효과 음식을 먹고
        /// 포만감만 있는 음식을 먹었을 때 효과 음식이 밀리지 않고 포만감만 채워지는" 버그가 재발하지
        /// 않는지(1번 케이스) 명시적으로 확인한다.
        /// </summary>
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
                // 1) [버그 재현 케이스] 효과 음식(pizza)을 먹은 뒤 효과 없는 음식(oatmeal)을 먹어도,
                //    pizza가 큐에서 밀려나거나 조기 소비되지 않고 제자리(선입 순서)를 지켜야 한다.
                controller.InitializeAsNormal(0f, false);
                RequireApply(controller, pizza, 20f, 100f, 0f, out float currentHunger);
                RequireApproximately(currentHunger, 20f, "pizza resulting hunger");
                RequireSegment(controller, 0, "item_pizza", 20f, "pizza newest");

                RequireApply(controller, oatmeal, 20f, 100f, currentHunger, out currentHunger);
                RequireApproximately(currentHunger, 40f, "oatmeal resulting hunger");
                RequireSegment(controller, 0, "item_oatmeal", 20f, "oatmeal became newest (front of queue)");
                RequireSegment(controller, 1, "item_pizza", 20f, "pizza pushed back but preserved, not consumed early");
                RequireApproximately(
                    controller.GetActiveEffectTotal(EffectType.Fulling),
                    20f,
                    "pizza effect must still be active after eating plain food");

                // 2) 세 번째 음식(sandwich, 효과 있음)을 먹으면 큐 맨 앞에 추가되고 순서는
                //    [sandwich(신규), oatmeal, pizza(가장 오래됨)] 순서를 유지해야 한다.
                RequireApply(controller, sandwich, 30f, 100f, currentHunger, out currentHunger);
                RequireApproximately(currentHunger, 70f, "sandwich resulting hunger");
                RequireSegment(controller, 0, "item_sandwich", 30f, "sandwich newest");
                RequireSegment(controller, 1, "item_oatmeal", 20f, "oatmeal middle");
                RequireSegment(controller, 2, "item_pizza", 20f, "pizza oldest, still last");
                RequireApproximately(controller.MoveSpeedMultiplier, 1.6f, "sandwich speed active");
                RequireApproximately(controller.GetActiveEffectTotal(EffectType.Fulling), 20f, "pizza effect still active");

                // 3) 자연 소비(가장 오래된 것부터)는 실제 취식 순서를 그대로 따라야 한다: pizza -> oatmeal -> sandwich.
                controller.ConsumeSatiety(15f);
                RequireSegment(controller, 0, "item_sandwich", 30f, "sandwich untouched (newest)");
                RequireSegment(controller, 1, "item_oatmeal", 20f, "oatmeal untouched");
                RequireSegment(controller, 2, "item_pizza", 5f, "pizza (oldest) drains first, in real eat order");

                controller.ConsumeSatiety(10f);
                RequireSegment(controller, 0, "item_sandwich", 30f, "sandwich still untouched");
                RequireSegment(controller, 1, "item_oatmeal", 15f, "oatmeal starts draining only after pizza is fully gone");
                if (controller.FoodSegments.Count != 2)
                {
                    throw new InvalidOperationException("pizza should be fully drained and removed from the queue.");
                }
                RequireApproximately(controller.GetActiveEffectTotal(EffectType.Fulling), 0f, "pizza effect gone once fully consumed");

                // 4) 효과 음식은 배고픔이 가득 차 있어도 항상 전체 포만감으로 삽입되고, 자리가 없으면
                //    가장 오래된 세그먼트부터 밀어낸다. 효과 없는 음식은 가득 찬 상태에서 아예 막힌다.
                controller.InitializeAsNormal(100f, false);
                if (controller.CanApplyFood(oatmeal, 20f, 100f, 100f))
                {
                    throw new InvalidOperationException("Plain food must be blocked at full hunger.");
                }
                if (!controller.CanApplyFood(pizza, 20f, 100f, 100f))
                {
                    throw new InvalidOperationException("Effect food must be allowed even at full hunger.");
                }

                RequireApply(controller, pizza, 20f, 100f, 100f, out currentHunger);
                RequireApproximately(currentHunger, 100f, "hunger stays capped at max");
                RequireSegment(controller, 0, "item_pizza", 20f, "new pizza inserted at front even while full");
                RequireApproximately(SumTrackedSatiety(controller), 100f, "old satiety trimmed from the tail to make room");

                // 5) 세이브/로드 라운드트립: 큐 순서와 효과가 그대로 보존되어야 한다.
                controller.InitializeAsNormal(0f, false);
                RequireApply(controller, pizza, 20f, 100f, 0f, out currentHunger);
                RequireApply(controller, oatmeal, 20f, 100f, currentHunger, out currentHunger);
                RequireApply(controller, sandwich, 30f, 100f, currentHunger, out currentHunger);

                var savedState = controller.CaptureSaveData();
                controller.InitializeAsNormal(0f, false);
                controller.RestoreSaveData(savedState, currentHunger);
                RequireSegment(controller, 0, "item_sandwich", 30f, "restored sandwich newest");
                RequireSegment(controller, 1, "item_oatmeal", 20f, "restored oatmeal middle");
                RequireSegment(controller, 2, "item_pizza", 20f, "restored pizza oldest");

                // 6) 구버전(레이어드 normalSatiety) 세이브 마이그레이션: normalSatiety는 가장 오래된
                //    자리(맨 뒤)로 들어가야 옛 소비 우선순위(항상 먼저 소비됨)와 동일하게 유지된다.
                var legacyData = new KMSFoodEffectStateSaveData
                {
                    layoutVersion = 2,
                    normalSatiety = 15f,
                    segments = new[]
                    {
                        new KMSFoodEffectSegmentSaveData
                        {
                            itemId = "item_pizza",
                            remainingSatiety = 20f,
                            effects = new[]
                            {
                                new KMSFoodEffectValueSaveData { effectType = (int)EffectType.Fulling, value = 20f }
                            }
                        }
                    }
                };
                controller.InitializeAsNormal(0f, false);
                controller.RestoreSaveData(legacyData, 35f);
                RequireSegment(controller, 0, "item_pizza", 20f, "legacy effect segment kept newest");
                if (controller.FoodSegments.Count != 2)
                {
                    throw new InvalidOperationException("legacy normalSatiety must migrate into exactly one extra segment.");
                }
                RequireApproximately(controller.FoodSegments[1].RemainingSatiety, 15f, "legacy normalSatiety migrated as oldest segment");
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
            if (index < 0 || index >= controller.FoodSegments.Count)
                throw new InvalidOperationException($"{label}: segment index {index} is missing.");

            KMSFoodEffectSegment segment = controller.FoodSegments[index];
            if (segment.ItemId != itemId)
                throw new InvalidOperationException(
                    $"{label}: expected item {itemId}, actual {segment.ItemId}");

            RequireApproximately(segment.RemainingSatiety, satiety, label);
        }

        private static float SumTrackedSatiety(KMSFoodEffectController controller)
        {
            float total = 0f;
            foreach (KMSFoodEffectSegment segment in controller.FoodSegments)
            {
                if (segment != null) total += segment.RemainingSatiety;
            }

            return total;
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
