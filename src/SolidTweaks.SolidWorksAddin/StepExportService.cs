using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidTweaks.Shared;

namespace SolidTweaks.SolidWorksAddin
{
    internal sealed class StepExportService
    {
        private readonly ISldWorks application;

        public StepExportService(ISldWorks application)
        {
            this.application = application;
        }

        public void ExportActiveDocument()
        {
            IModelDoc2 model = application.ActiveDoc as IModelDoc2;
            if (model == null)
            {
                ShowMessage(UiText.Get("Open a part or assembly before exporting."), swMessageBoxIcon_e.swMbWarning);
                return;
            }

            int documentType = model.GetType();
            if (documentType != (int)swDocumentTypes_e.swDocPART &&
                documentType != (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                ShowMessage(UiText.Get("STEP export is available only for parts and assemblies."), swMessageBoxIcon_e.swMbWarning);
                return;
            }

            string sourcePath = model.GetPathName();
            if (string.IsNullOrWhiteSpace(sourcePath) || !Path.IsPathRooted(sourcePath))
            {
                ShowMessage(
                    UiText.Get("Save the part or assembly before exporting the STEP file."),
                    swMessageBoxIcon_e.swMbInformation);
                return;
            }

            bool openFolderAfterExport;
            StepExportProtocol protocol;
            StepExportAction action = StepExportActionDialog.Show(
                Process.GetCurrentProcess().MainWindowHandle,
                out openFolderAfterExport,
                out protocol);
            if (action == StepExportAction.Cancel)
            {
                return;
            }

            try
            {
                string outputPath = BuildOutputPath(sourcePath);
                object[] previousSelection = CaptureSelection(model);
                int exportedItemCount;
                try
                {
                    exportedItemCount = SelectVisibleGeometry(model, documentType);
                    using (new StepPreferenceScope(application, protocol))
                    {
                        int errors = 0;
                        int warnings = 0;
                        bool saved = model.Extension.SaveAs3(
                            outputPath,
                            (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                            (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                            null,
                            null,
                            ref errors,
                            ref warnings);
                        if (!saved || errors != 0 || !File.Exists(outputPath))
                        {
                            throw new UserFacingException(
                                "SOLIDWORKS did not complete the export. Error code: " + errors + "; warnings: " + warnings + ".",
                                UiText.Format("SOLIDWORKS did not complete the export. Error code: {0}; warnings: {1}.", errors, warnings));
                        }
                    }
                }
                finally
                {
                    RestoreSelection(model, previousSelection);
                }

                IFrame frame = application.IFrameObject();
                if (frame != null)
                {
                    frame.SetStatusBarText(UiText.Format(
                        exportedItemCount == 1 ? "STEP exported ({0} item): {1}" : "STEP exported ({0} items): {1}",
                        exportedItemCount, outputPath));
                }

                if (action == StepExportAction.ExportAndOpen)
                {
                    OpenWithDefaultApplication(outputPath);
                }
                else if (openFolderAfterExport)
                {
                    OpenContainingFolder(outputPath);
                }
            }
            catch (Exception exception)
            {
                ShowMessage(UiText.Format("Could not export the STEP file.\n\n{0}", UiText.UserMessage(exception)), swMessageBoxIcon_e.swMbStop);
            }
        }

        private void OpenContainingFolder(string outputPath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.GetDirectoryName(outputPath),
                    UseShellExecute = true
                });
            }
            catch (Exception exception)
            {
                ShowMessage(
                    UiText.Format("The STEP file was exported, but the folder could not be opened.\n\n{0}", UiText.UserMessage(exception)),
                    swMessageBoxIcon_e.swMbWarning);
            }
        }

        private void OpenWithDefaultApplication(string outputPath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true
                });
            }
            catch (Exception exception)
            {
                ShowMessage(
                    UiText.Format("The STEP file was exported, but it could not be opened in the default application.\n\n{0}", UiText.UserMessage(exception)),
                    swMessageBoxIcon_e.swMbWarning);
            }
        }

        private int SelectVisibleGeometry(IModelDoc2 model, int documentType)
        {
            model.ClearSelection2(true);
            var geometry = new List<object>();
            if (documentType == (int)swDocumentTypes_e.swDocPART)
            {
                IPartDoc part = (IPartDoc)model;
                AddBodies(geometry, part.GetBodies2((int)swBodyType_e.swSolidBody, true));
                AddBodies(geometry, part.GetBodies2((int)swBodyType_e.swSheetBody, true));
            }
            else
            {
                IAssemblyDoc assembly = (IAssemblyDoc)model;
                assembly.ResolveAllLightWeightComponents(false);
                Array components = assembly.GetComponents(false) as Array;
                if (components != null)
                {
                    foreach (object item in components)
                    {
                        IComponent2 component = item as IComponent2;
                        if (component != null && IsVisibleLeafComponent(component))
                        {
                            geometry.Add(component);
                        }
                    }
                }
            }

            if (geometry.Count == 0)
            {
                throw new UserFacingException(
                    "No visible body or component was found for export.",
                    UiText.Get("No visible body or component was found for export."));
            }

            int selected = 0;
            foreach (object item in geometry)
            {
                IBody2 body = item as IBody2;
                if (body != null && body.Select2(true, null))
                {
                    selected++;
                    continue;
                }

                IComponent2 component = item as IComponent2;
                if (component != null && component.Select4(true, null, false))
                {
                    selected++;
                }
            }

            if (selected != geometry.Count)
            {
                throw new UserFacingException(
                    "Not all visible geometry could be selected for export.",
                    UiText.Get("Not all visible geometry could be selected for export."));
            }
            return geometry.Count;
        }

        private static bool IsVisibleLeafComponent(IComponent2 component)
        {
            IComponent2 current = component;
            while (current != null)
            {
                if (current.IsSuppressed() ||
                    current.Visible != (int)swComponentVisibilityState_e.swComponentVisible)
                {
                    return false;
                }
                current = current.GetParent();
            }

            Array children = component.GetChildren() as Array;
            return children == null || children.Length == 0;
        }

        private static void AddBodies(ICollection<object> target, object bodiesObject)
        {
            Array bodies = bodiesObject as Array;
            if (bodies == null)
            {
                return;
            }
            foreach (object body in bodies)
            {
                if (body != null)
                {
                    target.Add(body);
                }
            }
        }

        private static object[] CaptureSelection(IModelDoc2 model)
        {
            ISelectionMgr selectionManager = (ISelectionMgr)model.SelectionManager;
            int count = selectionManager.GetSelectedObjectCount2(-1);
            var selected = new List<object>(count);
            for (int index = 1; index <= count; index++)
            {
                object item = selectionManager.GetSelectedObject6(index, -1);
                if (item != null)
                {
                    selected.Add(item);
                }
            }
            return selected.ToArray();
        }

        private static void RestoreSelection(IModelDoc2 model, object[] selected)
        {
            model.ClearSelection2(true);
            if (selected.Length > 0)
            {
                model.Extension.MultiSelect2(selected, true, null);
            }
        }

        private static string BuildOutputPath(string sourcePath)
        {
            string folder = Path.GetDirectoryName(sourcePath);
            string name = Path.GetFileNameWithoutExtension(sourcePath);
            return Path.Combine(folder, name + "_" + DateTime.Now.ToString("HHmmss") + ".STEP");
        }

        private void ShowMessage(string message, swMessageBoxIcon_e icon)
        {
            application.SendMsgToUser2(message, (int)icon, (int)swMessageBoxBtn_e.swMbOk);
        }

        private sealed class StepPreferenceScope : IDisposable
        {
            private readonly ISldWorks application;
            private readonly int protocol;
            private readonly int geometryPreference;
            private readonly bool configurationData;
            private readonly bool faceEdgeProperties;
            private readonly bool appearances;
            private readonly bool splitPeriodicFaces;
            private readonly bool curveFeatures;
            private readonly bool atomicSave;
            private readonly string coordinateSystem;

            public StepPreferenceScope(ISldWorks application, StepExportProtocol selectedProtocol)
            {
                this.application = application;
                protocol = GetInteger(swUserPreferenceIntegerValue_e.swStepAP);
                geometryPreference = GetInteger(swUserPreferenceIntegerValue_e.swStepExportPreference);
                configurationData = GetToggle(swUserPreferenceToggle_e.swStepExportConfigurationData);
                faceEdgeProperties = GetToggle(swUserPreferenceToggle_e.swStepExportFaceEdgeProps);
                appearances = GetToggle(swUserPreferenceToggle_e.swStepExportAppearances);
                splitPeriodicFaces = GetToggle(swUserPreferenceToggle_e.swStepExportSplitPeriodic);
                curveFeatures = GetToggle(swUserPreferenceToggle_e.swStepExport3DCurveFeatures);
                atomicSave = GetToggle(swUserPreferenceToggle_e.swStepExportAtomicSave);
                coordinateSystem = application.GetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swExportOutputCoordinateSystem);

                SetInteger(swUserPreferenceIntegerValue_e.swStepAP, (int)selectedProtocol);
                SetInteger(
                    swUserPreferenceIntegerValue_e.swStepExportPreference,
                    (int)swAcisOutputGeometryPreference_e.swAcisOutputAsSolidAndSurface);
                SetToggle(swUserPreferenceToggle_e.swStepExportConfigurationData, false);
                SetToggle(swUserPreferenceToggle_e.swStepExportFaceEdgeProps, true);
                SetToggle(swUserPreferenceToggle_e.swStepExportAppearances, true);
                SetToggle(swUserPreferenceToggle_e.swStepExportSplitPeriodic, true);
                SetToggle(swUserPreferenceToggle_e.swStepExport3DCurveFeatures, false);
                SetToggle(swUserPreferenceToggle_e.swStepExportAtomicSave, true);
                application.SetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swExportOutputCoordinateSystem,
                    string.Empty);
            }

            public void Dispose()
            {
                SetInteger(swUserPreferenceIntegerValue_e.swStepAP, protocol);
                SetInteger(swUserPreferenceIntegerValue_e.swStepExportPreference, geometryPreference);
                SetToggle(swUserPreferenceToggle_e.swStepExportConfigurationData, configurationData);
                SetToggle(swUserPreferenceToggle_e.swStepExportFaceEdgeProps, faceEdgeProperties);
                SetToggle(swUserPreferenceToggle_e.swStepExportAppearances, appearances);
                SetToggle(swUserPreferenceToggle_e.swStepExportSplitPeriodic, splitPeriodicFaces);
                SetToggle(swUserPreferenceToggle_e.swStepExport3DCurveFeatures, curveFeatures);
                SetToggle(swUserPreferenceToggle_e.swStepExportAtomicSave, atomicSave);
                application.SetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swExportOutputCoordinateSystem,
                    coordinateSystem ?? string.Empty);
            }

            private int GetInteger(swUserPreferenceIntegerValue_e preference)
            {
                return application.GetUserPreferenceIntegerValue((int)preference);
            }

            private bool GetToggle(swUserPreferenceToggle_e preference)
            {
                return application.GetUserPreferenceToggle((int)preference);
            }

            private void SetInteger(swUserPreferenceIntegerValue_e preference, int value)
            {
                if (!application.SetUserPreferenceIntegerValue((int)preference, value))
                {
                    throw new UserFacingException(
                        "A STEP preference could not be applied.",
                        UiText.Get("A STEP preference could not be applied."));
                }
            }

            private void SetToggle(swUserPreferenceToggle_e preference, bool value)
            {
                application.SetUserPreferenceToggle((int)preference, value);
            }
        }
    }
}
