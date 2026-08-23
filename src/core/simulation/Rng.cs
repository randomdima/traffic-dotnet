namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// One agent's own stream of numbers. PCG32 rather than <see cref="System.Random"/> for two
/// reproducibility reasons: it is value-typed, so five hundred streams live in one array and drawing
/// from one allocates nothing; and its state is two <c>ulong</c>s, so a run can be resumed at a tick
/// rather than only replayed from the start.
/// </summary>
internal struct Rng
{
    const ulong Multiplier = 6364136223846793005ul;

    ulong _state;
    readonly ulong _increment;

    public Rng(ulong seed, ulong stream)
    {
        _increment = (stream << 1) | 1ul;
        _state = 0ul;
        Next();
        _state += seed;
        Next();
    }

    /// <summary>Uniform in [0, 1).</summary>
    public float NextFloat() => (Next() >> 8) * (1f / (1 << 24));

    /// <summary>Uniform in [min, max).</summary>
    public float NextFloat(float min, float max) => min + (max - min) * NextFloat();

    /// <summary>Uniform in [0, bound). A zero or negative bound answers zero rather than throwing.</summary>
    public int NextInt(int bound) => bound <= 0 ? 0 : (int)(NextUint() % (uint)bound);

    public uint NextUint() => Next();

    uint Next()
    {
        var previous = _state;
        _state = previous * Multiplier + _increment;
        var xorshifted = (uint)(((previous >> 18) ^ previous) >> 27);
        var rotation = (int)(previous >> 59);
        return (xorshifted >> rotation) | (xorshifted << ((-rotation) & 31));
    }
}
