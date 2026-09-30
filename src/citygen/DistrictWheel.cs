using System.Numerics;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>Which district of a town a point stands in</b> (GEN-56): the wheel its districts were laid on — a hub,
/// the spokes out of it and the orbital round it — carried with the plan so the town it was laid into can
/// still be asked. A district is a sector between two spokes, inside the orbital or outside it.
/// </summary>
/// <remarks>
/// <b>A district is numbered the way the generator laid it</b>: the sectors inside the orbital first, then
/// those outside it. A town with no orbital is its inside sectors alone, and a map not laid on a wheel — a
/// scenario, a map laid in code — is one district (<see cref="Whole"/>).
/// </remarks>
internal readonly record struct DistrictWheel(Vector2 HubM, float RingRadiusM, float FirstSpokeRad, int Spokes)
{
    /// <summary>A town that is one district, which is every map not laid on a wheel.</summary>
    public static DistrictWheel Whole => new(Vector2.Zero, 0f, 0f, 1);

    public bool HasRing => RingRadiusM > 0f;

    public int Count => HasRing ? Spokes * 2 : Spokes;

    public int At(Vector2 pointM)
    {
        var offsetM = pointM - HubM;
        var angle = MathF.Atan2(offsetM.Y, offsetM.X) - FirstSpokeRad;
        angle -= MathF.Tau * MathF.Floor(angle / MathF.Tau);
        var sector = (int)(angle / (MathF.Tau / Spokes)) % Spokes;
        var inside = !HasRing || offsetM.Length() <= RingRadiusM;
        return inside ? sector : Spokes + sector;
    }
}
