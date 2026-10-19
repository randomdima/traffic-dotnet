// The engine layers' common words (app/debug/EngineLayers.cs): the frame as the host wrote it, what is fixed when they
// are laid, and the quads they write — app/screen's OverlayQuad, drawn by the overlay pipeline in three runs, the ground's,
// the level above's and the run over the bodies, each as many as its draw says.
//
// The includer names three bindings before including this (ENGINE_FRAME, ENGINE_FIGURES, ENGINE_QUADS and ENGINE_COUNTS).

#ifndef ENGINE_GLSL
#define ENGINE_GLSL

// The frame (EngineLayers.FrameRow), field for field.
layout(std430, binding = ENGINE_FRAME) readonly buffer Frame
{
    vec2 viewCentreM;
    vec2 viewSpanM;
    vec2 pointerM;
    vec2 solidUv;
    float hairlineM;
    float wayLineM;
    float agentLineM;
    float pitchM;
    uint viewSlot;
    uint bodyCount;
    uint linkCount;
    uint arcCount;
    uint claimCount;
    uint intentCount;
    uint runRoom;
    uint groundChannels;
    uint pinnedPlusOne;
} frame;

// What is fixed when the layers are laid (EngineLayers.Figures): a colour a collision layer, a colour a kind of way, the
// holders' hues, a wash a rung, and the few the layers share.
const uint MostLayers = 8u;
const uint MostWayKinds = 8u;
const uint Hues = 12u;
const uint MostRungs = 16u;

layout(std430, binding = ENGINE_FIGURES) readonly buffer Figures
{
    vec4 layerColours[MostLayers];
    vec4 wayColours[MostWayKinds];
    vec4 hues[Hues];
    float rungWash[MostRungs];
    vec4 linkColour;
    vec4 obstruction;
    vec4 casing;
    uint enabledBit;
    uint staticBit;
    uint rosterBits;
    float edgeOverWash;
} figures;

// The quads, fifteen words each (OverlayQuad is sixty bytes, which std430 would pad a struct of to sixty-four), three
// runs of runRoom each; and how many each run has reserved, which may run past its room.
layout(std430, binding = ENGINE_QUADS) writeonly buffer Quads { float quadWords[]; };
layout(std430, binding = ENGINE_COUNTS) buffer Counts { uint counts[]; };

const uint GroundRun = 0u;
const uint AboveRun = 1u;
const uint OverRun = 2u;
const uint QuadWords = 15u;

// Room for n quads in one run, or ~0u where the run has none left: what a thread writes is all of its quads or none.
uint reserve(uint run, uint n)
{
    uint first = atomicAdd(counts[run], n);
    return first + n <= frame.runRoom ? run * frame.runRoom + first : ~0u;
}

void quad(uint at, vec2 centre, vec2 halfSize, vec4 colour, float rotation, float taper)
{
    uint w = at * QuadWords;
    quadWords[w + 0u] = centre.x;
    quadWords[w + 1u] = centre.y;
    quadWords[w + 2u] = halfSize.x;
    quadWords[w + 3u] = halfSize.y;
    quadWords[w + 4u] = frame.solidUv.x;
    quadWords[w + 5u] = frame.solidUv.y;
    quadWords[w + 6u] = 0.0;
    quadWords[w + 7u] = 0.0;
    quadWords[w + 8u] = colour.x;
    quadWords[w + 9u] = colour.y;
    quadWords[w + 10u] = colour.z;
    quadWords[w + 11u] = colour.w;
    quadWords[w + 12u] = rotation;
    quadWords[w + 13u] = uintBitsToFloat(0u);
    quadWords[w + 14u] = taper;
}

// A line in the town's metres (ScreenDraw.LineM); nothing where it has no length, which a reserved slot still has to be,
// so it is written as a quad of no size.
void bar(uint at, vec2 fromM, vec2 toM, float widthM, vec4 colour)
{
    vec2 along = toM - fromM;
    float lengthM = length(along);
    quad(at, (fromM + toM) * 0.5, vec2(lengthM * 0.5, lengthM > 0.0 ? widthM * 0.5 : 0.0), colour, atan(along.y, along.x), 0.0);
}

// One piece of a band down a bending line (ScreenDraw.BandM): both ends cut square to the line, so the next piece shares
// its edge.
void band(uint at, vec2 fromM, vec2 toM, float turnRad, float widthM, vec4 colour)
{
    vec2 along = toM - fromM;
    float lengthM = length(along);
    float halfM = widthM * 0.5;
    float taper = clamp(halfM * sin(turnRad * 0.5), -lengthM * 0.5, lengthM * 0.5);
    quad(at, (fromM + toM) * 0.5, vec2(lengthM * 0.5, lengthM > 0.0 ? halfM * cos(turnRad * 0.5) : 0.0), colour, atan(along.y, along.x), taper);
}

// OBS-2b's cull: whether a place is inside the view once what stands there has been allowed its reach.
bool onScreen(vec2 atM, float reachM)
{
    vec2 off = abs(atM - frame.viewCentreM);
    return off.x <= frame.viewSpanM.x * 0.5 + reachM && off.y <= frame.viewSpanM.y * 0.5 + reachM;
}

// A holder's colour, off its key (LaneOccupancy.KeyOf): its own line's (Theme.AgentLine), by its index in its roster, so a
// claim, the line running out of it and whatever a driver's own layer draws for it are one colour; furniture holds as
// nobody.
vec4 holderColour(int key)
{
    if (key < 0) return figures.obstruction;
    return figures.hues[(uint(key) >> figures.rosterBits) % Hues];
}

vec2 turned(vec2 v, vec2 rotation) { return vec2(v.x * rotation.x - v.y * rotation.y, v.x * rotation.y + v.y * rotation.x); }

#endif
