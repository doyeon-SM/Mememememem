#if UNITY_EDITOR
using Michsky.MUIP;
using UnityEditor;
using UnityEngine;

namespace GH.Loading.Editor
{
    [InitializeOnLoad]
    internal static class LoadingPanelProgressBarReferenceUpgrader
    {
        private const string LoadingPanelPrefabPath =
            "Assets/_GH/05.Prefeb/UI/UI_Loading_Panel.prefab";

        static LoadingPanelProgressBarReferenceUpgrader()
        {
            EditorApplication.delayCall += UpgradeIfNeeded;
        }

        [MenuItem("Tools/GH/Repair Loading Panel ProgressBar Reference")]
        private static void UpgradeIfNeeded()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(LoadingPanelPrefabPath);
            if (root == null)
            {
                return;
            }

            try
            {
                LoadingPanelView view = root.GetComponent<LoadingPanelView>();
                ProgressBar progressBar = root.GetComponentInChildren<ProgressBar>(true);
                if (view == null || progressBar == null)
                {
                    Debug.LogError(
                        "[LoadingPanel] LoadingPanelView 또는 ProgressBar를 찾지 못했습니다.",
                        root);
                    return;
                }

                SerializedObject serializedView = new SerializedObject(view);
                SerializedProperty progressBarProperty =
                    serializedView.FindProperty("progressBar");
                bool changed = progressBarProperty.objectReferenceValue != progressBar;

                if (changed)
                {
                    progressBarProperty.objectReferenceValue = progressBar;
                    serializedView.ApplyModifiedPropertiesWithoutUndo();
                }

                changed |= progressBar.isOn;
                changed |= progressBar.restart;
                changed |= progressBar.invert;
                changed |= !Mathf.Approximately(progressBar.minValue, 0f);
                changed |= !Mathf.Approximately(progressBar.maxValue, 100f);
                changed |= !Mathf.Approximately(progressBar.currentPercent, 0f);

                progressBar.isOn = false;
                progressBar.restart = false;
                progressBar.invert = false;
                progressBar.minValue = 0f;
                progressBar.maxValue = 100f;
                progressBar.currentPercent = 0f;

                if (changed)
                {
                    EditorUtility.SetDirty(progressBar);
                    PrefabUtility.SaveAsPrefabAsset(root, LoadingPanelPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
