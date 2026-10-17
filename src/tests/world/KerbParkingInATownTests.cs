using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>Parking at the kerb</b> (GEN-4n): a traced town's own cars stand along its roadsides, each in a place opened where
/// it stands and held by its body's ordinary claim on the roadside — on a block of four streets, each with a roadside
/// at both kerbs, and nobody living in it.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P5)]
public class KerbParkingInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary><b>Every car the map stands is stood at a kerb</b>, in a kerb place of the register (GEN-7).</summary>
    [Fact]
    public void EveryCarTheMapStandsIsAtAKerb()
    {
        using var world = new TownWorld(TownPlan.Lay(Block(), Config, BuildingSizes.None), Config);

        Assert.True(world.Cars.Count > 0);
        for (var car = 0; car < world.Cars.Count; car++)
        {
            var place = world.Parking.BayOf(car);
            Assert.True(place >= 0 && world.Parking.IsKerb(place), $"car {car} stands in place {place}, no kerb's");
        }
    }

    /// <summary>
    /// <b>A car at the kerb holds the roadside it stands on by its body</b>, as any body holds the way under it
    /// (TER-4c.2): no slot is held for it, only the ground its wheels are on.
    /// </summary>
    [Fact]
    public void ACarAtTheKerbHoldsTheRoadsideByItsBody()
    {
        using var world = new TownWorld(TownPlan.Lay(Block(), Config, BuildingSizes.None), Config);
        new SimLoop<TownWorld>(world, Config).Advance(1);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            var roadside = world.Parking.KerbLaneOf(world.Parking.BayOf(car));
            Assert.True(world.Occupancy.HasTheBodyOf(world.Ways.OfRoadLane(roadside), car, LaneRoster.Driving), $"car {car} holds none of its roadside");
        }
    }

    /// <summary>
    /// <b>Two cars at one kerb stand a gap apart</b> (<see cref="RoadFigures.KerbParkingGapM"/>): no place is opened over
    /// another, nor within a gap of it.
    /// </summary>
    [Fact]
    public void TwoCarsAtOneKerbStandAGapApart()
    {
        using var world = new TownWorld(TownPlan.Lay(Block(), Config, BuildingSizes.None), Config);

        for (var one = 0; one < world.Cars.Count; one++)
        {
            for (var other = one + 1; other < world.Cars.Count; other++)
            {
                var (a, b) = (world.Parking.BayOf(one), world.Parking.BayOf(other));
                if (world.Parking.KerbLaneOf(a) != world.Parking.KerbLaneOf(b)) continue;

                var apartM = MathF.Max(world.Parking.KerbFromM(b) - world.Parking.KerbToM(a), world.Parking.KerbFromM(a) - world.Parking.KerbToM(b));
                Assert.True(apartM >= Config.Road.KerbParkingGapM - LineTolerance.RoundingM, $"cars {one} and {other} stand {apartM:F2} m apart at one kerb");
            }
        }
    }

    /// <summary>The side of the block, in metres, and where its corner stands.</summary>
    const float SideM = 300f, CornerM = 100f;

    /// <summary>
    /// A square block of four two-way streets, each a lane each way and a roadside at both kerbs, on a map that stands
    /// twenty cars and nobody in them.
    /// </summary>
    static TownMap Block()
    {
        var sizeM = new Vector2(500, 500);
        var zones = new TownMap.ZoneArrays.Builder();
        zones.Add(-1, ZoneKind.Town, [(ZoneParam.Cars, 20f)], [TownMap.WholeOutline(sizeM)]);
        var widthM = (2f * Config.CityGen.TracedLaneWidthM) + (2f * Config.CityGen.TracedRoadsideWidthM);
        return TownMap.Bare("Block", "a block with roadsides", 12888405, sizeM, ZoneKind.Town, []) with
        {
            PointM = [new(CornerM, CornerM), new(CornerM + SideM, CornerM), new(CornerM + SideM, CornerM + SideM), new(CornerM, CornerM + SideM)],
            Roads = [Street(1, 0, 1), Street(2, 1, 2), Street(3, 2, 3), Street(4, 3, 0)],
            Zones = zones.Arrays(),
        };

        TracedRoad Street(long id, int from, int to) => new()
        {
            OsmId = id, Highway = "residential", LanesForward = 1, LanesBackward = 1, LanesShared = 0, LanesTagged = true,
            WidthM = widthM, Mended = true, RoadsideAlong = true, RoadsideAgainst = true, Points = [from, to],
        };
    }
}
