using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Keeps Windows players on Direct3D 11. The resolution settings UI runs after
/// Unity has created the graphics device, so it cannot replace -force-d3d11 at runtime.
/// </summary>
[InitializeOnLoad]
internal sealed class GHWindowsGraphicsApiBuildGuard : IPreprocessBuildWithReport
{
    private static readonly GraphicsDeviceType[] Direct3D11Only =
    {
        GraphicsDeviceType.Direct3D11
    };

    static GHWindowsGraphicsApiBuildGuard()
    {
        EditorApplication.delayCall += EnsureAfterAssemblyReload;
    }

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        BuildTarget target = report.summary.platform;
        if (target == BuildTarget.StandaloneWindows ||
            target == BuildTarget.StandaloneWindows64)
        {
            EnsureDirect3D11PlayerSetting();
        }
    }

    [MenuItem("Tools/GH/Enforce Windows Direct3D 11")]
    private static void EnforceFromMenu()
    {
        bool changed = EnsureDirect3D11PlayerSetting();
        Debug.Log(changed
            ? "[GH Graphics] Windows Player is now fixed to Direct3D 11."
            : "[GH Graphics] Windows Player is already fixed to Direct3D 11.");
    }

    private static bool EnsureDirect3D11PlayerSetting()
    {
        const BuildTarget target = BuildTarget.StandaloneWindows64;
        GraphicsDeviceType[] currentApis = PlayerSettings.GetGraphicsAPIs(target);
        bool alreadyDirect3D11Only =
            !PlayerSettings.GetUseDefaultGraphicsAPIs(target) &&
            currentApis != null &&
            currentApis.Length == 1 &&
            currentApis[0] == GraphicsDeviceType.Direct3D11;

        if (alreadyDirect3D11Only)
        {
            return false;
        }

        PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
        PlayerSettings.SetGraphicsAPIs(target, Direct3D11Only);
        Debug.Log(
            "[GH Graphics] Windows builds were fixed to Direct3D 11 to prevent the " +
            "DX12 black-screen issue when changing to a lower resolution.");
        return true;
    }

    private static void EnsureAfterAssemblyReload()
    {
        EnsureDirect3D11PlayerSetting();
    }
}
