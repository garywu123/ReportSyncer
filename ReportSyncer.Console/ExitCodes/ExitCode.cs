// -----------------------------------------------------------------------------
// <copyright file="ExitCode.cs" company="Gary Wu">
// Copyright (c) 2026 Gary Wu. All rights reserved.
// </copyright>
// <author>Gary Wu</author>
// <date>2026-01-04</date>
// <summary>
// Defines the exit codes for the ReportSyncer Console application.
// </summary>
// -----------------------------------------------------------------------------

namespace ReportSyncer.Console.ExitCodes;

/// <summary>
/// Exit codes for the ReportSyncer Console application, aligned with the specification
/// in docs/60_Console Host Spec.md Section 8.
/// </summary>
/// <remarks>
/// Severity order (light to heavy): 0 &lt; 2 &lt; 3 &lt; 4 &lt; 5 &lt; 6 &lt; 1
/// </remarks>
internal enum ExitCode
{
    /// <summary>
    /// Success (0): The sync operation completed successfully, or dry-run completed without errors.
    /// </summary>
    Success = 0,

    /// <summary>
    /// Unhandled Fatal (1): An unexpected internal error occurred that was not handled by the application.
    /// </summary>
    UnhandledFatal = 1,

    /// <summary>
    /// Invalid Arguments (2): CLI usage error, such as unknown arguments or missing required inputs.
    /// </summary>
    InvalidArguments = 2,

    /// <summary>
    /// Invalid Configuration (3): Configuration loading or validation failed (e.g., YAML parse error, IO error).
    /// </summary>
    InvalidConfiguration = 3,

    /// <summary>
    /// Preflight Failed (4): Preflight checks failed (e.g., schema mismatch, safety violations, permission issues).
    /// </summary>
    PreflightFailed = 4,

    /// <summary>
    /// Execution Failed (5): Sync execution failed (e.g., database errors, write failures during execution phase).
    /// </summary>
    ExecutionFailed = 5,

    /// <summary>
    /// Cancelled (6): The operation was cancelled by the user (e.g., Ctrl+C).
    /// </summary>
    Cancelled = 6,
}
