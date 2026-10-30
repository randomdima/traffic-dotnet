// The one binding layout the passes of the emit run over (sprites_count, sprites_write, sprites_order), so one set serves them, and
// sprites.glsl over it. Every row is app/devicetick/DeviceSprites' of the same name, field for field. Sixteen storage
// buffers and no more, which is all a page gives a kernel (WEB-12): the figures ride behind the frame's words, and every
// table laid once is one run of words.

#include "app/render/kernels/sprites_car.glsl"
#include "app/render/kernels/sprites_person.glsl"
#include "app/render/kernels/smoke.glsl"
#include "world/town/kernels/world_rows.glsl"

// The air at a moment back from the frame's (AirBack, Wind.Back): how far it has carried since what it held then, and its
// velocity then; DeviceSprites.AirMomentCount of them, figures.perAirStepS apart.
struct AirBack
{
    vec2 sinceM;
    vec2 mps;
};

const uint AirMoments = 32u;

// The frame's own words, written by the host each frame into a buffer rather than pushed, because a frame is recorded
// once (CameraView): the view as the fill takes it, the narrowest part drawn, the town's clock, its live rosters, which
// of the world's two views the host is showing (sprites_world.comp), how slow a walker is drawn standing, and the air of
// the moments back from the town's, which carries the smoke.
struct SpriteFrame
{
    vec2 viewCentreM;
    vec2 viewSpanM;
    float leastPartM;
    float elapsedS;
    uint personCount;
    uint carCount;
    uint viewSlot;
    float standingBelowMps;
    AirBack air[AirMoments];
};

// One kind of pipe's smoke (ExhaustLook): each pair idling and at full throttle, how its body's track is laid, and what
// those come to at their most.
struct SmokeKind
{
    float idleOpacity;
    float fullOpacity;
    float idleBirthWidthM;
    float fullBirthWidthM;
    float idleLifeS;
    float fullLifeS;
    float idleOutflowMps;
    float fullOutflowMps;
    float spreadMps;
    float thickestAt;
    float puffGapM;
    float shade;
    float reachM;
    float widestM;
    float longestS;
    uint puffsEach;
    uint trackPoints;
    float perTrackEveryS;
    float perFullThrottle;
};

// What is fixed when the town is laid: the rosters' room, the runs' room, the sheet slots and the figures.
struct SpriteFigures
{
    uint personCapacity;
    uint carCapacity;
    uint headCount;
    uint levelled;
    uint roomOver;
    uint roomAbove;
    uint mostLenses;
    uint personSheets;
    // SpriteTint: what a casualty's stance is multiplied by (Theme.Casualty).
    uint casualtyTint;
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
    // How high a body's tilt on its springs is seen from (ViewFigures.CarTiltEyeHeightM, CAR-56).
    float carTiltEyeHeightM;
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
    // The smoke (CAR-54, TRN-15, SmokeLook): its sheet and the tailpipes' stride, what every pipe shares, the rates divided
    // out, and each kind's own, its gap among them.
    uint smokeSheet;
    uint mostExhausts;
    float puffEveryS;
    float puffPerEveryS;
    uint puffSplits;
    float scatterMps;
    float dragS;
    SmokeKind tailpipe;
    SmokeKind stack;
    // The chimneys' smoke (OBJ-7, ChimneyLook): how many pots, then its figures, the rates divided out, a house's pot's.
    uint chimneyCount;
    uint chimneyPuffsEach;
    float chimneyEveryS;
    float chimneyPerEveryS;
    float chimneyLifeS;
    float chimneyRiseS;
    float chimneyPerPotWidthM;
    float chimneySpreadMps;
    float chimneyScatterMps;
    float chimneyDragS;
    float chimneyOpacity;
    float chimneyShade;
    float chimneyBreathShare;
    float chimneyPerBreathS;
    float chimneySmokingShare;
    float chimneyPerFireS;
    float widestChimneyPuffM;
    float chimneyReachM;
    // The rail vehicles' smoke (TRN-15): where their tracks start among the smoke's (DeviceSmoke.FirstWagon).
    uint firstWagonTrack;
    // One over how far apart the frame's moments of the air are (DeviceSprites.AirStepS).
    float perAirStepS;
};

// The frame's words, which the host copies in each frame, and the figures laid once behind them.
layout(std430, binding = 0) readonly buffer FrameAndFigures
{
    SpriteFrame frame;
    SpriteFigures figures;
};

// One vehicle of the rolling stock as drawn (DeviceSprites.TrainLookRow): its half-size and its exhaust stacks (TRN-15),
// from its middle with +x its front.
struct TrainLook
{
    vec2 halfM;
    uint exhaustCount;
    uint pad;
    vec2 exhaustsM[2];
};

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

// One walker's look (DeviceSprites.PersonLook): its quad, standing or down.
struct PersonLook
{
    vec2 halfM;
    float heightM;
};

// One car's look (DeviceSprites.CarLook): every part's quad and reach, its lenses' and its tailpipes' counts and its arm's
// two pictures.
struct CarLook
{
    vec2 bodyHalfM;
    vec2 wreckHalfM;
    float bodyReachM;
    float wreckReachM;
    vec2 tyreHalfM;
    // Where its wheels stand on the body, how many it stands on, and which steer a bit each (CarBuild.Wheels).
    vec2 wheelsM[8];
    uint wheelCount;
    uint steeredWheels;
    float tyreReachM;
    float wheelWidthM;
    float lampReachM;
    uint lensCount;
    uint exhaustCount;
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

// One pot of a chimney (ChimneyRow, OBJ-7), laid once: where it stands in the town, its key — its stack's fire above
// ChimneyPotBits, which pot of the stack below — and how wide the pot is there.
struct ChimneyRow
{
    vec2 atM;
    uint seed;
    float potWidthM;
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

// Every table laid once in one run of words (DeviceSprites.Tables), each table's first word named in the run's own head
// and each row its DeviceSprites row's words: the looks, the lenses and their tints, the heads and the signals' tables,
// the rolling stock, the crossings' gear and lights, the tailpipes and the chimneys' pots.
layout(std430, binding = 1) readonly buffer Tables { uint tables[]; };

const uint TablePersonLooks = 0u;
const uint TableCarLooks = 1u;
const uint TableLenses = 2u;
const uint TableLampTints = 3u;
const uint TableHeads = 4u;
const uint TableLaneAxis = 5u;
const uint TableLaneJunction = 6u;
const uint TableOffsets = 7u;
const uint TableCrossingAxis = 8u;
const uint TableCrossingJunction = 9u;
const uint TableTrainLooks = 10u;
const uint TableGearRows = 11u;
const uint TableLightRows = 12u;
const uint TableExhausts = 13u;
const uint TableChimneys = 14u;

layout(std430, binding = 2) readonly buffer People { PersonRow people[]; };
layout(std430, binding = 3) readonly buffer Cars { CarRow cars[]; };
layout(std430, binding = 4) buffer Starts { uint starts[]; };
layout(std430, binding = 5) readonly buffer Totals { uint totals[]; };
layout(std430, binding = 6) buffer LampsShown { uvec2 lampsShown[]; };
layout(std430, binding = 7) writeonly buffer Instances { SpriteOut instances[]; };
layout(std430, binding = 8) writeonly buffer Draws { SpriteDraw draws[]; };

// The world's two views of the wagons (the host showing the frame's slot), which every wagon is drawn off.
layout(std430, binding = 9) readonly buffer WagonViews0 { WagonViewRow wagonViews0[]; };
layout(std430, binding = 10) readonly buffer WagonViews1 { WagonViewRow wagonViews1[]; };

// What each approach's crossing gear shows this frame (RAIL-7).
layout(std430, binding = 11) readonly buffer GearShown { GearShownRow gearShown[]; };

// Every car's and rail vehicle's smoke's track as the step lays it (smoke_track.comp, CAR-54).
layout(std430, binding = 12) readonly buffer SmokeHeads { SmokeHead smokeHeads[]; };
layout(std430, binding = 13) readonly buffer SmokePoints { SmokePoint smokePoints[]; };

// Which units wear a ring this frame (SpriteRings, CTL-1, CTL-5d), written by the host: the car somebody else has the
// wheel of, then a bit a unit — the walkers' to personCapacity, the cars' to carCapacity, then the wagons'.
layout(std430, binding = 14) readonly buffer Rings
{
    int secondHandCar;
    uint ringBits[];
};

// The walkers shown, as the write pass builds them in roster order, for sprites_order.comp to put in the order drawn (PER-31).
layout(std430, binding = 15) buffer WalkersShown { SpriteOut walkersShown[]; };

uint spritesWord(uint table, uint at) { return tables[tables[table] + at]; }

float spritesFloat(uint table, uint at) { return uintBitsToFloat(spritesWord(table, at)); }

vec2 spritesPair(uint table, uint at) { return vec2(spritesFloat(table, at), spritesFloat(table, at + 1u)); }

PersonLook spritesPersonLook(uint variant)
{
    uint at = variant * 3u;
    return PersonLook(spritesPair(TablePersonLooks, at), spritesFloat(TablePersonLooks, at + 2u));
}

CarLook spritesCarLook(uint variant)
{
    uint at = variant * 44u;
    const uint t = TableCarLooks;
    vec2 wheelsM[8] = vec2[8](
        spritesPair(t, at + 8u), spritesPair(t, at + 10u), spritesPair(t, at + 12u), spritesPair(t, at + 14u),
        spritesPair(t, at + 16u), spritesPair(t, at + 18u), spritesPair(t, at + 20u), spritesPair(t, at + 22u));
    return CarLook(
        spritesPair(t, at), spritesPair(t, at + 2u), spritesFloat(t, at + 4u), spritesFloat(t, at + 5u), spritesPair(t, at + 6u),
        wheelsM, spritesWord(t, at + 24u), spritesWord(t, at + 25u), spritesFloat(t, at + 26u), spritesFloat(t, at + 27u),
        spritesFloat(t, at + 28u), spritesWord(t, at + 29u), spritesWord(t, at + 30u), spritesWord(t, at + 31u),
        spritesPair(t, at + 32u), spritesPair(t, at + 34u), spritesPair(t, at + 36u), spritesFloat(t, at + 38u),
        spritesFloat(t, at + 39u), spritesFloat(t, at + 40u), spritesFloat(t, at + 41u), spritesWord(t, at + 42u),
        spritesWord(t, at + 43u));
}

LensRow spritesLensRow(uint lens)
{
    uint at = lens * 6u;
    return LensRow(
        spritesPair(TableLenses, at), spritesWord(TableLenses, at + 2u), spritesWord(TableLenses, at + 3u),
        spritesFloat(TableLenses, at + 4u), spritesWord(TableLenses, at + 5u));
}

uint spritesLampTint(uint colour) { return spritesWord(TableLampTints, colour); }

HeadRow spritesHeadRow(uint head)
{
    uint at = head * 6u;
    return HeadRow(
        spritesPair(TableHeads, at), spritesFloat(TableHeads, at + 2u), int(spritesWord(TableHeads, at + 3u)),
        spritesWord(TableHeads, at + 4u), 0u);
}

int spritesLaneAxis(int lane) { return int(spritesWord(TableLaneAxis, uint(lane))); }

int spritesLaneJunction(int lane) { return int(spritesWord(TableLaneJunction, uint(lane))); }

float spritesOffsetS(int junction) { return spritesFloat(TableOffsets, uint(junction)); }

int spritesCrossingAxis(int crossing) { return int(spritesWord(TableCrossingAxis, uint(crossing))); }

int spritesCrossingJunction(int crossing) { return int(spritesWord(TableCrossingJunction, uint(crossing))); }

TrainLook spritesTrainLook(uint look)
{
    uint at = look * 8u;
    return TrainLook(
        spritesPair(TableTrainLooks, at), spritesWord(TableTrainLooks, at + 2u), 0u,
        vec2[2](spritesPair(TableTrainLooks, at + 4u), spritesPair(TableTrainLooks, at + 6u)));
}

GearRow spritesGearRow(uint gear)
{
    uint at = gear * 6u;
    return GearRow(
        spritesPair(TableGearRows, at), spritesPair(TableGearRows, at + 2u), spritesFloat(TableGearRows, at + 4u),
        spritesFloat(TableGearRows, at + 5u));
}

LightRow spritesLightRow(uint light)
{
    uint at = light * 4u;
    return LightRow(spritesPair(TableLightRows, at), spritesFloat(TableLightRows, at + 2u), spritesWord(TableLightRows, at + 3u));
}

vec2 spritesExhaust(uint at) { return spritesPair(TableExhausts, at * 2u); }

ChimneyRow spritesChimney(uint chimney)
{
    uint at = chimney * 4u;
    return ChimneyRow(spritesPair(TableChimneys, at), spritesWord(TableChimneys, at + 2u), spritesFloat(TableChimneys, at + 3u));
}

#define LAMP_FIGURES figures
#define SIGNAL_FIGURES figures
#define SIGNAL_LANE_AXIS(lane) spritesLaneAxis(lane)
#define SIGNAL_LANE_JUNCTION(lane) spritesLaneJunction(lane)
#define SIGNAL_OFFSETS(junction) spritesOffsetS(junction)
#define SIGNAL_CROSSING_AXIS(crossing) spritesCrossingAxis(crossing)
#define SIGNAL_CROSSING_JUNCTION(crossing) spritesCrossingJunction(crossing)

#include "core/geometry/kernels/rounded.glsl"
#include "agents/car/body/kernels/lamps.glsl"
#include "agents/person/body/kernels/look.glsl"
#include "agents/trafficlight/control/kernels/signal.glsl"
#include "app/render/kernels/sprites.glsl"
