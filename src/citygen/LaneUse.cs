namespace TrafficSimulation.CityGen;

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
        if (fromKerb < first || fromKerb > last) return false;

        var at = fromKerb - first;
        var of = last - first + 1;
        var lowest = at * ontoLanes / of;
        var highest = ((((at + 1) * ontoLanes) + of - 1) / of) - 1;
        return ontoFromKerb >= lowest && ontoFromKerb <= highest;
    }

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
