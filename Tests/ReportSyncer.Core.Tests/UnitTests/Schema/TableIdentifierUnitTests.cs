#region License

// author:         GWu
// created:        15:12
// description:

#endregion

using FluentAssertions;
using ReportSyncer.Core.Schema;
// ReSharper disable ObjectCreationAsStatement
#pragma warning disable CA1806

namespace ReportSyncer.Core.Tests.UnitTests.Schema;

[Trait("Type", "UnitTest")]
[Trait("Area", "Schema")]
public class TableIdentifierUnitTests
{
    [Fact]
    public void Ctor_Rejects_Blank_Schema_Or_Table()
    {
        Action a1 = () => new TableIdentifier(null!, "T");
        Action a2 = () => new TableIdentifier("", "T");
        Action a3 = () => new TableIdentifier("s", null!);
        Action a4 = () => new TableIdentifier("s", " ");

        a1.Should().Throw<ArgumentException>();
        a2.Should().Throw<ArgumentException>();
        a3.Should().Throw<ArgumentException>();
        a4.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_Is_Case_Insensitive_But_Schema_Must_Match()
    {
        var a = new TableIdentifier("dbo", "Customer");
        var b = new TableIdentifier("DBO", "CUSTOMER");
        var c = new TableIdentifier("report", "Customer");

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());

        a.Equals(c).Should().BeFalse();
    }

    [Fact]
    public void ToString_Formats_Schema_Dot_Table()
    {
        var t = new TableIdentifier("dbo","MyTable");
        t.ToString().Should().Be("dbo.MyTable");
    }
}
