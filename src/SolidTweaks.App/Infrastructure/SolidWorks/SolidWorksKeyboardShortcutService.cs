using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using SolidTweaks.Shared;
using System.Windows.Forms;
using SolidTweaks.Core.SolidWorks;

namespace SolidTweaks.Infrastructure.SolidWorks
{
    internal enum SolidWorksKeyboardCommand
    {
        ViewOrientation = 341,
        NormalTo = 169,
        Fix = 170,
        ZoomToFit = 171,
        ConstructionGeometry = 172,
        Circle = 173,
        Measure = 174,
        OffsetEntities = 76
    }

    internal sealed class SolidWorksKeyboardShortcutService
    {
        private const int CustomizeCommand = 1137;
        private const int WmSetText = 0x000C;
        private const int WmKeyDown = 0x0100;
        private const int WmKeyUp = 0x0101;
        private const int WmChar = 0x0102;
        private const int WmLeftButtonDown = 0x0201;
        private const int WmLeftButtonUp = 0x0202;
        private const int BmClick = 0x00F5;
        private const int VirtualKeySpace = 0x20;
        private const int VirtualKeyF = 0x46;
        private const int VirtualKeyC = 0x43;
        private const int VirtualKeyM = 0x4D;
        private const int VirtualKeyBackspace = 0x08;
        private const int ScanCodeSpace = 0x39;
        private const int ScanCodeF = 0x21;
        private const int ScanCodeC = 0x2E;
        private const int ScanCodeM = 0x32;
        private const int ScanCodeBackspace = 0x0E;
        private const int AutomationIdTabControl = 12320;
        private const int AutomationIdSearch = 14985;
        private const int AutomationIdGrid = 14906;
        private const int AutomationIdCopyList = 14910;
        private const int AutomationIdOk = 7380;
        private const int AutomationIdCancel = 7381;

        private readonly ISolidWorksService solidWorks;

        public SolidWorksKeyboardShortcutService(ISolidWorksService solidWorks)
        {
            this.solidWorks = solidWorks;
        }

        public void AssignSpace(SolidWorksKeyboardCommand target, Action<string> reportProgress)
        {
            AssignShortcut(target, VirtualKeySpace, ScanCodeSpace, UiText.Get("Space"), reportProgress);
        }

        public void AssignF(SolidWorksKeyboardCommand target, Action<string> reportProgress)
        {
            AssignShortcut(target, VirtualKeyF, ScanCodeF, "F", reportProgress);
        }

        public void AssignC(SolidWorksKeyboardCommand target, Action<string> reportProgress)
        {
            AssignShortcut(target, VirtualKeyC, ScanCodeC, "C", reportProgress);
        }

        public void AssignM(SolidWorksKeyboardCommand target, Action<string> reportProgress)
        {
            AssignShortcut(target, VirtualKeyM, ScanCodeM, "M", reportProgress);
        }

        public void RemoveM(SolidWorksKeyboardCommand target, Action<string> reportProgress)
        {
            RemoveShortcut(target, "M", reportProgress);
        }

        public void Assign(ShortcutAction action, ShortcutBinding binding, Action<string> reportProgress)
        {
            if (binding == null)
            {
                throw new ArgumentException("A keyboard binding is required.", "binding");
            }
            AssignShortcut(
                CommandForAction(action),
                binding.VirtualKey,
                binding.ScanCode,
                ShortcutDisplayName(binding),
                reportProgress,
                binding.IsExtended);
        }

        public void Remove(ShortcutAction action, ShortcutBinding binding, Action<string> reportProgress)
        {
            if (binding == null)
            {
                return;
            }
            RemoveShortcut(CommandForAction(action), ShortcutDisplayName(binding), reportProgress, true);
        }

        public void RemoveForReplacement(ShortcutAction action, ShortcutBinding binding, Action<string> reportProgress)
        {
            if (binding == null)
            {
                return;
            }
            RemoveShortcut(CommandForAction(action), ShortcutDisplayName(binding), reportProgress, false);
        }

        public void Restore(ShortcutAction action, ShortcutBinding binding, Action<string> reportProgress)
        {
            if (binding == null)
            {
                return;
            }
            AssignShortcut(
                CommandForAction(action),
                binding.VirtualKey,
                binding.ScanCode,
                ShortcutDisplayName(binding),
                reportProgress,
                binding.IsExtended);
        }

        public void RevertLegacyShortcuts(bool fixEnabled, bool constructionEnabled, bool measureEnabled, Action<string> reportProgress)
        {
            if (fixEnabled)
            {
                AssignF(SolidWorksKeyboardCommand.ZoomToFit, reportProgress);
            }
            if (constructionEnabled)
            {
                AssignC(SolidWorksKeyboardCommand.Circle, reportProgress);
            }
            if (measureEnabled)
            {
                RemoveM(SolidWorksKeyboardCommand.Measure, reportProgress);
            }
        }

        private void AssignShortcut(
            SolidWorksKeyboardCommand target,
            int virtualKey,
            int scanCode,
            string shortcutName,
            Action<string> reportProgress,
            bool isExtended = false)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                throw new InvalidOperationException("Keyboard customization must be run from the main user interface.");
            }

            Process process = GetSolidWorksProcess();
            if (FindCustomizeDialog(process.Id) != null)
            {
                throw new InvalidOperationException("Close the Customize window that is already open in SOLIDWORKS.");
            }

            Report(reportProgress, UiText.Get("Opening SOLIDWORKS keyboard customization..."));
            bool commandResult = false;
            Exception commandError = null;
            var commandCompleted = new ManualResetEvent(false);
            var commandThread = new Thread(delegate()
            {
                try
                {
                    commandResult = solidWorks.RunActiveDocumentCommand(CustomizeCommand);
                }
                catch (Exception exception)
                {
                    commandError = exception;
                }
                finally
                {
                    commandCompleted.Set();
                }
            });
            commandThread.IsBackground = true;
            commandThread.SetApartmentState(ApartmentState.STA);
            commandThread.Start();

            AutomationElement dialog = null;
            try
            {
                dialog = WaitForElement(delegate
                {
                    AutomationElement opened = FindCustomizeDialog(process.Id);
                    if (opened == null && commandCompleted.WaitOne(0))
                    {
                        if (commandError != null)
                        {
                            throw new InvalidOperationException("SOLIDWORKS did not open the Customize window.", commandError);
                        }
                        if (!commandResult)
                        {
                            throw new InvalidOperationException("SOLIDWORKS did not open the Customize window.");
                        }
                    }
                    return opened;
                }, 5000, "The Customize window was not found.");
                OpenKeyboardTab(dialog);

                AutomationElement search = FindByAutomationId(dialog, AutomationIdSearch);
                AutomationElement grid = FindByAutomationId(dialog, AutomationIdGrid);
                AutomationElement copyList = FindByAutomationId(dialog, AutomationIdCopyList);
                if (search == null || grid == null || copyList == null)
                {
                    throw new InvalidOperationException("The Keyboard tab controls were not found.");
                }

                Report(reportProgress, UiText.Format("Locating the {0} command...", CommandDisplayName(target)));

                TargetRow targetRow = LocateTargetRow(dialog, search, copyList, process.Id, target);
                if (!ContainsShortcut(targetRow.Shortcuts, shortcutName))
                {
                    SelectGridRow(grid, targetRow.Index);
                    Report(reportProgress, UiText.Format("Assigning the {0} key and resolving conflicts...", shortcutName));
                    PostShortcutKey(
                        grid.Current.NativeWindowHandle,
                        dialog.Current.NativeWindowHandle,
                        virtualKey,
                        scanCode,
                        isExtended);
                    AcceptReassignment(process.Id, dialog);

                    TargetRow confirmed = LocateTargetRow(dialog, search, copyList, process.Id, target);
                    if (!ContainsShortcut(confirmed.Shortcuts, shortcutName))
                    {
                        throw new InvalidOperationException(
                            "The SOLIDWORKS table did not confirm the " + shortcutName + " key.");
                    }
                }

                Report(reportProgress, UiText.Get("Confirming and saving the native shortcut..."));
                ClickButton(FindByAutomationId(dialog, AutomationIdOk), "OK");
                WaitUntil(() => FindCustomizeDialog(process.Id) == null, 3000);
                if (!commandCompleted.WaitOne(3000))
                {
                    throw new InvalidOperationException("SOLIDWORKS did not complete keyboard customization.");
                }
                if (commandError != null)
                {
                    throw new InvalidOperationException("SOLIDWORKS did not save keyboard customization.", commandError);
                }
                if (!commandResult)
                {
                    throw new InvalidOperationException("SOLIDWORKS rejected keyboard customization.");
                }
            }
            catch
            {
                if (dialog != null)
                {
                    AutomationElement cancel = FindByAutomationId(dialog, AutomationIdCancel);
                    if (cancel != null)
                    {
                        SendMessage(new IntPtr(cancel.Current.NativeWindowHandle), BmClick, IntPtr.Zero, IntPtr.Zero);
                    }
                }
                throw;
            }
            finally
            {
                if (commandCompleted.WaitOne(0))
                {
                    commandCompleted.Close();
                }
            }
        }

        private void RemoveShortcut(
            SolidWorksKeyboardCommand target,
            string shortcutName,
            Action<string> reportProgress,
            bool preserveOtherShortcuts = true)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                throw new InvalidOperationException("Keyboard customization must be run from the main user interface.");
            }

            Process process = GetSolidWorksProcess();
            if (FindCustomizeDialog(process.Id) != null)
            {
                throw new InvalidOperationException("Close the Customize window that is already open in SOLIDWORKS.");
            }

            Report(reportProgress, UiText.Get("Opening SOLIDWORKS keyboard customization..."));
            bool commandResult = false;
            Exception commandError = null;
            var commandCompleted = new ManualResetEvent(false);
            var commandThread = new Thread(delegate()
            {
                try
                {
                    commandResult = solidWorks.RunActiveDocumentCommand(CustomizeCommand);
                }
                catch (Exception exception)
                {
                    commandError = exception;
                }
                finally
                {
                    commandCompleted.Set();
                }
            });
            commandThread.IsBackground = true;
            commandThread.SetApartmentState(ApartmentState.STA);
            commandThread.Start();

            AutomationElement dialog = null;
            try
            {
                dialog = WaitForElement(delegate
                {
                    AutomationElement opened = FindCustomizeDialog(process.Id);
                    if (opened == null && commandCompleted.WaitOne(0))
                    {
                        if (commandError != null)
                        {
                            throw new InvalidOperationException("SOLIDWORKS did not open the Customize window.", commandError);
                        }
                        if (!commandResult)
                        {
                            throw new InvalidOperationException("SOLIDWORKS did not open the Customize window.");
                        }
                    }
                    return opened;
                }, 5000, "The Customize window was not found.");
                OpenKeyboardTab(dialog);

                AutomationElement search = FindByAutomationId(dialog, AutomationIdSearch);
                AutomationElement grid = FindByAutomationId(dialog, AutomationIdGrid);
                AutomationElement copyList = FindByAutomationId(dialog, AutomationIdCopyList);
                if (search == null || grid == null || copyList == null)
                {
                    throw new InvalidOperationException("The Keyboard tab controls were not found.");
                }

                Report(reportProgress, UiText.Format("Locating the {0} command...", CommandDisplayName(target)));
                TargetRow targetRow = LocateTargetRow(dialog, search, copyList, process.Id, target);
                if (ContainsShortcut(targetRow.Shortcuts, shortcutName))
                {
                    Report(reportProgress, UiText.Format("Removing the {0} key...", shortcutName));
                    int maximumAttempts = Math.Max(1, SplitShortcuts(targetRow.Shortcuts).Length + 2);
                    TargetRow confirmed = targetRow;
                    for (int attempt = 0; attempt < maximumAttempts && ContainsShortcut(confirmed.Shortcuts, shortcutName); attempt++)
                    {
                        SelectGridShortcutCell(grid, confirmed.Index);
                        PostClearShortcutKey(
                            grid.Current.NativeWindowHandle,
                            dialog.Current.NativeWindowHandle);
                        Thread.Sleep(250);
                        confirmed = LocateTargetRow(dialog, search, copyList, process.Id, target);
                    }
                    if (ContainsShortcut(confirmed.Shortcuts, shortcutName))
                    {
                        throw new InvalidOperationException(
                            "The SOLIDWORKS table did not remove the " + shortcutName + " key.");
                    }
                    if (preserveOtherShortcuts && !OtherShortcutsWerePreserved(targetRow.Shortcuts, confirmed.Shortcuts, shortcutName))
                    {
                        throw new InvalidOperationException(
                            "SOLIDWORKS removed another shortcut together with the " + shortcutName + " key.");
                    }
                }

                Report(reportProgress, UiText.Get("Confirming and saving the native shortcut..."));
                ClickButton(FindByAutomationId(dialog, AutomationIdOk), "OK");
                WaitUntil(() => FindCustomizeDialog(process.Id) == null, 3000);
                if (!commandCompleted.WaitOne(3000))
                {
                    throw new InvalidOperationException("SOLIDWORKS did not complete keyboard customization.");
                }
                if (commandError != null)
                {
                    throw new InvalidOperationException("SOLIDWORKS did not save keyboard customization.", commandError);
                }
                if (!commandResult)
                {
                    throw new InvalidOperationException("SOLIDWORKS rejected keyboard customization.");
                }
            }
            catch
            {
                if (dialog != null)
                {
                    AutomationElement cancel = FindByAutomationId(dialog, AutomationIdCancel);
                    if (cancel != null)
                    {
                        SendMessage(new IntPtr(cancel.Current.NativeWindowHandle), BmClick, IntPtr.Zero, IntPtr.Zero);
                    }
                }
                throw;
            }
            finally
            {
                if (commandCompleted.WaitOne(0))
                {
                    commandCompleted.Close();
                }
            }
        }

        private static void OpenKeyboardTab(AutomationElement dialog)
        {
            AutomationElement tab = FindByAutomationId(dialog, AutomationIdTabControl);
            if (tab == null)
            {
                throw new InvalidOperationException("The tab control in the Customize window was not found.");
            }

            // A aba Keyboard/Teclado é a quinta aba. As proporções cobrem PT-BR e inglês
            // sem depender de leitura de memória do controle nativo SysTabControl32.
            double[] candidateRatios = { 0.58, 0.54, 0.62, 0.50 };
            foreach (double ratio in candidateRatios)
            {
                System.Windows.Rect bounds = tab.Current.BoundingRectangle;
                int x = Math.Max(8, (int)Math.Round(bounds.Width * ratio));
                int y = Math.Max(6, Math.Min(14, (int)Math.Round(bounds.Height * 0.02)));
                SendMouseClick(new IntPtr(tab.Current.NativeWindowHandle), x, y);
                Thread.Sleep(120);
                if (FindByAutomationId(dialog, AutomationIdSearch) != null)
                {
                    return;
                }
            }

            throw new InvalidOperationException("Could not open the Keyboard tab.");
        }

        private static TargetRow LocateTargetRow(
            AutomationElement dialog,
            AutomationElement search,
            AutomationElement copyList,
            int processId,
            SolidWorksKeyboardCommand target)
        {
            bool portuguese = Normalize(dialog.Current.Name).Contains("personalizar");
            string[] searchTerms;
            string[] aliases;
            switch (target)
            {
                case SolidWorksKeyboardCommand.NormalTo:
                    searchTerms = portuguese
                        ? new[] { "Normal a", "Normal To" }
                        : new[] { "Normal To", "Normal a" };
                    aliases = new[] { "normal a", "normal to" };
                    break;
                case SolidWorksKeyboardCommand.Fix:
                    searchTerms = portuguese
                        ? new[] { "Tornar fixo", "Fix", "Fixar" }
                        : new[] { "Make Fixed", "Fix", "Fixed" };
                    aliases = new[] { "tornar fixo", "make fixed", "fixar", "fix", "fixed" };
                    break;
                case SolidWorksKeyboardCommand.ZoomToFit:
                    searchTerms = portuguese
                        ? new[] { "Zoom para ajustar", "Zoom to Fit" }
                        : new[] { "Zoom to Fit", "Zoom para ajustar" };
                    aliases = new[] { "zoom para ajustar", "zoom to fit" };
                    break;
                case SolidWorksKeyboardCommand.ConstructionGeometry:
                    searchTerms = portuguese
                        ? new[] { "Geometria de construção", "Construção", "Construction Geometry" }
                        : new[] { "Construction Geometry", "Geometria de construção" };
                    aliases = new[]
                    {
                        "geometria de construcao",
                        "construction geometry",
                        "para construcao",
                        "for construction"
                    };
                    break;
                case SolidWorksKeyboardCommand.Circle:
                    searchTerms = portuguese
                        ? new[] { "Círculo", "Circle" }
                        : new[] { "Circle", "Círculo" };
                    aliases = new[] { "circulo", "circle" };
                    break;
                case SolidWorksKeyboardCommand.Measure:
                    searchTerms = portuguese
                        ? new[] { "Medir", "Measure" }
                        : new[] { "Measure", "Medir" };
                    aliases = new[] { "medir", "measure" };
                    break;
                case SolidWorksKeyboardCommand.OffsetEntities:
                    searchTerms = portuguese
                        ? new[] { "Offset de entidades", "Offset Entities" }
                        : new[] { "Offset Entities", "Offset de entidades" };
                    aliases = new[] { "offset de entidades", "offset entities" };
                    break;
                default:
                    searchTerms = portuguese
                        ? new[] { "Orientação", "View Orientation" }
                        : new[] { "View Orientation", "Orientation" };
                    aliases = new[] { "orientacao", "orientation", "view orientation" };
                    break;
            }

            foreach (string searchTerm in searchTerms)
            {
                SendMessageText(new IntPtr(search.Current.NativeWindowHandle), WmSetText, searchTerm);
                Thread.Sleep(600);
                IList<GridRow> rows = CopyVisibleRows(copyList, processId, dialog);
                for (int index = 0; index < rows.Count; index++)
                {
                    if (aliases.Contains(Normalize(rows[index].Command)))
                    {
                        return new TargetRow(index, rows[index].Shortcuts);
                    }
                }
            }

            throw new InvalidOperationException(
                "The " + CommandDisplayName(target) + " command was not found in the table.");
        }

        private static IList<GridRow> CopyVisibleRows(
            AutomationElement copyList,
            int processId,
            AutomationElement customizeDialog)
        {
            ClipboardBackup backup = ClipboardBackup.Capture();
            try
            {
                string text = string.Empty;
                for (int attempt = 0; attempt < 3 && string.IsNullOrWhiteSpace(text); attempt++)
                {
                    uint initialSequence = GetClipboardSequenceNumber();
                    PostMessage(new IntPtr(copyList.Current.NativeWindowHandle), BmClick, IntPtr.Zero, IntPtr.Zero);
                    CloseCopyNotice(processId, customizeDialog);
                    WaitUntil(() => GetClipboardSequenceNumber() != initialSequence, 1500);
                    text = ReadClipboardText();
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        Thread.Sleep(200);
                    }
                }

                var rows = new List<GridRow>();
                if (string.IsNullOrWhiteSpace(text))
                {
                    // Uma pesquisa sem correspondências gera uma lista vazia. O chamador
                    // deve continuar com os demais nomes do comando em vez de abortar.
                    return rows;
                }
                string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string[] columns = line.Split('\t');
                    if (columns.Length >= 7)
                    {
                        rows.Add(new GridRow(columns[3], columns[6]));
                    }
                }
                return rows;
            }
            finally
            {
                backup.Restore();
            }
        }

        private static void CloseCopyNotice(int processId, AutomationElement customizeDialog)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < 1200)
            {
                foreach (AutomationElement window in GetProcessDialogs(processId))
                {
                    if (window.Current.NativeWindowHandle == customizeDialog.Current.NativeWindowHandle)
                    {
                        continue;
                    }

                    AutomationElementCollection elements = window.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                    bool isCopyNotice = elements.Cast<AutomationElement>().Any(element =>
                    {
                        string name = Normalize(element.Current.Name);
                        return name.Contains("lista foi copia") || name.Contains("list was copied") || name.Contains("list has been copied");
                    });
                    if (!isCopyNotice)
                    {
                        continue;
                    }

                    AutomationElement ok = FindButtonByNames(window, "OK", "&OK");
                    ClickButton(ok, "OK");
                    Thread.Sleep(150);
                    return;
                }
                Thread.Sleep(50);
            }
            // Algumas instalações copiam a lista diretamente, sem mostrar confirmação.
            // A ausência do aviso não significa falha; o conteúdo é validado em seguida.
        }

        private static string ReadClipboardText()
        {
            Exception lastError = null;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    string text = Clipboard.GetText(TextDataFormat.UnicodeText);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        text = Clipboard.GetText(TextDataFormat.Text);
                    }
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
                catch (ExternalException exception)
                {
                    lastError = exception;
                }
                Thread.Sleep(50);
            }
            if (lastError != null)
            {
                throw new InvalidOperationException("The clipboard is busy.", lastError);
            }
            return string.Empty;
        }

        private static void SelectGridRow(AutomationElement grid, int rowIndex)
        {
            System.Windows.Rect bounds = grid.Current.BoundingRectangle;
            uint dpi = GetDpiForWindow(new IntPtr(grid.Current.NativeWindowHandle));
            double scale = dpi == 0 ? 1.0 : dpi / 96.0;
            int x = Math.Max(60, Math.Min((int)(bounds.Width * 0.32), (int)(190 * scale)));
            int y = (int)Math.Round((41 + 12 + (24 * rowIndex)) * scale);
            if (y <= 0 || y >= bounds.Height)
            {
                throw new InvalidOperationException("The command row moved outside the visible table.");
            }

            SendMouseClick(new IntPtr(grid.Current.NativeWindowHandle), x, y);
            Thread.Sleep(100);
        }

        private static void SelectGridShortcutCell(AutomationElement grid, int rowIndex)
        {
            System.Windows.Rect bounds = grid.Current.BoundingRectangle;
            uint dpi = GetDpiForWindow(new IntPtr(grid.Current.NativeWindowHandle));
            double scale = dpi == 0 ? 1.0 : dpi / 96.0;
            int x = Math.Max(
                (int)Math.Round(220 * scale),
                Math.Min((int)(bounds.Width * 0.58), (int)Math.Round(350 * scale)));
            int y = (int)Math.Round((41 + 12 + (24 * rowIndex)) * scale);
            if (y <= 0 || y >= bounds.Height)
            {
                throw new InvalidOperationException("The command row moved outside the visible table.");
            }

            SendMouseClick(new IntPtr(grid.Current.NativeWindowHandle), x, y);
            Thread.Sleep(100);
        }

        private static void FocusWindow(
            IntPtr window,
            IntPtr foregroundWindow,
            int virtualKey,
            int scanCode,
            bool sendCharacter,
            bool sendWindowMessages,
            bool isExtended = false)
        {
            uint targetProcessId;
            uint targetThread = GetWindowThreadProcessId(window, out targetProcessId);
            uint currentThread = GetCurrentThreadId();
            uint foregroundProcessId;
            uint foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out foregroundProcessId);
            bool attachedTarget = currentThread != targetThread && AttachThreadInput(currentThread, targetThread, true);
            bool attachedForeground = currentThread != foregroundThread && foregroundThread != targetThread &&
                AttachThreadInput(currentThread, foregroundThread, true);
            try
            {
                IntPtr root = GetAncestor(foregroundWindow, 2);
                ShowWindow(root, 5);
                BringWindowToTop(root);
                SwitchToThisWindow(root, true);
                SetForegroundWindow(root);
                SetActiveWindow(foregroundWindow);
                SetFocus(window);
                if (GetFocus() != window)
                {
                    throw new InvalidOperationException("The shortcut table did not receive keyboard focus.");
                }
                int extendedFlag = isExtended ? 0x01000000 : 0;
                int keyDownData = extendedFlag | (scanCode << 16) | 1;
                int keyUpData = unchecked((int)(0xC0000001U | (uint)extendedFlag | ((uint)scanCode << 16)));
                uint nativeExtended = isExtended ? 1U : 0U;
                keybd_event((byte)virtualKey, (byte)scanCode, nativeExtended, UIntPtr.Zero);
                keybd_event((byte)virtualKey, (byte)scanCode, nativeExtended | 2U, UIntPtr.Zero);
                if (sendWindowMessages)
                {
                    SendMessage(window, WmKeyDown, new IntPtr(virtualKey), new IntPtr(keyDownData));
                    if (sendCharacter)
                    {
                        SendMessage(window, WmChar, new IntPtr(virtualKey), new IntPtr(keyDownData));
                    }
                    SendMessage(window, WmKeyUp, new IntPtr(virtualKey), new IntPtr(keyUpData));
                }
            }
            finally
            {
                if (attachedForeground)
                {
                    AttachThreadInput(currentThread, foregroundThread, false);
                }
                if (attachedTarget)
                {
                    AttachThreadInput(currentThread, targetThread, false);
                }
            }
        }

        private static void PostShortcutKey(int gridHandle, int dialogHandle, int virtualKey, int scanCode, bool isExtended)
        {
            IntPtr handle = new IntPtr(gridHandle);
            FocusWindow(handle, new IntPtr(dialogHandle), virtualKey, scanCode, true, true, isExtended);
        }

        private static void PostClearShortcutKey(int gridHandle, int dialogHandle)
        {
            IntPtr grid = new IntPtr(gridHandle);
            IntPtr shortcutEditor = WaitForChildWindow(grid, "GXEDIT", 1000);
            FocusWindow(
                shortcutEditor,
                new IntPtr(dialogHandle),
                VirtualKeyBackspace,
                ScanCodeBackspace,
                false,
                false);
            Thread.Sleep(250);
        }

        private static IntPtr WaitForChildWindow(
            IntPtr parent,
            string className,
            int timeoutMilliseconds)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < timeoutMilliseconds)
            {
                IntPtr child = FindWindowEx(parent, IntPtr.Zero, className, null);
                if (child != IntPtr.Zero)
                {
                    return child;
                }
                Thread.Sleep(50);
            }
            throw new InvalidOperationException("The shortcut field editor was not found.");
        }

        private static void AcceptReassignment(int processId, AutomationElement customizeDialog)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < 1800)
            {
                foreach (AutomationElement confirmation in GetProcessDialogs(processId))
                {
                    if (confirmation.Current.NativeWindowHandle == customizeDialog.Current.NativeWindowHandle)
                    {
                        continue;
                    }
                    AutomationElement yes = FindButtonByNames(confirmation, "Sim", "Yes", "&Sim", "&Yes");
                    if (yes != null)
                    {
                        ShowWindow(new IntPtr(confirmation.Current.NativeWindowHandle), 0);
                        ClickButton(yes, "Sim/Yes");
                        Thread.Sleep(250);
                        return;
                    }
                }
                Thread.Sleep(10);
            }
        }

        private static SolidWorksKeyboardCommand CommandForAction(ShortcutAction action)
        {
            switch (action)
            {
                case ShortcutAction.MakeFixed:
                    return SolidWorksKeyboardCommand.Fix;
                case ShortcutAction.ConstructionGeometry:
                    return SolidWorksKeyboardCommand.ConstructionGeometry;
                case ShortcutAction.Measure:
                    return SolidWorksKeyboardCommand.Measure;
                default:
                    return SolidWorksKeyboardCommand.OffsetEntities;
            }
        }

        public static string ShortcutDisplayName(ShortcutBinding binding)
        {
            if (binding == null)
            {
                return UiText.Get("Set key");
            }
            Keys key = (Keys)binding.VirtualKey;
            switch (key)
            {
                case Keys.Space:
                    return UiText.Get("Space");
                case Keys.Escape:
                    return "Esc";
                case Keys.Return:
                    return "Enter";
                case Keys.Back:
                    return "Backspace";
                case Keys.Delete:
                    return "Delete";
                case Keys.PageUp:
                    return "Page Up";
                case Keys.PageDown:
                    return "Page Down";
            }
            string converted = new KeysConverter().ConvertToString(key);
            return string.IsNullOrWhiteSpace(converted) ? "VK " + binding.VirtualKey : converted;
        }

        private static IEnumerable<AutomationElement> GetProcessDialogs(int processId)
        {
            var results = new List<AutomationElement>();
            var handles = new HashSet<IntPtr>();
            EnumWindowsProc childVisitor = delegate(IntPtr child, IntPtr parameter)
            {
                uint childProcessId;
                GetWindowThreadProcessId(child, out childProcessId);
                if (childProcessId == (uint)processId && IsDialogWindow(child))
                {
                    handles.Add(child);
                }
                return true;
            };

            EnumWindowsProc topLevelVisitor = delegate(IntPtr window, IntPtr parameter)
            {
                uint windowProcessId;
                GetWindowThreadProcessId(window, out windowProcessId);
                if (windowProcessId == (uint)processId)
                {
                    if (IsDialogWindow(window))
                    {
                        handles.Add(window);
                    }
                    EnumChildWindows(window, childVisitor, IntPtr.Zero);
                }
                return true;
            };
            EnumWindows(topLevelVisitor, IntPtr.Zero);

            foreach (IntPtr handle in handles)
            {
                try
                {
                    results.Add(AutomationElement.FromHandle(handle));
                }
                catch (ElementNotAvailableException) { }
            }
            return results;
        }

        private static bool IsDialogWindow(IntPtr window)
        {
            var className = new StringBuilder(32);
            GetClassName(window, className, className.Capacity);
            return string.Equals(className.ToString(), "#32770", StringComparison.Ordinal);
        }

        private static AutomationElement FindButtonByNames(AutomationElement parent, params string[] names)
        {
            AutomationElementCollection elements = parent.FindAll(TreeScope.Descendants, Condition.TrueCondition);
            foreach (AutomationElement element in elements)
            {
                if (names.Any(name => string.Equals(element.Current.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    return element;
                }
            }
            return null;
        }

        private static AutomationElement FindCustomizeDialog(int processId)
        {
            foreach (AutomationElement window in GetProcessDialogs(processId))
            {
                try
                {
                    string name = Normalize(window.Current.Name);
                    if (name == "personalizar" || name == "customize")
                    {
                        return window;
                    }
                }
                catch (ElementNotAvailableException) { }
            }
            return null;
        }

        private static AutomationElement FindByAutomationId(AutomationElement parent, int automationId)
        {
            try
            {
                return parent.FindFirst(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, automationId.ToString(CultureInfo.InvariantCulture)));
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
        }

        private static Process GetSolidWorksProcess()
        {
            Process process = Process.GetProcessesByName("SLDWORKS")
                .FirstOrDefault(item => item.MainWindowHandle != IntPtr.Zero);
            if (process == null)
            {
                throw new InvalidOperationException("The SOLIDWORKS main window was not found.");
            }
            return process;
        }

        private static T WaitForElement<T>(Func<T> getter, int timeoutMilliseconds, string errorMessage) where T : class
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < timeoutMilliseconds)
            {
                T value = getter();
                if (value != null)
                {
                    return value;
                }
                Thread.Sleep(75);
            }
            throw new InvalidOperationException(errorMessage);
        }

        private static void WaitUntil(Func<bool> condition, int timeoutMilliseconds)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < timeoutMilliseconds)
            {
                if (condition())
                {
                    return;
                }
                Thread.Sleep(50);
            }
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string decomposed = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            bool previousSpace = false;
            foreach (char character in decomposed)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                    previousSpace = false;
                }
                else if (char.IsWhiteSpace(character) && !previousSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    previousSpace = true;
                }
            }
            return builder.ToString().Trim();
        }

        private static bool ContainsShortcut(string shortcuts, string shortcutName)
        {
            if (string.Equals(shortcutName, UiText.Get("Space"), StringComparison.Ordinal))
            {
                return Normalize(shortcuts).Contains("space");
            }
            string[] assignedKeys = shortcuts.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return assignedKeys.Any(value => string.Equals(Normalize(value), Normalize(shortcutName), StringComparison.Ordinal));
        }

        private static bool OtherShortcutsWerePreserved(
            string originalShortcuts,
            string confirmedShortcuts,
            string removedShortcut)
        {
            string[] original = SplitShortcuts(originalShortcuts)
                .Where(value => !string.Equals(Normalize(value), Normalize(removedShortcut), StringComparison.Ordinal))
                .Select(Normalize)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] confirmed = SplitShortcuts(confirmedShortcuts)
                .Select(Normalize)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return original.SequenceEqual(confirmed, StringComparer.Ordinal);
        }

        private static string[] SplitShortcuts(string shortcuts)
        {
            return (shortcuts ?? string.Empty).Split(
                new[] { ',', ';', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
        }

        private static string CommandDisplayName(SolidWorksKeyboardCommand target)
        {
            switch (target)
            {
                case SolidWorksKeyboardCommand.NormalTo:
                    return UiText.Get("Normal To");
                case SolidWorksKeyboardCommand.Fix:
                    return UiText.Get("Make Fixed");
                case SolidWorksKeyboardCommand.ZoomToFit:
                    return UiText.Get("Zoom to Fit");
                case SolidWorksKeyboardCommand.ConstructionGeometry:
                    return UiText.Get("Construction Geometry");
                case SolidWorksKeyboardCommand.Circle:
                    return UiText.Get("Circle");
                case SolidWorksKeyboardCommand.Measure:
                    return UiText.Get("Measure");
                case SolidWorksKeyboardCommand.OffsetEntities:
                    return UiText.Get("Offset Entities");
                default:
                    return UiText.Get("View Orientation");
            }
        }

        private static void ClickButton(AutomationElement button, string name)
        {
            if (button == null)
            {
                throw new InvalidOperationException("The " + name + " button was not found.");
            }
            SendMessage(new IntPtr(button.Current.NativeWindowHandle), BmClick, IntPtr.Zero, IntPtr.Zero);
        }

        private static void SendMouseClick(IntPtr window, int x, int y)
        {
            IntPtr coordinates = new IntPtr((y << 16) | (x & 0xFFFF));
            SendMessage(window, WmLeftButtonDown, new IntPtr(1), coordinates);
            SendMessage(window, WmLeftButtonUp, IntPtr.Zero, coordinates);
        }

        private static void Report(Action<string> reportProgress, string message)
        {
            if (reportProgress != null)
            {
                reportProgress(message);
            }
        }

        private sealed class GridRow
        {
            public GridRow(string command, string shortcuts)
            {
                Command = command;
                Shortcuts = shortcuts;
            }
            public string Command { get; private set; }
            public string Shortcuts { get; private set; }
        }

        private sealed class TargetRow
        {
            public TargetRow(int index, string shortcuts)
            {
                Index = index;
                Shortcuts = shortcuts;
            }
            public int Index { get; private set; }
            public string Shortcuts { get; private set; }
        }

        private sealed class ClipboardBackup
        {
            private readonly DataObject data;
            private readonly bool wasEmpty;

            private ClipboardBackup(DataObject data, bool wasEmpty)
            {
                this.data = data;
                this.wasEmpty = wasEmpty;
            }

            public static ClipboardBackup Capture()
            {
                System.Windows.Forms.IDataObject source = Clipboard.GetDataObject();
                if (source == null)
                {
                    return new ClipboardBackup(null, true);
                }

                string[] formats = source.GetFormats(false);
                var copy = new DataObject();
                foreach (string format in formats)
                {
                    object value = source.GetData(format, false);
                    if (value == null)
                    {
                        continue;
                    }

                    object materialized = Materialize(value);
                    if (materialized == null)
                    {
                        throw new InvalidOperationException(
                            "The clipboard contains a format that cannot be safely preserved: " + format + ".");
                    }
                    copy.SetData(format, false, materialized);
                }
                return new ClipboardBackup(copy, formats.Length == 0);
            }

            public void Restore()
            {
                if (wasEmpty || data == null)
                {
                    Clipboard.Clear();
                }
                else
                {
                    Clipboard.SetDataObject(data, true);
                }
            }

            private static object Materialize(object value)
            {
                string text = value as string;
                if (text != null)
                {
                    return string.Copy(text);
                }

                string[] files = value as string[];
                if (files != null)
                {
                    return files.ToArray();
                }

                byte[] bytes = value as byte[];
                if (bytes != null)
                {
                    return bytes.ToArray();
                }

                MemoryStream stream = value as MemoryStream;
                if (stream != null)
                {
                    return new MemoryStream(stream.ToArray(), false);
                }

                Image image = value as Image;
                if (image != null)
                {
                    return new Bitmap(image);
                }

                return value.GetType().IsValueType ? value : null;
            }
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, string text);

        private static void SendMessageText(IntPtr window, int message, string text)
        {
            SendMessage(window, message, IntPtr.Zero, text);
        }

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint threadId, uint attachToThreadId, bool attach);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr window, uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetActiveWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern void SwitchToThisWindow(IntPtr window, bool altTab);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(
            IntPtr parent,
            IntPtr childAfter,
            string className,
            string windowName);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr window);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);


    }
}
