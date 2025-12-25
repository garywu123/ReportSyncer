// File: Tests/ReportSyncer.Core.Tests/UnitTests/Schema/Dependency/DependencyResolverUnitTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using Xunit;

namespace ReportSyncer.Core.Tests.UnitTests.Schema.Dependency;

public class DependencyResolverUnitTests
{
    private readonly IDependencyResolver _resolver = new DependencyResolver();

    [Fact]
    public void BuildExecutionPlan_WithNoForeignKeys_ReturnsAllTablesInAnyOrder()
    {
        var table1 = new TableIdentifier("dbo", "Table1");
        var table2 = new TableIdentifier("dbo", "Table2");

        var snapshot = CreateSnapshot(
            (table1, Array.Empty<ForeignKeySchema>()),
            (table2, Array.Empty<ForeignKeySchema>())
        );

        var selected = new[] { table1, table2 };

        var result = _resolver.BuildExecutionPlan(snapshot, selected);

        result.IsSuccess.Should().BeTrue();
        result.Value.InsertOrder.Should().HaveCount(2);
        result.Value.InsertOrder.Should().Contain(table1);
        result.Value.InsertOrder.Should().Contain(table2);
        result.Value.DeleteOrder.Should().Equal(result.Value.InsertOrder.Reverse());
    }

    [Fact]
    public void BuildExecutionPlan_WithSimpleParentChild_ReturnsParentBeforeChild()
    {
        var customers = new TableIdentifier("dbo", "Customers");
        var orders = new TableIdentifier("dbo", "Orders");

        var fkOrdersToCustomers = new ForeignKeySchema(
            "FK_Orders_Customers",
            fromTable: orders,
            toTable: customers,
            columnPairs: [new ColumnPair("CustomerId", "CustomerId")],
            isCascadeDelete: false
        );

        var snapshot = CreateSnapshot(
            (customers, Array.Empty<ForeignKeySchema>()),
            (orders, new[] { fkOrdersToCustomers })
        );

        var selected = new[] { customers, orders };

        var result = _resolver.BuildExecutionPlan(snapshot, selected);

        result.IsSuccess.Should().BeTrue();
        result.Value.InsertOrder.Should().Equal(customers, orders);
        result.Value.DeleteOrder.Should().Equal(orders, customers);
    }

    [Fact]
    public void BuildExecutionPlan_WithChainDependency_ReturnsCorrectOrder()
    {
        // A -> B -> C (A depends on B, B depends on C)
        var tableA = new TableIdentifier("dbo", "A");
        var tableB = new TableIdentifier("dbo", "B");
        var tableC = new TableIdentifier("dbo", "C");

        var fkAtoB = new ForeignKeySchema("FK_A_B", tableA, tableB,
            new[] { new ColumnPair("BId", "Id") }, false);
        var fkBtoC = new ForeignKeySchema("FK_B_C", tableB, tableC,
            new[] { new ColumnPair("CId", "Id") }, false);

        var snapshot = CreateSnapshot(
            (tableA, new[] { fkAtoB }),
            (tableB, new[] { fkBtoC }),
            (tableC, Array.Empty<ForeignKeySchema>())
        );

        var selected = new[] { tableA, tableB, tableC };

        var result = _resolver.BuildExecutionPlan(snapshot, selected);

        result.IsSuccess.Should().BeTrue();
        // Insert order should be C, B, A
        result.Value.InsertOrder.Should().Equal(tableC, tableB, tableA);
        result.Value.DeleteOrder.Should().Equal(tableA, tableB, tableC);
    }

    [Fact]
    public void BuildExecutionPlan_WithMissingParent_FailsWithClearError()
    {
        var customers = new TableIdentifier("dbo", "Customers");
        var orders = new TableIdentifier("dbo", "Orders");

        var fk = new ForeignKeySchema(
            "FK_Orders_Customers",
            fromTable: orders,
            toTable: customers,
            columnPairs: new[] { new ColumnPair("CustomerId", "CustomerId") },
            isCascadeDelete: false
        );

        var snapshot = CreateSnapshot(
            (customers, Array.Empty<ForeignKeySchema>()),
            (orders, new[] { fk })
        );

        var selected = new[] { orders }; // customers not selected

        var result = _resolver.BuildExecutionPlan(snapshot, selected);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Orders");
        result.Error.Should().Contain("Customers");
        result.Error.Should().Contain("not included");
    }

    [Fact]
    public void BuildExecutionPlan_WithCycle_FailsWithCycleError()
    {
        var tableA = new TableIdentifier("dbo", "TableA");
        var tableB = new TableIdentifier("dbo", "TableB");

        var fkAtoB = new ForeignKeySchema("FK_A_B", tableA, tableB,
            new[] { new ColumnPair("BId", "Id") }, false);
        var fkBtoA = new ForeignKeySchema("FK_B_A", tableB, tableA,
            new[] { new ColumnPair("AId", "Id") }, false);

        var snapshot = CreateSnapshot(
            (tableA, new[] { fkAtoB }),
            (tableB, new[] { fkBtoA })
        );

        var selected = new[] { tableA, tableB };

        var result = _resolver.BuildExecutionPlan(snapshot, selected);

        result.IsFailure.Should().BeTrue();
        ((result.Error ?? string.Empty).Contains("Circular") || (result.Error ?? string.Empty).Contains("Cycle")).Should().BeTrue();
    }

    [Fact]
    public void BuildExecutionPlan_WithMultipleMissingParents_ListsAllErrors()
    {
        var parent1 = new TableIdentifier("dbo", "Parent1");
        var parent2 = new TableIdentifier("dbo", "Parent2");
        var child1 = new TableIdentifier("dbo", "Child1");
        var child2 = new TableIdentifier("dbo", "Child2");

        var fk1 = new ForeignKeySchema("FK_Child1_Parent1", child1, parent1,
            new[] { new ColumnPair("P1Id", "Id") }, false);
        var fk2 = new ForeignKeySchema("FK_Child2_Parent2", child2, parent2,
            new[] { new ColumnPair("P2Id", "Id") }, false);

        var snapshot = CreateSnapshot(
            (parent1, Array.Empty<ForeignKeySchema>()),
            (parent2, Array.Empty<ForeignKeySchema>()),
            (child1, new[] { fk1 }),
            (child2, new[] { fk2 })
        );

        // Select only the children, leaving parents out
        var selected = new[] { child1, child2 };

        var result = _resolver.BuildExecutionPlan(snapshot, selected);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Parent1");
        result.Error.Should().Contain("Parent2");
    }

    [Fact]
    public void BuildExecutionPlan_DeleteOrder_IsReverseOfInsertOrder()
    {
        var a = new TableIdentifier("dbo", "A");
        var b = new TableIdentifier("dbo", "B");

        var fkAtoB = new ForeignKeySchema("FK_A_B", a, b,
            new[] { new ColumnPair("BId", "Id") }, false);

        var snapshot = CreateSnapshot(
            (a, new[] { fkAtoB }),
            (b, Array.Empty<ForeignKeySchema>())
        );

        var selected = new[] { a, b };

        var result = _resolver.BuildExecutionPlan(snapshot, selected);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeleteOrder.Should().Equal(result.Value.InsertOrder.Reverse());
    }

    private static SchemaSnapshot CreateSnapshot(
        params (TableIdentifier table, ForeignKeySchema[] fks)[] tables)
    {
        var tableSchemas = new Dictionary<TableIdentifier, TableSchema>();

        foreach (var (table, fks) in tables)
        {
            var schema = new TableSchema(
                table,
                Array.Empty<ColumnSchema>(),
                Array.Empty<string>(),
                fks
            );
            tableSchemas[table] = schema;
        }

        return new SchemaSnapshot(tableSchemas.Values, SchemaRole.Target, SchemaInspectionLevel.Full);
    }
}
