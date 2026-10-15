// A 32-bit by 32-bit product's 64 bits, as a high and a low word, in 32-bit arithmetic alone: WGSL has neither 64-bit
// integers nor a widening multiply, and every machine multiplies 16-bit halves exactly.
// Included by whatever needs it, as often as it likes.

#ifndef WIDE_GLSL
#define WIDE_GLSL

void wideProduct(uint a, uint b, out uint high, out uint low)
{
    uint aLow = a & 0xffffu;
    uint aHigh = a >> 16;
    uint bLow = b & 0xffffu;
    uint bHigh = b >> 16;

    uint lowLow = aLow * bLow;
    uint lowHigh = aLow * bHigh;
    uint highLow = aHigh * bLow;
    uint highHigh = aHigh * bHigh;

    uint middle = (lowLow >> 16) + (lowHigh & 0xffffu) + (highLow & 0xffffu);
    low = (middle << 16) | (lowLow & 0xffffu);
    high = highHigh + (lowHigh >> 16) + (highLow >> 16) + (middle >> 16);
}

// Whether the 64-bit (aHigh, aLow) is below, equal to or above (bHigh, bLow): -1, 0 or 1.
int wideCompare(uint aHigh, uint aLow, uint bHigh, uint bLow)
{
    if (aHigh != bHigh) return aHigh < bHigh ? -1 : 1;
    if (aLow != bLow) return aLow < bLow ? -1 : 1;
    return 0;
}

// (high, low) shifted left by a count below 64.
void wideShiftLeft(inout uint high, inout uint low, uint count)
{
    if (count == 0u) return;
    if (count >= 32u)
    {
        high = low << (count - 32u);
        low = 0u;
        return;
    }

    high = (high << count) | (low >> (32u - count));
    low <<= count;
}

#endif
