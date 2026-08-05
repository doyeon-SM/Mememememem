using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace PotaToon.Editor
{
    internal static class PotaToonShaderGUISearchHelper
    {
        public static string searchQuery = "";

        private static Dictionary<string, bool> visibleGroups = new Dictionary<string, bool>{ {"Main Settings", true} };
        private static Dictionary<string, bool> searchKeywordMatchingGroups = new Dictionary<string, bool>();

        private static List<string> advancedSettingsLabel = new List<string>(){
            "Character Shadow",
            "Face SDF",
            "High Light",
            "Emission",
            "Glitter",
            "Hair High Light",
            "OIT",
            "Stencil / ZTest",
            "Pass Control",
        };

        private static GUIStyle headerStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            fixedHeight = 30,
            normal = { textColor = Color.white }
        };

        private static GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 5, 5),
            margin = new RectOffset(5, 5, 5, 5)
        };

        private static GUIStyle borderStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(1, 1, 1, 1),
            margin = new RectOffset(5, 5, 5, 5),
            normal = { background = Texture2D.grayTexture }
        };

        /// <summary>
        /// Checks if the property of the given label matches the search condition.
        /// </summary>
        /// <param name="label">Label of the property</param>
        /// <returns>Returns true if it matches the search condition, otherwise false</returns>
        public static bool IsSearchMatched(string label)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                return true;
            }

            if (label.StartsWith("$_"))
            {
                return false;
            }

            return IsSearchExactMatched(label);
        }

        public static bool IsSearchExactMatched(string label)
        {
            return label.Replace(" ", "").Contains(searchQuery.Replace(" ", ""), System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAdvancedSettingsMatched()
        {
            return string.IsNullOrWhiteSpace(searchQuery) || searchKeywordMatchingGroups.Keys.Any(key => advancedSettingsLabel.Contains(key));
        }

        /// <summary>
        /// Renders the property of the given label. If it does not match the search condition, it will not be rendered.
        /// </summary>
        /// <param name="label">Label of the property</param>
        /// <param name="props">Action to render the property</param>
        public static void Property(string label, System.Action<string> props)
        {
            if (!IsSearchMatched(label))
            {
                return;
            }

            props(label);
        }

        private static void PropertyGroupBase(string groupLabel, System.Action<bool, System.Action<string, System.Action<string>>> props)
        {
            // Initialize the existing group search conditions.
            searchKeywordMatchingGroups.Clear();

            var groupItems = new List<(string, System.Action<string>)>();

            // First, execute the action passed to Props to collect all properties in the group.
            props(false, (propertyLabel, propertyAction) => groupItems.Add((propertyLabel, propertyAction)));

            // Check if there is a matching searchQuery within the collected properties.
            foreach (var (propertyLabel, propertyAction) in groupItems)
            {
                if (!IsSearchMatched(groupLabel + propertyLabel)) continue;
                searchKeywordMatchingGroups[groupLabel] = true;
            }

            // If it matches the search result, render the group.
            if (searchKeywordMatchingGroups.ContainsKey(groupLabel))
            {
                props(true, (propertyLabel, propertyAction) =>
                {
                    if (!IsSearchMatched(groupLabel + propertyLabel)) return;
                    propertyAction(propertyLabel);
                });
            }
        }

        public static void PropertyGroupBox(string label, System.Action<System.Action<string, System.Action<string>>, bool> props)
        {
            PropertyGroupBase(label, (runRender, wrappedPropertyAction) =>
            {
                if (!runRender) { props(wrappedPropertyAction, runRender); return; }

                var alwaysExtend = !string.IsNullOrEmpty(searchQuery) && searchKeywordMatchingGroups.ContainsKey(label);

                if (!visibleGroups.ContainsKey(label))
                {
                    visibleGroups[label] = false;
                }

                if (alwaysExtend)
                {
                    GUILayout.Button(label, headerStyle);
                }
                else
                {
                    if (GUILayout.Button((visibleGroups[label] ? "▼ " : "► ") + label, headerStyle))
                    {
                        visibleGroups[label] = !visibleGroups[label];
                    }
                }

                if (visibleGroups[label] || alwaysExtend)
                {
                    EditorGUILayout.BeginVertical(borderStyle);
                    EditorGUILayout.BeginVertical(boxStyle);
                    props(wrappedPropertyAction, runRender);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndVertical();
                }
            });
        }


        public static void PropertyGroupBox(string label, System.Action<System.Action<string, System.Action<string>>> props)
        {
            PropertyGroupBase(label, (runRender, wrappedPropertyAction) =>
            {
                if (!runRender) { props(wrappedPropertyAction); return; }

                var alwaysExtend = !string.IsNullOrEmpty(searchQuery) && searchKeywordMatchingGroups.ContainsKey(label);

                if (!visibleGroups.ContainsKey(label))
                {
                    visibleGroups[label] = false;
                }

                if (alwaysExtend)
                {
                    GUILayout.Button(label, headerStyle);
                }
                else
                {
                    if (GUILayout.Button((visibleGroups[label] ? "▼ " : "► ") + label, headerStyle))
                    {
                        visibleGroups[label] = !visibleGroups[label];
                    }
                }

                if (visibleGroups[label] || alwaysExtend)
                {
                    EditorGUILayout.BeginVertical(borderStyle);
                    EditorGUILayout.BeginVertical(boxStyle);
                    props(wrappedPropertyAction);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndVertical();
                }
            });
        }

        public static void PropertyGroup(string label, System.Action<System.Action<string, System.Action<string>>, bool> props)
        {
            PropertyGroupBase(label, (runRender, wrappedPropertyAction) =>
            {
                if (!runRender) { props(wrappedPropertyAction, runRender); return; }

                EditorGUILayout.BeginVertical(borderStyle);
                EditorGUILayout.BeginVertical(boxStyle);
                props(wrappedPropertyAction, runRender);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();
            });
        }

        public static void CustomFoldout(ref bool foldout, string label, System.Action action)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(boxStyle);

            foldout = EditorGUILayout.Foldout(foldout, label);
            if (foldout || (!string.IsNullOrEmpty(searchQuery) && IsSearchExactMatched(label)))
            {
                action();
            }

            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }
    }
}
