using System;
using System.Drawing;
using System.Windows.Forms;
using SolidTweaks.Shared;

namespace SolidTweaks.SolidWorksAddin
{
    internal sealed class SheetMetalDxfExportDialog : Form
    {
        private bool selectedOnly;
        private readonly CheckBox bendLinesCheckBox;
        private bool updatingPreference;

        private SheetMetalDxfExportDialog(bool hasSelection)
        {
            Text = UiText.Get("Export Sheet Metal DXF");
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(460, 154);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.White;

            Controls.Add(new Label
            {
                AutoSize = false,
                Text = hasSelection
                    ? UiText.Get("Choose which sheet-metal bodies to export:")
                    : UiText.Get("No body is selected. Export all sheet-metal bodies:"),
                Location = new Point(18, 16),
                Size = new Size(424, 22),
                TextAlign = ContentAlignment.MiddleLeft
            });

            var selectedButton = CreateButton(UiText.Get("Selected only"), 18, 52, 204);
            selectedButton.Enabled = hasSelection;
            selectedButton.Click += delegate
            {
                selectedOnly = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            var allButton = CreateButton(UiText.Get("All sheet-metal bodies"), 230, 52, 212);
            allButton.Click += delegate
            {
                selectedOnly = false;
                DialogResult = DialogResult.OK;
                Close();
            };

            bendLinesCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(18, 105),
                Text = UiText.Get("Bend lines"),
                Checked = SolidTweaksFeatureSettings.Read(
                    SolidTweaksFeatureSettings.DxfBendLinesPreferenceId,
                    true),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = true
            };
            bendLinesCheckBox.CheckedChanged += BendLinesCheckBoxOnCheckedChanged;

            AcceptButton = hasSelection ? selectedButton : allButton;
            Controls.Add(selectedButton);
            Controls.Add(allButton);
            Controls.Add(bendLinesCheckBox);
        }

        public static bool ShowExport(
            IntPtr ownerHandle,
            bool hasSelection,
            out bool selectedOnly,
            out bool includeBendLines)
        {
            using (var dialog = new SheetMetalDxfExportDialog(hasSelection))
            {
                DialogResult result = ownerHandle == IntPtr.Zero
                    ? dialog.ShowDialog()
                    : dialog.ShowDialog(new WindowHandle(ownerHandle));
                selectedOnly = dialog.selectedOnly;
                includeBendLines = dialog.bendLinesCheckBox.Checked;
                return result == DialogResult.OK;
            }
        }

        private void BendLinesCheckBoxOnCheckedChanged(object sender, EventArgs e)
        {
            if (updatingPreference)
            {
                return;
            }
            try
            {
                SolidTweaksFeatureSettings.Write(
                    SolidTweaksFeatureSettings.DxfBendLinesPreferenceId,
                    bendLinesCheckBox.Checked);
            }
            catch (Exception exception)
            {
                updatingPreference = true;
                bendLinesCheckBox.Checked = !bendLinesCheckBox.Checked;
                updatingPreference = false;
                MessageBox.Show(
                    this,
                    UiText.Format("Could not save the Bend lines option.\n\n{0}", UiText.UserMessage(exception)),
                    "Solid Tweaks",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
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
