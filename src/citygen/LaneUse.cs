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
/// <b>A lane making a movement joins the lane of its own number on the road taken</b> (<see cref="Joins"/>), both
/// numbered from the side the movement bears to — the kerb for the near side, the line for the far. So a turn onto
/// a road of one lane is made from the edge lane alone, never from the second lane onto the first, and no two
/// movements off one arm onto one road cross each other.
/// </para>
/// <para>
/// <b>A lane with no lane of its number there joins nothing</b> (GEN-50): of more lanes onto fewer the ones over end,
/// and of fewer onto more the ones over are reached by none — but onto a road of one lane more, whose last lane the
/// last lane in joins as well as its own. A car gets off the one and onto the other by moving across (CAR-53). <b>The
/// ground between them is still paved</b> (<see cref="Eases"/>), over the line the lanes' spread would have joined them by.
/// </para>
/// <para>
/// <b>One lane each way is today's town and comes out exactly as it was</b>: every lane is the kerb lane and
/// the inner lane at once, so it is offered every turn its node makes and reaches the one lane each takes.
/// </para>
/// <para>
/// <b>A painted arm is made from as it is painted</b> (a traced map's, GEN-57): a lane with arrows makes what they
/// name, and is joined onward as any other. What a node does not make at all — a restriction's turn, a turn whose
/// lanes OSM names — is the caller's (<c>LaneLines</c>).
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
    /// Whether the lane <paramref name="fromKerb"/> in from the kerb of an arm of <paramref name="lanes"/>, painted
    /// with <paramref name="arrows"/> (from the kerb, or none), is joined by a turn of this kind to the lane
    /// <paramref name="ontoFromKerb"/> in from the kerb of a road taking it in <paramref name="ontoLanes"/>: lane for
    /// lane, both numbered from the kerb where the movement <paramref name="bearsToTheKerb"/> and from the line where
    /// it bears away from it, and the last lane onto the last as well where the road taken has one lane more.
    /// </summary>
    public static bool Joins(
        Offered offered, LaneTurn turn, ReadOnlySpan<MarkedTurns> arrows, int fromKerb, int lanes, int ontoFromKerb,
        int ontoLanes, bool bearsToTheKerb)
    {
        if (!Makes(offered, turn, arrows, fromKerb, lanes)) return false;

        var from = FromItsSide(fromKerb, lanes, bearsToTheKerb);
        var onto = FromItsSide(ontoFromKerb, ontoLanes, bearsToTheKerb);
        return onto == from || (ontoLanes == lanes + 1 && from == lanes - 1 && onto == lanes);
    }

    /// <summary>
    /// <b>Whether the ground between two lanes is paved though no car is joined over it</b> (<see cref="LaneLines.Tapers"/>):
    /// the lanes making the turn spread over the lanes it reaches in the same order, fewer onto more each fanning out
    /// over its share, more onto fewer merging into theirs. Asked only where lane for lane (<see cref="Joins"/>) leaves
    /// one of the two ending or unreached — which is the caller's (<c>LaneLines</c>).
    /// </summary>
    public static bool Eases(
        Offered offered, LaneTurn turn, ReadOnlySpan<MarkedTurns> arrows, int fromKerb, int lanes, int ontoFromKerb, int ontoLanes)
    {
        var at = -1;
        var of = 0;
        for (var lane = 0; lane < lanes; lane++)
        {
            if (!Makes(offered, turn, arrows, lane, lanes)) continue;

            if (lane == fromKerb) at = of;
            of++;
        }

        return at >= 0 && Spread(at, of, ontoFromKerb, ontoLanes);
    }

    static int FromItsSide(int fromKerb, int lanes, bool countedFromTheKerb) =>
        countedFromTheKerb ? fromKerb : lanes - 1 - fromKerb;

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

    /// <summary>
    /// <b>Whether a lane makes a turn of this kind</b>: the turns its arrows name that the node offers, or — with none,
    /// none the node offers, or an arm painted for some other count of lanes — what a lane of an unmarked arm would.
    /// A turn no lane of the arm makes is not made from it.
    /// </summary>
    static bool Makes(Offered offered, LaneTurn turn, ReadOnlySpan<MarkedTurns> arrows, int lane, int lanes)
    {
        var admitted = arrows.Length == lanes ? Admitted(arrows[lane], offered) : MarkedTurns.None;
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
