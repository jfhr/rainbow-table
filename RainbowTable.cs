using System;
using System.Collections.Generic;
using System.Threading;

namespace Rainbow
{
    /// <summary> Threadsafe. </summary>
    internal class RainbowTable
    {
        /// <summary>
        /// Maps last column (hash) to first column (password).
        /// </summary>
        private readonly List<(string Start, ByteString End)> rows = new List<(string Start, ByteString End)>();
        private readonly object rowSync = new object();
#if DEBUG
        private bool isWorking = false;
#endif
        private RainbowParameters Pms => Program.Pms;

        public int RowCount => rows.Count;

        /// <summary>
        /// Build a new row based on the <paramref name="startPassword"/>, unless one already exists.
        /// </summary>
        private void TryBuildNewRow(string startPassword)
        {
            if (startPassword == "psqc")
            {
                ;
            }
            string password = startPassword;
            ByteString hash = RainbowHelper.HashPassword(password);

            // we've already done one hash, so the for loop starts at 1
            for (int i = 1; i < Pms.RowLength; i++)
            {
                password = RainbowHelper.DerivePassword(hash, i);
                hash = RainbowHelper.HashPassword(password);
            }

            lock (rowSync)
            {
                rows.Add((startPassword, hash));
                //rows.TryAdd(hash, startPassword);
            }
        }

        private void BuildRowsUntilCancelled(CancellationToken cancellationToken)
        {
            // while we have covered less than 10% of all hashes, we create random passwords
            while (!cancellationToken.IsCancellationRequested)
            {
                TryBuildNewRow(RainbowHelper.GenerateRandomPassword());
            }
        }

        /// <summary>
        /// Start building in the background. This method returns almost immediately.
        /// </summary>
        public void StartBuilding(CancellationToken cancellationToken)
        {
            RunInThreads((idx, ct) => BuildRowsUntilCancelled(ct), cancellationToken);
        }

        /// <summary>
        /// Get a password for a specific hash from the row starting with <paramref name="startPassword"/>,
        /// or <see langword="null"/> if it isn't found.
        /// </summary>
        private string GetPasswordFromKnownRow(ByteString searchedHash, string startPassword)
        {
            string password = startPassword;
            ByteString hash = RainbowHelper.HashPassword(password);

            if (hash == searchedHash)
            {
                return password;
            }

            // we've already done one hash, so the for loop starts at 1
            for (int i = 1; i < Pms.RowLength; i++)
            {
                password = RainbowHelper.DerivePassword(hash, i);
                hash = RainbowHelper.HashPassword(password);
                if (hash == searchedHash)
                {
                    return password;
                }
            }

            return null;
        }

        /// <summary>
        /// Return a password that hashes to <paramref name="searchedHash"/>, or <see langword="null"/>
        /// if cancelled or the search has been exhaustive.
        /// </summary>
        public string SearchPassword(ByteString searchedHash)
        {
            for (int startIdx = Pms.RowLength; startIdx >= 0; startIdx--)
            {
                ByteString hash = searchedHash;
                for (int iteration = startIdx; iteration < Pms.RowLength; iteration++)
                {
                    string password = RainbowHelper.DerivePassword(hash, iteration);
                    hash = RainbowHelper.HashPassword(password);
                }
                //if (rows.ContainsKey(hash))
                foreach (var (start, end) in rows)
                {
                    if (end == hash)
                    {
                        var possiblePassword = GetPasswordFromKnownRow(searchedHash, start);
                        if (possiblePassword != null)
                        {
                            return possiblePassword;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Run a method in parallel on background threads.
        /// This method returns almost immediately.
        /// </summary>
        /// <remarks>
        /// Each method call received two parameters:
        /// <list type="bullet">
        /// <item>The thread index (starting from 0)</item>
        /// <item>The cancellation token</item>
        /// </list>
        /// </remarks>
        private void RunInThreads(Action<int, CancellationToken> action, CancellationToken cancellationToken)
        {
#if DEBUG
            if (isWorking)
            {
                throw new InvalidOperationException("I'm already working.");
            }

            isWorking = true;
            cancellationToken.Register(() => isWorking = false);
#endif
            var threads = new Thread[Pms.ThreadCount];

            // 1 main thread, the rest are workers for us to use
            for (int i = 1; i < threads.Length; i++)
            {
                var thread = new Thread(new ThreadStart(() => action(i, cancellationToken)))
                {
                    Name = $"RainbowWorker {i}",
                    IsBackground = true
                };
                thread.Start();
                threads[i] = thread;
            }
        }
    }
}
