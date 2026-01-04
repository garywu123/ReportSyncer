// ============================================================================
// File: ConnectionStringResolverTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Unit tests for ConnectionStringResolver.
// ============================================================================

using FluentAssertions;
using ReportSyncer.Console.Hosting;
using ReportSyncer.Core.Configuration;

namespace ReportSyncer.Console.Tests.Hosting;

/// <summary>
/// Tests for <see cref="ConnectionStringResolver"/>.
/// </summary>
public sealed class ConnectionStringResolverTests
{
    [Fact]
    public void Constructor_WithValidConnections_BuildsMap()
    {
        // Arrange
        var connections = new[]
        {
            new ConnectionConfig("SRC", "Server=src;", EnvironmentType.Dev, ConnectionType.Application),
            new ConnectionConfig("TGT", "Server=tgt;", EnvironmentType.Dev, ConnectionType.Reporting)
        };

        // Act
        var resolver = new ConnectionStringResolver(connections);

        // Assert
        resolver.Get("SRC").Should().Be("Server=src;");
        resolver.Get("TGT").Should().Be("Server=tgt;");
    }

    [Fact]
    public void Get_WithExistingConnectionName_ReturnsConnectionString()
    {
        // Arrange
        var connections = new[]
        {
            new ConnectionConfig("SRC", "Server=src;Database=AppDB;", EnvironmentType.Dev, ConnectionType.Application)
        };
        var resolver = new ConnectionStringResolver(connections);

        // Act
        var result = resolver.Get("SRC");

        // Assert
        result.Should().Be("Server=src;Database=AppDB;");
    }

    [Fact]
    public void Get_WithMissingConnectionName_ThrowsInvalidOperationException()
    {
        // Arrange
        var connections = new[]
        {
            new ConnectionConfig("SRC", "Server=src;", EnvironmentType.Dev, ConnectionType.Application)
        };
        var resolver = new ConnectionStringResolver(connections);

        // Act
        var act = () => resolver.Get("MISSING");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MISSING*")
            .WithMessage("*connections*");
    }

    [Fact]
    public void Constructor_WithDuplicateConnectionNames_ThrowsInvalidOperationException()
    {
        // Arrange
        var connections = new[]
        {
            new ConnectionConfig("SRC", "Server=src1;", EnvironmentType.Dev, ConnectionType.Application),
            new ConnectionConfig("SRC", "Server=src2;", EnvironmentType.Prod, ConnectionType.Application)
        };

        // Act
        var act = () => new ConnectionStringResolver(connections);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SRC*")
            .WithMessage("*Duplicate*");
    }

    [Fact]
    public void Constructor_WithNullConnections_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ConnectionStringResolver(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Get_WithNullOrWhitespaceConnectionName_ThrowsArgumentException()
    {
        // Arrange
        var connections = new[]
        {
            new ConnectionConfig("SRC", "Server=src;", EnvironmentType.Dev, ConnectionType.Application)
        };
        var resolver = new ConnectionStringResolver(connections);

        // Act & Assert
        var actNull = () => resolver.Get(null!);
        actNull.Should().Throw<ArgumentException>();

        var actEmpty = () => resolver.Get("");
        actEmpty.Should().Throw<ArgumentException>();

        var actWhitespace = () => resolver.Get("   ");
        actWhitespace.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Get_WithCaseInsensitiveMatch_ReturnsConnectionString()
    {
        // Arrange
        var connections = new[]
        {
            new ConnectionConfig("SRC", "Server=src;", EnvironmentType.Dev, ConnectionType.Application)
        };
        var resolver = new ConnectionStringResolver(connections);

        // Act
        var result = resolver.Get("src"); // lowercase

        // Assert
        result.Should().Be("Server=src;");
    }
}
