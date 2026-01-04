// ============================================================================
// File: AreaBPagerTests.cs
// Author: Gary Wu
// Project: ReportSyncer.Console.Tests
// Date: January 5, 2026
// Description: Tests for AreaBPager page clamping and selection logic.
// ============================================================================

using FluentAssertions;
using ReportSyncer.Console.UI;
using ReportSyncer.Core.Sync.Contracts;
using Xunit;

namespace ReportSyncer.Console.Tests.UI;

public sealed class AreaBPagerTests
{
    [Fact]
    public void Constructor_WithDefaultPageSize_UsesDefaultOf20()
    {
        // Act
        var pager = new AreaBPager();

        // Assert
        pager.PageSize.Should().Be(20);
        pager.PageIndex.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithPositivePageSize_UsesThatValue()
    {
        // Act
        var pager = new AreaBPager(pageSize: 10);

        // Assert
        pager.PageSize.Should().Be(10);
    }

    [Fact]
    public void Constructor_WithZeroPageSize_NormalizesTo20()
    {
        // Act
        var pager = new AreaBPager(pageSize: 0);

        // Assert
        pager.PageSize.Should().Be(20);
    }

    [Fact]
    public void Constructor_WithNegativePageSize_NormalizesTo20()
    {
        // Act
        var pager = new AreaBPager(pageSize: -5);

        // Assert
        pager.PageSize.Should().Be(20);
    }

    [Fact]
    public void GetPageInfo_WithZeroTotal_ReturnsOnePageWithZeroRows()
    {
        // Arrange
        var pager = new AreaBPager(pageSize: 20);

        // Act
        var pageInfo = pager.GetPageInfo(totalCount: 0);

        // Assert
        pageInfo.PageCount.Should().Be(1);
        pageInfo.PageIndex.Should().Be(0);
        pageInfo.StartRowNumber.Should().Be(0);
        pageInfo.EndRowNumber.Should().Be(0);
        pageInfo.TotalCount.Should().Be(0);
    }

    [Fact]
    public void GetPageInfo_WithNegativePageIndex_ClampsToZero()
    {
        // Arrange
        var pager = new AreaBPager(pageSize: 20);
        pager.PageIndex = -3;

        // Act
        var pageInfo = pager.GetPageInfo(totalCount: 100);

        // Assert
        pageInfo.PageIndex.Should().Be(0);
        pager.PageIndex.Should().Be(0); // Should be clamped in pager too
    }

    [Fact]
    public void GetPageInfo_WithPageIndexBeyondMax_ClampsToLastPage()
    {
        // Arrange
        var pager = new AreaBPager(pageSize: 20);
        pager.PageIndex = 999;

        // Act
        var pageInfo = pager.GetPageInfo(totalCount: 21);

        // Assert
        pageInfo.PageCount.Should().Be(2); // ceil(21/20) = 2
        pageInfo.PageIndex.Should().Be(1); // Last page index
        pager.PageIndex.Should().Be(1);
    }

    [Fact]
    public void GetPageInfo_WithExactPageBoundary_CalculatesCorrectly()
    {
        // Arrange
        var pager = new AreaBPager(pageSize: 20);
        pager.PageIndex = 0;

        // Act
        var pageInfo = pager.GetPageInfo(totalCount: 40);

        // Assert
        pageInfo.PageCount.Should().Be(2);
        pageInfo.StartRowNumber.Should().Be(1);
        pageInfo.EndRowNumber.Should().Be(20);
    }

    [Fact]
    public void GetPageInfo_OnSecondPage_CalculatesCorrectRowNumbers()
    {
        // Arrange
        var pager = new AreaBPager(pageSize: 20);
        pager.PageIndex = 1;

        // Act
        var pageInfo = pager.GetPageInfo(totalCount: 50);

        // Assert
        pageInfo.StartRowNumber.Should().Be(21); // 1-based
        pageInfo.EndRowNumber.Should().Be(40);
        pageInfo.TotalCount.Should().Be(50);
    }

    [Fact]
    public void GetPageInfo_OnLastPartialPage_EndsAtTotalCount()
    {
        // Arrange
        var pager = new AreaBPager(pageSize: 20);
        pager.PageIndex = 1;

        // Act
        var pageInfo = pager.GetPageInfo(totalCount: 25);

        // Assert
        pageInfo.PageCount.Should().Be(2);
        pageInfo.StartRowNumber.Should().Be(21);
        pageInfo.EndRowNumber.Should().Be(25);
    }

    [Fact]
    public void SelectPage_WithEmptyInput_ReturnsEmpty()
    {
        // Arrange
        var pager = new AreaBPager(pageSize: 20);
        var rows = Array.Empty<TableRowState>();

        // Act
        var result = pager.SelectPage(rows);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SelectPage_WithNullInput_ReturnsEmpty()
    {
        // Arrange
        var pager = new AreaBPager(pageSize: 20);

        // Act
        var result = pager.SelectPage(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SelectPage_FirstPage_ReturnsFirstPageSizeItems()
    {
        // Arrange
        var pager = new AreaBPager(pageSize: 3);
        var rows = CreateTestRows(10);

        // Act
        var result = pager.SelectPage(rows);

        // Assert
        result.Should().HaveCount(3);
        result[0].TableName.Should().Be("Table_0");
        result[1].TableName.Should().Be("Table_1");
        result[2].TableName.Should().Be("Table_2");
    }

    [Fact]
    public void SelectPage_SecondPage_ReturnsSecondPageItems()
    {
        // Arrange
        var pager = new AreaBPager(pageSize: 3);
        pager.PageIndex = 1;
        var rows = CreateTestRows(10);

        // Act
        var result = pager.SelectPage(rows);

        // Assert
        result.Should().HaveCount(3);
        result[0].TableName.Should().Be("Table_3");
        result[1].TableName.Should().Be("Table_4");
        result[2].TableName.Should().Be("Table_5");
    }

    [Fact]
    public void SelectPage_LastPartialPage_ReturnsRemainingItems()
    {
        // Arrange
        var pager = new AreaBPager(pageSize: 3);
        pager.PageIndex = 3;
        var rows = CreateTestRows(10);

        // Act
        var result = pager.SelectPage(rows);

        // Assert
        result.Should().HaveCount(1);
        result[0].TableName.Should().Be("Table_9");
    }

    [Fact]
    public void SelectPage_WithPageIndexBeyondMax_ClampsAndReturnsLastPage()
    {
        // Arrange
        var pager = new AreaBPager(pageSize: 3);
        pager.PageIndex = 999;
        var rows = CreateTestRows(10);

        // Act
        var result = pager.SelectPage(rows);

        // Assert
        // Should clamp to last page (index 3)
        result.Should().HaveCount(1);
        result[0].TableName.Should().Be("Table_9");
    }

    private static List<TableRowState> CreateTestRows(int count)
    {
        var rows = new List<TableRowState>();
        for (int i = 0; i < count; i++)
        {
            rows.Add(new TableRowState(
                TableName: $"Table_{i}",
                InsertOrder: i,
                Status: TableStatus.Planned,
                Phase: "Test",
                Remarks: null));
        }
        return rows;
    }
}
