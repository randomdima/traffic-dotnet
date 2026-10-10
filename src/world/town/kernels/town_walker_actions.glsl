// What each walker's actions keep of it between ticks (agents/person/actions), by id (1152–1279) and by its C# name:
// sidestepping_LooksInS(p) is Sidestepping._looksInS[p]. Laid once off the town (app/devicetick/TownState.WalkerActions).
//
// Much of it a walk row already holds (walker_rows.glsl's WalkerRow, town_rows.glsl's people_Walk), and is named here
// over that row rather than kept twice: the pass's swept runs and how many (people_PassRun, passRuns), the straight's
// (people_StraightRun, straightRuns), where it walks back to, the straight's ends, its hold and whose ground it is laid as.
// Two of those are kept as the row keeps them and not as the C#: a straight never committed to is the row's
// WalkerOnAStraight flag clear, where the C# has its end a NaN, and the roster a walker is laid as is the row's
// WalkerHeldAsDriving flag (driving, else walking).

#ifndef TOWN_WALKER_ACTIONS_GLSL
#define TOWN_WALKER_ACTIONS_GLSL

#include "world/town/kernels/town_tables.glsl"

// Sidestepping.MostRunsOfAPass.
const uint SidesteppingMostRunsOfAPass = 32u;

// SidestepLeg: which leg of a pass a walker is on (Sidestep.LegAt).
const uint SidestepLegAcross = 0u;
const uint SidestepLegAlong = 1u;
const uint SidestepLegBack = 2u;
const uint SidestepLegOver = 3u;

// A walker's pass (Sidestep): which way of its route it asked on and how far along, how far down it is clear, how far
// across, and whether it is begun.
struct Sidestep
{
    int slot;
    float fromM;
    float clearM;
    float asideM;
    uint begun;
};

// The walkers' actions' counts since the town was laid.
struct WalkerActionsCounts
{
    int64_t sidesteppingAsked;
    int64_t sidesteppingWithdrawn;
    int64_t sidesteppingMade;
};

layout(buffer_reference, std430, buffer_reference_align = 4) buffer SidestepRows { Sidestep r[]; };
layout(buffer_reference, std430, buffer_reference_align = 8) buffer WalkerActionsCountsRows { WalkerActionsCounts r[]; };

const uint SidesteppingDecidedId = 1152u;
const uint SidesteppingSweptStationAtMId = 1153u;
const uint SidesteppingAskedRunsId = 1154u;
const uint SidesteppingAskedCountId = 1155u;
const uint SidesteppingAskedStationAtMId = 1156u;
const uint SidesteppingPassedId = 1157u;
const uint SidesteppingLooksInSId = 1158u;
const uint SidesteppingWaitedSId = 1159u;
const uint SidesteppingDecidesInSId = 1160u;
const uint WalkingGroundStraightStationAtMId = 1161u;
const uint WalkerActionsCountsId = 1162u;

// Sidestepping: the pass decided, the ground it sweeps (runs and count in the walk row) and the ground it asks for, the
// body it passes as a claim row whose way is the one it was met on (_passedOn), and its clocks.
#define sidestepping_Decided(p) TOWN(SidestepRows, SidesteppingDecidedId).r[p]
#define sidestepping_SweptRun(p, i) people_PassRun(p, i)
#define sidestepping_SweptCount(p) people_Walk(p).passRuns
#define sidestepping_SweptStationAtM(p) TOWN(Floats, SidesteppingSweptStationAtMId).v[p]
#define sidestepping_AskedRun(p, i) TOWN(SweptRunRows, SidesteppingAskedRunsId).r[(p) * SidesteppingMostRunsOfAPass + (i)]
#define sidestepping_AskedCount(p) TOWN(Ints, SidesteppingAskedCountId).v[p]
#define sidestepping_AskedStationAtM(p) TOWN(Floats, SidesteppingAskedStationAtMId).v[p]
#define sidestepping_Passed(p) TOWN(ClaimRows, SidesteppingPassedId).r[p]
#define sidestepping_PassedOn(p) TOWN(ClaimRows, SidesteppingPassedId).r[p].way
#define sidestepping_LooksInS(p) TOWN(Floats, SidesteppingLooksInSId).v[p]
#define sidestepping_WaitedS(p) TOWN(Floats, SidesteppingWaitedSId).v[p]
#define sidestepping_DecidesInS(p) TOWN(Floats, SidesteppingDecidesInSId).v[p]

// WalkingGround: the straight committed to (runs and count in the walk row, its ends there too), the hold laid, and
// whose ground the walker's body is laid as.
#define walkingGround_StraightRun(p, i) people_StraightRun(p, i)
#define walkingGround_StraightCount(p) people_Walk(p).straightRuns
#define walkingGround_StraightStationAtM(p) TOWN(Floats, WalkingGroundStraightStationAtMId).v[p]
#define walkingGround_StraightFromM(p) people_Walk(p).straightFromM
#define walkingGround_StraightToM(p) people_Walk(p).straightToM
#define walkingGround_WalkHold(p) people_Walk(p).hold
#define walkingGround_HeldAs(p) people_Walk(p).heldAs

// WalkingBack: where a walker rejoining walks back to.
#define walkingBack_BackToM(p) people_Walk(p).backToM

#define walkerActionsCounts TOWN(WalkerActionsCountsRows, WalkerActionsCountsId).r[0]
#define sidestepping_Asked walkerActionsCounts.sidesteppingAsked
#define sidestepping_Withdrawn walkerActionsCounts.sidesteppingWithdrawn
#define sidestepping_Made walkerActionsCounts.sidesteppingMade

#endif
