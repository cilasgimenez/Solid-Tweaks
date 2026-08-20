using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidTweaks.Shared;

namespace SolidTweaks.SolidWorksAddin
{
    internal enum StepExportAction
    {
        Cancel,
        ExportOnly,
        ExportAndOpen
    }

    internal sealed class StepExportActionDialog : Form
    {
        private const string RegistryPath = @"Software\SolidTweaks\Features";
        private const string OpenFolderValueName = "ExportStepOpenFolder";
        private StepExportAction selectedAction;
        private readonly CheckBox openFolderCheckBox;
        private bool updatingPreference;

        private StepExportActionDialog()
        {
            Text = UiText.Get("Export STEP");
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(430, 140);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.White;

            var prompt = new Label
            {
                AutoSize = false,
                Text = UiText.Get("Choose what to do after generating the STEP file:"),
                Location = new Point(18, 18),
                Size = new Size(394, 22),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var exportButton = CreateButton(UiText.Get("Export"), 18, 58, 125);
            exportButton.Click += delegate
            {
                selectedAction = StepExportAction.ExportOnly;
                DialogResult = DialogResult.OK;
                Close();
            };

            var printButton = CreateButton(UiText.Get("Send to Print"), 151, 58, 261);
            printButton.Click += delegate
            {
                selectedAction = StepExportAction.ExportAndOpen;
                DialogResult = DialogResult.OK;
                Close();
            };

            openFolderCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(18, 101),
                Text = UiText.Get("Open folder"),
                Checked = ReadOpenFolderPreference(),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = true
            };
            openFolderCheckBox.CheckedChanged += OpenFolderCheckBoxOnCheckedChanged;

            AcceptButton = exportButton;
            CancelButton = null;
            Controls.Add(prompt);
            Controls.Add(exportButton);
            Controls.Add(printButton);
            Controls.Add(openFolderCheckBox);
        }

        public static StepExportAction Show(IntPtr ownerHandle, out bool openFolderAfterExport)
        {
            using (var dialog = new StepExportActionDialog())
            {
                DialogResult result = ownerHandle == IntPtr.Zero
                    ? dialog.ShowDialog()
                    : dialog.ShowDialog(new WindowHandle(ownerHandle));
                openFolderAfterExport = dialog.openFolderCheckBox.Checked;
                return result == DialogResult.OK ? dialog.selectedAction : StepExportAction.Cancel;
            }
        }

        private void OpenFolderCheckBoxOnCheckedChanged(object sender, EventArgs e)
        {
            if (updatingPreference)
            {
                return;
            }

            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key == null)
                    {
                        throw new InvalidOperationException("The preference could not be stored.");
                    }
                    key.SetValue(
                        OpenFolderValueName,
                        openFolderCheckBox.Checked ? 1 : 0,
                        RegistryValueKind.DWord);
                }
            }
            catch (Exception exception)
            {
                updatingPreference = true;
                openFolderCheckBox.Checked = !openFolderCheckBox.Checked;
                updatingPreference = false;
                MessageBox.Show(
                    this,
                    UiText.Format("Could not save the Open folder option.\n\n{0}", UiText.UserMessage(exception)),
                    "Solid Tweaks",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static bool ReadOpenFolderPreference()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                {
                    object value = key == null ? null : key.GetValue(OpenFolderValueName);
                    return value != null && Convert.ToInt32(value) != 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static Button CreateButton(string text, int left, int top, int width)
        {
            return new Button
            {
                Text = text,
                Location = new Point(left, top),
                Size = new Size(width, 36),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
        }

        private sealed class WindowHandle : IWin32Window
        {
            public WindowHandle(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; private set; }
        }
    }
}
