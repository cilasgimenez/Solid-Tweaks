using System;
using Microsoft.Win32;

namespace SolidTweaks.Shared
{
    internal enum ShortcutAction
    {
        MakeFixed,
        ConstructionGeometry,
        Measure,
        OffsetEntities
    }

    internal sealed class ShortcutBinding
    {
        private ShortcutBinding() { }

        public int VirtualKey { get; private set; }
        public int ScanCode { get; private set; }
        public bool IsExtended { get; private set; }

        public static ShortcutBinding Keyboard(int virtualKey, int scanCode, bool isExtended)
        {
            return new ShortcutBinding
            {
                VirtualKey = virtualKey,
                ScanCode = scanCode,
                IsExtended = isExtended
            };
        }

        public string Serialize()
        {
            return string.Format("keyboard:{0}:{1}:{2}", VirtualKey, ScanCode, IsExtended ? 1 : 0);
        }

        public static bool TryParse(string value, out ShortcutBinding binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] parts = value.Split(':');
            int virtualKey;
            int scanCode;
            int extended;
            if (parts.Length == 4 &&
                string.Equals(parts[0], "keyboard", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(parts[1], out virtualKey) &&
                int.TryParse(parts[2], out scanCode) &&
                int.TryParse(parts[3], out extended) &&
                virtualKey > 0 && virtualKey <= 255 && scanCode >= 0)
            {
                binding = Keyboard(virtualKey, scanCode, extended != 0);
                return true;
            }
            return false;
        }

        public bool SameInput(ShortcutBinding other)
        {
            return other != null && VirtualKey == other.VirtualKey;
        }
    }

    internal static class ShortcutBindingStore
    {
        public const string MigrationMarker = "shortcut-bindings.v2-initialized";

        public static ShortcutBinding Read(ShortcutAction action)
        {
            object raw;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SolidTweaksFeatureSettings.RegistryPath, false))
            {
                raw = key == null ? null : key.GetValue(ValueName(action));
            }

            ShortcutBinding binding;
            if (raw != null && ShortcutBinding.TryParse(raw.ToString(), out binding))
            {
                return binding;
            }

            if (raw != null && raw.ToString().StartsWith("mouse:", StringComparison.OrdinalIgnoreCase))
            {
                Write(action, null);
            }
            return null;
        }

        public static void Write(ShortcutAction action, ShortcutBinding binding)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SolidTweaksFeatureSettings.RegistryPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Could not persist the shortcut binding.");
                }
                if (binding == null)
                {
                    key.DeleteValue(ValueName(action), false);
                }
                else
                {
                    key.SetValue(ValueName(action), binding.Serialize(), RegistryValueKind.String);
                }
            }
        }

        public static bool IsMigrationComplete()
        {
            return SolidTweaksFeatureSettings.Read(MigrationMarker, false);
        }

        public static void MarkMigrationComplete()
        {
            SolidTweaksFeatureSettings.Write(MigrationMarker, true);
        }

        public static ShortcutAction? FindAction(ShortcutBinding candidate, ShortcutAction except)
        {
            foreach (ShortcutAction action in Enum.GetValues(typeof(ShortcutAction)))
            {
                if (action == except)
                {
                    continue;
                }
                ShortcutBinding current = Read(action);
                if (current != null && current.SameInput(candidate))
                {
                    return action;
                }
            }
            return null;
        }

        public static string ValueName(ShortcutAction action)
        {
            switch (action)
            {
                case ShortcutAction.MakeFixed:
                    return "shortcut.make-fixed";
                case ShortcutAction.ConstructionGeometry:
                    return "shortcut.construction-geometry";
                case ShortcutAction.Measure:
                    return "shortcut.measure";
                default:
                    return "shortcut.offset-entities";
            }
        }
    }
}
