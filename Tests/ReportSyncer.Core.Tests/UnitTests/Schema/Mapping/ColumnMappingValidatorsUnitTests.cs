using FluentAssertions;
using ReportSyncer.Core.Schema.Mapping;

namespace ReportSyncer.Core.Tests.UnitTests.Schema.Mapping;

[Trait("Type", "UnitTest")]
[Trait("Area", "Schema.Mapping")]
public class ColumnMappingValidatorsUnitTests
{
    [Fact]
    public void TypeCompatibilityValidator_ReturnsNull_When_TypesCompatible()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("col1", typeof(int), "int"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("col1", typeof(int), "int"));

        var mapping = new ColumnMapping(src.Columns.First(), tgt.Columns.First(), MappingKind.OneToOne, null);
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), null, src, tgt);

        var v = new TypeCompatibilityValidator();
        var res = v.Validate(mapping, ctx);

        res.Should().BeNull();
    }

    [Fact]
    public void TypeCompatibilityValidator_ReturnsError_When_TypesIncompatible()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("c", typeof(int), "int"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("c", typeof(string), "varchar"));

        var mapping = new ColumnMapping(src.Columns.First(), tgt.Columns.First(), MappingKind.OneToOne, null);
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), null, src, tgt);

        var v = new TypeCompatibilityValidator();
        var res = v.Validate(mapping, ctx);

        res.Should().NotBeNull();
        res!.Code.Should().Be(SchemaMappingErrorCode.TargetColumnTypeIncompatible);
    }

    [Fact]
    public void WritabilityValidator_ReturnsError_For_ComputedColumn()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("a"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("a", isComputed: true));

        var mapping = new ColumnMapping(src.Columns.First(), tgt.Columns.First(), MappingKind.OneToOne, null);
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), null, src, tgt);

        var v = new WritabilityValidator();
        var res = v.Validate(mapping, ctx);

        res.Should().NotBeNull();
        res!.Code.Should().Be(SchemaMappingErrorCode.ComputedColumnCannotBeWritten);
    }

    [Fact]
    public void WritabilityValidator_ReturnsError_For_RowVersion()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("a"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("a", isRowVersion: true));

        var mapping = new ColumnMapping(src.Columns.First(), tgt.Columns.First(), MappingKind.OneToOne, null);
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), null, src, tgt);

        var v = new WritabilityValidator();
        var res = v.Validate(mapping, ctx);

        res.Should().NotBeNull();
        res!.Code.Should().Be(SchemaMappingErrorCode.RowVersionColumnCannotBeWritten);
    }

    [Fact]
    public void WritabilityValidator_ReturnsError_For_IdentityWithoutInsertEnabled()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("id"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("id", isIdentity: true));

        var mapping = new ColumnMapping(src.Columns.First(), tgt.Columns.First(), MappingKind.OneToOne, null);
        var tt = MappingTestBuilder.TableTask(enableIdentityInsert: false);
        var ctx = MappingTestBuilder.Context(tt, null, src, tgt);

        var v = new WritabilityValidator();
        var res = v.Validate(mapping, ctx);

        res.Should().NotBeNull();
        res!.Code.Should().Be(SchemaMappingErrorCode.IdentityInsertNotEnabled);
    }

    [Fact]
    public void WritabilityValidator_Allows_WritableColumns()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("c"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("c"));

        var mapping = new ColumnMapping(src.Columns.First(), tgt.Columns.First(), MappingKind.OneToOne, null);
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), null, src, tgt);

        var v = new WritabilityValidator();
        var res = v.Validate(mapping, ctx);

        res.Should().BeNull();
    }
}
