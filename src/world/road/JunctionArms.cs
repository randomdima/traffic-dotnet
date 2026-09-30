using TrafficSimulation.CityGen;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>What stands at each of the plan's junctions</b>: the road arms that are not a bay's — up to the two a
/// junction forking nothing carries — and which junctions carry more than two.
/// </summary>
/// <remarks>
/// <para>
/// <b>A bay is not an arm.</b> A car park's bays stand on nodes of their own joined to nothing (GEN-53), and are
/// nowhere a driver going past could turn.
/// </para>
/// <para>
/// <b>One question, and two marks read opposite answers off it</b>: a lane line carries on through a
/// junction that forks nothing (<see cref="CentrelineRuns"/>) and a bar is painted only where one forks
/// (<see cref="StopBars"/>). Asked twice, the two would be entitled to disagree about which nodes are which.
/// </para>
/// </remarks>
internal sealed class JunctionArms
{
    /// <summary>No road end, which is what a junction carries in place of one and what a walk stops on.</summary>
    public const int NoEnd = -1;

    readonly int[] _ends;
    readonly bool[] _forks;
    readonly bool[] _circulates;

    JunctionArms(int[] ends, bool[] forks, bool[] circulates)
    {
        _ends = ends;
        _forks = forks;
        _circulates = circulates;
    }

    /// <summary>One end of one road, as the pair a walk carries: which road, and which of its two ends.</summary>
    public static int End(int road, bool atTo) => (road * 2) + (atTo ? 1 : 0);

    public static int Road(int end) => end / 2;

    /// <summary>Whether an end is the road's far end rather than the one it was drawn from.</summary>
    public static bool AtTo(int end) => end % 2 == 1;

    /// <summary>
    /// Whether more than two road arms stand at a junction, which is the whole of what makes it a place a
    /// driver has a choice to make.
    /// </summary>
    public bool Forks(int junction) => _forks[junction];

    /// <summary>
    /// <b>Whether a road is a roundabout's own ring rather than an arm onto one</b> (GEN-19). A ring is a
    /// carriageway with no frontage on either hand — nothing fronts onto it and nothing crosses it — so it
    /// carries no paint (<see cref="Crossings"/>) and stands no pedestrian node (WLK-2).
    /// </summary>
    public bool Circulates(int road) => _circulates[road];

    /// <summary>
    /// The other road end at a junction that forks nothing, or <see cref="NoEnd"/> where the junction forks
    /// or this end is the only one standing there.
    /// </summary>
    public int Across(int junction, int end)
    {
        if (junction < 0 || _forks[junction]) return NoEnd;

        var one = _ends[junction * 2];
        var other = _ends[(junction * 2) + 1];

        return one == end ? other : other == end ? one : NoEnd;
    }

    public static JunctionArms Of(CityPlan plan)
    {
        var roads = plan.Roads;
        var junctions = plan.Junctions.Count;
        var ends = new int[junctions * 2];
        Array.Fill(ends, NoEnd);
        var forks = new bool[junctions];
        var circulates = new bool[roads.Count];
        foreach (var ring in plan.Roundabouts.Road) circulates[ring] = true;

        for (var road = 0; road < roads.Count; road++)
        {
            if (roads.IsABay(road)) continue;

            Take(roads.FromJunction[road], End(road, atTo: false));
            Take(roads.ToJunction[road], End(road, atTo: true));
        }

        return new JunctionArms(ends, forks, circulates);

        void Take(int junction, int end)
        {
            if (junction < 0) return;

            if (ends[junction * 2] == NoEnd) ends[junction * 2] = end;
            else if (ends[(junction * 2) + 1] == NoEnd) ends[(junction * 2) + 1] = end;
            else forks[junction] = true;
        }
    }
}
