using FluentAssertions;
using ReportSyncer.Core.Schema.Mapping;

namespace ReportSyncer.Core.Tests.UnitTests.Schema.Mapping;

[Trait("Type", "UnitTest")]
[Trait("Area", "Schema.Mapping")]
public class ColumnMappingHelpersUnitTests
{
    [Fact]
    public void CreateError_ReturnsConsistentError_WithExpectedCodeAndMessage()
    {
        var src = MappingTestBuilder.Table("S", MappingTestBuilder.Column("c", typeof(int), "int"));
        var tgt = MappingTestBuilder.Table("T", MappingTestBuilder.Column("c", typeof(string), "varchar"));

        var err = ColumnMappingHelpers.CreateError(
            SchemaMappingErrorCode.TargetColumnTypeIncompatible,
            src.Table,
            tgt.Table,
            "c",
            "int",
            "varchar"
        );

        err.Should().NotBeNull();
        err.Code.Should().Be(SchemaMappingErrorCode.TargetColumnTypeIncompatible);
        err.Message.Should().Contain("Incompatible types");
    }

    [Fact]
    public void IsTypeCompatible_Covers_CommonFamilies()
    {
        var s1 = MappingTestBuilder.Column("x", typeof(string), "varchar");
        var t1 = MappingTestBuilder.Column("x", typeof(string), "nvarchar");
        ColumnMappingHelpers.IsTypeCompatible(s1, t1).Should().BeTrue();

        var s2 = MappingTestBuilder.Column("n", typeof(int), "int");
        var t2 = MappingTestBuilder.Column("n", typeof(string), "varchar");
        ColumnMappingHelpers.IsTypeCompatible(s2, t2).Should().BeFalse();
    }

    [Fact]
    public void TryGetJobParameter_ReturnsValue_When_Found_CaseInsensitive()
    {
        var p = new Dictionary<string, string> { { "p1", "val" } };
        var ok = ColumnMappingHelpers.TryGetJobParameter(p, "P1", out var v);
        ok.Should().BeTrue();
        v.Should().Be("val");
    }

    [Fact]
    public void GetColumnWritabilityError_BehaviorCases()
    {
        var computed = MappingTestBuilder.Column("c", isComputed: true);
        ColumnMappingHelpers.GetColumnWritabilityError(computed, MappingKind.OneToOne, false)
            .Should().Be(SchemaMappingErrorCode.ComputedColumnCannotBeWritten);

        var ignoredCol = MappingTestBuilder.Column("c2");
        ColumnMappingHelpers.GetColumnWritabilityError(ignoredCol, MappingKind.Ignored, false)
            .Should().BeNull();

        var identity = MappingTestBuilder.Column("id", isIdentity: true);
        ColumnMappingHelpers.GetColumnWritabilityError(identity, MappingKind.OneToOne, false)
            .Should().Be(SchemaMappingErrorCode.IdentityInsertNotEnabled);
    }
}
