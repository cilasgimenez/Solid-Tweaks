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

    internal enum StepExportProtocol
    {
        AP203 = 203,
        AP214 = 214
    }

    internal sealed class StepExportActionDialog : Form
    {
        private const string RegistryPath = @"Software\SolidTweaks\Features";
        private const string OpenFolderValueName = "ExportStepOpenFolder";
        private const string ProtocolValueName = "ExportStepProtocol";
        private StepExportAction selectedAction;
        private StepExportProtocol selectedProtocol;
        private readonly RadioButton ap203RadioButton;
        private readonly RadioButton ap214RadioButton;
        private readonly CheckBox openFolderCheckBox;
        private bool updatingPreference;

        private StepExportActionDialog()
        {
            Text = UiText.Get("Export STEP");
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(544, 170);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.White;

            var protocolLabel = new Label
            {
                AutoSize = true,
                Text = UiText.Get("STEP protocol:"),
                Location = new Point(25, 29)
            };

            selectedProtocol = ReadProtocolPreference();
            ap203RadioButton = CreateProtocolRadioButton("AP203", 159, StepExportProtocol.AP203);
            ap214RadioButton = CreateProtocolRadioButton("AP214", 272, StepExportProtocol.AP214);
            ap203RadioButton.CheckedChanged += ProtocolRadioButtonOnCheckedChanged;
            ap214RadioButton.CheckedChanged += ProtocolRadioButtonOnCheckedChanged;

            var exportButton = CreateButton(UiText.Get("Export"), 25, 68, 157);
            exportButton.Click += delegate
            {
                selectedAction = StepExportAction.ExportOnly;
                DialogResult = DialogResult.OK;
                Close();
            };

            var printButton = CreateButton(UiText.Get("Send to Print"), 195, 68, 331);
            printButton.Click += delegate
            {
                selectedAction = StepExportAction.ExportAndOpen;
                DialogResult = DialogResult.OK;
                Close();
            };

            openFolderCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(24, 136),
                Text = UiText.Get("Open folder"),
                Checked = ReadOpenFolderPreference(),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = true
            };
            openFolderCheckBox.CheckedChanged += OpenFolderCheckBoxOnCheckedChanged;

            AcceptButton = exportButton;
            CancelButton = null;
            Controls.Add(protocolLabel);
            Controls.Add(ap203RadioButton);
            Controls.Add(ap214RadioButton);
            Controls.Add(exportButton);
            Controls.Add(printButton);
            Controls.Add(openFolderCheckBox);
        }

        public static StepExportAction Show(
            IntPtr ownerHandle,
            out bool openFolderAfterExport,
            out StepExportProtocol protocol)
        {
            using (var dialog = new StepExportActionDialog())
            {
                DialogResult result = ownerHandle == IntPtr.Zero
                    ? dialog.ShowDialog()
                    : dialog.ShowDialog(new WindowHandle(ownerHandle));
                openFolderAfterExport = dialog.openFolderCheckBox.Checked;
                protocol = dialog.selectedProtocol;
                return result == DialogResult.OK ? dialog.selectedAction : StepExportAction.Cancel;
            }
        }

        private RadioButton CreateProtocolRadioButton(string text, int left, StepExportProtocol protocol)
        {
            return new RadioButton
            {
                AutoSize = true,
                Checked = selectedProtocol == protocol,
                Cursor = Cursors.Hand,
                Location = new Point(left, 27),
                Text = text,
                UseVisualStyleBackColor = true
            };
        }

        private void ProtocolRadioButtonOnCheckedChanged(object sender, EventArgs e)
        {
            var radioButton = sender as RadioButton;
            if (updatingPreference || radioButton == null || !radioButton.Checked)
            {
                return;
            }

            StepExportProtocol protocol = ReferenceEquals(radioButton, ap214RadioButton)
                ? StepExportProtocol.AP214
                : StepExportProtocol.AP203;
            if (protocol == selectedProtocol)
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
                    key.SetValue(ProtocolValueName, (int)protocol, RegistryValueKind.DWord);
                }
                selectedProtocol = protocol;
            }
            catch (Exception exception)
            {
                updatingPreference = true;
                ap203RadioButton.Checked = selectedProtocol == StepExportProtocol.AP203;
                ap214RadioButton.Checked = selectedProtocol == StepExportProtocol.AP214;
                updatingPreference = false;
                MessageBox.Show(
                    this,
                    UiText.Format("Could not save the STEP protocol.\n\n{0}", UiText.UserMessage(exception)),
                    "Solid Tweaks",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
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

        private static StepExportProtocol ReadProtocolPreference()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                {
                    object value = key == null ? null : key.GetValue(ProtocolValueName);
                    return value != null && Convert.ToInt32(value) == (int)StepExportProtocol.AP214
                        ? StepExportProtocol.AP214
                        : StepExportProtocol.AP203;
                }
            }
            catch
            {
                return StepExportProtocol.AP203;
            }
        }

        private static Button CreateButton(string text, int left, int top, int width)
        {
            return new Button
            {
                Text = text,
                Location = new Point(left, top),
                Size = new Size(width, 44),
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
