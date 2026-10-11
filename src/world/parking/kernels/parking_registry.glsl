// ParkingRegistry on the device: who stands in each bay, whose leg is aimed at it, who turns in it, and the free bays near
// a place nearest first — named as the C# names them (parking_Claim is ParkingRegistry.Claim). The fields are the
// includer's accessors (town_places.glsl's parking_…, bayStreets_CanBeReached); the bays' grid is walked cell by cell as
// BucketGrid.Query hands its cells back (gridWindow_TryOverlap). The includer defines PARKING_NEAR_ROOM, the room the C#'s
// callers hand BaysNear, and includes rounded.glsl and trig.glsl first.
//
// A hold is first-come in the C#; here two threads may ask for one bay in one dispatch, and the owner is swapped in
// atomically (atomicCompSwap): a race lost is answered as the C# answers a bay already taken.

#ifndef PARKING_REGISTRY_GLSL
#define PARKING_REGISTRY_GLSL

int parking_BayOf(uint car) { return parking_BayOfCar(car); }

int parking_ClaimedBayOf(uint car) { return parking_ClaimedBayOfCar(car); }

int parking_TurnOf(uint car) { return parking_TurnOfCar(car); }

bool parking_CanBeReached(int bay) { return bayStreets_CanBeReached(bay); }

// Free to this car (or to nobody, for ParkingRegistryNobody): nobody in it or on the way, nobody else turning in it or
// holding it, and a way in (GEN-4k).
bool parking_IsFreeFor(int car, int bay)
{
    return parking_CarInBay(bay) == ParkingRegistryNobody && parking_ClaimedBy(bay) == ParkingRegistryNobody
        && bayStreets_CanBeReached(bay)
        && (parking_TurningIn(bay) == ParkingRegistryNobody || parking_TurningIn(bay) == car)
        && (parking_HeldFor(bay) == ParkingRegistryNobody || parking_HeldFor(bay) == car);
}

bool parking_IsFree(int bay) { return parking_IsFreeFor(ParkingRegistryNobody, bay); }

// The ground beside the driver's door of a car standing in this bay, which flank by how it stands (GEN-4e, GEN-4j).
vec2 parking_WayInM(int bay, bool noseIn)
{
    precise vec2 wayInM = parking_CentreM(bay) + (noseIn ? parking_DoorM(bay) : -parking_DoorM(bay));
    return wayInM;
}

// Whether a body this size stands inside a bay at all (CAR-11b).
bool parking_Takes(float lengthM, float widthM) { return lengthM <= parking_SpaceLengthM && widthM <= parking_SpaceWidthM; }

// A bay held out of the town (GEN-4k): for an apron's vehicle not yet stood, for that vehicle, or for a depot's yard (EVA-2).
void parking_HoldTheApron(int bay) { parking_HeldFor(bay) = ParkingRegistryClaimed; }

void parking_HoldForTheCar(int bay, uint car) { parking_HeldFor(bay) = int(car); }

void parking_HoldForTheYard(int bay) { parking_HeldFor(bay) = ParkingRegistryTheYard; }

// The claim given back.
void parking_Release(uint car)
{
    int bay = parking_ClaimedBayOfCar(car);
    if (bay == ParkingRegistryNoBay) return;

    parking_ClaimedBayOfCar(car) = ParkingRegistryNoBay;
    parking_ClaimedBy(bay) = ParkingRegistryNobody;
}

// A bay claimed for a leg, and the one that leg held before it given back. The bay was free to it, so the one it held is
// another.
bool parking_Claim(uint car, int bay)
{
    if (!parking_IsFreeFor(int(car), bay)) return false;
    if (atomicCompSwap(parking_ClaimedBy(bay), ParkingRegistryNobody, int(car)) != ParkingRegistryNobody) return false;

    parking_Release(car);
    parking_ClaimedBayOfCar(car) = bay;
    parking_ClaimedBy(bay) = int(car);
    return true;
}

// The turn given back (GEN-4l).
void parking_LeaveTheTurn(uint car)
{
    int bay = parking_TurnOfCar(car);
    if (bay == ParkingRegistryNoBay) return;

    parking_TurnOfCar(car) = ParkingRegistryNoBay;
    parking_TurningIn(bay) = ParkingRegistryNobody;
}

// A bay a leg turns in (GEN-4l). Taken again by the car already turning in it, it stays the car's, as the C#'s leave and
// take come to.
bool parking_TakeTheTurn(uint car, int bay)
{
    if (!parking_IsFreeFor(int(car), bay)) return false;

    if (parking_TurnOfCar(car) != bay)
    {
        if (atomicCompSwap(parking_TurningIn(bay), ParkingRegistryNobody, int(car)) != ParkingRegistryNobody) return false;

        parking_LeaveTheTurn(car);
    }

    parking_TurnOfCar(car) = bay;
    parking_TurningIn(bay) = int(car);
    return true;
}

void parking_Vacate(uint car)
{
    int bay = parking_BayOfCar(car);
    if (bay == ParkingRegistryNoBay) return;

    parking_BayOfCar(car) = ParkingRegistryNoBay;
    parking_CarInBay(bay) = ParkingRegistryNobody;
}

// A car come to rest in a bay: the claim becomes an occupancy.
void parking_Occupy(int bay, uint car)
{
    parking_Vacate(car);
    parking_Release(car);
    parking_LeaveTheTurn(car);
    parking_BayOfCar(car) = bay;
    parking_CarInBay(bay) = int(car);
}

// Whether a body standing here is in this bay: the bay's own space, on its own axes, against the body's middle (GEN-4i).
bool parking_HoldsTheBody(int bay, vec2 atM)
{
    precise vec2 offsetM = atM - parking_CentreM(bay);
    vec2 forward = trigUnit(parking_HeadingRad(bay));
    vec2 right = vec2(-forward.y, forward.x);
    precise float along = offsetM.x * forward.x + offsetM.y * forward.y;
    precise float across = offsetM.x * right.x + offsetM.y * right.y;
    precise float halfLengthM = parking_SpaceLengthM * 0.5;
    precise float halfWidthM = parking_SpaceWidthM * 0.5;
    return abs(along) <= halfLengthM && abs(across) <= halfWidthM;
}

// The cells of the bays' grid a query of radiusM round a point reads (BucketGrid.Query: the radius widened by the most a
// bay sticks out of its bucket), as (fromX, fromY, toX, toY); false where it reads none.
bool parking_NearRange(vec2 centreM, float radiusM, out ivec4 range)
{
    BucketGridRow grid = parking_Near;
    precise float reachM = radiusM + grid.maxRadiusM;
    precise vec2 leastM = centreM - vec2(reachM);
    precise vec2 mostM = centreM + vec2(reachM);
    return gridWindow_TryOverlap(grid.fromX, grid.fromY, grid.width, grid.height, grid.perCellM, leastM, mostM, range);
}

// The bay whose space a body standing here is in, read off the place and never off the register.
int parking_BayHolding(vec2 atM)
{
    BucketGridRow grid = parking_Near;
    ivec4 range;
    if (!parking_NearRange(atM, parking_SpaceLengthM, range)) return ParkingRegistryNoBay;

    for (int y = range.y; y <= range.w; y++)
    {
        for (int x = range.x; x <= range.z; x++)
        {
            int bucket = ((y - grid.fromY) * grid.width) + (x - grid.fromX);
            int start = parking_NearStart(bucket);
            for (int slot = start; slot < start + parking_NearCount(bucket); slot++)
            {
                int bay = parking_NearItem(slot);
                if (parking_HoldsTheBody(bay, atM)) return bay;
            }
        }
    }

    return ParkingRegistryNoBay;
}

// The free bays within withinM of a place, nearest first, as many as fit (the choice layer); how many.
int parking_BaysNear(vec2 toM, float withinM, out int into[PARKING_NEAR_ROOM])
{
    const int room = int(PARKING_NEAR_ROOM);
    for (int at = 0; at < room; at++) into[at] = ParkingRegistryNoBay;

    int count = 0;
    float distanceM[PARKING_NEAR_ROOM];
    BucketGridRow grid = parking_Near;
    ivec4 range;
    if (!parking_NearRange(toM, withinM, range)) return 0;

    for (int y = range.y; y <= range.w; y++)
    {
        for (int x = range.x; x <= range.z; x++)
        {
            int bucket = ((y - grid.fromY) * grid.width) + (x - grid.fromX);
            int start = parking_NearStart(bucket);
            for (int slot = start; slot < start + parking_NearCount(bucket); slot++)
            {
                int bay = parking_NearItem(slot);
                precise vec2 offM = parking_CentreM(bay) - toM;
                float farM = roundedLength(offM);
                if (farM > withinM) continue;

                int at = count < room ? count : room - 1;
                if (count == room && farM >= distanceM[at]) continue;
                if (!parking_IsFree(bay)) continue;

                while (at > 0 && distanceM[at - 1] > farM)
                {
                    distanceM[at] = distanceM[at - 1];
                    into[at] = into[at - 1];
                    at--;
                }

                distanceM[at] = farM;
                into[at] = bay;
                if (count < room) count++;
            }
        }
    }

    return count;
}

#endif
