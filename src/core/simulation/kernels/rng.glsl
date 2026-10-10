// One agent's own stream of numbers on the device (core/simulation/Rng): PCG32, line for line with the host's, so a
// draw a kernel takes from a stream laid off the host is the draw the host would have taken. A stream is an lvalue in a
// table (town_cars.glsl's cars_Draw, town_people.glsl's people_Draw) handed to these as inout.

#ifndef RNG_GLSL
#define RNG_GLSL

#extension GL_EXT_shader_explicit_arithmetic_types_int64 : require

// Rng, field for field: its state, and the odd increment that names the stream.
struct Rng
{
    uint64_t state;
    uint64_t increment;
};

const uint64_t RngMultiplier = 6364136223846793005UL;

// The bits of a draw a float in [0, 1) is made of, and its step (Rng.NextFloat).
const uint RngFloatShift = 8u;
const float RngFloatStep = 1.0 / 16777216.0;

uint rngNext(inout Rng rng)
{
    uint64_t previous = rng.state;
    rng.state = previous * RngMultiplier + rng.increment;
    uint xorshifted = uint(((previous >> 18) ^ previous) >> 27);
    uint rotation = uint(previous >> 59);
    return (xorshifted >> rotation) | (xorshifted << (uint(-int(rotation)) & 31u));
}

// A stream seeded as the host's constructor seeds one.
Rng rngOf(uint64_t seed, uint64_t stream)
{
    Rng rng = Rng(0UL, (stream << 1) | 1UL);
    rngNext(rng);
    rng.state += seed;
    rngNext(rng);
    return rng;
}

// Uniform in [0, 1).
float rngNextFloat(inout Rng rng)
{
    return float(rngNext(rng) >> RngFloatShift) * RngFloatStep;
}

// Uniform in [least, most).
float rngNextFloat(inout Rng rng, float least, float most)
{
    precise float drawn = least + (most - least) * rngNextFloat(rng);
    return drawn;
}

// Uniform in [0, bound); a bound of zero or less answers zero and draws nothing.
int rngNextInt(inout Rng rng, int bound)
{
    if (bound <= 0) return 0;

    return int(rngNext(rng) % uint(bound));
}

uint rngNextUint(inout Rng rng) { return rngNext(rng); }

#endif
