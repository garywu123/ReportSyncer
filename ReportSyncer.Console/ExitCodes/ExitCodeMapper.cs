// -----------------------------------------------------------------------------
// <copyright file="ExitCodeMapper.cs" company="Gary Wu">
// Copyright (c) 2026 Gary Wu. All rights reserved.
// </copyright>
// <author>Gary Wu</author>
// <date>2026-01-04</date>
// <summary>
// Central mapping logic from exceptions and execution phases to exit codes.
// </summary>
// -----------------------------------------------------------------------------

using System;
using System.IO;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Exceptions;

namespace ReportSyncer.Console.ExitCodes;

/// <summary>
/// Provides centralized logic for mapping exceptions and execution phases to exit codes.
/// </summary>
/// <remarks>
/// This is the single source of truth for exit code mapping. All exception-to-exit-code
/// decisions should be made here to ensure consistency.
/// <para>
/// Mapping rules:
/// <list type="bullet">
/// <item>CLI usage errors → <see cref="ExitCode.InvalidArguments"/> (2)</item>
/// <item><see cref="OperationCanceledException"/> → <see cref="ExitCode.Cancelled"/> (6)</item>
/// <item><see cref="ConfigurationException"/> or IO errors → <see cref="ExitCode.InvalidConfiguration"/> (3)</item>
/// <item>Schema/safety failures during Preflight → <see cref="ExitCode.PreflightFailed"/> (4)</item>
/// <item>Execution failures → <see cref="ExitCode.ExecutionFailed"/> (5)</item>
/// <item>Unhandled exceptions → <see cref="ExitCode.UnhandledFatal"/> (1)</item>
/// </list>
/// </para>
/// <example>
/// <code>
/// <![CDATA[
/// try
/// {
///     // Some operation
/// }
/// catch (Exception ex)
/// {
///     var code = ExitCodeMapper.FromException(ex, HostPhase.Preflight);
///     return (int)code;
/// }
/// ]]>
/// </code>
/// </example>
/// </remarks>
internal static class ExitCodeMapper
{
    /// <summary>
    /// Returns the exit code for CLI usage errors (unknown arguments, missing required inputs, etc.).
    /// </summary>
    /// <returns><see cref="ExitCode.InvalidArguments"/> (2)</returns>
    internal static ExitCode FromUsageError() => ExitCode.InvalidArguments;

    /// <summary>
    /// Maps an exception to an exit code based on the exception type and the current execution phase.
    /// </summary>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="phase">The current execution phase when the exception occurred.</param>
    /// <returns>The appropriate exit code for the given exception and phase.</returns>
    internal static ExitCode FromException(Exception ex, HostPhase phase)
    {
        // Cancellation always wins (except for unhandled fatal)
        if (ex is OperationCanceledException)
            return ExitCode.Cancelled;

        // Configuration failures (examples: ConfigurationException, IO errors)
        if (ex is ConfigurationException)
            return ExitCode.InvalidConfiguration;

        if (ex is FileNotFoundException or IOException or UnauthorizedAccessException)
            return ExitCode.InvalidConfiguration;

        // Preflight phase failures
        if (phase == HostPhase.Preflight)
        {
            // Schema mismatch during preflight
            if (ex is SchemaMismatchException)
                return ExitCode.PreflightFailed;

            // TODO: Add SafetyViolationException when it's implemented in Core
            // if (ex is SafetyViolationException)
            //     return ExitCode.PreflightFailed;

            // Other exceptions during preflight: treat as preflight failed unless clearly config/cancelled
            return ExitCode.PreflightFailed;
        }

        // Execution phase failures
        if (phase == HostPhase.Execution)
        {
            if (ex is SyncExecutionException)
                return ExitCode.ExecutionFailed;

            if (ex is SchemaMismatchException)
                return ExitCode.ExecutionFailed; // Schema issues found during execution

            // TODO: Add SafetyViolationException when it's implemented in Core
            // if (ex is SafetyViolationException)
            //     return ExitCode.ExecutionFailed;

            // Unknown exception during execution is still UnhandledFatal (bug or unexpected)
            return ExitCode.UnhandledFatal;
        }

        // Bootstrap unknown failures are fatal
        return ExitCode.UnhandledFatal;
    }
}
