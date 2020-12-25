using System;
using System.Collections.Generic;
using System.Threading;

namespace Rainbow
{
    class Program
    {
        static void Main(string[] args)
        {
            PrintRainbow();
            var pms = new RainbowParameters();
            AskPgParams(pms);
            pms.HashLength = AskInt32("hash size", 4, 512);
            pms.RowLength = AskInt32("row length", 4, 512);
            pms.ThreadCount = AskInt32("thread count", 1, Environment.ProcessorCount);
            AskGo();

            Console.Clear();
            PrintRainbow();
            var table = new RainbowTable(pms);
            Build(table, pms);
        }

        static void Search(RainbowTable table, RainbowParameters pms)
        {
            var tokenSource = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, args) =>
            {
                args.Cancel = false;
                tokenSource.Cancel();
            };

            Console.WriteLine();
            string search = AskSearchHash(pms);
            table.FoundPassword += (sender, args) =>
            {
#if DEBUG
                if (args.Hash != search)
                {
                    throw new Exception("Found hash is not actually equal to search.");
                }
                byte[] actualHash = RainbowHelper.HashPassword(pms, args.Password);
                if (Convert.ToBase64String(actualHash) != search)
                {
                    throw new Exception("Found image does not actually hash to desired value.");
                }
#endif
                Console.WriteLine($"Found match: {args.Password}");
            };
            table.SearchPassword(search, tokenSource.Token);
        }

        static string AskSearchHash(RainbowParameters pms)
        {
            Console.WriteLine($"Enter hash value to search");
            Console.Write("> ");
            string input = ReadLineWithForegroundColor(ConsoleColor.Cyan);

            if (input.Length != pms.HashLength)
            {
                FailError($"Length not equal to hash length ({pms.HashLength}).");
            }

            Console.WriteLine();
            return input;
        }

        static void Build(RainbowTable table, RainbowParameters pms)
        {
            var tokenSource = new CancellationTokenSource();

            Console.WriteLine();
            Console.WriteLine($"Building with {pms.ThreadCount} threads. Press Ctrl+C to pause.");
            BuildInBackground(table, tokenSource);

            Search(table, pms);
        }

        static void BuildInBackground(RainbowTable table, CancellationTokenSource tokenSource)
        {
            Console.CancelKeyPress += (sender, args) =>
            {
                args.Cancel = false;
                tokenSource.Cancel();
            };

            table.StartBuilding(tokenSource.Token);

            while (!tokenSource.IsCancellationRequested)
            {
                var (est, best, worst) = table.GetStats();
                Console.Write($" Worst: {worst:P} Est: {est:P} Best: {best:P}");
                Console.SetCursorPosition(0, Console.CursorTop);
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

        static void AskPgParams(RainbowParameters pms)
        {
            Console.WriteLine("Type sample password (may include uppercase alpha, lowercase alpha, digits)");
            Console.Write("> ");
            string sample = ReadLineWithForegroundColor(ConsoleColor.Cyan);

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
            Console.Write("> ");
            string input = ReadLineWithForegroundColor(ConsoleColor.Cyan);

            if (!int.TryParse(input, out int value) || value < min || value > max)
            {
                FailError($"Must be a number in [{min}, {max}]");
            }

            Console.WriteLine();
            return value;
        }

        static void AskGo()
        {
            Console.Write("All set! ENTER to go ");
            Console.ReadKey();
        }

        static string ReadLineWithForegroundColor(ConsoleColor fg)
        {
            Console.ForegroundColor = fg;
            string input = Console.ReadLine();
            Console.ResetColor();
            return input;
        }

        static void FailError(string message)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
            Console.WriteLine("Exiting.");
            Environment.Exit(-1);
        }
    }

    class RainbowParameters
    {
        public int PasswordLength { get; set; }
        public string PasswordChars { get; set; }
        public int HashLength { get; set; }
        public int RowLength { get; set; }
        public int ThreadCount { get; set; }
    }
}
