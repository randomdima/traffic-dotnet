using System.Numerics;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>The one grid's contract</b> (SIM-8): a point's cell on a coarser level is its cell on a finer one
/// shifted, a level is chosen by the rule its caller asked for, and a window holds whatever stands off it in
/// its rim.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class WorldGridTests
{
    /// <summary>The shipped main cell, and one no power of two is — the relation may not lean on either.</summary>
    public static TheoryData<float> MainCells => [8f, 7.6f];

    /// <summary>
    /// <b>No rounding puts a point in a fine cell outside the coarse cell holding it</b> — asked where it would
    /// show: on the lines of every level, a hair either side of them, below the origin and far across a town.
    /// The subnormals a hair either side of nought are the stated exception and are not asked.
    /// </summary>
    [Theory]
    [MemberData(nameof(MainCells))]
    public void APointsCoarseCellIsItsFineCellShifted(float mainCellM)
    {
        var grid = new WorldGrid(mainCellM);
        var finest = grid.Level(8);
        var rng = new Random(7);

        var places = new List<float>();
        for (var line = -40; line <= 40; line++)
        {
            var onM = finest.EdgeM(line * 37);
            places.AddRange([onM, MathF.BitDecrement(onM), MathF.BitIncrement(onM)]);
        }

        for (var at = 0; at < 2000; at++) places.Add((float)((rng.NextDouble() * 6000.0) - 1000.0));

        foreach (var atM in places)
        {
            if (float.IsSubnormal(atM)) continue;

            for (var depth = 0; depth <= finest.Depth; depth++)
            {
                var level = grid.Level(depth);
                Assert.Equal(level.CellOf(atM), finest.Up(finest.CellOf(atM), level));
            }
        }
    }

    [Theory]
    [InlineData(0.1f, 0.125f)]
    [InlineData(5f, 8f)]
    [InlineData(15.2f, 8f)]
    [InlineData(4f, 4f)]
    public void CoveringIsTheFinestLevelAtLeastTheReach(float reachM, float cellM) =>
        Assert.Equal(cellM, new WorldGrid(8f).Covering(reachM).CellM);

    [Theory]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1.5f, 1f)]
    [InlineData(40f, 8f)]
    public void WithinIsTheCoarsestLevelNoWiderThanTheCell(float cellM, float levelM) =>
        Assert.Equal(levelM, new WorldGrid(8f).Within(cellM).CellM);

    [Fact]
    public void AMainCellIsSplitByHalvingAlone() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldGrid(8f).Split(3));

    /// <summary>
    /// <b>A place off the window is held in its rim</b>, so an index that filed a body off the edge finds it
    /// again from a box off the edge; and a box that misses a window that holds everything it filed reads
    /// nothing of it.
    /// </summary>
    [Fact]
    public void WhatStandsOffAWindowIsHeldInItsRim()
    {
        var level = new WorldGrid(8f).Main;
        var window = GridWindow.Over(level, new Vector2(16f, 16f), new Vector2(47f, 31f));
        var far = new Vector2(500f, -300f);

        Assert.Equal(window.IndexOf(window.ToX, window.FromY), window.IndexAt(far));
        Assert.True(window.TryRange(far, far + Vector2.One, out var rim));
        Assert.Equal(new CellRange(window.ToX, window.FromY, window.ToX, window.FromY), rim);
        Assert.False(window.TryOverlap(far, far + Vector2.One, out _));
    }
}
