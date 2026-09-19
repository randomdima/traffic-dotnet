using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The zebras the town is painted with: which road ends carry one, and where the band lies. What one
/// <em>looks</em> like — the stripes and their pitch — is the mesh's (<c>GroundMeshTests</c>), and where the
/// pedestrian nodes it is laid between stand is <see cref="PedestrianNodesTests"/>'.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class CrossingsTests
{
    /// <summary>
    /// <b>A zebra stands on every road end whose two pedestrian nodes hand a crossing over to each other,
    /// and on no other</b> (TER-6, WLK-10). Nothing else decides it: not whether the junction behind it
    /// forks, not how much road is left behind the paint, and not the arm's own end.
    /// </summary>
    [Fact]
    public void AZebraStandsWhereTwoPedestrianNodesCrossToEachOtherAndNowhereElse()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var connectors = FootConnectors.Lay(plan, config);
        var crossings = Crossings.Lay(plan, config, connectors.Crossed());

        var wanted = 0;
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            foreach (var atTo in (ReadOnlySpan<bool>)[false, true])
            {
                var end = JunctionArms.End(road, atTo);
                var crosses = Crosses(connectors, FootJunctions.Node(end, -1))
                              && Crosses(connectors, FootJunctions.Node(end, +1));

                Assert.Equal(crosses, crossings.At(road, atTo) != Crossings.None);
                if (crosses) wanted++;
            }
        }

        // The staging and not the claim: a fixture with no pavement would ask nothing above, and pass.
        Assert.NotEqual(0, wanted);
        Assert.Equal(wanted, crossings.Count);
    }

    /// <summary>
    /// <b>A zebra runs from the kerb one of its nodes stands off to the kerb the other does</b> (TER-6):
    /// the band's middle is the two of them, and what it reaches across is the distance between them — the
    /// carriageway's width where the kerb runs straight, and more at a mouth where the ground a junction's
    /// movements are driven over reaches past the arm's own edge.
    /// </summary>
    [Fact]
    public void AZebraRunsBetweenTheTwoKerbsItsNodesStandOff()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var connectors = FootConnectors.Lay(plan, config);
        var crossings = Crossings.Lay(plan, config, connectors.Crossed());

        var checked_ = 0;
        for (var crossing = 0; crossing < crossings.Count; crossing++)
        {
            var end = crossings.End[crossing];
            var nearM = connectors.KerbAtM(FootJunctions.Node(end, -1));
            var farM = connectors.KerbAtM(FootJunctions.Node(end, +1));

            checked_++;
            Assert.Equal(
                0f,
                Vector2.Distance((nearM + farM) * 0.5f, crossings.CentreM[crossing]),
                LineTolerance.RoundingM);
            Assert.Equal(Vector2.Distance(nearM, farM), crossings.SpanM[crossing], LineTolerance.RoundingM);

            // And it is laid square to that run, which is what puts the stripes across the traffic.
            Assert.Equal(
                0f,
                Vector2.Dot(crossings.Axis[crossing], Vector2.Normalize(farM - nearM)),
                LineTolerance.RoundingM);
        }

        Assert.NotEqual(0, checked_);
    }

    /// <summary>
    /// <b>A zebra reaches the carriageway it crosses on both hands</b> (TER-6): it is at least as wide as
    /// the road it is painted on, since the kerbs it runs between are that road's own edges or further out.
    /// </summary>
    [Fact]
    public void AZebraReachesAtLeastItsRoadsWidth()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var crossings = Crossings.Lay(plan, config, FootConnectors.Lay(plan, config).Crossed());

        for (var crossing = 0; crossing < crossings.Count; crossing++)
        {
            var widthM = plan.Roads.WidthM[crossings.Road[crossing]];
            Assert.True(
                crossings.SpanM[crossing] >= widthM - LineTolerance.RoundingM,
                $"a zebra reaches {crossings.SpanM[crossing]:F2} m over a {widthM:F2} m carriageway at " +
                $"{crossings.CentreM[crossing].X:F0},{crossings.CentreM[crossing].Y:F0}");
        }
    }

    static bool Crosses(FootConnectors connectors, int node) =>
        connectors.StandsAt(node) && connectors.HandsOver(node, FootConnectorKind.Crossing);
}
