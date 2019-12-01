using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiquidPromptWin.Elevated
{
    public class Signal : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);

        public void Release() => _semaphore.Release();

        public void Wait() => _semaphore.Wait();

        public Task WaitAsync() => _semaphore.WaitAsync();

        public void Dispose() => _semaphore.Dispose();
    }
    public class ElevatedCliProcess : IDisposable
    {
        private readonly Process _nativeProcess;
        private readonly Signal _exitSignal = new Signal();
        private readonly StringBuilder _standardOutputBuffer = new StringBuilder();
        private readonly Signal _standardOutputEndSignal = new Signal();
        private readonly StringBuilder _standardErrorBuffer = new StringBuilder();
        private readonly Signal _standardErrorEndSignal = new Signal();

        public int Id => _nativeProcess.Id;

        public ElevatedCliProcess(ProcessStartInfo startInfo,
            Action<string> standardOutputObserver = null, Action<string> standardErrorObserver = null,
            Action standardOutputClosedObserver = null, Action standardErrorClosedObserver = null)
        {
            // Create underlying process
            _nativeProcess = new Process { StartInfo = startInfo };

            // Configure start info
            _nativeProcess.StartInfo.CreateNoWindow = false;
            _nativeProcess.StartInfo.UseShellExecute = true;
            _nativeProcess.StartInfo.Verb = "runas";
        }

        public void Start()
        {
            // Start process
            _nativeProcess.Start();
        }

        public void Dispose()
        {
            // Unsubscribe from process events
            // (process may still trigger events even after getting disposed)
            _nativeProcess.EnableRaisingEvents = false;

            // Dispose dependencies
            _nativeProcess.Dispose();
            _exitSignal.Dispose();
            _standardOutputEndSignal.Dispose();
            _standardErrorEndSignal.Dispose();
        }
    }
}