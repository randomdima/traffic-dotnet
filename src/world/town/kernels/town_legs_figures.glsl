// What the drive leg, the line and parking read of the town's figures (SimConfig, one row) and count of what they did
// (TownWorld's instruments, one row), by id in the cars' range: legsFigures.carPatienceS is SimConfig.CarPatienceS,
// legsCounts.legsGivenUp TownWorld.LegsGivenUp. Laid once off the town (app/devicetick/TownState.Legs). A count is a
// word, added to atomically; the host's are longs, and take these as what the device added since it laid them.

#ifndef TOWN_LEGS_FIGURES_GLSL
#define TOWN_LEGS_FIGURES_GLSL

#include "world/town/kernels/town_root.glsl"

struct LegsFigures
{
    float stopSpeedMps;
    float carPatienceS;
    int reroutesPerLeg;
    float lightQueueInCarLengths;
    float carBlockedWayPriceM;
    float carBlockedWayLifeS;
    float personWalkWorthM;
    float turnAtALotWithinM;
    float projectionWindowInCarLengths;
    float carOffPathM;
    float gripMargin;
    float brakingMargin;
    float turnPriceNearSideCarLengths;
    float turnPriceAcrossOncomingCarLengths;
    float nominalCarLengthM;
    float laneSwitchPriceCarLengths;
};

struct LegsCounts
{
    uint legsGivenUp;
    uint reroutesTaken;
    uint placesGivenUp;
    uint turnsAtALotBegun;
    uint linesReacquired;
    uint baysParkedIn;
    uint routeArrivals;
    uint routeSearches;
};

layout(buffer_reference, std430, buffer_reference_align = 4) buffer LegsFiguresRows { LegsFigures r[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer LegsCountsRows { LegsCounts r[]; };

const uint LegsFiguresId = 171u;
const uint LegsCountsId = 172u;

#define legsFigures TOWN(LegsFiguresRows, LegsFiguresId).r[0]
#define legsCounts TOWN(LegsCountsRows, LegsCountsId).r[0]

#endif
