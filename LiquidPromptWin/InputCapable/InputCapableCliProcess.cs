﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiquidPromptWin.Elevated;

namespace LiquidPromptWin.InputCapable
{
    public class InputCapableCliProcess : IDisposable
    {
        private readonly Process _nativeProcess;
        private readonly Signal _exitSignal = new Signal();
        private readonly StringBuilder _standardOutputBuffer = new StringBuilder();
        private readonly Signal _standardOutputEndSignal = new Signal();
        private readonly StringBuilder _standardErrorBuffer = new StringBuilder();
        private readonly Signal _standardErrorEndSignal = new Signal();

        private readonly CancellationTokenSource _inputTokenSource = new CancellationTokenSource();

        private bool _isReading;

        public DateTimeOffset StartTime { get; private set; }

        public DateTimeOffset ExitTime { get; private set; }

        public bool InputPipingFinished { get; private set; } = false;
        private bool _hasUnreadBytes = false;
        private byte[] _buffer;
        private int _bytesCount;
        public byte[] BytesUnread
        {
            get
            {
                if (!_inputTokenSource.IsCancellationRequested)
                {
                    return null;
                }
                if (!InputPipingFinished)
                {
                    return null;
                }
                if (!_hasUnreadBytes)
                {
                    return new byte[0];
                }

                return _buffer;
            }
        }

        public int Id => _nativeProcess.Id;

        public int ExitCode => _nativeProcess.ExitCode;

        public string StandardOutput => _standardOutputBuffer.ToString();

        public string StandardError => _standardErrorBuffer.ToString();

        public InputCapableCliProcess(ProcessStartInfo startInfo,
            Action<string> standardOutputObserver = null, Action<string> standardErrorObserver = null,
            Action standardOutputClosedObserver = null, Action standardErrorClosedObserver = null)
        {
            // Create underlying process
            _nativeProcess = new Process { StartInfo = startInfo };

            // Configure start info
            _nativeProcess.StartInfo.CreateNoWindow = true;
            _nativeProcess.StartInfo.RedirectStandardOutput = true;
            _nativeProcess.StartInfo.RedirectStandardError = true;
            _nativeProcess.StartInfo.RedirectStandardInput = true;
            _nativeProcess.StartInfo.UseShellExecute = false;

            // Wire exit event
            _nativeProcess.EnableRaisingEvents = true;
            _nativeProcess.Exited += (sender, args) =>
            {
                // Record exit time
                ExitTime = DateTimeOffset.Now;

                // Release signal
                _exitSignal.Release();
            };

            // Wire stdout
            _nativeProcess.OutputDataReceived += (sender, args) =>
            {
                // Actual data
                if (args.Data != null)
                {
                    // Write to buffer and invoke observer
                    _standardOutputBuffer.AppendLine(args.Data);
                    standardOutputObserver?.Invoke(args.Data);
                }
                // Null means end of stream
                else
                {
                    // Release signal
                    standardOutputClosedObserver?.Invoke();
                    _standardOutputEndSignal.Release();
                }
            };

            // Wire stderr
            _nativeProcess.ErrorDataReceived += (sender, args) =>
            {
                // Actual data
                if (args.Data != null)
                {
                    // Write to buffer and invoke observer
                    _standardErrorBuffer.AppendLine(args.Data);
                    standardErrorObserver?.Invoke(args.Data);
                }
                // Null means end of stream
                else
                {
                    // Release signal
                    standardErrorClosedObserver?.Invoke();
                    _standardErrorEndSignal.Release();
                }
            };
        }

        public void Start()
        {
            // Start process
            _nativeProcess.Start();

            // Record start time
            StartTime = DateTimeOffset.Now;

            // Begin reading streams
            _nativeProcess.BeginOutputReadLine();
            _nativeProcess.BeginErrorReadLine();

            // Set flag
            _isReading = true;
        }

        public void PipeStandardInput(Stream stream)
        {
            var token = _inputTokenSource.Token;
            // Copy stream and close stdin
            Task.Run(async () =>
            {
                using (_nativeProcess.StandardInput)
                {
                    int bufferSize = 1;
                    _buffer = new byte[bufferSize];
                    while (!token.IsCancellationRequested)
                    {
                        stream.Read(_buffer, 0, bufferSize);

                        if (token.IsCancellationRequested)
                        {
                            break;
                        }

                        if (_bytesCount > 0)
                        {
                            _hasUnreadBytes = true;
                            try
                            {
                                await _nativeProcess.StandardInput.BaseStream.WriteAsync(_buffer, 0, _bytesCount,
                                    token);
                            }
                            catch (TaskCanceledException tcexc)
                            {
                                break;
                            }

                            _hasUnreadBytes = false;
                        }
                    }

                    InputPipingFinished = true;
                }
            });
        }

        public async Task PipeStandardInputAsync(Stream stream)
        {
            var token = _inputTokenSource.Token;
            // Copy stream and close stdin
            using (_nativeProcess.StandardInput)
                await stream.CopyToAsync(_nativeProcess.StandardInput.BaseStream);
        }

        public void WaitForExit()
        {
            // Wait until process exits
            _exitSignal.Wait();

            _inputTokenSource.Cancel();

            // Wait until streams finished reading
            _standardOutputEndSignal.Wait();
            _standardErrorEndSignal.Wait();
        }

        public async Task WaitForExitAsync()
        {
            // Wait until process exits
            await _exitSignal.WaitAsync();

            // Wait until streams finished reading
            await _standardOutputEndSignal.WaitAsync();
            await _standardErrorEndSignal.WaitAsync();
        }

        public bool TryKill(bool killEntireProcessTree)
        {
            try
            {
#if NET45
                if (killEntireProcessTree)
                    ProcessEx.KillProcessTree(_nativeProcess.Id);
                else
                    _nativeProcess.Kill();
#elif NETCOREAPP3_0
                _nativeProcess.Kill(killEntireProcessTree);
#else
                // .NET std doesn't let us kill the entire process tree
                _nativeProcess.Kill();
#endif

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                // It's possible that stdout/stderr streams are still alive after killing the process.
                // We forcefully release signals because we're not interested in the output at this point anyway.
                _standardOutputEndSignal.Release();
                _standardErrorEndSignal.Release();
            }
        }

        public void Dispose()
        {
            // Unsubscribe from process events
            // (process may still trigger events even after getting disposed)
            _nativeProcess.EnableRaisingEvents = false;
            if (_isReading)
            {
                _nativeProcess.CancelOutputRead();
                _nativeProcess.CancelErrorRead();

                _isReading = false;
            }

            // Dispose dependencies
            _nativeProcess.Dispose();
            _exitSignal.Dispose();
            _standardOutputEndSignal.Dispose();
            _standardErrorEndSignal.Dispose();
        }

    }
}