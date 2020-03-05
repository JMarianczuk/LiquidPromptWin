using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using LibGit2Sharp;
using LiquidPromptWin.Elevated;
using LiquidPromptWin.InputCapable;

namespace LiquidPromptWin
{
    public class CmdWrapper
    {
        private readonly IList<string> _inputs;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly object _positionLockHandle = new object();

        public const ConsoleColor DefaultColor = ConsoleColor.Gray;

        public CmdWrapper()
        {
            _inputs = new List<string>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        private string GetArgumentsForElevatedCmd(DirectoryInfo currentWorkingDirectory, string filePath, string originalArguments)
        {
            return string.Join(" && ", $"/K cd /D {currentWorkingDirectory.FullName}",
                $"echo {filePath} {originalArguments} @ {currentWorkingDirectory.FullName}", $"{filePath} {originalArguments}");
        }

        private bool IsElevated() =>
            new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        public ICli Wrap(string input, DirectoryInfo currentWorkingDirectory, bool elevatedPermission = false, bool elevatedCommand = false)
        {
            var split = input.Split(new[] { ' ' }, 2);
            var filePath = split[0];
            string arguments = "";
            if (split.Length > 1)
            {
                arguments = split[1];
            }
            ICli wrap;
            if (elevatedPermission && !IsElevated())
            {
                wrap = new ElevatedCli(filePath);
            }
            else if (elevatedCommand && !IsElevated())
            {
                wrap = new ElevatedCli("cmd.exe");
                arguments = GetArgumentsForElevatedCmd(currentWorkingDirectory, filePath, arguments);
            }
            else
            {
                wrap = Cli.Wrap(filePath);
                //wrap = new InputCapableCli(filePath);
            }


            wrap.SetStandardOutputCallback(Console.WriteLine)
                .SetStandardErrorCallback(Console.WriteLine)
                //.SetStandardInput(Console.OpenStandardInput())
                .EnableExitCodeValidation(false)
                .SetWorkingDirectory(currentWorkingDirectory.FullName)
                .SetArguments(arguments)
                .SetCancellationToken(_cancellationTokenSource.Token);

            return wrap;
        }
        public void Initialize(string[] args)
        {
            Console.WriteLine("LiquidPrompt for Windows by Boden_Units");
            Console.WriteLine();
            var initialOutput = new List<string>();
            var cmd = Cli.Wrap("cmd.exe");
            cmd.SetStandardOutputCallback(line => initialOutput.Add(line));
            cmd.Execute();
            foreach (var ele in initialOutput.Take(initialOutput.Count - 2))
            {
                Console.WriteLine(ele);
            }
        }

        public async Task Start()
        {
            DirectoryInfo currentWorkingDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
            //ExecutionResultWithRemainingInput takeWithResult = null;
            //ConsoleKeyInfo? takeWithKey = null;
            TimeSpan timeElapsed = TimeSpan.Zero;
            while (true)
            {
                Console.WriteLine();
                WriteInfos(currentWorkingDirectory, timeElapsed);
                var inputBuilder = new StringBuilder();

                bool inputFinished = false;
                int position = 0;
                int inHistory = _inputs.Count;
                while (!inputFinished)
                {
                    //ConsoleKeyInfo key;
                    //if (takeWithKey.HasValue)
                    //{
                    //    key = takeWithKey.Value;
                    //}
                    //else
                    //{
                    //    key = Console.ReadKey(true);
                    //    if (takeWithResult != null && takeWithResult.RemainingInput.Length > 0)
                    //    {
                    //        var temp = Console.InputEncoding.GetChars(takeWithResult.RemainingInput)[0];
                    //        takeWithKey = key;
                    //        key = new ConsoleKeyInfo(temp, ConsoleKey.A, false, false, false);
                    //    }
                    //}
                    var key = Console.ReadKey(true);

                    if (key.Modifiers != 0)
                    {
                        if (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.L)
                        {
                            //PerformDiscovery(currentWorkingDirectory, inputBuilder);
                            continue;
                        }
                    }
                    if (key.Key == ConsoleKey.Enter)
                    {
                        inputFinished = true;
                    }
                    else if (IsSpecialKey(key.Key))
                    {
                        HandleSpecialKey(key.Key, currentWorkingDirectory, ref position, ref inHistory, inputBuilder);
                    }
                    else
                    {
                        inputBuilder.Append(key.KeyChar);
                        Console.Write(key.KeyChar);
                        position += 1;
                    }
                }

                var input = inputBuilder.ToString().Trim();
                if (string.IsNullOrEmpty(input))
                {
                    Console.WriteLine();
                    timeElapsed = TimeSpan.FromSeconds(0);
                    continue;
                }
                if (input == "exit")
                {
                    Console.WriteLine();
                    break;
                }

                if (input.StartsWith("cd "))
                {
                    var path = input.Split(new[] { ' ' }, 2)[1];
                    var newWorkingDirectory = TraverseDirectories(currentWorkingDirectory, path);

                    if (newWorkingDirectory != null)
                    {
                        currentWorkingDirectory = newWorkingDirectory;
                        Console.WriteLine();
                        continue;
                    }
                }

                bool executeWithElevatedPermission = false;
                bool executeCommandWithElevatedPermission = false;
                if (input.StartsWith("sudo "))
                {
                    executeWithElevatedPermission = true;
                    input = input.Split(new[] { ' ' }, 2)[1];
                }
                if (input.StartsWith("sudoc ") || input.StartsWith("sudocommand "))
                {
                    executeCommandWithElevatedPermission = true;
                    input = input.Split(new[] { ' ' }, 2)[1];
                }

                _inputs.Add(input);
                Console.WriteLine();
                var wrap = Wrap(input, currentWorkingDirectory, executeWithElevatedPermission, executeCommandWithElevatedPermission);
                try
                {
                    var result = wrap.Execute();
                    timeElapsed = result.RunTime;
                }
                catch(Exception exc)
                {
                    Console.WriteLine($"Error while executing: {exc.GetType()}: {exc.Message}");
                }

                //if (result is ExecutionResultWithRemainingInput withInput)
                //{
                //    takeWithResult = withInput;
                //}
            }
        }

        public void PerformDiscovery(DirectoryInfo currentWorkingDirectory, StringBuilder inputBuilder)
        {
            Console.WriteLine();
            var files = currentWorkingDirectory.GetFiles();
            Array.Sort(files, (left, right) => left.Name.CompareTo(right.Name));
            var fileString = string.Join(" ", (IEnumerable<FileInfo>) files);
            Console.WriteLine(fileString);
            Console.WriteLine();
            WriteInfos(currentWorkingDirectory, TimeSpan.Zero);
            Console.Write(inputBuilder.ToString());
        }

        private static readonly ConsoleKey[] SpecialKeys = {
            ConsoleKey.Backspace,
            ConsoleKey.Delete,
            ConsoleKey.LeftArrow,
            ConsoleKey.RightArrow,
            ConsoleKey.UpArrow,
            ConsoleKey.DownArrow,
            ConsoleKey.Tab
        };
        private bool IsSpecialKey(ConsoleKey key) => SpecialKeys.Contains(key);

        private void HandleSpecialKey(ConsoleKey key, DirectoryInfo currentWorkingDirectory, ref int position, ref int inHistory, StringBuilder inputBuilder)
        {
            switch (key)
            {
                case ConsoleKey.Backspace:
                    if (position <= 0)
                    {
                        break;

                    }

                    inputBuilder.Remove(position - 1, 1);
                    position -= 1;
                    MoveCursorRight(-1);
                    DelOperation(inputBuilder, position);
                    break;
                case ConsoleKey.Delete:
                    if (position >= inputBuilder.Length)
                    {
                        break;
                    }

                    inputBuilder.Remove(position, 1);
                    DelOperation(inputBuilder, position);
                    break;
                case ConsoleKey.RightArrow:
                    if (position >= inputBuilder.Length)
                    {
                        break;
                    }
                    position += 1;
                    MoveCursorRight(1);
                    break;
                case ConsoleKey.LeftArrow:
                    if (position <= 0)
                    {
                        break;
                    }
                    position -= 1;
                    MoveCursorRight(-1);
                    break;
                case ConsoleKey.UpArrow:
                    if (inHistory > 0)
                    {
                        inHistory -= 1;
                        ReplaceCurrentInput(inputBuilder, _inputs[inHistory], ref position);
                    }
                    break;
                case ConsoleKey.DownArrow:
                    if (_inputs.Count == 0)
                    {
                        ReplaceCurrentInput(inputBuilder, "", ref position);
                    }
                    else if (inHistory < _inputs.Count)
                    {
                        inHistory += 1;
                        if (inHistory < _inputs.Count)
                        {
                            ReplaceCurrentInput(inputBuilder, _inputs[inHistory], ref position);
                        }
                        else
                        {
                            ReplaceCurrentInput(inputBuilder, "", ref position);
                        }
                    }
                    break;
                case ConsoleKey.Tab:
                    HandleTabKeyPressed(currentWorkingDirectory, ref position, inputBuilder);
                    break;
                default:
                    ShowExceptionAndThrow("Missing special key handling");
                    break;
            }
        }
        private void ShowExceptionAndThrow(string text, [CallerMemberName] string caller = null)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Application has experienced an error with the following message: '{text}'");
            if (Debugger.IsAttached)
            {
                throw new Exception($"Error in {caller}: {text}");
            }
            Console.ForegroundColor = DefaultColor;
            Console.WriteLine();
        }
        private void HandleTabKeyPressed(DirectoryInfo currentWorkingDirectory, ref int position, StringBuilder inputBuilder)
        {
            var inputs = inputBuilder.ToString().Split(' ');
            var currentWord = inputs.Length > 0 ? inputs[inputs.Length - 1] : "";
            if (currentWord.Contains("="))
            {
                var split = currentWord.Split(new[] { '=' }, 2);
                currentWord = split[1];
            }
            var files = currentWorkingDirectory.GetFileSystemInfos();
            Array.Sort(files, (left, right) => left.Name.CompareTo(right.Name));
            var match = files.FirstOrDefault(f => f.Name.StartsWith(currentWord));
            if (match != null)
            {
                if (match.Name != currentWord)
                {
                    var extra = match.Name.Substring(currentWord.Length,
                        match.Name.Length - currentWord.Length);
                    Console.Write(extra);
                    inputBuilder.Append(extra);
                    position += extra.Length;
                }
                else
                {
                    var matchIndex = Array.IndexOf(files, match);
                    if (matchIndex + 1 == files.Length)
                    {
                        matchIndex = -1;
                    }

                    match = files[matchIndex + 1];
                    ReplaceCurrentTail(inputBuilder, currentWord, match.Name, ref position);
                }
            }
            else
            {
                Console.Beep();
            }
        }
        private void MoveCursorRight(int steps)
        {
            while (Console.CursorLeft + steps < 0)
            {
                Console.CursorTop -= 1;
                steps += Console.WindowWidth;
            }
            while (Console.CursorLeft + steps >= Console.WindowWidth)
            {
                Console.CursorTop += 1;
                steps -= Console.WindowWidth;
            }
            Console.CursorLeft += steps;
        }
        private void ReplaceCurrentTail(StringBuilder inputBuilder, string currentTail, string newInput, ref int position)
        {
            position -= currentTail.Length;
            MoveCursorRight(-currentTail.Length);
            DelOperation(inputBuilder, position, true);
            inputBuilder.Remove(position, currentTail.Length);
            Console.Write(newInput);
            position += newInput.Length;
            inputBuilder.Append(newInput);
        }

        private void ReplaceCurrentInput(StringBuilder inputBuilder, string newInput, ref int position)
        {
            ReplaceCurrentTail(inputBuilder, inputBuilder.ToString(), newInput, ref position);
        }

        private DirectoryInfo TraverseDirectories(DirectoryInfo currentWorkingDirectory, string path)
        {
            var parts = path.Split('/');
            DirectoryInfo working = new DirectoryInfo(currentWorkingDirectory.FullName);
            for (int i = 0; i < parts.Length; i += 1)
            {
                if (parts[i] == ".")
                {
                    continue;
                }
                if (parts[i] == "..")
                {
                    working = working.Parent;
                    continue;
                }
                var child = working.GetDirectories().FirstOrDefault(d => d.Name == parts[i]);
                if (child == null)
                {
                    working = null;
                    break;
                }
                else
                {
                    working = child;
                }
            }
            return working;
        }
        private void WriteInfos(DirectoryInfo currentWorkingDirectory, TimeSpan timeElapsed)
        {
            var infoBuilder = new CommandLineStringBuilder();
            infoBuilder.Append("$ ", ConsoleColor.Green);
            infoBuilder.Append(currentWorkingDirectory.FullName);

            var discovered = Repository.Discover(currentWorkingDirectory.FullName);
            if (!string.IsNullOrEmpty(discovered))
            {
                var repo = new Repository(discovered);
                var branch = repo.Head.FriendlyName;
                var stashes = repo.Stashes.Count();
                //var commits = repo.Commits.Count(c => c.)
                var statusOptions = new StatusOptions
                {
                    ExcludeSubmodules = true,
                    IncludeIgnored = false,
                    IncludeUnaltered = false,
                    IncludeUntracked = true,
                    RecurseIgnoredDirs = false,
                    RecurseUntrackedDirs = true
                };
                var status = repo.RetrieveStatus(statusOptions);
                infoBuilder.Append(" ");

                infoBuilder.Append($"[{branch}");
                if (stashes > 0)
                {
                    infoBuilder.Append($"[{stashes}]");
                }
                var ahead = (repo.Head.TrackingDetails.AheadBy ?? 0) - (repo.Head.TrackingDetails.BehindBy ?? 0);
                if (ahead != 0)
                {
                    var aheadString = ahead.ToString();
                    ConsoleColor aheadColor = ConsoleColor.Yellow;
                    if (ahead > 0)
                    {
                        aheadString = "+" + aheadString;
                        aheadColor = ConsoleColor.DarkGray;
                    }

                    infoBuilder.Append($"({aheadString})", aheadColor);
                }
                if (status.IsDirty)
                {
                    infoBuilder.Append($" (");

                    var added = status.Added.Count();
                    var staged = status.Staged.Count();
                    var removed = status.Removed.Count();
                    var modified = status.Modified.Count();
                    var untracked = status.Untracked.Count();
                    var missing = status.Missing.Count();

                    IList<Chunk> statusUpdates = new List<Chunk>();
                    if (added > 0)
                    {
                        statusUpdates.Add(new Chunk { Content = $"+{added}", Color = ConsoleColor.Green});
                    }
                    if (modified > 0)
                    {
                        statusUpdates.Add(new Chunk { Content = $"~{modified}", Color = ConsoleColor.Yellow });
                    }
                    if (removed > 0)
                    {
                        statusUpdates.Add(new Chunk { Content = $"-{removed}", Color = ConsoleColor.Red });
                    }
                    if ((added > 0 || modified > 0 || removed > 0) && (staged > 0 || untracked > 0))
                    {
                        statusUpdates.Add(new Chunk { Content = "|", Color = DefaultColor });
                    }
                    if (staged > 0)
                    {
                        statusUpdates.Add(new Chunk { Content = $"@{staged}", Color = ConsoleColor.Magenta });
                    }
                    if (untracked > 0)
                    {
                        statusUpdates.Add(new Chunk { Content = $"?{untracked}", Color = ConsoleColor.DarkGreen });
                    }
                    if (missing > 0)
                    {
                        statusUpdates.Add(new Chunk { Content = $"x{missing}", Color = ConsoleColor.Red });
                    }

                    infoBuilder.AppendMany(statusUpdates, new Chunk { Content = " ", Color = DefaultColor });
                    infoBuilder.Append(")");
                }

                infoBuilder.Append("]");
            }

            if (timeElapsed > TimeSpan.FromMilliseconds(500))
            {
                infoBuilder.Append($" {Math.Round(timeElapsed.TotalSeconds, 1):0.0}s", ConsoleColor.White);
            }

            infoBuilder.Append(">");
            infoBuilder.Flush();
        }
        private void DelOperation(StringBuilder inputBuilder, int position, bool clearTrailing = false)
        {
            string currentInput = inputBuilder.ToString();
            var cursorLeft = Console.CursorLeft;
            var cursorTop = Console.CursorTop;
            if (clearTrailing)
            {
                var arr = new char[currentInput.Length - position + 1];
                for (int i = 0; i < arr.Length; i += 1)
                {
                    arr[i] = ' ';
                }

                Console.Write(arr);
            }
            else
            {
                Console.Write(currentInput.Substring(position, currentInput.Length - position));
                Console.Write(' ');
            }

            Console.CursorLeft = cursorLeft;
            Console.CursorTop = cursorTop;
        }
    }
}