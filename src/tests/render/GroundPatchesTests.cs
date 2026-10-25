using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// The ground's patches as its stages read them (<see cref="GroundPatches.From"/>): the vertex stage picks a surface's
/// row by the number its corner files the surface under, so the rows stand in <see cref="Surface"/>'s order.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class GroundPatchesTests
{
    [Theory]
    [InlineData((uint)Surface.Grass, 0.01f, 0.11f)]
    [InlineData((uint)Surface.Tarmac, 0.02f, 0.12f)]
    [InlineData((uint)Surface.Pavement, 0.03f, 0.13f)]
    [InlineData((uint)Surface.Deck, 0.04f, 0.14f)]
    [InlineData((uint)Surface.Water, 0.05f, 0.15f)]
    [InlineData((uint)GroundPatches.ShoreRow, 0.06f, 0.16f)]
    public void ASurfacesRowIsItsOwnFigures(uint surface, float light, float warm)
    {
        var view = new ViewFigures
        {
            GroundPatchLightShore = 0.06f, GroundPatchWarmShore = 0.16f,
            GroundPatchLightGrass = 0.01f, GroundPatchWarmGrass = 0.11f,
            GroundPatchLightTarmac = 0.02f, GroundPatchWarmTarmac = 0.12f,
            GroundPatchLightPavement = 0.03f, GroundPatchWarmPavement = 0.13f,
            GroundPatchLightDeck = 0.04f, GroundPatchWarmDeck = 0.14f,
            GroundPatchLightWater = 0.05f, GroundPatchWarmWater = 0.15f,
        };
        var patches = GroundPatches.From(view);

        // The block as laid: the field, then a row a surface — what `patchLook[filed]` indexes.
        var rows = MemoryMarshal.Cast<GroundPatches, Vector4>(MemoryMarshal.CreateReadOnlySpan(ref patches, 1));

        var row = rows[1 + (int)surface];
        Assert.Equal(new Vector2(light, warm), new Vector2(row.X, row.Y));
    }

    /// <summary>Every surface's patches are as wide as its own figure, so no two surfaces wear one field.</summary>
    [Fact]
    public void ASurfacesPatchesAreItsOwnWidth()
    {
        var patches = GroundPatches.From(new ViewFigures
        {
            GroundPatchAcrossGrassM = 1f, GroundPatchAcrossTarmacM = 2f, GroundPatchAcrossPavementM = 4f,
            GroundPatchAcrossDeckM = 5f, GroundPatchAcrossWaterM = 8f, GroundPatchAcrossShoreM = 10f,
        });

        Assert.Equal(new Vector4(1f, 0.5f, 0.25f, 0.2f), patches.PerAcross);
        Assert.Equal(new Vector2(0.125f, 0.1f), new Vector2(patches.PerAcrossWet.X, patches.PerAcrossWet.Y));
    }

    /// <summary>The water's looks drift the way the wind prevails.</summary>
    [Fact]
    public void TheWaterDriftsDownThePrevailingWind()
    {
        var config = new SimConfig();
        var wind = new Wind(config.Wind, seed: 7);
        var drift = GroundPatches.DriftAt(wind, config, tick: 10 * config.Sim.TickRateHz);

        var (sin, cos) = MathF.SinCos(wind.PrevailingRad);
        var downwind = new Vector2(cos, sin);
        var still = new Vector2(drift.X, drift.Y);
        var lively = new Vector2(drift.Z, drift.W);
        Assert.Equal(1f, Vector2.Dot(Vector2.Normalize(still), downwind), 1e-4f);
        Assert.Equal(1f, Vector2.Dot(Vector2.Normalize(lively), downwind), 1e-4f);
    }

    /// <summary>The longest train's crests run the way the wind prevails.</summary>
    [Fact]
    public void TheLongestWavesRunDownThePrevailingWind()
    {
        var config = new SimConfig();
        var wind = new Wind(config.Wind, seed: 7);
        var before = WaterWaves.At(wind, config, tick: 0);
        var after = WaterWaves.At(wind, config, tick: config.Sim.TickRateHz / 10);

        var numberPerM = new Vector2(after.NumberXPerM.X, after.NumberYPerM.X);
        var crestMovedM = numberPerM * ((after.PhaseRad.X - before.PhaseRad.X) / numberPerM.LengthSquared());
        var (sin, cos) = MathF.SinCos(wind.PrevailingRad);
        Assert.Equal(1f, Vector2.Dot(Vector2.Normalize(crestMovedM), new Vector2(cos, sin)), 1e-4f);
    }

    /// <summary>Half the ground in each look puts the hand-over's middle on the field's mean.</summary>
    [Fact]
    public void AnEvenShareHandsOverAtTheMean()
    {
        var look = GroundPatches.From(new ViewFigures { GroundLookShareGrass = 0.5f }).Grass;

        Assert.Equal(0f, (look.Z + look.W) / 2f, 1e-5f);
    }
}
