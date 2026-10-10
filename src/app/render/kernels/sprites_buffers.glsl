// The one binding layout both passes of the emit run over (sprites_count, sprites_write), so one set serves them, and
// sprites.glsl over it. Every row is app/devicetick/DeviceSprites' of the same name, field for field. The including
// kernel enables doubles, which rounded.glsl answers its quotients in.

#include "app/render/kernels/sprites_car.glsl"
#include "app/render/kernels/sprites_person.glsl"

// The frame's own words, written by the host each frame into a buffer rather than pushed, because a frame is recorded
// once (CameraView): the view as the fill takes it, the narrowest part drawn, the town's clock and its live rosters.
layout(std430, binding = 0) readonly buffer Frame
{
    vec2 viewCentreM;
    vec2 viewSpanM;
    float leastPartM;
    float elapsedS;
    uint personCount;
    uint carCount;
    uint handCount;
    int alsoDrivenCar;
} frame;

// What is fixed when the town is laid: the rosters' room, the runs' room, the sheet slots and the figures.
layout(std430, binding = 1) readonly buffer Figures
{
    uint personCapacity;
    uint carCapacity;
    uint headCount;
    uint levelled;
    uint roomOver;
    uint roomAbove;
    uint mostLenses;
    uint personSheets;
    uint firstDownSheet;
    uint carSheets;
    uint firstCarSheet;
    uint rubberSheet;
    uint lensSheet;
    uint glowSheet;
    uint firstHeadSheet;
    uint groundLevel;
    uint aboveLevel;
    uint laneCount;
    uint crossingCount;
    float widestTyreM;
    float widestGlowM;
    float facingFromRad;
    float facingStepRad;
    float personCellU;
    float personCellV;
    float lampBrakeMps2;
    float stepBackLeadS;
    float junctionAheadM;
    float indicateRad;
    float flashHz;
    float flashOnShare;
    float beaconHz;
    float halfLensCellM;
    float lensCellU;
    float lensCellV;
    float carHeadHalfX;
    float carHeadHalfY;
    float walkHeadHalfX;
    float walkHeadHalfY;
    float headReachM;
    float carHeadRedU;
    float carHeadAmberU;
    float carHeadGreenU;
    float carHeadCellU;
    float walkHeadRedU;
    float walkHeadAmberU;
    float walkHeadGreenU;
    float walkHeadCellU;
    float cycleS;
    float amberTailS;
} figures;

// One walker's look (DeviceSprites.PersonLook): its quad standing and lying down, and how far the latter reaches.
struct PersonLook
{
    vec2 standingHalfM;
    vec2 downHalfM;
    float downReachM;
    float heightM;
};

// One car's look (DeviceSprites.CarLook): every part's quad and reach, its lenses' count and its arm's two pictures.
struct CarLook
{
    vec2 bodyHalfM;
    vec2 wreckHalfM;
    float bodyReachM;
    float wreckReachM;
    vec2 tyreHalfM;
    vec2 firstSteeredM;
    vec2 secondSteeredM;
    float tyreReachM;
    float wheelWidthM;
    float lampReachM;
    uint lensCount;
    float towGripM;
    uint hasBeam;
    vec2 pivotM;
    vec2 collapsedHalfM;
    vec2 extendedHalfM;
    float collapsedHingeAtM;
    float extendedHingeAtM;
    float collapsedReachM;
    float extendedReachM;
    uint collapsedSheet;
    uint extendedSheet;
};

// One lens of a look (DeviceSprites.LensRow): where it is on the body, its two cells, its glow, and its fitting with
// the colour its first cell burns in the next byte.
struct LensRow
{
    vec2 atBodyM;
    vec2 firstCellUv;
    vec2 secondCellUv;
    float glowM;
    uint fittingAndFirstColour;
};


// One signal head (DeviceSprites.HeadRow), in the order its rows file it.
struct HeadRow
{
    vec2 centreM;
    float headingRad;
    int subject;
    uint forCars;
    uint pad;
};

// One instance as the sprite pipeline reads it (SpriteInstance): fifty-six bytes, the tint as two pairs so the row
// packs to eight and not to sixteen.
struct SpriteOut
{
    vec2 centreM;
    vec2 halfSizeM;
    vec2 uvMin;
    vec2 uvSize;
    vec2 tintRG;
    vec2 tintBA;
    uint sheet;
    float headingRad;
};

// VkDrawIndirectCommand.
struct SpriteDraw
{
    uint vertexCount;
    uint instanceCount;
    uint firstVertex;
    uint firstInstance;
};

layout(std430, binding = 2) readonly buffer PersonLooks { PersonLook personLooks[]; };
layout(std430, binding = 3) readonly buffer CarLooks { CarLook carLooks[]; };
layout(std430, binding = 4) readonly buffer Lenses { LensRow lenses[]; };
layout(std430, binding = 5) readonly buffer LampTints { vec4 lampTints[]; };
layout(std430, binding = 6) readonly buffer People { PersonRow people[]; };
layout(std430, binding = 7) readonly buffer Cars { CarRow cars[]; };
layout(std430, binding = 8) readonly buffer Hands { uint hands[]; };
layout(std430, binding = 9) readonly buffer Heads { HeadRow heads[]; };
layout(std430, binding = 10) readonly buffer LaneAxis { int laneAxis[]; };
layout(std430, binding = 11) readonly buffer LaneJunction { int laneJunction[]; };
layout(std430, binding = 12) readonly buffer Offsets { float offsetS[]; };
layout(std430, binding = 13) readonly buffer CrossingAxis { int crossingAxis[]; };
layout(std430, binding = 14) readonly buffer CrossingJunction { int crossingJunction[]; };
layout(std430, binding = 15) buffer Starts { uint starts[]; };
layout(std430, binding = 16) readonly buffer Totals { uint totals[]; };
layout(std430, binding = 17) buffer LampsShown { uvec2 lampsShown[]; };
layout(std430, binding = 18) writeonly buffer Instances { SpriteOut instances[]; };
layout(std430, binding = 19) writeonly buffer Draws { SpriteDraw draws[]; };

#define LAMP_FIGURES figures
#define SIGNAL_FIGURES figures
#define SIGNAL_LANE_AXIS laneAxis
#define SIGNAL_LANE_JUNCTION laneJunction
#define SIGNAL_OFFSETS offsetS
#define SIGNAL_CROSSING_AXIS crossingAxis
#define SIGNAL_CROSSING_JUNCTION crossingJunction

#include "core/geometry/kernels/rounded.glsl"
#include "agents/car/body/kernels/lamps.glsl"
#include "agents/person/body/kernels/look.glsl"
#include "agents/trafficlight/control/kernels/signal.glsl"
#include "app/render/kernels/sprites.glsl"
