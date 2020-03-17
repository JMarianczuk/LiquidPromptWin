using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;

namespace LiquidPromptWin.Elevated
{
    public class ElevatedCli
    {
        private readonly string _filePath;

        private string _workingDirectory;
        private string _arguments;
        private readonly IDictionary<string, string> _environmentVariables = new Dictionary<string, string>();

        public int? ProcessId { get; private set; }

        public ElevatedCli(string filePath)
        {
            _filePath = filePath;
        }

        #region Options

        public ElevatedCli SetWorkingDirectory(string workingDirectory)
        {
            _workingDirectory = workingDirectory;
            return this;
        }

        public ElevatedCli SetArguments(string arguments)
        {
            _arguments = arguments;
            return this;
        }

        public ElevatedCli SetArguments(IReadOnlyList<string> arguments)
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
                startInfo
            );
            process.Start();

            return process;
        }

        public void Execute()
        {
            var tSource = new TaskCompletionSource<int>();
            using (var process = StartProcess())
            {
                ProcessId = process.Id;
            }
        }

        #endregion
    }
}