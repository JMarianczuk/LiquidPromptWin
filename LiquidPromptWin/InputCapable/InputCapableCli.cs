using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Exceptions;

namespace LiquidPromptWin.InputCapable
{
    public class InputCapableCli
    {
        private readonly string _filePath;

        private string _workingDirectory;
        private string _arguments;
        private Stream _standardInput = Stream.Null;
        private readonly IDictionary<string, string> _environmentVariables = new Dictionary<string, string>();
        private Encoding _standardOutputEncoding = Console.OutputEncoding;
        private Encoding _standardErrorEncoding = Console.OutputEncoding;
        private Action<string> _standardOutputObserver;
        private Action<string> _standardErrorObserver;
        private bool _exitCodeValidation = true;
        private bool _standardErrorValidation;

        
        public int? ProcessId { get; private set; }

        public InputCapableCli(string filePath)
        {
            _filePath = filePath;
        }

        #region Options

        
        public InputCapableCli SetWorkingDirectory(string workingDirectory)
        {
            _workingDirectory = workingDirectory;
            return this;
        }

        
        public InputCapableCli SetArguments(string arguments)
        {
            _arguments = arguments;
            return this;
        }
        
        public InputCapableCli SetStandardInput(Stream standardInput)
        {
            _standardInput = standardInput;
            return this;
        }
        
        public InputCapableCli SetEnvironmentVariable(string key, string value)
        {
            _environmentVariables[key] = value;
            return this;
        }

        
        public InputCapableCli SetStandardOutputCallback(Action<string> callback)
        {
            _standardOutputObserver = callback;
            return this;
        }

        
        public InputCapableCli SetStandardErrorCallback(Action<string> callback)
        {
            _standardErrorObserver = callback;
            return this;
        }

        
        public InputCapableCli EnableExitCodeValidation(bool isEnabled = true)
        {
            _exitCodeValidation = isEnabled;
            return this;
        }

        
        public InputCapableCli EnableStandardErrorValidation(bool isEnabled = true)
        {
            _standardErrorValidation = isEnabled;
            return this;
        }

        #endregion

        #region Execute

        private InputCapableCliProcess StartProcess()
        {
            // Create process start info
            var startInfo = new ProcessStartInfo
            {
                FileName = _filePath,
                WorkingDirectory = _workingDirectory,
                Arguments = _arguments,
                StandardOutputEncoding = _standardOutputEncoding,
                StandardErrorEncoding = _standardErrorEncoding
            };

            // Set environment variables
#if NET45
            foreach (var variable in _environmentVariables)
                startInfo.EnvironmentVariables[variable.Key] = variable.Value;
#else
            foreach (var variable in _environmentVariables)
                startInfo.Environment[variable.Key] = variable.Value;
#endif

            // Create and start process
            var process = new InputCapableCliProcess(
                startInfo,
                _standardOutputObserver,
                _standardErrorObserver
            );
            process.Start();

            return process;
        }
        
        public void Execute()
        {
            using (var process = StartProcess())
            {
                ProcessId = process.Id;

                var pipingTokenSource = new CancellationTokenSource();
                var piper = process.PipeStandardInput(_standardInput, pipingTokenSource.Token);

                process.WaitForExit();

                pipingTokenSource.Cancel();
                piper.Wait();

                var result = new ExecutionResultWithRemainingInput(process.ExitCode,
                    process.StandardOutput,
                    process.StandardError,
                    process.StartTime,
                    process.ExitTime,
                    process);

                return;
            }
        }

        #endregion

    }
}