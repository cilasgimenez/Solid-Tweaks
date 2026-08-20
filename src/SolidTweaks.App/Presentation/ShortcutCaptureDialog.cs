using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SolidTweaks.Infrastructure.SolidWorks;
using SolidTweaks.Shared;

namespace SolidTweaks.Presentation
{
    internal sealed class ShortcutCaptureDialog : Form
    {
        private readonly Label validationLabel;
        private bool allowClose;

        public ShortcutCaptureDialog(string actionName, ShortcutBinding current)
        {
            Text = UiText.Get("Set shortcut") + " - " + actionName;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(430, current == null ? 142 : 178);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            BackColor = UiPalette.Surface;
            Font = new Font("Segoe UI", 9F);

            var instruction = new Label
            {
                AutoSize = false,
                Location = new Point(22, 20),
                Size = new Size(386, 34),
                Text = UiText.Get("Press the desired key for the shortcut..."),
                Font = new Font("Segoe UI Semibold", 11F),
                ForeColor = UiPalette.Text,
                TextAlign = ContentAlignment.MiddleCenter
            };
            validationLabel = new Label
            {
                AutoSize = false,
                Location = new Point(22, 58),
                Size = new Size(386, 34),
                ForeColor = UiPalette.Error,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var cancel = new Button
            {
                Text = UiText.Get("Cancel"),
                Size = new Size(112, 32),
                Location = new Point(296, current == null ? 98 : 134),
                Cursor = Cursors.Hand
            };
            cancel.Click += delegate
            {
                allowClose = true;
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Controls.Add(instruction);
            Controls.Add(validationLabel);
            Controls.Add(cancel);

            if (current != null)
            {
                var currentLabel = new Label
                {
                    AutoSize = false,
                    Location = new Point(22, 94),
                    Size = new Size(252, 28),
                    Text = UiText.Format("Current key: {0}", SolidWorksKeyboardShortcutService.ShortcutDisplayName(current)),
                    ForeColor = UiPalette.Muted,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                var clear = new Button
                {
                    Text = UiText.Get("Clear key"),
                    Size = new Size(122, 32),
                    Location = new Point(22, 134),
                    Cursor = Cursors.Hand
                };
                clear.Click += delegate
                {
                    ClearRequested = true;
                    allowClose = true;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                Controls.Add(currentLabel);
                Controls.Add(clear);
            }

            Shown += delegate { Activate(); };
            Deactivate += delegate
            {
                if (!allowClose && IsHandleCreated)
                {
                    BeginInvoke(new Action(Activate));
                }
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!allowClose)
                {
                    e.Cancel = true;
                }
            };
        }

        public ShortcutBinding CapturedBinding { get; private set; }
        public bool ClearRequested { get; private set; }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            Keys modifiers = keyData & Keys.Modifiers;
            Keys key = keyData & Keys.KeyCode;
            if (modifiers != Keys.None || IsUnsupportedKey(key))
            {
                validationLabel.Text = UiText.Get("Use one key without Ctrl, Alt, or Shift.");
                return true;
            }

            int virtualKey = (int)key;
            int mapped = (int)MapVirtualKey((uint)virtualKey, 0);
            int scanCode = mapped & 0xFF;
            bool extended = IsExtendedKey(key);
            CompleteCapture(ShortcutBinding.Keyboard(virtualKey, scanCode, extended));
            return true;
        }

        private void CompleteCapture(ShortcutBinding binding)
        {
            CapturedBinding = binding;
            allowClose = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static bool IsUnsupportedKey(Keys key)
        {
            return key == Keys.None || key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu ||
                key == Keys.LControlKey || key == Keys.RControlKey || key == Keys.LShiftKey || key == Keys.RShiftKey ||
                key == Keys.LMenu || key == Keys.RMenu || key == Keys.LWin || key == Keys.RWin ||
                key == Keys.Apps || key == Keys.ProcessKey || key == Keys.Packet;
        }

        private static bool IsExtendedKey(Keys key)
        {
            return key == Keys.Insert || key == Keys.Delete || key == Keys.Home || key == Keys.End ||
                key == Keys.PageUp || key == Keys.PageDown || key == Keys.Left || key == Keys.Right ||
                key == Keys.Up || key == Keys.Down || key == Keys.Divide || key == Keys.NumLock;
        }

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint code, uint mapType);
    }
}
