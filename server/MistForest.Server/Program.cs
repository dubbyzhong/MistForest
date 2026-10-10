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
                "Scout mode: " + MapInfo.Size(Mode.Scout) + "x" + MapInfo.Rows(Mode.Scout)
            );
            Console.WriteLine(
                "Self-defined mode 47 = " + MapInfo.Size((Mode)47) + "x" + MapInfo.Rows((Mode)47)
            );
        }
    }
}
