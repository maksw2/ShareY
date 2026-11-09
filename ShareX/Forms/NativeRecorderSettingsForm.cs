#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using ShareX.ScreenRecordingLib;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShareX
{
    public sealed class NativeRecorderSettingsForm : Form
    {
        public NativeRecorderSettingsForm(TaskSettingsCapture settings)
        {
            Text = "Native recording options";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(420, 300);

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 2,
                RowCount = 7
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            Controls.Add(layout);

            NumericUpDown bitrate = new NumericUpDown
            {
                Minimum = 100,
                Maximum = 200000,
                Value = Math.Clamp(settings.ScreenRecordVideoBitrate, 100, 200000),
                Dock = DockStyle.Fill
            };
            layout.Controls.Add(new Label { Text = "Video bitrate (kbps)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            layout.Controls.Add(bitrate, 1, 0);

            CheckBox systemAudio = new CheckBox { Text = "Capture system audio", Checked = settings.ScreenRecordSystemAudio, AutoSize = true };
            CheckBox microphone = new CheckBox { Text = "Capture microphone", Checked = settings.ScreenRecordMicrophone, AutoSize = true };
            CheckBox camera = new CheckBox { Text = "Camera overlay", Checked = settings.ScreenRecordCamera, AutoSize = true };
            CheckBox hardware = new CheckBox { Text = "Require hardware encoder", Checked = settings.ScreenRecordRequireHardwareEncoder, AutoSize = true };
            layout.SetColumnSpan(systemAudio, 2);
            layout.SetColumnSpan(microphone, 2);
            layout.SetColumnSpan(camera, 2);
            layout.SetColumnSpan(hardware, 2);
            layout.Controls.Add(systemAudio, 0, 1);
            layout.Controls.Add(microphone, 0, 2);
            layout.Controls.Add(camera, 0, 3);
            layout.Controls.Add(hardware, 0, 4);

            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            Button save = new Button { Text = "Save", DialogResult = DialogResult.OK };
            Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            layout.SetColumnSpan(buttons, 2);
            layout.Controls.Add(buttons, 0, 6);
            AcceptButton = save;
            CancelButton = cancel;

            save.Click += (sender, args) =>
            {
                settings.ScreenRecordVideoBitrate = (int)bitrate.Value;
                settings.ScreenRecordSystemAudio = systemAudio.Checked;
                settings.ScreenRecordMicrophone = microphone.Checked;
                settings.ScreenRecordCamera = camera.Checked;
                settings.ScreenRecordRequireHardwareEncoder = hardware.Checked;
            };
        }
    }
}
