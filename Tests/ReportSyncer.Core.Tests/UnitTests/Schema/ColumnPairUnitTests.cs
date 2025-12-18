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
public class ColumnPairUnitTests
{
    [Fact]
    public void Ctor_Rejects_Blank_From_Or_To()
    {
        Action a1 = () => new ColumnPair(null!, "T");
        Action a2 = () => new ColumnPair(" ", "T");
        Action a3 = () => new ColumnPair("F", null!);
        Action a4 = () => new ColumnPair("F", " ");

        a1.Should().Throw<ArgumentException>();
        a2.Should().Throw<ArgumentException>();
        a3.Should().Throw<ArgumentException>();
        a4.Should().Throw<ArgumentException>();
    }
}
