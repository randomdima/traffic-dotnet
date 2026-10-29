using System.Numerics;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>One district's streets as they are to be laid: its own bearing and spacing, and what it is built as.</summary>
/// <param name="BearingRad">The way its lattice runs: its own ground's longer axis, turned a little by a draw (GEN-74).</param>
/// <param name="BlockAlongM">How far apart its streets stand along its bearing.</param>
/// <param name="BlockAcrossM">And across it, which is the longer of the two: a block is a rectangle.</param>
/// <param name="Strict">Whether it is laid as a grid rather than loose (GEN-47).</param>
/// <param name="StraightShare">
/// <b>How many of its streets are laid straight</b> (GEN-47), drawn for the district near its kind's own share, so no two
/// of a kind lay the same mix. The lattice takes it as a count that is never all of them and never none
/// (<see cref="Lattice"/>).
/// </param>
/// <param name="Kind">What it is built as (GEN-74).</param>
internal readonly record struct District(
    float BearingRad, float BlockAlongM, float BlockAcrossM, bool Strict, float StraightShare, ZoneKind Kind)
{
    /// <summary>The tighter of its two spacings, which is what a street's own wander is bounded by.</summary>
    public float BlockTightestM => MathF.Min(BlockAlongM, BlockAcrossM);
}

/// <summary>
/// <b>What each district the land was cut into is like</b> (GEN-47, GEN-74): its bearing, its spacing, whether it is a
/// grid, and what it is built as — drawn off where it stands and what it borders, never off a compass.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bearing belongs to the district and not to the node</b>: a grid is bearing coherence, and a bearing drawn per
/// node produces noise wearing a grid's clothes. It is the district's own longer axis, so its streets run the way its
/// ground does.
/// </para>
/// <para>
/// <b>The middle of a town is finer than its edge, and denser</b>: a district near the core draws its spacing from the
/// tighter half of the range and is built as an old town, the ring past it as housing, and the edge as suburbs or estates —
/// but one a railway or an intercity road runs along most of leans industrial, which is where a town puts its works.
/// </para>
/// </remarks>
internal sealed class Districts
{
    readonly District[] _districts;
    readonly Partition _partition;
    readonly float _outsideM;

    Districts(District[] districts, Partition partition, float outsideM, CityGenFigures figures)
    {
        _districts = districts;
        _partition = partition;
        _outsideM = outsideM;
        CoarsestM = Coarsest(figures);
        foreach (var district in districts) CoarsestM = (MathF.Max(CoarsestM.AlongM, district.BlockAlongM), MathF.Max(CoarsestM.AcrossM, district.BlockAcrossM));
    }

    /// <summary>
    /// The coarsest any of these districts' streets are spaced along their bearing and across it — never finer than any
    /// kind's own (<see cref="Coarsest"/>), and coarser where a district's blocks were widened by its fill (GEN-76).
    /// </summary>
    public (float AlongM, float AcrossM) CoarsestM { get; }

    public int Count => _districts.Length;

    public District this[int district] => _districts[district];

    /// <summary>Which district a point stands in, or −1.</summary>
    public int At(Vector2 pointM) => _partition.At(pointM);

    /// <summary>How tight the blocks are where a point stands: its district's tighter spacing, or the tightest a town lays outside any.</summary>
    public float BlockTightestM(Vector2 pointM) =>
        At(pointM) is var district and >= 0 ? _districts[district].BlockTightestM : _outsideM;

    /// <param name="industrial">The share of each district's edge a railway or an intercity road runs along.</param>
    /// <param name="fill">
    /// How much of what its kind builds each district builds (<see cref="TownExtent.Fill"/>), its blocks widened by the
    /// fill to the power less <see cref="CityGenFigures.TownSparseBlockShare"/>; none, or a fill of nought, widens no block.
    /// </param>
    public static Districts Plan(
        Partition partition, float[] industrial, Vector2 coreM, float gridShare, SimConfig config, ulong seed, float[]? fill = null)
    {
        var figures = config.CityGen;
        var count = partition.Count;
        var awayM = new float[count];
        for (var district = 0; district < count; district++) awayM[district] = Vector2.Distance(partition.CentreM[district], coreM);

        var furthestM = count == 0 ? 1f : MathF.Max(1f, awayM.Max());
        var byNearness = Enumerable.Range(0, count).OrderBy(district => awayM[district]).ToArray();
        var oldTowns = Math.Max(1, (int)MathF.Round(count * figures.OldTownShare));
        var rank = new int[count];
        for (var at = 0; at < count; at++) rank[byNearness[at]] = at;

        var strict = Strict(partition, gridShare, seed);
        var (axis, narrowM) = Axes(partition);
        var districts = new District[count];
        for (var district = 0; district < count; district++)
        {
            var draw = new Rng(seed, Streams.Keyed(Streams.District ^ PlanStream, KeyOf(partition, district)));
            var inner = awayM[district] < furthestM * figures.InnerDistrictShare;
            var kind = rank[district] < oldTowns ? ZoneKind.OldTown
                : industrial[district] >= figures.DistrictIndustrialEdgeShare ? ZoneKind.Industrial
                : inner ? ZoneKind.Residential
                : strict[district] ? ZoneKind.HighRise
                : ZoneKind.Suburb;
            var blocks = BlocksOf(kind, figures);
            var widened = fill is null || fill[district] <= 0f ? 1f : MathF.Pow(fill[district], -figures.TownSparseBlockShare);
            var alongM = draw.NextFloat(blocks.AlongMinM, blocks.AlongMaxM) * widened;
            var acrossM = alongM * draw.NextFloat(blocks.AspectMin, blocks.AspectMax);

            // <b>A district too narrow for its blocks' long side to stand rows across it lays them the other way round</b>:
            // its blocks run along its length, so the short side is what its narrow way is cut by.
            var bearingRad = axis[district] + draw.NextFloat(-figures.DistrictBearingSpreadRad, figures.DistrictBearingSpreadRad);
            if (narrowM[district] < acrossM * NarrowRows) bearingRad += MathF.PI * 0.5f;

            districts[district] = new District(
                bearingRad,
                alongM,
                acrossM,
                strict[district],
                StraightShareOf(seed, KeyOf(partition, district), strict[district], config),
                kind);
        }

        return new Districts(districts, partition, TightestAlongM(figures), figures);
    }

    /// <summary>
    /// How many of its blocks' long sides a district must be wide across its own axis to lay them across it: two, so its
    /// lattice stands three rows of points and a street runs between each pair.
    /// </summary>
    const float NarrowRows = 2f;

    /// <summary>How a kind of district lays its blocks (GEN-74); a kind no district is laid as, the housing's.</summary>
    public static BlockFigures BlocksOf(ZoneKind kind, CityGenFigures figures) => kind switch
    {
        ZoneKind.OldTown => figures.OldTownBlocks,
        ZoneKind.HighRise => figures.HighRiseBlocks,
        ZoneKind.Suburb => figures.SuburbBlocks,
        ZoneKind.Industrial => figures.IndustrialBlocks,
        _ => figures.ResidentialBlocks,
    };

    /// <summary>The kinds a district is laid as (GEN-74).</summary>
    static ReadOnlySpan<ZoneKind> Laid => [ZoneKind.OldTown, ZoneKind.Residential, ZoneKind.HighRise, ZoneKind.Suburb, ZoneKind.Industrial];

    /// <summary>The tightest any district's streets are spaced along its bearing.</summary>
    public static float TightestAlongM(CityGenFigures figures)
    {
        var tightestM = float.PositiveInfinity;
        foreach (var kind in Laid) tightestM = MathF.Min(tightestM, BlocksOf(kind, figures).AlongMinM);
        return tightestM;
    }

    /// <summary>The coarsest any district's streets are spaced along its bearing, and across it.</summary>
    public static (float AlongM, float AcrossM) Coarsest(CityGenFigures figures)
    {
        var (alongM, acrossM) = (0f, 0f);
        foreach (var kind in Laid)
        {
            var blocks = BlocksOf(kind, figures);
            (alongM, acrossM) = (MathF.Max(alongM, blocks.AlongMaxM), MathF.Max(acrossM, blocks.AlongMaxM * blocks.AspectMax));
        }

        return (alongM, acrossM);
    }

    /// <summary>The stream a district's plan is drawn on, apart from the partition's own (GEN-11).</summary>
    const ulong PlanStream = 0x706C_616E_0000_0000;

    /// <summary>
    /// <b>What a district's draws are keyed on: the cell its middle stands in</b> (GEN-11), and not its number — a district
    /// cut, merged or renumbered elsewhere moves no other district's bearing, spacing or grid.
    /// </summary>
    public static ulong KeyOf(Partition partition, int district) => (ulong)partition.Grid.IndexOf(partition.CentreM[district]);

    /// <summary>
    /// <b>Each district's longer axis</b>: the way its own cells spread furthest, off their second moments — so a district
    /// long along a river is laid along it — <b>and how wide it is across that axis</b>: the width of the strip whose
    /// spread across is its own, a strip of width w spreading w²/12.
    /// </summary>
    static (float[] Axis, float[] AcrossM) Axes(Partition partition)
    {
        var count = partition.Count;
        var (xx, yy, xy, cells) = (new double[count], new double[count], new double[count], new int[count]);
        for (var cell = 0; cell < partition.Label.Length; cell++)
        {
            var district = partition.Label[cell];
            if (district < 0) continue;

            var offM = partition.Grid.CentreOf(cell) - partition.CentreM[district];
            xx[district] += offM.X * offM.X;
            yy[district] += offM.Y * offM.Y;
            xy[district] += offM.X * offM.Y;
            cells[district]++;
        }

        var (axis, acrossM) = (new float[count], new float[count]);
        for (var district = 0; district < count; district++)
        {
            axis[district] = 0.5f * (float)Math.Atan2(2.0 * xy[district], xx[district] - yy[district]);
            var n = Math.Max(1, cells[district]);
            var (a, b, c) = (xx[district] / n, xy[district] / n, yy[district] / n);
            var least = ((a + c) * 0.5) - Math.Sqrt((((a - c) * 0.5) * ((a - c) * 0.5)) + (b * b));
            acrossM[district] = (float)Math.Sqrt(12.0 * Math.Max(0.0, least));
        }

        return (axis, acrossM);
    }

    /// <summary>
    /// <b>How many of one district's streets are laid straight</b>: its kind's share, moved by a draw inside
    /// <see cref="CityGenFigures.StraightShareSpread"/>. <b>A stream of its own, keyed on the district</b>, so the mix is
    /// retuned without moving a block.
    /// </summary>
    static float StraightShareOf(ulong seed, ulong key, bool strict, SimConfig config)
    {
        var figures = config.CityGen;
        var draw = new Rng(seed, Streams.Keyed(StraightStream, key));
        var share = strict ? figures.GridStraightShare : figures.LooseStraightShare;
        return Math.Clamp(share + draw.NextFloat(-figures.StraightShareSpread, figures.StraightShareSpread), 0f, 1f);
    }

    const ulong StraightStream = 0x7374_7261_6967_6874;

    const ulong StrictStream = 0x7374_7269_6374_0000;

    /// <summary>
    /// Which districts are grids: the brief's share of them, rounded, ranked by a draw each district keys on its own place
    /// (<see cref="KeyOf"/>).
    /// </summary>
    static bool[] Strict(Partition partition, float share, ulong seed)
    {
        var count = partition.Count;
        var strict = new bool[count];
        var order = new float[count];
        for (var district = 0; district < count; district++) order[district] = new Rng(seed, Streams.Keyed(StrictStream, KeyOf(partition, district))).NextFloat();

        var ranked = Enumerable.Range(0, count).OrderBy(district => order[district]).ToArray();
        var grids = (int)MathF.Round(count * share);
        for (var at = 0; at < grids; at++) strict[ranked[at]] = true;

        return strict;
    }
}
