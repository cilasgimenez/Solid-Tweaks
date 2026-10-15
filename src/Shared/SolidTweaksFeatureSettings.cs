using System;
using Microsoft.Win32;

namespace SolidTweaks.Shared
{
    internal static class SolidTweaksFeatureSettings
    {
        public static string RegistryPath { get { return SolidWorksTarget.FeaturesPath; } }
        public const string ExportStepFeatureId = "resources.export-step";
        public const string ExportDxfFeatureId = "resources.export-sheet-metal-dxf";
        public const string DxfBendLinesPreferenceId = "export-sheet-metal-dxf.bend-lines";

        public static bool Read(string valueName, bool defaultValue)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                object value = key == null ? null : key.GetValue(valueName);
                if (value == null)
                {
                    return defaultValue;
                }
                try
                {
                    return Convert.ToInt32(value) != 0;
                }
                catch
                {
                    return string.Equals(value.ToString(), "1", StringComparison.Ordinal);
                }
            }
        }

        public static bool HasValue(string valueName)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                return key != null && key.GetValue(valueName) != null;
            }
        }

        public static void Write(string valueName, bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Could not persist the feature state.");
                }
                key.SetValue(valueName, enabled ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        public static bool HasAnyCommandFeatureEnabled()
        {
            return Read(ExportStepFeatureId, false) || Read(ExportDxfFeatureId, false);
        }

        public static bool HasAnyAddinFeatureEnabled()
        {
            return HasAnyCommandFeatureEnabled();
        }
    }
}
