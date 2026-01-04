// -----------------------------------------------------------------------------
// <copyright file="FailureSummaryWriter.cs" company="Gary Wu">
// Copyright (c) 2026 Gary Wu. All rights reserved.
// </copyright>
// <author>Gary Wu</author>
// <date>2026-01-04</date>
// <summary>
// Provides centralized failure summary output for the console host.
// </summary>
// -----------------------------------------------------------------------------

using System;
using DotNetToolkit.Logging;

namespace ReportSyncer.Console.ExitCodes;

/// <summary>
/// Provides centralized logic for writing failure summaries to the console and logs.
/// Ensures consistent error reporting and avoids repeating the same failure message multiple times.
/// </summary>
/// <remarks>
/// This class ensures that failure information is presented once in a user-friendly way,
/// with technical details available in logs but not exposed as part of the external contract.
/// </remarks>
internal static class FailureSummaryWriter
{
    /// <summary>
    /// Writes a failure summary to the console error stream and logs.
    /// </summary>
    /// <param name="log">The log service for writing to logs.</param>
    /// <param name="code">The exit code representing the failure.</param>
    /// <param name="summary">A user-friendly summary of the failure.</param>
    /// <param name="ex">The optional exception that caused the failure (for detailed logging).</param>
    /// <remarks>
    /// The summary should be concise and actionable. Do not expose internal exception type names
    /// to users in the console output, but log them for debugging purposes.
    /// </remarks>
    internal static void Write(ILogService log, ExitCode code, string summary, Exception? ex = null)
    {
        // Log structured information for debugging
        var message = $"Run failed with ExitCode={(int)code}. Summary={summary}";
        log.LogError(message, ex);

        // Write user-facing error to console error stream
        System.Console.Error.WriteLine($"ERROR: {summary} (exit={(int)code})");
    }

    /// <summary>
    /// Builds a user-friendly summary message based on the exit code and optional context.
    /// </summary>
    /// <param name="code">The exit code.</param>
    /// <param name="context">Optional context information (e.g., exception message, phase details).</param>
    /// <returns>A user-friendly summary string.</returns>
    internal static string BuildSummary(ExitCode code, string? context = null)
    {
        var baseSummary = code switch
        {
            ExitCode.InvalidArguments => "Invalid arguments. See usage.",
            ExitCode.InvalidConfiguration => "Invalid configuration. Check YAML path and settings.",
            ExitCode.PreflightFailed => "Preflight failed. Check schema/permissions/safety.",
            ExitCode.ExecutionFailed => "Execution failed. Check logs for table-level failures.",
            ExitCode.Cancelled => "Cancelled by user.",
            ExitCode.UnhandledFatal => "Unexpected internal error.",
            _ => "Unknown error."
        };

        return string.IsNullOrWhiteSpace(context)
            ? baseSummary
            : $"{baseSummary} {context}";
    }
}
