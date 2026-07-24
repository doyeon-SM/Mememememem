#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KMS.Audio.Editor
{
    public static class KMSAudioSetupTool
    {
        private const string Root = "Assets/KMS/Audio";
        private const string ClipRoot = Root + "/Clips";
        private const string SfxRoot = ClipRoot + "/SFX";
        private const string BgmRoot = ClipRoot + "/BGM";
        private const string ResourceRoot = Root + "/Resources";
        private const string CatalogPath = ResourceRoot + "/KMSAudioCatalog.asset";

        [MenuItem("KMS/Audio/Setup And Refresh Catalog")]
        public static void Run()
        {
            EnsureFolder(Root);
            EnsureFolder(ClipRoot);
            EnsureFolder(SfxRoot);
            EnsureFolder(BgmRoot);
            EnsureFolder(BgmRoot + "/Territory");
            EnsureFolder(BgmRoot + "/Exploration");
            EnsureFolder(ResourceRoot);
            EnsureFolder(Root + "/Mixers");
            EnsureFolder(Root + "/Prefabs");

            foreach (GameSfxId id in Enum.GetValues(typeof(GameSfxId)))
            {
                EnsureFolder($"{SfxRoot}/{id}");
            }

            KMSAudioCatalog catalog =
                AssetDatabase.LoadAssetAtPath<KMSAudioCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<KMSAudioCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.EnsureDefaults();
            foreach (GameSfxId id in Enum.GetValues(typeof(GameSfxId)))
            {
                string folder = $"{SfxRoot}/{id}";
                AudioClip[] clips = FindAudioClips(folder);
                catalog.SetCueClips(id, clips);
            }

            AudioClip territoryMusic = FindAudioClips(BgmRoot + "/Territory").FirstOrDefault();
            AudioClip explorationMusic = FindAudioClips(BgmRoot + "/Exploration").FirstOrDefault();
            catalog.SetSceneMusicClip("Territory", territoryMusic);
            catalog.SetSceneMusicClip("Main_World", explorationMusic);
            catalog.SetSceneMusicClip("Main_World_2", explorationMusic);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[KMS Audio] 폴더와 카탈로그를 갱신했습니다. " +
                "각 Clips 하위 폴더에 오디오 파일을 넣으면 자동으로 연결됩니다.");
        }

        private static AudioClip[] FindAudioClips(string folder)
        {
            return AssetDatabase.FindAssets("t:AudioClip", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .Where(clip => clip != null)
                .ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string normalized = path.Replace('\\', '/');
            string parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            string name = Path.GetFileName(normalized);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }

    public sealed class KMSAudioClipPostprocessor : AssetPostprocessor
    {
        private static bool refreshQueued;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (refreshQueued
                || !ContainsChangedAudio(
                    importedAssets,
                    deletedAssets,
                    movedAssets,
                    movedFromAssetPaths))
            {
                return;
            }

            refreshQueued = true;
            EditorApplication.delayCall += Refresh;
        }

        private static bool ContainsChangedAudio(params string[][] pathGroups)
        {
            return pathGroups.Any(paths =>
                paths != null
                && paths.Any(path =>
                    path.StartsWith("Assets/KMS/Audio/Clips/", StringComparison.Ordinal)
                    && IsAudioFile(path)));
        }

        private static bool IsAudioFile(string path)
        {
            string extension = Path.GetExtension(path);
            return extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".aif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".aiff", StringComparison.OrdinalIgnoreCase);
        }

        private static void Refresh()
        {
            refreshQueued = false;
            KMSAudioSetupTool.Run();
        }
    }
}
#endif
