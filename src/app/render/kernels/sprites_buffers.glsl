// The one binding layout both passes of the emit run over (sprites_count, sprites_write), so one set serves them, and
// sprites.glsl over it. Every row is app/devicetick/DeviceSprites' of the same name, field for field. The including
// kernel enables doubles, which rounded.glsl answers its quotients in.

#include "app/render/kernels/sprites_car.glsl"
#include "app/render/kernels/sprites_person.glsl"
#include "world/town/kernels/world_rows.glsl"

// The frame's own words, written by the host each frame into a buffer rather than pushed, because a frame is recorded
// once (CameraView): the view as the fill takes it, the narrowest part drawn, the town's clock, its live rosters, and
// which of the world's two views the host is showing (sprites_world.comp).
layout(std430, binding = 0) readonly buffer Frame
{
    vec2 viewCentreM;
    vec2 viewSpanM;
    float leastPartM;
    float elapsedS;
    uint personCount;
    uint carCount;
    uint viewSlot;
    uint pad;
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
    uint personColumns;
    uint personRows;
    float flashHz;
    float flashOnShare;
    float beaconHz;
    float halfLensCellM;
    float carHeadHalfX;
    float carHeadHalfY;
    float walkHeadHalfX;
    float walkHeadHalfY;
    float headReachM;
    uint carHeadRed;
    uint carHeadAmber;
    uint carHeadGreen;
    uint carHeadFrames;
    uint walkHeadRed;
    uint walkHeadAmber;
    uint walkHeadGreen;
    uint walkHeadFrames;
    float cycleS;
    float amberTailS;
    uint wagonCount;
    uint firstTrainSheet;
    uint gearCount;
    uint barrierSheet;
    uint barrierDriveSheet;
    uint crossingLightSheet;
    uint crossingLitSheet;
    uint lightCount;
    float barrierWidthM;
    float driveHalfLengthM;
    float driveHalfWidthM;
    float lightHalfLengthM;
    float lightHalfWidthM;
    uint bridged;
    uint bridgeChannel;
} figures;

// One approach's crossing gear (DeviceSprites.GearRow, CrossingGear): where its arm pivots, the way it lies lowered and on
// what heading, and how far any of it reaches past its pivot.
struct GearRow
{
    vec2 pivotM;
    vec2 downAlong;
    float armHeadingRad;
    float reachM;
};

// And what it shows this frame, as the host worked it out (CrossingSprites.ArmDrawn, CrossingGear.Lit): where its arm is
// drawn, pivot end to tip, and whether its lights are lit.
struct GearShownRow
{
    vec2 armFromM;
    vec2 armToM;
    uint lit;
    uint pad;
};

// One X light over a lane coming in (DeviceSprites.LightRow, CrossingLight): where, its quad's heading, and its gear.
struct LightRow
{
    vec2 centreM;
    float headingRad;
    uint gear;
};

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
    float unused;
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

// One lens of a look (DeviceSprites.LensRow): where it is on the body, its two cells (SpriteCell, packed), its glow, and
// its fitting with the colour its first cell burns in the next byte.
struct LensRow
{
    vec2 atBodyM;
    uint firstCell;
    uint secondCell;
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

// One instance as the sprite pipeline reads it (SpriteInstance): twenty-eight bytes, every member four, so the row is
// laid at four and not rounded up to a vec2's eight — the half-size as two halves, the cell as four bytes (SpriteCell),
// the tint as four (SpriteTint).
struct SpriteOut
{
    float centreX;
    float centreY;
    uint halfSize;
    float headingRad;
    uint cell;
    uint tint;
    uint sheet;
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
layout(std430, binding = 5) readonly buffer LampTints { uint lampTints[]; };
layout(std430, binding = 6) readonly buffer People { PersonRow people[]; };
layout(std430, binding = 7) readonly buffer Cars { CarRow cars[]; };
layout(std430, binding = 8) readonly buffer Heads { HeadRow heads[]; };
layout(std430, binding = 9) readonly buffer LaneAxis { int laneAxis[]; };
layout(std430, binding = 10) readonly buffer LaneJunction { int laneJunction[]; };
layout(std430, binding = 11) readonly buffer Offsets { float offsetS[]; };
layout(std430, binding = 12) readonly buffer CrossingAxis { int crossingAxis[]; };
layout(std430, binding = 13) readonly buffer CrossingJunction { int crossingJunction[]; };
layout(std430, binding = 14) buffer Starts { uint starts[]; };
layout(std430, binding = 15) readonly buffer Totals { uint totals[]; };
layout(std430, binding = 16) buffer LampsShown { uvec2 lampsShown[]; };
layout(std430, binding = 17) writeonly buffer Instances { SpriteOut instances[]; };
layout(std430, binding = 18) writeonly buffer Draws { SpriteDraw draws[]; };

// The rolling stock's half-sizes by vehicle (DeviceSprites.TrainLooks), and the world's two views of the wagons (the
// host showing the frame's slot), which every wagon is drawn off.
layout(std430, binding = 19) readonly buffer TrainLooks { vec2 trainLooks[]; };
layout(std430, binding = 20) readonly buffer WagonViews0 { WagonViewRow wagonViews0[]; };
layout(std430, binding = 21) readonly buffer WagonViews1 { WagonViewRow wagonViews1[]; };

// The crossings' gear, laid once, what each shows this frame, and their X lights, laid once (RAIL-7).
layout(std430, binding = 22) readonly buffer GearRows { GearRow gearRows[]; };
layout(std430, binding = 23) readonly buffer GearShown { GearShownRow gearShown[]; };
layout(std430, binding = 24) readonly buffer LightRows { LightRow lightRows[]; };

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
