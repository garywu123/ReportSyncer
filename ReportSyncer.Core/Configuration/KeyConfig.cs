// ============================================================================
// File: KeyConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Configuration for business key definitions used in deduplication.
// ============================================================================

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// <summary>
    /// Key column configuration for table sync tasks.
    /// </summary>
    public class KeyConfig
    {
        private readonly string[] _businessKey;

        /// <summary>
        /// Gets the list of column names that form the business key.
        /// For composite keys, columns are listed in the order they should be evaluated.
        /// </summary>
        public IReadOnlyList<string> BusinessKey => _businessKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyConfig"/> class.
        /// </summary>
        /// <param name="businessKey">
        /// The column names that form the business key. Must contain at least one column name.
        /// For composite keys, provide multiple column names in evaluation order.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="businessKey"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="businessKey"/> is empty or contains null/empty column names.
        /// </exception>
        public KeyConfig(IEnumerable<string> businessKey)
        {
            if (businessKey == null)
                throw new ArgumentNullException(nameof(businessKey));

            _businessKey = businessKey.ToArray();

            if (_businessKey.Length == 0)
                throw new ArgumentException("Business key must contain at least one column name.", nameof(businessKey));

            if (_businessKey.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Business key cannot contain null or empty column names.", nameof(businessKey));
        }
    }
}
