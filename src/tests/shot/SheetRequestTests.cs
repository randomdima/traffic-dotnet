using System.Numerics;
using TrafficSimulation.App.Shot;
using Xunit;

namespace TrafficSimulation.Tests.Shot;

/// <summary>
/// SHT-4: a sheet is asked for as a document, what the cells have in common is stated once, and a
/// member the schema does not carry is refused rather than ignored.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class SheetRequestTests
{
    const string Junctions = """
        {
          "title": "junction kinds",
          "out": ".tmp/junctions.png",
          "map": "Test",
          "size": [640, 480],
          "view": 45,
          "seconds": 20,
          "note": "the paint must stop at the give-way line",
          "cells": [
            { "label": "crossroad", "at": [120, 90] },
            { "label": "zebra", "at": [200, 160], "view": 30, "seconds": 0, "ui": ["nodes"],
              "note": "the stripes must be square to the kerb" }
          ]
        }
        """;

    [Fact]
    public void ACellInheritsTheSheetsFiguresAndOverridesWhatItDiffersIn()
    {
        var sheet = SheetRequest.Parse(Junctions, "the sheet");

        var first = sheet.ForCell(0, "cell-1.png");
        Assert.Equal("Test", first.Map);
        Assert.Equal(45f, first.ViewM);
        Assert.Equal(20, first.Seconds);
        Assert.Equal(new Vector2(120, 90), first.AtM);
        Assert.Null(first.Ui);

        var second = sheet.ForCell(1, "cell-2.png");
        Assert.Equal("Test", second.Map);
        Assert.Equal(30f, second.ViewM);
        Assert.Equal(0, second.Seconds);
        Assert.Equal(["nodes"], second.Ui);
    }

    /// <summary>The cells are the sheet's size, since a sheet of cells at two sizes cannot be tiled.</summary>
    [Fact]
    public void EveryCellIsTheSheetsOwnSize()
    {
        var sheet = SheetRequest.Parse(Junctions, "the sheet");

        Assert.Equal((640, 480), sheet.SizePx);
        Assert.Equal(640, sheet.ForCell(1, "cell-2.png").WidthPx);
        Assert.Equal(480, sheet.ForCell(1, "cell-2.png").HeightPx);
    }

    /// <summary>A cell says what it is of; the sheet's note stands where the cell has none.</summary>
    [Fact]
    public void ACellIsLabelledAndCarriesWhicheverNoteIsNearest()
    {
        var sheet = SheetRequest.Parse(Junctions, "the sheet");

        Assert.Equal("crossroad", sheet.LabelOf(0));
        Assert.Equal("the paint must stop at the give-way line", sheet.NoteOf(0));
        Assert.Equal("the stripes must be square to the kerb", sheet.NoteOf(1));
    }

    /// <summary>A sheet of one cell is the document's own defaults, titled rather than numbered.</summary>
    [Fact]
    public void ASheetWithNoCellsIsOneCellOfItsOwnFigures()
    {
        var sheet = SheetRequest.Parse("""{"title": "the whole town", "map": "Test", "view": 300}""", "the sheet");

        Assert.Equal(1, sheet.CellCount);
        Assert.Equal("the whole town", sheet.LabelOf(0));
        Assert.Equal(300f, sheet.ForCell(0, "one.png").ViewM);
    }

    /// <summary>
    /// A misspelt member is the failure this format exists to make impossible: it would otherwise
    /// photograph tick zero and say nothing about why.
    /// </summary>
    [Fact]
    public void AMemberTheSchemaDoesNotCarryIsRefused()
    {
        var complaint = Assert.Throws<ArgumentException>(
            () => SheetRequest.Parse("""{"map": "Test", "secondes": 20}""", "the sheet"));

        Assert.Contains("the sheet", complaint.Message);
    }

    [Theory]
    [InlineData("""{"map": "Test", "size": [640]}""")]
    [InlineData("""{"map": "Test", "size": [0, 480]}""")]
    [InlineData("""{"map": "Test", "at": [120]}""")]
    [InlineData("""{"map": "Test", "rule": [[1, 2, 3]]}""")]
    public void AFigureOfTheWrongShapeIsRefused(string document) =>
        Assert.Throws<ArgumentException>(() => SheetRequest.Parse(document, "the sheet"));

    /// <summary>A sheet past the grid's own bound is two sheets, and is said so rather than tiled unreadably.</summary>
    [Fact]
    public void ASheetOfMoreCellsThanTheGridHoldsIsRefused()
    {
        var cells = string.Join(',', Enumerable.Repeat("""{"at": [10, 10]}""", SheetRequest.MostCells + 1));

        var complaint = Assert.Throws<ArgumentException>(
            () => SheetRequest.Parse($$"""{"map": "Test", "cells": [{{cells}}]}""", "the sheet"));

        Assert.Contains($"{SheetRequest.MostCells}", complaint.Message);
    }

    /// <summary>A cell that names no map anywhere is refused when it is resolved, not photographed blank.</summary>
    [Fact]
    public void ACellWithNoMapAnywhereIsRefused()
    {
        var sheet = SheetRequest.Parse("""{"cells": [{"at": [10, 10]}]}""", "the sheet");

        Assert.Throws<ArgumentException>(() => sheet.ForCell(0, "cell-1.png"));
    }

    /// <summary>Two points to a measurement, fed the way the ruler is clicked.</summary>
    [Fact]
    public void ATapeIsReadAsPairsOfPoints()
    {
        var sheet = SheetRequest.Parse("""{"map": "Test", "rule": [[10, 20, 30, 40]]}""", "the sheet");

        Assert.Equal(
            [new Vector2(10, 20), new Vector2(30, 40)],
            sheet.ForCell(0, "one.png").RulerPointsM);
    }

    /// <summary>The caption is on unless the document turns it off: a picture nobody can date is the
    /// state this whole path exists to leave behind.</summary>
    [Fact]
    public void TheCaptionIsOnUnlessTheDocumentSaysOtherwise()
    {
        Assert.True(SheetRequest.Parse("""{"map": "Test"}""", "the sheet").Caption);
        Assert.False(SheetRequest.Parse("""{"map": "Test", "caption": false}""", "the sheet").Caption);
    }
}
