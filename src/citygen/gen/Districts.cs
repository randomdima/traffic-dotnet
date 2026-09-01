using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// One district: the ground between two spokes, inside the orbital or outside it, laid on its own bearing
/// at its own block spacing.
/// </summary>
/// <param name="Strict">
/// Whether its streets are chords or are allowed to wander. A strict district is a grid, and the traced
/// cities say a grid is straight: half of Odesa's straight road length stands inside one five-degree band.
/// </param>
/// <param name="BlockAlongM">How far apart its streets stand along its own bearing.</param>
/// <param name="BlockAcrossM">And across it, which is the longer of the two: a block is a rectangle.</param>
internal readonly record struct District(
    int Sector, bool Inside, float BearingRad, float BlockAlongM, float BlockAcrossM, bool Strict)
{
    /// <summary>The tighter of its two spacings, which is what a street's own wander is bounded by.</summary>
    public float BlockTightestM => MathF.Min(BlockAlongM, BlockAcrossM);
}

/// <summary>
/// <b>Where the districts are, and what each is like.</b> A town is a wheel — a hub, its spokes and one
/// orbital — and a district is one of the pieces that arrangement cuts the ground into.
/// </summary>
/// <remarks>
/// <para>
/// <b>The regions are convex, and that is the whole reason for them.</b> A sector inside the orbital is a
/// disc cut by two half-planes, and a street is laid between two lattice points of one district — so a
/// street cannot leave its own district, and two districts' streets cannot cross. It is what keeps the
/// number of roads that have to be unpicked afterwards (GEN-17,
/// <see cref="TownLayout.UnpickTheCrossings"/>) down to the handful a town loses rather than the search a
/// generator that laid its streets anywhere would have to do. The sectors outside the orbital are the one
/// region that is not convex, and they are the reason the lattice still tests an edge against the ring
/// itself (<see cref="Lattice"/>).
/// </para>
/// <para>
/// <b>The bearing belongs to the district and not to the node</b>: a grid is bearing coherence, and a
/// bearing drawn per node produces noise wearing a grid's clothes.
/// </para>
/// </remarks>
internal sealed class Districts
{
    /// <summary>How few and how many spokes a wheel is laid with. Three is the fewest that makes sectors; beyond five the hub is a roundabout nobody laid.</summary>
    const int SpokesFewest = 3;

    const int SpokesMost = 5;

    readonly District[] _districts;
    readonly float _firstSpokeRad;

    Districts(District[] districts, int spokes, Vector2 hubM, float ringRadiusM, float firstSpokeRad)
    {
        _districts = districts;
        Spokes = spokes;
        HubM = hubM;
        RingRadiusM = ringRadiusM;
        _firstSpokeRad = firstSpokeRad;
    }

    public int Spokes { get; }

    public Vector2 HubM { get; }

    /// <summary>How far out the orbital stands, or zero where the brief lays none.</summary>
    public float RingRadiusM { get; }

    public bool HasRing => RingRadiusM > 0f;

    public int Count => _districts.Length;

    public District this[int district] => _districts[district];

    /// <summary>The bearing a spoke runs out on. The first is drawn, so a town is not always laid to the compass.</summary>
    public float SpokeBearingRad(int spoke) => _firstSpokeRad + (spoke * MathF.Tau / Spokes);

    public static Districts Lay(
        TownBrief brief, SimConfig config, GenRaster raster, TerrainStage.Water water, ref Rng draw)
    {
        var extentM = new Vector2(brief.WidthM, brief.HeightM);
        var spokes = Math.Clamp((brief.Districts + 1) / 2, SpokesFewest, SpokesMost);
        var ringRadiusM = brief.RingShare > 0f ? MathF.Min(extentM.X, extentM.Y) * brief.RingShare : 0f;
        var hubM = DryHubM(
            extentM * 0.5f, raster, water, config.CityGen.BlockSpacingAlongMaxM, out var inlandM);

        var townBearingRad = draw.NextFloat(0f, MathF.PI * 0.5f);
        var spreadRad = brief.BearingSpreadDeg * MathF.PI / 180f;
        var strictOf = StrictSectors(spokes * 2, brief.GridDistrictShare, ref draw);

        var districts = new District[spokes * 2];
        for (var district = 0; district < districts.Length; district++)
        {
            // <b>The middle of a town is finer than its edge.</b> A district inside the orbital draws from
            // the tighter half of the range and one outside it from the wider half, which is what the traced
            // cities are and what keeps the ground inside the ring from being one block wide.
            var inside = district < spokes;
            var middlingM = (config.CityGen.BlockSpacingAlongMinM + config.CityGen.BlockSpacingAlongMaxM) * 0.5f;
            var alongM = inside
                ? draw.NextFloat(config.CityGen.BlockSpacingAlongMinM, middlingM)
                : draw.NextFloat(middlingM, config.CityGen.BlockSpacingAlongMaxM);
            districts[district] = new District(
                district % spokes,
                inside,
                townBearingRad + draw.NextFloat(-spreadRad, spreadRad),
                alongM,
                alongM * draw.NextFloat(config.CityGen.BlockAspectMin, config.CityGen.BlockAspectMax),
                strictOf[district]);
        }

        // <b>A river town is turned so that one spoke runs down the river's own normal</b> (GEN-14b). The
        // crossing a town on two banks cannot do without is then the shortest line over the water there is,
        // and it is the wheel's rotation that buys it rather than any node moved afterwards.
        var firstSpokeRad = water.Bridgeable
            ? MathF.Atan2(-inlandM.Y, -inlandM.X)
            : draw.NextFloat(0f, MathF.Tau / spokes);

        return new Districts(districts, spokes, hubM, ringRadiusM, firstSpokeRad);
    }

    /// <summary>
    /// <b>The middle of the town, moved off the water it fell in</b> (GEN-14). A river runs through the
    /// centre of the map, so a hub left there is a junction in the river and every spoke out of it starts
    /// wet. It stands a block back from the bank instead, on whichever side the ground comes back first.
    /// </summary>
    /// <param name="inlandM">The way it moved, which is the way the water lies from it.</param>
    static Vector2 DryHubM(
        Vector2 atM, GenRaster raster, TerrainStage.Water water, float clearanceM, out Vector2 inlandM)
    {
        inlandM = water.Any ? water.Across(atM) : Vector2.UnitX;
        if (raster.At(atM) != Ground.Water) return atM;

        // Both banks are walked out from together, so the hub crosses the narrower half of the water rather
        // than whichever side the shore normal happened to point at.
        var reachM = MathF.Min(raster.Width, raster.Height) * raster.CellSizeM * 0.5f;
        for (var offM = raster.CellSizeM; offM < reachM; offM += raster.CellSizeM)
        {
            if (raster.At(atM + (inlandM * offM)) != Ground.Water) return atM + (inlandM * (offM + clearanceM));

            if (raster.At(atM - (inlandM * offM)) != Ground.Water)
            {
                inlandM = -inlandM;
                return atM + (inlandM * (offM + clearanceM));
            }
        }

        return atM;
    }

    /// <summary>Which district a point stands in, or −1 where it is outside the town's own ground.</summary>
    public int At(Vector2 pointM)
    {
        var offsetM = pointM - HubM;
        var radiusM = offsetM.Length();
        var sector = SectorAt(offsetM);
        var inside = !HasRing || radiusM <= RingRadiusM;
        for (var district = 0; district < _districts.Length; district++)
        {
            if (_districts[district].Sector == sector && _districts[district].Inside == inside) return district;
        }

        return -1;
    }

    int SectorAt(Vector2 offsetM)
    {
        var angle = MathF.Atan2(offsetM.Y, offsetM.X) - _firstSpokeRad;
        angle -= MathF.Tau * MathF.Floor(angle / MathF.Tau);
        return (int)(angle / (MathF.Tau / Spokes)) % Spokes;
    }

    /// <summary>The town's own centre, and the bearing each spoke leaves it on, as a direction.</summary>
    public Vector2 SpokeUnit(int spoke) => Heading.Unit(SpokeBearingRad(spoke));

    static bool[] StrictSectors(int count, float share, ref Rng draw)
    {
        var strict = new bool[count];
        var strictly = (int)MathF.Round(count * share);
        for (var district = 0; district < count; district++) strict[district] = district < strictly;

        // Drawn rather than taken in order, so which districts are grids is a fact about the seed and not
        // about the sector numbering.
        for (var district = count - 1; district > 0; district--)
        {
            var other = draw.NextInt(district + 1);
            (strict[district], strict[other]) = (strict[other], strict[district]);
        }

        return strict;
    }
}
