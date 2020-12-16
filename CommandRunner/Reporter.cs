using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Duotify.EFCore.EntityPartialGenerator.AnsiConstants;

namespace Duotify.EFCore.EntityPartialGenerator
{
    internal static class Reporter
    {
        public static bool IsVerbose { get; set; }
        public static bool NoColor { get; set; }

        public static string Colorize(string value, Func<string, string> colorizeFunc)
            => NoColor ? value : colorizeFunc(value);

        public static void WriteError(string message)
            => Console.WriteLine(Colorize(message, x => Bold + Red + x + Reset));

        public static void WriteWarning(string message)
            => Console.WriteLine(Colorize(message, x => Bold + Yellow + x + Reset));

        public static void WriteInformation(string message)
            => Console.WriteLine(message);

        public static void WriteData(string message)
            => Console.WriteLine(Colorize(message, x => Bold + White + x + Reset));

        public static void WriteVerbose(string message)
        {
            if (IsVerbose)
            {
                Console.WriteLine(Colorize(message, x => Bold + Black + x + Reset));
            }
        }
    }
}
