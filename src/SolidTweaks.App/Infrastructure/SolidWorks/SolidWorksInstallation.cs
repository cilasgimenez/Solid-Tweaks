using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using SolidTweaks.Shared;

namespace SolidTweaks.Infrastructure.SolidWorks
{
    internal sealed class SolidWorksInstallation
    {
        public int Year { get; private set; }
        public string Folder { get; private set; }
        public string ExecutablePath { get { return Path.Combine(Folder, "SLDWORKS.exe"); } }
        public bool IsSupported { get { return Year == 2025 || Year == 2026; } }

        internal SolidWorksInstallation(int year, string folder) { Year = year; Folder = folder; }
        public override string ToString()
        {
            return "SOLIDWORKS " + Year + (IsSupported ? "" : " — " + UiText.Get("Not supported"));
        }

        public static IList<SolidWorksInstallation> Discover()
        {
            var found = new Dictionary<int, SolidWorksInstallation>();
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            using (RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            using (RegistryKey root = machine.OpenSubKey(@"SOFTWARE\SolidWorks", false))
            {
                if (root == null) continue;
                foreach (string name in root.GetSubKeyNames())
                {
                    int year;
                    if (!name.StartsWith("SOLIDWORKS ", StringComparison.OrdinalIgnoreCase) ||
                        !int.TryParse(name.Substring(11), out year)) continue;
                    using (RegistryKey setup = root.OpenSubKey(name + @"\Setup", false))
                    {
                        string folder = setup == null ? null : setup.GetValue("SolidWorks Folder") as string;
                        if (string.IsNullOrWhiteSpace(folder)) continue;
                        string exe = Path.Combine(folder, "SLDWORKS.exe");
                        // Ignore stale Setup keys and keys pointing at another year's installation.
                        if (File.Exists(exe) && SolidWorksTarget.YearFromRevision(
                            FileVersionInfo.GetVersionInfo(exe).ProductVersion) == year && !found.ContainsKey(year))
                            found.Add(year, new SolidWorksInstallation(year, folder));
                    }
                }
            }
            return found.Values.OrderByDescending(item => item.Year).ToList();
        }

        public static SolidWorksInstallation Selected
        {
            get { return Discover().FirstOrDefault(item => item.Year == SolidWorksTarget.Year); }
        }

        public static void InitializeSelection()
        {
            IList<SolidWorksInstallation> installed = Discover();
            int saved = 0;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\SolidTweaks\Application", false))
                int.TryParse(Convert.ToString(key == null ? null : key.GetValue("SelectedSolidWorksYear")), out saved);
            SolidWorksInstallation initial = installed.FirstOrDefault(item => item.Year == saved && item.IsSupported)
                ?? installed.FirstOrDefault(item => item.Year == 2025)
                ?? installed.FirstOrDefault(item => item.IsSupported) ?? installed.FirstOrDefault();
            SolidWorksTarget.Year = initial == null ? 0 : initial.Year;
        }

        public static void SaveSelection()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\SolidTweaks\Application"))
                key.SetValue("SelectedSolidWorksYear", SolidWorksTarget.Year, RegistryValueKind.DWord);
        }

        public static bool IsSelectedProcessRunning()
        {
            return IsProcessRunning(SolidWorksTarget.Year);
        }

        internal static bool IsProcessRunning(int year)
        {
            foreach (Process process in Process.GetProcessesByName("SLDWORKS"))
            using (process)
            {
                try
                {
                    if (SolidWorksTarget.YearFromRevision(process.MainModule.FileVersionInfo.ProductVersion) == year)
                        return true;
                }
                catch (System.ComponentModel.Win32Exception) { return true; } // fail closed for updates
                catch (InvalidOperationException) { }
            }
            return false;
        }
    }
}
