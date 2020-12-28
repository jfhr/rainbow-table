namespace Rainbow
{
    internal class RainbowParameters
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
}
