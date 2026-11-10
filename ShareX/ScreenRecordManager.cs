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
using ShareX.ScreenCaptureLib;
using ShareX.ScreenRecordingLib;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using NativeScreenRecorder = ShareX.ScreenRecordingLib.ScreenRecorder;

namespace ShareX
{
    public static class ScreenRecordManager
    {
        public static bool IsRecording { get; private set; }

        private static NativeScreenRecorder nativeRecorder;
        private static ScreenRecordForm recordForm;

        public static void StartStopRecording(ScreenRecordOutput outputType, ScreenRecordStartMethod startMethod, TaskSettings taskSettings)
        {
            if (IsRecording)
            {
                if (recordForm != null && !recordForm.IsDisposed)
                {
                    recordForm.StartStopRecording();
                }
            }
            else
            {
                StartRecording(outputType, taskSettings, startMethod);
            }
        }

        public static void StopRecording()
        {
            if (recordForm?.RestartRequested == true)
            {
                nativeRecorder?.RequestAbort();
                return;
            }

            if (recordForm?.Status is ScreenRecordingStatus.Waiting or ScreenRecordingStatus.Working)
            {
                recordForm.AbortRecording();
                return;
            }

            if (recordForm?.Status == ScreenRecordingStatus.Aborted)
            {
                nativeRecorder?.RequestAbort();
            }
            else
            {
                nativeRecorder?.RequestStop();
            }
        }

        public static void PauseScreenRecording()
        {
            if (IsRecording && recordForm != null && !recordForm.IsDisposed)
            {
                recordForm.PauseResumeRecording();
            }
        }

        public static void AbortRecording()
        {
            if (IsRecording && recordForm != null && !recordForm.IsDisposed)
            {
                recordForm.AbortRecording();
            }
        }

        private static void StartRecording(ScreenRecordOutput outputType, TaskSettings taskSettings, ScreenRecordStartMethod startMethod)
        {
            if (taskSettings.GeneralSettings.ToastWindowAutoHide)
            {
                NotificationForm.CloseActiveForm();
            }

            bool isGif = outputType == ScreenRecordOutput.GIF;
            Rectangle region = Rectangle.Empty;
            TaskMetadata metadata = new TaskMetadata();

            switch (startMethod)
            {
                case ScreenRecordStartMethod.Region:
                    if (taskSettings.CaptureSettings.ScreenRecordTransparentRegion)
                    {
                        RegionCaptureTasks.GetRectangleRegionTransparent(out region);
                    }
                    else
                    {
                        RegionCaptureTasks.GetRectangleRegion(out region, out WindowInfo windowInfo, taskSettings.CaptureSettings.SurfaceOptions);
                        metadata.UpdateInfo(windowInfo);
                    }
                    break;
                case ScreenRecordStartMethod.ActiveWindow:
                    region = taskSettings.CaptureSettings.CaptureClientArea
                        ? CaptureHelpers.GetActiveWindowClientRectangle()
                        : CaptureHelpers.GetActiveWindowRectangle();
                    metadata.UpdateInfo(new WindowInfo(NativeMethods.GetForegroundWindow()));
                    break;
                case ScreenRecordStartMethod.CustomRegion:
                    region = taskSettings.CaptureSettings.CaptureCustomRegion;
                    break;
                case ScreenRecordStartMethod.LastRegion:
                    region = Program.Settings.ScreenRecordRegion;
                    break;
            }

            region = Rectangle.Intersect(region, CaptureHelpers.GetScreenBounds());
            if (!isGif)
            {
                region = CaptureHelpers.EvenRectangleSize(region);
            }

            if (IsRecording || nativeRecorder != null || !region.IsValid())
            {
                return;
            }

            Program.Settings.ScreenRecordRegion = region;
            IsRecording = true;
            recordForm = new ScreenRecordForm(region)
            {
                ActivateWindow = startMethod == ScreenRecordStartMethod.Region,
                Duration = taskSettings.CaptureSettings.ScreenRecordFixedDuration ? taskSettings.CaptureSettings.ScreenRecordDuration : 0,
                AskConfirmationOnAbort = taskSettings.CaptureSettings.ScreenRecordAskConfirmationOnAbort,
                UseInProcessPause = true,
                ExcludeFromCapture = true
            };
            recordForm.StopRequested += StopRecording;
            recordForm.PauseRequested += () => nativeRecorder?.Pause();
            recordForm.ResumeRequested += () => nativeRecorder?.Resume(recordForm.RecordingRegion);
            recordForm.Show();

            string path = null;
            string error = null;
            bool aborted = false;
            bool completed = false;

            Task.Run(async () =>
            {
                string temporaryPath = null;
                try
                {
                    string extension = isGif ? "gif" : "mp4";
                    string folder = TaskHelpers.GetScreenshotsFolder(taskSettings, metadata);
                    string name = TaskHelpers.GetFileName(taskSettings, extension, metadata);
                    path = TaskHelpers.HandleExistsFile(folder, name, taskSettings);
                    if (string.IsNullOrEmpty(path))
                    {
                        aborted = true;
                        return;
                    }

                    temporaryPath = Path.Combine(Path.GetDirectoryName(path),
                        $"{Path.GetFileNameWithoutExtension(path)}-recording-{Guid.NewGuid():N}.{extension}");
                    var settings = taskSettings.CaptureSettings;
                    var options = new RecordingOptions
                    {
                        OutputPath = temporaryPath,
                        Region = recordForm.RecordingRegion,
                        FramesPerSecond = isGif ? Math.Clamp(settings.GIFFPS, 1, 100) : Math.Clamp(settings.ScreenRecordFPS, 1, 240),
                        VideoBitrate = settings.ScreenRecordVideoBitrate * 1000,
                        CaptureSystemAudio = !isGif && settings.ScreenRecordSystemAudio,
                        CaptureMicrophone = !isGif && settings.ScreenRecordMicrophone,
                        SystemAudioDeviceId = settings.ScreenRecordSystemAudioDeviceId,
                        MicrophoneDeviceId = settings.ScreenRecordMicrophoneDeviceId,
                        CaptureCamera = settings.ScreenRecordCamera,
                        CameraDeviceId = settings.ScreenRecordCameraDeviceId,
                        RequireHardwareEncoder = settings.ScreenRecordRequireHardwareEncoder,
                        IncludeCursor = settings.ScreenRecordShowCursor,
                        Duration = settings.ScreenRecordFixedDuration ? TimeSpan.FromSeconds(settings.ScreenRecordDuration) : TimeSpan.Zero
                    };

                    bool restarting = false;
                    while (true)
                    {
                        if (restarting) recordForm.InvokeSafe(() => recordForm.PrepareForRestart());
                        try
                        {
                            nativeRecorder = new NativeScreenRecorder(options);
                            nativeRecorder.Diagnostic += message => DebugHelper.WriteLine("Native recorder: " + message);
                            nativeRecorder.EncodingProgressChanged += progress =>
                            {
                                recordForm.InvokeSafe(() =>
                                {
                                    if (progress == 0)
                                    {
                                        recordForm.ChangeState(ScreenRecordState.RecordingEnd);
                                        recordForm.ChangeState(ScreenRecordState.Encoding);
                                    }
                                    recordForm.ChangeStateProgress(progress);
                                });
                            };

                            recordForm.ChangeState(ScreenRecordState.BeforeStart);
                            await nativeRecorder.PrepareAsync();

                            if (!settings.ScreenRecordAutoStart && !restarting)
                            {
                                recordForm.RecordResetEvent.WaitOne();
                            }
                            else if (settings.ScreenRecordStartDelay > 0)
                            {
                                int delay = checked((int)(settings.ScreenRecordStartDelay * 1000));
                                recordForm.InvokeSafe(() => recordForm.StartCountdown(delay));
                                recordForm.RecordResetEvent.WaitOne(delay);
                            }

                            if (recordForm.Status is ScreenRecordingStatus.Aborted or ScreenRecordingStatus.Stopped)
                            {
                                aborted = true;
                                nativeRecorder.RequestAbort();
                                return;
                            }

                            if (recordForm.RecordingRegion != options.Region)
                            {
                                await nativeRecorder.DisposeAsync();
                                nativeRecorder = new NativeScreenRecorder(options with { Region = recordForm.RecordingRegion });
                                nativeRecorder.Diagnostic += message => DebugHelper.WriteLine("Native recorder: " + message);
                                nativeRecorder.EncodingProgressChanged += progress =>
                                    recordForm.InvokeSafe(() =>
                                    {
                                        if (progress == 0)
                                        {
                                            recordForm.ChangeState(ScreenRecordState.RecordingEnd);
                                            recordForm.ChangeState(ScreenRecordState.Encoding);
                                        }
                                        recordForm.ChangeStateProgress(progress);
                                    });
                                await nativeRecorder.PrepareAsync();
                            }

                            recordForm.ChangeState(ScreenRecordState.AfterStart);
                            await nativeRecorder.StartAsync();
                            if (recordForm.Status == ScreenRecordingStatus.Aborted)
                            {
                                nativeRecorder.RequestAbort();
                            }
                            else if (recordForm.Status == ScreenRecordingStatus.Stopped)
                            {
                                nativeRecorder.RequestStop();
                            }
                            else
                            {
                                recordForm.ChangeState(ScreenRecordState.AfterRecordingStart);
                            }

                            RecordingResult result = await nativeRecorder.Completion;
                            DebugHelper.WriteLine("Native recording: {0}, frames: {1}, dropped: {2}",
                                result.Encoder.Name, result.VideoFrames, result.DroppedVideoFrames);
                            aborted = recordForm.Status == ScreenRecordingStatus.Aborted;
                            if (recordForm.RestartRequested) { restarting = true; continue; }
                            if (!aborted)
                            {
                                File.Move(temporaryPath, path, true);
                                completed = true;
                                TaskHelpers.PlayNotificationSoundAsync(NotificationSound.ActionCompleted, taskSettings);
                            }
                            break;
                        }
                        catch (OperationCanceledException) when (recordForm?.RestartRequested == true)
                        {
                        }
                        finally
                        {
                            if (nativeRecorder != null)
                            {
                                await nativeRecorder.DisposeAsync();
                                nativeRecorder = null;
                            }
                            FileHelpers.DeleteFile(temporaryPath);
                        }
                        restarting = true;
                    }
                }
                catch (OperationCanceledException) when (recordForm?.Status is ScreenRecordingStatus.Aborted or ScreenRecordingStatus.Stopped)
                {
                    aborted = true;
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                    DebugHelper.WriteException(exception);
                }
                finally
                {
                    if (nativeRecorder != null)
                    {
                        await nativeRecorder.DisposeAsync();
                        nativeRecorder = null;
                    }
                    if (!completed && !string.IsNullOrEmpty(temporaryPath))
                    {
                        FileHelpers.DeleteFile(temporaryPath);
                    }
                    if (recordForm != null)
                    {
                        recordForm.InvokeSafe(() =>
                        {
                            recordForm.CompleteRecording();
                            recordForm.Close();
                            recordForm.Dispose();
                            recordForm = null;
                        });
                    }
                }
            }).ContinueInCurrentContext(() =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    MessageBox.Show(error, "ShareX - Screen recording", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                if (!aborted && completed && File.Exists(path) && TaskHelpers.ShowAfterCaptureForm(taskSettings, out string customFileName, null, path))
                {
                    if (!string.IsNullOrEmpty(customFileName))
                    {
                        string originalName = Path.GetFileNameWithoutExtension(path);
                        if (!originalName.Equals(customFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            path = FileHelpers.RenameFile(path, customFileName + Path.GetExtension(path));
                        }
                    }
                    TaskManager.Start(WorkerTask.CreateFileJobTask(path, metadata, taskSettings, customFileName));
                }
                IsRecording = false;
            });
        }
    }
}
