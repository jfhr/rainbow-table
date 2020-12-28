using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

[assembly: InternalsVisibleTo("Rainbow.Test")]

namespace Rainbow
{
    internal static class Program
    {
        public static RainbowParameters Pms { get; set; } = new RainbowParameters();

        private static void Main(string[] args)
        {
            PrintBanner();
            AskPgParams();
            Pms.HashLength = AskInt32("hash size in bytes", 1, 16);
            Pms.RowLength = AskInt32("row length", 4, 512);
            Pms.ThreadCount = AskInt32("thread count", 2, Environment.ProcessorCount);
            AskGo();

            Console.Clear();
            PrintBanner();
            var table = new RainbowTable();
            Build(table);
        }

        private static void Search(RainbowTable table)
        {
            Console.WriteLine();
            if (!AskSearchHash(out ByteString search, out string searchAsString))
            {
                return;
            }
            
            Console.WriteLine($"Searching for hash '{searchAsString}'.");
            string password = table.SearchPassword(search);

            if (password == null)
            {
                Console.WriteLine("No match found.");
            }
            else
            {
#if DEBUG
                // extra check
                var actualHash = RainbowHelper.HashPassword(password);
                if (actualHash != search)
                {
                    throw new Exception("Found image does not actually hash to desired value.");
                }
#endif
                Console.Write($"Found match: ");
                ConsoleHelper.WriteLineWithColor(password, ConsoleColor.Green);
            }

            // another round
            Search(table);
        }

        /// <summary>
        /// Returns <see langword="true"/> and sets the parameters to the hash entered by the user,
        /// or <see langword="false"/> if the user wants to go back to building.
        /// </summary>
        private static bool AskSearchHash(out ByteString hash, out string hashAsString)
        {
            while (true)
            {
                Console.WriteLine($"Enter hash value to search (hexadecimal, no prefix, length {Pms.HashLength} bytes)");
                Console.WriteLine("Leave empty to go back to building");
                string input = ConsoleHelper.ReadLineWithColor(ConsoleColor.Cyan);

                if (string.IsNullOrEmpty(input))
                {
                    hash = default;
                    hashAsString = null;
                    return false;
                }

                var searchHash = new byte[Pms.HashLength];
                if (TryParseHex(input, searchHash, out int bytesWritten) && bytesWritten == Pms.HashLength)
                {
                    hash = (ByteString)searchHash;
                    hashAsString = input.ToUpper();
                    return true;
                }

                ConsoleHelper.PrintError($"Not a valid hex string, or not the right length of {Pms.HashLength} bytes.");
            }
        }

        /// <summary>
        /// Tries to parse a hexadecimal string into a byte array.
        /// </summary>
        /// <remarks>
        /// The operation fails if the string is not valid hex (without prefix)
        /// or if the buffer runs out.
        /// </remarks>
        internal static bool TryParseHex(string hex, byte[] buffer, out int bytesWritten)
        {
            bool isLower = false;
            bytesWritten = 0;
            for (int i = 0; i < hex.Length; i++)
            {
                byte digit = ParseHexDigit(hex[i]);
                if (digit > 15)
                {
                    return false;
                }

                // is the buffer exhausted?
                if (i / 2 >= buffer.Length)
                {
                    return false;
                }

                if (isLower)
                {
                    buffer[i / 2] |= digit;
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
        private static byte ParseHexDigit(char digit)
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

        private static void Build(RainbowTable table)
        {
            var tokenSource = new CancellationTokenSource();

            Console.WriteLine();
            Console.WriteLine($"Building with {Pms.ThreadCount} threads. Press q to pause and search for a hash image.");
            BuildInBackground(table, tokenSource);

            // if the user pauses, start search interface
            Search(table);
            // if the user ends the pause interface, start building again
            Build(table);
        }

        private static void BuildInBackground(RainbowTable table, CancellationTokenSource tokenSource)
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

                Console.SetCursorPosition(0, Console.CursorTop);
                //Console.Write($" Worst: {worst:P} Est: {est:P} Best: {best:P}");
                Console.Write($"{table.RowCount} rows");

                Thread.Sleep(10);
            }
        }

        private static void PrintBanner()
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

        private static void PrintRainbow()
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

        private static void PrintColorlessBanner()
        {
            Console.WriteLine("+-----------------+");
            Console.WriteLine("| Rainbow by jfhr |");
            Console.WriteLine("+-----------------+");
            Console.WriteLine();
        }

        private static void AskPgParams()
        {
            Console.WriteLine("Type sample password (may include uppercase alpha, lowercase alpha, digits)");
            string sample = ConsoleHelper.ReadLineWithColor(ConsoleColor.Cyan);

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
            Pms.PasswordChars =
                (uppercaseAlpha ? upper : "")
                + (lowercaseAlpha ? lower : "")
                + (digits ? digit : "");

            Pms.PasswordLength = sample.Length;

            // reflect params to user
            Console.WriteLine($"Params: " +
                (pmsAsStrings.Count < 3 ? "only " : "") +
                string.Join(", ", pmsAsStrings) +
                $", length is {Pms.PasswordLength}.");

            Console.WriteLine();
        }

        private static int AskInt32(string name, int min, int max)
        {
            Console.WriteLine($"Enter {name} (must be in [{min}, {max}])");
            string input = ConsoleHelper.ReadLineWithColor(ConsoleColor.Cyan);

            if (!int.TryParse(input, out int value) || value < min || value > max)
            {
                FailError($"Must be a number in [{min}, {max}]");
            }

            Console.WriteLine();
            return value;
        }

        private static void AskGo()
        {
            Console.Write("All set! Press any key to go ");
            Console.ReadKey();
        }

        private static void FailError(string message)
        {
            ConsoleHelper.PrintError(message);
            Console.WriteLine("Exiting.");
            Environment.Exit(-1);
        }
    }
}
