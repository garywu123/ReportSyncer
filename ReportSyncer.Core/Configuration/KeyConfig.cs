// ============================================================================
// File: KeyConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Configuration for business key definitions used in deduplication.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Defines the business key (composite key) for a table, used for deduplication
    /// logic and WHERE NOT EXISTS patterns during synchronization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Business keys identify unique records for merge/upsert operations. When specified,
    /// the sync engine uses these keys to determine which records already exist in the
    /// target and which need to be inserted.
    /// </para>
    /// <para>
    /// For composite keys, specify multiple column names in the order they should be
    /// evaluated. All key columns must exist in both source and target tables.
    /// </para>
    /// </remarks>
    /// <example>
    /// <![CDATA[
    /// // Single key column
    /// var singleKey = new KeyConfig(new[] { "CustomerId" });
    /// 
    /// // Composite key (CustomerId + OrderDate)
    /// var compositeKey = new KeyConfig(new[] { "CustomerId", "OrderDate" });
    /// ]]>
    /// </example>
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
