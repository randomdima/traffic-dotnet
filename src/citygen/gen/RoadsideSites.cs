using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>The places along a road that already stands where something could be laid off it</b> (GEN-52, GEN-53):
/// each inside one arc of the road's own line, so what is laid there reads its bearing off that arc rather than
/// off a curve fitted through it.
/// </summary>
internal static class RoadsideSites
{
    /// <summary>One place something could be laid off a road: which road, how far along its line, and where that is.</summary>
    internal readonly record struct Site(int Road, float AlongM, Vector2 AtM);

    /// <summary>
    /// <b>The places something could be laid off one road</b>, one per arc of it that can carry one: the
    /// middle of the stretch of that arc which is far enough from both of the road's own junctions
    /// (<c>CityGenFigures.LocalityM</c>, GEN-16), <paramref name="standoffM"/> clear of both ends of the arc,
    /// and bending no tighter than <paramref name="curvatureMax"/> — on no bridge, ring, car park or intercity road
    /// (GEN-53, GEN-68).
    /// </summary>
    /// <param name="standoffM">
    /// How far back of the place the road is held clear. <b>The caller's and not the town's</b>: a car park
    /// whose bays are spread along the street stands off the whole of them (GEN-53).
    /// </param>
    /// <remarks>
    /// <b>A road that runs one way is offered like any other</b> (GEN-18, TER-4d): what the street's one
    /// direction costs is the bays' own — each is reached the one way the street runs, which is every way it
    /// has (GEN-53).
    /// </remarks>
    public static void SitesOn(
        TownLayout layout, SimConfig config, int road, float standoffM, float curvatureMax, List<Site> into)
    {
        var edge = layout.Edges[road];
        if (edge.Class is RoadClass.Bridge or RoadClass.Roundabout or RoadClass.CarPark or RoadClass.Intercity or RoadClass.Slip || edge.Over) return;

        var line = layout.LineOf(road);
        if (line.Length == 0) return;

        var leadM = config.CityGen.ConnectionStandoffM;
        var localityM = config.CityGen.LocalityM;
        var lengthM = Spline.TotalLengthM(line);

        // A node's distance from the junction behind it is the lead that junction's own arm stands off plus
        // the line between them, so what the locality costs the line is a locality less one lead at each end.
        var fromM = MathF.Max(standoffM, localityM - leadM);
        var toM = lengthM - fromM;

        var arcM = 0f;
        foreach (var arc in line)
        {
            var startM = arcM;
            arcM += arc.LengthM;
            if (MathF.Abs(arc.Curvature) > curvatureMax) continue;

            var earliestM = MathF.Max(fromM, startM + standoffM);
            var latestM = MathF.Min(toM, arcM - standoffM);
            if (latestM <= earliestM) continue;

            var alongM = (earliestM + latestM) * 0.5f;
            into.Add(new Site(road, alongM, Spline.SampleAt(line, alongM).PositionM));
        }
    }

    /// <summary>
    /// <b>Every place in the town something could be laid off a road</b>, over every road there is and with
    /// the ones standing inside a locality of a junction already there left out (GEN-16).
    /// </summary>
    /// <inheritdoc cref="SitesOn" path="/param[@name='standoffM']"/>
    public static List<Site> Sites(TownLayout layout, SimConfig config, float standoffM, float curvatureMax)
    {
        // <b>A road at a time, on as many threads as there are, and strung in road order.</b> Nothing here
        // writes to the layout, and the locality test below is asked for every place on every road — of the
        // nodes near each place (TownLayout.StandsClear). Keeping each road's answer in its own slot is what
        // makes the order the loop's rather than the threads'.
        var onARoad = new List<Site>?[layout.Edges.Count];
        InChunks.Over(
            layout.Edges.Count,
            () => new List<Site>(),
            (room, road) =>
            {
                room.Clear();
                SitesOn(layout, config, road, standoffM, curvatureMax, room);

                List<Site>? kept = null;
                foreach (var site in room)
                {
                    if (layout.StandsClear(site.AtM)) (kept ??= []).Add(site);
                }

                onARoad[road] = kept;
            });

        var sites = new List<Site>();
        foreach (var kept in onARoad)
        {
            if (kept is not null) sites.AddRange(kept);
        }

        return sites;
    }
}
