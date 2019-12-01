using System;
using System.Collections.Generic;
using System.Linq;

namespace LiquidPromptWin
{
    public class CommandLineStringBuilder
    {
        private readonly IList<Chunk> _chunks;
        public CommandLineStringBuilder()
        {
            _chunks = new List<Chunk>();
        }

        public void Append(string content, ConsoleColor color = CmdWrapper.DefaultColor)
        {
            Append(new Chunk { Content = content, Color = color });
        }
        private void Append(Chunk chunk)
        {
            _chunks.Add(chunk);
        }
        public void AppendMany(IEnumerable<Chunk> chunks, Chunk separator)
        {
            bool first = true;
            foreach (var c in chunks)
            {
                if (first)
                {
                    Append(c.Copy());
                    first = false;
                }
                else
                {
                    Append(separator.Copy());
                    Append(c.Copy());
                }
            }
        }
        public void Flush()
        {
            if (_chunks.Count == 0)
            {
                return;
            }

            var combinedChunks = new List<Chunk> {_chunks[0]};
            Chunk last = _chunks[0];
            foreach (var c in _chunks.Skip(1))
            {
                if (last.Color == c.Color)
                {
                    last.Content += c.Content;
                }
                else
                {
                    combinedChunks.Add(c);
                    last = c;
                }
            }

            foreach (var c in combinedChunks)
            {
                Console.ForegroundColor = c.Color;
                Console.Write(c.Content);
            }
            Console.ForegroundColor = CmdWrapper.DefaultColor;
        }
    }
    public class Chunk
    {
        public string Content { get; set; }
        public ConsoleColor Color { get; set; }

        public Chunk Copy() => new Chunk { Content = Content, Color = Color };

        public override string ToString()
        {
            return $"{Content} ({Color})";
        }
    }
}