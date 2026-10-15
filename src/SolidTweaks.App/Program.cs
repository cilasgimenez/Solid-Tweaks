using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using SolidTweaks.Infrastructure.Diagnostics;
using SolidTweaks.Infrastructure.SolidWorks;
using SolidTweaks.Presentation;
using SolidTweaks.Shared;

namespace SolidTweaks
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ApplicationLog.Initialize(args);
            RegisterGlobalExceptionHandlers();
            ApplicationLog.Info("Solid Tweaks initialization.");
            AppDomain.CurrentDomain.AssemblyResolve += ResolveSolidWorksInterop;
            SolidWorksInstallation.InitializeSelection();
            UiLanguageSelector.Initialize(SolidWorksPreferenceResolver.ReadCurrentLanguage());
            if ((args.Length == 2 || args.Length == 3) &&
                (string.Equals(args[0], "--register-addin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--unregister-addin", StringComparison.OrdinalIgnoreCase)))
            {
                SolidWorksTarget.Year = args.Length == 3 && args[2] == "2026" ? 2026 : 2025;
                Environment.ExitCode = RunAddinRegistration(args[0], args[1]);
                ApplicationLog.Info("Helper process finished with code " + Environment.ExitCode + ".");
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new MainForm());
                ApplicationLog.Info("Application exited normally.");
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("Fatal failure in the application main loop.", exception);
                throw;
            }
        }

        private static void RegisterGlobalExceptionHandlers()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs eventArgs)
            {
                ApplicationLog.Error("Unhandled user interface exception.", eventArgs.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs eventArgs)
            {
                ApplicationLog.Error(
                    "Unhandled AppDomain exception. Terminating=" + eventArgs.IsTerminating + ".",
                    eventArgs.ExceptionObject as Exception);
            };
            TaskScheduler.UnobservedTaskException += delegate(object sender, UnobservedTaskExceptionEventArgs eventArgs)
            {
                ApplicationLog.Error("Unobserved asynchronous task exception.", eventArgs.Exception);
                eventArgs.SetObserved();
            };
        }

        private static Assembly ResolveSolidWorksInterop(object sender, ResolveEventArgs args)
        {
            var requested = new AssemblyName(args.Name);
            if (!requested.Name.StartsWith("SolidWorks.Interop.", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Resolve the referenced API, independently of the currently selected UI year.
            // The application uses API 33; the 2026 add-in uses its private API 34 package.
            string resource = "SolidTweaks.Embedded.SW" + (requested.Version.Major >= 34 ? "2026" : "2025") + "." + requested.Name + ".dll";
            using (Stream input = typeof(Program).Assembly.GetManifestResourceStream(resource))
            {
                if (input == null) return null;
                using (var buffer = new MemoryStream())
                {
                    input.CopyTo(buffer);
                    Assembly resolved = Assembly.Load(buffer.ToArray());
                    if (resolved.GetName().FullName != requested.FullName)
                        throw new FileLoadException("Packaged API identity mismatch: " + requested.FullName);
                    return resolved;
                }
            }
        }
        private static int RunAddinRegistration(string command, string assemblyPath)
        {
            try
            {
                if (command != "--register-addin") throw new InvalidOperationException("Unregistration is no longer used for feature changes.");
                ApplicationLog.Info("Registering SW" + SolidWorksTarget.Year + " package: " + assemblyPath);
                new SolidWorksVersionedAddinRegistration(null, SolidWorksTarget.Year).RegisterPackage(Path.GetFullPath(assemblyPath));
                return 0;
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("COM registration helper process failed.", exception);
                return 1;
            }
        }
    }
}