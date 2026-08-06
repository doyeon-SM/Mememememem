using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PotaToon.Editor
{
    class PotaToonShaderStripper : IPreprocessShaders, IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private static readonly HashSet<string> k_TargetShaders = new HashSet<string>
        {
            "PotaToon/Toon",
            "PotaToon/SimpleToon",
            "PotaToon/Eye",
        };

        private static readonly HashSet<string> k_DitherFadePasses = new HashSet<string>
        {
            "OpaqueDitherFade",
            "OpaqueDitherFadeOutline",
        };

        private static bool? s_CachedDitherFadePassesEnabled;
        private static int s_StrippedVariantCount;
        private static int s_TotalVariantCount;

        public void OnPreprocessBuild(BuildReport report)
        {
            s_CachedDitherFadePassesEnabled = null;
            s_StrippedVariantCount = 0;
            s_TotalVariantCount = 0;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!AreDitherFadePassesEnabled())
                Debug.Log($"[PotaToon] Dither Fade Passes disabled. Stripped {s_StrippedVariantCount} / {s_TotalVariantCount} OpaqueDitherFade* variants from this build.");
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader == null || data == null || data.Count == 0) return;
            if (!k_TargetShaders.Contains(shader.name)) return;
            if (!k_DitherFadePasses.Contains(snippet.passName)) return;

            int before = data.Count;
            s_TotalVariantCount += before;

            if (AreDitherFadePassesEnabled())
                return;

            data.Clear();
            s_StrippedVariantCount += before;
        }

        private static bool AreDitherFadePassesEnabled()
        {
            if (s_CachedDitherFadePassesEnabled.HasValue) return s_CachedDitherFadePassesEnabled.Value;

            bool enabled = PotaToonProjectSettings.EnableDitherFadePasses;
            s_CachedDitherFadePassesEnabled = enabled;
            return enabled;
        }

        [InitializeOnLoadMethod]
        private static void ResetCacheOnReload()
        {
            s_CachedDitherFadePassesEnabled = null;
        }
    }

    class PotaToonDitherFadeBuildValidator : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (PotaToonProjectSettings.EnableDitherFadePasses || !scene.IsValid())
                return;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var character in root.GetComponentsInChildren<PotaToonCharacter>(true))
                {
                    if (character == null || !character.useDitherFade)
                        continue;

                    string sceneName = string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
                    string hierarchyPath = GetHierarchyPath(character.transform);
                    throw new BuildFailedException(
                        $"[PotaToon] Dither Fade Passes is disabled in Project Settings > PotaToon, but '{hierarchyPath}' in scene '{sceneName}' has PotaToonCharacter.useDitherFade enabled. Enable Project Settings > PotaToon > Dither Fade Passes or disable useDitherFade before building.");
                }
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
