namespace SolidTweaks.Shared
{
    // Parallel identity, NOT a replacement of the existing SW2025 COM component.
    // Keep this GUID stable for all future updates of the SW2026 component.
    internal static class SolidTweaks2026AddinIdentity
    {
        public const string CurrentGuid = "{CDF446F5-C95E-49D5-A7FC-C4A57A87C726}";
        public const string CurrentGuidWithoutBraces = "CDF446F5-C95E-49D5-A7FC-C4A57A87C726";
        public const string CurrentProgId = "SolidTweaks.SolidWorksAddin.SW2026";
        public const string AddinsRoot = @"SOFTWARE\SolidWorks\SOLIDWORKS 2026\Addins";
        public const string StartupRoot = @"Software\SolidWorks\SOLIDWORKS 2026\AddInsStartup";
        // Previous identities belong to SW2025. Repair scopes discovery to that year
        // without unregistering their COM servers or changing their binaries.
        public static readonly string[] LegacyGuids = GetLegacyGuids();
        private static string[] GetLegacyGuids()
        {
            var result = new string[SolidTweaksAddinIdentity.LegacyGuids.Length + 2];
            result[0] = SolidTweaksAddinIdentity.CurrentGuid;
            // Early 1.13 package found in deployed machines, before the later V113 GUID.
            result[1] = "{F00EFE1F-296E-4F52-9B46-FF7DBFDF5FBE}";
            System.Array.Copy(SolidTweaksAddinIdentity.LegacyGuids, 0, result, 2, result.Length - 2);
            return result;
        }
    }
}
