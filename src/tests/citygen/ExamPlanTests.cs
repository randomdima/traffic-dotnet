using System.Numerics;
using TrafficSimulation.CityGen.Exam;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>The scenario map is laid as its cards ask</b>: every card on a junction of the shape it is about, every
/// car it stages standing in the lane it is about to drive and sent to one it can reach, everybody walking a
/// pavement standing clear of every lane, and every lit junction holding its traffic at a bar. Whether the
/// town then drives the cards as they expect is the map's own tier (<c>ExamTests</c>); this is whether the
/// questions were asked at all.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P3)]
public class ExamPlanTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Lazy<ExamLattice> Lattice = new(() => ExamLattice.Of(Config));

    /// <summary>
    /// <b>A card is staged at the shape it is about</b>: a crossroads or a ring where four roads meet, a T on
    /// an edge with its missing arm where the card's frame put it, and a dead end at the head of a spur.
    /// </summary>
    [Fact]
    public void EveryCardStandsOnAJunctionOfTheShapeItAsks()
    {
        var lattice = Lattice.Value;
        var ground = lattice.Ground;
        for (var card = 0; card < lattice.Cards; card++)
        {
            var of = lattice.Card(card);
            var cell = lattice.CellOf(card);
            switch (of.Shape)
            {
                case ExamShape.Crossroads or ExamShape.Roundabout:
                    Assert.True(Arms(ground, cell) == 4, $"\"{of.Name}\" is a {of.Shape} on a cell of {Arms(ground, cell)} arms");
                    Assert.Equal(of.Shape == ExamShape.Roundabout, ground.Cell(cell).Roundabout);
                    break;

                case ExamShape.Tee:
                    Assert.Equal(3, Arms(ground, cell));
                    Assert.False(ground.HasArm(cell, lattice.Arm(card, ExamArm.North)), $"\"{of.Name}\" has an arm where its T is open");
                    break;

                default:
                    Assert.True(ground.Head(cell) >= 0, $"\"{of.Name}\" is a dead end on a cell with no spur");
                    break;
            }
        }
    }

    /// <summary>How many arms a cell's junction has, a spur included.</summary>
    static int Arms(ExamGround ground, int cell)
    {
        var arms = 0;
        for (var arm = 0; arm < 4; arm++)
        {
            if (ground.HasArm(cell, (ExamArm)arm)) arms++;
        }

        return arms;
    }

    /// <summary>
    /// <b>Every staged car stands in the lane it is about to drive</b>, facing the way that lane runs, and is
    /// sent to a place in a lane. A car off its lane is a card about recovering a line (CAR-9), and one sent
    /// off every lane is a card about a route that cannot be laid.
    /// </summary>
    [Fact]
    public void EveryStagedCarStandsInALaneAndIsSentToOne()
    {
        var lattice = Lattice.Value;
        var roads = RoadGraph.Build(ExamPlan.Lay(Config), Config);
        for (var card = 0; card < lattice.Cards; card++)
        {
            for (var driver = 0; driver < lattice.Card(card).Drivers.Length; driver++)
            {
                var name = $"\"{lattice.Card(card).Name}\" driver {driver}";
                InALane(roads, lattice.StandM(card, driver), Heading.Unit(lattice.StandHeadingRad(card, driver)), $"{name} stands");
                if (lattice.Card(card).Drivers[driver].Parked) continue;

                InALane(roads, lattice.AimM(card, driver), null, $"{name} is sent");
            }
        }
    }

    /// <summary>
    /// <b>Somebody walking round a corner sets off and ends on the pavement</b>, their whole body clear of every
    /// lane. One standing in a lane is a card about a body in the road, not about a car passing somebody on foot.
    /// </summary>
    [Fact]
    public void EveryWalkRoundACornerIsOnThePavement()
    {
        var lattice = Lattice.Value;
        var plan = ExamPlan.Lay(Config);
        var roads = RoadGraph.Build(plan, Config);
        var ends = plan.Paving(Config).RoadEnds(Config);
        var clearM = Config.LaneOffsetM + (Config.PersonDiameterM * 0.5f);
        for (var card = 0; card < lattice.Cards; card++)
        {
            for (var walker = 0; walker < lattice.Card(card).Walkers.Length; walker++)
            {
                if (!lattice.Card(card).Walkers[walker].Strolls) continue;

                Assert.True(lattice.Kerbs(card, walker, ends, out var fromM, out var toM));
                foreach (var atM in (ReadOnlySpan<Vector2>)[fromM, toM])
                {
                    var lane = roads.NearestLane(atM, out var alongM);
                    var offM = (Spline.SampleAt(roads.ArcsOf(lane), alongM).PositionM - atM).Length();
                    Assert.True(offM > clearM, $"\"{lattice.Card(card).Name}\" walker {walker} stands {offM:F2} m off a lane's line");
                }
            }
        }
    }

    static void InALane(RoadGraph roads, Vector2 atM, Vector2? facing, string what)
    {
        var lane = roads.NearestLane(atM, out var alongM);
        Assert.True(lane >= 0, $"{what} nowhere near a lane");

        var on = Spline.SampleAt(roads.ArcsOf(lane), alongM);
        var offM = (on.PositionM - atM).Length();
        Assert.True(offM <= Config.LaneOffsetM, $"{what} {offM:F2} m off the nearest lane's line");
        if (facing is { } unit) Assert.True(Vector2.Dot(on.Direction, unit) > 0.9f, $"{what} facing against its lane");
    }

    /// <summary>
    /// <b>A lit junction holds every lane arriving at it at a bar</b> (TLT-1, TER-6). A lane with no bar is a
    /// lane whose red nothing stops at, and a card about lights asked there would be about nothing.
    /// </summary>
    [Fact]
    public void EveryLitJunctionHoldsEveryLaneArrivingAtItAtABar()
    {
        var plan = ExamPlan.Lay(Config);
        var roads = RoadGraph.Build(plan, Config);
        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            if (!plan.Junctions.Lit[junction]) continue;

            var bars = 0;
            for (var bar = 0; bar < plan.StopLines.Count; bar++)
            {
                if (plan.StopLines.Junction[bar] == junction) bars++;
            }

            Assert.Equal(roads.LanesIntoJunction(junction).Length, bars);
        }
    }
}
