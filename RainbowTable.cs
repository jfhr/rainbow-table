using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rainbow
{
    /// <summary> Threadsafe. </summary>
    class RainbowTable
    {
        /// <summary>
        /// Maps last column (hash) to first column (password).
        /// </summary>
        private readonly Dictionary<HashableByteArray, string> rows = new Dictionary<HashableByteArray, string>();
        private readonly object rowSync = new object();
        private readonly RainbowParameters pms;
        private bool isWorking = false;

        public event EventHandler<(HashableByteArray Hash, string Password)> FoundPassword;

        public int RowCount => rows.Count;

        public RainbowTable(RainbowParameters pms)
        {
            this.pms = pms;
        }

        /// <summary>
        /// Returns a tuple of the following values:
        /// 
        /// <list type="number">
        /// <item>
        /// Estimated percentage of covered hash values.
        /// </item>
        /// <item>
        /// Percentage of covered hash values in the best case (i.e. no collisions).
        /// </item>
        /// <item>
        /// Percentage of covered hash values in the worst case (only one unique hash per row).
        /// </item>
        /// </list>
        /// </summary>
        public (double, double, double) GetStats()
        {

            // number of hashes
            int n = RowCount * pms.RowLength;
            // hash function domain size (no of possible hash values)
            double d = Math.Pow(2, pms.HashLength);
            // expected no of collisions (see https://en.wikipedia.org/wiki/Birthday_problem#Generalizations)
            double expectedCollisions = n - d + d * Math.Pow((d - 1) / d, n);
            int expectedUniqueHashes = n - (int)expectedCollisions;

            double est = Math.Min(1, expectedUniqueHashes / d);
            double best = Math.Min(1, n / d);
            double worst = Math.Min(1, RowCount / d);
            return (est, best, worst);
        }

        /// <summary>
        /// Generate a random password and build a new row based on it, unless one already exists.
        /// </summary>
        private void TryBuildNewRow()
        {
            string start;
            start = RainbowHelper.GenerateRandomPassword(pms);

            string password = start;
            HashableByteArray hash = default;

            for (int i = 0; i < pms.RowLength; i++)
            {
                hash = (HashableByteArray) RainbowHelper.HashPassword(pms, password);
                password = RainbowHelper.DerivePassword(pms, hash, i);
            }

            lock (rowSync)
            {
                rows.TryAdd(hash, start);
            }
        }

        private void BuildRowsUntilCancelled(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TryBuildNewRow();
            }
        }

        private void SearchRow(KeyValuePair<HashableByteArray, string> row, HashableByteArray search, CancellationToken cancellationToken)
        {
            var pHash = row.Key;
            string password;

            // index where we start the search
            for (int startIndex = pms.RowLength - 1; startIndex >= 0; startIndex--)
            {
                // index of the current column
                for (int columnIndex = startIndex; columnIndex < pms.RowLength; columnIndex++)
                {
                    password = RainbowHelper.DerivePassword(pms, pHash, columnIndex);
                    pHash = RainbowHelper.HashPassword(pms, password);
                    if (search == pHash)
                    {
                        FoundPassword?.Invoke(this, (search, password));
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }
        }

        private void SearchPasswordInternal(HashableByteArray hash, int threadIndex, CancellationToken cancellationToken)
        {
            // we search only rows from here...
            int startIndex = RowCount / pms.ThreadCount * threadIndex;
            // ...to here (exclusive)
            int endIndex = RowCount / pms.ThreadCount * (threadIndex + 1);

            var ourRows = rows.Skip(startIndex - 1).Take(endIndex - startIndex);

            foreach (var row in ourRows)
            {
                SearchRow(row, hash, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
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
            if (isWorking)
            {
                throw new InvalidOperationException("I'm already working.");
            }

            var threads = new Thread[pms.ThreadCount];
            isWorking = true;
            cancellationToken.Register(() => isWorking = false);

            for (int i = 0; i < threads.Length; i++)
            {
                var thread = new Thread(new ThreadStart(() => action(i, cancellationToken)))
                {
                    IsBackground = true
                };
                thread.Start();
                threads[i] = thread;
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
        /// Start searching a password in the background. This method returns almost immediately.
        /// </summary>
        public void SearchPassword(HashableByteArray hash, CancellationToken cancellationToken)
        {
            if (rows.ContainsKey(hash))
            {
                // We're lucky! The hash value is already in a final row
                FoundPassword?.Invoke(this, (hash, rows[hash]));
            }
            // We're still going to continue searching in case there are other matches
            // The user can cancel at any time

            RunInThreads((idx, ct) => SearchPasswordInternal(hash, idx, ct), cancellationToken);
        }
    }
}
