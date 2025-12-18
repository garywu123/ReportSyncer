using FluentAssertions;
using ReportSyncer.Core.Schema;

#pragma warning disable CA1806
namespace ReportSyncer.Core.Tests.UnitTests.Schema;

[Trait("Type", "UnitTest")]
[Trait("Area", "Schema")]
public class ForeignKeySchemaUnitTests
{
    [Fact]
    public void Ctor_Rejects_Blank_Name_Or_Null_Tables_Or_Empty_Pairs()
    {
        var t = new TableIdentifier("dbo", "T");
        var pairs = new List<ColumnPair> { new ColumnPair("a", "b") };

        Action a1 = () => new ForeignKeySchema(null!, t, t, pairs, false);
        Action a2 = () => new ForeignKeySchema(" ", t, t, pairs, false);
        Action a3 = () => new ForeignKeySchema("fk", null!, t, pairs, false);
        Action a4 = () => new ForeignKeySchema("fk", t, null!, pairs, false);
        Action a5 = () => new ForeignKeySchema("fk", t, t, null!, false);
        Action a6 = () => new ForeignKeySchema("fk", t, t, new List<ColumnPair>(), false);

        a1.Should().Throw<ArgumentException>();
        a2.Should().Throw<ArgumentException>();
        a3.Should().Throw<ArgumentNullException>();
        a4.Should().Throw<ArgumentNullException>();
        a5.Should().Throw<ArgumentException>();
        a6.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_Preserves_Cascade_And_ColumnPair_Order()
    {
        var from = new TableIdentifier("dbo", "Child");
        var to = new TableIdentifier("dbo", "Parent");
        var pairs = new List<ColumnPair>
        {
            new ColumnPair("c1", "p1"),
            new ColumnPair("c2", "p2")
        };

        var fk = new ForeignKeySchema("FK_Child_Parent", from, to, pairs, true);

        fk.Name.Should().Be("FK_Child_Parent");
        fk.FromTable.Should().BeEquivalentTo(from);
        fk.ToTable.Should().BeEquivalentTo(to);
        fk.IsCascadeDelete.Should().BeTrue();
        fk.ColumnPairs.Should().HaveCount(2);
        fk.ColumnPairs[0].FromColumn.Should().Be("c1");
        fk.ColumnPairs[1].ToColumn.Should().Be("p2");
    }
}
