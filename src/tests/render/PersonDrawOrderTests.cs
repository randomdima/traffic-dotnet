using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// PER-31: <b>a walker is drawn over every walker whose feet are further up the picture than its own</b>
/// (<see cref="PersonSprites.Depth"/>) — whatever order the frame listed them in.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class PersonDrawOrderTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Less than a walker's height, so the two pictures overlap.</summary>
    const float ApartM = 0.5f;

    /// <summary>The <c>y</c> of each instance written, in the order written, for walkers listed in <paramref name="listed"/>'s order.</summary>
    static float[] DrawnY(ReadOnlySpan<Vector2> standAtM, ReadOnlySpan<int> listed)
    {
        var catalog = PersonCatalog.Shared;
        var people = new PersonFleet(standAtM.Length);
        foreach (var atM in standAtM) people.Add(atM, 0f, Config.Person.MassKg, Config.PersonRadiusM, 0, new Rng(1, 1), reckless: false);

        var aspects = Enumerable.Repeat(1f, catalog.SheetCount).ToArray();
        var into = new SpriteInstance[standAtM.Length];
        var written = PersonSprites.Fill(
            people, listed, catalog, aspects, Config.Person.DrawnStandingBelowMps, Vector2.Zero, new Vector2(10f), into,
            new PersonSprites.Depth[standAtM.Length]);
        return [.. into.AsSpan(0, written).ToArray().Select(static instance => instance.CentreM.Y)];
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void TheWalkerWhoseFeetAreFurtherDownIsDrawnLast(int first, int second) =>
        Assert.Equal([0f, ApartM], DrawnY([new(0f, 0f), new(0.1f, ApartM)], [first, second]));
}
