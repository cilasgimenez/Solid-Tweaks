using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidTweaks.Infrastructure.SolidWorks;
using SolidTweaks.Shared;

// Deliberately uses the installed SOLIDWORKS sessions and the production lifecycle.
// All CAD documents created/closed by this harness must remain under tests/artifacts.
internal static class LiveValidation
{
    const string Step = SolidTweaksFeatureSettings.ExportStepFeatureId;
    const string Dxf = SolidTweaksFeatureSettings.ExportDxfFeatureId;
    static readonly string ArtifactRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "artifacts", "live-validation"));
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            int year = int.Parse(args[1]); SolidWorksTarget.Year = year;
            string action = args[0];
            using (var host = new SolidWorksService(new SolidWorksPreferenceResolver()))
            {
                var registration = new SolidWorksVersionedAddinRegistration(host, year);
                if (action == "update") { registration.Update(); Check(registration.IsCurrent, "complete package and registration"); return 0; }
                if (action == "set-offline") { int mask = int.Parse(args[2]); registration.Write(Step, (mask & 1) != 0); registration.Write(Dxf, (mask & 2) != 0); registration.SetStartup(mask != 0); return 0; }
                Check(host.TryConnect(), "connected to SW" + year);
                ISldWorks sw = host.GetApplication();
                Console.WriteLine("Revision=" + sw.RevisionNumber() + "; PID=" + sw.GetProcessID() + "; Documents=" + sw.GetDocumentCount());
                Check(sw.GetAddInObject(year == 2025 ? SolidTweaks2026AddinIdentity.CurrentGuid : SolidTweaksAddinIdentity.CurrentGuid) == null, "other version's component is absent");
                if (action == "set")
                {
                    int mask = int.Parse(args[2]);
                    registration.Change(Step, (mask & 1) != 0);
                    registration.Change(Dxf, (mask & 2) != 0);
                    Check(registration.IsFeaturePresent(Step) == ((mask & 1) != 0), "STEP matches mask " + mask);
                    Check(registration.IsFeaturePresent(Dxf) == ((mask & 2) != 0), "DXF matches mask " + mask);
                }
                else if (action == "cycle")
                {
                    IntPtr before = Marshal.GetIUnknownForObject(sw.GetAddInObject(registration.Guid));
                    bool step = registration.Read(Step), dxf = registration.Read(Dxf);
                    registration.Change(Step, false); registration.Change(Dxf, false);
                    Check(sw.GetAddInObject(registration.Guid) != null, "removing both retains dormant component");
                    Check(!registration.IsFeaturePresent(Step) && !registration.IsFeaturePresent(Dxf), "both buttons removed");
                    registration.Change(Step, true); registration.Change(Dxf, true);
                    Check(registration.IsFeaturePresent(Step) && registration.IsFeaturePresent(Dxf), "both restored in same session");
                    IntPtr after = Marshal.GetIUnknownForObject(sw.GetAddInObject(registration.Guid));
                    try { Check(before == after, "same COM instance retained"); }
                    finally { Marshal.Release(before); Marshal.Release(after); }
                    registration.Change(Step, step); registration.Change(Dxf, dxf);
                }
                else if (action == "create") CreatePart(sw, year, args[2]);
                else if (action == "open")
                {
                    string path = Path.Combine(ArtifactRoot, "SW" + year, args[2] + ".SLDPRT");
                    Check(File.Exists(path), "existing test fixture");
                    int errors = 0, warnings = 0;
                    Check(sw.OpenDoc6(path, 1, 1, "", ref errors, ref warnings) != null && errors == 0, "fixture reopened");
                }
                else if (action == "native-command")
                {
                    int command = int.Parse(args[2]);
                    Check(command == 1073 || command == 1137, "only native add-in/customization inspection commands allowed");
                    Check(sw.RunCommand(command, ""), "native dialog command accepted");
                }
                else if (action == "export-step" || action == "export-dxf")
                {
                    EnsureTestDocument((IModelDoc2)sw.ActiveDoc);
                    object addin = sw.GetAddInObject(registration.Guid);
                    addin.GetType().InvokeMember(action == "export-step" ? "ExportStep" : "ExportSheetMetalDxf", BindingFlags.InvokeMethod, null, addin, null);
                }
                else if (action == "verify-step")
                {
                    IModelDoc2 original = (IModelDoc2)sw.ActiveDoc; EnsureTestDocument(original);
                    double volume = Volume(original); string source = original.GetPathName();
                    string path = Path.ChangeExtension(source, ".STEP");
                    if (!File.Exists(path)) path = Path.ChangeExtension(source, ".step");
                    if (!File.Exists(path)) path = Directory.GetFiles(Path.GetDirectoryName(source), Path.GetFileNameWithoutExtension(source) + "_*.step").OrderByDescending(File.GetLastWriteTimeUtc).First();
                    Check(new FileInfo(path).Length > 1000, "nonempty STEP file " + path);
                    Console.WriteLine(string.Join(" ", File.ReadLines(path).Where(line => line.Contains("FILE_SCHEMA"))));
                    int errors = 0;
                    var imported = (IModelDoc2)sw.LoadFile4(path, "r", sw.GetImportFileData(path), ref errors);
                    Check(imported != null && errors == 0, "STEP reimport succeeds; error=" + errors);
                    Check(Math.Abs(Volume(imported) - volume) < 1e-10, "STEP round-trip preserves solid volume=" + volume);
                    sw.CloseDoc(imported.GetTitle());
                    sw.ActivateDoc3(source, false, 0, ref errors);
                }
                else if (action == "close-tests")
                {
                    object[] documents = sw.GetDocuments() as object[] ?? new object[0];
                    foreach (IModelDoc2 document in documents) { EnsureTestDocument(document); sw.CloseDoc(document.GetTitle()); }
                }
                else if (action == "exit")
                {
                    Check(sw.GetDocumentCount() == 0, "empty session before exit"); sw.ExitApp(); return 0;
                }
                Console.WriteLine("Startup=" + ReadStartup(registration.Guid) + "; Loaded=" + (sw.GetAddInObject(registration.Guid) != null) + "; STEP=" + registration.IsFeaturePresent(Step) + "; DXF=" + registration.IsFeaturePresent(Dxf));
                if (action == "status")
                {
                    Check(registration.IsFeaturePresent(Step) == registration.Read(Step), "STEP UI reflects persisted preference");
                    Check(registration.IsFeaturePresent(Dxf) == registration.Read(Dxf), "DXF UI reflects persisted preference");
                    if (args.Length > 2 && args[2] == "unloaded") Check(sw.GetAddInObject(registration.Guid) == null, "disabled component not loaded after restart");
                }
            }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static void Check(bool value, string description) { if (!value) throw new Exception("FAIL: " + description); Console.WriteLine("PASS: " + description); }
    static int ReadStartup(string guid) { using (var key = Registry.CurrentUser.OpenSubKey(@"Software\SolidWorks\AddInsStartup\" + guid)) return Convert.ToInt32(key.GetValue(null)); }
    static void EnsureTestDocument(IModelDoc2 document) { Check(document != null && document.GetPathName().StartsWith(ArtifactRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "document belongs to test artifact directory"); }
    static double Volume(IModelDoc2 model) { return ((IPartDoc)model).GetBodies2(0, false) is object[] bodies ? bodies.Cast<IBody2>().Sum(body => ((double[])body.GetMassProperties(1))[3]) : 0; }
    static void CreatePart(ISldWorks sw, int year, string kind)
    {
        string folder = Path.Combine(ArtifactRoot, "SW" + year); Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, kind + ".SLDPRT");
        Check(!File.Exists(path), "fixture does not overwrite an existing model");
        string template = sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
        Console.WriteLine("Template=" + template);
        var model = (IModelDoc2)sw.NewDocument(template, 0, 0, 0); Check(model != null, "new part created");
        if (kind.StartsWith("Sheet"))
        {
            IFeature plane = (IFeature)model.FirstFeature();
            while (plane != null && plane.GetTypeName2() != "RefPlane") plane = (IFeature)plane.GetNextFeature();
            Check(plane != null && plane.Select2(false, 0), "reference plane selected");
            model.SketchManager.InsertSketch(true);
            model.SketchManager.CreateCornerRectangle(0, 0, 0, 0.1, 0.05, 0);
            var feature = model.FeatureManager.InsertSheetMetalBaseFlange2(0.002, false, 0.004, 0.02, 0.01, false, 0, 0, 1, null, false, 2, 0.0001, 0.0001, 0.5, true, false, true, true);
            Check(feature != null, "sheet metal base flange created");
        }
        else
        {
            var body = ((IModeler)sw.GetModeler()).CreateBodyFromBox3(new double[] { 0, 0, 0, 0, 0, 1, 0.02, 0.03, 0.01 });
            Check(((IPartDoc)model).CreateFeatureFromBody3(body, false, 0) != null, "solid box created");
        }
        model.ClearSelection2(true); model.ViewZoomtofit2();
        int errors = 0, warnings = 0;
        Check(model.Extension.SaveAs3(path, 0, 1, null, null, ref errors, ref warnings) && errors == 0, "fixture saved: " + path);
        Check(Volume(model) > 0, "fixture contains solid volume=" + Volume(model));
    }
}
