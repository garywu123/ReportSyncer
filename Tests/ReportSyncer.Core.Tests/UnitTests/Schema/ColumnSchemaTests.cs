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

public class ColumnSchemaTests
{
    [Fact]
    [Trait("Type","Unit")]
    [Trait("Area","Schema")]
    public void Ctor_Rejects_Blank_Name()
    {
        Action act1 = () => new ColumnSchema(null!, typeof(string), "varchar", false, false, false, null);
        Action act2 = () => new ColumnSchema(" ", typeof(string), "varchar", false, false, false, null);

        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Type","Unit")]
    [Trait("Area","Schema")]
    public void Null_ClrType_Defaults_To_Object_And_Blank_DbType_Becomes_Empty()
    {
        var col = new ColumnSchema("C", null!, " ", true, true, true, 0);

        col.ClrType.Should().Be(typeof(object));
        col.DbType.Should().Be(string.Empty);
    }

    [Fact]
    [Trait("Type","Unit")]
    [Trait("Area","Schema")]
    public void Flags_And_MaxLength_Are_Preserved()
    {
        var col = new ColumnSchema("C", typeof(int), "int", true, true, false, 123);

        col.IsNullable.Should().BeTrue();
        col.IsIdentity.Should().BeTrue();
        col.IsPrimaryKeyPart.Should().BeFalse();
        col.MaxLength.Should().Be(123);
    }
}
