using KMS;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KMS.Editor
{
    public static class KMSCelestialClockSetup
    {
        private static readonly string[] PrefabPaths =
        {
            "Assets/KMS/2.Prefabs/PlayerHUDLayer.prefab",
            "Assets/KMS/2.Prefabs/PlayerCanvas_Root.prefab"
        };

        [MenuItem("Tools/KMS/Apply Celestial Clock")]
        public static void Apply()
        {
            foreach (string prefabPath in PrefabPaths)
                ApplyToPrefab(prefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KMS] Celestial clock setup applied.");
        }

        public static void ApplyFromCommandLine()
        {
            Apply();
        }

        private static void ApplyToPrefab(string prefabPath)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                KMSExplorationClockView view =
                    prefabRoot.GetComponentInChildren<KMSExplorationClockView>(true);
                if (view == null)
                {
                    Debug.LogWarning($"[KMS] No exploration clock found in {prefabPath}");
                    return;
                }

                SerializedObject viewData = new SerializedObject(view);
                GameObject sun = viewData.FindProperty("sunIcon").objectReferenceValue as GameObject;
                GameObject moon = viewData.FindProperty("moonIcon").objectReferenceValue as GameObject;
                CanvasGroup gameTimeGroup =
                    viewData.FindProperty("gameTimeGroup").objectReferenceValue as CanvasGroup;
                TMPro.TMP_Text realTimeText =
                    viewData.FindProperty("realTimeText").objectReferenceValue as TMPro.TMP_Text;
                RectTransform legacyFill =
                    viewData.FindProperty("phaseFill").objectReferenceValue as RectTransform;
                RectTransform assignedOrbit =
                    viewData.FindProperty("celestialOrbit").objectReferenceValue as RectTransform;
                RectTransform orbit = assignedOrbit != null ? assignedOrbit : legacyFill;

                if (sun == null || moon == null || orbit == null)
                {
                    Debug.LogError($"[KMS] Clock references are incomplete in {prefabPath}");
                    return;
                }

                RectTransform viewport = assignedOrbit != null
                    ? orbit.parent as RectTransform
                    : sun.transform.parent as RectTransform;
                if (viewport == null)
                {
                    Debug.LogError($"[KMS] Clock viewport is missing in {prefabPath}");
                    return;
                }

                SetupViewport(viewport);
                SetupOrbit(orbit, sun, moon);
                SetupTimeLabels(realTimeText, gameTimeGroup);

                viewData.Update();
                viewData.FindProperty("phaseFill").objectReferenceValue = null;
                viewData.FindProperty("celestialOrbit").objectReferenceValue = orbit;
                viewData.FindProperty("orbitRadius").floatValue = 32f;
                viewData.FindProperty("dayStartAngle").floatValue = 25f;
                viewData.FindProperty("clockwise").boolValue = false;
                viewData.FindProperty("keepIconsUpright").boolValue = true;
                viewData.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                Debug.Log($"[KMS] Updated {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void SetupViewport(RectTransform viewport)
        {
            viewport.gameObject.name = "CelestialViewport";
            viewport.anchorMin = new Vector2(0f, 0.5f);
            viewport.anchorMax = new Vector2(0f, 0.5f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.anchoredPosition = new Vector2(45f, 0f);
            viewport.sizeDelta = new Vector2(96f, 50f);

            Image oldBackground = viewport.GetComponent<Image>();
            if (oldBackground != null) oldBackground.enabled = false;

            RectMask2D mask = viewport.GetComponent<RectMask2D>();
            if (mask == null) mask = viewport.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;
            mask.softness = new Vector2Int(0, 2);
        }

        private static void SetupOrbit(RectTransform orbit, GameObject sun, GameObject moon)
        {
            orbit.gameObject.name = "CelestialOrbit";
            orbit.anchorMin = new Vector2(0.5f, 0.5f);
            orbit.anchorMax = new Vector2(0.5f, 0.5f);
            orbit.pivot = new Vector2(0.5f, 0.5f);
            orbit.anchoredPosition = new Vector2(0f, -25.5f);
            orbit.sizeDelta = new Vector2(86f, 86f);
            orbit.localRotation = Quaternion.identity;
            orbit.localScale = Vector3.one;

            Image oldFillImage = orbit.GetComponent<Image>();
            if (oldFillImage != null) Object.DestroyImmediate(oldFillImage, true);

            KMSCelestialOrbitGraphic halo = orbit.GetComponent<KMSCelestialOrbitGraphic>();
            if (halo == null) halo = orbit.gameObject.AddComponent<KMSCelestialOrbitGraphic>();
            halo.raycastTarget = false;
            SerializedObject haloData = new SerializedObject(halo);
            haloData.FindProperty("radius").floatValue = 32f;
            haloData.FindProperty("segments").intValue = 96;
            haloData.FindProperty("glowWidth").floatValue = 5.5f;
            haloData.FindProperty("dayColor").colorValue =
                new Color(0.34f, 0.78f, 1f, 0.42f);
            haloData.FindProperty("nightColor").colorValue =
                new Color(0.12f, 0.2f, 0.52f, 0.46f);
            haloData.FindProperty("dayCenterAngle").floatValue = 25f;
            haloData.ApplyModifiedPropertiesWithoutUndo();

            SetupIcon(sun, orbit);
            SetupIcon(moon, orbit);
            PlaceIcon(sun, 25f, 32f);
            PlaceIcon(moon, 205f, 32f);
            orbit.SetAsFirstSibling();
        }

        private static void SetupIcon(GameObject icon, RectTransform orbit)
        {
            RectTransform iconRect = icon.transform as RectTransform;
            iconRect.SetParent(orbit, false);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(26f, 26f);
            iconRect.localScale = Vector3.one;
            iconRect.localRotation = Quaternion.identity;

            Image image = icon.GetComponent<Image>();
            if (image != null)
            {
                image.enabled = true;
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }

            icon.SetActive(true);
        }

        private static void SetupTimeLabels(TMPro.TMP_Text realTimeText, CanvasGroup group)
        {
            if (realTimeText != null && realTimeText.transform is RectTransform realTimeRect)
            {
                // Keep the real-world time between the orbit and the hover-only
                // game time so the two labels never share the same screen area.
                realTimeRect.anchorMin = new Vector2(0f, 0.5f);
                realTimeRect.anchorMax = new Vector2(0f, 0.5f);
                realTimeRect.pivot = new Vector2(0f, 0.5f);
                realTimeRect.anchoredPosition = new Vector2(92f, 0f);
                realTimeRect.sizeDelta = new Vector2(150f, 50f);
            }

            if (group == null || !(group.transform is RectTransform groupRect)) return;

            // The game time occupies only the extra width revealed on hover.
            groupRect.anchorMin = new Vector2(0f, 0.5f);
            groupRect.anchorMax = new Vector2(0f, 0.5f);
            groupRect.pivot = new Vector2(0f, 0.5f);
            groupRect.anchoredPosition = new Vector2(245f, 0f);
            groupRect.sizeDelta = new Vector2(105f, 50f);
        }

        private static void PlaceIcon(GameObject icon, float angleDegrees, float radius)
        {
            RectTransform iconRect = icon.transform as RectTransform;
            float radians = angleDegrees * Mathf.Deg2Rad;
            iconRect.anchoredPosition =
                new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
        }
    }
}
