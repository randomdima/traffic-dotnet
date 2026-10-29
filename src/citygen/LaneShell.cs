using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The outside of the ground the town is driven over</b>: every lane, every movement through a box and every
/// bay taken as the band of ground it covers, and all of those merged into one shape (<see cref="BandShell"/>,
/// GEN-4b, TER-5) — <b>one a level</b> (TER-7b).
/// </summary>
/// <remarks>
/// <para>
/// <b>It is which lines the town hands over and nothing else.</b> The merge is a fact about bands and knows
/// no more about a lane than about any other line with a width (<see cref="BandShell"/>); what is the town's
/// is that the lines it covers are the driven ones — <see cref="Paving.DrivenCount"/>, at
/// <see cref="Paving.DrivenWidthM"/> — and that the index they are weighed against is the town's own
/// (<see cref="Paving.DrivenLines"/>) rather than a second copy laid for this.
/// </para>
/// <para>
/// <b>A lane is an area and not a line</b>, which is what makes this the boundary it is: the perimeter of a
/// plain road is the outer edge of its outer lanes — its roadside, where it has one (<see cref="LaneLines.IsRoadside"/>)
/// — and at a junction it is whatever piece of whichever band reaches past the rest. A junction has no shape of its
/// own to merge (TER-5). <b>The one band that is no lane is a taper</b> (<see cref="LaneLines.Tapers"/>): where a road
/// loses its roadside, the outline would turn round the strip's square end, and a rounding cannot take a corner that
/// sticks out off without cutting the lane it is the corner of — so the ground eased in over is handed over as a band.
/// So is the ground a lane lost or gained at a node is eased over, which no movement crosses (TER-5j): without it the
/// box keeps a kerbed hole where the lane stops.
/// </para>
/// <para>
/// <b>Two lanes laid over one line are one band</b> (<see cref="LaneLines.LaneOverOneLine"/>): a lane and its
/// reverse there cover the same ground, so only the one running with its road is handed over and the other is
/// handed over empty, keeping the numbering the index is in. Handed over twice, the two bands' ends lie on one
/// another facing opposite ways, and where nothing else covers them the merge keeps half of the one end and neither
/// half of the other, and the boundary does not close.
/// </para>
/// <para>
/// <b>A line on another level is handed over empty</b>, for the same reason: a bridge over a road shares no
/// ground with it (PHY-1a), so the two are two shapes and never one. <b>The ground runs on under a bridge past
/// each bridgehead</b> (<see cref="SimConfig.UnderTheDeckM"/>) — the first metres of every lane above handed to
/// the ground's merge as well — so the end the ground's boundary turns round stands under the deck drawn over it
/// and the road meets the bridge straight. <b>And on past the map's edge where a road runs off it</b>
/// (<see cref="OffTheMap"/>), so the end it turns round is off the map and what the map keeps of the boundary is
/// cut by the edge rather than turned on it (GEN-2b).
/// </para>
/// </remarks>
/// <summary>Which of the driven ground's bands one shell is merged from (GEN-75).</summary>
internal enum ShellOf : byte
{
    /// <summary>All of them: the town's boundary, kerb to kerb.</summary>
    Driven,

    /// <summary>Those a walk is struck off (<see cref="Paving.Walked"/>).</summary>
    Walked,

    /// <summary>
    /// The same, run on further where a walk ends at a crossing: those the walk's lanes are struck off
    /// (<see cref="Paving.WalkedCourses"/>).
    /// </summary>
    WalkedCourses,

    /// <summary>Those of the roads with no walk, and the movements between them (<see cref="Paving.Unwalked"/>).</summary>
    Unwalked,
}

internal static class LaneShell
{
    /// <summary>
    /// <b>The town's driven ground on one level merged into one shape.</b> Laid on the first ask and not
    /// before (<see cref="Paving.Perimeter"/>, <see cref="Paving.Above"/>): nothing the town needs to be laid
    /// reads it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No ring narrower than a kerb on the whole is kept</b> (<see cref="BandShell.WithoutRingsNarrowerThan"/>): turns
    /// drawn tight at their corners leave hairs of ground between three of them that no band covers, which a kerb could
    /// not be laid round and the town drives over as it would the tarmac either side.
    /// </para>
    /// <para>
    /// <b>Nor any hole on the ground standing wholly inside one junction's disc, or that no road's kerb runs along</b>
    /// (<see cref="BandShell.WithoutHoles"/>, TER-3c.7): the movements through a box leave patches between them that no
    /// band covers, and kerbed they are chunks of concrete scattered over the junction that nobody laid. The disc takes
    /// those that touch a lane's end; the kerb those of a box wider than its disc. A block, a median and an island
    /// between roads reach past the disc and have a lane running along some stretch of their edge, and are kept.
    /// </para>
    /// </remarks>
    /// <param name="of">Which of the driven ground's bands are merged (GEN-75).</param>
    public static BandShell Of(Paving paving, SimConfig config, byte level, ShellOf of = ShellOf.Driven)
    {
        var (lines, widthM) = Bands(paving, config, level, of);
        var shell = BandShell.Of(lines, widthM, lines.Length == paving.DrivenCount ? paving.DrivenLines(config) : Indexed(lines, config))
            .WithoutRingsNarrowerThan(config.Road.KerbWidthM);
        if (level != CityPlan.RoadArrays.Ground) return shell;

        var junctions = paving.Of.Junctions;
        var widestM = junctions.RadiusM.Length == 0 ? 0f : junctions.RadiusM.Max();
        var under = new KerbEnds.Under(paving, config);
        return shell.WithoutHoles(ring => InsideABox(junctions, widestM, ring) || !AlongAKerb(under, ring));
    }

    /// <summary>Whether a ring stands wholly inside one junction's disc (<see cref="CityPlan.JunctionArrays.RadiusM"/>).</summary>
    static bool InsideABox(CityPlan.JunctionArrays junctions, float widestM, ArcSeg[] ring)
    {
        var (leastM, mostM) = (new Vector2(float.MaxValue), new Vector2(float.MinValue));
        foreach (var arc in ring)
        {
            leastM = Vector2.Min(leastM, Vector2.Min(arc.StartM, arc.EndM));
            mostM = Vector2.Max(mostM, Vector2.Max(arc.StartM, arc.EndM));
        }

        var middleM = (leastM + mostM) * 0.5f;
        var reachM = Vector2.Distance(leastM, mostM) * 0.5f;
        if (reachM > widestM) return false;

        for (var junction = 0; junction < junctions.CentreM.Length; junction++)
        {
            if (Vector2.Distance(middleM, junctions.CentreM[junction]) + reachM <= junctions.RadiusM[junction]) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether any piece of a ring runs along a lane's own edge (<see cref="KerbEnds.LaneUnder"/>) — a road's kerb, as
    /// against the edge of a movement through a box or a band's square end — at either end or at its middle.
    /// </summary>
    static bool AlongAKerb(KerbEnds.Under under, ArcSeg[] ring)
    {
        foreach (var piece in ring)
        {
            if (under.LaneOf(piece, fromStart: true) >= 0 || under.LaneOf(piece, fromStart: false) >= 0 || under.LaneMidway(piece) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <b>Every band the driven ground on one level is merged from</b>, a line and its width each: the town's driven
    /// lines under their own numbers — those on another level or laid over a twin's line empty — and after them the
    /// stubs under the decks and past the map's edge, and the tapers a kerb eases in over where a roadside or a lane is
    /// lost (<see cref="LaneLines.Tapers"/>).
    /// </summary>
    internal static (ArcSeg[][] Lines, float[] WidthM) Bands(
        Paving paving, SimConfig config, byte level, ShellOf of = ShellOf.Driven)
    {
        var count = paving.DrivenCount;
        var lines = new List<ArcSeg[]>(count);
        var widthM = new List<float>(count);
        var lanes = paving.Lanes;
        var roads = paving.Of.Roads;

        for (var line = 0; line < count; line++)
        {
            var handed = !Twin(lanes, line) && paving.DrivenLevel(line) == level && Takes(of, lanes, roads, line);
            lines.Add(handed ? paving.ArcsOfDriven(line).ToArray() : []);
            widthM.Add(paving.DrivenWidthM(line));
        }

        if (level == CityPlan.RoadArrays.Ground) UnderTheDecks(paving, config, lines, widthM, of);
        if (of is ShellOf.Walked or ShellOf.WalkedCourses) PastTheCrossings(paving, config, level, of, lines, widthM);
        OffTheMap(paving, config, level, lines, widthM, of);
        foreach (var taper in lanes.Tapers)
        {
            if (taper.Level != level || !TakesRoad(of, roads, taper.Road)) continue;

            lines.Add(taper.Line);
            widthM.Add(taper.WidthM);
        }

        return ([.. lines], [.. widthM]);
    }

    /// <summary>The bands' own index, for a set of them that is not the town's driven lines under their numbers.</summary>
    internal static ChainIndex Indexed(ReadOnlySpan<ArcSeg[]> lines, SimConfig config)
    {
        var building = new ChainIndex.Builder();
        for (var line = 0; line < lines.Length; line++)
        {
            building.Add(line, lines[line], Spline.TotalLengthM(lines[line]));
        }

        return building.Seal(config.Grid.Main);
    }

    static bool Twin(LaneLines lanes, int line) =>
        line < lanes.LaneCount && lanes.LaneOverOneLine[line] && !lanes.LaneForward[line] && lanes.LaneReverse[line] >= 0;

    /// <summary>
    /// <b>Whether a walk is struck off one driven line</b> (GEN-75): a lane of a walked road, a movement between two of
    /// them, and the turn to the kerb side between one of them and a road with no walk — the movement the corner's
    /// kerb runs along, so the walk turns that corner and ends at its mouth rather than stopping square at the box.
    /// </summary>
    /// <remarks>
    /// <b>Never the far-side turn onto or off such a road</b>: it crosses the whole carriageway, and the walk struck off
    /// it would stand at the far kerb, where nothing is walked.
    /// </remarks>
    static bool Walked(LaneLines lanes, CityPlan.RoadArrays roads, int line)
    {
        if (line < lanes.LaneCount) return roads.IsWalked(lanes.LaneRoad[line]);

        var movement = line - lanes.LaneCount;
        var from = roads.IsWalked(lanes.LaneRoad[lanes.ConnectorFromLane[movement]]);
        var to = roads.IsWalked(lanes.LaneRoad[lanes.ConnectorToLane[movement]]);
        return from && to || (from || to) && lanes.ConnectorKind[movement] == LaneTurn.NearSide;
    }

    /// <summary>
    /// <b>Whether a driven line is a road with no walk's own</b> (GEN-75): a lane of one, or a movement between two of
    /// them — never a turn onto one from a walked road, which the town's own carriageway carries.
    /// </summary>
    static bool Unwalked(LaneLines lanes, CityPlan.RoadArrays roads, int line)
    {
        if (line < lanes.LaneCount) return !roads.IsWalked(lanes.LaneRoad[line]);

        var movement = line - lanes.LaneCount;
        return !roads.IsWalked(lanes.LaneRoad[lanes.ConnectorFromLane[movement]])
               && !roads.IsWalked(lanes.LaneRoad[lanes.ConnectorToLane[movement]]);
    }

    static bool Takes(ShellOf of, LaneLines lanes, CityPlan.RoadArrays roads, int line) => of switch
    {
        ShellOf.Walked or ShellOf.WalkedCourses => Walked(lanes, roads, line),
        ShellOf.Unwalked => Unwalked(lanes, roads, line),
        _ => true,
    };

    static bool TakesRoad(ShellOf of, CityPlan.RoadArrays roads, int road) => of switch
    {
        ShellOf.Walked or ShellOf.WalkedCourses => roads.IsWalked(road),
        ShellOf.Unwalked => !roads.IsWalked(road),
        _ => true,
    };

    /// <summary>
    /// <b>The first metres of a road with no walk past each mouth a walk turns onto it at every kerb</b> (GEN-75): the
    /// lane a walked turn to the kerb side joins, from the box out to the crossing painted there from the kerb end — to
    /// its middle, half of <see cref="RoadFigures.CrossingDepthM"/>, for the walk, and on to its far edge, the whole of
    /// it, for the walk's lanes (<see cref="ShellOf.WalkedCourses"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The walk's own end reaches past the band's</b>, its outer face rounding the band's end at its whole width, so
    /// from the zebra's middle it covers the zebra and the walk's lanes to its far edge, and ends a little past it. <b>The
    /// lanes need their own band to run that far</b>: a course rounds a band's end at its own offset, so struck off the
    /// walk's it would turn back before the zebra's middle. Their walkers run straight onto the zebra and off it
    /// (<c>CrossingWays</c>).
    /// </para>
    /// <para>
    /// <b>None at a mouth turned onto at one kerb of two</b>: no zebra is painted there (<see cref="KerbEnds"/>), so the
    /// walk ends on the corner's own turn. A two-way road has two kerb lanes at a box, one leaving it and one arriving; a
    /// one-way road, one carriageway of a divided road among them, has the one.
    /// </para>
    /// </remarks>
    static void PastTheCrossings(
        Paving paving, SimConfig config, byte level, ShellOf of, List<ArcSeg[]> lines, List<float> widthM)
    {
        var lanes = paving.Lanes;
        var roads = paving.Of.Roads;
        var reachM = config.Road.CrossingDepthM * (of == ShellOf.WalkedCourses ? 1f : 0.5f);
        var stubs = new HashSet<(int Lane, bool Leaving)>();
        var walkedKerbs = new Dictionary<(int Road, int Junction), int>();
        for (var movement = 0; movement < lanes.ConnectorCount; movement++)
        {
            if (lanes.ConnectorKind[movement] != LaneTurn.NearSide) continue;

            var (from, to) = (lanes.ConnectorFromLane[movement], lanes.ConnectorToLane[movement]);
            var (fromWalked, toWalked) = (roads.IsWalked(lanes.LaneRoad[from]), roads.IsWalked(lanes.LaneRoad[to]));
            if (fromWalked == toWalked) continue;

            var lane = fromWalked ? to : from;
            if (lanes.LaneFromKerb[lane] != 0 || lanes.LaneLevel[lane] != level || Twin(lanes, lane)) continue;
            if (!stubs.Add((lane, fromWalked))) continue;

            var mouth = Mouth(lane, fromWalked);
            walkedKerbs[mouth] = walkedKerbs.GetValueOrDefault(mouth) + 1;
        }

        var kerbs = new Dictionary<(int Road, int Junction), int>();
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            if (lanes.LaneFromKerb[lane] != 0 || Twin(lanes, lane)) continue;

            foreach (var mouth in (ReadOnlySpan<(int, int)>)[Mouth(lane, leaving: true), Mouth(lane, leaving: false)])
            {
                if (walkedKerbs.ContainsKey(mouth)) kerbs[mouth] = kerbs.GetValueOrDefault(mouth) + 1;
            }
        }

        var scratch = new ArcSeg[lanes.LaneArcs.Length];
        foreach (var (lane, leaving) in stubs)
        {
            var mouth = Mouth(lane, leaving);
            if (walkedKerbs[mouth] < kerbs[mouth]) continue;

            var lengthM = lanes.LaneLengthM[lane];
            var stubM = MathF.Min(reachM, lengthM);
            var (stubFromM, stubToM) = leaving ? (0f, stubM) : (lengthM - stubM, lengthM);
            var pieces = Spline.SubChainInto(lanes.ArcsOf(lane), stubFromM, stubToM, scratch);
            if (pieces == 0) continue;

            lines.Add(scratch.AsSpan(0, pieces).ToArray());
            widthM.Add(lanes.LaneWidthM[lane]);
        }

        (int Road, int Junction) Mouth(int lane, bool leaving) =>
            (lanes.LaneRoad[lane], leaving ? lanes.LaneFromJunction[lane] : lanes.LaneToJunction[lane]);
    }

    /// <summary>
    /// The first metres of every lane above the ground at each bridgehead it ends at, as lines of the ground's own
    /// (<see cref="SimConfig.UnderTheDeckM"/>): numbered after the town's driven lines, so they need an index of
    /// their own.
    /// </summary>
    static void UnderTheDecks(Paving paving, SimConfig config, List<ArcSeg[]> lines, List<float> widthM, ShellOf of)
    {
        var lanes = paving.Lanes;
        if (!lanes.Levelled) return;

        var scratch = new ArcSeg[lanes.LaneArcs.Length];
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            if (lanes.LaneLevel[lane] == CityPlan.RoadArrays.Ground || Twin(lanes, lane)) continue;
            if (!TakesRoad(of, paving.Of.Roads, lanes.LaneRoad[lane])) continue;

            var lengthM = lanes.LaneLengthM[lane];
            var stubM = MathF.Min(config.UnderTheDeckM, lengthM);
            if (lanes.IsBridgehead(lanes.LaneFromJunction[lane])) Stub(0f, stubM);
            if (lanes.IsBridgehead(lanes.LaneToJunction[lane])) Stub(lengthM - stubM, lengthM);

            void Stub(float fromM, float toM)
            {
                var pieces = Spline.SubChainInto(lanes.ArcsOf(lane), fromM, toM, scratch);
                if (pieces == 0) return;

                lines.Add(scratch.AsSpan(0, pieces).ToArray());
                widthM.Add(lanes.LaneWidthM[lane]);
            }
        }
    }

    /// <summary>
    /// <b>Every lane that runs off the map carried on straight past its edge</b> (<see cref="CityPlan.JunctionArrays.RunsOffTheMap"/>,
    /// GEN-2b): out to where the whole of its width has left the map and <see cref="SimConfig.PastTheMapEdgeM"/> on, so
    /// the boundary turns round its end off the map and the map keeps a road that runs to its edge. Numbered after the
    /// town's driven lines, as the decks' are.
    /// </summary>
    static void OffTheMap(Paving paving, SimConfig config, byte level, List<ArcSeg[]> lines, List<float> widthM, ShellOf of)
    {
        var junctions = paving.Of.Junctions;
        if (junctions.RunsOffTheMap.Length == 0) return;

        var lanes = paving.Lanes;
        var worldM = paving.Of.WorldSizeM;
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            if (lanes.LaneLevel[lane] != level || Twin(lanes, lane)) continue;
            if (!TakesRoad(of, paving.Of.Roads, lanes.LaneRoad[lane])) continue;

            PastTheEdge(
                junctions, worldM, config, lanes.ArcsOf(lane), lanes.LaneWidthM[lane], lanes.LaneFromJunction[lane],
                lanes.LaneToJunction[lane], lines, widthM);
        }
    }

    /// <summary>
    /// A band run on straight past the map's edge from whichever of its ends stands where a road runs off the map: out
    /// to where the whole of its width has left the map and <see cref="SimConfig.PastTheMapEdgeM"/> on.
    /// </summary>
    static void PastTheEdge(
        CityPlan.JunctionArrays junctions, Vector2 worldM, SimConfig config, ReadOnlySpan<ArcSeg> arcs, float bandM,
        int fromJunction, int toJunction, List<ArcSeg[]> lines, List<float> widthM)
    {
        if (junctions.RunsOff(fromJunction)) Stub(arcs[0].StartM, arcs[0].HeadingRad + MathF.PI);
        if (junctions.RunsOff(toJunction)) Stub(arcs[^1].EndM, arcs[^1].HeadingAtRad(arcs[^1].LengthM));

        void Stub(Vector2 fromM, float headingRad)
        {
            var along = Heading.Unit(headingRad);
            var acrossM = Heading.RightOf(along) * (bandM * 0.5f);
            var leftM = MathF.Max(OutOf(worldM, fromM + acrossM, along), OutOf(worldM, fromM - acrossM, along));
            lines.Add([new ArcSeg(fromM, headingRad, leftM + config.PastTheMapEdgeM, 0f)]);
            widthM.Add(bandM);
        }
    }

    /// <summary>How far a place on the map goes along a direction before it leaves the map — nought from off it.</summary>
    static float OutOf(Vector2 worldM, Vector2 fromM, Vector2 along)
    {
        var leftM = float.PositiveInfinity;
        if (along.X != 0f) leftM = MathF.Min(leftM, ((along.X > 0f ? worldM.X : 0f) - fromM.X) / along.X);
        if (along.Y != 0f) leftM = MathF.Min(leftM, ((along.Y > 0f ? worldM.Y : 0f) - fromM.Y) / along.Y);
        return MathF.Max(0f, leftM);
    }
}
