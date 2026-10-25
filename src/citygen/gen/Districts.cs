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
    /// <summary>How far a district's bearing is turned off its own axis, either way.</summary>
    const float BearingSpreadRad = 0.17f;

    /// <summary>The share of a town's districts nearest its core that are its old town, and at least one.</summary>
    const float OldTownShare = 0.1f;

    /// <summary>How far out, as a share of the furthest district's distance from the core, the housing gives way to the edge's kinds.</summary>
    const float InnerShare = 0.55f;

    readonly District[] _districts;
    readonly Partition _partition;
    readonly float _outsideM;

    Districts(District[] districts, Partition partition, float outsideM)
    {
        _districts = districts;
        _partition = partition;
        _outsideM = outsideM;
    }

    public int Count => _districts.Length;

    public District this[int district] => _districts[district];

    /// <summary>Which district a point stands in, or −1.</summary>
    public int At(Vector2 pointM) => _partition.At(pointM);

    /// <summary>How tight the blocks are where a point stands: its district's tighter spacing, or the tightest a town lays outside any.</summary>
    public float BlockTightestM(Vector2 pointM) =>
        At(pointM) is var district and >= 0 ? _districts[district].BlockTightestM : _outsideM;

    /// <param name="industrial">The share of each district's edge a railway or an intercity road runs along.</param>
    public static Districts Plan(Partition partition, float[] industrial, Vector2 coreM, float gridShare, SimConfig config, ulong seed)
    {
        var figures = config.CityGen;
        var draw = new Rng(seed, Streams.District ^ PlanStream);
        var count = partition.Count;
        var awayM = new float[count];
        for (var district = 0; district < count; district++) awayM[district] = Vector2.Distance(partition.CentreM[district], coreM);

        var furthestM = count == 0 ? 1f : MathF.Max(1f, awayM.Max());
        var byNearness = Enumerable.Range(0, count).OrderBy(district => awayM[district]).ToArray();
        var oldTowns = Math.Max(1, (int)MathF.Round(count * OldTownShare));
        var rank = new int[count];
        for (var at = 0; at < count; at++) rank[byNearness[at]] = at;

        var strict = Strict(count, gridShare, ref draw);
        var axis = Axes(partition);
        var districts = new District[count];
        var middlingM = (figures.BlockSpacingAlongMinM + figures.BlockSpacingAlongMaxM) * 0.5f;
        for (var district = 0; district < count; district++)
        {
            var inner = awayM[district] < furthestM * InnerShare;
            var alongM = inner
                ? draw.NextFloat(figures.BlockSpacingAlongMinM, middlingM)
                : draw.NextFloat(middlingM, figures.BlockSpacingAlongMaxM);
            var kind = rank[district] < oldTowns ? ZoneKind.OldTown
                : industrial[district] >= figures.DistrictIndustrialEdgeShare ? ZoneKind.Industrial
                : inner ? ZoneKind.Residential
                : strict[district] ? ZoneKind.HighRise
                : ZoneKind.Suburb;
            districts[district] = new District(
                axis[district] + draw.NextFloat(-BearingSpreadRad, BearingSpreadRad),
                alongM,
                alongM * draw.NextFloat(figures.BlockAspectMin, figures.BlockAspectMax),
                strict[district],
                StraightShareOf(seed, district, strict[district], config),
                kind);
        }

        return new Districts(districts, partition, figures.BlockSpacingAlongMinM);
    }

    /// <summary>The stream a district's plan is drawn on, apart from the partition's own (GEN-11).</summary>
    const ulong PlanStream = 0x706C_616E_0000_0000;

    /// <summary>
    /// <b>Each district's longer axis</b>: the way its own cells spread furthest, off their second moments — so a district
    /// long along a river is laid along it.
    /// </summary>
    static float[] Axes(Partition partition)
    {
        var count = partition.Count;
        var (xx, yy, xy) = (new double[count], new double[count], new double[count]);
        for (var cell = 0; cell < partition.Label.Length; cell++)
        {
            var district = partition.Label[cell];
            if (district < 0) continue;

            var offM = partition.Grid.CentreOf(cell) - partition.CentreM[district];
            xx[district] += offM.X * offM.X;
            yy[district] += offM.Y * offM.Y;
            xy[district] += offM.X * offM.Y;
        }

        var axis = new float[count];
        for (var district = 0; district < count; district++) axis[district] = 0.5f * (float)Math.Atan2(2.0 * xy[district], xx[district] - yy[district]);

        return axis;
    }

    /// <summary>
    /// <b>How many of one district's streets are laid straight</b>: its kind's share, moved by a draw inside
    /// <see cref="CityGenFigures.StraightShareSpread"/>. <b>A stream of its own, keyed on the district</b>, so the mix is
    /// retuned without moving a block.
    /// </summary>
    static float StraightShareOf(ulong seed, int district, bool strict, SimConfig config)
    {
        var figures = config.CityGen;
        var draw = new Rng(seed, StraightStream ^ (ulong)district);
        var share = strict ? figures.GridStraightShare : figures.LooseStraightShare;
        return Math.Clamp(share + draw.NextFloat(-figures.StraightShareSpread, figures.StraightShareSpread), 0f, 1f);
    }

    const ulong StraightStream = 0x7374_7261_6967_6874;

    /// <summary>Which districts are grids: the brief's share of them, rounded, at drawn places.</summary>
    static bool[] Strict(int count, float share, ref Rng draw)
    {
        var strict = new bool[count];
        var order = new float[count];
        for (var district = 0; district < count; district++) order[district] = draw.NextFloat();

        var ranked = Enumerable.Range(0, count).OrderBy(district => order[district]).ToArray();
        var grids = (int)MathF.Round(count * share);
        for (var at = 0; at < grids; at++) strict[ranked[at]] = true;

        return strict;
    }
}
