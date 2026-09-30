using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>A car park is a rank of bays laid off the kerb of a street that stays whole</b> (GEN-53), each bay a short
/// road of its own joined to nothing.
/// </summary>
/// <remarks>
/// <b>Asked of a town laid twice at one seed</b>, once with car parks and once without, where the question is
/// whether the street moved. A car park is the last thing done to a layout and every road's shape is a function
/// of its own link (GEN-11), so the two towns are the same town apart from what the car parks put in it.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P3)]
public class CarParkTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// How many buildings the towns here plan, which is what their car parks are counted off
    /// (<see cref="SimConfig.CarParksFor"/>). <b>Enough that both shapes of a car park are laid</b> — the one
    /// with a rank on each side of the street and the one with a bare side — on every seed.
    /// </summary>
    const int Buildings = 48;

    public static TheoryData<ulong> Seeds() => Towns.Seeds();

    static CityPlan Laid(ulong seed) => Plans.GetOrAdd(seed, at => Towns.LayFresh(Towns.Brief(at, buildings: Buildings)));

    static readonly ConcurrentDictionary<ulong, CityPlan> Plans = new();

    /// <summary>
    /// <b>A car park moves no road</b> (GEN-53): every road the town had carries the arcs it carried, piece for
    /// piece — the street a rank stands off among them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void LayingACarParkMovesNoRoad(ulong seed)
    {
        var was = Towns.LaidFrom(seed);
        var now = Laid(seed);
        Assert.True(now.CarParks.Count > 0, $"seed {seed} laid no car park to ask about");

        for (var road = 0; road < was.Roads.Count; road++)
        {
            Assert.Equal(was.Roads.SegmentsOf(road).ToArray(), now.Roads.SegmentsOf(road).ToArray());
        }
    }

    /// <summary>
    /// <b>A bay is joined to nothing</b> (GEN-53): both of its nodes are its own, and no other road meets either.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryBayStandsOnNodesOfItsOwn(ulong seed)
    {
        var plan = Laid(seed);
        var arms = ArmsOf(plan);
        foreach (var road in plan.CarParks.Road)
        {
            Assert.Equal(1, arms[plan.Roads.FromJunction[road]]);
            Assert.Equal(1, arms[plan.Roads.ToJunction[road]]);
        }
    }

    /// <summary>
    /// <b>No movement leaves a bay or arrives in one</b> (GEN-53): getting in and out is the car's own manoeuvre
    /// (GEN-4f), and nothing about it is laid with the town.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoMovementLeavesABayOrArrivesInOne(ulong seed)
    {
        var plan = Laid(seed);
        var lanes = plan.Paving(Config).Lanes;
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var from = lanes.LaneRoad[lanes.ConnectorFromLane[connector]];
            var onto = lanes.LaneRoad[lanes.ConnectorToLane[connector]];
            Assert.False(plan.Roads.IsABay(from), $"seed {seed} lays a movement out of bay {from}");
            Assert.False(plan.Roads.IsABay(onto), $"seed {seed} lays a movement into bay {onto}");
        }
    }

    /// <summary>
    /// <b>A side of a car park carries a handful of bays or none of them</b> (GEN-4b, GEN-53), and <b>not none
    /// on both sides</b> — a car park with no bay either side is no car park.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EverySideOfACarParkCarriesAHandfulOfBaysOrNone(ulong seed)
    {
        var plan = Laid(seed);
        for (var carPark = 0; carPark < plan.CarParks.Count; carPark++)
        {
            var right = plan.CarParks.BaysOn(carPark, right: true);
            var left = plan.CarParks.BaysOn(carPark, right: false);

            Assert.True(right + left > 0, $"car park {carPark} was laid for no bay at all");
            foreach (var bays in (int[])[right, left])
            {
                Assert.True(
                    bays == 0
                    || (bays >= Config.CityGen.BaysPerLotFewest && bays <= Config.CityGen.BaysPerLotMost),
                    $"car park {carPark} carries {bays} bays on one side, which is neither none nor a handful");
            }
        }
    }

    /// <summary>
    /// <b>A side's bays stand square to the street, a lane apart and centred on the car park's middle</b>
    /// (GEN-53): parallel to one another, so a rank is a row of spaces off one kerb and not a fan.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EverySideIsARankSquareToItsStreet(ulong seed)
    {
        var plan = Laid(seed);
        for (var carPark = 0; carPark < plan.CarParks.Count; carPark++)
        {
            var street = plan.Roads.SegmentsOf(plan.CarParks.Street[carPark]);
            var streetM = Spline.TotalLengthM(street);
            var middle = Spline.SampleAt(
                street, Spline.ProjectM(street, plan.CarParks.AtM[carPark], streetM * 0.5f, streetM));

            foreach (var right in (bool[])[true, false])
            {
                var outward = right ? middle.Right : -middle.Right;
                var alongM = new List<float>();
                for (var bay = plan.CarParks.BayOffsets[carPark]; bay < plan.CarParks.BayOffsets[carPark + 1]; bay++)
                {
                    if (plan.CarParks.Right[bay] != right) continue;

                    var line = plan.Roads.SegmentsOf(plan.CarParks.Road[bay]);
                    Assert.Equal(1f, Vector2.Dot(line[0].StartUnit, outward), 3);
                    alongM.Add(Vector2.Dot(line[0].StartM - plan.CarParks.AtM[carPark], middle.Direction));
                }

                if (alongM.Count == 0) continue;

                alongM.Sort();
                for (var bay = 1; bay < alongM.Count; bay++) Assert.Equal(Config.LaneWidthM, alongM[bay] - alongM[bay - 1], 3);

                Assert.Equal(0f, (alongM[0] + alongM[^1]) * 0.5f, 3);
            }
        }
    }

    /// <summary>
    /// <b>Every bay's mouth runs back over its street's own band, and by less than a touch</b> (GEN-53): the
    /// rank and the street are one piece of tarmac (TER-3c.8), and on a straight street they are not ground the
    /// two share (TER-5c). Read at both corners of every mouth, square off the street's line.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryBaysMouthRunsBackOverItsStreet(ulong seed)
    {
        var plan = Laid(seed);
        for (var carPark = 0; carPark < plan.CarParks.Count; carPark++)
        {
            var streetRoad = plan.CarParks.Street[carPark];
            var street = plan.Roads.SegmentsOf(streetRoad);
            var streetM = Spline.TotalLengthM(street);
            var halfM = plan.Roads.WidthM[streetRoad] * 0.5f;
            foreach (var road in plan.CarParks.RoadsOf(carPark))
            {
                var mouth = plan.Roads.SegmentsOf(road)[0];
                var across = Heading.RightOf(mouth.StartUnit) * (plan.Roads.WidthM[road] * 0.5f);
                foreach (var cornerM in (Vector2[])[mouth.StartM + across, mouth.StartM - across])
                {
                    var on = Spline.SampleAt(street, Spline.ProjectM(street, cornerM, streetM * 0.5f, streetM));
                    var overM = halfM - MathF.Abs(Vector2.Dot(cornerM - on.PositionM, on.Right));
                    Assert.True(
                        overM >= -LineTolerance.RoundingM,
                        $"bay {road} of car park {carPark} stands {-overM * 100f:F1} cm off its street");
                }
            }
        }
    }

    /// <summary>
    /// <b>A bay is one lane wide and driven both ways over its one line</b> (GEN-53): a body stands in it
    /// whichever way round it stands, so its two lanes are the line itself and not two halves of a carriageway.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryBayIsOneLaneWideOverOneLine(ulong seed)
    {
        var plan = Laid(seed);
        var lanes = plan.Paving(Config).Lanes;
        foreach (var road in plan.CarParks.Road)
        {
            Assert.True(plan.Roads.DrivenOverOneLine(road));
            Assert.Equal(RoadFlow.BothWays, plan.Roads.Flow[road]);
            Assert.Equal(Config.LaneWidthM, plan.Roads.WidthM[road], 3);

            for (var lane = 0; lane < lanes.LaneCount; lane++)
            {
                if (lanes.LaneRoad[lane] == road) Assert.True(lanes.LaneOverOneLine[lane]);
            }
        }
    }

    /// <summary>
    /// <b>No two one-way streets meet in a town with car parks in it</b> (GEN-18): a car park parts no street, so
    /// it makes no meeting either.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoTwoOneWayStreetsMeetInATownWithCarParksInIt(ulong seed) =>
        Assert.Null(OneWays.Meeting(Laid(seed)));

    /// <summary>
    /// <b>A car park stands a locality clear of every junction but its own</b> (GEN-16, GEN-53). <b>Its own are
    /// exempt</b> on the same terms a roundabout's nodes are: a bay's two nodes and its neighbours' are one car
    /// park laid out along a kerb, not spacings that landed on the same ground.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryCarParkStandsALocalityOffEveryJunctionThatIsNotItsOwn(ulong seed)
    {
        var plan = Laid(seed);
        var ownedBy = OwnedBy(plan);

        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            if (ownedBy[junction] < 0) continue;

            for (var other = 0; other < plan.Junctions.Count; other++)
            {
                if (other == junction || ownedBy[junction] == ownedBy[other]) continue;

                var apartM = Vector2.Distance(plan.Junctions.CentreM[junction], plan.Junctions.CentreM[other]);
                Assert.True(
                    apartM >= Config.CityGen.LocalityM,
                    $"junctions {junction} (car park {ownedBy[junction]}) and {other} (car park "
                    + $"{ownedBy[other]}) stand {apartM:F1} m apart, inside a locality of "
                    + $"{Config.CityGen.LocalityM:F0} m");
            }
        }
    }

    /// <summary>Which car park each junction belongs to — the two nodes of each of its bays — or −1.</summary>
    static int[] OwnedBy(CityPlan plan)
    {
        var owner = new int[plan.Junctions.Count];
        Array.Fill(owner, -1);
        for (var carPark = 0; carPark < plan.CarParks.Count; carPark++)
        {
            foreach (var road in plan.CarParks.RoadsOf(carPark))
            {
                owner[plan.Roads.FromJunction[road]] = carPark;
                owner[plan.Roads.ToJunction[road]] = carPark;
            }
        }

        return owner;
    }

    /// <summary>
    /// <b>A bay's arms are read off its own line</b> (GEN-52's reading, GEN-53): it arrives at each of its nodes
    /// on the bearing its arm there is read with, and each node stands a lead off the line's end.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryBayArrivesOnTheBearingsItsArmsAreReadWith(ulong seed)
    {
        var plan = Laid(seed);
        var ground = plan.Ground;
        var openRad = RoadStage.CreaseRad(Config);
        foreach (var road in plan.CarParks.Road)
        {
            Assert.True(ground.Roads.WasCut(road));

            var arcs = ground.Roads.SegmentsOf(road);
            var from = ConnectionPoints.ArmOf(ground, Config, road, atFrom: true);
            var to = ConnectionPoints.ArmOf(ground, Config, road, atFrom: false);

            Assert.Equal(0f, Apart(arcs[0].StartUnit, from.StandUnit), openRad * 2f);
            Assert.Equal(0f, Apart(Heading.Unit(arcs[^1].HeadingAtRad(arcs[^1].LengthM)), -to.StandUnit), openRad * 2f);
            Assert.Equal(Config.CityGen.ConnectionStandoffM, Lead(from), 2);
            Assert.Equal(Config.CityGen.ConnectionStandoffM, Lead(to), 2);
        }
    }

    /// <summary>How long the arc joining a node to the stand point of one of its arms is.</summary>
    static float Lead(in ConnectionPoints.Arm arm) =>
        Spline.ArcThrough(arm.NodeM, MathF.Atan2(arm.OutwardUnit.Y, arm.OutwardUnit.X), arm.StandM).LengthM;

    static float Apart(Vector2 one, Vector2 other) =>
        MathF.Acos(Math.Clamp(Vector2.Dot(one, other), -1f, 1f));

    /// <summary>How many roads meet at each junction of a plan.</summary>
    static int[] ArmsOf(CityPlan plan)
    {
        var arms = new int[plan.Junctions.Count];
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.SegmentsOf(road).Length == 0) continue;

            arms[plan.Roads.FromJunction[road]]++;
            arms[plan.Roads.ToJunction[road]]++;
        }

        return arms;
    }
}
