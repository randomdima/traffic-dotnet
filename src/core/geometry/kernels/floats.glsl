// NaN and infinity asked of a float's bits and made at run time, for every kernel. Vulkan keeps neither unless a
// kernel asks for it and a driver may fold `isnan` away; WGSL has no `isinf` or `isnan` at all, and refuses a NaN that
// is a constant, or an infinity — so either is the bits of one with the invocation's index masked out, which no compiler
// takes for a constant. Compute kernels only. Included by whatever needs it, as often as it likes.

#ifndef FLOATS_GLSL
#define FLOATS_GLSL

bool floatIsNaN(float x) { return (floatBitsToUint(x) & 0x7fffffffu) > 0x7f800000u; }

bool floatIsInfinite(float x) { return (floatBitsToUint(x) & 0x7fffffffu) == 0x7f800000u; }

// Neither NaN nor infinite.
bool floatIsFinite(float x) { return (floatBitsToUint(x) & 0x7f800000u) != 0x7f800000u; }

// The float of these bits, made at run time: naga writes a constant of the greatest float as a decimal past it, which
// WGSL refuses too.
float floatOfBits(uint bits) { return uintBitsToFloat(bits | (gl_LocalInvocationIndex & 0u)); }

float floatNaN() { return floatOfBits(0x7fc00000u); }

float floatInfinity() { return floatOfBits(0x7f800000u); }

float floatGreatest() { return floatOfBits(0x7f7fffffu); }

#endif
