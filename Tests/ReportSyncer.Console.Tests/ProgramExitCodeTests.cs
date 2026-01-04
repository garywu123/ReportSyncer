// -----------------------------------------------------------------------------
// <copyright file="ProgramExitCodeTests.cs" company="Gary Wu">
// Copyright (c) 2026 Gary Wu. All rights reserved.
// </copyright>
// <author>Gary Wu</author>
// <date>2026-01-04</date>
// <summary>
// Unit tests for Program exit code behavior.
// </summary>
// -----------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using ReportSyncer.Console.ExitCodes;
using Xunit;

namespace ReportSyncer.Console.Tests;

/// <summary>
/// Tests for <see cref="Program"/> exit code behavior.
/// </summary>
/// <remarks>
/// These tests verify basic CLI parsing and exit code mapping.
/// More comprehensive integration tests (with real config files and database) 
/// are better suited for separate integration test suites.
/// </remarks>
public class ProgramExitCodeTests
{
    /// <summary>
    /// Verifies that unknown CLI arguments return InvalidArguments (2).
    /// </summary>
    [Fact]
    public async Task RunAsync_UnknownArgument_ShouldReturnInvalidArguments()
    {
        // Arrange
        var args = new[] { "--unknown-argument" };
        var ct = CancellationToken.None;

        // Act
        var exitCode = await Program.RunAsync(args, ct);

        // Assert
        Assert.Equal((int)ExitCode.InvalidArguments, exitCode);
    }

    /// <summary>
    /// Verifies that invalid boolean value for --dry-run returns InvalidArguments (2).
    /// </summary>
    [Fact]
    public async Task RunAsync_InvalidBooleanValue_ShouldReturnInvalidArguments()
    {
        // Arrange
        var args = new[] { "--config", "test.yaml", "--dry-run", "maybe" };
        var ct = CancellationToken.None;

        // Act
        var exitCode = await Program.RunAsync(args, ct);

        // Assert
        Assert.Equal((int)ExitCode.InvalidArguments, exitCode);
    }

    // Note: Testing cancellation, preflight failures, execution failures, and configuration
    // errors would require more complex integration test setup with actual or mocked 
    // configuration files and database. Those tests are better suited for integration test 
    // suites rather than unit tests of the Program class itself.
    //
    // The ExitCodeMapper and ExitCodeCombiner have comprehensive unit tests that verify
    // the core exit code logic independently of the full Program orchestration.
}
