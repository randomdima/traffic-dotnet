using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The bands of carriageway a town's pedestrian nodes hand a crossing over</b> (TER-6): <b>one wherever a
/// pair of them face each other across a road</b> (WLK-10), from the kerb one of them stands off to the kerb
/// the other does — and none anywhere else.
/// </summary>
/// <remarks>
/// <para>
/// <b>A band is where a walk meets a road, and that is the whole of the placement.</b> Nothing here asks
/// whether the junction behind it forks, how far back an arm's lanes hand over, or how much road is left:
/// the walking side stands a pair of nodes facing each other across a carriageway and the band goes between
/// them. Every figure the old placement turned on — a setback off the arm's end, one crossing for a node
/// that forks nothing, a road too short to carry its two ends' paint — is gone with it.
/// </para>
/// <para>
/// <b>The walk has two answers and this is laid from either of them</b> (<see cref="KerbEnds"/>): the places
/// it crosses, which is what the town is painted with, and the places it is cut at the ends of each street,
/// which is what the traffic is held at (<see cref="StopBars"/>) and what the lane paint stops behind
/// (<see cref="CentrelineRuns"/>). They are the same list but for a road too short to be crossed twice,
/// which is crossed once midway between its two ends (<see cref="Midway"/>) while still being held at both
/// of them.
/// </para>
/// <para>
/// <b>Its span is the two kerbs it runs between</b>, which is the carriageway's width where the kerb is
/// straight and more at a mouth, where the ground a junction's movements are driven over reaches past the
/// arm's own edge. <b>What carries no node carries no band</b>: a bay, a roundabout's circulating
/// carriageway (GEN-19), and a car park's junction.
/// </para>
/// </remarks>
internal sealed class Crossings
{
    /// <summary>No crossing, which is what an arm too short for one carries and what a bay's way carries.</summary>
    public const int None = -1;

    readonly Vector2[] _centreM;
    readonly Vector2[] _axis;
    readonly float[] _depthM;
    readonly float[] _spanM;
    readonly int[] _road;
    readonly int[] _end;
    readonly int[] _junction;
    readonly bool[] _midway;
    readonly int[] _atEnd;

    Crossings(
        Vector2[] centreM, Vector2[] axis, float[] depthM, float[] spanM, int[] road, int[] end,
        int[] junction, bool[] midway, int[] atEnd)
    {
        _midway = midway;
        _centreM = centreM;
        _axis = axis;
        _depthM = depthM;
        _spanM = spanM;
        _road = road;
        _end = end;
        _junction = junction;
        _atEnd = atEnd;
    }

    public int Count => _centreM.Length;

    /// <summary>The middle of the band, which stands half its depth back from the arm's own end.</summary>
    public ReadOnlySpan<Vector2> CentreM => _centreM;

    /// <summary>The way the traffic crossing it is going, which the stripes are laid along.</summary>
    public ReadOnlySpan<Vector2> Axis => _axis;

    /// <summary>How much of the road's length the band covers.</summary>
    public ReadOnlySpan<float> DepthM => _depthM;

    /// <summary>And how far it reaches across, which is the carriageway's own width.</summary>
    public ReadOnlySpan<float> SpanM => _spanM;

    public ReadOnlySpan<int> Road => _road;

    /// <summary>
    /// <b>The arm it is painted on</b> (<see cref="JunctionArms.End"/>): which road, and which of that
    /// road's two ends — what <see cref="At"/> is read the other way round, for a caller holding a crossing
    /// rather than an end.
    /// </summary>
    public ReadOnlySpan<int> End => _end;

    /// <summary>The junction the arm it is painted on stands at, or the one its road was drawn from if it
    /// is painted between two arms rather than on one (<see cref="Midway"/>).</summary>
    public ReadOnlySpan<int> Junction => _junction;

    /// <summary>
    /// <b>Whether it stands between a road's two ends rather than at one of them</b> (WLK-10a): the single
    /// band a road too short to be crossed twice is painted with, which both of its ends answer with. A
    /// reading laid from the stations themselves has none.
    /// </summary>
    public ReadOnlySpan<bool> Midway => _midway;

    /// <summary>
    /// The band at one end of one road, or <see cref="None"/>: the one thing a bar asks, a bar on a lane
    /// being placed by the station at the end it arrives at and by nothing else (TER-6). In a reading laid
    /// from the paint, a road crossed midway answers the same band at both of its ends.
    /// </summary>
    public int At(int road, bool atTo) => _atEnd[JunctionArms.End(road, atTo)];

    /// <summary>
    /// <b>The bands a town's pedestrian nodes ask for</b> (TER-6): one across every place a pair of them
    /// face each other over a road, from the kerb one stands off to the kerb the other does, and none
    /// anywhere else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Where they go is not decided here and is not read off the roads.</b> A band is the ground between
    /// two places a walk arrives at, so the walking side says where one is wanted (WLK-10) and this lays what
    /// it asks for. <b>What comes down is the data and not the type</b>: the road tier stands below the walk
    /// ([slice-map.md](../../../../docs/slice-map.md)), so what it is handed is a pair of points per station.
    /// </para>
    /// <para>
    /// <b>Which of the walk's two answers it is handed is the reader's</b> — <see cref="KerbEnds.CrossedM"/>
    /// for the paint, <see cref="KerbEnds.HeldM"/> for what the traffic is held at. <b>The filing is here
    /// and not at either reader</b>: it is the one place that knows both how a station is named and how an
    /// arm is numbered (<see cref="JunctionArms.End"/>), and a mapping written out twice is two answers about
    /// the same band.
    /// </para>
    /// </remarks>
    public static Crossings Lay(CityPlan plan, SimConfig config, ReadOnlySpan<KerbNodes> stations)
    {
        var asked = new CrossedAtAnEnd[plan.Roads.Count * 2];
        foreach (var nodes in stations)
        {
            // A road crossed midway is asked for at both of its ends, both of them meaning the one band
            // between them: which is what leaves the traffic aware of it whichever way it drives.
            var crossed = new CrossedAtAnEnd(
                true, nodes.NearM, nodes.FarM, Midway: nodes.Cut == KerbCut.Midway,
                Painted: nodes.Painted);
            if (nodes.Cut != KerbCut.AtTheFarEnd) asked[JunctionArms.End(nodes.Road, atTo: false)] = crossed;
            if (nodes.Cut != KerbCut.AtTheNearEnd) asked[JunctionArms.End(nodes.Road, atTo: true)] = crossed;
        }

        return Lay(plan, config, asked);
    }

    public static Crossings Lay(CityPlan plan, SimConfig config, ReadOnlySpan<CrossedAtAnEnd> asked)
    {
        var roads = plan.Roads;
        var depthM = config.Road.CrossingDepthM;

        var centreM = new List<Vector2>();
        var axis = new List<Vector2>();
        var bandM = new List<float>();
        var spanM = new List<float>();
        var road = new List<int>();
        var end = new List<int>();
        var junction = new List<int>();
        var midway = new List<bool>();
        var atEnd = new int[roads.Count * 2];
        Array.Fill(atEnd, None);
        if (depthM <= 0f) return new Crossings([], [], [], [], [], [], [], [], atEnd);

        for (var at = 0; at < atEnd.Length && at < asked.Length; at++)
        {
            var crossed = asked[at];
            if (!crossed.Wanted) continue;

            var run = crossed.FarM - crossed.NearM;
            var reachM = run.Length();
            if (reachM <= 0f) continue;

            var on = JunctionArms.Road(at);
            var atTo = JunctionArms.AtTo(at);

            // <b>One band and not one per end</b>: a road crossed midway asks at both of its ends
            // (<see cref="CrossedAtAnEnd.Midway"/>), and the second of them is the first one again.
            var twin = at ^ 1;
            if (crossed.Midway && atEnd[twin] != None)
            {
                atEnd[at] = atEnd[twin];
                continue;
            }

            atEnd[at] = centreM.Count;
            centreM.Add((crossed.NearM + crossed.FarM) * 0.5f);

            // The stripes run the way the traffic does, which here is square to the walk rather than read
            // off the road's own line: the walk is what the band was placed by.
            axis.Add(Heading.RightOf(run / reachM));

            // <b>A place the walk does not cross carries no band</b> (WLK-10a): it is the end of the road's
            // kerb rather than a station, and what a bar stands a setback clear of there is the place
            // itself. Nought is that said in the one figure every reader already measures off.
            bandM.Add(crossed.Painted ? depthM : 0f);
            spanM.Add(reachM);
            road.Add(on);
            end.Add(at);
            junction.Add(atTo ? roads.ToJunction[on] : roads.FromJunction[on]);
            midway.Add(crossed.Midway);
        }

        return new Crossings(
            [.. centreM], [.. axis], [.. bandM], [.. spanM], [.. road], [.. end], [.. junction], [.. midway],
            atEnd);
    }
}

/// <summary>
/// <b>Where a zebra is wanted across one road end</b> (TER-6, WLK-10): the two places that end's pedestrian
/// nodes hand a crossing over at, one on each kerb, and whether the end asks for one at all.
/// </summary>
/// <remarks>
/// <para>
/// It is the walking side's answer carried down as plain points, the road tier standing below it — so the
/// paint is laid where a walk crosses without <see cref="Crossings"/> knowing anything about a walk.
/// </para>
/// <para>
/// <b><see cref="Midway"/> is the one thing an end says about the other end</b>: a road too short to be
/// crossed twice is crossed once between them (WLK-10a), and the walk asks for that band at both of its ends
/// so that the traffic meets it whichever way it drives. The two asks are one band, laid once.
/// </para>
/// <para>
/// <b><see cref="Painted"/> says whether there is any paint at the place at all.</b> A place asked for as
/// the end of a road's kerb rather than as a station carries none (WLK-10a), which comes out as a band of no
/// depth — so a bar behind it holds clear of the place itself and a lane line stops behind the bar, both of
/// them by the arithmetic they already use.
/// </para>
/// </remarks>
internal readonly record struct CrossedAtAnEnd(
    bool Wanted, Vector2 NearM, Vector2 FarM, bool Midway = false, bool Painted = true);
