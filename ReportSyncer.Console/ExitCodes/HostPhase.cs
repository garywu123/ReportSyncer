// -----------------------------------------------------------------------------
// <copyright file="HostPhase.cs" company="Gary Wu">
// Copyright (c) 2026 Gary Wu. All rights reserved.
// </copyright>
// <author>Gary Wu</author>
// <date>2026-01-04</date>
// <summary>
// Defines the host execution phases for exit code mapping.
// </summary>
// -----------------------------------------------------------------------------

namespace ReportSyncer.Console.ExitCodes;

/// <summary>
/// Represents the current phase of the console host execution.
/// Used to determine appropriate exit codes based on when an exception occurs.
/// </summary>
internal enum HostPhase
{
    /// <summary>
    /// Bootstrap phase: CLI parsing, configuration loading, dependency injection setup.
    /// </summary>
    Bootstrap,

    /// <summary>
    /// Preflight phase: Schema validation, safety checks, permission verification.
    /// </summary>
    Preflight,

    /// <summary>
    /// Execution phase: Actual sync operation running.
    /// </summary>
    Execution,
}
