﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace EmergenceGuardian.FFmpeg {

    #region Interface

    /// <summary>
    /// Executes commands through FFmpeg assembly.
    /// </summary>
    public interface IFFmpegProcess {
        /// <summary>
        /// Gets or sets the options to control the behaviors of the process.
        /// </summary>
        ProcessStartOptions Options { get; set; }
        /// <summary>
        /// Gets the process currently being executed.
        /// </summary>
        Process WorkProcess { get; }
        /// <summary>
        /// Occurs when the application writes to its output stream.
        /// </summary>
        event DataReceivedEventHandler DataReceived;
        /// <summary>
        /// Occurs after stream info is read from FFmpeg's output.
        /// </summary>
        event EventHandler InfoUpdated;
        /// <summary>
        /// Occurs when status update is received through FFmpeg's output stream.
        /// </summary>
        event StatusUpdatedEventHandler StatusUpdated;
        /// <summary>
        /// Occurs when the process has terminated its work.
        /// </summary>
        event CompletedEventHandler Completed;
        /// <summary>
        /// Returns the raw console output from FFmpeg.
        /// </summary>
        string Output { get; }
        /// <summary>
        /// Returns the duration of input file.
        /// </summary>
        TimeSpan FileDuration { get; }
        /// <summary>
        /// Returns the frame count of input file (estimated).
        /// </summary>
        long FrameCount { get; }
        /// <summary>
        /// Returns information about input streams.
        /// </summary>
        List<FFmpegStreamInfo> FileStreams { get; }
        /// <summary>
        /// Returns the CompletionStatus of the last operation.
        /// </summary>
        CompletionStatus LastCompletionStatus { get; }
        /// <summary>
        /// Returns the last status data received from DataReceived event.
        /// </summary>
        FFmpegStatus LastStatusReceived { get; }
        /// <summary>
        /// Runs FFmpeg with specified arguments.
        /// </summary>
        /// <param name="arguments">FFmpeg startup arguments.</param>
        /// <returns>The process completion status.</returns>
        CompletionStatus RunFFmpeg(string arguments, ProcessOutput output = ProcessOutput.Error);
        /// <summary>
        /// Runs FFmpeg with specified arguments through avs2yuv.
        /// </summary>
        /// <param name="source">The path of the source Avisynth script file.</param>
        /// <param name="arguments">FFmpeg startup arguments.</param>
        /// <returns>The process completion status.</returns>
        CompletionStatus RunAvisynthToEncoder(string source, string arguments);
        /// <summary>
        /// Runs an encoder (FFmpeg by default) with specified arguments through avs2yuv.
        /// </summary>
        /// <param name="source">The path of the source Avisynth script file.</param>
        /// <param name="arguments">FFmpeg startup arguments.</param>
        /// <param name="encoderPath">The path of the encoder to run.</param>
        /// <param name="encoderApp">The type of encoder to run, which alters parsing.</param>
        /// <returns>The process completion status.</returns>
        CompletionStatus RunAvisynthToEncoder(string source, string arguments, EncoderApp encoderApp, string encoderPath);
        /// <summary>
        /// Runs avs2yuv with specified source file. The output will be discarded.
        /// </summary>
        /// <param name="path">The path to the script to run.</param>
        CompletionStatus RunAvisynth(string path, ProcessOutput output = ProcessOutput.Error);
        /// <summary>
        /// Runs the command as 'cmd /c', allowing the use of command line features such as piping.
        /// </summary>
        /// <param name="cmd">The full command to be executed with arguments.</param>
        /// <param name="encoder">The type of application being run, which alters parsing.</param>
        /// <returns>The process completion status.</returns>
        CompletionStatus RunAsCommand(string cmd, EncoderApp encoder, ProcessOutput output = ProcessOutput.Error);
        /// <summary>
        /// Runs specified application with specified arguments.
        /// </summary>
        /// <param name="fileName">The application to start.</param>
        /// <param name="arguments">The set of arguments to use when starting the application.</param>
        /// <returns>The process completion status.</returns>
        CompletionStatus Run(string fileName, string arguments, ProcessOutput output = ProcessOutput.Error);
        /// <summary>
        /// Runs specified application with specified arguments.
        /// </summary>
        /// <param name="fileName">The application to start.</param>
        /// <param name="arguments">The set of arguments to use when starting the application.</param>
        /// <param name="encoder">The type of application being run, which alters parsing.</param>
        /// <param name="nestedProcess">If true, killing the process with kill all sub-processes.</param>
        /// <returns>The process completion status.</returns>
        CompletionStatus Run(string fileName, string arguments, EncoderApp encoder, bool nestedProcess, ProcessOutput output);
        /// <summary>
        /// Cancels the currently running job and terminate its process.
        /// </summary>
        void Cancel();
        /// <summary>
        /// Gets the first video stream from FileStreams.
        /// </summary>
        /// <returns>A FFmpegVideoStreamInfo object.</returns>
        FFmpegVideoStreamInfo VideoStream { get; }
        /// <summary>
        /// Gets the first audio stream from FileStreams.
        /// </summary>
        /// <returns>A FFmpegAudioStreamInfo object.</returns>
        FFmpegAudioStreamInfo AudioStream { get; }
        /// <summary>
        /// Returns the full command with arguments being run.
        /// </summary>
        string CommandWithArgs { get; }
    }

    #endregion

    /// <summary>
    /// Executes commands through FFmpeg assembly.
    /// </summary>
    public class FFmpegProcess : IFFmpegProcess {

        #region Declarations / Constructors

        /// <summary>
        /// Gets or sets the configuration settings for FFmpeg.
        /// </summary>
        public IFFmpegConfig Config { get; set; }
        /// <summary>
        /// Gets or sets the options to control the behaviors of the process.
        /// </summary>
        public ProcessStartOptions Options { get; set; }
        /// <summary>
        /// Gets the process currently being executed.
        /// </summary>
        public Process WorkProcess { get; private set; }
        /// <summary>
        /// Occurs when the application writes to its output stream.
        /// </summary>
        public event DataReceivedEventHandler DataReceived;
        /// <summary>
        /// Occurs after stream info is read from FFmpeg's output.
        /// </summary>
        public event EventHandler InfoUpdated;
        /// <summary>
        /// Occurs when status update is received through FFmpeg's output stream.
        /// </summary>
        public event StatusUpdatedEventHandler StatusUpdated;
        /// <summary>
        /// Occurs when the process has terminated its work.
        /// </summary>
        public event CompletedEventHandler Completed;
        /// <summary>
        /// Returns the raw console output from FFmpeg.
        /// </summary>
        public string Output {
            get { return output.ToString(); }
        }
        /// <summary>
        /// Returns the duration of input file.
        /// </summary>
        public TimeSpan FileDuration { get; private set; }
        /// <summary>
        /// Returns the frame count of input file (estimated).
        /// </summary>
        public long FrameCount { get; private set; }
        /// <summary>
        /// Returns information about input streams.
        /// </summary>
        public List<FFmpegStreamInfo> FileStreams { get; private set; }
        /// <summary>
        /// Returns the CompletionStatus of the last operation.
        /// </summary>
        public CompletionStatus LastCompletionStatus { get; private set; }
        /// <summary>
        /// Returns the last status data received from DataReceived event.
        /// </summary>
        public FFmpegStatus LastStatusReceived { get; private set; }

        private StringBuilder output;
        private EncoderApp encoder;
        private bool isStarted;
        private CancellationTokenSource cancelWork;

        public FFmpegProcess() : this(null, null) { }

        public FFmpegProcess(IFFmpegConfig config) : this(config, null) { }

        public FFmpegProcess(IFFmpegConfig config, ProcessStartOptions options) {
            this.Config = config ?? new FFmpegConfig();
            this.Options = options ?? new ProcessStartOptions();
        }

        #endregion

        /// <summary>
        /// Runs FFmpeg with specified arguments.
        /// </summary>
        /// <param name="arguments">FFmpeg startup arguments.</param>
        /// <returns>The process completion status.</returns>
        public CompletionStatus RunFFmpeg(string arguments, ProcessOutput output = ProcessOutput.Error) {
            return Run(Config.FFmpegPath, arguments, EncoderApp.FFmpeg, false, output);
        }

        /// <summary>
        /// Runs FFmpeg with specified arguments through avs2yuv.
        /// </summary>
        /// <param name="source">The path of the source Avisynth script file.</param>
        /// <param name="arguments">FFmpeg startup arguments.</param>
        /// <returns>The process completion status.</returns>
        public CompletionStatus RunAvisynthToEncoder(string source, string arguments) {
            return RunAvisynthToEncoder(source, arguments, EncoderApp.FFmpeg, null);
        }

        /// <summary>
        /// Runs an encoder (FFmpeg by default) with specified arguments through avs2yuv.
        /// </summary>
        /// <param name="source">The path of the source Avisynth script file.</param>
        /// <param name="arguments">FFmpeg startup arguments.</param>
        /// <param name="encoderPath">The path of the encoder to run.</param>
        /// <param name="encoderApp">The type of encoder to run, which alters parsing.</param>
        /// <returns>The process completion status.</returns>
        public CompletionStatus RunAvisynthToEncoder(string source, string arguments, EncoderApp encoderApp, string encoderPath) {
            if (!File.Exists(Config.Avs2yuvPath))
                throw new FileNotFoundException(string.Format(@"File ""{0}"" specified by Config.Avs2yuvPath is not found.", Config.Avs2yuvPath));
            String Query = string.Format(@"""{0}"" ""{1}"" -o - | ""{2}"" {3}", Config.Avs2yuvPath, source, encoderPath ?? Config.FFmpegPath, arguments);
            return RunAsCommand(Query, encoderApp);
        }

        /// <summary>
        /// Runs avs2yuv with specified source file. The output will be discarded.
        /// </summary>
        /// <param name="path">The path to the script to run.</param>
        public CompletionStatus RunAvisynth(string path, ProcessOutput output = ProcessOutput.Error) {
            if (!File.Exists(Config.Avs2yuvPath))
                throw new FileNotFoundException(string.Format(@"File ""{0}"" specified by Config.Avs2yuvPath is not found.", Config.Avs2yuvPath));
            string TempFile = path + ".out";
            string Args = string.Format(@"""{0}"" -o {1}", path, TempFile);
            CompletionStatus Result = Run(Config.Avs2yuvPath, Args, EncoderApp.Other, false, output);
            File.Delete(TempFile);
            return Result;
        }

        /// <summary>
        /// Runs the command as 'cmd /c', allowing the use of command line features such as piping.
        /// </summary>
        /// <param name="cmd">The full command to be executed with arguments.</param>
        /// <param name="encoder">The type of application being run, which alters parsing.</param>
        /// <returns>The process completion status.</returns>
        public CompletionStatus RunAsCommand(string cmd, EncoderApp encoder, ProcessOutput output = ProcessOutput.Error) {
            return Run("cmd", string.Format(@"/c "" {0} """, cmd), encoder, true, output);
        }

        /// <summary>
        /// Runs specified application with specified arguments.
        /// </summary>
        /// <param name="fileName">The application to start.</param>
        /// <param name="arguments">The set of arguments to use when starting the application.</param>
        /// <returns>The process completion status.</returns>
        public CompletionStatus Run(string fileName, string arguments, ProcessOutput output = ProcessOutput.Error) {
            return Run(fileName, arguments, EncoderApp.Other, false, output);
        }

        /// <summary>
        /// Runs specified application with specified arguments.
        /// </summary>
        /// <param name="fileName">The application to start.</param>
        /// <param name="arguments">The set of arguments to use when starting the application.</param>
        /// <param name="encoder">The type of application being run, which alters parsing.</param>
        /// <param name="nestedProcess">If true, killing the process with kill all sub-processes.</param>
        /// <returns>The process completion status.</returns>
        public CompletionStatus Run(string fileName, string arguments, EncoderApp encoder, bool nestedProcess, ProcessOutput output) {
            if (!File.Exists(Config.FFmpegPathAbsolute))
                throw new FileNotFoundException(string.Format(@"File ""{0}"" specified by Config.FFmpegPath is not found.", Config.FFmpegPath));
            if (WorkProcess != null)
                throw new InvalidOperationException("This instance of FFmpeg is busy. You can run concurrent commands by creating other class instances.");

            Process P = new Process();
            this.encoder = encoder;
            WorkProcess = P;
            this.output = new StringBuilder();
            isStarted = false;
            FileStreams = null;
            FileDuration = TimeSpan.Zero;
            cancelWork = new CancellationTokenSource();
            if (Options == null)
                Options = new ProcessStartOptions();
            FrameCount = Options.FrameCount;

            P.StartInfo.FileName = fileName;
            P.StartInfo.Arguments = arguments;
            if (output == ProcessOutput.Standard)
                P.OutputDataReceived += FFmpeg_DataReceived;
            else if (output == ProcessOutput.Error)
                P.ErrorDataReceived += FFmpeg_DataReceived;

            if (Options.DisplayMode != FFmpegDisplayMode.Native) {
                if (Options.DisplayMode == FFmpegDisplayMode.Interface && Config.UserInterfaceManager != null)
                    Config.UserInterfaceManager.Display(this);
                P.StartInfo.CreateNoWindow = true;
                P.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                if (output == ProcessOutput.Standard)
                    P.StartInfo.RedirectStandardOutput = true;
                else if (output == ProcessOutput.Error)
                    P.StartInfo.RedirectStandardError = true;
                P.StartInfo.UseShellExecute = false;
            }

            Options.RaiseStarted(this);

            P.Start();
            try {
                if (!P.HasExited)
                    P.PriorityClass = Options.Priority;
            } catch { }
            if (Options.DisplayMode != FFmpegDisplayMode.Native) {
                if (output == ProcessOutput.Standard)
                    P.BeginOutputReadLine();
                else if (output == ProcessOutput.Error)
                    P.BeginErrorReadLine();
            }

            bool Timeout = Wait();

            // ExitCode is 0 for normal exit. Different value when closing the console.
            CompletionStatus Result = Timeout ? CompletionStatus.Timeout : cancelWork.IsCancellationRequested ? CompletionStatus.Cancelled : P.ExitCode == 0 ? CompletionStatus.Success : CompletionStatus.Error;

            isStarted = false;
            cancelWork = null;
            LastCompletionStatus = Result;
            Completed?.Invoke(this, new CompletedEventArgs(Result));
            if ((Result == CompletionStatus.Error || Result == CompletionStatus.Timeout) && Options.DisplayMode == FFmpegDisplayMode.ErrorOnly)
                Config.UserInterfaceManager?.DisplayError(this);

            WorkProcess = null;
            return Result;
        }

        /// <summary>
        /// Waits for the process to terminate while allowing the cancellation token to kill the process.
        /// </summary>
        /// <returns>Whether a timeout occured.</returns>
        private bool Wait() {
            DateTime StartTime = DateTime.Now;
            while (!WorkProcess.HasExited) {
                if (cancelWork.Token.IsCancellationRequested && !WorkProcess.HasExited)
                    Config.SoftKill(WorkProcess);
                if (Options.Timeout > TimeSpan.Zero && DateTime.Now - StartTime > Options.Timeout) {
                    Config.SoftKill(WorkProcess);
                    return true;
                }
                WorkProcess.WaitForExit(500);
            }
            WorkProcess.WaitForExit();
            return false;


            //using (var waitHandle = new SafeWaitHandle(WorkProcess.Handle, false)) {
            //    using (var processFinishedEvent = new ManualResetEvent(false)) {
            //        processFinishedEvent.SafeWaitHandle = waitHandle;

            //        int index = WaitHandle.WaitAny(new[] { processFinishedEvent, cancelWork.Token.WaitHandle }, Options.Timeout);
            //        if (index > 0) {
            //            if (nestedProcess)
            //                KillProcessAndChildren(WorkProcess.Id);
            //            else if (!WorkProcess.HasExited)
            //                WorkProcess.Kill();
            //        }
            //        WorkProcess.WaitForExit();
            //        return (index == WaitHandle.WaitTimeout);
            //    }
            //}
        }

        /// <summary>
        /// Cancels the currently running job and terminate its process.
        /// </summary>
        public void Cancel() {
            cancelWork?.Cancel();
        }

        /// <summary>
        /// Occurs when data is received from the executing application.
        /// </summary>
        private void FFmpeg_DataReceived(object sender, DataReceivedEventArgs e) {
            if (e.Data == null) {
                // We're reading both Output and Error streams, only parse on 2nd null.
                if (!isStarted && encoder != EncoderApp.Other)
                    ParseFileInfo();
                return;
            }

            output.AppendLine(e.Data);
            DataReceived?.Invoke(sender, e);

            if (encoder == EncoderApp.FFmpeg) {
                if (FileStreams == null && (e.Data.StartsWith("Output ") || e.Data.StartsWith("Press [q] to stop")))
                    ParseFileInfo();
                if (e.Data.StartsWith("Press [q] to stop") || e.Data.StartsWith("frame="))
                    isStarted = true;

                if (isStarted && e.Data.StartsWith("frame=")) {
                    FFmpegStatus ProgressInfo = FFmpegParser.ParseFFmpegProgress(e.Data);
                    LastStatusReceived = ProgressInfo;
                    StatusUpdated?.Invoke(this, new StatusUpdatedEventArgs(ProgressInfo));
                }
            } else if (encoder == EncoderApp.x264) {
                if (!isStarted && e.Data.StartsWith("frames "))
                    isStarted = true;
                else if (isStarted && e.Data.Length == 48) {
                    FFmpegStatus ProgressInfo = FFmpegParser.ParseX264Progress(e.Data);
                    LastStatusReceived = ProgressInfo;
                    StatusUpdated?.Invoke(this, new StatusUpdatedEventArgs(ProgressInfo));
                }
            }
        }

        //private bool HasParsed = false;
        private void ParseFileInfo() {
            //HasParsed = true;
            TimeSpan fileDuration;
            FileStreams = FFmpegParser.ParseFileInfo(output.ToString(), out fileDuration);
            FileDuration = fileDuration;
            if (Options.FrameCount > 0)
                FrameCount = Options.FrameCount;
            else if (VideoStream != null)
                FrameCount = (int)(FileDuration.TotalSeconds * VideoStream.FrameRate);
            InfoUpdated?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// Gets the first video stream from FileStreams.
        /// </summary>
        /// <returns>A FFmpegVideoStreamInfo object.</returns>
        public FFmpegVideoStreamInfo VideoStream {
            get {
                if (FileStreams != null && FileStreams.Count > 0)
                    return (FFmpegVideoStreamInfo)FileStreams.FirstOrDefault(f => f.StreamType == FFmpegStreamType.Video);
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the first audio stream from FileStreams.
        /// </summary>
        /// <returns>A FFmpegAudioStreamInfo object.</returns>
        public FFmpegAudioStreamInfo AudioStream {
            get {
                if (FileStreams != null && FileStreams.Count > 0)
                    return (FFmpegAudioStreamInfo)FileStreams.FirstOrDefault(f => f.StreamType == FFmpegStreamType.Audio);
                else
                    return null;
            }
        }

        /// <summary>
        /// Returns the full command with arguments being run.
        /// </summary>
        public string CommandWithArgs {
            get {
                return string.Format(@"""{0}"" {1}", WorkProcess.StartInfo.FileName, WorkProcess.StartInfo.Arguments);
            }
        }
    }
}
