using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using SolidTweaks.Infrastructure.Diagnostics;
using SolidWorks.Interop.sldworks;
using SolidTweaks.Shared;
using SolidTweaks.Core.SolidWorks;

namespace SolidTweaks.Infrastructure.SolidWorks
{
    internal sealed class SolidWorksService : ISolidWorksService, IDisposable
    {
        private readonly SolidWorksPreferenceResolver preferenceResolver;
        private ISldWorks application;

        public SolidWorksService(SolidWorksPreferenceResolver preferenceResolver)
        {
            this.preferenceResolver = preferenceResolver;
        }

        public bool IsConnected { get { return application != null; } }

        public bool HasActiveDocument
        {
            get
            {
                return TryGetActiveDocument() != null;
            }
        }

        public string Version
        {
            get
            {
                if (application != null)
                {
                    try
                    {
                        return FormatRevisionNumber(application.RevisionNumber());
                    }
                    catch (COMException) { }
                }

                return GetInstalledVersion();
            }
        }

        public bool TryConnect()
        {
            try
            {
                application = Marshal.GetActiveObject("SldWorks.Application") as ISldWorks;
                ApplicationLog.Info("SOLIDWORKS COM connection obtained=" + (application != null) + ".");
                return application != null;
            }
            catch (COMException)
            {
                ReleaseApplication();
                return false;
            }
        }

        public bool GetToggle(string preferenceName)
        {
            ISldWorks sw = RequireApplication();
            int preference = preferenceResolver.ResolveToggle(preferenceName);
            return sw.GetUserPreferenceToggle(preference);
        }

        public void SetToggle(string preferenceName, bool enabled)
        {
            ISldWorks sw = RequireApplication();
            int preference = preferenceResolver.ResolveToggle(preferenceName);
            sw.SetUserPreferenceToggle(preference, enabled);
        }

        public bool RunActiveDocumentCommand(int commandId)
        {
            IModelDoc2 document = TryGetActiveDocument() as IModelDoc2;
            if (document == null)
            {
                throw new UserFacingException(
                    "No active document was found.",
                    UiText.Get("No active document was found."));
            }

            return document.Extension.RunCommand(commandId, string.Empty);
        }

        public object GetActiveDocument()
        {
            ISldWorks sw = RequireApplication();
            return sw.ActiveDoc;
        }

        internal ISldWorks GetApplication()
        {
            return RequireApplication();
        }

        public string GetExecutablePath()
        {
            string installFolder = SolidWorksPreferenceResolver.ReadInstallFolder();
            string executable = string.IsNullOrWhiteSpace(installFolder)
                ? Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles),
                    "SOLIDWORKS Corp",
                    "SOLIDWORKS",
                    "SLDWORKS.exe")
                : Path.Combine(installFolder, "SLDWORKS.exe");
            if (!File.Exists(executable))
            {
                throw new UserFacingException(
                    "The SOLIDWORKS executable was not found: " + executable + ".",
                    UiText.Get("The SOLIDWORKS executable was not found."));
            }
            return executable;
        }

        public int ExitForUpdate()
        {
            ISldWorks sw = RequireApplication();
            int processId = sw.GetProcessID();
            try
            {
                sw.ExitApp();
            }
            finally
            {
                ReleaseApplication();
            }
            return processId;
        }

        public object CreateDocument(string templatePath, SolidWorksDocumentKind kind)
        {
            if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            {
                throw new UserFacingException(
                    "The document template was not found: " + templatePath + ".",
                    UiText.Get("The document template was not found."));
            }

            ISldWorks sw = RequireApplication();
            // NewDocument usa o tipo contido no próprio template. O parâmetro kind mantém a API
            // preparada para validação e seleção automática de templates em recursos futuros.
            object document = sw.NewDocument(templatePath, 0, 0.0, 0.0);
            if (document == null)
            {
                throw new UserFacingException(
                    "Could not create the " + kind + " document.",
                    UiText.Format("Could not create the {0} document.", kind));
            }

            return document;
        }

        public void LoadAddin(string addinPath, string addinGuid)
        {
            ISldWorks sw = RequireApplication();
            sw.LoadAddIn(addinPath);
            object addin = sw.GetAddInObject(addinGuid);
            if (addin == null)
            {
                throw new UserFacingException(
                    "SOLIDWORKS could not load the Solid Tweaks tab.",
                    UiText.Get("SOLIDWORKS could not load the Solid Tweaks tab."));
            }
            if (!Convert.ToBoolean(InvokeAddinMethod(addin, "IsUserInterfaceReady")))
            {
                throw new UserFacingException(
                    "The add-in was loaded, but the Solid Tweaks tab is incomplete.",
                    UiText.Get("The add-in was loaded, but the Solid Tweaks tab is incomplete."));
            }
        }

        public bool IsAddinUserInterfaceReady(string addinGuid)
        {
            if (!IsConnected)
            {
                return false;
            }
            try
            {
                object addin = application.GetAddInObject(addinGuid);
                if (addin == null)
                {
                    AppendAddinLog(
                        "GetAddInObject did not find add-in " + addinGuid + ".",
                        new InvalidOperationException("COM object not found."));
                    return false;
                }
                return Convert.ToBoolean(InvokeAddinMethod(addin, "IsUserInterfaceReady"));
            }
            catch (Exception exception)
            {
                AppendAddinLog("Failed to query IsUserInterfaceReady through Solid Tweaks.", exception);
                return false;
            }
        }

        public bool IsConnectionAlive()
        {
            if (application == null)
            {
                return false;
            }
            try
            {
                application.GetProcessID();
                return true;
            }
            catch (COMException)
            {
                ReleaseApplication();
                return false;
            }
        }

        public void Disconnect()
        {
            ReleaseApplication();
        }

        public bool IsAddinFeatureReady(string addinGuid, string featureId)
        {
            if (!IsConnected)
            {
                return false;
            }
            try
            {
                object addin = application.GetAddInObject(addinGuid);
                return addin != null && Convert.ToBoolean(
                    InvokeAddinMethod(addin, "IsFeatureReady", featureId));
            }
            catch (Exception exception)
            {
                AppendAddinLog("Failed to query feature " + featureId + ".", exception);
                return false;
            }
        }

        public bool TryGetAddinFeaturePresence(string addinGuid, string featureId, out bool present)
        {
            present = false;
            if (!IsConnected)
            {
                return false;
            }
            try
            {
                object addin = application.GetAddInObject(addinGuid);
                if (addin == null)
                {
                    return false;
                }
                present = Convert.ToBoolean(
                    InvokeAddinMethod(addin, "IsFeaturePresentInUserInterface", featureId));
                return true;
            }
            catch (Exception exception)
            {
                AppendAddinLog("Failed to query the presence of feature " + featureId + ".", exception);
                return false;
            }
        }

        public void RemoveAndUnloadAddin(string addinPath, string addinGuid)
        {
            ISldWorks sw = RequireApplication();
            object addin;
            try
            {
                addin = sw.GetAddInObject(addinGuid);
            }
            catch (COMException)
            {
                return;
            }
            if (addin != null)
            {
                try
                {
                    InvokeAddinMethod(addin, "RemoveUserInterface");
                }
                finally
                {
                    if (Marshal.IsComObject(addin))
                    {
                        Marshal.FinalReleaseComObject(addin);
                    }
                    addin = null;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                int unloadResult = sw.UnloadAddIn(addinPath);
                if (unloadResult != 0)
                {
                    throw new UserFacingException(
                        "The Solid Tweaks add-in could not be unloaded. Code: " + unloadResult + ".",
                        UiText.Format("The Solid Tweaks add-in could not be unloaded. Code: {0}.", unloadResult));
                }
            }
        }

        private static object InvokeAddinMethod(object addin, string methodName)
        {
            return InvokeAddinMethod(addin, methodName, null);
        }

        private static object InvokeAddinMethod(object addin, string methodName, params object[] arguments)
        {
            return addin.GetType().InvokeMember(
                methodName,
                BindingFlags.InvokeMethod,
                null,
                addin,
                arguments);
        }

        private static void AppendAddinLog(string message, Exception exception)
        {
            ApplicationLog.Error(message, exception);
        }

        public void Dispose()
        {
            ReleaseApplication();
        }

        private ISldWorks RequireApplication()
        {
            if (application == null && !TryConnect())
            {
                throw new UserFacingException(
                    "No active SOLIDWORKS session was found.",
                    UiText.Get("No active SOLIDWORKS session was found."));
            }

            return application;
        }

        private object TryGetActiveDocument()
        {
            try
            {
                ISldWorks connected = Marshal.GetActiveObject("SldWorks.Application") as ISldWorks;
                if (connected != null)
                {
                    application = connected;
                    object active = connected.ActiveDoc;
                    if (active == null && connected.GetDocumentCount() > 0)
                    {
                        active = connected.GetFirstDocument();
                    }
                    if (active != null)
                    {
                        return active;
                    }
                }
            }
            catch (COMException)
            {
                // Tenta abaixo com o RCW que ja estava conectado.
            }

            if (application != null)
            {
                try
                {
                    object document = application.ActiveDoc;
                    if (document == null && application.GetDocumentCount() > 0)
                    {
                        document = application.GetFirstDocument();
                    }
                    if (document != null)
                    {
                        return document;
                    }
                }
                catch (COMException)
                {
                    // A sessao pode continuar aberta depois que o RCW armazenado fica obsoleto.
                }
            }

            if (!TryConnect())
            {
                return null;
            }

            try
            {
                object document = application.ActiveDoc;
                return document ?? (application.GetDocumentCount() > 0 ? application.GetFirstDocument() : null);
            }
            catch (COMException)
            {
                return null;
            }
        }

        private void ReleaseApplication()
        {
            try
            {
                if (application != null && Marshal.IsComObject(application))
                {
                    Marshal.FinalReleaseComObject(application);
                }
            }
            catch (COMException)
            {
                // A sessão pode já ter terminado durante ExitApp.
            }
            application = null;
        }

        private static string GetInstalledVersion()
        {
            string folder = SolidWorksPreferenceResolver.ReadInstallFolder();
            string executable = string.IsNullOrWhiteSpace(folder) ? null : Path.Combine(folder, "SLDWORKS.exe");
            if (!string.IsNullOrWhiteSpace(executable) && File.Exists(executable))
            {
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(executable);
                return FormatRevisionNumber(version.ProductVersion) + " — " + UiText.Get("Not connected");
            }

            return UiText.Get("SOLIDWORKS 2025 not found");
        }

        private static string FormatRevisionNumber(string revision)
        {
            string[] parts = string.IsNullOrWhiteSpace(revision)
                ? new string[0]
                : revision.Split('.');
            int major;
            int servicePack;
            int servicePackMinor;
            if (parts.Length >= 2 &&
                int.TryParse(parts[0], out major) &&
                int.TryParse(parts[1], out servicePack))
            {
                servicePackMinor = 0;
                if (parts.Length >= 3)
                {
                    int.TryParse(parts[2], out servicePackMinor);
                }
                return "SOLIDWORKS " + (major + 1992) + " SP" + servicePack + "." + servicePackMinor;
            }

            return "SOLIDWORKS " + revision;
        }
    }
}
