using System;
using LiquidPromptWin.Elevated;

namespace LiquidPromptWin.InputCapable
{
    public class ExecutionResultWithRemainingInput : ExecutionResult
    {
        private InputCapableCliProcess _process;
        public byte[] RemainingInput => _process.BytesUnread;

        /// <summary>
        /// Initializes an instance of <see cref="ExecutionResult"/> with given output data.
        /// </summary>
        public ExecutionResultWithRemainingInput(int exitCode, string standardOutput, string standardError,
            DateTimeOffset startTime, DateTimeOffset exitTime, InputCapableCliProcess process) : base(exitCode, standardOutput, standardError, startTime, exitTime)
        {
            _process = process;
        }

    }

    public class ExecutionResult
    {
        public ExecutionResult(int exitCode, string standardOutput, string standardError, DateTimeOffset startTime,
            DateTimeOffset exitTime)
        {

        }
    }
}