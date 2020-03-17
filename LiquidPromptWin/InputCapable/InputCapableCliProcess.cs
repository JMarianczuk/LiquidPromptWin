using System;
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
            Action<string> standardOutputObserver = null, Action<string> standardErrorObserver = null)
        {
            _nativeProcess = new Process { StartInfo = startInfo };

            _nativeProcess.StartInfo.CreateNoWindow = true;
            _nativeProcess.StartInfo.RedirectStandardOutput = true;
            _nativeProcess.StartInfo.RedirectStandardError = true;
            _nativeProcess.StartInfo.RedirectStandardInput = true;
            _nativeProcess.StartInfo.UseShellExecute = false;

            _nativeProcess.EnableRaisingEvents = true;
            _nativeProcess.Exited += (sender, args) =>
            {
                ExitTime = DateTimeOffset.Now;

                _exitSignal.Release();
            };

            _nativeProcess.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    _standardOutputBuffer.AppendLine(args.Data);
                    standardOutputObserver?.Invoke(args.Data);
                }
            };

            // Wire stderr
            _nativeProcess.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    _standardErrorBuffer.AppendLine(args.Data);
                    standardErrorObserver?.Invoke(args.Data);
                }
            };
        }

        public void Start()
        {
            _nativeProcess.Start();

            StartTime = DateTimeOffset.Now;

            _isReading = true;
        }

        public Task PipeStandardInput(Stream stream, CancellationToken token)
        {
            // Copy stream and close stdin
            return Task.Run(async () =>
            {
                using (_nativeProcess.StandardInput)
                {
                    int bufferSize = 1;
                    _buffer = new byte[bufferSize];
                    while (!token.IsCancellationRequested)
                    {
                        var readTask = stream.ReadAsync(_buffer, 0, bufferSize);
                        try
                        {
                            //ReadAsync will not react to passing the token, but WaitAll will throw if the token cancels
                            Task.WaitAll(new[] { readTask }, token);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }

                        if (_bytesCount > 0)
                        {
                            _hasUnreadBytes = true;
                            try
                            {
                                await _nativeProcess.StandardInput.BaseStream.WriteAsync(_buffer, 0, _bytesCount, token);
                            }
                            catch (TaskCanceledException)
                            {
                                break;
                            }

                            _hasUnreadBytes = false;
                        }
                    }

                    InputPipingFinished = true;
                }
            }, token);
        }

        public void WaitForExit()
        {
            _exitSignal.Wait();
        }

        public async Task WaitForExitAsync()
        {
            await _exitSignal.WaitAsync();

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
            //if (_isReading)
            //{
            //    _nativeProcess.CancelOutputRead();
            //    _nativeProcess.CancelErrorRead();

            //    _isReading = false;
            //}

            // Dispose dependencies
            _nativeProcess.Dispose();
            _exitSignal.Dispose();
            _standardOutputEndSignal.Dispose();
            _standardErrorEndSignal.Dispose();
        }

    }
}