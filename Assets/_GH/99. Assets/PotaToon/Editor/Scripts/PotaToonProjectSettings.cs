using UnityEditor;
using UnityEngine;

namespace PotaToon.Editor
{
    [FilePath("ProjectSettings/PotaToonSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class PotaToonProjectSettings : ScriptableSingleton<PotaToonProjectSettings>
    {
        [SerializeField] private bool m_EnableDitherFadePasses;

        internal static bool EnableDitherFadePasses => instance.m_EnableDitherFadePasses;

        internal static void SetDitherFadePassesEnabled(bool enabled)
        {
            if (instance.m_EnableDitherFadePasses == enabled)
                return;

            Undo.RecordObject(instance, "Change PotaToon Settings");
            instance.m_EnableDitherFadePasses = enabled;
            SaveSettings();
        }

        internal static void SaveSettings()
        {
            instance.Save(true);
        }
    }
}
