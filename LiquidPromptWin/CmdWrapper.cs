using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using CliWrap;
using LibGit2Sharp;

namespace LiquidPromptWin
{
    public class CmdWrapper
    {
        private readonly ICli _cmd;
        private readonly IList<string> _inputs;

        public CmdWrapper()
        {
            _cmd = Cli.Wrap("cmd.exe");
            _inputs = new List<string>();
        }

        public void Initialize()
        {
            Console.WriteLine("LiquidPrompt for Windows by Boden_Units");
            Console.WriteLine();
            var initialOutput = new List<string>();
            _cmd.SetStandardOutputCallback(line => initialOutput.Add(line));
            _cmd.Execute();
            foreach (var ele in initialOutput.Take(initialOutput.Count - 2))
            {
                Console.WriteLine(ele);
            }

            _cmd.SetStandardOutputCallback(Console.WriteLine);
            _cmd.SetStandardErrorCallback(Console.WriteLine);
            _cmd.EnableExitCodeValidation(false);
        }

        public void Start()
        {
            DirectoryInfo currentWorkingDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (true)
            {
                Console.WriteLine();
                WriteInfos(currentWorkingDirectory);
                var inputBuilder = new StringBuilder();

                bool inputFinished = false;
                int position = 0;
                int inHistory = _inputs.Count;
                while (!inputFinished)
                {
                    var key = Console.ReadKey(true);
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

                var input = inputBuilder.ToString();
                if (input == "exit")
                {
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

                _inputs.Add(input);
                Console.WriteLine();
                _cmd.SetWorkingDirectory(currentWorkingDirectory.FullName).SetArguments($"/C {input}").Execute();
            }
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
                    if (position <= 0) break;
                    inputBuilder.Remove(position - 1, 1);
                    position -= 1;
                    Console.CursorLeft -= 1;
                    DelOperation(inputBuilder.ToString(), position);
                    break;
                case ConsoleKey.Delete:
                    if (position >= inputBuilder.Length) break;
                    inputBuilder.Remove(position, 1);
                    DelOperation(inputBuilder.ToString(), position);
                    break;
                case ConsoleKey.RightArrow:
                    if (position >= inputBuilder.Length) break;
                    position += 1;
                    Console.CursorLeft += 1;
                    break;
                case ConsoleKey.LeftArrow:
                    if (position <= 0) break;
                    position -= 1;
                    Console.CursorLeft -= 1;
                    break;
                case ConsoleKey.UpArrow:
                    if (inHistory > 0)
                    {
                        inHistory -= 1;
                        ReplaceCurrentInput(inputBuilder, _inputs[inHistory], ref position);
                    }
                    break;
                case ConsoleKey.DownArrow:
                    inHistory += 1;
                    if (inHistory < _inputs.Count)
                    {
                        ReplaceCurrentInput(inputBuilder, _inputs[inHistory], ref position);
                    }
                    else
                    {
                        ReplaceCurrentInput(inputBuilder, "", ref position);
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
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
        }
        private void HandleTabKeyPressed(DirectoryInfo currentWorkingDirectory, ref int position, StringBuilder inputBuilder)
        {
            var inputs = inputBuilder.ToString().Split(' ');
            var currentWord = inputs.Length > 0 ? inputs[inputs.Length - 1] : "";
            var files = currentWorkingDirectory.GetFiles();
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
                    ReplaceCurrentInput(inputBuilder, match.Name, ref position);
                }
            }
            else
            {
                Console.Beep();
            }
        }
        private void ReplaceCurrentInput(StringBuilder inputBuilder, string newInput, ref int position)
        {
            var currentWord = inputBuilder.ToString();
            position -= currentWord.Length;
            Console.CursorLeft -= currentWord.Length;
            DelOperation(inputBuilder.ToString(), position, true);
            inputBuilder.Remove(position, currentWord.Length);
            Console.Write(newInput);
            position += newInput.Length;
            inputBuilder.Append(newInput);
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
        private void WriteInfos(DirectoryInfo currentWorkingDirectory)
        {
            var infoBuilder = new CommandLineStringBuilder();
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
                        statusUpdates.Add(new Chunk { Content = "|", Color = ConsoleColor.White });
                    }
                    if (staged > 0)
                    {
                        statusUpdates.Add(new Chunk { Content = $"@{staged}", Color = ConsoleColor.Magenta });
                    }
                    if (untracked > 0)
                    {
                        statusUpdates.Add(new Chunk { Content = $"?{untracked}", Color = ConsoleColor.Gray });
                    }
                    if (missing > 0)
                    {
                        statusUpdates.Add(new Chunk { Content = $"x{missing}", Color = ConsoleColor.Red });
                    }

                    infoBuilder.AppendMany(statusUpdates, new Chunk { Content = " ", Color = ConsoleColor.White });
                    infoBuilder.Append(")");
                }

                infoBuilder.Append("]");

            }

            infoBuilder.Append(">");
            infoBuilder.Flush();
        }
        private void DelOperation(string currentInput, int position, bool clearTrailing = false)
        {
            var cursor = Console.CursorLeft;
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

            Console.CursorLeft = cursor;
        }
    }
}