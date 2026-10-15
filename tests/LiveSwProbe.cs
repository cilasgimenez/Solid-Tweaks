using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidTweaks.Infrastructure.SolidWorks;

internal static class LiveSwProbe
{
    private const string Guid2026 = "{CDF446F5-C95E-49D5-A7FC-C4A57A87C726}";
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            int year = args.Length > 1 ? int.Parse(args[1]) : 2026;
            string guid = year == 2025 ? "{79D3CB00-7167-4FD6-9F1B-92D541DCA7CD}" : Guid2026;
            ISldWorks sw = SolidWorksRunningSessions.Find(year);
            if (sw == null) throw new Exception("No running SW" + year + " session was found.");
            Console.WriteLine("SW=" + sw.RevisionNumber() + "; PID=" + sw.GetProcessID() + "; documents=" + sw.GetDocumentCount());
            if (args.Length > 0 && args[0] == "exit-empty")
            {
                if (sw.GetDocumentCount() != 0) throw new Exception("Refusing to close a session with documents.");
                sw.ExitApp();
                return 0;
            }
            object addin = sw.GetAddInObject(guid);
            Console.WriteLine("Before: addin=" + (addin != null));
            if (args.Length > 0 && args[0].StartsWith("load") && addin == null)
            {
                string specifier = guid;
                if (args[0] == "load-path")
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"CLSID\" + guid + @"\InprocServer32"))
                    specifier = new Uri((string)key.GetValue("CodeBase")).LocalPath;
                Console.WriteLine("LoadAddIn(" + specifier + ")=" + sw.LoadAddIn(specifier));
                addin = sw.GetAddInObject(guid);
            }
            Console.WriteLine("After: addin=" + (addin != null));
            if (addin != null)
            {
                foreach (string name in new[] { "IsUserInterfaceReady", "IsFeaturePresentInUserInterface" })
                {
                    object[] values = name == "IsUserInterfaceReady" ? null : new object[] { "resources.export-step" };
                    Console.WriteLine(name + "=" + addin.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, addin, values));
                }
            }
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
