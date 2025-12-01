// ============================================================================
// File: SafetyConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Global safety configuration to prevent dangerous sync operations.
// ============================================================================

using System;

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Defines global safety rules that prevent dangerous synchronization operations,
    /// such as Production-to-Production syncs and self-syncs.
    /// </summary>
    /// <summary>
    /// Safety configuration for sync operations.
    /// </summary>
    public class SafetyConfig
    {
        /// <summary>
        /// Gets a value indicating whether Production-to-Production syncs are forbidden.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, the system blocks any sync job where both the source and target
        /// connections have <see cref="EnvironmentType.Prod"/> environment classification.
        /// </para>
        /// <para>
        /// This prevents accidental overwriting of production data with other production data,
        /// which is rarely intentional and usually indicates a configuration error.
        /// </para>
        /// </remarks>
        /// <warning>
        /// Disabling this protection in production environments is strongly discouraged.
        /// </warning>
        public bool ForbidProdToProd { get; }

        /// <summary>
        /// Gets a value indicating whether source and target connections must be different databases.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, the system blocks syncs where the source and target connection strings
        /// resolve to the same physical database (same server + database name).
        /// </para>
        /// <para>
        /// Self-syncs are rarely useful and usually indicate a configuration error where
        /// someone accidentally specified the same connection for both source and target.
        /// </para>
        /// </remarks>
        public bool RequireDifferentConnections { get; }

        /// <summary>
        /// Gets the threshold percentage for triggering large delete confirmation prompts.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When pre-sync deletes are estimated to remove more than this percentage of the
        /// target table's existing rows, the system requires explicit user confirmation
        /// before proceeding with the operation.
        /// </para>
        /// <para>
        /// Value range: 0.0 (always confirm) to 1.0 (only confirm if deleting 100% of rows).
        /// </para>
        /// <para>
        /// Typical values:
        /// </para>
        /// <list type="bullet">
        /// <item><description>0.5 (50%): Very cautious, prompts frequently</description></item>
        /// <item><description>0.8 (80%): Balanced protection (recommended)</description></item>
        /// <item><description>0.95 (95%): Only prompts for near-total wipes</description></item>
        /// </list>
        /// </remarks>
        /// <warning>
        /// Setting this too high (e.g., 1.0) may allow destructive operations without sufficient warning.
        /// </warning>
        public double ConfirmLargeDeletePct { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SafetyConfig"/> class.
        /// </summary>
        /// <param name="forbidProdToProd">
        /// Whether to forbid Production-to-Production syncs. Strongly recommended to be true.
        /// </param>
        /// <param name="requireDifferentConnections">
        /// Whether to require source and target to be different databases. Recommended to be true.
        /// </param>
        /// <param name="confirmLargeDeletePct">
        /// The threshold (0.0 to 1.0) for triggering large delete confirmation.
        /// For example, 0.8 means confirm if deleting more than 80% of target rows.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="confirmLargeDeletePct"/> is not between 0.0 and 1.0 inclusive.
        /// </exception>
        public SafetyConfig(
            bool forbidProdToProd,
            bool requireDifferentConnections,
            double confirmLargeDeletePct)
        {
            if (confirmLargeDeletePct < 0.0 || confirmLargeDeletePct > 1.0)
                throw new ArgumentException(
                    "Confirm large delete percentage must be between 0.0 and 1.0.",
                    nameof(confirmLargeDeletePct));

            ForbidProdToProd = forbidProdToProd;
            RequireDifferentConnections = requireDifferentConnections;
            ConfirmLargeDeletePct = confirmLargeDeletePct;
        }
    }
}
