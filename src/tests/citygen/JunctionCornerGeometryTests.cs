using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// Which side of a junction's kerb fillet is road, pinned against the ground the town was laid with. The
/// format carries a corner, an arc centre, a radius and two tangent points and says nothing about it
/// — and <b>the fillet, not the junction disc, is where a walker's ground ends</b>,
/// so the walking network's band round a junction is laid off whichever side this says.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P3)]
public class JunctionCornerGeometryTests
{
    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>
    /// <b>Every kerb fillet stands on the kerbs it rounds</b>: a fillet fills the wedge between two
    /// carriageways, so each of its tangent points is a point of the carriageway it is tangent to (TER-5).
    /// </summary>
    /// <remarks>
    /// <b>Asked of the tarmac with the fillets left out of it</b>, or a fillet answers for itself. What it
    /// catches is a corner struck where the two kerb <em>lines</em> cross rather than where the two kerbs
    /// do — behind an arm, where they have not met yet and are still running apart. Such a fillet is a lens
    /// of carriageway hanging off the kerb in the middle of a street, and the pavement wraps it: half a
    /// metre of bulge, two steps in the kerb and a walking lane wandering through both.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryKerbFilletStandsOnTheKerbsItRounds(string map)
    {
        var plan = Towns.Of(map);
        var corners = plan.JunctionCorners;
        var bare = plan.Ground.With(
            plan.Ground.Roads, plan.Ground.Bridges, plan.Ground.Junctions, new CityPlan.JunctionCornerArrays
            {
                CornerM = [], ArcCentreM = [], RadiusM = [], TangentAM = [], TangentBM = [],
            },
            plan.Ground.Roundabouts, plan.Ground.Crosswalks, plan.Ground.StopLines);
        var config = SimConfig.Shipped();
        var lanes = LaneLines.Of(plan.Ground, config);
        var tarmac = Kerbs.Of(
            bare, lanes, BayLines.Lay(bare, lanes, config), RoadCuts.RunsThrough(bare));

        for (var corner = 0; corner < corners.Count; corner++)
        {
            foreach (var tangentM in (ReadOnlySpan<Vector2>)[corners.TangentAM[corner], corners.TangentBM[corner]])
            {
                Assert.True(
                    tarmac.OffTheDrivenM(tangentM) <= Kerbs.OnePlaceM,
                    $"{map}: fillet {corner} at {corners.CornerM[corner]} is tangent to nothing at {tangentM}, "
                    + $"{tarmac.OffTheDrivenM(tangentM):F3} m off the tarmac");
            }
        }
    }

    /// <summary>
    /// <b>A junction disc is not its kerb</b> (TER-5). The disc is the ground its arms share, so it stops
    /// inside the pavement of every road that meets it — anything laid off a disc that reached past one is
    /// laid on the walk. Asked of every junction, because the arithmetic that gets it wrong looks perfectly
    /// reasonable and gets it wrong everywhere at once.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryJunctionDiscStopsInsideTheRoadsThatMeetAtIt(string map)
    {
        var plan = Towns.Of(map);
        var roads = plan.Roads;

        for (var road = 0; road < roads.Count; road++)
        {
            foreach (var junction in (ReadOnlySpan<int>)[roads.FromJunction[road], roads.ToJunction[road]])
            {
                var reachM = (roads.WidthM[road] * 0.5f) + plan.PavementWidthM;
                Assert.True(
                    plan.Junctions.RadiusM[junction] <= reachM,
                    $"{map}: junction {junction} is {plan.Junctions.RadiusM[junction]:F2} m across where road " +
                    $"{road} reaches {reachM:F2} m, so its disc is laid over the walk");
            }
        }
    }
}
