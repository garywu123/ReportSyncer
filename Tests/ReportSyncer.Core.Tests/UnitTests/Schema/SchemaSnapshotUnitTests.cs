using FluentAssertions;
using ReportSyncer.Core.Schema;

#pragma warning disable CA1806
namespace ReportSyncer.Core.Tests.UnitTests.Schema;

[Trait("Type", "UnitTest")]
[Trait("Area", "Schema")]
public class SchemaSnapshotUnitTests
{
    [Fact]
    public void Ctor_Rejects_Null_Tables()
    {
        Action act = () => new SchemaSnapshot(null!, SchemaRole.Source, SchemaInspectionLevel.ExistenceOnly);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Lookup_Is_Case_Insensitive_And_GetRequired_Throws_For_Missing()
    {
        var t = new TableIdentifier("dbo", "T1");
        var ts = new TableSchema(t, new List<ColumnSchema>(), null, null);
        var snap = new SchemaSnapshot([ts], SchemaRole.Target, SchemaInspectionLevel.ExistenceOnly);

        snap.TryGetTable(new TableIdentifier("DBO", "t1"), out var found).Should().BeTrue();
        found.Should().NotBeNull();

        Action act = () => snap.GetRequiredTable(new TableIdentifier("dbo", "missing"));
        act.Should().Throw<KeyNotFoundException>().WithMessage("*missing*");
    }

    [Fact]
    public void Tables_Property_Returns_Materialized_Copy()
    {
        var t1 = new TableIdentifier("dbo", "A");
        var t2 = new TableIdentifier("dbo", "B");
        var ts1 = new TableSchema(t1, new List<ColumnSchema>(), null, null);
        var ts2 = new TableSchema(t2, new List<ColumnSchema>(), null, null);
        var snap = new SchemaSnapshot([ts1, ts2], SchemaRole.Source, SchemaInspectionLevel.ExistenceOnly);

        var first = snap.Tables;
        var second = snap.Tables;

        // should be equal in content but not the same reference (materialized each call)
        first.Should().BeEquivalentTo(second);
        ReferenceEquals(first, second).Should().BeFalse();
    }
}
