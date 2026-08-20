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
            UiLanguageSelector.Initialize(SolidWorksPreferenceResolver.ReadCurrentLanguage());
            if (args.Length == 2 &&
                (string.Equals(args[0], "--register-addin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--unregister-addin", StringComparison.OrdinalIgnoreCase)))
            {
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

            string installFolder = SolidWorksPreferenceResolver.ReadInstallFolder();
            string path = string.IsNullOrWhiteSpace(installFolder)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "SOLIDWORKS Corp",
                    "SOLIDWORKS",
                    requested.Name + ".dll")
                : Path.Combine(installFolder, requested.Name + ".dll");
            if (!File.Exists(path))
            {
                ApplicationLog.Warning("Requested interop was not found: " + path);
                return null;
            }
            try
            {
                Assembly resolved = Assembly.LoadFrom(path);
                ApplicationLog.Info("Interop loaded: " + path);
                return resolved;
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("Failed to load interop " + path + ".", exception);
                return null;
            }
        }

        private static int RunAddinRegistration(string command, string assemblyPath)
        {
            try
            {
                ApplicationLog.Info("Starting " + command + " for " + assemblyPath + ".");
                Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(assemblyPath));
                var registration = new RegistrationServices();
                bool isRegister = string.Equals(command, "--register-addin", StringComparison.OrdinalIgnoreCase);
                Type addinType = assembly.GetType(
                    "SolidTweaks.SolidWorksAddin.SolidTweaksAddin",
                    true);
                bool success;
                if (isRegister)
                {
                    success = registration.RegisterAssembly(assembly, AssemblyRegistrationFlags.SetCodeBase);
                    InvokeRegistrationHook(addinType, "Register");
                }
                else
                {
                    InvokeRegistrationHook(addinType, "Unregister");
                    success = registration.UnregisterAssembly(assembly);
                }
                int result = success ? 0 : 2;
                ApplicationLog.Info("COM registration completed. Result=" + result + ".");
                return result;
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("COM registration helper process failed.", exception);
                return 1;
            }
        }

        private static void InvokeRegistrationHook(Type addinType, string methodName)
        {
            MethodInfo method = addinType.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                throw new MissingMethodException(addinType.FullName, methodName);
            }
            method.Invoke(null, new object[] { addinType });
        }
    }
}
