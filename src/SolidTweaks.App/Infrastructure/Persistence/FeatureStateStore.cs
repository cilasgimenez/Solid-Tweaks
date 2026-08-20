using Microsoft.Win32;

namespace SolidTweaks.Infrastructure.Persistence
{
    internal sealed class FeatureStateStore
    {
        private const string RegistryPath = @"Software\SolidTweaks\Features";

        public bool Read(string featureId, bool defaultValue)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                object value = key == null ? null : key.GetValue(featureId);
                return value == null ? defaultValue : value.ToString() == "1";
            }
        }

        public void Write(string featureId, bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key == null)
                {
                    throw new System.InvalidOperationException("Could not persist the feature state.");
                }

                key.SetValue(featureId, enabled ? 1 : 0, RegistryValueKind.DWord);
            }
        }
    }
}
