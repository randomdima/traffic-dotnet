using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// The skidpad, held to what it was laid for: a car of every look in every row, a square each, and
/// nothing on the map but road to drive it on.
/// </summary>
/// <remarks>
/// <b>What the pad measures is <see cref="Tests.Agents.Car.SkidpadFiguresTests"/>'s.</b> This is about the
/// map: the grid a reader navigates by — a column is a look and a row is a pedal — and the ground under it.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class SkidpadPlanTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    const byte SpawnKindCar = 1;

    /// <summary>
    /// <b>The pad on disk is the pad this build lays.</b> Byte for byte, as the proving grounds are: the
    /// file is what every reader gets, and the squares are sized against the cars that stand in them.
    /// </summary>
    /// <remarks>
    /// A red here is not a bug in the pad — it is <c>--lay-maps</c> not having been run since a figure the
    /// pad is laid against moved.
    /// </remarks>
    [Fact]
    public void ThePadOnDiskIsThePadThisBuildLays()
    {
        var onDisk = File.ReadAllBytes(ProjectPaths.TownFile(SkidpadPlan.Name));

        Assert.Equal(onDisk, TownWriter.Write(SkidpadPlan.Lay(Config)));
    }

    /// <summary>
    /// <b>A column is a look and a row is a pedal.</b> A car takes its variant off its own place in the
    /// spawns, so the whole readability of the map rests on the order they are laid in — and the count is a
    /// plan's, which may not read the fleet's own file. This is where the two are held to each other.
    /// </summary>
    [Fact]
    public void EveryLookStandsInEveryRow()
    {
        Assert.Equal(SkidpadPlan.Looks, CarCatalog.Shared.Count);

        var plan = SkidpadPlan.Lay(Config);
        Assert.Equal(SkidpadPlan.Cars, plan.Spawns.Count);

        for (var car = 0; car < plan.Spawns.Count; car++)
        {
            Assert.Equal(SpawnKindCar, plan.Spawns.Kind[car]);
            Assert.Equal(car % SkidpadPlan.Looks, SkidpadPlan.LookOf(car));
            Assert.Equal(car / SkidpadPlan.Looks, SkidpadPlan.RunOf(car));
        }
    }

    /// <summary>
    /// <b>Every car has its own hundred metres and nothing else is in it.</b> The whole pad is one
    /// comparison a square at a time, and two cars that can reach each other are two cars measuring each
    /// other.
    /// </summary>
    [Fact]
    public void NothingStandsWithinASquareOfAnythingElse()
    {
        var plan = SkidpadPlan.Lay(Config);

        for (var car = 0; car < plan.Spawns.Count; car++)
        {
            for (var other = car + 1; other < plan.Spawns.Count; other++)
            {
                var apartM = (plan.Spawns.PositionM[car] - plan.Spawns.PositionM[other]).Length();
                Assert.True(
                    apartM >= SkidpadPlan.PitchM - 1e-3f,
                    $"cars {car} and {other} stand {apartM:F1} m apart, of {SkidpadPlan.PitchM:F0} m each");
            }
        }
    }

    /// <summary>
    /// <b>The map is road, edge to edge.</b> Every square is the same tarmac at the same grip, or a row
    /// would be measuring the ground under it rather than the pedal it was given — and a car that ran onto
    /// grass would be measuring the verge.
    /// </summary>
    [Fact]
    public void EveryCellOfItIsRoad()
    {
        var plan = SkidpadPlan.Lay(Config);
        var ground = new TerrainGrid(plan, Config);

        for (var car = 0; car < plan.Spawns.Count; car++)
        {
            var centreM = plan.Spawns.PositionM[car];
            foreach (var cornerM in Corners(SkidpadPlan.PitchM * 0.5f))
            {
                var atM = centreM + cornerM;
                Assert.True(ground.At(atM).Drivable, $"the pad is not road at {atM.X:F0},{atM.Y:F0}");
            }
        }
    }

    /// <summary>The four corners of a square, pulled in far enough to stand inside the map's own edge.</summary>
    static Vector2[] Corners(float halfM)
    {
        var insideM = halfM - 1f;
        return
        [
            new(-insideM, -insideM), new(insideM, -insideM), new(-insideM, insideM), new(insideM, insideM),
        ];
    }
}
