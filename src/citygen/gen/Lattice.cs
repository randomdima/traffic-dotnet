using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>The streets inside each district</b>: a lattice on the district's own bearing at its own spacing,
/// clipped to the ground the district actually holds, and hung off the arterials it runs up against.
/// </summary>
/// <remarks>
/// <para>
/// <b>A lattice is laid, not searched for.</b> Every point is tested once — is it in this district, is it
/// on land, is it clear of an arterial — and a point that fails is simply not there. Nothing is placed and
/// taken back, and the two properties that would otherwise need a search are properties of the arrangement:
/// a district's region is convex, so a street between two of its own points stays inside it and cannot
/// cross another district's; and an arterial carries a node wherever a street meets it, so a street never
/// crosses one.
/// </para>
/// <para>
/// <b>The arrangement is what keeps the crossings rare and not what makes them impossible.</b> The stub a
/// lattice is hung by reaches ground the lattice itself was refused (<see cref="Hang"/>), which is the one
/// road here laid across a district's own edge rather than inside it; what that leaves crossing is unpicked
/// with the rest (GEN-17, <see cref="TownLayout.UnpickTheCrossings"/>) rather than searched for here.
/// </para>
/// <para>
/// <b>A street never bridges</b> (GEN-14a). It is not refused here: the layout asks the water of every road
/// it is offered, so a lattice edge that would stand on it is simply not laid.
/// </para>
/// <para>
/// <b>What is left over is deleted rather than joined up</b> — a block cut off by the water is dropped with
/// the rest of its component (<see cref="TownLayout.KeepTheLargestComponent"/>), because a link drawn to
/// reach it would have to cross whatever cut it off.
/// </para>
/// </remarks>
internal static class Lattice
{
    /// <summary>How much of a block a street's own stub may be, before the junction it would make is too near the last one.</summary>
    const float ShortestStubInBlocks = 0.15f;

    public static void Lay(
        TownLayout layout, Districts districts, Arterials arterials, TownBrief brief, GenRaster raster,
        SimConfig config, float marginM)
    {
        var extentM = new Vector2(brief.WidthM, brief.HeightM);
        var clearanceM = CorridorM(config);
        var weldM = config.RoadWidthM;

        for (var district = 0; district < districts.Count; district++)
        {
            LayOne(layout, districts, district, arterials, raster, extentM, marginM, clearanceM, weldM);
        }
    }

    /// <summary>
    /// How much ground an arterial keeps to itself: its own carriageway, its pavement, and the corner an arm
    /// turning off it needs — which is exactly the ground a street's junction with it stands on.
    /// </summary>
    public static float CorridorM(SimConfig config) =>
        (config.RoadWidthM * 0.5f) + config.PavementWidthM + config.IntersectionCornerRadiusM;

    static void LayOne(
        TownLayout layout, Districts districts, int district, Arterials arterials, GenRaster raster,
        Vector2 extentM, float marginM, float clearanceM, float weldM)
    {
        var alongSpacingM = districts[district].BlockAlongM;
        var acrossSpacingM = districts[district].BlockAcrossM;
        var along = Heading.Unit(districts[district].BearingRad);
        var across = Heading.RightOf(along);
        var reach = (int)MathF.Ceiling(extentM.Length() * 0.5f / MathF.Min(alongSpacingM, acrossSpacingM));

        var side = (reach * 2) + 1;
        var node = new int[side * side];
        Array.Fill(node, -1);

        for (var u = -reach; u <= reach; u++)
        {
            for (var v = -reach; v <= reach; v++)
            {
                var atM = districts.HubM + (along * (u * alongSpacingM)) + (across * (v * acrossSpacingM));
                if (!Stands(atM, districts, district, arterials, raster, extentM, marginM, clearanceM)) continue;

                node[Slot(u, v, reach, side)] = layout.AddNode(atM);
            }
        }

        for (var u = -reach; u <= reach; u++)
        {
            for (var v = -reach; v <= reach; v++)
            {
                var from = node[Slot(u, v, reach, side)];
                if (from < 0) continue;

                Reach(layout, node, raster, arterials, u, v, 1, 0, reach, side);
                Reach(layout, node, raster, arterials, u, v, 0, 1, reach, side);
                Hang(layout, node, districts, district, arterials, raster, extentM, marginM, clearanceM, weldM, u, v, reach, side);
            }
        }
    }

    /// <summary>Whether a lattice point is somewhere a junction may stand at all.</summary>
    static bool Stands(
        Vector2 atM, Districts districts, int district, Arterials arterials, GenRaster raster, Vector2 extentM,
        float marginM, float clearanceM) =>
        atM.X > marginM && atM.Y > marginM && atM.X < extentM.X - marginM && atM.Y < extentM.Y - marginM
        && raster.At(atM) == Ground.Grass
        && districts.At(atM) == district
        && !arterials.InACorridor(atM, clearanceM);

    /// <summary>One street of the lattice, where both its ends stand and the ground between them takes it.</summary>
    static void Reach(
        TownLayout layout, int[] node, GenRaster raster, Arterials arterials, int u, int v, int du, int dv,
        int reach, int side)
    {
        if (u + du > reach || v + dv > reach) return;

        var from = node[Slot(u, v, reach, side)];
        var to = node[Slot(u + du, v + dv, reach, side)];
        if (from < 0 || to < 0) return;

        var fromM = layout.NodeM[from];
        var toM = layout.NodeM[to];
        if (arterials.CrossesTheRing(fromM, toM)) return;

        layout.Join(from, to, RoadClass.Street);
    }

    /// <summary>
    /// The stub that joins a lattice to the arterial it stopped short of. <b>Only where the next lattice
    /// point along would have stood in the arterial's own corridor</b> — a lattice that stopped for the
    /// water or for the edge of the world has nothing to be joined to, and joining it to the nearest road
    /// anyway is how a street ends up crossing a block.
    /// </summary>
    static void Hang(
        TownLayout layout, int[] node, Districts districts, int district, Arterials arterials, GenRaster raster,
        Vector2 extentM, float marginM, float clearanceM, float weldM, int u, int v, int reach, int side)
    {
        var from = node[Slot(u, v, reach, side)];
        if (from < 0) return;

        var alongSpacingM = districts[district].BlockAlongM;
        var acrossSpacingM = districts[district].BlockAcrossM;
        var along = Heading.Unit(districts[district].BearingRad);
        var across = Heading.RightOf(along);

        foreach (var (du, dv) in (ReadOnlySpan<(int, int)>)[(1, 0), (-1, 0), (0, 1), (0, -1)])
        {
            var u2 = u + du;
            var v2 = v + dv;
            if (u2 < -reach || u2 > reach || v2 < -reach || v2 > reach) continue;
            if (node[Slot(u2, v2, reach, side)] >= 0) continue;

            var beyondM = districts.HubM + (along * (u2 * alongSpacingM)) + (across * (v2 * acrossSpacingM));
            if (!arterials.InACorridor(beyondM, clearanceM)) continue;
            if (beyondM.X < marginM || beyondM.Y < marginM
                || beyondM.X > extentM.X - marginM || beyondM.Y > extentM.Y - marginM)
            {
                continue;
            }

            var fromM = layout.NodeM[from];
            var onto = Meeting(layout, arterials, districts, fromM, beyondM, weldM);
            if (onto < 0) continue;

            var ontoM = layout.NodeM[onto];
            if ((ontoM - fromM).Length() < districts[district].BlockTightestM * ShortestStubInBlocks) continue;

            layout.Join(from, onto, RoadClass.Street);
        }
    }

    /// <summary>Which arterial the ground beyond a lattice belongs to, and the node on it a street should meet.</summary>
    static int Meeting(
        TownLayout layout, Arterials arterials, Districts districts, Vector2 fromM, Vector2 beyondM, float weldM)
    {
        var spoke = arterials.NearestSpoke(beyondM, out var alongSpokeM);
        var spokeAwayM = MathF.Abs(Cross(districts.SpokeUnit(spoke), beyondM - districts.HubM));
        var ringAwayM = districts.HasRing
            ? MathF.Abs((beyondM - districts.HubM).Length() - districts.RingRadiusM)
            : float.PositiveInfinity;

        if (ringAwayM < spokeAwayM) return arterials.OnTheRing(layout, arterials.ThetaOf(fromM), weldM);

        return alongSpokeM <= 0f ? -1 : arterials.OnASpoke(layout, spoke, alongSpokeM, weldM);
    }

    static int Slot(int u, int v, int reach, int side) => ((v + reach) * side) + u + reach;

    static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);
}
