using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidTweaks.Shared;
using Environment = System.Environment;

namespace SolidTweaks.SolidWorksAddin
{
    internal sealed class SheetMetalDxfExportResult
    {
        public SheetMetalDxfExportResult()
        {
            ExportedFiles = new List<string>();
            Warnings = new List<string>();
        }

        public IList<string> ExportedFiles { get; private set; }
        public IList<string> Warnings { get; private set; }
    }

    internal sealed class SheetMetalDxfExportService
    {
        private const int ExportFlatPatternGeometry = 1;
        private const int ExportBendLines = 4;
        private readonly ISldWorks application;
        private readonly Action<string> reportProgress;

        public SheetMetalDxfExportService(ISldWorks application)
            : this(application, null)
        {
        }

        internal SheetMetalDxfExportService(
            ISldWorks application,
            Action<string> reportProgress)
        {
            this.application = application;
            this.reportProgress = reportProgress;
        }

        public void ExportActiveDocument()
        {
            try
            {
                bool hasSelection = HasSelection();
                bool selectedOnly;
                bool includeBendLines;
                if (!SheetMetalDxfExportDialog.ShowExport(
                    System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle,
                    hasSelection,
                    out selectedOnly,
                    out includeBendLines))
                {
                    return;
                }

                SheetMetalDxfExportResult result = Export(selectedOnly, includeBendLines);
                if (result.ExportedFiles.Count == 0)
                {
                    ShowMessage(
                        result.Warnings.Count == 0
                            ? UiText.Get("No sheet-metal body was found.")
                            : string.Join(Environment.NewLine, result.Warnings),
                        swMessageBoxIcon_e.swMbWarning);
                    return;
                }

                SetStatus(ExportSummary(result.ExportedFiles.Count, result.Warnings.Count > 0));
                if (result.Warnings.Count > 0)
                {
                    ShowMessage(
                        ExportSummary(result.ExportedFiles.Count, true) + Environment.NewLine + Environment.NewLine +
                        string.Join(Environment.NewLine, result.Warnings),
                        swMessageBoxIcon_e.swMbWarning);
                }
            }
            catch (Exception exception)
            {
                ShowMessage(UiText.Format("Could not export the DXF files.\n\n{0}", FriendlyMessage(exception)), swMessageBoxIcon_e.swMbStop);
            }
        }

        public bool HasSelection()
        {
            IModelDoc2 model = GetSupportedActiveDocument();
            ISelectionMgr selectionManager = (ISelectionMgr)model.SelectionManager;
            return selectionManager.GetSelectedObjectCount2(-1) > 0;
        }

        public SheetMetalDxfExportResult Export(bool selectedOnly, bool includeBendLines)
        {
            IModelDoc2 originalModel = GetSupportedActiveDocument();
            string originalTitle = originalModel.GetTitle();
            object[] originalSelection = CaptureSelection(originalModel);
            SelectionScope selection = CaptureSelectionScope(originalModel);
            var result = new SheetMetalDxfExportResult();
            IList<PartContext> parts = CollectPartContexts(originalModel, selectedOnly, selection, result);
            if (parts.Count == 0)
            {
                string message = selectedOnly
                    ? "The selection contains no parts or sheet-metal bodies that can be exported."
                    : "No valid part was found in the open document.";
                throw new UserFacingException(message, UiText.Get(message));
            }

            try
            {
                foreach (PartContext context in parts)
                {
                    ExportPart(application, context, selectedOnly, selection, includeBendLines, result);
                }
            }
            finally
            {
                RestorePartConfigurations(parts);
                ActivateDocument(application, originalTitle);
                RestoreSelection(originalModel, originalSelection);
            }

            if (result.ExportedFiles.Count == 0 && result.Warnings.Count == 0)
            {
                throw new UserFacingException(
                    "No sheet-metal body was found for export.",
                    UiText.Get("No sheet-metal body was found for export."));
            }
            return result;
        }

        private void ExportPart(
            ISldWorks application,
            PartContext context,
            bool selectedOnly,
            SelectionScope selection,
            bool includeBendLines,
            SheetMetalDxfExportResult result)
        {
            IModelDoc2 model = context.Model;
            string path = model.GetPathName();
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            {
                result.Warnings.Add(UiText.Format("Save part {0} before exporting the DXF file.", model.GetTitle()));
                return;
            }

            if (!string.IsNullOrWhiteSpace(context.Configuration) &&
                !string.Equals(context.OriginalConfiguration, context.Configuration, StringComparison.OrdinalIgnoreCase))
            {
                if (!model.ShowConfiguration2(context.Configuration))
                {
                    result.Warnings.Add(UiText.Format(
                        "Could not activate configuration {0} in {1}.", context.Configuration, model.GetTitle()));
                    return;
                }
                model.ForceRebuild3(false);
            }

            if (!ActivateDocument(application, model.GetTitle()))
            {
                result.Warnings.Add(UiText.Format("Could not activate part {0}.", model.GetTitle()));
                return;
            }

            IPartDoc part = (IPartDoc)model;
            IList<IBody2> allSheetMetalBodies = GetSheetMetalBodies(part);
            if (allSheetMetalBodies.Count == 0)
            {
                return;
            }

            IList<IBody2> targetBodies = FilterBodies(path, allSheetMetalBodies, selectedOnly, selection);
            if (targetBodies.Count == 0)
            {
                return;
            }

            IDictionary<string, IFeature> flatPatterns = GetFlatPatternsByBodyName(
                application,
                model,
                allSheetMetalBodies);
            string folder = Path.GetDirectoryName(path);
            string fileName = Path.GetFileNameWithoutExtension(path);
            bool isMultiBody = allSheetMetalBodies.Count > 1;
            int options = ExportFlatPatternGeometry | (includeBendLines ? ExportBendLines : 0);

            foreach (IBody2 body in targetBodies)
            {
                string bodyName = body.Name;
                IFeature flatPattern;
                if (string.IsNullOrWhiteSpace(bodyName) || !flatPatterns.TryGetValue(bodyName, out flatPattern))
                {
                    result.Warnings.Add(UiText.Format(
                        "The flat pattern for body {0} was not found in {1}.",
                        bodyName ?? UiText.Get("unnamed"), model.GetTitle()));
                    continue;
                }

                double thicknessMillimeters;
                if (!TryGetSheetMetalThicknessMillimeters(
                    model,
                    body,
                    !isMultiBody,
                    out thicknessMillimeters))
                {
                    result.Warnings.Add(UiText.Format(
                        "The thickness of body {0} was not found in {1}.",
                        bodyName ?? UiText.Get("unnamed"), model.GetTitle()));
                    continue;
                }

                string originalOutputName = isMultiBody
                    ? fileName + "(" + SanitizeFileName(bodyName) + ").dxf"
                    : fileName + ".dxf";
                string outputName = FormatThickness(thicknessMillimeters) + " - " + originalOutputName;
                string outputPath = Path.Combine(folder, outputName);
                Report(UiText.Format("Exporting {0}...", outputName));

                model.ClearSelection2(true);
                if (!flatPattern.Select2(false, 0))
                {
                    result.Warnings.Add(UiText.Format("Could not select the flat pattern for {0}.", bodyName));
                    continue;
                }

                bool exported = part.ExportToDWG2(
                    outputPath,
                    path,
                    (int)swExportToDWG_e.swExportToDWG_ExportSheetMetal,
                    true,
                    new double[12],
                    false,
                    false,
                    options,
                    null);
                if (!exported || !File.Exists(outputPath))
                {
                    result.Warnings.Add(UiText.Format("SOLIDWORKS could not export {0}.", outputName));
                    continue;
                }
                result.ExportedFiles.Add(outputPath);
            }
            model.ClearSelection2(true);
        }

        private static bool TryGetSheetMetalThicknessMillimeters(
            IModelDoc2 model,
            IBody2 body,
            bool allowModelFallback,
            out double thicknessMillimeters)
        {
            Array bodyFeatures = body.GetFeatures() as Array;
            if (TryGetThicknessFromFeatures(bodyFeatures, out thicknessMillimeters))
            {
                return true;
            }

            // IBody2.GetFeatures identifica a feature de chapa de cada corpo em peças
            // multicorpos. Para uma peça de corpo único, usa também o template de chapa
            // recomendado pela API para modelos criados no SOLIDWORKS 2013 ou mais recente.
            if (allowModelFallback)
            {
                ISheetMetalFeatureData template =
                    model.Extension.GetTemplateSheetMetal() as ISheetMetalFeatureData;
                if (TryGetThicknessFromSheetMetalData(template, out thicknessMillimeters))
                {
                    return true;
                }
            }

            thicknessMillimeters = 0.0;
            return false;
        }

        private static bool TryGetThicknessFromFeatures(
            Array features,
            out double thicknessMillimeters)
        {
            if (features != null)
            {
                foreach (object item in features)
                {
                    IFeature feature = item as IFeature;
                    if (feature != null &&
                        TryGetThicknessFromFeature(feature, out thicknessMillimeters))
                    {
                        return true;
                    }
                }
            }

            thicknessMillimeters = 0.0;
            return false;
        }

        private static bool TryGetThicknessFromFeature(
            IFeature feature,
            out double thicknessMillimeters)
        {
            ISheetMetalFeatureData sheetMetal = string.Equals(
                feature.GetTypeName2(),
                "SheetMetal",
                StringComparison.OrdinalIgnoreCase)
                ? feature.GetDefinition() as ISheetMetalFeatureData
                : null;
            return TryGetThicknessFromSheetMetalData(sheetMetal, out thicknessMillimeters);
        }

        private static bool TryGetThicknessFromSheetMetalData(
            ISheetMetalFeatureData sheetMetal,
            out double thicknessMillimeters)
        {
            if (sheetMetal != null)
            {
                double thicknessMeters = sheetMetal.Thickness;
                if (thicknessMeters > 0.0 &&
                    !double.IsNaN(thicknessMeters) &&
                    !double.IsInfinity(thicknessMeters))
                {
                    thicknessMillimeters = thicknessMeters * 1000.0;
                    return true;
                }
            }

            thicknessMillimeters = 0.0;
            return false;
        }

        private static string FormatThickness(double thicknessMillimeters)
        {
            return thicknessMillimeters.ToString(
                "0.0##",
                CultureInfo.GetCultureInfo("pt-BR")) + "mm";
        }

        private IList<PartContext> CollectPartContexts(
            IModelDoc2 originalModel,
            bool selectedOnly,
            SelectionScope selection,
            SheetMetalDxfExportResult result)
        {
            var contexts = new Dictionary<string, PartContext>(StringComparer.OrdinalIgnoreCase);
            if (originalModel.GetType() == (int)swDocumentTypes_e.swDocPART)
            {
                AddPartContext(contexts, originalModel, GetActiveConfigurationName(originalModel));
                return new List<PartContext>(contexts.Values);
            }

            IAssemblyDoc assembly = (IAssemblyDoc)originalModel;
            assembly.ResolveAllLightWeightComponents(false);
            Array componentArray = assembly.GetComponents(false) as Array;
            if (componentArray == null)
            {
                return new List<PartContext>();
            }

            foreach (object item in componentArray)
            {
                IComponent2 component = item as IComponent2;
                if (component == null || component.IsSuppressed())
                {
                    continue;
                }

                string path = component.GetPathName();
                if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".sldprt", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (selectedOnly && !selection.ContainsPart(path))
                {
                    continue;
                }

                IModelDoc2 partModel = component.GetModelDoc2() as IModelDoc2;
                if (partModel == null)
                {
                    result.Warnings.Add(UiText.Format("Part {0} could not be loaded by the assembly.", Path.GetFileName(path)));
                    continue;
                }
                AddPartContext(contexts, partModel, component.ReferencedConfiguration);
            }
            return new List<PartContext>(contexts.Values);
        }

        private static void AddPartContext(
            IDictionary<string, PartContext> contexts,
            IModelDoc2 model,
            string configuration)
        {
            string path = model.GetPathName();
            string key = string.IsNullOrWhiteSpace(path) ? model.GetTitle() : path;
            if (!contexts.ContainsKey(key))
            {
                contexts.Add(
                    key,
                    new PartContext
                    {
                        Model = model,
                        Configuration = configuration,
                        OriginalConfiguration = GetActiveConfigurationName(model)
                    });
            }
        }

        private static IList<IBody2> GetSheetMetalBodies(IPartDoc part)
        {
            var result = new List<IBody2>();
            Array bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, false) as Array;
            if (bodies == null)
            {
                return result;
            }
            foreach (object item in bodies)
            {
                IBody2 body = item as IBody2;
                if (body != null && body.IsSheetMetal())
                {
                    result.Add(body);
                }
            }
            return result;
        }

        private static IList<IBody2> FilterBodies(
            string partPath,
            IList<IBody2> bodies,
            bool selectedOnly,
            SelectionScope selection)
        {
            if (!selectedOnly || selection.IncludesAllBodies(partPath))
            {
                return new List<IBody2>(bodies);
            }

            var result = new List<IBody2>();
            foreach (IBody2 body in bodies)
            {
                if (selection.ContainsBody(partPath, body.Name))
                {
                    result.Add(body);
                }
            }
            return result;
        }

        private static IDictionary<string, IFeature> GetFlatPatternsByBodyName(
            ISldWorks application,
            IModelDoc2 model,
            IList<IBody2> sheetMetalBodies)
        {
            var result = new Dictionary<string, IFeature>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var flatPatterns = new List<FlatPatternContext>();
            IFeature feature = model.FirstFeature() as IFeature;
            while (feature != null)
            {
                VisitFeature(model, feature, null, result, flatPatterns, visited);
                feature = feature.GetNextFeature() as IFeature;
            }

            foreach (IBody2 body in sheetMetalBodies)
            {
                if (body == null || string.IsNullOrWhiteSpace(body.Name) || result.ContainsKey(body.Name))
                {
                    continue;
                }

                foreach (FlatPatternContext flatPattern in flatPatterns)
                {
                    if (flatPattern.FixedFaceBody != null &&
                        application.IsSame(body, flatPattern.FixedFaceBody) ==
                            (int)swObjectEquality.swObjectSame)
                    {
                        result.Add(body.Name, flatPattern.Feature);
                        break;
                    }
                }
            }

            if (sheetMetalBodies.Count == 1 && result.Count == 0 && flatPatterns.Count == 1)
            {
                result.Add(sheetMetalBodies[0].Name, flatPatterns[0].Feature);
            }
            return result;
        }

        private static void VisitFeature(
            IModelDoc2 model,
            IFeature feature,
            IBody2 cutListBody,
            IDictionary<string, IFeature> result,
            IList<FlatPatternContext> flatPatterns,
            ISet<string> visited)
        {
            string identity = feature.Name + "|" + feature.GetTypeName2();
            if (!visited.Add(identity))
            {
                return;
            }

            string typeName = feature.GetTypeName2();
            IBody2 childCutListBody = cutListBody;
            if (string.Equals(typeName, "CutListFolder", StringComparison.OrdinalIgnoreCase))
            {
                childCutListBody = GetSingleSheetMetalBodyFromFolder(feature);
            }

            if (string.Equals(typeName, "FlatPattern", StringComparison.OrdinalIgnoreCase))
            {
                IBody2 fixedFaceBody = GetFlatPatternBody(model, feature);
                flatPatterns.Add(new FlatPatternContext
                {
                    Feature = feature,
                    FixedFaceBody = fixedFaceBody
                });
                if (childCutListBody != null &&
                    !string.IsNullOrWhiteSpace(childCutListBody.Name) &&
                    !result.ContainsKey(childCutListBody.Name))
                {
                    result.Add(childCutListBody.Name, feature);
                }
            }

            IFeature child = feature.GetFirstSubFeature() as IFeature;
            while (child != null)
            {
                VisitFeature(model, child, childCutListBody, result, flatPatterns, visited);
                child = child.GetNextSubFeature() as IFeature;
            }
        }

        private static IBody2 GetSingleSheetMetalBodyFromFolder(IFeature feature)
        {
            IBodyFolder bodyFolder = feature.GetSpecificFeature2() as IBodyFolder;
            if (bodyFolder == null)
            {
                return null;
            }

            Array bodies = bodyFolder.GetBodies() as Array;
            if (bodies == null || bodies.Length != 1)
            {
                return null;
            }

            IBody2 body = bodies.GetValue(0) as IBody2;
            return body != null && body.IsSheetMetal() ? body : null;
        }

        private static IBody2 GetFlatPatternBody(IModelDoc2 model, IFeature feature)
        {
            IBody2 body = feature.GetBody() as IBody2;
            if (body != null)
            {
                return body;
            }

            IFlatPatternFeatureData data = feature.GetDefinition() as IFlatPatternFeatureData;
            if (data == null || !data.AccessSelections(model, null))
            {
                return null;
            }
            try
            {
                return GetBodyFromEntity(data.FixedFace2);
            }
            finally
            {
                data.ReleaseSelectionAccess();
            }
        }

        private static SelectionScope CaptureSelectionScope(IModelDoc2 model)
        {
            var scope = new SelectionScope();
            ISelectionMgr selectionManager = (ISelectionMgr)model.SelectionManager;
            int count = selectionManager.GetSelectedObjectCount2(-1);
            string activePartPath = model.GetType() == (int)swDocumentTypes_e.swDocPART
                ? model.GetPathName()
                : null;

            for (int index = 1; index <= count; index++)
            {
                object selected = selectionManager.GetSelectedObject6(index, -1);
                IComponent2 selectedComponent = selected as IComponent2;
                if (selectedComponent != null)
                {
                    AddComponentAndChildren(scope, selectedComponent);
                    continue;
                }

                IComponent2 owner = selectionManager.GetSelectedObjectsComponent4(index, -1) as IComponent2;
                string partPath = owner == null ? activePartPath : owner.GetPathName();
                IBody2 body = GetBodyFromEntity(selected);
                if (!string.IsNullOrWhiteSpace(partPath) && body != null)
                {
                    scope.AddBody(partPath, body.Name);
                }
            }
            return scope;
        }

        private static void AddComponentAndChildren(SelectionScope scope, IComponent2 component)
        {
            if (component == null || component.IsSuppressed())
            {
                return;
            }
            string path = component.GetPathName();
            if (!string.IsNullOrWhiteSpace(path) && path.EndsWith(".sldprt", StringComparison.OrdinalIgnoreCase))
            {
                scope.AddAllBodies(path);
            }
            Array children = component.GetChildren() as Array;
            if (children == null)
            {
                return;
            }
            foreach (object child in children)
            {
                AddComponentAndChildren(scope, child as IComponent2);
            }
        }

        private static IBody2 GetBodyFromEntity(object entity)
        {
            IBody2 body = entity as IBody2;
            if (body != null)
            {
                return body;
            }
            IFace2 face = entity as IFace2;
            if (face != null)
            {
                return face.GetBody() as IBody2;
            }
            IEdge edge = entity as IEdge;
            if (edge != null)
            {
                return edge.GetBody() as IBody2;
            }
            IFeature feature = entity as IFeature;
            return feature == null ? null : feature.GetBody() as IBody2;
        }

        private IModelDoc2 GetSupportedActiveDocument()
        {
            IModelDoc2 model = application.ActiveDoc as IModelDoc2;
            if (model == null)
            {
                throw new UserFacingException(
                    "Open a part or assembly before exporting DXF files.",
                    UiText.Get("Open a part or assembly before exporting DXF files."));
            }
            int type = model.GetType();
            if (type != (int)swDocumentTypes_e.swDocPART && type != (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                throw new UserFacingException(
                    "DXF export is available only for parts and assemblies.",
                    UiText.Get("DXF export is available only for parts and assemblies."));
            }
            return model;
        }

        private static object[] CaptureSelection(IModelDoc2 model)
        {
            ISelectionMgr manager = (ISelectionMgr)model.SelectionManager;
            int count = manager.GetSelectedObjectCount2(-1);
            var result = new List<object>(count);
            for (int index = 1; index <= count; index++)
            {
                object selected = manager.GetSelectedObject6(index, -1);
                if (selected != null)
                {
                    result.Add(selected);
                }
            }
            return result.ToArray();
        }

        private static void RestoreSelection(IModelDoc2 model, object[] selection)
        {
            model.ClearSelection2(true);
            if (selection.Length > 0)
            {
                model.Extension.MultiSelect2(selection, true, null);
            }
        }

        private static bool ActivateDocument(ISldWorks application, string title)
        {
            int errors = 0;
            return application.ActivateDoc3(title, false, 0, ref errors) != null && errors == 0;
        }

        private static void RestorePartConfigurations(IEnumerable<PartContext> parts)
        {
            foreach (PartContext context in parts)
            {
                if (!string.IsNullOrWhiteSpace(context.OriginalConfiguration) &&
                    !string.Equals(
                        GetActiveConfigurationName(context.Model),
                        context.OriginalConfiguration,
                        StringComparison.OrdinalIgnoreCase))
                {
                    context.Model.ShowConfiguration2(context.OriginalConfiguration);
                }
            }
        }

        private static string GetActiveConfigurationName(IModelDoc2 model)
        {
            IConfiguration configuration = model.ConfigurationManager.ActiveConfiguration;
            return configuration == null ? string.Empty : configuration.Name;
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            char[] characters = name.ToCharArray();
            for (int index = 0; index < characters.Length; index++)
            {
                if (invalid.Contains(characters[index]))
                {
                    characters[index] = '_';
                }
            }
            return new string(characters);
        }

        private void Report(string message)
        {
            if (reportProgress != null)
            {
                reportProgress(message);
            }
            else
            {
                SetStatus(message);
            }
        }

        private void SetStatus(string message)
        {
            IFrame frame = application.IFrameObject();
            if (frame != null)
            {
                frame.SetStatusBarText(message);
            }
        }

        private void ShowMessage(string message, swMessageBoxIcon_e icon)
        {
            application.SendMsgToUser2(message, (int)icon, (int)swMessageBoxBtn_e.swMbOk);
        }

        private static string FriendlyMessage(Exception exception)
        {
            return UiText.UserMessage(exception);
        }

        private static string ExportSummary(int count, bool hasWarnings)
        {
            string key = hasWarnings
                ? (count == 1 ? "{0} DXF file exported with warnings." : "{0} DXF files exported with warnings.")
                : (count == 1 ? "{0} DXF file exported successfully." : "{0} DXF files exported successfully.");
            return UiText.Format(key, count);
        }

        private sealed class PartContext
        {
            public IModelDoc2 Model { get; set; }
            public string Configuration { get; set; }
            public string OriginalConfiguration { get; set; }
        }

        private sealed class FlatPatternContext
        {
            public IFeature Feature { get; set; }
            public IBody2 FixedFaceBody { get; set; }
        }

        private sealed class SelectionScope
        {
            private readonly ISet<string> allBodies =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly IDictionary<string, ISet<string>> bodies =
                new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase);

            public void AddAllBodies(string path)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    allBodies.Add(path);
                }
            }

            public void AddBody(string path, string bodyName)
            {
                if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(bodyName))
                {
                    return;
                }
                ISet<string> names;
                if (!bodies.TryGetValue(path, out names))
                {
                    names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    bodies.Add(path, names);
                }
                names.Add(bodyName);
            }

            public bool ContainsPart(string path)
            {
                return IncludesAllBodies(path) || bodies.ContainsKey(path);
            }

            public bool IncludesAllBodies(string path)
            {
                return !string.IsNullOrWhiteSpace(path) && allBodies.Contains(path);
            }

            public bool ContainsBody(string path, string bodyName)
            {
                ISet<string> names;
                return !string.IsNullOrWhiteSpace(path) &&
                    !string.IsNullOrWhiteSpace(bodyName) &&
                    bodies.TryGetValue(path, out names) &&
                    names.Contains(bodyName);
            }
        }
    }
}
