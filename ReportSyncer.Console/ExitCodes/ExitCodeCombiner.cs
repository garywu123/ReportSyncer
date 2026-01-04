// -----------------------------------------------------------------------------
// <copyright file="ExitCodeCombiner.cs" company="Gary Wu">
// Copyright (c) 2026 Gary Wu. All rights reserved.
// </copyright>
// <author>Gary Wu</author>
// <date>2026-01-04</date>
// <summary>
// Combines multiple exit codes to determine the worst (most severe) outcome.
// </summary>
// -----------------------------------------------------------------------------

namespace ReportSyncer.Console.ExitCodes;

/// <summary>
/// Provides logic for combining multiple exit codes to determine the worst (most severe) outcome.
/// </summary>
/// <remarks>
/// Severity order (light to heavy): 0 &lt; 2 &lt; 3 &lt; 4 &lt; 5 &lt; 6 &lt; 1
/// <para>
/// <see cref="ExitCode.UnhandledFatal"/> is the most severe, followed by <see cref="ExitCode.Cancelled"/>.
/// </para>
/// <example>
/// <code>
/// <![CDATA[
/// var current = ExitCode.Success;
/// current = ExitCodeCombiner.CombineWorst(current, ExitCode.InvalidArguments);
/// current = ExitCodeCombiner.CombineWorst(current, ExitCode.ExecutionFailed);
/// // current is now ExitCode.ExecutionFailed (5)
/// 
/// current = ExitCodeCombiner.CombineWorst(current, ExitCode.UnhandledFatal);
/// // current is now ExitCode.UnhandledFatal (1) - the most severe
/// ]]>
/// </code>
/// </example>
/// </remarks>
internal static class ExitCodeCombiner
{
    /// <summary>
    /// Combines two exit codes and returns the more severe one.
    /// </summary>
    /// <param name="currentWorst">The current worst exit code.</param>
    /// <param name="newCode">The new exit code to compare.</param>
    /// <returns>The more severe of the two exit codes based on the severity ranking.</returns>
    internal static ExitCode CombineWorst(ExitCode currentWorst, ExitCode newCode)
    {
        return Rank(newCode) > Rank(currentWorst) ? newCode : currentWorst;

        static int Rank(ExitCode code)
        {
            // Map to rank value (higher means worse)
            // Order: Success < InvalidArguments < InvalidConfiguration < PreflightFailed < ExecutionFailed < Cancelled < UnhandledFatal
            return code switch
            {
                ExitCode.Success => 1,
                ExitCode.InvalidArguments => 2,
                ExitCode.InvalidConfiguration => 3,
                ExitCode.PreflightFailed => 4,
                ExitCode.ExecutionFailed => 5,
                ExitCode.Cancelled => 6,
                ExitCode.UnhandledFatal => 7,
                _ => 7 // Unknown codes are treated as fatal
            };
        }
    }
}
