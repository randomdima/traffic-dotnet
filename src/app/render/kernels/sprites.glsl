// The frame's moving sprites on the device (TownSprites.FillMoving): the walkers, then every part of every car on the
// ground — the exhausts' smoke, the tyres, the bodies, the tow arms, the lamps — then the signal heads, and the
// walkers and the cars on the level above after them, part by part again. Line for line with PersonSprites.Fill,
// CarSprites.FillTyres, Fill and FillBeams, LampSprites.Fill and SignalSprites.Fill, `precise` wherever the host
// rounds — all but the smoke, which is the device's alone (SmokeSprites); the arm's bearing is
// trig.glsl's arctangent, and every quotient and root a verdict hangs on is rounded.glsl's.
//
// One count a part of a unit, laid in the order the instances are drawn: the walkers', then each part of the
// cars a run apiece, then the wagons', the heads', the crossings' and the chimneys' smoke (OBJ-7), then the walkers' and
// the five car parts again for the level above
// and the wagons again for those on a bridge (RAIL-8). A count pass writes them, a
// scan makes them where each part's instances start, and a write pass lays every part there — so the order is the
// host's whatever order the threads ran in. Every unit is asked, not only those a grid says are near: the cull is the
// host's own, per part, and the rosters are walked in index order as TownWorld.CarsIn hands them over.
//
// sprites_buffers.glsl declares everything read here.

#include "core/geometry/kernels/trig.glsl"
#include "core/geometry/kernels/floats.glsl"

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

// A car's parts in the order a level draws them (TownSprites.FillCars), a run of slots each, and every car's smoke over
// them all (CAR-54): its own and that of any car driving through it.
const uint PartTyres = 0u;
const uint PartBody = 1u;
const uint PartBeam = 2u;
const uint PartLamps = 3u;
const uint PartSmoke = 4u;
const uint CarParts = 5u;

// CarSprites.FillBeams: a pair driven exactly on top of each other has no direction, under this square of a length.
const float BeamLeastSquareM2 = 1e-6;

// Where the scan keeps the grand total (DeviceSprites).
const uint TotalAt = 0u;

// The trains' wagons (TRN-7), a slot each between the cars' parts and the heads, as TownSprites.FillMoving draws them.
uint spritesWagonsFrom() { return figures.personCapacity + CarParts * figures.carCapacity; }

uint spritesHeadsFrom() { return spritesWagonsFrom() + figures.wagonCount; }

// The crossings' gear (RAIL-7), two slots an approach — its arm, its housing — after the heads, then a slot an X light.
const uint GearParts = 2u;

uint spritesGearFrom() { return spritesHeadsFrom() + figures.headCount; }

uint spritesLightsFrom() { return spritesGearFrom() + GearParts * figures.gearCount; }

// The rail vehicles' smoke (TRN-15), a slot each, over everything on the ground but the chimneys' smoke.
uint spritesWagonSmokeFrom() { return spritesLightsFrom() + figures.lightCount; }

// The chimneys that smoke (OBJ-7), a slot each, last of the over run: a run fuller than its room cuts their puffs alone.
uint spritesChimneysFrom() { return spritesWagonSmokeFrom() + figures.wagonCount; }

uint spritesAboveFrom() { return spritesChimneysFrom() + figures.chimneyCount; }

// The walkers on the level above (PHY-1b), a slot each at the head of the run above, then the cars on it.
uint spritesCarsAboveFrom() { return spritesAboveFrom() + (figures.levelled != 0u ? figures.personCapacity : 0u); }

// The wagons on a bridge (RAIL-8), a slot each after the cars above, as TownSprites.FillMoving draws them, and the smoke
// of those that smoke after them.
uint spritesWagonsAboveFrom() { return spritesCarsAboveFrom() + (figures.levelled != 0u ? CarParts * figures.carCapacity : 0u); }

uint spritesWagonSmokeAboveFrom() { return spritesWagonsAboveFrom() + figures.wagonCount; }

uint spritesSlotCount() { return spritesWagonsAboveFrom() + (figures.bridged != 0u ? 2u * figures.wagonCount : 0u); }

// Whether the run above is laid at all: cars on a level above, or wagons on a bridge.
bool spritesAnythingAbove() { return figures.levelled != 0u || figures.bridged != 0u; }

uint spritesCarSlot(uint run, uint part, uint car)
{
    return (run == RunOver ? figures.personCapacity : spritesCarsAboveFrom()) + part * figures.carCapacity + car;
}

// The run a body on a level is drawn in: every body in the one run of a town with nothing above its ground, and otherwise
// the run of its level, or none for a level neither run draws.
uint spritesRunOfLevel(uint level)
{
    if (figures.levelled == 0u || level == figures.groundLevel) return RunOver;
    return level == figures.aboveLevel ? RunAbove : RunNone;
}

uint spritesRunOf(CarRow row) { return spritesRunOfLevel((row.flags >> CarLevelShift) & 0xFFu); }

uint spritesRunOfPerson(PersonRow row) { return spritesRunOfLevel((row.flags >> PersonLevelShift) & 0xFFu); }

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

// A body's tilt on its springs to the step it is drawn at (BodyTilt, CAR-56): nose down and right side down, each a
// signed count of steps.
const float BodyTiltStepsPerRad = 512.0;
const float BodyTiltMostSteps = 127.0;

vec2 spritesTiltSteps(CarRow row)
{
    return clamp(roundEven(unpackHalf2x16(row.tiltHalves) * BodyTiltStepsPerRad), -BodyTiltMostSteps, BodyTiltMostSteps);
}

// As its instance carries it in the ID (BodyTilt.Id).
uint spritesTiltId(CarRow row)
{
    ivec2 steps = ivec2(spritesTiltSteps(row));
    return (uint(steps.x) & 0xFFu) | ((uint(steps.y) & 0xFFu) << 8u);
}

// Where a point of a car's body is drawn, the body tilted (CarSprites.OnBodyM, BodyTilt.OnBodyM).
vec2 spritesOnTiltedBody(CarRow row, vec2 atBodyM)
{
    vec2 tiltRad = spritesTiltSteps(row) * (1.0 / BodyTiltStepsPerRad);
    precise float sinkM = atBodyM.x * tiltRad.x + atBodyM.y * tiltRad.y;
    precise float eyeM = figures.carTiltEyeHeightM + sinkM;
    precise vec2 drawnM = atBodyM * roundedQuotient(figures.carTiltEyeHeightM, eyeM);
    return spritesOnBody(row.positionM, row.forward, drawnM);
}

// SpriteRing, where an instance carries it (SpriteInstance.RingShift), and SpriteRings' bits.
const uint RingSelected = 1u;
const uint RingSecondHand = 2u;
const uint SpriteRingShift = 14u;

bool spritesRingBit(uint from, uint unit) { return ((ringBits[from + (unit >> 5u)] >> (unit & 31u)) & 1u) != 0u; }

uint spritesRingWords(uint units) { return (units + 31u) >> 5u; }

uint spritesPersonRing(uint person) { return spritesRingBit(0u, person) ? RingSelected << SpriteRingShift : 0u; }

uint spritesCarRing(uint car)
{
    uint ring = spritesRingBit(spritesRingWords(figures.personCapacity), car) ? RingSelected
        : int(car) == secondHandCar ? RingSecondHand : 0u;
    return ring << SpriteRingShift;
}

uint spritesWagonRing(uint wagon)
{
    uint from = spritesRingWords(figures.personCapacity) + spritesRingWords(figures.carCapacity);
    return spritesRingBit(from, wagon) ? RingSelected << SpriteRingShift : 0u;
}

// SpriteCell: a column and a row of a grid of columns and rows, a byte each from the low.
uint spritesCell(uint column, uint row, uint columns, uint rows) { return column | (row << 8) | (columns << 16) | (rows << 24); }

SpriteOut spritesQuad(vec2 centreM, vec2 halfSizeM, uint cell, uint tint, uint sheet, float headingRad)
{
    SpriteOut sprite;
    sprite.centreX = centreM.x;
    sprite.centreY = centreM.y;
    sprite.halfSize = floatHalves(halfSizeM);
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

    return !spritesOutside(row.positionM, spritesPersonLook(row.variant % figures.personSheets).halfM);
}

// And its instance: an upright cell of its facing and stride — a casualty's the stance, reddened.
SpriteOut spritesPerson(uint person)
{
    PersonRow row = people[person];
    uint variant = row.variant % figures.personSheets;
    PersonLook look = spritesPersonLook(variant);
    bool down = (row.flags & PersonDown) != 0u;
    int facing = personFacingRow(row.headingRad, figures.facingFromRad, figures.facingStepRad);
    int column = !down && (row.flags & PersonWalking) != 0u ? personWalkColumn(row.distanceWalkedM, look.heightM) : 0;
    uint cell = spritesCell(uint(column), uint(facing), figures.personColumns, figures.personRows);
    uint tint = down ? figures.casualtyTint : SpritePlain;
    return spritesQuad(row.positionM, look.halfM, cell, tint, variant | spritesPersonRing(person), 0.0);
}

// The wagon as the view the frame shows left it (WorldView.Wagons).
WagonViewRow spritesWagonRow(uint wagon) { return frame.viewSlot == 0u ? wagonViews0[wagon] : wagonViews1[wagon]; }

// TrainSprites.Fill's cull for one wagon: in the world, and its length's reach in view.
bool spritesWagonShown(uint wagon)
{
    WagonViewRow row = spritesWagonRow(wagon);
    if ((row.flags & WagonInWorld) == 0u) return false;

    return !spritesOutside(row.positionM, vec2(spritesTrainLook(row.flags & 0xffffu).halfM.x));
}

// WagonView.OnABridge: the run a wagon is drawn in is the bridge's where any of it is over one.
bool spritesWagonOnABridge(uint wagon) { return (spritesWagonRow(wagon).flags >> WagonChannelsShift) == figures.bridgeChannel; }

// And its picture, in the look its train put on it, its index its ID (SpriteInstance.Id: the sheet's high half-word).
SpriteOut spritesWagon(uint wagon)
{
    WagonViewRow row = spritesWagonRow(wagon);
    uint look = row.flags & 0xffffu;
    return spritesPlain(
        row.positionM, spritesTrainLook(look).halfM, (figures.firstTrainSheet + look) | spritesWagonRing(wagon) | (wagon << 16u),
        row.headingRad);
}

// CrossingSprites.Fill's cull for one approach's gear, and what of it is drawn: its arm where enough of it shows, and its
// drive — one each, or none.
uvec2 spritesGearShown(uint gear)
{
    GearRow row = spritesGearRow(gear);
    if (spritesOutside(row.pivotM, vec2(row.reachM))) return uvec2(0u);

    GearShownRow shown = gearShown[gear];
    return uvec2(length(shown.armToM - shown.armFromM) > figures.barrierWidthM ? 1u : 0u, 1u);
}

SpriteOut spritesBarrier(uint gear)
{
    GearShownRow shown = gearShown[gear];
    precise vec2 armM = shown.armToM - shown.armFromM;
    precise vec2 centreM = (shown.armFromM + shown.armToM) * 0.5;
    return spritesPlain(centreM, vec2(length(armM), figures.barrierWidthM) * 0.5, figures.barrierSheet, trigAtan2(armM.y, armM.x));
}

SpriteOut spritesBarrierDrive(uint gear)
{
    GearRow row = spritesGearRow(gear);
    precise vec2 centreM = gearShown[gear].armFromM - row.downAlong * figures.driveHalfLengthM;
    return spritesPlain(
        centreM, vec2(figures.driveHalfLengthM, figures.driveHalfWidthM), figures.barrierDriveSheet, row.armHeadingRad);
}

// CrossingSprites.Fill's cull for one X light.
bool spritesLightShown(uint light)
{
    return !spritesOutside(spritesLightRow(light).centreM, vec2(length(vec2(figures.lightHalfLengthM, figures.lightHalfWidthM))));
}

// And the picture its gear's lights show, dark or lit.
SpriteOut spritesCrossingLight(uint light)
{
    LightRow row = spritesLightRow(light);
    uint sheet = gearShown[row.gear].lit != 0u ? figures.crossingLitSheet : figures.crossingLightSheet;
    return spritesPlain(row.centreM, vec2(figures.lightHalfLengthM, figures.lightHalfWidthM), sheet, row.headingRad);
}

// CarSprites.FillTyres' cull for one car: a tyre a wheel, or none.
uint spritesTyresShown(uint car)
{
    if (figures.widestTyreM < frame.leastPartM) return 0u;

    CarRow row = cars[car];
    CarLook look = spritesCarLook(row.variant % figures.carSheets);
    return look.wheelWidthM < frame.leastPartM || spritesOutside(row.positionM, vec2(look.tyreReachM)) ? 0u : look.wheelCount;
}

// And one of them, in TyreModel's wheel order: the steered ones at the rack's angle, and none tilted with the body.
SpriteOut spritesTyre(uint car, uint tyre)
{
    CarRow row = cars[car];
    CarLook look = spritesCarLook(row.variant % figures.carSheets);
    precise float steeredRad = row.headingRad + row.steerRad;
    bool steered = (look.steeredWheels & (1u << tyre)) != 0u;
    return spritesPlain(
        spritesOnBody(row.positionM, row.forward, look.wheelsM[tyre]), look.tyreHalfM, figures.rubberSheet,
        steered ? steeredRad : row.headingRad);
}

// CarSprites.Fill's cull for one car: its picture's reach, or its wreck's, in view.
bool spritesBodyShown(uint car)
{
    if (figures.carSheets == 0u) return false;

    CarRow row = cars[car];
    CarLook look = spritesCarLook(row.variant % figures.carSheets);
    bool broken = (row.flags & CarBroken) != 0u;
    return !spritesOutside(row.positionM, vec2(broken ? look.wreckReachM : look.bodyReachM));
}

// And its picture, or its wreck's, its tilt its ID (SpriteInstance.Id: the sheet's high half-word).
SpriteOut spritesBody(uint car)
{
    CarRow row = cars[car];
    uint variant = row.variant % figures.carSheets;
    CarLook look = spritesCarLook(variant);
    bool broken = (row.flags & CarBroken) != 0u;
    uint sheet = figures.firstCarSheet + (broken ? figures.carSheets : 0u) + variant;
    return spritesPlain(
        row.positionM, broken ? look.wreckHalfM : look.bodyHalfM, sheet | spritesCarRing(car) | (spritesTiltId(row) << 16u),
        row.headingRad);
}

// CarSprites.FillBeams' cull for one car: an arm on an unbroken look, its reach in view.
bool spritesBeamShown(uint car)
{
    CarRow row = cars[car];
    CarLook look = spritesCarLook(row.variant % figures.carSheets);
    if ((row.flags & CarBroken) != 0u || look.hasBeam == 0u) return false;

    return !spritesOutside(row.positionM, vec2(row.towing != -1 ? look.extendedReachM : look.collapsedReachM));
}

// And the arm, hinged on its deck and pointing at the fork (TowBar.ForkM), or stowed.
SpriteOut spritesBeam(uint car)
{
    CarRow row = cars[car];
    CarLook look = spritesCarLook(row.variant % figures.carSheets);
    bool towing = row.towing != -1;
    vec2 hingeM = spritesOnTiltedBody(row, look.pivotM);
    precise vec2 alongTheArm = vec2(0.0);
    if (towing)
    {
        // A car by its index, a rail vehicle as −2 less its index (DeviceWorld.TowingCode), each as the frame's view has it.
        vec2 loadM = row.towing >= 0 ? cars[row.towing].positionM : spritesWagonRow(uint(-2 - row.towing)).positionM;
        vec2 loadForward = row.towing >= 0 ? cars[row.towing].forward : trigUnit(spritesWagonRow(uint(-2 - row.towing)).headingRad);
        precise vec2 forkM = loadM + (loadForward * row.forkAlongM);
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
    CarLook look = spritesCarLook(variant);
    if ((row.flags & CarBroken) != 0u || look.lensCount == 0u) return uvec2(0u);
    if (spritesOutside(row.positionM, vec2(look.lampReachM))) return uvec2(0u);

    // What its lamps say is the car's own, signalled where it changed (CarLamps.Showing, ControlAction.Show).
    CarLampMoment moment = lampsMoment(row.lamps, frame.elapsedS);
    for (uint lens = 0u; lens < look.lensCount; lens++)
    {
        moment.redGlass = moment.redGlass
                          || (spritesLensRow(variant * figures.mostLenses + lens).fittingAndFirstColour & 0xFFu) == FittingBeaconRed;
    }

    uvec2 shown = uvec2(0u);
    for (uint lens = 0u; lens < look.lensCount; lens++)
    {
        LensRow at = spritesLensRow(variant * figures.mostLenses + lens);
        bool lit;
        uint colour = lampsLensColour(moment, at.fittingAndFirstColour & 0xFFu, at.atBodyM, lit);
        if (!lit || at.glowM < frame.leastPartM) continue;

        shown.x |= 1u << lens;
        shown.y |= colour << (3u * lens);
    }

    return shown;
}

// A drawn lens's glow, laid under every lens of its car: its first cell, on the glow sheet's grid, baked across the
// lens's own glow (LampSprites.Glow).
SpriteOut spritesGlow(CarRow row, LensRow lens, uint colour)
{
    return spritesQuad(
        spritesOnTiltedBody(row, lens.atBodyM), vec2(lens.glowM * 0.5), lens.firstCell,
        spritesLampTint(colour), figures.glowSheet, row.headingRad);
}

// And the lens itself, from whichever of its two cells burns that colour (LampAtlas.StateOf).
SpriteOut spritesLens(CarRow row, LensRow lens, uint colour)
{
    uint cell = colour == (lens.fittingAndFirstColour >> 8) ? lens.firstCell : lens.secondCell;
    return spritesQuad(
        spritesOnTiltedBody(row, lens.atBodyM), vec2(figures.halfLensCellM), cell, SpritePlain,
        figures.lensSheet, row.headingRad);
}

// The "lowbias32" finaliser, and a hashed word's top 24 bits as a share in [0, 1).
uint spritesHash(uint x)
{
    x ^= x >> 16;
    x *= 0x7feb352du;
    x ^= x >> 15;
    x *= 0x846ca68bu;
    return x ^ (x >> 16);
}

float spritesShare(uint hash) { return float(hash >> 8) * (1.0 / 16777216.0); }

// SpriteTint.Pack's byte for one channel.
uint spritesByte(float share)
{
    precise float rounded = clamp(share, 0.0, 1.0) * 255.0 + 0.5;
    return uint(rounded);
}

// The exhausts' smoke (CAR-54, TRN-15, SmokeSprites), the device's alone: no host fill draws it, so nothing here is held
// to one. A car's track is its index among the smoke's, a rail vehicle's past them (figures.firstWagonTrack); every puff
// is aged off the moment of the view the host shows (the track's shownS, DeviceSmoke), and not the frame's.

// Where a pipe opens on its body (+x the nose, +y its right), and its kind's smoke.
struct SmokePipe
{
    vec2 atM;
    SmokeKind kind;
};

// Whether a track draws any smoke: a frame that draws its kind's widest puff at all, a track that has run within its
// kind's longest life, and the body as the step left it in its kind's reach of the view.
bool spritesTrackSmokes(uint track, SmokeKind kind)
{
    if (kind.widestM < frame.leastPartM) return false;

    SmokeHead head = smokeHeads[track];
    if (head.count == 0u || head.shownS - head.ranS >= kind.longestS) return false;
    return !spritesOutside(head.live.positionM, vec2(kind.reachM));
}

// And a car, of a look that ends in a pipe.
bool spritesSmokes(uint car)
{
    return figures.carSheets != 0u && spritesCarLook(cars[car].variant % figures.carSheets).exhaustCount != 0u
           && spritesTrackSmokes(car, figures.tailpipe);
}

// Where a track's ring starts among the points, and how many it holds: every car's first, then every rail vehicle's
// (DeviceSmoke.Points).
uint spritesRingFrom(uint track)
{
    if (track < figures.firstWagonTrack) return track * figures.tailpipe.trackPoints;
    return figures.firstWagonTrack * figures.tailpipe.trackPoints + (track - figures.firstWagonTrack) * figures.stack.trackPoints;
}

uint spritesRingPoints(uint track)
{
    return track < figures.firstWagonTrack ? figures.tailpipe.trackPoints : figures.stack.trackPoints;
}

// The body at bornS off its track: between the two points of its way either side, newest back, the newest of them and
// the body as the step left it — or nothing where the track does not reach back that far, or it was not running then.
bool spritesThen(uint track, float bornS, out SmokePoint then)
{
    SmokeHead head = smokeHeads[track];
    uint ring = spritesRingFrom(track);
    uint points = spritesRingPoints(track);
    SmokePoint later = head.live;
    for (uint back = 0u; back < head.count; back++)
    {
        SmokePoint earlier = smokePoints[ring + (head.newest + points - back) % points];
        if (bornS >= earlier.timeS)
        {
            float spanS = later.timeS - earlier.timeS;
            then = smokeBetween(earlier, later, spanS > 0.0 ? clamp((bornS - earlier.timeS) / spanS, 0.0, 1.0) : 1.0);
            return then.running > 0.0;
        }

        later = earlier;
    }

    then = head.live;
    return false;
}

// One pipe's run of puffs at the shown moment, on the finest grid a puff is laid on: the newest's number, how long it has
// been out in puffs, and the pipe's own hash that staggers it.
struct PuffRun
{
    float sinceNewest;
    uint born;
    uint seed;
};

PuffRun spritesPuffRun(uint track, uint pipe)
{
    uint seed = spritesHash(track * figures.mostExhausts + pipe);
    float slots = smokeHeads[track].shownS * figures.puffPerEveryS + spritesShare(seed);
    float newest = floor(slots);
    return PuffRun(slots - newest, uint(newest), seed);
}

// The air a puff ageS old has been out in (Wind.Back): how far it has carried it since it left, and its velocity as it
// left — the frame's moments of it, read between.
AirBack spritesAirSince(float ageS)
{
    float at = clamp(ageS * figures.perAirStepS, 0.0, float(AirMoments - 1u));
    uint before = min(uint(at), AirMoments - 2u);
    float between = at - float(before);
    AirBack younger = frame.air[before];
    AirBack older = frame.air[before + 1u];
    return AirBack(mix(younger.sinceM, older.sinceM, between), mix(younger.mps, older.mps, between));
}

// Where the puff that left a pipe ageS ago is: the pipe where the body was then, and the run it has made since. It left
// with the body's velocity and the pipe's outflow and has been closing on the air's since (SmokeFigures.DragS), so it is
// where the air alone would have carried it, and a run at the difference it left at that is never more than DragS of it.
vec2 spritesPuffAt(SmokePoint then, AirBack air, vec2 pipeM, float outflowMps, float ageS, float scatter)
{
    vec2 right = vec2(-then.forward.y, then.forward.x);
    vec2 pipeThenM = then.positionM + then.forward * pipeM.x + right * pipeM.y;
    vec2 leftWithMps = then.velocityMps - then.forward * outflowMps + right * (scatter * figures.scatterMps);
    float runS = ageS * figures.dragS / (figures.dragS + ageS);
    return pipeThenM + air.sinceM + (leftWithMps - air.mps) * runS;
}

// How many of the finest grid's puffs the one left at `then` stands for (SmokeFigures.PuffSplits, PuffGapM): all of a
// split, halved while the next would be left further off it than the gap — how far the body ran off the air between them,
// and at a standstill the outflow, or the eddies off a stack.
uint spritesPuffStride(SmokePoint then, vec2 airMps, float outflowMps)
{
    float apartM = max(length(then.velocityMps - airMps), max(outflowMps, figures.scatterMps)) * figures.puffEveryS;
    uint stride = figures.puffSplits;
    while (stride > 1u && apartM * float(stride) > figures.puffGapM) stride >>= 1u;
    return stride;
}

// One puff of a pipe's run, the `puff`-th youngest of the finest grid: whether the frame draws it — out, no older than its
// life, as wide as a part is drawn, left while the body ran, on its stride, and in view — and the instance.
bool spritesPuff(uint track, SmokePipe pipe, PuffRun run, uint puff, out SpriteOut sprite)
{
    SmokeKind kind = pipe.kind;
    float ageS = (run.sinceNewest + float(puff)) * figures.puffEveryS;
    if (puff > run.born || ageS > kind.longestS) return false;

    SmokePoint then;
    if (!spritesThen(track, smokeHeads[track].shownS - ageS, then)) return false;

    // As hard as its driver worked the engine when it left: how long it lives, how wide it leaves, how hard it is blown
    // out and how thick it gets (ExhaustLook).
    float lifeS = mix(kind.idleLifeS, kind.fullLifeS, then.throttle);
    float widthM = mix(kind.idleBirthWidthM, kind.fullBirthWidthM, then.throttle) + kind.spreadMps * ageS;
    if (ageS > lifeS || widthM < frame.leastPartM) return false;

    // A puff is on its stride's grid, so a body speeding up draws more between those it drew and none of them goes.
    uint born = run.born - puff;
    float outflowMps = mix(kind.idleOutflowMps, kind.fullOutflowMps, then.throttle);
    AirBack air = spritesAirSince(ageS);
    uint stride = spritesPuffStride(then, air.mps, outflowMps);
    if (born % stride != 0u) return false;

    uint which = spritesHash(born ^ run.seed);
    vec2 atM = spritesPuffAt(then, air, pipe.atM, outflowMps, ageS, spritesShare(which) * 2.0 - 1.0);
    if (spritesOutside(atM, vec2(widthM))) return false;

    // Clear as it leaves the pipe, thickest its kind's share of the way through its life, and gone at the end of it: its
    // life warped so that share is the middle, and a parabola over that. A car's is thickest halfway, so the youngest,
    // smallest puffs do not string out behind it at speed as beads. A puff of a split is its share of a whole one's
    // thickness, so a trail is as thick drawn in two as in one.
    float lived = ageS / lifeS;
    float warped = lived < kind.thickestAt ? 0.5 * lived / kind.thickestAt : 0.5 + 0.5 * (lived - kind.thickestAt) / (1.0 - kind.thickestAt);
    float whole = then.running * mix(kind.idleOpacity, kind.fullOpacity, then.throttle) * warped * (1.0 - warped) * 4.0;
    float alpha = 1.0 - pow(max(1.0 - whole, 0.0), float(stride) / float(figures.puffSplits));
    uint shade = spritesByte(kind.shade);
    uint tint = shade | (shade << 8) | (shade << 16) | (spritesByte(alpha) << 24);
    sprite = spritesQuad(atM, vec2(widthM * 0.5), SpriteWhole, tint, figures.smokeSheet, spritesShare(spritesHash(which)) * 6.28318548);
    return true;
}

// A car's tailpipe of a look, and a rail vehicle's stack.
SmokePipe spritesTailpipe(uint variant, uint pipe)
{
    return SmokePipe(spritesExhaust(variant * figures.mostExhausts + pipe), figures.tailpipe);
}

SmokePipe spritesStack(TrainLook look, uint stack)
{
    return SmokePipe(look.exhaustsM[stack], figures.stack);
}

// The puffs one car draws, every pipe's oldest first so the young puff at the pipe is over the spread one it follows,
// counted.
uint spritesPuffsShown(uint car)
{
    if (!spritesSmokes(car)) return 0u;

    uint variant = cars[car].variant % figures.carSheets;
    uint shown = 0u;
    for (uint pipe = 0u; pipe < spritesCarLook(variant).exhaustCount; pipe++)
    {
        PuffRun run = spritesPuffRun(car, pipe);
        SmokePipe tailpipe = spritesTailpipe(variant, pipe);
        for (uint left = figures.tailpipe.puffsEach; left > 0u; left--)
        {
            SpriteOut sprite;
            if (spritesPuff(car, tailpipe, run, left - 1u, sprite)) shown++;
        }
    }

    return shown;
}

// A rail vehicle's look as the view the frame shows has it, of which its stacks (TRN-15).
uint spritesWagonLook(uint wagon) { return spritesWagonRow(wagon).flags & 0xffffu; }

// Whether a rail vehicle draws any smoke: in the world, a look with a stack, and its track's say (spritesTrackSmokes).
bool spritesWagonSmokes(uint wagon)
{
    WagonViewRow row = spritesWagonRow(wagon);
    return (row.flags & WagonInWorld) != 0u && spritesTrainLook(row.flags & 0xffffu).exhaustCount != 0u
           && spritesTrackSmokes(figures.firstWagonTrack + wagon, figures.stack);
}

// The puffs one rail vehicle draws, every stack's oldest first, counted.
uint spritesWagonPuffsShown(uint wagon)
{
    if (!spritesWagonSmokes(wagon)) return 0u;

    uint track = figures.firstWagonTrack + wagon;
    TrainLook look = spritesTrainLook(spritesWagonLook(wagon));
    uint shown = 0u;
    for (uint stack = 0u; stack < look.exhaustCount; stack++)
    {
        PuffRun run = spritesPuffRun(track, stack);
        SmokePipe pipe = spritesStack(look, stack);
        for (uint left = figures.stack.puffsEach; left > 0u; left--)
        {
            SpriteOut sprite;
            if (spritesPuff(track, pipe, run, left - 1u, sprite)) shown++;
        }
    }

    return shown;
}

// The chimneys' smoke (OBJ-7, ChimneySprites), the device's alone, aged off the frame's clock: a chimney stands still, so
// a puff is where the air and the eddy it left on have carried it since, and nothing is kept for it.

// Whether a chimney draws any smoke: a frame that draws its widest puff at all, and its pot in the smoke's reach of the view.
bool spritesChimneySmokes(uint chimney)
{
    return figures.widestChimneyPuffM >= frame.leastPartM
           && !spritesOutside(spritesChimney(chimney).atM, vec2(figures.chimneyReachM));
}

// A chimney's pot as its plume is drawn off it (ChimneySprites), k times as wide as a house's: a puff leaves it as wide as
// it, as opaque as k of a house's laid one over the other, and lives √k times as long; and so how many it has out.
struct ChimneyPot
{
    float birthWidthM;
    float opacity;
    float lifeS;
    uint puffs;
};

ChimneyPot spritesChimneyPot(uint chimney)
{
    float potWidthM = spritesChimney(chimney).potWidthM;
    float k = potWidthM * figures.chimneyPerPotWidthM;
    float lifeS = figures.chimneyLifeS * sqrt(k);
    return ChimneyPot(
        potWidthM, 1.0 - pow(1.0 - figures.chimneyOpacity, k), lifeS,
        min(uint(lifeS * figures.chimneyPerEveryS) + 1u, figures.chimneyPuffsEach));
}

// ChimneySprites.PotBits: how many of a row's key's low bits are its pot within its stack.
const uint ChimneyPotBits = 4u;

// Whether a stack's fire burns at `atS` (ChimneyFigures.SmokingShare, FirePeriodS): the clock cut into turns, each fire's
// staggered off its own key, and in every turn the fire lit or let out afresh off its key and the turn alone.
bool spritesChimneyBurning(uint fire, float atS)
{
    uint turn = uint(max(atS, 0.0) * figures.chimneyPerFireS + spritesShare(spritesHash(spritesHash(fire))));
    return spritesShare(spritesHash(fire ^ spritesHash(turn))) < figures.chimneySmokingShare;
}

// One puff of a chimney's plume, the `puff`-th youngest: whether the frame draws it — out, left while its fire burned, no
// older than its life, as wide as a part is drawn and in view — and the instance.
bool spritesChimneyPuff(uint chimney, ChimneyPot pot, uint puff, out SpriteOut sprite)
{
    ChimneyRow row = spritesChimney(chimney);
    uint fire = row.seed >> ChimneyPotBits;
    float slots = frame.elapsedS * figures.chimneyPerEveryS + spritesShare(spritesHash(row.seed));
    float newest = floor(slots);
    float ageS = (slots - newest + float(puff)) * figures.chimneyEveryS;
    float widthM = pot.birthWidthM + figures.chimneySpreadMps * ageS;
    if (float(puff) > newest || ageS > pot.lifeS || widthM < frame.leastPartM) return false;
    if (!spritesChimneyBurning(fire, frame.elapsedS - ageS)) return false;

    // Up out of the pot first, which from above is standing on it (ChimneyFigures.RiseS); then the air and the eddy it
    // left on carry it, the eddy given up to the wind over the drag as a car's puff gives up its speed (spritesPuffAt).
    uint which = spritesHash((uint(newest) - puff) ^ row.seed);
    uint eddy = spritesHash(which);
    float wayRad = spritesShare(eddy) * 6.28318548;
    vec2 eddyMps = vec2(cos(wayRad), sin(wayRad)) * (spritesShare(spritesHash(eddy)) * figures.chimneyScatterMps);
    float carriedS = max(ageS - figures.chimneyRiseS, 0.0);
    float runS = carriedS * figures.chimneyDragS / (figures.chimneyDragS + carriedS);
    vec2 atM = row.atM + spritesAirSince(carriedS).sinceM + eddyMps * runS;
    if (spritesOutside(atM, vec2(widthM))) return false;

    // Thickening as it rises out of the pot, thickest as it leaves it, thinning to nothing over its life; and as thick as
    // the fire was stoked when it left, which swings slowly, each fire from its own start.
    float lived = ageS / pot.lifeS;
    float stoked = 0.5 + 0.5 * sin(6.28318548 * ((frame.elapsedS - ageS) * figures.chimneyPerBreathS + spritesShare(spritesHash(fire))));
    float alpha = pot.opacity * (1.0 - figures.chimneyBreathShare * stoked) * min(ageS / figures.chimneyRiseS, 1.0)
                  * (1.0 - lived);
    uint shade = spritesByte(figures.chimneyShade);
    uint tint = shade | (shade << 8) | (shade << 16) | (spritesByte(alpha) << 24);
    sprite = spritesQuad(atM, vec2(widthM * 0.5), SpriteWhole, tint, figures.smokeSheet, spritesShare(which) * 6.28318548);
    return true;
}

// The puffs one chimney draws, counted.
uint spritesChimneyPuffsShown(uint chimney)
{
    if (!spritesChimneySmokes(chimney)) return 0u;

    ChimneyPot pot = spritesChimneyPot(chimney);
    uint shown = 0u;
    for (uint puff = 0u; puff < pot.puffs; puff++)
    {
        SpriteOut sprite;
        if (spritesChimneyPuff(chimney, pot, puff, sprite)) shown++;
    }

    return shown;
}

// SignalSprites.Fill's cull for one head.
bool spritesHeadShown(uint head) { return !spritesOutside(spritesHeadRow(head).centreM, vec2(figures.headReachM)); }

// And the frame of its strip its approach or crossing is showing.
SpriteOut spritesHead(uint head)
{
    HeadRow row = spritesHeadRow(head);
    bool forCars = row.forCars != 0u;
    uint colour = signalForStretch(row.subject, forCars, frame.elapsedS);
    uint column;
    if (forCars) column = colour == SignalRed ? figures.carHeadRed : colour == SignalAmber ? figures.carHeadAmber : figures.carHeadGreen;
    else column = colour == SignalRed ? figures.walkHeadRed : colour == SignalAmber ? figures.walkHeadAmber : figures.walkHeadGreen;

    vec2 halfM = forCars ? vec2(figures.carHeadHalfX, figures.carHeadHalfY) : vec2(figures.walkHeadHalfX, figures.walkHeadHalfY);
    uint cell = spritesCell(column, 0u, forCars ? figures.carHeadFrames : figures.walkHeadFrames, 1u);
    return spritesQuad(row.centreM, halfM, cell, SpritePlain, figures.firstHeadSheet + (forCars ? 0u : 1u), row.headingRad);
}
