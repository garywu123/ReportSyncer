using FluentAssertions;
using ReportSyncer.Core.Configuration;

namespace ReportSyncer.Core.Tests.UnitTests.Configuration;

[Trait("Type", "UnitTest")]
[Trait("Area", "Configuration")]
public class TableTaskConfigUnitTests
{
    [Fact]
    public void Constructor_WhenIgnoreDependenciesProvided_SetsProperty()
    {
        var table = new TableTaskConfig("dbo.S", "dbo.T", ignoreDependencies: true);

        table.IgnoreDependencies.Should().BeTrue();
    }

    [Fact]
    public void Constructor_DefaultsIgnoreDependenciesToFalse()
    {
        var table = new TableTaskConfig("dbo.S", "dbo.T");

        table.IgnoreDependencies.Should().BeFalse();
    }
}
