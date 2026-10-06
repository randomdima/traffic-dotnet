// Shape.cs on the device: the one shape (SOL-1), a core box of half-extents grown by a corner radius, and the manifold
// two of them make. Written once for the general shape, with the two closed forms that cover most of a town's pairs —
// each line follows its C# counterpart so a change to one is found in the other. `precise` throughout: a fused
// multiply-add rounds once where the host rounds twice.

struct Manifold
{
    vec2 normal;
    vec2 point0;
    vec2 point1;
    float separation0;
    float separation1;
    int pointCount;
};

// How far apart the separating axis has to put two boxes before they are answered by where they are nearest
// (Shape.ApartM).
const float ShapeApartM = 0.0005;

vec2 shapeRotate(vec2 rotation, vec2 v)
{
    precise vec2 r = vec2(rotation.x * v.x - rotation.y * v.y, rotation.y * v.x + rotation.x * v.y);
    return r;
}

vec2 shapeInverseRotate(vec2 rotation, vec2 v)
{
    precise vec2 r = vec2(rotation.x * v.x + rotation.y * v.y, rotation.x * v.y - rotation.y * v.x);
    return r;
}

vec2 shapeLeftPerpendicular(vec2 v) { return vec2(-v.y, v.x); }

float shapeDot(vec2 a, vec2 b)
{
    precise float d = a.x * b.x + a.y * b.y;
    return d;
}

float shapeCross(vec2 a, vec2 b)
{
    precise float c = a.x * b.y - a.y * b.x;
    return c;
}

bool shapeIsPoint(vec2 extentM) { return extentM.x <= 0.0 && extentM.y <= 0.0; }

Manifold noManifold()
{
    Manifold m;
    m.normal = vec2(0.0);
    m.point0 = vec2(0.0);
    m.point1 = vec2(0.0);
    m.separation0 = 0.0;
    m.separation1 = 0.0;
    m.pointCount = 0;
    return m;
}

bool shapeTwoDiscs(vec2 centreA, float radiusA, vec2 centreB, float radiusB, float marginM, out Manifold m)
{
    m = noManifold();
    precise vec2 between = centreB - centreA;
    precise float apartSquared = between.x * between.x + between.y * between.y;
    precise float reachM = radiusA + radiusB + marginM;
    precise float reachSquared = reachM * reachM;
    if (apartSquared > reachSquared) return false;

    float apartM = sqrt(apartSquared);
    m.normal = apartM > 1e-6 ? between / apartM : vec2(1.0, 0.0);
    precise float separation = apartM - radiusA - radiusB;
    m.separation0 = separation;
    precise vec2 point = centreA + m.normal * (radiusA + separation * 0.5);
    m.point0 = point;
    m.pointCount = 1;
    return true;
}

// The normal points from the disc towards the box (Shape.DiscAndBox).
bool shapeDiscAndBox(
    vec2 centre, float discRadiusM, vec2 boxCentre, vec2 boxRotation, vec2 half_, float cornerRadiusM, float marginM,
    out Manifold m)
{
    m = noManifold();
    precise float radiusM = discRadiusM + cornerRadiusM;
    vec2 local = shapeInverseRotate(boxRotation, centre - boxCentre);
    vec2 nearest = vec2(clamp(local.x, -half_.x, half_.x), clamp(local.y, -half_.y, half_.y));

    precise vec2 outward = local - nearest;
    precise float outwardSquared = outward.x * outward.x + outward.y * outward.y;
    if (outwardSquared > 1e-12)
    {
        float apartM = sqrt(outwardSquared);
        precise float separationM = apartM - radiusM;
        if (separationM > marginM) return false;

        m.normal = -shapeRotate(boxRotation, outward / apartM);
        m.separation0 = separationM;
        precise vec2 point = ((boxCentre + shapeRotate(boxRotation, nearest) + centre + m.normal * radiusM) * 0.5)
                             - (m.normal * cornerRadiusM);
        m.point0 = point;
        m.pointCount = 1;
        return true;
    }

    precise float alongX = half_.x - abs(local.x);
    precise float alongY = half_.y - abs(local.y);
    vec2 outLocal = alongX < alongY
        ? vec2(local.x >= 0.0 ? 1.0 : -1.0, 0.0)
        : vec2(0.0, local.y >= 0.0 ? 1.0 : -1.0);

    m.normal = -shapeRotate(boxRotation, outLocal);
    precise float separation = -(min(alongX, alongY) + radiusM);
    m.separation0 = separation;
    m.point0 = centre;
    m.pointCount = 1;
    return true;
}

// The reference box's most separating own axis against the other (Shape.MaxSeparation).
float shapeMaxSeparation(
    vec2 referenceCentre, vec2 referenceRotation, vec2 referenceHalf, vec2 otherCentre, vec2 otherRotation,
    vec2 otherHalf, out vec2 normal, out int axis)
{
    vec2 referenceX = referenceRotation;
    vec2 referenceY = shapeLeftPerpendicular(referenceRotation);
    vec2 otherX = otherRotation;
    vec2 otherY = shapeLeftPerpendicular(otherRotation);
    precise vec2 between = otherCentre - referenceCentre;

    float acrossX = shapeDot(between, referenceX);
    precise float otherReachX = otherHalf.x * abs(shapeDot(referenceX, otherX)) + otherHalf.y * abs(shapeDot(referenceX, otherY));
    precise float separationX = abs(acrossX) - referenceHalf.x - otherReachX;

    float acrossY = shapeDot(between, referenceY);
    precise float otherReachY = otherHalf.x * abs(shapeDot(referenceY, otherX)) + otherHalf.y * abs(shapeDot(referenceY, otherY));
    precise float separationY = abs(acrossY) - referenceHalf.y - otherReachY;

    if (separationX >= separationY)
    {
        normal = acrossX >= 0.0 ? referenceX : -referenceX;
        axis = 0;
        return separationX;
    }

    normal = acrossY >= 0.0 ? referenceY : -referenceY;
    axis = 1;
    return separationY;
}

void shapeCorners(vec2 centreM, vec2 rotation, vec2 half_, out vec2 corners[4])
{
    vec2 along = shapeRotate(rotation, vec2(half_.x, 0.0));
    vec2 across = shapeRotate(rotation, vec2(0.0, half_.y));
    precise vec2 c0 = centreM + along + across;
    precise vec2 c1 = centreM - along + across;
    precise vec2 c2 = centreM - along - across;
    precise vec2 c3 = centreM + along - across;
    corners[0] = c0;
    corners[1] = c1;
    corners[2] = c2;
    corners[3] = c3;
}

vec2 shapeOnSegment(vec2 fromM, vec2 toM, vec2 pointM)
{
    precise vec2 along = toM - fromM;
    precise float lengthSquared = along.x * along.x + along.y * along.y;
    if (lengthSquared < 1e-12) return fromM;

    precise vec2 on = fromM + along * clamp(shapeDot(pointM - fromM, along) / lengthSquared, 0.0, 1.0);
    return on;
}

// Two disjoint cores, nearest corner against side either way (Shape.Nearest).
bool shapeNearest(
    vec2 centreA, vec2 rotationA, vec2 halfA, float cornerA, vec2 centreB, vec2 rotationB, vec2 halfB, float cornerB,
    out Manifold m)
{
    m = noManifold();
    vec2 cornersA[4];
    vec2 cornersB[4];
    shapeCorners(centreA, rotationA, halfA, cornersA);
    shapeCorners(centreB, rotationB, halfB, cornersB);

    float apartSquared = 3.402823466e+38;
    vec2 onA = vec2(0.0);
    vec2 onB = vec2(0.0);
    for (int corner = 0; corner < 4; corner++)
    {
        for (int side = 0; side < 4; side++)
        {
            vec2 first = cornersA[corner];
            vec2 second = shapeOnSegment(cornersB[side], cornersB[(side + 1) & 3], cornersA[corner]);
            precise vec2 gap = first - second;
            precise float between = gap.x * gap.x + gap.y * gap.y;
            if (between < apartSquared)
            {
                apartSquared = between;
                onA = first;
                onB = second;
            }

            first = shapeOnSegment(cornersA[side], cornersA[(side + 1) & 3], cornersB[corner]);
            second = cornersB[corner];
            gap = first - second;
            between = gap.x * gap.x + gap.y * gap.y;
            if (between < apartSquared)
            {
                apartSquared = between;
                onA = first;
                onB = second;
            }
        }
    }

    float apartM = sqrt(apartSquared);
    if (apartM < 1e-5) return false;

    m.normal = (onB - onA) / apartM;
    precise float separation = apartM - cornerA - cornerB;
    m.separation0 = separation;
    precise vec2 point = ((onA + onB) * 0.5) + (m.normal * ((cornerA - cornerB) * 0.5));
    m.point0 = point;
    m.pointCount = 1;
    return true;
}

// One clipped point, kept only where it reaches the reference face (Shape.Add).
void shapeAdd(inout Manifold m, vec2 pointM, vec2 faceCentre, vec2 normal, float referenceCorner, float incidentCorner, float marginM)
{
    float coreM = shapeDot(pointM - faceCentre, normal);
    precise float separationM = coreM - referenceCorner - incidentCorner;
    if (separationM > marginM) return;

    precise vec2 at = pointM - normal * ((coreM + incidentCorner - referenceCorner) * 0.5);
    if (m.pointCount == 0)
    {
        m.point0 = at;
        m.separation0 = separationM;
    }
    else
    {
        m.point1 = at;
        m.separation1 = separationM;
    }

    m.pointCount++;
}

bool shapeRoundedBoxes(
    vec2 centreA, vec2 rotationA, vec2 halfA, float cornerA, vec2 centreB, vec2 rotationB, vec2 halfB, float cornerB,
    float marginM, out Manifold m)
{
    m = noManifold();
    precise float coreMarginM = marginM + cornerA + cornerB;

    vec2 normalA;
    int axisA;
    float separationA = shapeMaxSeparation(centreA, rotationA, halfA, centreB, rotationB, halfB, normalA, axisA);
    if (separationA > coreMarginM) return false;

    vec2 normalB;
    int axisB;
    float separationB = shapeMaxSeparation(centreB, rotationB, halfB, centreA, rotationA, halfA, normalB, axisB);
    if (separationB > coreMarginM) return false;

    if (max(separationA, separationB) > ShapeApartM
        && shapeNearest(centreA, rotationA, halfA, cornerA, centreB, rotationB, halfB, cornerB, m))
    {
        if (m.separation0 <= marginM) return true;

        m = noManifold();
        return false;
    }

    bool flipped = separationB > separationA;

    vec2 referenceCentre = flipped ? centreB : centreA;
    vec2 referenceHalf = flipped ? halfB : halfA;
    int referenceAxis = flipped ? axisB : axisA;
    float referenceCorner = flipped ? cornerB : cornerA;
    vec2 normal = flipped ? normalB : normalA;

    vec2 incidentCentre = flipped ? centreA : centreB;
    vec2 incidentRotation = flipped ? rotationA : rotationB;
    vec2 incidentHalf = flipped ? halfA : halfB;
    float incidentCorner = flipped ? cornerA : cornerB;

    float referenceReachM = referenceAxis == 0 ? referenceHalf.x : referenceHalf.y;
    float referenceRunM = referenceAxis == 0 ? referenceHalf.y : referenceHalf.x;
    precise vec2 faceCentre = referenceCentre + normal * referenceReachM;
    vec2 along = shapeLeftPerpendicular(normal);

    vec2 incidentX = incidentRotation;
    vec2 incidentY = shapeLeftPerpendicular(incidentRotation);
    float facingX = shapeDot(normal, incidentX);
    float facingY = shapeDot(normal, incidentY);

    vec2 incidentNormal;
    float incidentReachM;
    float incidentRunM;
    if (abs(facingX) > abs(facingY))
    {
        incidentNormal = facingX > 0.0 ? -incidentX : incidentX;
        incidentReachM = incidentHalf.x;
        incidentRunM = incidentHalf.y;
    }
    else
    {
        incidentNormal = facingY > 0.0 ? -incidentY : incidentY;
        incidentReachM = incidentHalf.y;
        incidentRunM = incidentHalf.x;
    }

    vec2 incidentAlong = shapeLeftPerpendicular(incidentNormal);
    precise vec2 incidentMiddle = incidentCentre + incidentNormal * incidentReachM;
    precise vec2 first = incidentMiddle - incidentAlong * incidentRunM;
    precise vec2 second = incidentMiddle + incidentAlong * incidentRunM;

    float faceAt = shapeDot(faceCentre, along);
    precise float least = faceAt - referenceRunM;
    precise float most = faceAt + referenceRunM;
    float firstAt = shapeDot(first, along);
    precise float span = shapeDot(second, along) - firstAt;

    float fromFraction = 0.0;
    float toFraction = 1.0;
    if (abs(span) > 1e-9)
    {
        precise float atLeast = (least - firstAt) / span;
        precise float atMost = (most - firstAt) / span;
        fromFraction = max(fromFraction, min(atLeast, atMost));
        toFraction = min(toFraction, max(atLeast, atMost));
    }
    else if (firstAt < least || firstAt > most)
    {
        return false;
    }

    if (fromFraction > toFraction) return false;

    precise vec2 edge = second - first;
    precise vec2 from_ = first + edge * fromFraction;
    shapeAdd(m, from_, faceCentre, normal, referenceCorner, incidentCorner, marginM);
    precise float run = toFraction - fromFraction;
    if (run > 1e-6)
    {
        precise vec2 to_ = first + edge * toFraction;
        shapeAdd(m, to_, faceCentre, normal, referenceCorner, incidentCorner, marginM);
    }

    if (m.pointCount == 0) return false;

    m.normal = flipped ? -normal : normal;
    return true;
}

// Where two shapes meet, or false where they are further apart than the margin; the normal from A to B
// (Shape.Collide).
bool shapeCollide(
    vec2 positionA, vec2 rotationA, vec2 extentA, float cornerA, vec2 positionB, vec2 rotationB, vec2 extentB,
    float cornerB, float marginM, out Manifold m)
{
    bool pointA = shapeIsPoint(extentA);
    bool pointB = shapeIsPoint(extentB);

    if (pointA && pointB) return shapeTwoDiscs(positionA, cornerA, positionB, cornerB, marginM, m);

    if (pointA) return shapeDiscAndBox(positionA, cornerA, positionB, rotationB, extentB, cornerB, marginM, m);

    if (pointB)
    {
        bool met = shapeDiscAndBox(positionB, cornerB, positionA, rotationA, extentA, cornerA, marginM, m);
        m.normal = -m.normal;
        return met;
    }

    return shapeRoundedBoxes(positionA, rotationA, extentA, cornerA, positionB, rotationB, extentB, cornerB, marginM, m);
}
