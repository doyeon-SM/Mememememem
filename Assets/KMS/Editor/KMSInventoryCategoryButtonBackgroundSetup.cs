using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KMS.Editor
{
    public static class KMSInventoryCategoryButtonBackgroundSetup
    {
        private const string SourceSpritePath =
            "Assets/HDY/3.Assets/Rounded Filled 1024px_pp1000.png";

        private const string KmsSpritePath =
            "Assets/KMS/1.Scripts/InventoryDuped/UI/Sprite/Rounded Filled 1024px_pp1000.png";

        private const string InventoryPrefabPath =
            "Assets/KMS/2.Prefabs/0714_InventoryCanvas_Root.prefab";

        private static readonly HashSet<string> CategoryLabels =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "C",
                "EQP",
                "MAT",
                "FOD"
            };

        [MenuItem("KMS/Inventory/Apply Category Button Background")]
        public static void Apply()
        {
            EnsureKmsSpriteCopy();

            Sprite roundedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(KmsSpritePath);
            if (roundedSprite == null)
            {
                throw new InvalidOperationException(
                    $"Sprite를 불러오지 못했습니다: {KmsSpritePath}");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(InventoryPrefabPath);
            try
            {
                var appliedLabels = new HashSet<string>(StringComparer.Ordinal);
                int appliedButtonCount = 0;
                TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);

                foreach (TMP_Text label in labels)
                {
                    string labelText = label.text.Trim();
                    if (!CategoryLabels.Contains(labelText))
                    {
                        continue;
                    }

                    Button button = label.GetComponentInParent<Button>(true);
                    if (button == null)
                    {
                        throw new InvalidOperationException(
                            $"'{labelText}' 라벨의 상위 Button을 찾지 못했습니다.");
                    }

                    Image background = button.image;
                    if (background == null)
                    {
                        background = button.targetGraphic as Image;
                    }

                    if (background == null)
                    {
                        throw new InvalidOperationException(
                            $"'{labelText}' 버튼의 배경 Image를 찾지 못했습니다.");
                    }

                    if (HasAncestorNamed(label.transform, "InventorySortControls"))
                    {
                        RevertAccidentalSortControlOverride(background);
                        continue;
                    }

                    background.sprite = roundedSprite;
                    background.type = Image.Type.Sliced;
                    appliedLabels.Add(labelText);
                    appliedButtonCount++;
                }

                if (!appliedLabels.SetEquals(CategoryLabels) || appliedButtonCount != CategoryLabels.Count)
                {
                    throw new InvalidOperationException(
                        $"카테고리 버튼 4개를 정확히 찾지 못했습니다. " +
                        $"적용 수: {appliedButtonCount}, 라벨: {string.Join(", ", appliedLabels)}");
                }

                PrefabUtility.SaveAsPrefabAsset(root, InventoryPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[KMS] C/EQP/MAT/FOD 버튼 배경을 '{KmsSpritePath}'로 교체했습니다.");
        }

        private static void EnsureKmsSpriteCopy()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(KmsSpritePath) != null)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(SourceSpritePath) == null)
            {
                throw new InvalidOperationException(
                    $"원본 Sprite를 찾지 못했습니다: {SourceSpritePath}");
            }

            if (!AssetDatabase.CopyAsset(SourceSpritePath, KmsSpritePath))
            {
                throw new InvalidOperationException(
                    $"Sprite를 KMS 폴더로 복사하지 못했습니다: {KmsSpritePath}");
            }

            AssetDatabase.ImportAsset(KmsSpritePath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static bool HasAncestorNamed(Transform transform, string objectName)
        {
            for (Transform current = transform; current != null; current = current.parent)
            {
                if (current.name == objectName)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RevertAccidentalSortControlOverride(Image background)
        {
            var serializedImage = new SerializedObject(background);
            SerializedProperty spriteProperty = serializedImage.FindProperty("m_Sprite");
            if (spriteProperty != null && spriteProperty.prefabOverride)
            {
                PrefabUtility.RevertPropertyOverride(
                    spriteProperty,
                    InteractionMode.AutomatedAction);
            }
        }
    }
}
