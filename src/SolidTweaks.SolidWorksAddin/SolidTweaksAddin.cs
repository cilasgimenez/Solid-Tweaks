using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SolidTweaks.Infrastructure.Diagnostics;
using SolidTweaks.Shared;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using Environment = System.Environment;

namespace SolidTweaks.SolidWorksAddin
{
    [ComVisible(true)]
    [Guid(SolidTweaksAddinIdentity.CurrentGuidWithoutBraces)]
    [ProgId(SolidTweaksAddinIdentity.CurrentProgId)]
    [ClassInterface(ClassInterfaceType.AutoDispatch)]
    public sealed class SolidTweaksAddin : ISwAddin
    {
        public const string AddinGuid = SolidTweaksAddinIdentity.CurrentGuidWithoutBraces;
        public const string TabTitle = "Solid Tweaks";
        private const string LegacyTabTitle = "Exportar STEP";
        private const int CommandGroupId = 12001;
        private const int ExportStepCommandUserId = 12002;
        private const int ExportDxfCommandUserId = 12003;
        private static readonly string[] LegacyAddinGuids = SolidTweaksAddinIdentity.LegacyGuids;
        private static readonly int[] SupportedDocumentTypes =
        {
            (int)swDocumentTypes_e.swDocPART,
            (int)swDocumentTypes_e.swDocASSEMBLY
        };

        private ISldWorks application;
        private ICommandManager commandManager;
        private DSldWorksEvents_Event applicationEvents;
        private bool restoreFirstCommandTabPending;
        private int restoreFirstCommandTabAttempts;
        private DateTime nextRestoreFirstCommandTabAttemptUtc;
        private DateTime restoreFirstCommandTabDeadlineUtc;

        public bool ConnectToSW(object thisSw, int cookie)
        {
            try
            {
                application = (ISldWorks)thisSw;
                if (!application.SetAddinCallbackInfo2(0, this, cookie))
                {
                    throw new InvalidOperationException("SOLIDWORKS did not accept the add-in callbacks.");
                }
                commandManager = application.GetCommandManager(cookie);
                if (commandManager == null)
                {
                    throw new InvalidOperationException("CommandManager is not available.");
                }
                SubscribeApplicationEvents();
                CreateUserInterface();
                if (!IsUserInterfaceReady())
                {
                    throw new InvalidOperationException("The Solid Tweaks tab does not match the enabled features.");
                }
                ScheduleFirstCommandTabForActiveDocument();
                AppendLog("ConnectToSW completed. Solid Tweaks tab synchronized.");
                return true;
            }
            catch (Exception exception)
            {
                UnsubscribeApplicationEvents();
                AppendLog("ConnectToSW failed.", exception);
                return false;
            }
        }

        public bool DisconnectFromSW()
        {
            UnsubscribeApplicationEvents();
            restoreFirstCommandTabPending = false;
            commandManager = null;
            application = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return true;
        }

        private void SubscribeApplicationEvents()
        {
            applicationEvents = application as DSldWorksEvents_Event;
            if (applicationEvents == null)
            {
                throw new InvalidOperationException("SOLIDWORKS application events are not available.");
            }
            applicationEvents.FileOpenPostNotify += ApplicationOnFileOpenPostNotify;
            applicationEvents.FileNewNotify2 += ApplicationOnFileNewNotify2;
            applicationEvents.OnIdleNotify += ApplicationOnIdleNotify;
        }

        private void UnsubscribeApplicationEvents()
        {
            if (applicationEvents == null)
            {
                return;
            }
            try
            {
                applicationEvents.FileOpenPostNotify -= ApplicationOnFileOpenPostNotify;
                applicationEvents.FileNewNotify2 -= ApplicationOnFileNewNotify2;
                applicationEvents.OnIdleNotify -= ApplicationOnIdleNotify;
            }
            catch (Exception exception)
            {
                AppendLog("SOLIDWORKS events could not be fully disconnected.", exception);
            }
            finally
            {
                applicationEvents = null;
            }
        }

        private int ApplicationOnFileOpenPostNotify(string fileName)
        {
            ScheduleFirstCommandTabRestore();
            return 0;
        }

        private int ApplicationOnFileNewNotify2(object newDocument, int documentType, string templateName)
        {
            if (IsSupportedDocumentType(documentType))
            {
                ScheduleFirstCommandTabRestore();
            }
            return 0;
        }

        private int ApplicationOnIdleNotify()
        {
            if (!restoreFirstCommandTabPending || DateTime.UtcNow < nextRestoreFirstCommandTabAttemptUtc)
            {
                return 0;
            }

            restoreFirstCommandTabAttempts++;
            nextRestoreFirstCommandTabAttemptUtc = DateTime.UtcNow.AddMilliseconds(250);
            try
            {
                IModelDoc2 document = application == null ? null : application.ActiveDoc as IModelDoc2;
                if (document != null && !IsSupportedDocumentType(document.GetType()))
                {
                    restoreFirstCommandTabPending = false;
                    return 0;
                }
                if (document != null)
                {
                    document.Extension.ActiveCommandTabIndex = 0;
                    if (document.Extension.ActiveCommandTabIndex == 0)
                    {
                        restoreFirstCommandTabPending = false;
                        AppendLog("The first native CommandManager tab was restored for the opened document.");
                        return 0;
                    }
                }
            }
            catch (Exception exception)
            {
                AppendLog("The first CommandManager tab could not yet be restored.", exception);
            }

            if (restoreFirstCommandTabAttempts >= 20 || DateTime.UtcNow >= restoreFirstCommandTabDeadlineUtc)
            {
                restoreFirstCommandTabPending = false;
                AppendLog("The first CommandManager tab could not be restored within the expected time.");
            }
            return 0;
        }

        private void ScheduleFirstCommandTabForActiveDocument()
        {
            try
            {
                IModelDoc2 document = application == null ? null : application.ActiveDoc as IModelDoc2;
                if (document != null && IsSupportedDocumentType(document.GetType()))
                {
                    ScheduleFirstCommandTabRestore();
                }
            }
            catch (Exception exception)
            {
                AppendLog("The active document could not be inspected for its initial CommandManager tab.", exception);
            }
        }

        private void ScheduleFirstCommandTabRestore()
        {
            restoreFirstCommandTabPending = true;
            restoreFirstCommandTabAttempts = 0;
            nextRestoreFirstCommandTabAttemptUtc = DateTime.UtcNow;
            restoreFirstCommandTabDeadlineUtc = DateTime.UtcNow.AddSeconds(5);
        }

        private static bool IsSupportedDocumentType(int documentType)
        {
            return documentType == (int)swDocumentTypes_e.swDocPART ||
                documentType == (int)swDocumentTypes_e.swDocASSEMBLY;
        }

        public void ExportStep()
        {
            new StepExportService(application).ExportActiveDocument();
        }

        public int EnableExportStep()
        {
            return 1;
        }

        public void ExportSheetMetalDxf()
        {
            new SheetMetalDxfExportService(application).ExportActiveDocument();
        }

        public int EnableExportSheetMetalDxf()
        {
            return 1;
        }

        public void RemoveUserInterface()
        {
            if (commandManager == null)
            {
                return;
            }
            RemoveExistingTabs();
            commandManager.RemoveCommandGroup2(CommandGroupId, true);
        }

        public bool IsFeatureReady(string featureId)
        {
            return IsKnownFeature(featureId) &&
                SolidTweaksFeatureSettings.Read(featureId, false) &&
                IsUserInterfaceReady();
        }

        public bool IsFeaturePresentInUserInterface(string featureId)
        {
            if (!IsKnownFeature(featureId) || commandManager == null)
            {
                return false;
            }

            CommandDefinition requestedCommand = null;
            foreach (CommandDefinition command in GetAllCommands())
            {
                if (string.Equals(command.FeatureId, featureId, StringComparison.Ordinal))
                {
                    requestedCommand = command;
                    break;
                }
            }
            if (requestedCommand == null)
            {
                return false;
            }

            CommandGroup group = commandManager.GetCommandGroup(CommandGroupId);
            if (group == null)
            {
                return false;
            }
            int commandId = group.get_CommandID(requestedCommand.GroupIndex);
            if (commandId <= 0)
            {
                return false;
            }

            foreach (int documentType in SupportedDocumentTypes)
            {
                CommandTab tab = commandManager.GetCommandTab(documentType, TabTitle);
                if (tab == null || !TabContainsCommand(tab, commandId))
                {
                    return false;
                }
            }
            return true;
        }

        public bool IsUserInterfaceReady()
        {
            try
            {
                if (ValidateUserInterface())
                {
                    return true;
                }
                if (IsCommandGroupReady())
                {
                    SynchronizeCommandTabs();
                }
                else
                {
                    AppendLog("The command group is missing or incomplete; rebuilding the complete interface.");
                    CreateUserInterface();
                }
                return ValidateUserInterface();
            }
            catch (Exception exception)
            {
                AppendLog("Failed to validate or synchronize the Solid Tweaks tab.", exception);
                return false;
            }
        }

        private bool IsCommandGroupReady()
        {
            if (commandManager == null)
            {
                return false;
            }

            IList<CommandDefinition> allCommands = GetAllCommands();
            CommandGroup group = commandManager.GetCommandGroup(CommandGroupId);
            if (group == null || group.NumberOfGroupItems != allCommands.Count)
            {
                return false;
            }

            foreach (CommandDefinition command in allCommands)
            {
                if (group.get_CommandID(command.GroupIndex) <= 0)
                {
                    return false;
                }
            }
            return true;
        }

        private bool ValidateUserInterface()
        {
            if (commandManager == null)
            {
                return false;
            }
            IList<CommandDefinition> allCommands = GetAllCommands();
            IList<CommandDefinition> enabledCommands = GetEnabledCommands(allCommands);
            CommandGroup group = commandManager.GetCommandGroup(CommandGroupId);
            if (group == null || group.NumberOfGroupItems != allCommands.Count)
            {
                return false;
            }

            var commandIds = new int[enabledCommands.Count];
            for (int index = 0; index < commandIds.Length; index++)
            {
                commandIds[index] = group.get_CommandID(enabledCommands[index].GroupIndex);
                if (commandIds[index] <= 0)
                {
                    return false;
                }
            }

            foreach (int documentType in SupportedDocumentTypes)
            {
                CommandTab tab = commandManager.GetCommandTab(documentType, TabTitle);
                if ((enabledCommands.Count == 0 && tab != null) ||
                    (enabledCommands.Count > 0 && (tab == null || !TabContainsCommands(tab, commandIds))) ||
                    commandManager.GetCommandTab(documentType, LegacyTabTitle) != null)
                {
                    return false;
                }
            }
            return true;
        }

        [ComRegisterFunction]
        public static void Register(Type type)
        {
            string guid = "{" + type.GUID.ToString().ToUpperInvariant() + "}";
            foreach (string legacyGuid in LegacyAddinGuids)
            {
                Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\SOLIDWORKS\Addins\" + legacyGuid, false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\SOLIDWORKS\AddInsStartup\" + legacyGuid, false);
                Registry.ClassesRoot.DeleteSubKeyTree(@"CLSID\" + legacyGuid, false);
            }
            using (RegistryKey addin = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\SOLIDWORKS\Addins\" + guid))
            {
                if (addin == null)
                {
                    throw new InvalidOperationException("Could not register the add-in in SOLIDWORKS.");
                }
                addin.SetValue(null, 0, RegistryValueKind.DWord);
                addin.SetValue("Title", "Solid Tweaks");
                addin.SetValue("Description", UiText.Get("Solid Tweaks features in CommandManager"));
            }
            using (RegistryKey startup = Registry.CurrentUser.CreateSubKey(@"Software\SOLIDWORKS\AddInsStartup\" + guid))
            {
                if (startup != null)
                {
                    startup.SetValue(null, 1, RegistryValueKind.DWord);
                }
            }
        }

        [ComUnregisterFunction]
        public static void Unregister(Type type)
        {
            string guid = "{" + type.GUID.ToString().ToUpperInvariant() + "}";
            Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\SOLIDWORKS\Addins\" + guid, false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\SOLIDWORKS\AddInsStartup\" + guid, false);
        }

        private void CreateUserInterface()
        {
            RemoveExistingTabs();
            commandManager.RemoveCommandGroup2(CommandGroupId, true);
            IList<CommandDefinition> allCommands = GetAllCommands();

            int errors = 0;
            CommandGroup group = commandManager.CreateCommandGroup2(
                CommandGroupId,
                TabTitle,
                UiText.Get("Solid Tweaks features"),
                UiText.Get("Solid Tweaks features"),
                -1,
                true,
                errors);
            if (group == null)
            {
                throw new InvalidOperationException("The Solid Tweaks command group could not be created.");
            }

            string[] icons = CreateIconStrips(allCommands);
            group.IconList = icons;
            group.MainIconList = icons;
            for (int index = 0; index < allCommands.Count; index++)
            {
                CommandDefinition command = allCommands[index];
                int commandIndex = group.AddCommandItem2(
                    command.Title,
                    -1,
                    command.Tooltip,
                    command.Title,
                    index,
                    command.Callback,
                    command.EnableCallback,
                    command.UserId,
                    (int)swCommandItemType_e.swToolbarItem);
                if (commandIndex < 0)
                {
                    throw new InvalidOperationException("The " + command.Title + " command could not be registered.");
                }
                command.CommandIndex = commandIndex;
            }
            group.HasMenu = false;
            group.HasToolbar = true;
            if (!group.Activate())
            {
                throw new InvalidOperationException("The Solid Tweaks command group could not be activated.");
            }

            for (int index = 0; index < allCommands.Count; index++)
            {
                int commandId = group.get_CommandID(allCommands[index].CommandIndex);
                if (commandId <= 0)
                {
                    throw new InvalidOperationException("SOLIDWORKS returned an invalid ID for " + allCommands[index].Title + ".");
                }
            }
            SynchronizeCommandTabs();
        }

        private void SynchronizeCommandTabs()
        {
            RemoveExistingTabs();
            CommandGroup group = commandManager.GetCommandGroup(CommandGroupId);
            if (group == null)
            {
                throw new InvalidOperationException("The Solid Tweaks command group is not available.");
            }
            IList<CommandDefinition> enabledCommands = GetEnabledCommands(GetAllCommands());
            if (enabledCommands.Count == 0)
            {
                return;
            }

            var commandIds = new int[enabledCommands.Count];
            for (int index = 0; index < enabledCommands.Count; index++)
            {
                commandIds[index] = group.get_CommandID(enabledCommands[index].GroupIndex);
                if (commandIds[index] <= 0)
                {
                    throw new InvalidOperationException("The " + enabledCommands[index].Title + " command is not active.");
                }
            }
            foreach (int documentType in SupportedDocumentTypes)
            {
                CommandTab tab = commandManager.AddCommandTab(documentType, TabTitle);
                if (tab == null)
                {
                    throw new InvalidOperationException("The Solid Tweaks tab could not be created.");
                }
                CommandTabBox box = tab.AddCommandTabBox();
                var styles = new int[commandIds.Length];
                for (int index = 0; index < styles.Length; index++)
                {
                    styles[index] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;
                }
                bool added = box != null && box.AddCommands(commandIds, styles);
                if (!added || !TabBoxContainsCommands(box, commandIds))
                {
                    throw new InvalidOperationException("The buttons could not be confirmed on the Solid Tweaks tab.");
                }
                tab.Visible = true;
            }
        }

        private static IList<CommandDefinition> GetAllCommands()
        {
            var commands = new List<CommandDefinition>();
            commands.Add(new CommandDefinition
                {
                    FeatureId = SolidTweaksFeatureSettings.ExportStepFeatureId,
                    GroupIndex = 0,
                    Title = UiText.Get("Export STEP"),
                    Tooltip = UiText.Get("Export the active document to STEP"),
                    Callback = "ExportStep",
                    EnableCallback = "EnableExportStep",
                    UserId = ExportStepCommandUserId,
                    IconResource = "SolidTweaks.SolidWorksAddin.Exportar_Step.png",
                    IconFileKey = "STEP"
                });
            commands.Add(new CommandDefinition
                {
                    FeatureId = SolidTweaksFeatureSettings.ExportDxfFeatureId,
                    GroupIndex = 1,
                    Title = UiText.Get("Export Sheet Metal DXF"),
                    Tooltip = UiText.Get("Export sheet-metal flat patterns to DXF"),
                    Callback = "ExportSheetMetalDxf",
                    EnableCallback = "EnableExportSheetMetalDxf",
                    UserId = ExportDxfCommandUserId,
                    IconResource = "SolidTweaks.SolidWorksAddin.Exportar_DXF.png",
                    IconFileKey = "DXF"
                });
            return commands;
        }

        private static IList<CommandDefinition> GetEnabledCommands(IList<CommandDefinition> allCommands)
        {
            var commands = new List<CommandDefinition>();
            foreach (CommandDefinition command in allCommands)
            {
                if (SolidTweaksFeatureSettings.Read(command.FeatureId, false))
                {
                    commands.Add(command);
                }
            }
            return commands;
        }

        private void RemoveExistingTabs()
        {
            RemoveTabsWithTitle(TabTitle);
            RemoveTabsWithTitle(LegacyTabTitle);
        }

        private void RemoveTabsWithTitle(string title)
        {
            foreach (int documentType in SupportedDocumentTypes)
            {
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    CommandTab tab = commandManager.GetCommandTab(documentType, title);
                    if (tab == null)
                    {
                        break;
                    }
                    if (!commandManager.RemoveCommandTab(tab))
                    {
                        throw new InvalidOperationException("An old " + title + " tab could not be removed.");
                    }
                }
            }
        }

        private bool AnyTabExists(string title)
        {
            foreach (int documentType in SupportedDocumentTypes)
            {
                if (commandManager.GetCommandTab(documentType, title) != null)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TabContainsCommands(CommandTab tab, int[] expectedCommandIds)
        {
            Array boxes = tab.CommandTabBoxes() as Array;
            return boxes != null && boxes.Length == 1 &&
                TabBoxContainsCommands(boxes.GetValue(0) as CommandTabBox, expectedCommandIds);
        }

        private static bool TabContainsCommand(CommandTab tab, int commandId)
        {
            Array boxes = tab.CommandTabBoxes() as Array;
            if (boxes == null)
            {
                return false;
            }
            foreach (object item in boxes)
            {
                CommandTabBox box = item as CommandTabBox;
                if (box == null)
                {
                    continue;
                }
                object idsObject;
                object stylesObject;
                int count = box.GetCommands(out idsObject, out stylesObject);
                Array ids = count > 0 ? idsObject as Array : null;
                if (ids == null)
                {
                    continue;
                }
                foreach (object id in ids)
                {
                    if (Convert.ToInt32(id) == commandId)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool TabBoxContainsCommands(CommandTabBox box, int[] expectedCommandIds)
        {
            if (box == null)
            {
                return false;
            }
            object idsObject;
            object stylesObject;
            int count = box.GetCommands(out idsObject, out stylesObject);
            Array ids = idsObject as Array;
            Array styles = stylesObject as Array;
            if (count != expectedCommandIds.Length || ids == null || styles == null ||
                ids.Length != expectedCommandIds.Length || styles.Length != expectedCommandIds.Length)
            {
                return false;
            }
            for (int index = 0; index < expectedCommandIds.Length; index++)
            {
                if (Convert.ToInt32(ids.GetValue(index)) != expectedCommandIds[index] ||
                    Convert.ToInt32(styles.GetValue(index)) !=
                        (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsKnownFeature(string featureId)
        {
            return string.Equals(featureId, SolidTweaksFeatureSettings.ExportStepFeatureId, StringComparison.Ordinal) ||
                string.Equals(featureId, SolidTweaksFeatureSettings.ExportDxfFeatureId, StringComparison.Ordinal);
        }

        private static string[] CreateIconStrips(IList<CommandDefinition> commands)
        {
            int[] sizes = { 20, 32, 40, 64, 96, 128 };
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SolidTweaks",
                "CommandManagerIcons");
            Directory.CreateDirectory(folder);
            var sources = new List<Image>();
            try
            {
                foreach (CommandDefinition command in commands)
                {
                    using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(command.IconResource))
                    {
                        if (stream == null)
                        {
                            throw new InvalidOperationException("The icon for " + command.Title + " was not found.");
                        }
                        using (Image source = Image.FromStream(stream))
                        {
                            sources.Add(new Bitmap(source));
                        }
                    }
                }

                string key = string.Join("_", GetIconKeys(commands));
                var paths = new string[sizes.Length];
                for (int sizeIndex = 0; sizeIndex < sizes.Length; sizeIndex++)
                {
                    int size = sizes[sizeIndex];
                    string path = Path.Combine(folder, "SolidTweaks_" + key + "_" + size + ".png");
                    using (var bitmap = new Bitmap(size * commands.Count, size))
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        for (int commandIndex = 0; commandIndex < sources.Count; commandIndex++)
                        {
                            graphics.DrawImage(sources[commandIndex], commandIndex * size, 0, size, size);
                        }
                        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    paths[sizeIndex] = path;
                }
                return paths;
            }
            finally
            {
                foreach (Image source in sources)
                {
                    source.Dispose();
                }
            }
        }

        private static string[] GetIconKeys(IList<CommandDefinition> commands)
        {
            var keys = new string[commands.Count];
            for (int index = 0; index < commands.Count; index++)
            {
                keys[index] = commands[index].IconFileKey;
            }
            return keys;
        }

        private static void AppendLog(string message, Exception exception = null)
        {
            if (exception == null)
            {
                ApplicationLog.Info("[ADDIN] " + message);
            }
            else
            {
                ApplicationLog.Error("[ADDIN] " + message, exception);
            }
        }

        private sealed class CommandDefinition
        {
            public string FeatureId { get; set; }
            public int GroupIndex { get; set; }
            public string Title { get; set; }
            public string Tooltip { get; set; }
            public string Callback { get; set; }
            public string EnableCallback { get; set; }
            public int UserId { get; set; }
            public string IconResource { get; set; }
            public string IconFileKey { get; set; }
            public int CommandIndex { get; set; }
        }
    }
}
