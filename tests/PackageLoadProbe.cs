using System;
using System.IO;
using System.Reflection;

// Run in a fresh process with no preloaded SOLIDWORKS API and no assembly resolver.
// This verifies the deployed package rather than the developer machine's search paths.
internal static class PackageLoadProbe
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string path = Path.GetFullPath(args[0]);
            Assembly assembly = Assembly.LoadFrom(path);
            assembly.GetType("SolidTweaks.Infrastructure.Diagnostics.ApplicationLog")
                .GetField("initialized", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
            Type type = assembly.GetType("SolidTweaks.SolidWorksAddin.SolidTweaksAddin", true);
            object addin = Activator.CreateInstance(type);
            object ready = type.GetMethod("IsUserInterfaceReady").Invoke(addin, null);
            if (!false.Equals(ready)) throw new Exception("An unconnected add-in must not report its UI as ready.");
            foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!loaded.GetName().Name.StartsWith("SolidWorks.Interop.")) continue;
                if (!string.Equals(Path.GetDirectoryName(loaded.Location), Path.GetDirectoryName(path), StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Dependency loaded outside the package: " + loaded.Location);
                Console.WriteLine("Package dependency: " + loaded.GetName().Name);
            }
            Console.WriteLine("PASS: add-in activated in a clean process from its package only.");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
