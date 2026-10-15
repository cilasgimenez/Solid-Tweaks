using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Security.Cryptography;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidTweaks.Infrastructure.Persistence;
using SolidTweaks.Infrastructure.SolidWorks;
using SolidTweaks.Presentation;
using SolidTweaks.Shared;

internal static class Tests
{
    private static int passed;
    private static string workspace;
    private static string sw2025Folder;
    private static string sw2026Folder;
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Step = SolidTweaksFeatureSettings.ExportStepFeatureId;
    private const string Dxf = SolidTweaksFeatureSettings.ExportDxfFeatureId;

    [STAThread]
    private static int Main(string[] args)
    {
        workspace = Path.GetFullPath(args[0]);
        try
        {
            typeof(SolidTweaks.Infrastructure.Diagnostics.ApplicationLog).GetField("initialized", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
            var installed = SolidWorksInstallation.Discover();
            sw2025Folder = installed.Single(item => item.Year == 2025).Folder;
            sw2026Folder = installed.Single(item => item.Year == 2026).Folder;
            Check(installed.All(item => File.Exists(item.ExecutablePath)), "Installed versions have real executables");
            Console.WriteLine("Detected: " + string.Join(", ", installed.Select(item => item.ToString())));
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs request)
            {
                var name = new AssemblyName(request.Name);
                if (!name.Name.StartsWith("SolidWorks.Interop.")) return null;
                return Assembly.LoadFrom(Path.Combine(name.Version.Major >= 34 ? sw2026Folder : sw2025Folder, name.Name + ".dll"));
            };
            foreach (string folder in new[] { sw2025Folder, sw2026Folder })
                foreach (string name in new[] { "sldworks", "swconst", "swpublished" })
                    Assembly.LoadFrom(Path.Combine(folder, "SolidWorks.Interop." + name + ".dll"));
            using (var hash = SHA256.Create()) hash.ComputeHash(new byte[] { 1 });
            using (var registry = new IsolatedRegistry())
            {
                TestDetection();
                TestPersistence();
                TestLifecycle();
                TestRegistrationAndPackage();
                TestDeploymentDependencies();
                TestSessionRevisionGuard();
                registry.RestoreMachineAndClasses();
                TestForm();
            }
            Console.WriteLine("PASS: " + passed + " assertions. Registry redirected to disposable test keys; no installed add-in changed.");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
    private static void Check(bool result, string description)
    {
        if (!result) throw new Exception("FAIL: " + description);
        passed++;
        Console.WriteLine("PASS: " + description);
    }
    private static void TestDetection()
    {
        foreach (int year in new[] { 2017, 2025, 2026, 2027 })
        using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\SolidWorks\SOLIDWORKS " + year + @"\Setup"))
            key.SetValue("SolidWorks Folder", year == 2026 ? sw2026Folder : sw2025Folder);
        var found = SolidWorksInstallation.Discover();
        Check(found.Count == 2 && found.Any(item => item.Year == 2025) && found.Any(item => item.Year == 2026), "Stale year keys do not appear as installations");
        SolidWorksInstallation.InitializeSelection();
        Check(SolidWorksTarget.Year == 2025, "First selection preserves SW2025 workflow");
        SolidWorksTarget.Year = 2026;
        SolidWorksInstallation.SaveSelection();
        SolidWorksInstallation.InitializeSelection();
        Check(SolidWorksTarget.Year == 2026, "Selected version persists");
    }
    private static void TestPersistence()
    {
        SolidWorksTarget.Year = 2025;
        SolidTweaksFeatureSettings.Write(Step, true);
        SolidTweaksFeatureSettings.Write(Dxf, false);
        new FeatureStateStore().Write("keyboard.space-normal-to", true);
        ShortcutBindingStore.Write(ShortcutAction.Measure, ShortcutBinding.Keyboard(77, 50, false));
        var visuals = new VisualSettingsService();
        visuals.SetEnhancedGraphicsPerformance(true);
        visuals.SetFullAntialiasing(true);
        SolidWorksTarget.Year = 2026;
        Check(!SolidTweaksFeatureSettings.Read(Step, false) && ShortcutBindingStore.Read(ShortcutAction.Measure) == null,
            "New SW2026 profile does not pretend SW2025 features are installed");
        Check(!new FeatureStateStore().Read("keyboard.space-normal-to", false), "Space state is separate by version");
        SolidTweaksFeatureSettings.Write(Step, false);
        SolidTweaksFeatureSettings.Write(Dxf, true);
        ShortcutBindingStore.Write(ShortcutAction.Measure, ShortcutBinding.Keyboard(78, 49, false));
        visuals.SetEnhancedGraphicsPerformance(false);
        visuals.SetFullAntialiasing(false);
        Check(SolidTweaksFeatureSettings.HasValue(Step) && !SolidTweaksFeatureSettings.Read(Step, true), "Explicit disabled is distinct from missing");
        Check(ShortcutBindingStore.IsMigrationComplete(), "SW2026 never migrates the old SW2025 shortcuts");
        SolidWorksTarget.Year = 2025;
        Check(SolidTweaksFeatureSettings.Read(Step, false) && !SolidTweaksFeatureSettings.Read(Dxf, true), "SW2025 feature values preserved");
        Check(ShortcutBindingStore.Read(ShortcutAction.Measure).VirtualKey == 77, "SW2025 shortcut preserved");
        Check(visuals.IsEnhancedGraphicsPerformanceEnabled() && visuals.IsFullAntialiasingEnabled(), "SW2025 visual preferences preserved");
    }
    private static void TestLifecycle()
    {
        var environment = new FakeEnvironment();
        var lifecycle = new AddinFeatureLifecycle(environment);
        lifecycle.Change(Step, true);
        lifecycle.Change(Dxf, true);
        Check(environment.Loads == 1 && environment.Startup, "Install both buttons with one load");
        lifecycle.Change(Step, false);
        Check(!environment.IsFeaturePresent(Step) && environment.IsFeaturePresent(Dxf), "Remove one button preserves the other");
        lifecycle.Change(Dxf, false);
        Check(!environment.Startup && environment.Loaded && environment.Present.Count == 0, "Remove both: dormant object and startup disabled");
        lifecycle.Change(Step, true);
        Check(environment.Loads == 1 && environment.IsFeaturePresent(Step), "Re-add in same session without unload/reload");
        lifecycle.Change(Step, false);
        environment.Restart();
        Check(!environment.Loaded && environment.Loads == 1, "Restart with both disabled does not load");
        lifecycle.Change(Dxf, true);
        Check(environment.Loads == 2 && environment.IsFeaturePresent(Dxf), "Add after restart loads once");
        foreach (int mask in new[] { 0, 1, 2, 3 })
        {
            environment.Write(Step, (mask & 1) != 0);
            environment.Write(Dxf, (mask & 2) != 0);
            environment.IsConnected = environment.IsTargetRunning = false;
            environment.IsCurrent = false;
            lifecycle.Update();
            Check(environment.Read(Step) == ((mask & 1) != 0) && environment.Read(Dxf) == ((mask & 2) != 0) &&
                environment.Startup == (mask != 0), "Update preserves preferences and startup for mask=" + mask);
            environment.Restart();
            Check(environment.Loaded == (mask != 0), "Restart after update honors disabled/enabled mask=" + mask);
        }
        environment.IsCurrent = false;
        bool rejected = false;
        try { lifecycle.Change(Step, false); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && environment.Read(Step), "Open target blocks DLL replacement before changing preferences");
        environment.IsCurrent = true;
        environment.FailSynchronization = true;
        rejected = false;
        try { lifecycle.Change(Step, false); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && environment.Read(Step), "Synchronization failure restores previous preference");
    }
    private static void TestRegistrationAndPackage()
    {
        SolidWorksTarget.Year = 2026;
        string oldGuid = SolidTweaksAddinIdentity.CurrentGuid;
        string oldStartup = @"Software\SolidWorks\AddInsStartup\" + oldGuid;
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(oldStartup)) key.SetValue(null, 1);
        using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\SolidWorks\Addins\" + oldGuid)) key.SetValue("Title", "untouched-2025");
        using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(@"CLSID\" + oldGuid + @"\InprocServer32")) key.SetValue("CodeBase", "original-2025.dll");
        using (var host = new SolidWorksService(new SolidWorksPreferenceResolver()))
        {
            var registration = new SolidWorksVersionedAddinRegistration(host, 2026);
            registration.SetStartup(true);
            registration.SetStartup(false);
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(oldStartup))
                Check(Convert.ToInt32(key.GetValue(null)) == 1, "2026 startup writes preserve global SW2025 startup");
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SolidTweaks2026AddinIdentity.StartupRoot + "\\" + oldGuid))
                Check(key == null, "2026 startup never creates aliases of the 2025 identity");
        }
        Assembly app = typeof(SolidWorksTarget).Assembly;
        using (Stream packaged = app.GetManifestResourceStream("SolidTweaks.Embedded.SolidTweaks.SolidWorksAddin.v1_14.dll"))
        using (Stream original = File.OpenRead(Path.Combine(workspace, @"src\Compatibility\SW2025\SolidTweaks.SolidWorksAddin.v1_14.dll")))
        using (var sha = SHA256.Create())
            Check(BitConverter.ToString(sha.ComputeHash(packaged)) == BitConverter.ToString(sha.ComputeHash(original)), "SW2025 embedded DLL is byte-for-byte original");
        Assembly addin = Assembly.LoadFrom(Path.Combine(workspace, @"src\SolidTweaks.SolidWorks2026Addin\bin\Release\SolidTweaks.SolidWorksAddin.SW2026.dll"));
        addin.GetType("SolidTweaks.Infrastructure.Diagnostics.ApplicationLog").GetField("initialized", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        Type type = addin.GetType("SolidTweaks.SolidWorksAddin.SolidTweaksAddin", true);
        Check(type.GUID.ToString("B").Equals(SolidTweaks2026AddinIdentity.CurrentGuid, StringComparison.OrdinalIgnoreCase), "Application and SW2026 DLL GUID match");
        Check(addin.GetReferencedAssemblies().Where(item => item.Name.StartsWith("SolidWorks.Interop.")).All(item => item.Version.Major == 34), "SW2026 add-in compiled against revision 34 API");
        type.GetMethod("Register").Invoke(null, new object[] { type });
        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(SolidTweaks2026AddinIdentity.AddinsRoot + "\\" + oldGuid))
            Check(key == null, "2026 registration never duplicates the 2025 identity");
        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(SolidTweaks2026AddinIdentity.AddinsRoot + "\\" + SolidTweaks2026AddinIdentity.CurrentGuid))
            Check(key != null, "SW2026 registration is version-specific");
        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\SolidWorks\Addins\" + oldGuid))
            Check((string)key.GetValue("Title") == "untouched-2025", "SW2026 registration preserves SW2025 add-in entry");
        using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"CLSID\" + oldGuid + @"\InprocServer32"))
            Check((string)key.GetValue("CodeBase") == "original-2025.dll", "SW2026 registration preserves SW2025 COM path");
        type.GetMethod("Unregister").Invoke(null, new object[] { type });
        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\SolidWorks\Addins\" + oldGuid))
            Check(key != null, "SW2026 unregistration preserves SW2025");
        SolidWorksVersionedAddinRegistration.RepairDiscoveryIsolation();
        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\SolidWorks\Addins\" + oldGuid))
            Check(key == null, "Repair removes globally discoverable 2025 entry");
        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\SolidWorks\SOLIDWORKS 2025\Addins\" + oldGuid))
            Check((string)key.GetValue("Title") == "untouched-2025", "Repair preserves 2025 discovery values in its own version");
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(oldStartup))
            Check(Convert.ToInt32(key.GetValue(null)) == 1, "Isolation migration preserves 2025 startup");
        Check(!SolidWorksVersionedAddinRegistration.NeedsIsolationRepair, "Repair is complete and idempotent");        Type api = Assembly.LoadFrom(Path.Combine(sw2026Folder, "SolidWorks.Interop.sldworks.dll")).GetType("SolidWorks.Interop.sldworks.ISldWorks");
        object wrongHost = new FakeSwProxy(api, "33.5.0", 2025).GetTransparentProxy();
        Check(!(bool)type.GetMethod("ConnectToSW").Invoke(Activator.CreateInstance(type), new object[] { wrongHost, 1 }), "SW2026 add-in refuses SW2025 before changing its UI");
    }
    private static void TestDeploymentDependencies()
    {
        var preserved = new SolidWorksVersionedAddinRegistration(null, 2025);
        string preservedFolder = Path.Combine(workspace, @"tests\artifacts\package-2025-" + Guid.NewGuid().ToString("N"));
        preserved.ExtractPackage(preservedFolder);
        string preservedDll = Path.Combine(preservedFolder, "SolidTweaks.SolidWorksAddin.v1_14.dll");
        Check(preserved.MatchesPackage(preservedDll), "SW2025 original package includes its own API 33 and TLB");
        Check(RunPackageProbe(preservedDll) == 0, "Original SW2025 component activates from its own complete package");
        var registration = new SolidWorksVersionedAddinRegistration(null, 2026);
        string folder = Path.Combine(workspace, @"tests\artifacts\package-" + Guid.NewGuid().ToString("N"));
        registration.ExtractPackage(folder);
        string dll = Path.Combine(folder, "SolidTweaks.SolidWorksAddin.SW2026.dll");
        Check(registration.MatchesPackage(dll), "Extracted SW2026 package includes verified API dependencies");
        Check(RunPackageProbe(dll) == 0, "Packaged SW2026 component loads without API preloading or a custom resolver");
        foreach (string dependency in new[] { "sldworks", "swconst", "swpublished" })
        {
            string path = Path.Combine(folder, "SolidWorks.Interop." + dependency + ".dll");
            byte[] original = File.ReadAllBytes(path);
            File.Delete(path);
            Check(!registration.MatchesPackage(dll), "Missing " + dependency + " marks the installation incomplete");
            if (dependency == "swpublished")
                Check(RunPackageProbe(dll) != 0, "Regression control reproduces activation failure when swpublished is missing");
            File.WriteAllBytes(path, new byte[] { 0 });
            Check(!registration.MatchesPackage(dll), "Damaged " + dependency + " marks the installation incomplete");
            File.WriteAllBytes(path, original);
        }
        string tlb = Path.ChangeExtension(dll, ".tlb");
        byte[] typeLibrary = File.ReadAllBytes(tlb);
        File.Delete(tlb);
        Check(!registration.MatchesPackage(dll), "Missing TLB marks native SOLIDWORKS package incomplete");
        File.WriteAllBytes(tlb, new byte[] { 0 });
        Check(!registration.MatchesPackage(dll), "Damaged TLB marks native SOLIDWORKS package incomplete");
        File.WriteAllBytes(tlb, typeLibrary);        Check(RunPackageProbe(dll) == 0, "Restored complete package loads in another clean process");
    }
    private static int RunPackageProbe(string dll)
    {
        using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.Combine(workspace, @"tests\probe-bin\PackageLoadProbe.exe"),
            Arguments = "\"" + dll + "\"", UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        }))
        {
            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode == 0) Console.Write(output);
            else Console.WriteLine("Expected missing-dependency probe exit: " + process.ExitCode + "; " + errors.Split('\n')[0]);
            return process.ExitCode;
        }
    }
    private static void TestSessionRevisionGuard()
    {
        Type api = Assembly.LoadFrom(Path.Combine(sw2025Folder, "SolidWorks.Interop.sldworks.dll")).GetType("SolidWorks.Interop.sldworks.ISldWorks");
        using (var service = new SolidWorksService(new SolidWorksPreferenceResolver()))
        {
            FieldInfo connection = service.GetType().GetField("application", AnyInstance);
            SolidWorksTarget.Year = 2026;
            connection.SetValue(service, new FakeSwProxy(api, "34.4.1", 2026).GetTransparentProxy());
            Check(service.IsConnectionAlive() && service.ProcessId == 2026, "SW2026 accepts only its matching session/PID");
            SolidWorksTarget.Year = 2025;
            Check(!service.IsConnectionAlive() && !service.IsConnected, "Version switch discards a mismatched session");
            connection.SetValue(service, new FakeSwProxy(api, "33.5.0", 2025).GetTransparentProxy());
            Check(service.IsConnectionAlive() && service.ProcessId == 2025, "SW2025 connection remains supported");
        }
    }
    private static void TestForm()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        SolidWorksTarget.Year = 2025;
        UiLanguageSelector.SetCurrent(UiLanguage.Portuguese);
        using (var form = new MainForm(new SolidWorksService(new SolidWorksPreferenceResolver(), year => null)))
        {
            form.ShowInTaskbar = false;
            form.Opacity = 0;
            form.Show();
            Application.DoEvents();
            var selector = (ComboBox)form.GetType().GetField("versionSelector", AnyInstance).GetValue(form);
            var stepBox = (CheckBox)form.GetType().GetField("exportStepCheckBox", AnyInstance).GetValue(form);
            var dxfBox = (CheckBox)form.GetType().GetField("exportSheetMetalDxfCheckBox", AnyInstance).GetValue(form);
            Check(selector.Items.Count == 2 && stepBox.Checked && !dxfBox.Checked, "Initial UI shows existing SW2025 preferences");
            int height = form.Height;
            selector.SelectedItem = selector.Items.Cast<SolidWorksInstallation>().Single(item => item.Year == 2026);
            Application.DoEvents();
            Check(!stepBox.Checked && dxfBox.Checked && form.Height == height, "Switch to SW2026 rereads independent preferences without resizing");
            var content = (FlowLayoutPanel)form.GetType().GetField("contentPanel", AnyInstance).GetValue(form);
            Check(content.Padding.Bottom >= 16, "Bottom margin is preserved");
            SaveForm(form, "sw2026-compact.png");
            var tweaks = (Button)form.GetType().GetField("tweakOptionsButton", AnyInstance).GetValue(form);
            var visual = (Button)form.GetType().GetField("visualOptionsButton", AnyInstance).GetValue(form);
            tweaks.PerformClick(); visual.PerformClick(); Application.DoEvents();
            Check(form.Height == height && content.VerticalScroll.Visible, "Expanding options scrolls without changing form size");
            SaveForm(form, "sw2026-expanded.png");
            tweaks.PerformClick(); visual.PerformClick(); Application.DoEvents();
            Check(form.Height == height && !content.VerticalScroll.Visible, "Collapsing options removes unnecessary scroll");
            selector.SelectedItem = selector.Items.Cast<SolidWorksInstallation>().Single(item => item.Year == 2025);
            Application.DoEvents();
            Check(stepBox.Checked && !dxfBox.Checked && SolidTweaksFeatureSettings.Read(Step, false), "Switching back preserves SW2025 selections");
            SaveForm(form, "sw2025-compact.png");
            form.Close();
        }
    }
    private static void SaveForm(Form form, string name)
    {
        string dir = Path.Combine(workspace, "tests", "artifacts"); Directory.CreateDirectory(dir);
        using (var bitmap = new Bitmap(form.Width, form.Height))
        { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(dir, name), System.Drawing.Imaging.ImageFormat.Png); }
    }

    private sealed class FakeSwProxy : RealProxy
    {
        private readonly string revision; private readonly int pid;
        public FakeSwProxy(Type type, string revision, int pid) : base(type) { this.revision = revision; this.pid = pid; }
        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            object result = call.MethodName == "RevisionNumber" ? (object)revision : call.MethodName == "GetProcessID" ? pid : (object)null;
            return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
        }
    }
    private sealed class FakeEnvironment : IAddinFeatureEnvironment
    {
        private readonly Dictionary<string, bool> choices = new Dictionary<string, bool>();
        public readonly HashSet<string> Present = new HashSet<string>();
        public bool Startup, Loaded, FailSynchronization; public int Loads;
        public bool IsCurrent { get; set; }
        public bool IsRegistered { get; set; }
        public bool IsTargetRunning { get; set; } = true;
        public bool IsConnected { get; set; } = true;
        public bool Read(string feature) { bool value; return choices.TryGetValue(feature, out value) && value; }
        public void Write(string feature, bool enabled) { choices[feature] = enabled; }
        public bool AnyEnabled { get { return choices.Values.Any(value => value); } }
        public void Install() { IsRegistered = IsCurrent = true; }
        public void SetStartup(bool enabled) { Startup = enabled; }
        public void Synchronize()
        {
            if (FailSynchronization) throw new InvalidOperationException("Simulated host failure");
            if (!Loaded) { Loaded = true; Loads++; }
            Present.Clear(); foreach (var pair in choices) if (pair.Value) Present.Add(pair.Key);
        }
        public void RemoveInterface() { Present.Clear(); }
        public bool IsFeaturePresent(string feature) { return Present.Contains(feature); }
        public void Restart() { Loaded = false; Present.Clear(); IsTargetRunning = IsConnected = true; if (Startup) Synchronize(); }
    }
    private sealed class IsolatedRegistry : IDisposable
    {
        [DllImport("advapi32.dll")] private static extern int RegOverridePredefKey(IntPtr key, IntPtr replacement);
        private readonly string path = @"Software\SolidTweaks.AutomatedTests\" + Guid.NewGuid().ToString("N");
        private readonly List<RegistryKey> keys = new List<RegistryKey>();
        private readonly IntPtr[] roots = { new IntPtr(unchecked((int)0x80000001)), new IntPtr(unchecked((int)0x80000002)), new IntPtr(unchecked((int)0x80000000)) };
        public IsolatedRegistry()
        {
            using (RegistryKey root = Registry.CurrentUser.CreateSubKey(path))
            { keys.Add(root.CreateSubKey("User")); keys.Add(root.CreateSubKey("Machine")); keys.Add(root.CreateSubKey("Classes")); }
            for (int i = 0; i < roots.Length; i++)
            {
                int error = RegOverridePredefKey(roots[i], keys[i].Handle.DangerousGetHandle());
                if (error != 0) { Dispose(); throw new System.ComponentModel.Win32Exception(error); }
            }
        }
        public void Dispose()
        {
            foreach (IntPtr root in roots) RegOverridePredefKey(root, IntPtr.Zero);
            foreach (RegistryKey key in keys) key.Dispose();
            Registry.CurrentUser.DeleteSubKeyTree(path, false);
        }
        public void RestoreMachineAndClasses()
        {
            RegOverridePredefKey(roots[1], IntPtr.Zero);
            RegOverridePredefKey(roots[2], IntPtr.Zero);
        }
    }
}
