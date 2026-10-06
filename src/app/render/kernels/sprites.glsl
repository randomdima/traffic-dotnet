// The frame's moving sprites on the device (TownSprites.FillMoving): the walkers, then every part of every car on the
// ground — the steered tyres, the bodies, the tow arms, the lamps — then the signal heads, and the cars on the level
// above after them, part by part again. Line for line with PersonSprites.Fill, CarSprites.FillFrontTyres, Fill and
// FillBeams, LampSprites.Fill and SignalSprites.Fill, `precise` wherever the host rounds; the arm's bearing is the
// device's own arctangent, and every quotient and root a verdict hangs on is rounded.glsl's.
//
// One count a part of a unit, laid in the order the instances are drawn: the walkers', then each part of the
// cars a run apiece, then the heads', then the four car parts again for the level above. A count pass writes them, a
// scan makes them where each part's instances start, and a write pass lays every part there — so the order is the
// host's whatever order the threads ran in. Every unit is asked, not only those a grid says are near: the cull is the
// host's own, per part, and the rosters are walked in index order as TownWorld.CarsIn hands them over.
//
// sprites_buffers.glsl declares everything read here.

// PersonSprites.Plain: the tint everything but a mark and a glow is drawn with.
const vec4 SpritePlain = vec4(1.0);

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

uint spritesHeadsFrom() { return figures.personCapacity + CarParts * figures.carCapacity; }

uint spritesAboveFrom() { return spritesHeadsFrom() + figures.headCount; }

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

SpriteOut spritesQuad(vec2 centreM, vec2 halfSizeM, vec2 uvMin, vec2 uvSize, vec4 tint, uint sheet, float headingRad)
{
    SpriteOut sprite;
    sprite.centreM = centreM;
    sprite.halfSizeM = halfSizeM;
    sprite.uvMin = uvMin;
    sprite.uvSize = uvSize;
    sprite.tintRG = tint.xy;
    sprite.tintBA = tint.zw;
    sprite.sheet = sheet;
    sprite.headingRad = headingRad;
    return sprite;
}

SpriteOut spritesPlain(vec2 centreM, vec2 halfSizeM, uint sheet, float headingRad)
{
    return spritesQuad(centreM, halfSizeM, vec2(0.0), vec2(1.0), SpritePlain, sheet, headingRad);
}

// PersonSprites.Fill for one walker: an upright cell of its facing and stride, or one frame lying along the ground.
bool spritesPerson(uint person, out SpriteOut sprite)
{
    PersonRow row = people[person];
    if ((row.flags & PersonInside) != 0u) return false;

    uint variant = row.variant % figures.personSheets;
    PersonLook look = personLooks[variant];
    bool down = (row.flags & PersonDown) != 0u;
    vec2 halfSizeM = down ? look.downHalfM : look.standingHalfM;
    if (spritesOutside(row.positionM, down ? vec2(look.downReachM) : halfSizeM)) return false;

    if (down)
    {
        sprite = spritesPlain(row.positionM, halfSizeM, figures.firstDownSheet + variant, row.headingRad);
        return true;
    }

    int facing = personFacingRow(row.headingRad, figures.facingFromRad, figures.facingStepRad);
    int column = (row.flags & PersonWalking) != 0u ? personWalkColumn(row.distanceWalkedM, look.heightM) : 0;
    vec2 cell = vec2(figures.personCellU, figures.personCellV);
    precise vec2 uvMin = vec2(float(column) * cell.x, float(facing) * cell.y);
    sprite = spritesQuad(row.positionM, halfSizeM, uvMin, cell, SpritePlain, variant, 0.0);
    return true;
}

// CarSprites.FillFrontTyres for one car: the steered pair at the rack's angle, in TyreModel's wheel order, or none.
uint spritesTyres(uint car, out SpriteOut first, out SpriteOut second)
{
    if (figures.widestTyreM < frame.leastPartM) return 0u;

    CarRow row = cars[car];
    CarLook look = carLooks[row.variant % figures.carSheets];
    if (look.wheelWidthM < frame.leastPartM || spritesOutside(row.positionM, vec2(look.tyreReachM))) return 0u;

    precise float tyreRad = row.headingRad + row.steerRad;
    first = spritesPlain(spritesOnBody(row.positionM, row.forward, look.firstSteeredM), look.tyreHalfM, figures.rubberSheet, tyreRad);
    second = spritesPlain(spritesOnBody(row.positionM, row.forward, look.secondSteeredM), look.tyreHalfM, figures.rubberSheet, tyreRad);
    return 2u;
}

// CarSprites.Fill for one car: its picture, or its wreck's.
bool spritesBody(uint car, out SpriteOut sprite)
{
    if (figures.carSheets == 0u) return false;

    CarRow row = cars[car];
    uint variant = row.variant % figures.carSheets;
    CarLook look = carLooks[variant];
    bool broken = (row.flags & CarBroken) != 0u;
    if (spritesOutside(row.positionM, vec2(broken ? look.wreckReachM : look.bodyReachM))) return false;

    uint sheet = (broken ? figures.carSheets : 0u) + variant;
    sprite = spritesPlain(row.positionM, broken ? look.wreckHalfM : look.bodyHalfM, figures.firstCarSheet + sheet, row.headingRad);
    return true;
}

// CarSprites.FillBeams for one car: its arm, hinged on its deck and pointing at the fork (TowBar.ForkM), or stowed.
bool spritesBeam(uint car, out SpriteOut sprite)
{
    CarRow row = cars[car];
    CarLook look = carLooks[row.variant % figures.carSheets];
    if ((row.flags & CarBroken) != 0u || look.hasBeam == 0u) return false;

    bool towing = row.towing >= 0;
    if (spritesOutside(row.positionM, vec2(towing ? look.extendedReachM : look.collapsedReachM))) return false;

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
    sprite = spritesPlain(
        centreM, towing ? look.extendedHalfM : look.collapsedHalfM, towing ? look.extendedSheet : look.collapsedSheet,
        atan(pointing.y, pointing.x));
    return true;
}

// Selection.Holds over the hands at the wheel, and the second hand's car (CTL-5c, CTL-5d).
bool spritesHandAt(uint car)
{
    if (int(car) == frame.alsoDrivenCar) return true;

    for (uint at = 0u; at < frame.handCount; at++)
    {
        if (hands[at] == car) return true;
    }

    return false;
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

    CarLampFacts facts;
    facts.broken = false;
    facts.driven = (row.flags & CarDriven) != 0u;
    facts.hand = spritesHandAt(car);
    facts.reverse = (row.flags & CarReverse) != 0u;
    facts.blueLight = (row.flags & CarBlueLight) != 0u;
    facts.atWork = (row.flags & CarAtWork) != 0u;
    facts.passBegun = (row.flags & CarPassBegun) != 0u;
    facts.turningAtTheBox = (row.flags & CarTurningAtTheBox) != 0u;
    facts.lineIsReverse = (row.flags & CarLineIsReverse) != 0u;
    facts.brakeMps2 = row.brakeMps2;
    facts.progressM = row.progressM;
    facts.alongMps = row.alongMps;
    facts.passSteppedOutM = row.passSteppedOutM;
    facts.passBackM = row.passBackM;
    facts.passEndsM = row.passEndsM;
    facts.passAsideM = row.passAsideM;
    facts.contextPassAsideM = row.contextPassAsideM;
    facts.toTheBoxM = row.toTheBoxM;
    facts.firstArc = row.firstArc;
    facts.arcCount = row.arcCount;

    CarLampMoment moment = lampsMoment(lampsShowing(facts), frame.elapsedS, row.fuseJitter);
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

// A drawn lens's glow, laid under every lens of its car.
SpriteOut spritesGlow(CarRow row, LensRow lens, uint colour)
{
    vec2 halfM = vec2(lens.glowM * 0.5);
    return spritesQuad(
        spritesOnBody(row.positionM, row.forward, lens.atBodyM), halfM, vec2(0.0), vec2(1.0), lampTints[colour],
        figures.glowSheet, row.headingRad);
}

// And the lens itself, from whichever of its two cells burns that colour (LampAtlas.StateOf).
SpriteOut spritesLens(CarRow row, LensRow lens, uint colour)
{
    vec2 uvMin = colour == (lens.fittingAndFirstColour >> 8) ? lens.firstCellUv : lens.secondCellUv;
    return spritesQuad(
        spritesOnBody(row.positionM, row.forward, lens.atBodyM), vec2(figures.halfLensCellM), uvMin,
        vec2(figures.lensCellU, figures.lensCellV), SpritePlain, figures.lensSheet, row.headingRad);
}

// SignalSprites.Fill for one head: the frame of its strip its approach or crossing is showing.
bool spritesHead(uint head, out SpriteOut sprite)
{
    HeadRow row = heads[head];
    if (spritesOutside(row.centreM, vec2(figures.headReachM))) return false;

    bool forCars = row.forCars != 0u;
    uint colour = signalForStretch(row.subject, forCars, frame.elapsedS);
    float u;
    if (forCars) u = colour == SignalRed ? figures.carHeadRedU : colour == SignalAmber ? figures.carHeadAmberU : figures.carHeadGreenU;
    else u = colour == SignalRed ? figures.walkHeadRedU : colour == SignalAmber ? figures.walkHeadAmberU : figures.walkHeadGreenU;

    vec2 halfM = forCars ? vec2(figures.carHeadHalfX, figures.carHeadHalfY) : vec2(figures.walkHeadHalfX, figures.walkHeadHalfY);
    vec2 cell = vec2(forCars ? figures.carHeadCellU : figures.walkHeadCellU, 1.0);
    sprite = spritesQuad(row.centreM, halfM, vec2(u, 0.0), cell, SpritePlain, figures.firstHeadSheet + (forCars ? 0u : 1u), row.headingRad);
    return true;
}
