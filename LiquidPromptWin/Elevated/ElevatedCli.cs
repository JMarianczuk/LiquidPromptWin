using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Models;

namespace LiquidPromptWin.Elevated
{
    public class ElevatedCli : ICli
    {
        private readonly string _filePath;

        private string _workingDirectory;
        private string _arguments;
        private Stream _standardInput = Stream.Null;
        private readonly IDictionary<string, string> _environmentVariables = new Dictionary<string, string>();
        private Encoding _standardOutputEncoding = Console.OutputEncoding;
        private Encoding _standardErrorEncoding = Console.OutputEncoding;
        private Action<string> _standardOutputObserver;
        private Action _standardOutputClosedObserver;
        private Action<string> _standardErrorObserver;
        private Action _standardErrorClosedObserver;
        private CancellationToken _cancellationToken;
        private bool _killEntireProcessTree;
        private bool _exitCodeValidation = true;
        private bool _standardErrorValidation;

        /// <inheritdoc />
        public int? ProcessId { get; private set; }

        /// <summary>
        /// Initializes an instance of <see cref="Cli"/> on the target executable.
        /// </summary>
        public ElevatedCli(string filePath)
        {
            _filePath = filePath;
        }

        #region Options

        /// <inheritdoc />
        public ICli SetWorkingDirectory(string workingDirectory)
        {
            _workingDirectory = workingDirectory;
            return this;
        }

        /// <inheritdoc />
        public ICli SetArguments(string arguments)
        {
            _arguments = arguments;
            return this;
        }

        /// <inheritdoc />
        public ICli SetArguments(IReadOnlyList<string> arguments)
        {
            var buffer = new StringBuilder();

            foreach (var argument in arguments)
            {
                // If buffer has something in it - append a space
                if (buffer.Length != 0)
                    buffer.Append(' ');

                // If argument is clean and doesn't need escaping - append it directly
                if (argument.Length != 0 && argument.All(c => !char.IsWhiteSpace(c) && c != '"'))
                {
                    buffer.Append(argument);
                }
                // Otherwise - escape problematic characters
                else
                {
                    // Escaping logic taken from CoreFx source code

                    buffer.Append('"');

                    for (var i = 0; i < argument.Length;)
                    {
                        var c = argument[i++];

                        if (c == '\\')
                        {
                            var numBackSlash = 1;
                            while (i < argument.Length && argument[i] == '\\')
                            {
                                numBackSlash++;
                                i++;
                            }

                            if (i == argument.Length)
                            {
                                buffer.Append('\\', numBackSlash * 2);
                            }
                            else if (argument[i] == '"')
                            {
                                buffer.Append('\\', numBackSlash * 2 + 1);
                                buffer.Append('"');
                                i++;
                            }
                            else
                            {
                                buffer.Append('\\', numBackSlash);
                            }
                        }
                        else if (c == '"')
                        {
                            buffer.Append('\\');
                            buffer.Append('"');
                        }
                        else
                        {
                            buffer.Append(c);
                        }
                    }

                    buffer.Append('"');
                }
            }

            return SetArguments(buffer.ToString());
        }

        /// <inheritdoc />
        public ICli SetStandardInput(Stream standardInput)
        {
            _standardInput = standardInput;
            return this;
        }

        /// <inheritdoc />
        public ICli SetStandardInput(string standardInput, Encoding encoding)
        {
            // Represent string as stream
            var data = encoding.GetBytes(standardInput);
            var stream = new MemoryStream(data, false);

            return SetStandardInput(stream);
        }

        /// <inheritdoc />
        public ICli SetStandardInput(string standardInput) => SetStandardInput(standardInput, Console.InputEncoding);

        /// <inheritdoc />
        public ICli SetEnvironmentVariable(string key, string value)
        {
            _environmentVariables[key] = value;
            return this;
        }

        /// <inheritdoc />
        public ICli SetStandardOutputEncoding(Encoding encoding)
        {
            _standardOutputEncoding = encoding;
            return this;
        }

        /// <inheritdoc />
        public ICli SetStandardErrorEncoding(Encoding encoding)
        {
            _standardErrorEncoding = encoding;
            return this;
        }

        /// <inheritdoc />
        public ICli SetStandardOutputCallback(Action<string> callback)
        {
            _standardOutputObserver = callback;
            return this;
        }

        /// <inheritdoc />
        public ICli SetStandardOutputClosedCallback(Action callback)
        {
            _standardOutputClosedObserver = callback;
            return this;
        }

        /// <inheritdoc />
        public ICli SetStandardErrorCallback(Action<string> callback)
        {
            _standardErrorObserver = callback;
            return this;
        }

        /// <inheritdoc />
        public ICli SetStandardErrorClosedCallback(Action callback)
        {
            _standardErrorClosedObserver = callback;
            return this;
        }

        /// <inheritdoc />
        public ICli SetCancellationToken(CancellationToken token, bool killEntireProcessTree)
        {
            _cancellationToken = token;
            _killEntireProcessTree = killEntireProcessTree;
            return this;
        }

        /// <inheritdoc />
        public ICli SetCancellationToken(CancellationToken token)
        {
            _cancellationToken = token;
            return this;
        }

        /// <inheritdoc />
        public ICli EnableExitCodeValidation(bool isEnabled = true)
        {
            _exitCodeValidation = isEnabled;
            return this;
        }

        /// <inheritdoc />
        public ICli EnableStandardErrorValidation(bool isEnabled = true)
        {
            _standardErrorValidation = isEnabled;
            return this;
        }

        #endregion

        #region Execute

        private ElevatedCliProcess StartProcess()
        {
            // Create process start info
            var startInfo = new ProcessStartInfo
            {
                FileName = _filePath,
                WorkingDirectory = _workingDirectory,
                Arguments = _arguments,
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
            var process = new ElevatedCliProcess(
                startInfo,
                _standardOutputObserver,
                _standardErrorObserver,
                _standardOutputClosedObserver,
                _standardErrorClosedObserver
            );
            process.Start();

            return process;
        }

        private void ValidateExecutionResult(ExecutionResult result)
        {
            
        }

        /// <inheritdoc />
        public ExecutionResult Execute()
        {
            ExecuteAndForget();
            return new ExecutionResult(0, "", "", DateTime.UtcNow, DateTime.UtcNow);
        }

        public Task<ExecutionResult> ExecuteAsync()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void ExecuteAndForget()
        {
            // Set up execution context
            using (var process = StartProcess())
            {
                ProcessId = process.Id;
            }
        }

        #endregion
    }
}