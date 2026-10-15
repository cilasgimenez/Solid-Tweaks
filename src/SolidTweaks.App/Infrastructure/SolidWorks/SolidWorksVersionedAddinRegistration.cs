using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32;
using SolidTweaks.Shared;

namespace SolidTweaks.Infrastructure.SolidWorks
{
    internal sealed class SolidWorksVersionedAddinRegistration : IAddinFeatureEnvironment
    {
        private readonly SolidWorksService host;
        private readonly int year;
        private readonly AddinFeatureLifecycle lifecycle;
        internal SolidWorksVersionedAddinRegistration(SolidWorksService host, int year)
        {
            if (year != 2025 && year != 2026) throw new ArgumentOutOfRangeException("year");
            this.host = host;
            this.year = year;
            lifecycle = new AddinFeatureLifecycle(this);
        }
        internal string Guid { get { return year == 2025 ? SolidTweaksAddinIdentity.CurrentGuid : SolidTweaks2026AddinIdentity.CurrentGuid; } }
        private string AddinsPath { get { return @"SOFTWARE\SolidWorks\SOLIDWORKS " + year + @"\Addins\" + Guid; } }
        private string FeaturesPath { get { return year == 2025 ? @"Software\SolidTweaks\Features" : @"Software\SolidTweaks\Versions\2026\Features"; } }
        private string DllName { get { return year == 2025 ? "SolidTweaks.SolidWorksAddin.v1_14.dll" : "SolidTweaks.SolidWorksAddin.SW2026.dll"; } }
        private string[] PackageFiles { get { return new[] { DllName, Path.ChangeExtension(DllName, ".tlb"), "SolidWorks.Interop.sldworks.dll", "SolidWorks.Interop.swconst.dll", "SolidWorks.Interop.swpublished.dll" }; } }
        public bool Read(string feature)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(FeaturesPath))
                return Convert.ToInt32(key == null ? 0 : key.GetValue(feature, 0)) != 0;
        }
        public void Write(string feature, bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(FeaturesPath)) key.SetValue(feature, enabled ? 1 : 0, RegistryValueKind.DWord);
        }
        public bool AnyEnabled { get { return Read(SolidTweaksFeatureSettings.ExportStepFeatureId) || Read(SolidTweaksFeatureSettings.ExportDxfFeatureId); } }
        public bool IsConnected { get { return host != null && SolidWorksTarget.Year == year && host.IsConnectionAlive(); } }
        public bool IsTargetRunning { get { return SolidWorksInstallation.IsProcessRunning(year); } }
        public bool IsRegistered
        {
            get { using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"CLSID\" + Guid)) return key != null; }
        }
        internal string RegisteredPath
        {
            get
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"CLSID\" + Guid + @"\InprocServer32"))
                {
                    Uri uri;
                    return Uri.TryCreate(key == null ? null : key.GetValue("CodeBase") as string, UriKind.Absolute, out uri) && uri.IsFile ? uri.LocalPath : null;
                }
            }
        }
        public bool IsCurrent
        {
            get
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(AddinsPath))
                    return key != null && !NeedsIsolationRepair && string.Equals(RegisteredPath, DeployedPath, StringComparison.OrdinalIgnoreCase) && MatchesPackage(RegisteredPath);
            }
        }
        public bool IsUpdateAvailable { get { return IsRegistered && !IsCurrent; } }
        public bool IsInterfaceReady { get { return IsCurrent && (!AnyEnabled || !IsConnected || host.IsAddinUserInterfaceReady(Guid)); } }
        public bool IsFeatureEnabled(string feature)
        {
            // Preserve explicit preferences while an installed component needs repair.
            return Read(feature) && (!IsConnected || IsUpdateAvailable ||
                (host.IsAddinUserInterfaceReady(Guid) && IsFeaturePresent(feature)));
        }
        public bool IsFeaturePresent(string feature)
        {
            bool present;
            return IsConnected && host.TryGetAddinFeaturePresence(Guid, feature, out present) && present;
        }
        public void Change(string feature, bool enabled) { lifecycle.Change(feature, enabled); }
        public void Update() { lifecycle.Update(); }
        public void RemoveInterface() { host.RemoveAddinUserInterface(Guid); }
        public void Synchronize() { host.LoadAddin(DeployedPath, Guid); }
        public void SetStartup(bool enabled)
        {
            // Native SOLIDWORKS uses the global startup key even for scoped discovery.
            // Distinct CLSIDs AND discovery restricted to one year are essential.
            foreach (string root in new[] { @"Software\SolidWorks\AddInsStartup", @"Software\SolidWorks\SOLIDWORKS " + year + @"\AddInsStartup" })
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(root + "\\" + Guid))
                key.SetValue(null, enabled ? 1 : 0, RegistryValueKind.DWord);
        }
        public void Install()
        {
            RequireClosedForInstall();
            ExtractPackage(Path.GetDirectoryName(DeployedPath));
            using (Process process = Process.Start(new ProcessStartInfo
            {
                FileName = typeof(SolidWorksVersionedAddinRegistration).Assembly.Location,
                Arguments = "--register-addin \"" + DeployedPath + "\" " + year,
                UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden
            }))
            {
                if (process == null) throw new InvalidOperationException("The registration process did not start.");
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException("Add-in registration returned " + process.ExitCode + ".");
            }
            if (!IsCurrent) throw new InvalidOperationException("The SW" + year + " registration could not be confirmed.");
        }
        private void RequireClosedForInstall()
        {
            if (IsRegistered && IsTargetRunning)
                throw new InvalidOperationException(UiText.Get("Close the selected SOLIDWORKS version before updating the add-in."));
            if (NeedsIsolationRepair && (SolidWorksInstallation.IsProcessRunning(2025) || SolidWorksInstallation.IsProcessRunning(2026)))
                throw new InvalidOperationException(UiText.Get("Close SOLIDWORKS 2025 and 2026 to repair the shared add-in registration."));
        }
        internal void RegisterPackage(string path)
        {
            RequireClosedForInstall();
            bool repairIsolation = NeedsIsolationRepair;
            if (!MatchesPackage(path) || !string.Equals(Path.GetFullPath(path), DeployedPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only the complete packaged SW" + year + " add-in may be registered.");
            Assembly assembly = Assembly.LoadFrom(path);
            Type addin = assembly.GetType("SolidTweaks.SolidWorksAddin.SolidTweaksAddin", true);
            if (!addin.GUID.ToString("B").Equals(Guid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The packaged add-in GUID does not match SW" + year + ".");
            // Keep the original SW2025 COM registration and binary when already correct.
            if (!string.Equals(RegisteredPath, path, StringComparison.OrdinalIgnoreCase))
            {
                if (!new RegistrationServices().RegisterAssembly(assembly, AssemblyRegistrationFlags.SetCodeBase))
                    throw new InvalidOperationException("COM registration failed.");
            }
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(AddinsPath))
            {
                if (key.GetValue(null) == null) key.SetValue(null, 0, RegistryValueKind.DWord);
                key.SetValue("Title", "Solid Tweaks (SW" + year + ")");
                key.SetValue("Description", "Recursos do Solid Tweaks no CommandManager");
            }
            RepairDiscoveryIsolation();
            if (repairIsolation && year == 2026)
            {
                var previous = new SolidWorksVersionedAddinRegistration(null, 2025);
                if (previous.MatchesPackagedFile(previous.RegisteredPath, previous.DllName))
                {
                    // Repair the dependencies of the unchanged original binary, and
                    // restore startup from explicit preferences, including explicit off.
                    previous.ExtractPackage(Path.GetDirectoryName(previous.RegisteredPath));
                    previous.SetStartup(previous.AnyEnabled);
                }
            }
            SetStartup(AnyEnabled);
        }
        internal static bool NeedsIsolationRepair
        {
            get
            {
                foreach (string guid in SolidTweaks2026AddinIdentity.LegacyGuids)
                using (RegistryKey global = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\SolidWorks\Addins\" + guid))
                using (RegistryKey wrong = Registry.LocalMachine.OpenSubKey(SolidTweaks2026AddinIdentity.AddinsRoot + "\\" + guid))
                    if (global != null || wrong != null) return true;
                using (RegistryKey global = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\SolidWorks\Addins\" + SolidTweaks2026AddinIdentity.CurrentGuid))
                    return global != null;
            }
        }
        internal static void RepairDiscoveryIsolation()
        {
            // Move discovery only. COM paths, binaries and existing preferences are preserved.
            foreach (string guid in SolidTweaks2026AddinIdentity.LegacyGuids)
            {
                string globalPath = @"SOFTWARE\SolidWorks\Addins\" + guid;
                using (RegistryKey original = Registry.LocalMachine.OpenSubKey(globalPath))
                {
                    if (original != null)
                    using (RegistryKey scoped = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\SolidWorks\SOLIDWORKS 2025\Addins\" + guid))
                        foreach (string name in original.GetValueNames()) scoped.SetValue(name, original.GetValue(name), original.GetValueKind(name));
                }
                Registry.LocalMachine.DeleteSubKeyTree(globalPath, false);
                Registry.LocalMachine.DeleteSubKeyTree(SolidTweaks2026AddinIdentity.AddinsRoot + "\\" + guid, false);
                Registry.CurrentUser.DeleteSubKeyTree(SolidTweaks2026AddinIdentity.StartupRoot + "\\" + guid, false);
            }
            Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\SolidWorks\Addins\" + SolidTweaks2026AddinIdentity.CurrentGuid, false);
        }
        internal void ExtractPackage(string folder)
        {
            Directory.CreateDirectory(folder);
            foreach (string name in PackageFiles)
            {
                string destination = Path.Combine(folder, name);
                if (MatchesPackagedFile(destination, name)) continue;
                if (IsRegistered && IsTargetRunning && string.Equals(
                    Path.GetDirectoryName(destination), Path.GetDirectoryName(RegisteredPath), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(UiText.Get("Close the selected SOLIDWORKS version before updating the add-in."));
                string temporary = destination + "." + System.Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (Stream input = OpenPackage(name))
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write)) input.CopyTo(output);
                    File.Copy(temporary, destination, true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
        internal string DeployedPath
        {
            get
            {
                string registered = RegisteredPath;
                if (year == 2025 && MatchesPackagedFile(registered, DllName)) return registered;
                using (Stream stream = OpenPackage(DllName))
                {
                    string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SolidTweaks", "Addins");
                    if (year == 2026) root = Path.Combine(root, "SW2026");
                    return Path.Combine(root, (year == 2025 ? "1.14.0.0-" : "1.16.0.0-") + Hash(stream), DllName);
                }
            }
        }
        internal bool MatchesPackage(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            foreach (string name in PackageFiles)
                if (!MatchesPackagedFile(name == DllName ? path : Path.Combine(Path.GetDirectoryName(path), name), name)) return false;
            return true;
        }
        private bool MatchesPackagedFile(string path, string name)
        {
            if (!File.Exists(path)) return false;
            using (Stream packaged = OpenPackage(name))
            using (Stream file = File.OpenRead(path)) return Hash(packaged) == Hash(file);
        }
        private Stream OpenPackage(string name)
        {
            string resource = name == DllName ? "SolidTweaks.Embedded." + name : "SolidTweaks.Embedded.SW" + year + "." + name;
            Stream stream = typeof(SolidWorksVersionedAddinRegistration).Assembly.GetManifestResourceStream(resource);
            if (stream == null) throw new InvalidOperationException("Missing package resource: " + resource);
            return stream;
        }
        private static string Hash(Stream stream)
        {
            using (SHA256 algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
        }
    }
}
