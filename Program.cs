using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Rainbow
{
    static class Program
    {
        static RainbowParameters pms;

        static void Main(string[] args)
        {
            PrintBanner();
            pms = new RainbowParameters();
            AskPgParams();
            pms.HashLength = AskInt32("hash size in bytes", 1, 64);
            pms.RowLength = AskInt32("row length", 4, 512);
            pms.ThreadCount = AskInt32("thread count", 1, Environment.ProcessorCount);
            AskGo();

            Console.Clear();
            PrintBanner();
            var table = new RainbowTable(pms);
            Build(table);
        }

        static void Search(RainbowTable table)
        {
            var tokenSource = new CancellationTokenSource();

            Console.WriteLine();
            var (search, searchAsString) = AskSearchHash();

            table.FoundPassword += (sender, args) =>
            {
#if DEBUG
                // some extra checks
                if (args.Hash != search)
                {
                    throw new Exception("Found hash is not actually equal to search.");
                }
                var actualHash = RainbowHelper.HashPassword(pms, args.Password);
                if (actualHash != search)
                {
                    throw new Exception("Found image does not actually hash to desired value.");
                }
#endif
                Console.WriteLine($"Found match: {args.Password}");
            };

            Console.WriteLine($"Searching for hash '{searchAsString}'. Press q to go back to building.");
            table.SearchPassword(search, tokenSource.Token);

            while (!tokenSource.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey();
                    if (key.KeyChar == 'q')
                    {
                        tokenSource.Cancel();
                    }
                }
            }
        }

        static (HashableByteArray, string) AskSearchHash()
        {
            Console.WriteLine($"Enter hash value to search (hexadecimal, no prefix, length {pms.HashLength} bytes)");
            string input = ConsoleHelper.ReadLineWithForegroundColor(ConsoleColor.Cyan);
            var searchHash = new byte[pms.HashLength];

            if (TryParseHex(input, searchHash, out int bytesWritten) && bytesWritten == pms.HashLength)
            {
                return ((HashableByteArray)searchHash, input);
            }

            ConsoleHelper.PrintError($"Not a valid hex string, or not the right length of {pms.HashLength} bytes.");
            return AskSearchHash();
        }

        /// <summary>
        /// Tries to parse a hexadecimal string into a byte array.
        /// </summary>
        /// <remarks>
        /// The operation fails if the string is not valid hex (without prefix)
        /// or if the buffer runs out.
        /// </remarks>
        static bool TryParseHex(string hex, byte[] buffer, out int bytesWritten)
        {
            bool isLower = true;
            bytesWritten = 0;
            for (int i = 0; i < hex.Length; i++)
            {
                byte digit = ParseHexDigit(hex[^(i + 1)]);
                if (digit > 15)
                {
                    return false;
                }

                // is the buffr exhausted?
                if (i / 2 >= buffer.Length)
                {
                    return false;
                }

                if (isLower)
                {
                    buffer[i / 2] = digit;
                    bytesWritten++;
                }
                else
                {
                    buffer[i / 2] |= (byte)(digit << 4);
                }
                isLower = !isLower;
            }
            return true;
        }

        /// <summary>
        /// Parse a hex digit into a value in [0, 15].
        /// Returns a value > 15 if the parsing fails.
        /// </summary>
        static byte ParseHexDigit(char digit)
        {
            if ('0' <= digit && digit <= '9')
            {
                return (byte)(digit - '0');
            }
            if ('A' <= digit && digit <= 'F')
            {
                return (byte)(digit - 'A' + 10);
            }
            if ('a' <= digit && digit <= 'f')
            {
                return (byte)(digit - 'a' + 10);
            }
            return 255;
        }

        static void Build(RainbowTable table)
        {
            var tokenSource = new CancellationTokenSource();

            Console.WriteLine();
            Console.WriteLine($"Building with {pms.ThreadCount} threads. Press q to pause.");
            BuildInBackground(table, tokenSource);

            // if the user pauses, start search interface
            Search(table);
            // if the user ends the pause interface, start building again
            Build(table);
        }

        static void BuildInBackground(RainbowTable table, CancellationTokenSource tokenSource)
        {
            table.StartBuilding(tokenSource.Token);

            while (!tokenSource.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey();
                    if (key.KeyChar == 'q')
                    {
                        tokenSource.Cancel();
                    }
                }

                var (est, best, worst) = table.GetStats();
                Console.Write($" Worst: {worst:P} Est: {est:P} Best: {best:P}");
                Console.SetCursorPosition(0, Console.CursorTop);
            }
        }

        static void PrintBanner()
        {
            if (ConsoleHelper.VibeCheck())
            {
                PrintRainbow();
            }
            else
            {
                PrintColorlessBanner();
            }
        }

        static void PrintRainbow()
        {
            const char box = '█';
            int columnWidth = Console.BufferWidth / 7;
            var boxes = new string(box, columnWidth);

            var colors = new[]
            {
                ConsoleColor.Red,
                ConsoleColor.Yellow,
                ConsoleColor.Green,
                ConsoleColor.Cyan,
                ConsoleColor.Blue,
                ConsoleColor.Magenta,
            };

            for (int r = 0; r < 5; r++)
            {
                foreach (var color in colors)
                {
                    Console.ForegroundColor = color;
                    Console.Write(boxes);
                }
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.ResetColor();
        }

        static void PrintColorlessBanner()
        {
            Console.WriteLine("+-----------------+");
            Console.WriteLine("| Rainbow by jfhr |");
            Console.WriteLine("+-----------------+");
            Console.WriteLine();
        }

        static void AskPgParams()
        {
            Console.WriteLine("Type sample password (may include uppercase alpha, lowercase alpha, digits)");
            string sample = ConsoleHelper.ReadLineWithForegroundColor(ConsoleColor.Cyan);

            if (string.IsNullOrWhiteSpace(sample))
            {
                FailError("Sample password can not be empty.");
            }

            var pmsAsStrings = new HashSet<string>(3);
            bool uppercaseAlpha = false;
            bool lowercaseAlpha = false;
            bool digits = false;

            foreach (var c in sample)
            {
                if ('A' <= c && c <= 'Z')
                {
                    uppercaseAlpha = true;
                    pmsAsStrings.Add("uppercase alpha");
                }
                else if ('a' <= c && c <= 'z')
                {
                    lowercaseAlpha = true;
                    pmsAsStrings.Add("lowercase alpha");
                }
                else if ('0' <= c && c <= '9')
                {
                    digits = true;
                    pmsAsStrings.Add("digits");
                }
                else
                {
                    FailError("Only use uppercase alpha, lowercase alpha, digits");
                }
            }

            // compute allowed chars
            const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lower = "abcdefghijklmnopqrstuvwxyz";
            const string digit = "0123456789";
            pms.PasswordChars =
                (uppercaseAlpha ? upper : "")
                + (lowercaseAlpha ? lower : "")
                + (digits ? digit : "");

            pms.PasswordLength = sample.Length;

            // reflect params to user
            Console.WriteLine($"Params: " +
                (pmsAsStrings.Count < 3 ? "only " : "") +
                string.Join(", ", pmsAsStrings) +
                $", length is {pms.PasswordLength}.");

            Console.WriteLine();
        }

        static int AskInt32(string name, int min, int max)
        {
            Console.WriteLine($"Enter {name} (must be in [{min}, {max}])");
            string input = ConsoleHelper.ReadLineWithForegroundColor(ConsoleColor.Cyan);

            if (!int.TryParse(input, out int value) || value < min || value > max)
            {
                FailError($"Must be a number in [{min}, {max}]");
            }

            Console.WriteLine();
            return value;
        }

        static void AskGo()
        {
            Console.Write("All set! Press any key to go ");
            Console.ReadKey();
        }

        public static void FailError(string message)
        {
            ConsoleHelper.PrintError(message);
            Console.WriteLine("Exiting.");
            Environment.Exit(-1);
        }
    }

    class RainbowParameters
    {
        /// <summary>
        /// Password length in characters.
        /// </summary>
        public int PasswordLength { get; set; }

        /// <summary>
        /// Allowed characters in a password.
        /// </summary>
        public string PasswordChars { get; set; }

        /// <summary>
        /// Hash length in bytes.
        /// </summary>
        public int HashLength { get; set; }

        /// <summary>
        /// Length of a row of the rainbow table.
        /// </summary>
        public int RowLength { get; set; }

        /// <summary>
        /// Number of threads for parallel operations.
        /// </summary>
        public int ThreadCount { get; set; }
    }

    static class ConsoleHelper
    {
        /// <summary>
        /// Read a line of user input, the input will be reflected in the given color.
        /// </summary>
        public static string ReadLineWithForegroundColor(ConsoleColor fg)
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
            Console.WriteLine();
            if (VibeCheck())
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            Console.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>
        /// Does console support color?
        /// </summary>
        public static bool VibeCheck()
        {
            if (Environment.GetEnvironmentVariable("NO_COLOR") != null)
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
    }
}
