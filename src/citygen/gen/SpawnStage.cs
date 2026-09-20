using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Where the roster stands at the first tick</b>: a car on a lane, and a person at a door.
/// </summary>
/// <remarks>
/// <para>
/// <b>GEN-7 says a car starts stopped in a parking space and a person starts inside a building.</b> The
/// second half holds now — a body stood at a way in walks through it before the town's first tick
/// (<c>TownWorld.MoveIn</c>) — and the first does not, there being no bay to stand a car in. That half is
/// named in the known gaps rather than reworded to match the code.
/// </para>
/// <para>
/// <b>A car is stood on a lane because otherwise the town does not move at all.</b> Nothing else stands one
/// up: the roster is the plan's spawns, and a stage that placed nothing would leave a perfect road fabric
/// with no traffic on it — which looks right in a picture, passes every static test, and makes every
/// dynamic reading vacuous at once.
/// </para>
/// <para>
/// <b>One car a lane, and that is also the bound.</b> Clear of the other cars by construction rather than
/// by a search, and the count a brief may ask for is how many lanes the town laid that are long enough to
/// stand one on — which is the bound that replaces the bays a car count used to be clamped to.
/// </para>
/// </remarks>
internal static class SpawnStage
{
    const byte Car = 1;

    /// <summary>Which is also the value an unwritten slot holds, so a person is the kind a spawn is by default.</summary>
    const byte Person = 0;

    public static CityPlan.SpawnArrays Lay(
        TownBrief brief, Paving paving, CityPlan.BuildingArrays buildings, SimConfig config, ref Rng draw)
    {
        var lanes = paving.Lanes;

        // A lane a car cannot be stood clear of both its ends on is not one this stage can use: a body
        // standing over a lane's own end is a body in the box beyond it before the town has ticked once.
        var roomM = config.Car.LengthM + (config.Car.WidthM * 2f);
        var standable = new List<int>();
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            // <b>And a lane with no movement off it is not one either</b>: a car stood there is a car with
            // nowhere to go, which is a body standing still with no clock running for it. The town lays one
            // such lane — the way in to a car park's bays, which nothing has yet laid the bays at
            // ([the known gaps](../../../docs/index.md#known-gaps)) — and asking the lane rather than the
            // road it is one of is what makes this a fact about the town and not about car parks.
            if (lanes.LaneLengthM[lane] < roomM) continue;
            if (lanes.ConnectorAt[lane + 1] == lanes.ConnectorAt[lane]) continue;

            standable.Add(lane);
        }

        var cars = Math.Min(brief.Cars, standable.Count);
        var doors = buildings.EntryPointM.Length;
        var people = Math.Min(brief.People, doors);
        var kind = new byte[cars + people];
        var positionM = new Vector2[cars + people];
        var headingRad = new float[cars + people];

        var taken = 0;
        foreach (var lane in Spread(standable.Count, cars, ref draw))
        {
            var on = standable[lane];
            var at = Spline.SampleAt(lanes.ArcsOf(on), lanes.LaneLengthM[on] * 0.5f);

            kind[taken] = Car;
            positionM[taken] = at.PositionM;
            headingRad[taken] = at.HeadingRad;
            taken++;
        }

        // <b>One person a door, spread over the whole town's worth of them</b> (GEN-7). A body stood at a
        // way in is a body inside that building before the first tick, so what the count is bounded by is
        // how many doors the buildings were laid with — and a door already taken would be two people
        // walking out of one doorway onto one another.
        foreach (var slot in Spread(doors, people, ref draw))
        {
            var doorM = buildings.EntryPointM[slot];
            var building = BuildingOfEntry(buildings, slot);

            kind[taken] = Person;
            positionM[taken] = doorM;
            // Facing out of the door it is standing at, which is the way a body that has just come
            // through one is pointing.
            headingRad[taken] = Bearing(doorM - buildings.CentreM[building]);
            taken++;
        }

        return new CityPlan.SpawnArrays { Kind = kind, PositionM = positionM, HeadingRad = headingRad };
    }

    /// <summary>Which building a way in belongs to, walked from the offsets that index them.</summary>
    static int BuildingOfEntry(CityPlan.BuildingArrays buildings, int entry)
    {
        for (var building = 0; building < buildings.Count; building++)
        {
            if (entry < buildings.EntryOffsets[building + 1]) return building;
        }

        return buildings.Count - 1;
    }

    static float Bearing(Vector2 outwardM) =>
        outwardM.LengthSquared() > 1e-6f ? MathF.Atan2(outwardM.Y, outwardM.X) : 0f;

    /// <summary>
    /// Which of the lanes are stood on: every <c>n</c>th one from a drawn start, so the traffic is spread
    /// over the town rather than filling whichever corner of it was laid first.
    /// </summary>
    static IEnumerable<int> Spread(int have, int want, ref Rng draw)
    {
        if (have <= 0 || want <= 0) return [];

        var step = MathF.Max(1f, have / (float)want);
        var from = draw.NextInt(have);
        var taken = new int[want];
        for (var at = 0; at < want; at++) taken[at] = (from + (int)(at * step)) % have;
        return taken;
    }
}
