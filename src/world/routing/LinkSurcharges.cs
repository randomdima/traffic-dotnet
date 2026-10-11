namespace TrafficSimulation.World.Routing;

/// <summary>
/// The one thing that changes about a network while the town runs: a way somebody gave up on, handed to
/// the search as a <b>surcharge</b> rather than baked into a weight.
/// </summary>
/// <remarks>
/// <para>
/// <b>Expensive, never impassable.</b> In a town this small the only road to a place may be the marked
/// one, so a ban would strand whoever marked it. A walker's crossing wait and the one stretch a re-plan
/// is going round are this same device with a different price on it.
/// </para>
/// <para>
/// <b>A mark expires and is never swept</b>, so the size of the table says nothing about what is marked
/// and no caller may infer one from the other. What a reader outside the search asks instead is
/// <see cref="Generation"/>, which moves whenever the answer to <em>is this marked</em> moves for any
/// link — a mark laid, a mark overwritten, or a mark whose life simply ran out.
/// </para>
/// </remarks>
internal sealed class LinkSurcharges
{
    readonly int[] _link;
    readonly float[] _untilS;
    readonly float[] _priceM;

    /// <summary>
    /// The slots that may still be live, each once: every slot live when a life last ran out, and every one
    /// marked since. What <see cref="PriceM"/> walks, so a table full of spent marks costs it nothing.
    /// </summary>
    readonly int[] _live;

    readonly bool[] _listed;

    int _count;
    int _oldest;
    int _liveCount;
    float _nowS;
    float _nextExpiryS = float.PositiveInfinity;

    /// <param name="most">
    /// How many marks are live at once. The oldest is overwritten rather than the table grown: a town
    /// with more gave-up roads than this has a problem no bookkeeping fixes.
    /// </param>
    public LinkSurcharges(int most)
    {
        _link = new int[most];
        _untilS = new float[most];
        _priceM = new float[most];
        _live = new int[most];
        _listed = new bool[most];
    }

    /// <summary>Moves whenever what is marked changes, expiry included.</summary>
    public int Generation { get; private set; }

    /// <summary>Prices a link up for a while. A second mark on one link is a second entry and the dearer answers.</summary>
    public void Mark(int link, float priceM, float forS)
    {
        var slot = _count < _link.Length ? _count++ : _oldest++ % _link.Length;
        _oldest %= _link.Length;
        _link[slot] = link;
        _priceM[slot] = priceM;
        _untilS[slot] = _nowS + forS;
        _nextExpiryS = MathF.Min(_nextExpiryS, _untilS[slot]);
        List(slot);
        Generation++;
    }

    /// <summary>
    /// What this link costs on top of its weight, or nothing. Walked rather than indexed: the table
    /// holds a handful of entries against a network of thousands of links, so an array per link would be
    /// a town's worth of zeroes to say that almost nothing is marked.
    /// </summary>
    /// <remarks>
    /// <b>Only the slots that may be live are walked</b> (<see cref="_live"/>), and a mark that has expired
    /// since it was listed is still asked its life, so what is walked changes the cost and never the answer.
    /// </remarks>
    public float PriceM(int link)
    {
        var priceM = 0f;
        for (var index = 0; index < _liveCount; index++)
        {
            var slot = _live[index];
            if (_link[slot] == link && _untilS[slot] > _nowS) priceM = MathF.Max(priceM, _priceM[slot]);
        }

        return priceM;
    }

    /// <summary>How many marks may still be live — the bound <see cref="MarkedLink"/> is asked under.</summary>
    public int MayBeLive => _liveCount;

    /// <summary>
    /// How many slots hold a mark, live or spent — what a copy of the table is laid from (<see cref="MarkIn"/>), for a
    /// search that reads it elsewhere (<c>DeviceRoutes</c>).
    /// </summary>
    /// <remarks>
    /// <b>A slot off the live list is spent at <see cref="NowS"/></b>: it leaves the list only on a scan that found its life
    /// run out, and every slot is listed again when the clock runs back. So every slot asked its life against
    /// <see cref="NowS"/> answers what <see cref="PriceM"/> and <see cref="MarkedLink"/> answer.
    /// </remarks>
    public int SlotsMarked => _count;

    /// <summary>One slot's mark as laid: the link it prices up, by how much, and until when.</summary>
    public (int Link, float PriceM, float UntilS) MarkIn(int slot) => (_link[slot], _priceM[slot], _untilS[slot]);

    /// <summary>The clock the marks are read against, as <see cref="Advance"/> last set it.</summary>
    public float NowS => _nowS;

    /// <summary>
    /// <b>How many marks were ever laid</b>, as far as which slot the next one overwrites goes: the slots filled, and past
    /// a full table the oldest's place — what a copy of the table marks on from (<c>route_tables.glsl</c>'s <c>marked</c>).
    /// </summary>
    public int MarksLaid => _count < _link.Length ? _count : _link.Length + _oldest;

    /// <summary>
    /// The link one of the marks that may still be live prices up, or <see cref="TravelGraph.NoLink"/> where its
    /// life has run out — for a search that has to know whether its ground holds one (<see cref="RouteSearch"/>).
    /// </summary>
    public int MarkedLink(int index)
    {
        var slot = _live[index];
        return _untilS[slot] > _nowS ? _link[slot] : TravelGraph.NoLink;
    }

    /// <summary>
    /// The clock the marks live against. The scan happens only on the tick a mark's life actually ends,
    /// and what it produces is the next such tick — so the ordinary tick reads one comparison.
    /// </summary>
    public void Advance(float nowS)
    {
        // A clock run backwards brings spent marks back to life, so every slot is listed again.
        if (nowS < _nowS)
        {
            for (var slot = 0; slot < _count; slot++) List(slot);
        }

        _nowS = nowS;
        if (nowS < _nextExpiryS) return;

        Generation++;
        _nextExpiryS = float.PositiveInfinity;
        _liveCount = 0;
        for (var slot = 0; slot < _count; slot++)
        {
            _listed[slot] = false;
            if (_untilS[slot] > nowS)
            {
                _nextExpiryS = MathF.Min(_nextExpiryS, _untilS[slot]);
                List(slot);
            }
        }
    }

    void List(int slot)
    {
        if (_listed[slot]) return;

        _listed[slot] = true;
        _live[_liveCount++] = slot;
    }
}
