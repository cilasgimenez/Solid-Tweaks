using System;

namespace SolidTweaks.Shared
{
    // One target per application process. The selector is locked while work is in flight.
    internal static class SolidWorksTarget
    {
#if SW2026
        public static int Year { get { return 2026; } }
#else
        public static int Year { get; set; } = 2025;
#endif
        public static bool IsSupported { get { return Year == 2025 || Year == 2026; } }
        public static string VersionRoot { get { return @"Software\SolidWorks\SOLIDWORKS " + Year; } }
        public static string FeaturesPath
        {
            get { return Year == 2025 ? @"Software\SolidTweaks\Features" : @"Software\SolidTweaks\Versions\" + Year + @"\Features"; }
        }
        public static int YearFromRevision(string revision)
        {
            int major;
            return !string.IsNullOrWhiteSpace(revision) && int.TryParse(revision.Split('.')[0], out major)
                ? major + 1992 : 0;
        }
    }
}
