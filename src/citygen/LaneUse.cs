namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The turns a lane is marked for</b> (TER-5j) — the arrows on it — at the junction it runs into, in this
/// engine's own words: straight on, to the near side, across to the far side, and a slight turn, which is its
/// side's turn where the junction makes one and straight on where it makes none.
/// </summary>
[Flags]
internal enum MarkedTurns : byte
{
    None = 0,
    Straight = 1 << 0,
    NearSide = 1 << 1,
    FarSide = 1 << 2,
    BearNearSide = 1 << 3,
    BearFarSide = 1 << 4,
}

/// <summary>A turn at one junction from one road into another.</summary>
internal readonly record struct RoadTurn(int Junction, int FromRoad, int ToRoad);

/// <summary>A turn at one junction from one lane of a road onto one lane of another, each counted from its kerb.</summary>
internal readonly record struct LaneLink(int Junction, int FromRoad, int FromKerb, int ToRoad, int ToKerb);

/// <summary>
/// <b>Which lanes of an arm a movement is made from, and which lanes of the road it takes it reaches</b>
/// (TER-5j): carrying straight on from any of them, turning to the near side from the kerb lane and to the
/// far side from the lane beside the line the two ways meet on — the rule of a carriageway marked with
/// nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where nothing carries straight on, the lanes are shared out between the turns there are</b>: the kerb
/// half to the near side and the other half to the far, the middle lane of an odd count to both, and the
/// whole of them to a turn that is the only kind on offer — so a bend, or a fork into two roads, is driven
/// out of every lane that arrives at it. <b>No lane arrives at a junction that offers it nothing.</b>
/// </para>
/// <para>
/// <b>The lanes a movement is made from are spread over the lanes it reaches in the same order</b>, each
/// arriving lane taking the share of them that stands where it does: as many onto as many lane for lane,
/// fewer onto more each fanning out over its share, more onto fewer merging into theirs. So no two movements off
/// one arm onto one road cross each other, and no lane of the road taken is reached by none of them.
/// </para>
/// <para>
/// <b>One lane each way is today's town and comes out exactly as it was</b>: every lane is the kerb lane and
/// the inner lane at once, so it is offered every turn its node makes and reaches the one lane each takes.
/// </para>
/// <para>
/// <b>A painted arm is made from as it is painted</b> (a traced map's, GEN-57): a lane with arrows makes what they
/// name, and the lanes making a turn are spread over the lanes it reaches as any others are. What a node does not
/// make at all — a restriction's turn, a turn whose lanes OSM names — is the caller's (<c>LaneLines</c>).
/// </para>
/// </remarks>
internal static class LaneUse
{
    /// <summary>The kinds of turn a node offers the lanes of one arm, before any is given to a lane.</summary>
    public readonly record struct Offered(bool Straight, bool NearSide, bool FarSide)
    {
        public Offered With(LaneTurn turn) => turn switch
        {
            LaneTurn.Straight => this with { Straight = true },
            LaneTurn.NearSide => this with { NearSide = true },
            _ => this with { FarSide = true },
        };
    }

    /// <summary>
    /// Whether the lane <paramref name="fromKerb"/> in from the kerb of an arm of <paramref name="lanes"/> is
    /// joined by a turn of this kind to the lane <paramref name="ontoFromKerb"/> in from the kerb of a road
    /// taking it in <paramref name="ontoLanes"/>.
    /// </summary>
    public static bool Joins(Offered offered, LaneTurn turn, int fromKerb, int lanes, int ontoFromKerb, int ontoLanes)
    {
        var (first, last) = MadeFrom(offered, turn, lanes);
        return fromKerb >= first && fromKerb <= last && Spread(fromKerb - first, last - first + 1, ontoFromKerb, ontoLanes);
    }

    /// <summary>
    /// <b>The same, off an arm with arrows painted on it</b> (<see cref="MarkedTurns"/>, from the kerb, or none). A lane
    /// with arrows makes the turns they name that the node offers; a lane with none — or none the node offers — makes
    /// what a lane of an unmarked arm would. <b>A turn no lane of the arm makes is not made from it.</b>
    /// </summary>
    public static bool Joins(
        Offered offered, LaneTurn turn, ReadOnlySpan<MarkedTurns> arrows, int fromKerb, int lanes, int ontoFromKerb, int ontoLanes)
    {
        if (arrows.Length != lanes) return Joins(offered, turn, fromKerb, lanes, ontoFromKerb, ontoLanes);

        var at = -1;
        var of = 0;
        for (var lane = 0; lane < lanes; lane++)
        {
            if (!Makes(offered, turn, arrows[lane], lane, lanes)) continue;

            if (lane == fromKerb) at = of;
            of++;
        }

        return at >= 0 && Spread(at, of, ontoFromKerb, ontoLanes);
    }

    /// <summary>
    /// Whether the <paramref name="at"/>th of the <paramref name="of"/> lanes a movement is made from, counted from
    /// the kerb, reaches a lane of the road taken: its own share of them, in the same order.
    /// </summary>
    static bool Spread(int at, int of, int ontoFromKerb, int ontoLanes)
    {
        var lowest = at * ontoLanes / of;
        var highest = ((((at + 1) * ontoLanes) + of - 1) / of) - 1;
        return ontoFromKerb >= lowest && ontoFromKerb <= highest;
    }

    static bool Makes(Offered offered, LaneTurn turn, MarkedTurns arrows, int lane, int lanes)
    {
        var admitted = Admitted(arrows, offered);
        if (admitted != MarkedTurns.None) return (admitted & Arrow(turn)) != 0;

        var (first, last) = MadeFrom(offered, turn, lanes);
        return lane >= first && lane <= last;
    }

    /// <summary>The turns a lane's arrows name that the node offers, each slight one read as its side's or straight on.</summary>
    static MarkedTurns Admitted(MarkedTurns arrows, Offered offered)
    {
        var admitted = MarkedTurns.None;
        if (arrows.HasFlag(MarkedTurns.Straight) && offered.Straight) admitted |= MarkedTurns.Straight;
        if (arrows.HasFlag(MarkedTurns.NearSide) && offered.NearSide) admitted |= MarkedTurns.NearSide;
        if (arrows.HasFlag(MarkedTurns.FarSide) && offered.FarSide) admitted |= MarkedTurns.FarSide;
        if (arrows.HasFlag(MarkedTurns.BearNearSide)) admitted |= Bearing(offered.NearSide, MarkedTurns.NearSide, offered);
        if (arrows.HasFlag(MarkedTurns.BearFarSide)) admitted |= Bearing(offered.FarSide, MarkedTurns.FarSide, offered);
        return admitted;

        static MarkedTurns Bearing(bool sideOffered, MarkedTurns side, Offered offered) =>
            sideOffered ? side : offered.Straight ? MarkedTurns.Straight : MarkedTurns.None;
    }

    static MarkedTurns Arrow(LaneTurn turn) => turn switch
    {
        LaneTurn.Straight => MarkedTurns.Straight,
        LaneTurn.NearSide => MarkedTurns.NearSide,
        _ => MarkedTurns.FarSide,
    };

    /// <summary>The lanes of an arm a turn of this kind is made from, counted in from the kerb, first and last.</summary>
    public static (int First, int Last) MadeFrom(Offered offered, LaneTurn turn, int lanes) => turn switch
    {
        LaneTurn.Straight => (0, lanes - 1),
        LaneTurn.NearSide when offered.Straight => (0, 0),
        LaneTurn.NearSide when offered.FarSide => (0, ((lanes + 1) / 2) - 1),
        LaneTurn.NearSide => (0, lanes - 1),
        _ when offered.Straight => (lanes - 1, lanes - 1),
        _ when offered.NearSide => (lanes / 2, lanes - 1),
        _ => (0, lanes - 1),
    };
}
