// -----------------------------------------------------------------------------
// <copyright file="ExitCodeMapperTests.cs" company="Gary Wu">
// Copyright (c) 2026 Gary Wu. All rights reserved.
// </copyright>
// <author>Gary Wu</author>
// <date>2026-01-04</date>
// <summary>
// Unit tests for ExitCodeMapper.
// </summary>
// -----------------------------------------------------------------------------

using System;
using System.IO;
using ReportSyncer.Console.ExitCodes;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Exceptions;
using Xunit;

namespace ReportSyncer.Console.Tests.ExitCodes;

/// <summary>
/// Tests for <see cref="ExitCodeMapper"/>.
/// </summary>
public class ExitCodeMapperTests
{
    /// <summary>
    /// Verifies that FromUsageError returns InvalidArguments.
    /// </summary>
    [Fact]
    public void FromUsageError_ShouldReturnInvalidArguments()
    {
        // Act
        var code = ExitCodeMapper.FromUsageError();

        // Assert
        Assert.Equal(ExitCode.InvalidArguments, code);
    }

    /// <summary>
    /// Verifies that OperationCanceledException always returns Cancelled, regardless of phase.
    /// </summary>
    [Fact]
    public void FromException_OperationCanceledException_ShouldReturnCancelled()
    {
        // Arrange
        var ex = new OperationCanceledException("User cancelled");

        // Act & Assert - test all phases
        Assert.Equal(ExitCode.Cancelled, ExitCodeMapper.FromException(ex, HostPhase.Bootstrap));
        Assert.Equal(ExitCode.Cancelled, ExitCodeMapper.FromException(ex, HostPhase.Preflight));
        Assert.Equal(ExitCode.Cancelled, ExitCodeMapper.FromException(ex, HostPhase.Execution));
    }

    /// <summary>
    /// Verifies that ConfigurationException returns InvalidConfiguration regardless of phase.
    /// </summary>
    [Fact]
    public void FromException_ConfigurationException_ShouldReturnInvalidConfiguration()
    {
        // Arrange - using simple message constructor
        var ex = new ConfigurationException("Invalid YAML syntax");

        // Act & Assert - test all phases
        Assert.Equal(ExitCode.InvalidConfiguration, ExitCodeMapper.FromException(ex, HostPhase.Bootstrap));
        Assert.Equal(ExitCode.InvalidConfiguration, ExitCodeMapper.FromException(ex, HostPhase.Preflight));
        Assert.Equal(ExitCode.InvalidConfiguration, ExitCodeMapper.FromException(ex, HostPhase.Execution));
    }

    /// <summary>
    /// Verifies that IO-related exceptions return InvalidConfiguration.
    /// </summary>
    [Fact]
    public void FromException_IOExceptions_ShouldReturnInvalidConfiguration()
    {
        // Arrange & Act & Assert
        var fileNotFound = new FileNotFoundException("Config file not found");
        Assert.Equal(ExitCode.InvalidConfiguration, ExitCodeMapper.FromException(fileNotFound, HostPhase.Bootstrap));
        Assert.Equal(ExitCode.InvalidConfiguration, ExitCodeMapper.FromException(fileNotFound, HostPhase.Preflight));
        Assert.Equal(ExitCode.InvalidConfiguration, ExitCodeMapper.FromException(fileNotFound, HostPhase.Execution));

        var ioException = new IOException("Cannot read file");
        Assert.Equal(ExitCode.InvalidConfiguration, ExitCodeMapper.FromException(ioException, HostPhase.Bootstrap));

        var unauthorizedException = new UnauthorizedAccessException("Access denied");
        Assert.Equal(ExitCode.InvalidConfiguration, ExitCodeMapper.FromException(unauthorizedException, HostPhase.Bootstrap));
    }

    /// <summary>
    /// Verifies that SchemaMismatchException during Preflight returns PreflightFailed.
    /// </summary>
    [Fact]
    public void FromException_SchemaMismatchInPreflight_ShouldReturnPreflightFailed()
    {
        // Arrange
        var ex = new SchemaMismatchException("Column mismatch");

        // Act
        var code = ExitCodeMapper.FromException(ex, HostPhase.Preflight);

        // Assert
        Assert.Equal(ExitCode.PreflightFailed, code);
    }

    /// <summary>
    /// Verifies that SchemaMismatchException during Execution returns ExecutionFailed.
    /// </summary>
    [Fact]
    public void FromException_SchemaMismatchInExecution_ShouldReturnExecutionFailed()
    {
        // Arrange
        var ex = new SchemaMismatchException("Column mismatch during execution");

        // Act
        var code = ExitCodeMapper.FromException(ex, HostPhase.Execution);

        // Assert
        Assert.Equal(ExitCode.ExecutionFailed, code);
    }

    /// <summary>
    /// Verifies that SyncExecutionException during Execution returns ExecutionFailed.
    /// </summary>
    [Fact]
    public void FromException_SyncExecutionException_ShouldReturnExecutionFailed()
    {
        // Arrange
        var ex = new SyncExecutionException("Database write failed");

        // Act
        var code = ExitCodeMapper.FromException(ex, HostPhase.Execution);

        // Assert
        Assert.Equal(ExitCode.ExecutionFailed, code);
    }

    /// <summary>
    /// Verifies that unknown exceptions during Preflight return PreflightFailed.
    /// </summary>
    [Fact]
    public void FromException_UnknownExceptionInPreflight_ShouldReturnPreflightFailed()
    {
        // Arrange
        var ex = new InvalidOperationException("Unknown preflight issue");

        // Act
        var code = ExitCodeMapper.FromException(ex, HostPhase.Preflight);

        // Assert
        Assert.Equal(ExitCode.PreflightFailed, code);
    }

    /// <summary>
    /// Verifies that unknown exceptions during Execution return UnhandledFatal.
    /// </summary>
    [Fact]
    public void FromException_UnknownExceptionInExecution_ShouldReturnUnhandledFatal()
    {
        // Arrange
        var ex = new InvalidOperationException("Unexpected execution error");

        // Act
        var code = ExitCodeMapper.FromException(ex, HostPhase.Execution);

        // Assert
        Assert.Equal(ExitCode.UnhandledFatal, code);
    }

    /// <summary>
    /// Verifies that unknown exceptions during Bootstrap return UnhandledFatal.
    /// </summary>
    [Fact]
    public void FromException_UnknownExceptionInBootstrap_ShouldReturnUnhandledFatal()
    {
        // Arrange
        var ex = new InvalidOperationException("Bootstrap failure");

        // Act
        var code = ExitCodeMapper.FromException(ex, HostPhase.Bootstrap);

        // Assert
        Assert.Equal(ExitCode.UnhandledFatal, code);
    }

    /// <summary>
    /// Verifies that ArgumentException during Bootstrap returns UnhandledFatal
    /// (argument validation should be caught before this point).
    /// </summary>
    [Fact]
    public void FromException_ArgumentExceptionInBootstrap_ShouldReturnUnhandledFatal()
    {
        // Arrange
        var ex = new ArgumentException("Invalid argument");

        // Act
        var code = ExitCodeMapper.FromException(ex, HostPhase.Bootstrap);

        // Assert
        Assert.Equal(ExitCode.UnhandledFatal, code);
    }
}
