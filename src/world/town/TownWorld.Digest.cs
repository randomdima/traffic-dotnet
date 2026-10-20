namespace TrafficSimulation.World.Town;

internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>Every body's pose and motion as one number another process, build or head agrees on</b> — the cars' places,
    /// headings and velocities and the walkers' places, folded bit for bit (FNV-1a, not <c>HashCode</c>, whose seed is
    /// drawn per process). Equal on two heads at one tick of one plan is the same town (WEB-1).
    /// </summary>
    public ulong Digest()
    {
        var hash = 14695981039346656037UL;
        for (var car = 0; car < Cars.Count; car++)
        {
            Mix(ref hash, Cars.PositionM[car].X);
            Mix(ref hash, Cars.PositionM[car].Y);
            Mix(ref hash, Cars.HeadingRad[car]);
            Mix(ref hash, Cars.VelocityMps[car].X);
            Mix(ref hash, Cars.VelocityMps[car].Y);
        }

        for (var person = 0; person < People.Count; person++)
        {
            Mix(ref hash, People.PositionM[person].X);
            Mix(ref hash, People.PositionM[person].Y);
        }

        return hash;

        static void Mix(ref ulong into, float value)
        {
            into ^= (uint)BitConverter.SingleToInt32Bits(value);
            into *= 1099511628211UL;
        }
    }
}
