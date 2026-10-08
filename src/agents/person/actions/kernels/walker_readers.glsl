// What the town's readers read of a walker's hold (WalkingGround.WalkHold, LaneOccupancy.HoldEndsAtM and HoldCutOn), copied
// where they keep up with the device's claims (walkers_readers.comp) and asked by its decision (walker_decide.glsl): whether
// it laid one, whether anything cut it, the way it was cut on, and the claim that cut it. The includer includes
// claims_rows.glsl first.

#ifndef WALKER_READERS_GLSL
#define WALKER_READERS_GLSL

struct WalkerReadersHold
{
    uint bits;
    int cutOn;
    uint pad0;
    uint pad1;
    ClaimRow cutter;
};

const uint ReadersHasAHold = 1u;
const uint ReadersCut = 2u;

#endif
