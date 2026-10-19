// The frame's moving sprites on the device (TownSprites.FillMoving): the walkers, then every part of every car on the
// ground — the steered tyres, the bodies, the tow arms, the lamps — then the signal heads, and the cars on the level
// above after them, part by part again. Line for line with PersonSprites.Fill, CarSprites.FillFrontTyres, Fill and
// FillBeams, LampSprites.Fill and SignalSprites.Fill, `precise` wherever the host rounds; the arm's bearing is
// trig.glsl's arctangent, and every quotient and root a verdict hangs on is rounded.glsl's.
//
// One count a part of a unit, laid in the order the instances are drawn: the walkers', then each part of the
// cars a run apiece, then the heads', then the four car parts again for the level above. A count pass writes them, a
// scan makes them where each part's instances start, and a write pass lays every part there — so the order is the
// host's whatever order the threads ran in. Every unit is asked, not only those a grid says are near: the cull is the
// host's own, per part, and the rosters are walked in index order as TownWorld.CarsIn hands them over.
//
// sprites_buffers.glsl declares everything read here.

#include "core/geometry/kernels/trig.glsl"

// SpriteTint.Plain: the tint everything but a mark and a glow is drawn with.
const uint SpritePlain = 0xFFFFFFFFu;

// SpriteCell.Whole: the one cell of a grid of one.
const uint SpriteWhole = 0x01010000u;

// The corners of the quad every instance is drawn as, which is what each draw is told to draw of it.
const uint SpriteQuadCorners = 4u;

// The two runs a car can be drawn in (CarSprites.IsOn against TownSprites.FillMoving's two levels), and neither.
const uint RunOver = 0u;
const uint RunAbove = 1u;
const uint RunNone = 2u;

// A car's parts in the order a level draws them (TownSprites.FillCars), a run of slots each.
const uint PartTyres = 0u;
const uint PartBody = 1u;
const uint PartBeam = 2u;
const uint PartLamps = 3u;
const uint CarParts = 4u;

// CarSprites.FillBeams: a pair driven exactly on top of each other has no direction, under this square of a length.
const float BeamLeastSquareM2 = 1e-6;

// Where the scan keeps the grand total (DeviceSprites).
const uint TotalAt = 0u;

// The trains' wagons (TRN-7), a slot each between the cars' parts and the heads, as TownSprites.FillMoving draws them.
uint spritesWagonsFrom() { return figures.personCapacity + CarParts * figures.carCapacity; }

uint spritesHeadsFrom() { return spritesWagonsFrom() + figures.wagonCount; }

// The crossings' gear (RAIL-7), three slots an approach — its barrier, its head, its lit lamp — after the heads.
const uint GearParts = 3u;

uint spritesGearFrom() { return spritesHeadsFrom() + figures.headCount; }

uint spritesAboveFrom() { return spritesGearFrom() + GearParts * figures.gearCount; }

uint spritesSlotCount() { return spritesAboveFrom() + (figures.levelled != 0u ? CarParts * figures.carCapacity : 0u); }

uint spritesCarSlot(uint run, uint part, uint car)
{
    return (run == RunOver ? figures.personCapacity : spritesAboveFrom()) + part * figures.carCapacity + car;
}

// The run a car is drawn in: every car in the one run of a town with nothing above its ground, and otherwise the run of
// its level, or none for a level neither run draws.
uint spritesRunOf(CarRow row)
{
    if (figures.levelled == 0u) return RunOver;

    uint level = (row.flags >> CarLevelShift) & 0xFFu;
    if (level == figures.groundLevel) return RunOver;
    return level == figures.aboveLevel ? RunAbove : RunNone;
}

// The host's cull: a quad whose middle is further off the view's middle than half the view and its own reach, either way.
bool spritesOutside(vec2 centreM, vec2 reachM)
{
    precise vec2 halfView = frame.viewSpanM * 0.5;
    precise vec2 offset = centreM - frame.viewCentreM;
    precise vec2 edge = halfView + reachM;
    return abs(offset.x) > edge.x || abs(offset.y) > edge.y;
}

// A place on a body, in the body's own frame — +x the nose, +y its right — put in the town.
vec2 spritesOnBody(vec2 centreM, vec2 forward, vec2 atBodyM)
{
    vec2 rightOf = vec2(-forward.y, forward.x);
    precise vec2 along = forward * atBodyM.x;
    precise vec2 across = rightOf * atBodyM.y;
    precise vec2 atM = centreM + along + across;
    return atM;
}

// A float to the nearest half, ties to even, as the host's (Half) conversion rounds it — in integer arithmetic, because
// packHalf2x16's rounding is the driver's to choose.
uint spritesHalf(float value)
{
    uint bits = floatBitsToUint(value);
    uint sign = (bits >> 16) & 0x8000u;
    uint magnitude = bits & 0x7FFFFFFFu;

    // Past the largest finite half once rounded, or not a number: infinity, or a quiet NaN.
    if (magnitude >= 0x47800000u) return sign | (magnitude > 0x7F800000u ? 0x7E00u : 0x7C00u);

    // Under the least normal half: a subnormal, in units of 2^-24, or nought under half the least of them.
    if (magnitude < 0x38800000u)
    {
        uint exponent = magnitude >> 23;
        if (exponent < 102u) return sign;

        uint mantissa = (magnitude & 0x7FFFFFu) | 0x800000u;
        uint shift = 126u - exponent;
        uint kept = mantissa >> shift;
        uint rest = mantissa & ((1u << shift) - 1u);
        uint halfway = 1u << (shift - 1u);
        if (rest > halfway || (rest == halfway && (kept & 1u) != 0u)) kept += 1u;
        return sign | kept;
    }

    // A normal: the exponent rebased, the mantissa cut to ten bits to the nearest, ties to even; a carry into the
    // exponent is the right answer, up to infinity.
    uint rebased = magnitude - 0x38000000u;
    rebased += 0xFFFu + ((rebased >> 13) & 1u);
    return sign | (rebased >> 13);
}

uint spritesHalves(vec2 halfSizeM) { return spritesHalf(halfSizeM.x) | (spritesHalf(halfSizeM.y) << 16); }

// SpriteCell: a column and a row of a grid of columns and rows, a byte each from the low.
uint spritesCell(uint column, uint row, uint columns, uint rows) { return column | (row << 8) | (columns << 16) | (rows << 24); }

SpriteOut spritesQuad(vec2 centreM, vec2 halfSizeM, uint cell, uint tint, uint sheet, float headingRad)
{
    SpriteOut sprite;
    sprite.centreX = centreM.x;
    sprite.centreY = centreM.y;
    sprite.halfSize = spritesHalves(halfSizeM);
    sprite.headingRad = headingRad;
    sprite.cell = cell;
    sprite.tint = tint;
    sprite.sheet = sheet;
    return sprite;
}

SpriteOut spritesPlain(vec2 centreM, vec2 halfSizeM, uint sheet, float headingRad)
{
    return spritesQuad(centreM, halfSizeM, SpriteWhole, SpritePlain, sheet, headingRad);
}

// Each part comes in two: whether the host's fill draws it — the count pass's whole question, a cull and nothing built —
// and the instance itself, which the write pass builds only for a part the count pass counted.

// PersonSprites.Fill's cull for one walker: inside nothing, and its quad in view.
bool spritesPersonShown(uint person)
{
    PersonRow row = people[person];
    if ((row.flags & PersonInside) != 0u) return false;

    PersonLook look = personLooks[row.variant % figures.personSheets];
    bool down = (row.flags & PersonDown) != 0u;
    return !spritesOutside(row.positionM, down ? vec2(look.downReachM) : look.standingHalfM);
}

// And its instance: an upright cell of its facing and stride, or one frame lying along the ground.
SpriteOut spritesPerson(uint person)
{
    PersonRow row = people[person];
    uint variant = row.variant % figures.personSheets;
    PersonLook look = personLooks[variant];
    if ((row.flags & PersonDown) != 0u) return spritesPlain(row.positionM, look.downHalfM, figures.firstDownSheet + variant, row.headingRad);

    int facing = personFacingRow(row.headingRad, figures.facingFromRad, figures.facingStepRad);
    int column = (row.flags & PersonWalking) != 0u ? personWalkColumn(row.distanceWalkedM, look.heightM) : 0;
    uint cell = spritesCell(uint(column), uint(facing), figures.personColumns, figures.personRows);
    return spritesQuad(row.positionM, look.standingHalfM, cell, SpritePlain, variant, 0.0);
}

// The wagon as the view the frame shows left it (WorldView.Wagons).
WagonViewRow spritesWagonRow(uint wagon) { return frame.viewSlot == 0u ? wagonViews0[wagon] : wagonViews1[wagon]; }

// TrainSprites.Fill's cull for one wagon: in the world, and its length's reach in view.
bool spritesWagonShown(uint wagon)
{
    WagonViewRow row = spritesWagonRow(wagon);
    if ((row.flags & WagonInWorld) == 0u) return false;

    return !spritesOutside(row.positionM, vec2(trainLooks[row.flags & 0xffffu].x));
}

// And its picture, in the look its train put on it.
SpriteOut spritesWagon(uint wagon)
{
    WagonViewRow row = spritesWagonRow(wagon);
    uint look = row.flags & 0xffffu;
    return spritesPlain(row.positionM, trainLooks[look], figures.firstTrainSheet + look, row.headingRad);
}

// CrossingSprites.Fill's cull for one approach's gear, and what of it is drawn: its barrier where enough of it shows, its
// head, and its lit lamp — one each, or none.
uvec3 spritesGearShown(uint gear)
{
    GearRow row = gearRows[gear];
    precise float reachM = row.armM + figures.gearPastKerbM;
    if (spritesOutside(row.pivotM, vec2(reachM))) return uvec3(0u);

    GearShownRow shown = gearShown[gear];
    return uvec3(shown.shownM > figures.barrierWidthM ? 1u : 0u, 1u, shown.lit >= 0 ? 1u : 0u);
}

SpriteOut spritesBarrier(uint gear)
{
    GearRow row = gearRows[gear];
    float shownM = gearShown[gear].shownM;
    precise vec2 centreM = row.pivotM + row.downAlong * (shownM * 0.5);
    return spritesPlain(centreM, vec2(shownM, figures.barrierWidthM) * 0.5, figures.barrierSheet, row.armHeadingRad);
}

SpriteOut spritesCrossingHead(uint gear)
{
    GearRow row = gearRows[gear];
    return spritesPlain(row.headM, vec2(figures.headHalfM), figures.crossingHeadSheet, row.headHeadingRad);
}

SpriteOut spritesCrossingLamp(uint gear)
{
    GearRow row = gearRows[gear];
    precise vec2 lampM = gearShown[gear].lit == 0 ? row.headM - row.lampAcrossM : row.headM + row.lampAcrossM;
    return spritesQuad(lampM, vec2(figures.lampGlowHalfM), SpriteWhole, figures.glowTint, figures.glowSheet, row.headHeadingRad);
}

// CarSprites.FillFrontTyres' cull for one car: two tyres, or none.
uint spritesTyresShown(uint car)
{
    if (figures.widestTyreM < frame.leastPartM) return 0u;

    CarRow row = cars[car];
    CarLook look = carLooks[row.variant % figures.carSheets];
    return look.wheelWidthM < frame.leastPartM || spritesOutside(row.positionM, vec2(look.tyreReachM)) ? 0u : 2u;
}

// And the steered pair at the rack's angle, in TyreModel's wheel order.
void spritesTyres(uint car, out SpriteOut first, out SpriteOut second)
{
    CarRow row = cars[car];
    CarLook look = carLooks[row.variant % figures.carSheets];
    precise float tyreRad = row.headingRad + row.steerRad;
    first = spritesPlain(spritesOnBody(row.positionM, row.forward, look.firstSteeredM), look.tyreHalfM, figures.rubberSheet, tyreRad);
    second = spritesPlain(spritesOnBody(row.positionM, row.forward, look.secondSteeredM), look.tyreHalfM, figures.rubberSheet, tyreRad);
}

// CarSprites.Fill's cull for one car: its picture's reach, or its wreck's, in view.
bool spritesBodyShown(uint car)
{
    if (figures.carSheets == 0u) return false;

    CarRow row = cars[car];
    CarLook look = carLooks[row.variant % figures.carSheets];
    bool broken = (row.flags & CarBroken) != 0u;
    return !spritesOutside(row.positionM, vec2(broken ? look.wreckReachM : look.bodyReachM));
}

// And its picture, or its wreck's.
SpriteOut spritesBody(uint car)
{
    CarRow row = cars[car];
    uint variant = row.variant % figures.carSheets;
    CarLook look = carLooks[variant];
    bool broken = (row.flags & CarBroken) != 0u;
    uint sheet = (broken ? figures.carSheets : 0u) + variant;
    return spritesPlain(row.positionM, broken ? look.wreckHalfM : look.bodyHalfM, figures.firstCarSheet + sheet, row.headingRad);
}

// CarSprites.FillBeams' cull for one car: an arm on an unbroken look, its reach in view.
bool spritesBeamShown(uint car)
{
    CarRow row = cars[car];
    CarLook look = carLooks[row.variant % figures.carSheets];
    if ((row.flags & CarBroken) != 0u || look.hasBeam == 0u) return false;

    return !spritesOutside(row.positionM, vec2(row.towing >= 0 ? look.extendedReachM : look.collapsedReachM));
}

// And the arm, hinged on its deck and pointing at the fork (TowBar.ForkM), or stowed.
SpriteOut spritesBeam(uint car)
{
    CarRow row = cars[car];
    CarLook look = carLooks[row.variant % figures.carSheets];
    bool towing = row.towing >= 0;
    vec2 hingeM = spritesOnBody(row.positionM, row.forward, look.pivotM);
    precise vec2 alongTheArm = vec2(0.0);
    if (towing)
    {
        CarRow towed = cars[row.towing];
        float endSign = (towed.flags & CarHeldByTheTail) != 0u ? -1.0 : 1.0;
        precise vec2 gripM = (towed.forward * endSign) * carLooks[towed.variant % figures.carSheets].towGripM;
        precise vec2 forkM = towed.positionM + gripM;
        alongTheArm = forkM - hingeM;
    }

    precise float squareM2 = alongTheArm.x * alongTheArm.x + alongTheArm.y * alongTheArm.y;
    vec2 pointing = -row.forward;
    if (squareM2 > BeamLeastSquareM2)
    {
        float lengthM = roundedSqrt(squareM2);
        pointing = vec2(roundedQuotient(alongTheArm.x, lengthM), roundedQuotient(alongTheArm.y, lengthM));
    }

    float hingeAtM = towing ? look.extendedHingeAtM : look.collapsedHingeAtM;
    precise vec2 backM = pointing * hingeAtM;
    precise vec2 centreM = hingeM - backM;
    return spritesPlain(
        centreM, towing ? look.extendedHalfM : look.collapsedHalfM, towing ? look.extendedSheet : look.collapsedSheet,
        trigAtan2(pointing.y, pointing.x));
}

// LampSprites.Fill's verdict for one car: which of its lenses are drawn, as a mask, and the CarLamp each carries in three
// bits a lens. A lens is drawn lit and with a glow at least the narrowest part a frame draws (CAR-14).
uvec2 spritesLamps(uint car)
{
    if (figures.widestGlowM < frame.leastPartM) return uvec2(0u);

    CarRow row = cars[car];
    uint variant = row.variant % figures.carSheets;
    CarLook look = carLooks[variant];
    if ((row.flags & CarBroken) != 0u || look.lensCount == 0u) return uvec2(0u);
    if (spritesOutside(row.positionM, vec2(look.lampReachM))) return uvec2(0u);

    // What its lamps say is the car's own, signalled where it changed (CarLamps.Showing, ControlAction.Show).
    CarLampMoment moment = lampsMoment(row.lamps, frame.elapsedS, row.fuseJitter);
    uvec2 shown = uvec2(0u);
    for (uint lens = 0u; lens < look.lensCount; lens++)
    {
        LensRow at = lenses[variant * figures.mostLenses + lens];
        bool lit;
        uint colour = lampsLensColour(moment, at.fittingAndFirstColour & 0xFFu, at.atBodyM, lit);
        if (!lit || at.glowM < frame.leastPartM) continue;

        shown.x |= 1u << lens;
        shown.y |= colour << (3u * lens);
    }

    return shown;
}

// LampSprites.Glows: a headlamp burns its lens and lays no glow (CAR-14.8).
bool spritesGlows(uint colour) { return colour != LampHeadWhite; }

// How many of the lenses spritesLamps drew lay a glow.
uint spritesGlowing(uvec2 shown)
{
    uint glowing = 0u;
    for (uint lens = 0u; lens < figures.mostLenses; lens++)
    {
        if ((shown.x & (1u << lens)) != 0u && spritesGlows((shown.y >> (3u * lens)) & 7u)) glowing++;
    }

    return glowing;
}

// A drawn lens's glow, laid under every lens of its car.
SpriteOut spritesGlow(CarRow row, LensRow lens, uint colour)
{
    vec2 halfM = vec2(lens.glowM * 0.5);
    return spritesQuad(
        spritesOnBody(row.positionM, row.forward, lens.atBodyM), halfM, SpriteWhole, lampTints[colour], figures.glowSheet,
        row.headingRad);
}

// And the lens itself, from whichever of its two cells burns that colour (LampAtlas.StateOf).
SpriteOut spritesLens(CarRow row, LensRow lens, uint colour)
{
    uint cell = colour == (lens.fittingAndFirstColour >> 8) ? lens.firstCell : lens.secondCell;
    return spritesQuad(
        spritesOnBody(row.positionM, row.forward, lens.atBodyM), vec2(figures.halfLensCellM), cell, SpritePlain,
        figures.lensSheet, row.headingRad);
}

// SignalSprites.Fill's cull for one head.
bool spritesHeadShown(uint head) { return !spritesOutside(heads[head].centreM, vec2(figures.headReachM)); }

// And the frame of its strip its approach or crossing is showing.
SpriteOut spritesHead(uint head)
{
    HeadRow row = heads[head];
    bool forCars = row.forCars != 0u;
    uint colour = signalForStretch(row.subject, forCars, frame.elapsedS);
    uint column;
    if (forCars) column = colour == SignalRed ? figures.carHeadRed : colour == SignalAmber ? figures.carHeadAmber : figures.carHeadGreen;
    else column = colour == SignalRed ? figures.walkHeadRed : colour == SignalAmber ? figures.walkHeadAmber : figures.walkHeadGreen;

    vec2 halfM = forCars ? vec2(figures.carHeadHalfX, figures.carHeadHalfY) : vec2(figures.walkHeadHalfX, figures.walkHeadHalfY);
    uint cell = spritesCell(column, 0u, forCars ? figures.carHeadFrames : figures.walkHeadFrames, 1u);
    return spritesQuad(row.centreM, halfM, cell, SpritePlain, figures.firstHeadSheet + (forCars ? 0u : 1u), row.headingRad);
}
