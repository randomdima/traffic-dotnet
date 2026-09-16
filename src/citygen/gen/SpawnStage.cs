using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Where the roster stands at the first tick</b>: a car on a lane, and nobody anywhere.
/// </summary>
/// <remarks>
/// <para>
/// <b>GEN-7 says a car starts stopped in a parking space and a person starts inside a building, and both
/// halves are false of a town this build lays.</b> There is no bay to stand a car in and no door to stand
/// anybody at, and the rule is named in the known gaps rather than reworded to match the code — the code is
/// what is temporarily wrong here.
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

    public static CityPlan.SpawnArrays Lay(TownBrief brief, Paving paving, SimConfig config, ref Rng draw)
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
        var kind = new byte[cars];
        var positionM = new Vector2[cars];
        var headingRad = new float[cars];

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

        return new CityPlan.SpawnArrays { Kind = kind, PositionM = positionM, HeadingRad = headingRad };
    }

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
