using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// The one bound PER-24 puts on where a step may land, against a lane and a point — no claims, no town and
/// nobody in the way, because this is the half of the step that is about ground rather than about bodies.
/// </summary>
/// <remarks>
/// Where the step goes is the grant's and is asked of a running town
/// (<see cref="TrafficSimulation.Tests.World.FootOccupancyTests"/>, <c>StepRoundTests</c>): a step is the
/// walk asked for again from an offset across the way, so there is no geometry here to check it against.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class StepAroundTests
{
    /// <summary>
    /// <b>The kerb is a line to be grazed and not a wall.</b> A step round a body standing on a pavement
    /// lane's own line reaches a quarter of a body past the kerb, so a rule that refused the carriageway
    /// outright would turn nearly every step in the town the other way — and the graze is what the step is
    /// short by, with the middle of the body still at the channel.
    /// </summary>
    [Theory]
    [InlineData(0.25f, true)]
    [InlineData(-0.25f, true)]
    [InlineData(-1f, false)]
    [InlineData(-2f, false)]
    public void ACarriagewayIsGrazedAndNeverEntered(float fromTheKerbM, bool clear)
    {
        var lane = Roads.NearestLane(Middle, out var alongM);
        var on = Spline.SampleAt(Roads.ArcsOf(lane), alongM);

        // Out from the middle of the lane to its own kerb line, and then the distance being asked about:
        // positive is the pavement side of it and negative is into the traffic.
        var atM = on.PositionM + (on.Right * ((Roads.LaneWidthM[lane] * 0.5f) + fromTheKerbM));

        Assert.Equal(clear, StepAround.IsClearOfTheTraffic(Roads, atM, Config.PersonRoadGrazeM));
    }

    /// <summary>Ground no lane is anywhere near is a walker's to step onto, which is most of a town.</summary>
    [Fact]
    public void GroundAwayFromEveryLaneIsClear() =>
        Assert.True(StepAround.IsClearOfTheTraffic(Roads, Middle + new Vector2(0f, 400f), Config.PersonRoadGrazeM));

    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly RoadGraph Roads = RoadGraph.Build(Towns.Of(TrackPlan.Name), Config);

    /// <summary>The middle of the proving ground's own straight, which is a lane with a kerb either side.</summary>
    static Vector2 Middle
    {
        get
        {
            var lap = TrackPlan.Lap()[TrackPlan.Straight];
            var arcs = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lap);
            return Spline.SampleAt(arcs, Spline.TotalLengthM(arcs) * 0.5f).PositionM;
        }
    }
}
