using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiquidPromptWin
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var cmd = new CmdWrapper();
            cmd.Initialize();
            cmd.Start();
        }
    }
}
