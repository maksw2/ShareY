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

using ShareX.HelpersLib;
using ShareX.ScreenCaptureLib.Properties;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ShareX.ScreenCaptureLib
{
    public partial class ScreenRecordForm : Form
    {
        public event Action StopRequested;
        public event Action PauseRequested;
        public event Action ResumeRequested;
        public bool UseInProcessPause { get; set; }
        public bool ExcludeFromCapture { get; set; }

        private int restartRequested;
        public bool RestartRequested => Volatile.Read(ref restartRequested) != 0;
        public bool ConsumeRestartRequest() => Interlocked.Exchange(ref restartRequested, 0) != 0;

        private readonly NoFocusBorderButton btnRestart = new NoFocusBorderButton();
        private readonly ToolStripMenuItem tsmiRestart = new ToolStripMenuItem();

        private ScreenRecordingStatus status;

        public ScreenRecordingStatus Status
        {
            get
            {
                return status;
            }
            private set
            {
                status = value;
            }
        }

        public TimeSpan Countdown { get; set; }
        public bool IsCountdown { get; private set; }
        public Stopwatch Timer { get; private set; }
        public ManualResetEvent RecordResetEvent { get; set; }

        public bool ActivateWindow { get; set; } = true;
        public float Duration { get; set; } = 0;
        public bool AskConfirmationOnAbort { get; set; } = false;

        public Rectangle RecordingRegion
        {
            get
            {
                return GetRecordingRegion(Location);
            }
        }

        protected override bool ShowWithoutActivation
        {
            get
            {
                return !ActivateWindow;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams createParams = base.CreateParams;
                createParams.ExStyle |= (int)(WindowStyles.WS_EX_TOPMOST | WindowStyles.WS_EX_TOOLWINDOW);
                return createParams;
            }
        }

        private Color borderColor = Color.Red;
        private Rectangle borderRectangle;
        private Rectangle borderRectangle0Based;
        private bool dragging;
        private Point initialLocation;
        private static int lastIconStatus = -1;
        private const int panelOffset = 3;

        public ScreenRecordForm(Rectangle regionRectangle)
        {
            InitializeComponent();
            niTray.Icon = ShareXResources.Icon;

            btnRestart.Text = Resources.ScreenRecordForm_Restart;
            btnRestart.Font = btnPause.Font;
            btnRestart.FlatStyle = btnPause.FlatStyle;
            btnRestart.Size = new Size(Math.Max(btnPause.Width, TextRenderer.MeasureText(btnRestart.Text, btnRestart.Font).Width + 12), btnPause.Height);
            btnRestart.Location = new Point(btnAbort.Right, 0);
            btnRestart.MouseClick += (sender, e) => { if (e.Button == MouseButtons.Left) RestartRecording(); };
            pInfo.Controls.Add(btnRestart);
            lblTimer.Left += btnRestart.Width;

            tsmiRestart.Text = Resources.ScreenRecordForm_Restart;
            tsmiRestart.Click += (sender, e) => RestartRecording();
            cmsMain.Items.Insert(2, tsmiRestart);
            btnRestart.Enabled = tsmiRestart.Enabled = false;
            ShareXResources.ApplyTheme(this);

            borderRectangle = regionRectangle.Offset(1);
            borderRectangle0Based = new Rectangle(0, 0, borderRectangle.Width, borderRectangle.Height);

            Location = borderRectangle.Location;
            int windowWidth = Math.Max(borderRectangle.Width, pInfo.Width);
            Size = new Size(windowWidth, borderRectangle.Height + panelOffset + pInfo.Height);
            pInfo.Location = new Point(0, borderRectangle.Height + panelOffset);

            Region region = new Region(ClientRectangle);
            region.Exclude(borderRectangle0Based.Offset(-1));
            region.Exclude(new Rectangle(0, borderRectangle.Height, windowWidth, panelOffset));
            if (borderRectangle.Width < pInfo.Width)
            {
                region.Exclude(new Rectangle(borderRectangle.Width, 0, pInfo.Width - borderRectangle.Width, borderRectangle.Height));
            }
            else if (borderRectangle.Width > pInfo.Width)
            {
                region.Exclude(new Rectangle(pInfo.Width, borderRectangle.Height + panelOffset, borderRectangle.Width - pInfo.Width, pInfo.Height));
            }
            Region = region;

            Timer = new Stopwatch();
            UpdateTimer();

            RecordResetEvent = new ManualResetEvent(false);

            ChangeState(ScreenRecordState.Waiting);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }

                if (RecordResetEvent != null)
                {
                    RecordResetEvent.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private Rectangle GetRecordingRegion(Point windowLocation)
        {
            return new Rectangle(windowLocation.X + 1, windowLocation.Y + 1, borderRectangle.Width - 2, borderRectangle.Height - 2);
        }

        public void StartStopRecording()
        {
            if (Status == ScreenRecordingStatus.Working)
            {
                AbortRecording();
            }
            else if (Status == ScreenRecordingStatus.Recording)
            {
                Status = ScreenRecordingStatus.Stopped;
                OnStopRequested();
            }
            else if (Status == ScreenRecordingStatus.Paused)
            {
                Status = ScreenRecordingStatus.Stopped;
                RecordResetEvent?.Set();
                OnStopRequested();
            }
            else
            {
                RecordResetEvent?.Set();
            }
        }

        public void RestartRecording()
        {
            bool active = Status == ScreenRecordingStatus.Recording || (UseInProcessPause && Status == ScreenRecordingStatus.Paused);
            if (!active) return;

            Interlocked.Exchange(ref restartRequested, 1);
            Status = ScreenRecordingStatus.Waiting;
            Timer.Reset();
            IsCountdown = false;
            StopRecordingTimer();
            UpdateUI();
            OnStopRequested();
            RecordResetEvent.Set();
        }

        public void PrepareForRestart()
        {
            ConsumeRestartRequest();
            RecordResetEvent.Reset();
            ChangeState(ScreenRecordState.BeforeStart);
        }

        public void PauseResumeRecording()
        {
            if (Status == ScreenRecordingStatus.Recording)
            {
                Status = ScreenRecordingStatus.Paused;
                RecordResetEvent?.Reset();
                if (UseInProcessPause)
                {
                    Timer.Stop();
                    timerRefresh.Stop();
                    PauseRequested?.Invoke();
                    UpdateUI();
                }
                else
                {
                    OnStopRequested();
                }
            }
            else if (UseInProcessPause && Status == ScreenRecordingStatus.Paused)
            {
                Status = ScreenRecordingStatus.Recording;
                Timer.Start();
                timerRefresh.Start();
                ResumeRequested?.Invoke();
                UpdateUI();
            }
            else
            {
                RecordResetEvent?.Set();
            }
        }

        public void CompleteRecording()
        {
            Status = ScreenRecordingStatus.Stopped;
            ChangeState(ScreenRecordState.RecordingEnd);
        }

        public void AbortRecording()
        {
            Status = ScreenRecordingStatus.Aborted;
            OnStopRequested();
            RecordResetEvent?.Set();
        }

        protected void OnStopRequested()
        {
            StopRequested?.Invoke();
        }

        public void StartCountdown(int milliseconds)
        {
            IsCountdown = true;
            Countdown = TimeSpan.FromMilliseconds(milliseconds);

            Timer.Start();
            timerRefresh.Start();
            UpdateTimer();
        }

        public void StartRecordingTimer()
        {
            if (IsCountdown)
            {
                Timer.Reset();
                IsCountdown = false;
            }

            if (Duration > 0)
            {
                IsCountdown = true;
                Countdown = TimeSpan.FromSeconds(Duration);
            }

            borderColor = Color.Lime;
            Refresh();

            Timer.Start();
            timerRefresh.Start();
            UpdateTimer();
        }

        public void StopRecordingTimer()
        {
            Timer.Stop();
            timerRefresh.Stop();
            UpdateTimer();
        }

        private void UpdateTimer()
        {
            if (!IsDisposed)
            {
                TimeSpan timer;

                if (IsCountdown)
                {
                    timer = Countdown - Timer.Elapsed;
                    if (timer.Ticks < 0) timer = TimeSpan.Zero;
                }
                else
                {
                    timer = Timer.Elapsed;
                }

                lblTimer.Text = timer.ToString("mm\\:ss\\:ff");
            }
        }

        private void UpdateUI()
        {
            btnRestart.Enabled = tsmiRestart.Enabled = Status is ScreenRecordingStatus.Recording or ScreenRecordingStatus.Paused;
            switch (Status)
            {
                case ScreenRecordingStatus.Working:
                    string trayTextWorking = "ShareX - " + Resources.ScreenRecordForm_StartRecording_Click_tray_icon_to_stop_recording_;
                    niTray.Text = trayTextWorking.Truncate(63);
                    niTray.Icon = Resources.control_record.ToIcon();
                    btnStart.Text = Resources.ScreenRecordForm_Stop;
                    tsmiStart.Text = Resources.ScreenRecordForm_Stop;
                    tsmiStart.Image = Resources.tick;
                    break;
                case ScreenRecordingStatus.Waiting:
                case ScreenRecordingStatus.Paused:
                    if (Status == ScreenRecordingStatus.Paused)
                    {
                        string trayTextWaiting = "ShareX - " + Resources.ScreenRecordForm_StartRecording_Click_tray_icon_to_stop_recording_;
                        niTray.Text = trayTextWaiting.Truncate(63);
                    }
                    else
                    {
                        string trayTextBeforeStart = "ShareX - " + Resources.ScreenRecordForm_StartRecording_Click_tray_icon_to_start_recording_;
                        niTray.Text = trayTextBeforeStart.Truncate(63);
                    }
                    niTray.Icon = Resources.control_record_yellow.ToIcon();
                    btnPause.Text = Resources.Resume;
                    tsmiPause.Text = Resources.Resume;
                    tsmiPause.Image = Resources.control;
                    lblTimer.Cursor = Cursors.SizeAll;
                    borderColor = Color.FromArgb(241, 196, 27);
                    Refresh();
                    break;
                case ScreenRecordingStatus.Recording:
                    niTray.Icon = Resources.control_record.ToIcon();
                    btnPause.Text = Resources.Pause;
                    tsmiPause.Text = Resources.Pause;
                    tsmiPause.Image = Resources.control_pause;
                    lblTimer.Cursor = Cursors.Default;
                    break;
            }
        }

        public void ChangeState(ScreenRecordState state)
        {
            this.InvokeSafe(() =>
            {
                switch (state)
                {
                    case ScreenRecordState.Waiting:
                        string trayTextWaiting = "ShareX - " + Resources.ScreenRecordForm_StartRecording_Waiting___;
                        niTray.Text = trayTextWaiting.Truncate(63);
                        niTray.Icon = Resources.control_record_yellow.ToIcon();
                        cmsMain.Enabled = false;
                        niTray.Visible = true;
                        break;
                    case ScreenRecordState.BeforeStart:
                        cmsMain.Enabled = true;
                        UpdateUI();
                        break;
                    case ScreenRecordState.AfterStart:
                        dragging = false;
                        Status = ScreenRecordingStatus.Working;
                        UpdateUI();
                        break;
                    case ScreenRecordState.AfterRecordingStart:
                        Status = ScreenRecordingStatus.Recording;
                        StartRecordingTimer();
                        UpdateUI();
                        break;
                    case ScreenRecordState.RecordingEnd:
                        StopRecordingTimer();
                        UpdateUI();
                        break;
                    case ScreenRecordState.Encoding:
                        Hide();
                        cmsMain.Enabled = false;
                        string trayTextAfterStop = "ShareX - " + Resources.ScreenRecordForm_StartRecording_Encoding___;
                        niTray.Text = trayTextAfterStop.Truncate(63);
                        niTray.Icon = Resources.camcorder__pencil.ToIcon();
                        break;
                }
            });
        }

        public void ChangeStateProgress(int progress)
        {
            niTray.Text = $"ShareX - {Resources.ScreenRecordForm_StartRecording_Encoding___} {progress}%";

            if (niTray.Visible && lastIconStatus != progress)
            {
                Icon icon;

                if (progress >= 0)
                {
                    try
                    {
                        icon = Helpers.GetProgressIcon(progress, Color.FromArgb(140, 0, 36));
                    }
                    catch (Exception e)
                    {
                        DebugHelper.WriteException(e);
                        progress = -1;
                        if (lastIconStatus == progress) return;
                        icon = Resources.camcorder__pencil.ToIcon();
                    }
                }
                else
                {
                    icon = Resources.camcorder__pencil.ToIcon();
                }

                using (Icon oldIcon = niTray.Icon)
                {
                    niTray.Icon = icon;
                    oldIcon.DisposeHandle();
                }

                lastIconStatus = progress;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (Pen pen1 = new Pen(ShareXResources.Theme.BorderColor) { DashPattern = new float[] { 5, 5 } })
            using (Pen pen2 = new Pen(borderColor) { DashPattern = new float[] { 5, 5 }, DashOffset = 5 })
            {
                e.Graphics.DrawRectangleProper(pen1, borderRectangle0Based);
                e.Graphics.DrawRectangleProper(pen2, borderRectangle0Based);
            }

            base.OnPaint(e);
        }

        private void ScreenRegionForm_Shown(object sender, EventArgs e)
        {
            if (ExcludeFromCapture && !NativeMethods.SetWindowDisplayAffinity(Handle, 0x11))
            {
                DebugHelper.WriteLine("Could not exclude recording controls from capture. Win32 error: {0}", Marshal.GetLastWin32Error());
            }

            if (ExcludeFromCapture)
            {
                Region region = Region;
                Region = region.Clone();
                region.Dispose();
            }

            if (ActivateWindow)
            {
                this.ForceActivate();
            }
        }

        private void ScreenRecordForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Status != ScreenRecordingStatus.Stopped)
            {
                AbortRecording();
            }
        }

        private void timerRefresh_Tick(object sender, EventArgs e)
        {
            UpdateTimer();
        }

        private void btnStart_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                StartStopRecording();
            }
        }

        private void btnPause_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                PauseResumeRecording();
            }
        }

        private void btnAbort_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (!AskConfirmationOnAbort || MessageBox.Show(Resources.ScreenRecordForm_ConfirmCancel, "ShareX", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    AbortRecording();
                }
            }
        }

        private void lblTimer_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && (Status == ScreenRecordingStatus.Waiting || Status == ScreenRecordingStatus.Paused))
            {
                dragging = true;
                initialLocation = e.Location;
            }
        }

        private void lblTimer_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point newLocation = new Point(Location.X + e.X - initialLocation.X, Location.Y + e.Y - initialLocation.Y);
                Rectangle recordingRegion = GetRecordingRegion(newLocation);
                if (CaptureHelpers.GetScreenBounds().Contains(recordingRegion))
                {
                    Location = newLocation;
                    Update();
                }
                else
                {
                    initialLocation = e.Location;
                }
            }
        }

        private void lblTimer_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = false;
            }
        }
    }
}