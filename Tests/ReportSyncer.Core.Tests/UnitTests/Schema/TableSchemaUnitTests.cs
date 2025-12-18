using FluentAssertions;
using ReportSyncer.Core.Schema;

#pragma warning disable CA1806
namespace ReportSyncer.Core.Tests.UnitTests.Schema;

[Trait("Type", "UnitTest")]
[Trait("Area", "Schema")]
public class TableSchemaUnitTests
{
    [Fact]
    public void Ctor_Rejects_Null_Table()
    {
        Action act = () => new TableSchema(null!, new List<ColumnSchema> { new ColumnSchema("C", typeof(int), "int", false, false, false, null) }, null, null);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Null_Pk_And_Fks_Default_To_Empty_And_GetColumn_Is_Case_Insensitive()
    {
        var tableId = new TableIdentifier("dbo", "T");
        var col = new ColumnSchema("MyCol", typeof(string), "nvarchar", true, false, false, 50);
        var ts = new TableSchema(tableId, new List<ColumnSchema> { col }, null, null);

        ts.PrimaryKeyColumns.Should().BeEmpty();
        ts.ForeignKeys.Should().BeEmpty();

        var found = ts.GetColumn("mycol");
        found.Should().NotBeNull();
        found!.Name.Should().Be("MyCol");

        ts.GetColumn(" ").Should().BeNull();
        ts.GetColumn("NotExists").Should().BeNull();
    }
}
