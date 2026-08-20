using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Win32;
using SolidTweaks.Infrastructure.Diagnostics;
using SolidTweaks.Shared;

namespace SolidTweaks.Infrastructure.SolidWorks
{
    internal sealed class SolidWorksAddinRegistrationService
    {
        internal const string AddinGuid = "{6330361C-6CCC-4DA1-890D-D6685BC8DBF0}";
        private const string LegacyAddinGuidV111 = "{F27105A6-4344-4081-B657-7E421FF0D6ED}";
        private const string LegacyAddinGuidV110 = "{E8327E13-5F74-41DB-9234-47994B1DDC75}";
        private const string LegacyAddinGuidV19 = "{0E9FA022-92A6-4C34-8915-7512514F3B05}";
        private const string LegacyAddinGuidV18 = "{131EB904-E091-4C86-95B0-B41E4F9869F0}";
        private const string LegacyAddinGuid = "{7D87C1B8-777D-4D69-BD53-6BD88C2F3E48}";
        private const string LegacyAddinGuidV12 = "{7271ABC6-6994-4C09-A8E1-CE231967E48A}";
        private const string LegacyAddinGuidV13 = "{0C6C9A95-EE42-40AE-928D-D31949D7251E}";
        private const string LegacyAddinGuidV14 = "{DF9D52E8-E929-42A4-A742-DB8F54B4F984}";
        private const string LegacyAddinGuidV15 = "{ADB6989A-A69B-4A6B-B979-37527988BB15}";
        private const string LegacyAddinGuidV16 = "{D52DA99A-B23B-4342-83F6-D9779C252217}";
        private const string LegacyAddinGuidV17 = "{C7D41943-0042-4140-93DE-7523EEDE7684}";
        private const string PackagedAddinResourceName =
            "SolidTweaks.Embedded.SolidTweaks.SolidWorksAddin.v1_13.dll";
        private readonly SolidWorksService solidWorks;

        public SolidWorksAddinRegistrationService(SolidWorksService solidWorks)
        {
            this.solidWorks = solidWorks;
            ApplicationLog.Info("Initializing the add-in registration service.");
            MigrateLegacyFeatureState();
            LogRegistrationState("State after migration");
        }

        public bool IsFeatureEnabled(string featureId)
        {
            ValidateFeatureId(featureId);
            bool presentInSolidWorks;
            if (solidWorks.TryGetAddinFeaturePresence(AddinGuid, featureId, out presentInSolidWorks))
            {
                if (SolidTweaksFeatureSettings.Read(featureId, false) != presentInSolidWorks)
                {
                    SolidTweaksFeatureSettings.Write(featureId, presentInSolidWorks);
                }
                return presentInSolidWorks;
            }

            if (!SolidTweaksFeatureSettings.Read(featureId, false))
            {
                return false;
            }

            if (IsUpdateAvailable())
            {
                return true;
            }
            if (!IsRegistrationCurrent())
            {
                return false;
            }
            return !solidWorks.IsConnected || solidWorks.IsAddinUserInterfaceReady(AddinGuid);
        }

        public bool IsUpdateAvailable()
        {
            return IsAnyLegacyAddinRegistered() || IsCurrentAddinUpdateAvailable();
        }

        public bool IsInstalledInterfaceReady()
        {
            return IsRegistrationCurrent() &&
                (!solidWorks.IsConnected || solidWorks.IsAddinUserInterfaceReady(AddinGuid));
        }

        public void UpdateInstalledAddin()
        {
            ApplicationLog.Info("Add-in update requested.");
            if (!IsUpdateAvailable())
            {
                ApplicationLog.Info("No update is available; request ignored.");
                return;
            }
            MigrateLegacyFeatureState();
            if (!SolidTweaksFeatureSettings.HasAnyAddinFeatureEnabled())
            {
                LogRegistrationState("Update blocked with no configured features");
                throw new UserFacingException(
                    "No Solid Tweaks tab feature is configured for the update.",
                    UiText.Get("No Solid Tweaks tab feature is configured for the update."));
            }
            InstallOrRefresh();
            if (!IsRegistrationCurrent() || IsUpdateAvailable())
            {
                throw new UserFacingException(
                    "The add-in update could not be confirmed.",
                    UiText.Get("The add-in update could not be confirmed."));
            }
            LogRegistrationState("Update completed");
        }

        public void SetFeatureEnabled(string featureId, bool enabled)
        {
            ValidateFeatureId(featureId);
            ApplicationLog.Info("Changing feature " + featureId + " to " + enabled + ".");
            bool previousValue = SolidTweaksFeatureSettings.Read(featureId, false);
            SolidTweaksFeatureSettings.Write(featureId, enabled);
            try
            {
                if (SolidTweaksFeatureSettings.HasAnyAddinFeatureEnabled())
                {
                    InstallOrRefresh();
                }
                else
                {
                    Uninstall();
                }

                if (enabled != IsFeatureEnabled(featureId))
                {
                    throw new UserFacingException(
                        "The feature state could not be confirmed: " + featureId + ".",
                        UiText.Format("The state of {0} could not be confirmed.", FeatureDisplayName(featureId)));
                }
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("Failed to change feature " + featureId + ". Restoring the previous state.", exception);
                SolidTweaksFeatureSettings.Write(featureId, previousValue);
                try
                {
                    RestoreConfiguredInterface();
                }
                catch
                {
                    // Mantém o erro original; uma nova tentativa pelo checkbox fará o reparo.
                }
                throw;
            }
        }

        private void InstallOrRefresh()
        {
            ApplicationLog.Info("Starting installation/repair. Destination=" + DeployedAddinPath + ".");
            EnsurePackagedAddinResource();

            if (!IsRegistrationCurrent())
            {
                ApplicationLog.Info("Current registration does not match the package. Unloading known versions.");
                UnloadKnownAddins();
                DeployAddin();
                ApplicationLog.Info("DLL deployed. Starting elevated registration.");
                RunElevated("--register-addin");
                if (solidWorks.IsConnected)
                {
                    ApplicationLog.Info("Loading the new add-in in the active SOLIDWORKS session.");
                    solidWorks.LoadAddin(DeployedAddinPath, AddinGuid);
                }
            }
            else if (solidWorks.IsConnected && !solidWorks.IsAddinUserInterfaceReady(AddinGuid))
            {
                ApplicationLog.Info("Registration is current; loading the missing interface.");
                solidWorks.LoadAddin(DeployedAddinPath, AddinGuid);
            }
            if (!IsRegistrationCurrent() ||
                (solidWorks.IsConnected && !solidWorks.IsAddinUserInterfaceReady(AddinGuid)))
            {
                throw new UserFacingException(
                    "The Solid Tweaks tab could not be confirmed.",
                    UiText.Get("The Solid Tweaks tab could not be confirmed."));
            }
            ApplicationLog.Info("Installation/repair confirmed.");
        }

        public void Uninstall()
        {
            UnloadKnownAddins();
            if (IsRegistered())
            {
                RunElevated("--unregister-addin");
            }
            if (IsRegistered())
            {
                throw new UserFacingException(
                    "The removal of the Solid Tweaks tab could not be confirmed.",
                    UiText.Get("The removal of the Solid Tweaks tab could not be confirmed."));
            }
        }

        private void UnloadKnownAddins()
        {
            if (!solidWorks.IsConnected)
            {
                return;
            }
            solidWorks.RemoveAndUnloadAddin(RegisteredAddinPath ?? DeployedAddinPath, AddinGuid);
            solidWorks.RemoveAndUnloadAddin(
                LegacyRegisteredAddinPathV111 ?? LegacyAddinPathV111,
                LegacyAddinGuidV111);
            solidWorks.RemoveAndUnloadAddin(LegacyAddinPath, LegacyAddinGuid);
            solidWorks.RemoveAndUnloadAddin(LegacyAddinPathV12, LegacyAddinGuidV12);
            solidWorks.RemoveAndUnloadAddin(LegacyAddinPathV13, LegacyAddinGuidV13);
            solidWorks.RemoveAndUnloadAddin(LegacyAddinPathV14, LegacyAddinGuidV14);
            solidWorks.RemoveAndUnloadAddin(LegacyAddinPathV15, LegacyAddinGuidV15);
            solidWorks.RemoveAndUnloadAddin(LegacyAddinPathV16, LegacyAddinGuidV16);
            solidWorks.RemoveAndUnloadAddin(LegacyAddinPathV17, LegacyAddinGuidV17);
            solidWorks.RemoveAndUnloadAddin(
                LegacyRegisteredAddinPathV18 ?? LegacyAddinPathV18,
                LegacyAddinGuidV18);
            solidWorks.RemoveAndUnloadAddin(
                LegacyRegisteredAddinPathV19 ?? LegacyAddinPathV19,
                LegacyAddinGuidV19);
            solidWorks.RemoveAndUnloadAddin(
                LegacyRegisteredAddinPathV110 ?? LegacyAddinPathV110,
                LegacyAddinGuidV110);
        }

        private void RestoreConfiguredInterface()
        {
            if (SolidTweaksFeatureSettings.HasAnyAddinFeatureEnabled())
            {
                InstallOrRefresh();
            }
            else if (IsRegistered())
            {
                Uninstall();
            }
        }

        private static void ValidateFeatureId(string featureId)
        {
            if (!string.Equals(featureId, SolidTweaksFeatureSettings.ExportStepFeatureId, StringComparison.Ordinal) &&
                !string.Equals(featureId, SolidTweaksFeatureSettings.ExportDxfFeatureId, StringComparison.Ordinal))
            {
                throw new ArgumentOutOfRangeException("featureId", featureId, "Unknown tab feature.");
            }
        }

        private static string FeatureDisplayName(string featureId)
        {
            return string.Equals(featureId, SolidTweaksFeatureSettings.ExportStepFeatureId, StringComparison.Ordinal)
                ? UiText.Get("Export STEP")
                : UiText.Get("Export Sheet Metal DXF");
        }

        private static void MigrateLegacyFeatureState()
        {
            bool currentRegistered = IsRegistered();
            bool legacyRegistered = IsAnyLegacyAddinRegistered();
            if (!currentRegistered && !legacyRegistered)
            {
                return;
            }

            bool stepEnabled = SolidTweaksFeatureSettings.Read(
                SolidTweaksFeatureSettings.ExportStepFeatureId,
                false);
            bool dxfEnabled = SolidTweaksFeatureSettings.Read(
                SolidTweaksFeatureSettings.ExportDxfFeatureId,
                false);
            if (stepEnabled || dxfEnabled)
            {
                return;
            }

            bool supportsBothCommands = currentRegistered || IsRegistered(LegacyAddinGuidV111);
            SolidTweaksFeatureSettings.Write(SolidTweaksFeatureSettings.ExportStepFeatureId, true);
            SolidTweaksFeatureSettings.Write(SolidTweaksFeatureSettings.ExportDxfFeatureId, supportsBothCommands);
            ApplicationLog.Warning(
                "Add-in registered with no active features in the Registry. State recovered automatically: " +
                "STEP=true; DXF=" + supportsBothCommands + ".");
        }

        private static void LogRegistrationState(string context)
        {
            try
            {
                ApplicationLog.Info(
                    context + ": currentRegistered=" + IsRegistered() +
                    "; legacyRegistered=" + IsAnyLegacyAddinRegistered() +
                    "; updateAvailable=" + (IsAnyLegacyAddinRegistered() || IsCurrentAddinUpdateAvailable()) +
                    "; STEP=" + SolidTweaksFeatureSettings.Read(SolidTweaksFeatureSettings.ExportStepFeatureId, false) +
                    "; DXF=" + SolidTweaksFeatureSettings.Read(SolidTweaksFeatureSettings.ExportDxfFeatureId, false) +
                    "; registeredPath=" + (RegisteredAddinPath ?? "<missing>") +
                    "; destination=" + DeployedAddinPath + ".");
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("Failed to record add-in state diagnostics.", exception);
            }
        }

        private static bool IsAnyLegacyAddinRegistered()
        {
            return IsRegistered(LegacyAddinGuidV111) ||
                IsRegistered(LegacyAddinGuidV110) ||
                IsRegistered(LegacyAddinGuidV19) ||
                IsRegistered(LegacyAddinGuidV18) ||
                IsRegistered(LegacyAddinGuidV17) ||
                IsRegistered(LegacyAddinGuidV16) ||
                IsRegistered(LegacyAddinGuidV15) ||
                IsRegistered(LegacyAddinGuidV14) ||
                IsRegistered(LegacyAddinGuidV13) ||
                IsRegistered(LegacyAddinGuidV12) ||
                IsRegistered(LegacyAddinGuid);
        }

        private static bool IsCurrentAddinUpdateAvailable()
        {
            if (!IsRegistered())
            {
                return false;
            }

            string registeredPath = RegisteredAddinPath;
            return !PathsEqual(registeredPath, DeployedAddinPath) ||
                !EmbeddedAddinEqualsFile(registeredPath);
        }

        private static bool IsLegacyV18Registered()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\SOLIDWORKS\Addins\" + LegacyAddinGuidV18,
                false))
            {
                return key != null;
            }
        }

        private static bool IsLegacyV19Registered()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\SOLIDWORKS\Addins\" + LegacyAddinGuidV19,
                false))
            {
                return key != null;
            }
        }

        private bool IsRegistrationCurrent()
        {
            if (!File.Exists(DeployedAddinPath) || !IsRegistered() || !IsStartupEnabled() ||
                !EmbeddedAddinEqualsFile(DeployedAddinPath))
            {
                return false;
            }
            try
            {
                Version fileVersion = AssemblyName.GetAssemblyName(DeployedAddinPath).Version;
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(
                    @"CLSID\" + AddinGuid + @"\InprocServer32",
                    false))
                {
                    string assemblyValue = key == null ? null : key.GetValue("Assembly") as string;
                    string codeBase = key == null ? null : key.GetValue("CodeBase") as string;
                    return !string.IsNullOrWhiteSpace(assemblyValue) &&
                        new AssemblyName(assemblyValue).Version == fileVersion &&
                        PathsEqual(CodeBaseToPath(codeBase), DeployedAddinPath);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsRegistered()
        {
            return IsRegistered(AddinGuid);
        }

        private static bool IsRegistered(string addinGuid)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\SOLIDWORKS\Addins\" + addinGuid,
                false))
            {
                return key != null;
            }
        }

        private static bool IsStartupEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Software\SOLIDWORKS\AddInsStartup\" + AddinGuid,
                false))
            {
                return key != null && Convert.ToInt32(key.GetValue(null, 0)) == 1;
            }
        }

        private static void DeployAddin()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DeployedAddinPath));
            if (EmbeddedAddinEqualsFile(DeployedAddinPath))
            {
                ApplicationLog.Info("The deployed DLL already matches the embedded resource.");
                return;
            }

            string temporaryPath = DeployedAddinPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (Stream source = OpenPackagedAddinResource())
                using (var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    source.CopyTo(destination);
                }
                File.Copy(temporaryPath, DeployedAddinPath, true);
                ApplicationLog.Info("Embedded resource extracted to " + DeployedAddinPath + ".");
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static void EnsurePackagedAddinResource()
        {
            using (Stream stream = OpenPackagedAddinResource())
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("The Solid Tweaks tab component was not embedded in the executable.");
                }
            }
        }

        private static Stream OpenPackagedAddinResource()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PackagedAddinResourceName);
            if (stream == null)
            {
                throw new InvalidOperationException("The Solid Tweaks tab component was not embedded in the executable.");
            }
            return stream;
        }

        private static bool EmbeddedAddinEqualsFile(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using (SHA256 algorithm = SHA256.Create())
            using (Stream resource = OpenPackagedAddinResource())
            using (FileStream file = File.OpenRead(path))
            {
                byte[] resourceHash = algorithm.ComputeHash(resource);
                byte[] fileHash = algorithm.ComputeHash(file);
                if (resourceHash.Length != fileHash.Length)
                {
                    return false;
                }
                for (int index = 0; index < resourceHash.Length; index++)
                {
                    if (resourceHash[index] != fileHash[index])
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private static string DeployedAddinPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SolidTweaks",
                    "Addins",
                    "1.13.0.0-" + GetPackagedAddinHash(),
                    "SolidTweaks.SolidWorksAddin.v1_13.dll");
            }
        }

        private static string GetPackagedAddinHash()
        {
            using (SHA256 algorithm = SHA256.Create())
            using (Stream resource = OpenPackagedAddinResource())
            {
                byte[] hash = algorithm.ComputeHash(resource);
                var value = new System.Text.StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    value.Append(hash[index].ToString("X2"));
                }
                return value.ToString();
            }
        }

        private static string LegacyAddinPathV111
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SolidTweaks",
                    "Addins",
                    "1.11.0.0",
                    "SolidTweaks.SolidWorksAddin.v1_11.dll");
            }
        }

        private static string LegacyAddinPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SolidTweaks.SolidWorksAddin.dll"); }
        }

        private static string LegacyAddinPathV12
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SolidTweaks",
                    "Addins",
                    "1.2.0.0",
                    "SolidTweaks.SolidWorksAddin.v1_2.dll");
            }
        }

        private static string LegacyAddinPathV13
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SolidTweaks",
                    "Addins",
                    "1.3.0.0",
                    "SolidTweaks.SolidWorksAddin.v1_3.dll");
            }
        }

        private static string LegacyAddinPathV14
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SolidTweaks",
                    "Addins",
                    "1.4.0.0",
                    "SolidTweaks.SolidWorksAddin.v1_4.dll");
            }
        }

        private static string LegacyAddinPathV15
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SolidTweaks",
                    "Addins",
                    "1.5.0.0",
                    "SolidTweaks.SolidWorksAddin.v1_5.dll");
            }
        }

        private static string LegacyAddinPathV16
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SolidTweaks",
                    "Addins",
                    "1.6.0.0",
                    "SolidTweaks.SolidWorksAddin.v1_6.dll");
            }
        }

        private static string LegacyAddinPathV17
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SolidTweaks",
                    "Addins",
                    "1.7.0.0",
                    "SolidTweaks.SolidWorksAddin.v1_7.dll");
            }
        }

        private static string LegacyAddinPathV18
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SolidTweaks",
                    "Addins",
                    "1.8.0.0",
                    "SolidTweaks.SolidWorksAddin.v1_8.dll");
            }
        }

        private static string LegacyAddinPathV19
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SolidTweaks",
                    "Addins",
                    "1.9.0.0",
                    "SolidTweaks.SolidWorksAddin.v1_9.dll");
            }
        }

        private static string LegacyAddinPathV110
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SolidTweaks",
                    "Addins",
                    "1.10.0.0",
                    "SolidTweaks.SolidWorksAddin.v1_10.dll");
            }
        }

        private static string RegisteredAddinPath
        {
            get
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(
                    @"CLSID\" + AddinGuid + @"\InprocServer32",
                    false))
                {
                    return key == null ? null : CodeBaseToPath(key.GetValue("CodeBase") as string);
                }
            }
        }

        private static string LegacyRegisteredAddinPathV18
        {
            get
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(
                    @"CLSID\" + LegacyAddinGuidV18 + @"\InprocServer32",
                    false))
                {
                    return key == null ? null : CodeBaseToPath(key.GetValue("CodeBase") as string);
                }
            }
        }

        private static string LegacyRegisteredAddinPathV19
        {
            get
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(
                    @"CLSID\" + LegacyAddinGuidV19 + @"\InprocServer32",
                    false))
                {
                    return key == null ? null : CodeBaseToPath(key.GetValue("CodeBase") as string);
                }
            }
        }

        private static string LegacyRegisteredAddinPathV110
        {
            get
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(
                    @"CLSID\" + LegacyAddinGuidV110 + @"\InprocServer32",
                    false))
                {
                    return key == null ? null : CodeBaseToPath(key.GetValue("CodeBase") as string);
                }
            }
        }

        private static string LegacyRegisteredAddinPathV111
        {
            get
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(
                    @"CLSID\" + LegacyAddinGuidV111 + @"\InprocServer32",
                    false))
                {
                    return key == null ? null : CodeBaseToPath(key.GetValue("CodeBase") as string);
                }
            }
        }

        private static string CodeBaseToPath(string codeBase)
        {
            Uri uri;
            return !string.IsNullOrWhiteSpace(codeBase) && Uri.TryCreate(codeBase, UriKind.Absolute, out uri) && uri.IsFile
                ? uri.LocalPath
                : null;
        }

        private static bool PathsEqual(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) &&
                string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
        }

        private static void RunElevated(string command)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ApplicationExecutablePath,
                Arguments = command + " \"" + DeployedAddinPath + "\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                ApplicationLog.Info("Starting elevated process: " + command + ".");
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        throw new InvalidOperationException("The registration process did not start.");
                    }
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException("Add-in registration returned code " + process.ExitCode + ".");
                    }
                    ApplicationLog.Info("Elevated process completed with code 0.");
                }
            }
            catch (Win32Exception exception)
            {
                ApplicationLog.Error("Failed to start or authorize the elevated process.", exception);
                throw new UserFacingException(
                    "Windows authorization is required to change the SOLIDWORKS add-in.",
                    UiText.Get("Windows authorization is required to change the SOLIDWORKS add-in."),
                    exception);
            }
        }

        private static string ApplicationExecutablePath
        {
            get { return Process.GetCurrentProcess().MainModule.FileName; }
        }
    }
}
