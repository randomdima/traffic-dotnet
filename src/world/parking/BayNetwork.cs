using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Parking;

/// <summary>
/// <b>The bays as <see cref="IWayNetwork"/></b>: every way a bay is worked off is a lane of its own, and the
/// bay itself is the node they all end at — the town's third network, read in the same words the carriageway
/// and the pavement are, so that one walk lays a body onto whichever of them it is standing on (TER-4c.2,
/// <see cref="GroundUnder"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>A lane here is numbered in the road's own way numbers</b> (<see cref="LaneOccupancy.WayOfLane"/>),
/// because a bay's ways are numbered beside its lanes and its joins (<see cref="BayWays.FirstWay"/>)
/// and <see cref="BayWays"/> has always spoken in them. So the numbering stays the road's and this network
/// needs none of its own.
/// </para>
/// <para>
/// <b>The bay is the node and there are no joins</b> (<see cref="TurnsFrom"/>). Every way of a bay ends at
/// the one pose, so a body standing there is on all of them — which is what a car parked in a bay has always
/// held, and it now holds it for the reason every other body holds ground rather than by an arithmetic of its
/// own. The other end of a way is the carriageway, which is a node of a different network: it is
/// <see cref="NoNode"/> here, and what the body standing there holds of the street is the road's own walk to
/// say.
/// </para>
/// <para>
/// <b>The band is the bay's width and not the lane's</b> (<see cref="SimConfig.ParkingSpaceWidthM"/>). A
/// bay's way opens on the carriageway, so measured at a lane's width every car driving past a frontage would
/// be standing on every bay it passed; measured at the space it serves, what is on it is what is in it or
/// across its mouth.
/// </para>
/// </remarks>
internal readonly struct BayNetwork(BayWays bays, RoadGraph roads, float spaceWidthM) : IWayNetwork
{
    /// <summary>The carriageway end of a way, which belongs to the road's network and not to this one.</summary>
    const int NoNode = -1;

    /// <summary>
    /// <b>A bay's ways are numbered in the town's numbering to begin with</b> (<see cref="BayWays.WayOf"/>),
    /// so this is the identity — the walk over this network deals in way numbers throughout.
    /// </summary>
    public int WayOfLane(int lane) => lane;

    /// <summary>
    /// Nothing turns at a bay (<see cref="TurnsFrom"/>), so this is never asked and the identity is the only
    /// answer that could not be wrong.
    /// </summary>
    public int WayOfTurn(int slot) => slot;

    /// <summary>
    /// <b>The way of the nearest bay to a place</b>, which the walk then reads that bay's other ways off.
    /// <see cref="BayWays.NoWay"/> where the stretch this place is beside has no bays at all, which is most of
    /// the town.
    /// </summary>
    /// <remarks>
    /// <b>Compared against the poses and not against the lines</b> (<see cref="BayWays.AtTheBayM"/>). Every
    /// way of a bay is a dozen metres of arc reaching out to the carriageway, and projecting a place onto all
    /// of them to find out which bay it is at is the whole cost of this walk done twice; the pose each one
    /// ends at is a point, and the band test is what settles the question either way.
    /// </remarks>
    public int NearestLane(Vector2 atM, out float alongM)
    {
        alongM = 0f;

        var lane = roads.NearestLane(atM, out _);
        if (lane < 0) return BayWays.NoWay;

        var nearest = BayWays.NoWay;
        var nearestM2 = float.MaxValue;
        Nearest(lane, atM, ref nearest, ref nearestM2);

        // And the bays of the other side of the same street, which are as near the middle of it as these are.
        var back = roads.LaneReverse[lane];
        if (back >= 0) Nearest(back, atM, ref nearest, ref nearestM2);

        if (nearest == BayWays.NoWay) return BayWays.NoWay;

        alongM = Spline.ProjectM(bays.ArcsOf(nearest), atM, AtTheBayM(nearest), bays.LengthM(nearest));
        return nearest;
    }

    public int Reverse(int lane) => bays.PairOf(lane);

    public float LaneLengthM(int lane) => bays.LengthM(lane);

    public float LaneWidthM(int lane) => spaceWidthM;

    public ReadOnlySpan<ArcSeg> ArcsOf(int lane) => bays.ArcsOf(lane);

    public float JoinedAtM(int lane) => 0f;

    /// <summary>
    /// <b>How far short of a way's own end the drive leaves it</b> (TER-5d): the run past the pose that a
    /// way in carries and a way out has none of (<see cref="BayWays.DrivenLengthM"/>). So a body standing
    /// anywhere from the pose to the back of the space is read as being at the bay, and the walk writes it
    /// onto every way the bay is worked off rather than onto the one it happens to be nearest.
    /// </summary>
    public float LeftAtM(int lane) => bays.LengthM(lane) - bays.DrivenLengthM(lane);

    public int FromNode(int lane) => bays.IsEntry(lane) ? NoNode : bays.BayOfWay(lane);

    public int ToNode(int lane) => bays.IsEntry(lane) ? bays.BayOfWay(lane) : NoNode;

    public ReadOnlySpan<int> LanesIn(int node) => bays.WaysOf(node);

    public ReadOnlySpan<int> LanesOut(int node) => bays.WaysOf(node);

    /// <summary>Nothing is driven from one bay to the next: a bay is where a way ends and never a place it passes through.</summary>
    public ReadOnlySpan<int> TurnsFrom(int lane) => [];

    public int TurnSlotAt(int lane, int turn) => NoNode;

    public ReadOnlySpan<ArcSeg> JoinArcs(int slot) => [];

    public float JoinLengthM(int slot) => 0f;

    public int MostWaysUnderAPlace => GroundUnder.MostWaysUnderAPlace(0, bays.MostWaysAtABay);

    /// <summary>
    /// Where the pose in the bay falls in a way's own metres (GEN-4f) — the far end of a way in, less the
    /// run past it, and the near end of a way out. It seeds the projection, so it is the place bodies are
    /// actually clustered and not merely an end.
    /// </summary>
    float AtTheBayM(int way) => bays.IsEntry(way) ? bays.DrivenLengthM(way) : 0f;

    void Nearest(int lane, Vector2 atM, ref int nearest, ref float nearestM2)
    {
        foreach (var bay in bays.BaysOffLane(lane))
        {
            foreach (var way in bays.WaysOf(bay))
            {
                var reachM2 = (atM - bays.AtTheBayM(way)).LengthSquared();
                if (reachM2 >= nearestM2) continue;

                nearestM2 = reachM2;
                nearest = way;
            }
        }
    }
}
