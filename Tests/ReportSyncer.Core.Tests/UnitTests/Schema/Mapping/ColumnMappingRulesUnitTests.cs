using FluentAssertions;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Schema.Mapping;

namespace ReportSyncer.Core.Tests.UnitTests.Schema.Mapping;

[Trait("Type", "UnitTest")]
[Trait("Area", "Schema.Mapping")]
public class ColumnMappingRulesUnitTests
{
    #region ExplicitIgnoreRule

    [Fact]
    public void ExplicitIgnoreRule_ReturnsIgnored_When_ExplicitRuleIgnoreTrue()
    {
        var targetCol = MappingTestBuilder.Column("T", typeof(string));
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("T", typeof(string)));
        var tgt = MappingTestBuilder.Table("Tgt", targetCol);

        var explicitRule = new ColumnMappingRule { Ignore = true };
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), explicitRule, src, tgt, null, MappingTestBuilder.SchemaPolicy());

        var rule = new ExplicitIgnoreRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeTrue();
        res.Mapping.Should().NotBeNull();
        res.Mapping!.Kind.Should().Be(MappingKind.Ignored);
    }

    [Fact]
    public void ExplicitIgnoreRule_NotHandled_When_NoExplicitRule()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("A"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("A"));

        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), null, src, tgt);

        var rule = new ExplicitIgnoreRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeFalse();
    }

    [Fact]
    public void ExplicitIgnoreRule_Prioritization_WithOtherRules()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("Same"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("Same"));

        var explicitRule = new ColumnMappingRule { Ignore = true, FromParameter = "p1" };
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(columnMapping: null), explicitRule, src, tgt, new Dictionary<string, string>{{"p1","v"}});

        var chain = new IColumnMappingRule[]
        {
            new ExplicitIgnoreRule(),
            new FromParameterRule(),
            new ConstRule(),
            new FromSourceRule(),
            new ExplicitFallbackIgnoreRule(),
            new AutomapRule(),
            new ExtraTargetAllowedRule(),
            new MissingSourceRule()
        };

        ColumnResolution? chosen = null;
        foreach (var r in chain)
        {
            var rr = r.TryMap(ctx);
            if (rr.Handled)
            {
                chosen = rr;
                break;
            }
        }

        chosen.Should().NotBeNull();
        chosen!.Mapping.Should().NotBeNull();
        chosen.Mapping!.Kind.Should().Be(MappingKind.Ignored);
    }

    #endregion

    #region FromParameterRule

    [Fact]
    public void FromParameterRule_ReturnsError_When_ParameterMissing()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("A"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("A"));

        var explicitRule = new ColumnMappingRule { FromParameter = "p1" };
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), explicitRule, src, tgt, new Dictionary<string, string>());

        var rule = new FromParameterRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeTrue();
        res.Error.Should().NotBeNull();
        res.Error!.Code.Should().Be(SchemaMappingErrorCode.JobParameterMissingForMapping);
    }

    [Fact]
    public void FromParameterRule_ReturnsConstant_When_ParameterExists()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("A"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("A"));

        var explicitRule = new ColumnMappingRule { FromParameter = "p1" };
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), explicitRule, src, tgt, new Dictionary<string, string>{{"p1","val"}});

        var rule = new FromParameterRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeTrue();
        res.Mapping.Should().NotBeNull();
        res.Mapping!.Kind.Should().Be(MappingKind.Constant);
        res.Mapping.ConstantValue.Should().Be("val");
    }

    [Fact]
    public void FromParameterRule_Ignored_If_ExplicitIgnorePresent()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("Same"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("Same"));

        var explicitRule = new ColumnMappingRule { Ignore = true, FromParameter = "p1" };
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), explicitRule, src, tgt, new Dictionary<string, string>{{"p1","v"}});

        var chain = new IColumnMappingRule[] { new ExplicitIgnoreRule(), new FromParameterRule() };
        ColumnResolution? chosen = null;
        foreach (var r in chain)
        {
            var rr = r.TryMap(ctx);
            if (rr.Handled) { chosen = rr; break; }
        }

        chosen.Should().NotBeNull();
        chosen!.Mapping.Should().NotBeNull();
        chosen.Mapping!.Kind.Should().Be(MappingKind.Ignored);
    }

    #endregion

    #region ConstRule

    [Fact]
    public void ConstRule_ReturnsConstant_When_ConstProvided()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("A"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("A"));

        var explicitRule = new ColumnMappingRule { Const = "42" };
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), explicitRule, src, tgt);

        var rule = new ConstRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeTrue();
        res.Mapping.Should().NotBeNull();
        res.Mapping!.Kind.Should().Be(MappingKind.Constant);
        res.Mapping.ConstantValue.Should().Be("42");
    }

    [Fact]
    public void ConstRule_NotHandled_When_NoConst()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("A"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("A"));

        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), null, src, tgt);

        var rule = new ConstRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeFalse();
    }

    [Fact]
    public void ConstRule_Priority_Over_Automap()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("Same", typeof(string)));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("Same", typeof(string)));

        var explicitRule = new ColumnMappingRule { Const = "constVal" };
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), explicitRule, src, tgt);

        var chain = new IColumnMappingRule[] { new ConstRule(), new AutomapRule() };
        ColumnResolution? chosen = null;
        foreach (var r in chain)
        {
            var rr = r.TryMap(ctx);
            if (rr.Handled) { chosen = rr; break; }
        }

        chosen.Should().NotBeNull();
        chosen!.Mapping.Should().NotBeNull();
        chosen.Mapping!.Kind.Should().Be(MappingKind.Constant);
    }

    #endregion

    #region FromSourceRule

    [Fact]
    public void FromSourceRule_ReturnsOneToOne_When_SourceExists()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("col1", typeof(int), "int"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("col1", typeof(int), "int"));

        var explicitRule = new ColumnMappingRule { FromSource = "col1" };
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), explicitRule, src, tgt);

        var rule = new FromSourceRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeTrue();
        res.Mapping.Should().NotBeNull();
        res.Mapping!.Kind.Should().Be(MappingKind.OneToOne);
        res.Mapping.SourceColumn.Should().NotBeNull();
        res.Mapping.SourceColumn!.Name.Should().Be("col1");
    }

    [Fact]
    public void FromSourceRule_ReturnsError_When_SourceMissing()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("other"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("colX"));

        var explicitRule = new ColumnMappingRule { FromSource = "colX" };
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), explicitRule, src, tgt);

        var rule = new FromSourceRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeTrue();
        res.Error.Should().NotBeNull();
        res.Error!.Code.Should().Be(SchemaMappingErrorCode.TargetColumnMissingInSource);
    }

    [Fact]
    public void FromSourceRule_NotHandled_When_FromSourceNotSet()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("A"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("A"));

        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), null, src, tgt);

        var rule = new FromSourceRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeFalse();
    }

    #endregion

    #region ExplicitFallbackIgnoreRule

    [Fact]
    public void ExplicitFallbackIgnoreRule_ReturnsIgnored_When_ExplicitRuleButNoDirective()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("A"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("A"));

        var explicitRule = new ColumnMappingRule { /* no directive set */ };
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), explicitRule, src, tgt);

        var rule = new ExplicitFallbackIgnoreRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeTrue();
        res.Mapping.Should().NotBeNull();
        res.Mapping!.Kind.Should().Be(MappingKind.Ignored);
    }

    [Fact]
    public void ExplicitFallbackIgnoreRule_NotHandled_When_NoExplicitRule()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("A"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("A"));

        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), null, src, tgt);

        var rule = new ExplicitFallbackIgnoreRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeFalse();
    }

    #endregion

    #region AutomapRule

    [Fact]
    public void AutomapRule_ReturnsOneToOne_When_AutomapEnabled_And_SameNameSourceExists()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("Same", typeof(string)));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("Same", typeof(string)));

        var columnMapping = new ColumnMappingConfig(automapByName: true, mappings: null);
        var tableTask = MappingTestBuilder.TableTask(source: "dbo.Src", target: "dbo.Tgt", enableIdentityInsert: false, columnMapping: columnMapping);

        var ctx = MappingTestBuilder.Context(tableTask, null, src, tgt);

        var rule = new AutomapRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeTrue();
        res.Mapping.Should().NotBeNull();
        res.Mapping!.Kind.Should().Be(MappingKind.OneToOne);
    }

    [Fact]
    public void AutomapRule_NotHandled_When_AutomapDisabled()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("Same", typeof(string)));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("Same", typeof(string)));

        var columnMapping = new ColumnMappingConfig(automapByName: false, mappings: null);
        var tableTask = MappingTestBuilder.TableTask(source: "dbo.Src", target: "dbo.Tgt", enableIdentityInsert: false, columnMapping: columnMapping);
        var ctx = MappingTestBuilder.Context(tableTask, null, src, tgt);

        var rule = new AutomapRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeFalse();
    }

    [Fact]
    public void AutomapRule_DoesNotOverride_ExplicitRule()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("Same", typeof(string)));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("Same", typeof(string)));

        var explicitRule = new ColumnMappingRule { FromSource = "Same" };
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), explicitRule, src, tgt);

        var rule = new AutomapRule();
        var res = rule.TryMap(ctx);

        // Automap should not run when explicit rule exists
        res.Handled.Should().BeFalse();
    }

    #endregion

    #region ExtraTargetAllowedRule

    [Fact]
    public void ExtraTargetAllowedRule_ReturnsIgnored_When_PolicyAllows_And_ColumnSafeToIgnore()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("A"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("X", isNullable: true));

        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), null, src, tgt, null, MappingTestBuilder.SchemaPolicy(allowExtraTarget: true), targetColumnOverride: tgt.Columns[0]);

        var rule = new ExtraTargetAllowedRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeTrue();
        res.Mapping.Should().NotBeNull();
        res.Mapping!.Kind.Should().Be(MappingKind.Ignored);
    }

    [Fact]
    public void ExtraTargetAllowedRule_NotHandled_When_PolicyDisallows()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("A"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("X", isNullable: true));

        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), null, src, tgt, null, MappingTestBuilder.SchemaPolicy(allowExtraTarget: false), targetColumnOverride: tgt.Columns[0]);

        var rule = new ExtraTargetAllowedRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeFalse();
    }

    [Fact]
    public void ExtraTargetAllowedRule_NotApplied_When_ExplicitRuleExists()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("A"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("X", isNullable: true));

        var explicitRule = new ColumnMappingRule { Ignore = true };
        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), explicitRule, src, tgt, null, MappingTestBuilder.SchemaPolicy(allowExtraTarget: true), targetColumnOverride: tgt.Columns[0]);

        var rule = new ExtraTargetAllowedRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeFalse();
    }

    #endregion

    #region MissingSourceRule

    [Fact]
    public void MissingSourceRule_ReturnsError_When_NoPriorRuleHandled()
    {
        var src = MappingTestBuilder.Table("Src", MappingTestBuilder.Column("A"));
        var tgt = MappingTestBuilder.Table("Tgt", MappingTestBuilder.Column("MissingCol"));

        var ctx = MappingTestBuilder.Context(MappingTestBuilder.TableTask(), null, src, tgt);

        var rule = new MissingSourceRule();
        var res = rule.TryMap(ctx);

        res.Handled.Should().BeTrue();
        res.Error.Should().NotBeNull();
        res.Error!.Code.Should().Be(SchemaMappingErrorCode.TargetColumnMissingInSource);
    }

    #endregion

}
