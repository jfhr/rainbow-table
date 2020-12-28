using System;
using System.Runtime.InteropServices;

namespace Rainbow
{
    internal static class ConsoleHelper
    {
        /// <summary>
        /// Read a line of user input, the input will be reflected in the given color.
        /// </summary>
        public static string ReadLineWithColor(ConsoleColor fg)
        {
            Console.Write("> ");
            if (VibeCheck())
            {
                Console.ForegroundColor = fg;
            }
            string input = Console.ReadLine();
            Console.ResetColor();
            return input;
        }

        /// <summary>
        /// Print an error message.
        /// </summary>
        public static void PrintError(string message)
        {
            WriteLineWithColor(message, ConsoleColor.Red);
        }

        /// <summary>
        /// Does the console support color?
        /// </summary>
        public static bool VibeCheck()
        {
            if (Environment.GetEnvironmentVariable("NO_COLOR") != null 
                || Environment.GetEnvironmentVariable("CLICOLOR") == "0")
            {
                // user doesn't like color
                return false;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string build = RuntimeInformation.OSDescription.Split('.')[^1];
                if (int.TryParse(build, out int buildNumber) && buildNumber < 10586)
                {
                    // we are on an old windows version that doesn't support color
                    return false;
                }
            }

            return true;
        }

        public static void WriteLineWithColor(string message, ConsoleColor color)
        {
            if (VibeCheck())
            {
                Console.ForegroundColor = color;
            }
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public static void Log(string message)
        {
            if (VibeCheck())
            {
                Console.ForegroundColor = ConsoleColor.Gray;
            }
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}
