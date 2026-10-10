using System;
using MistForest.Core.Model;

namespace MistForest.Server
{
    internal static class Program
    {
        private static void Main()
        {
            var map = new Map();
            Console.WriteLine("default map: " + map.Width + "x" + map.Height);
            Console.WriteLine(
                "Scout mode: " + MapInfo.Rows(Mode.Scout) + "x" + MapInfo.Cols(Mode.Scout)
            );
            Console.WriteLine(
                "Self-defined mode 47 = " + MapInfo.Rows((Mode)47) + "x" + MapInfo.Cols((Mode)47)
            );
        }
    }
}
