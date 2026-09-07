using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>What a generated town owes whatever seed it was laid from</b> — the properties the generator holds by
/// construction, asked of towns rather than of a fixture.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are the safety net for the rule that no stage retries.</b> Each stage constrains the next so
/// that a violation cannot be produced; what is asked here is whether that is actually true, over several
/// seeds, of the town that comes out. A failure is a defect in the arrangement rather than a seed to skip.
/// </para>
/// <para>
/// <b>A small brief and not a city.</b> The properties are about the arrangement rather than about scale,
/// and a town two kilometres across exercises every stage in a fraction of a second. <b>It is not smaller
/// than a wheel</b>: below about that, the orbital and its spokes are most of what the ground holds, the
/// lattice inside them is a handful of points, and what comes out is a chain of streets rather than a town
/// with blocks in it — a fixture that answers questions about no arrangement anybody ships.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class GeneratorTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// The roofs a building may be sized to. <b>Two of them and not the catalogue's</b>: what is being asked
    /// here is what the generator does with the footprints it is handed, and a test that read the art would
    /// be a test of the art.
    /// </summary>
    static readonly Vector2[] RoofsM = [new(12f, 12f), new(18f, 16f)];

    static CityPlan Lay(TownBrief brief) => TownGenerator.Lay(brief, Config, RoofsM);

    static TownBrief Brief(ulong seed) => new()
    {
        Name = "Fixture",
        Description = "A town laid to ask the generator its own questions",
        Seed = seed,
        WidthM = 2000f,
        HeightM = 1500f,
        Districts = 6,
        Buildings = 120,
        People = 40,
        Cars = 40,
        Water = WaterKind.River,
        WaterBearingDeg = 20f,

        // Narrow enough that the deck a town builds spans it (GEN-14a): a river this town cannot bridge is
        // a town in two halves, and the half that is deleted takes the properties being asked about with it.
        WaterShare = 0.06f,
    };

    /// <summary>The same town on a coast, which is the water a town may not bridge (GEN-14b).</summary>
    static TownBrief Coast(ulong seed)
    {
        var brief = Brief(seed);
        return new TownBrief
        {
            Name = brief.Name, Description = brief.Description, Seed = brief.Seed, WidthM = brief.WidthM,
            HeightM = brief.HeightM, Districts = brief.Districts, Buildings = brief.Buildings,
            People = brief.People, Cars = brief.Cars, Water = WaterKind.Coast,
            WaterBearingDeg = brief.WaterBearingDeg, WaterShare = 0.2f,
        };
    }

    public static TheoryData<ulong> Seeds()
    {
        var seeds = new TheoryData<ulong>();
        foreach (var seed in (ulong[])[1, 7, 4242, 0xDEADBEEF]) seeds.Add(seed);
        return seeds;
    }

    [Fact]
    public void OneSeedLaysOneTown()
    {
        var once = Lay(Brief(99));
        var again = Lay(Brief(99));

        Assert.Equal(Shape(once), Shape(again));
    }

    [Fact]
    public void AnotherSeedLaysAnotherTown()
    {
        Assert.NotEqual(
            Shape(Lay(Brief(99))),
            Shape(Lay(Brief(100))));
    }

    /// <summary>
    /// <b>A stage's draws are its own</b>: what the slot stage is asked for cannot move where the roads went,
    /// because each stage draws on its own stream of the seed. It is what makes a stage worth retuning.
    /// </summary>
    [Fact]
    public void RetuningALaterStageLeavesTheRoadsWhereTheyWere()
    {
        var town = Lay(Brief(3));
        var brief = Brief(3);
        var other = Lay(
            new TownBrief
            {
                Name = brief.Name, Description = brief.Description, Seed = brief.Seed, WidthM = brief.WidthM,
                HeightM = brief.HeightM, Districts = brief.Districts, Buildings = brief.Buildings,
                People = brief.People, Cars = brief.Cars, Water = brief.Water,
                WaterBearingDeg = brief.WaterBearingDeg, WaterShare = brief.WaterShare,
                ParkingSlotShare = brief.ParkingSlotShare * 0.5f,
            });

        Assert.Equal(town.Roads.Count, other.Roads.Count);
        Assert.Equal(town.Junctions.Count, other.Junctions.Count);
        for (var road = 0; road < town.Roads.Count; road++)
        {
            Assert.Equal(town.Roads.SegmentsOf(road).Length, other.Roads.SegmentsOf(road).Length);
        }
    }

    /// <summary>
    /// <b>One town and not several</b> (GEN-5): whatever the water and the districts cut off is deleted with
    /// its own piece, so every junction is reachable from every other by road.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryJunctionIsReachableFromEveryOther(ulong seed)
    {
        var plan = Lay(Brief(seed));
        var root = new int[plan.Junctions.Count];
        for (var junction = 0; junction < root.Length; junction++) root[junction] = junction;

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var a = Find(root, plan.Roads.FromJunction[road]);
            var b = Find(root, plan.Roads.ToJunction[road]);
            if (a != b) root[b] = a;
        }

        var pieces = 0;
        for (var junction = 0; junction < root.Length; junction++)
        {
            if (Find(root, junction) == junction) pieces++;
        }

        Assert.Equal(1, pieces);
    }

    /// <summary>
    /// <b>Nothing the map carries stands off the map</b> (GEN-2b). There is no ground past the extent to
    /// walk, drive or classify, so a shape laid out there hangs over the void — and the water is the case
    /// that has to be cut rather than never laid, since its shore is drawn past the town on purpose.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NothingStandsOffTheEdgeOfTheMap(ulong seed)
    {
        var plan = Lay(Brief(seed));

        foreach (var (what, rings) in Towns.WaterRingsOf(plan.Water))
        {
            for (var ring = 0; ring < rings.Count; ring++)
            {
                foreach (var pointM in rings.RingOf(ring)) OnTheMap(plan, pointM, $"{what} {ring}");
            }
        }

        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            OnTheMap(plan, plan.Junctions.CentreM[junction], $"junction {junction}");
        }

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var chain = plan.Roads.SegmentsOf(road);
            var lengthM = Spline.TotalLengthM(chain);
            for (var alongM = 0f; alongM <= lengthM; alongM += 1f)
            {
                OnTheMap(plan, Spline.SampleAt(chain, alongM).PositionM, $"road {road}");
            }
        }

        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var halfM = plan.Buildings.SizeM[building] * 0.5f;
            var reachM = MathF.Max(halfM.X, halfM.Y);
            OnTheMap(plan, plan.Buildings.CentreM[building], $"building {building}", reachM);
        }

        for (var bay = 0; bay < plan.ParkingLots.SpaceCount; bay++)
        {
            OnTheMap(plan, plan.ParkingLots.SpacePositionM[bay], $"bay {bay}");
        }

        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            OnTheMap(plan, plan.Props.CentreM[prop], $"prop {prop}", plan.Props.RadiusM[prop]);
        }

        for (var spawn = 0; spawn < plan.Spawns.Count; spawn++)
        {
            OnTheMap(plan, plan.Spawns.PositionM[spawn], $"spawn {spawn}");
        }
    }

    static void OnTheMap(CityPlan plan, Vector2 pointM, string what, float reachM = 0f)
    {
        var standsM = new Vector2(reachM, reachM);
        var leastM = standsM;
        var mostM = plan.WorldSizeM - standsM;
        Assert.True(
            pointM.X >= leastM.X && pointM.Y >= leastM.Y && pointM.X <= mostM.X && pointM.Y <= mostM.Y,
            $"{what} stands at {pointM}, off a map of {plan.WorldSizeM}");
    }

    /// <summary>
    /// <b>The water is set in a shore, and the shore is nobody's ground</b> (GEN-2c): every cell between the
    /// bank and the grass is shore, and nothing the town scatters stands on one — a prop takes the grass that
    /// is left over, and the shore is not left over.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheShoreIsBetweenTheWaterAndTheGrassAndNothingStandsOnIt(ulong seed)
    {
        var plan = Lay(Brief(seed));
        Assert.Equal(plan.Water.Outline.Count, plan.Water.Shore.Count);

        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            Assert.Equal(Ground.Grass, GroundAt(plan, plan.Props.CentreM[prop]));
        }

        // And the shore is what the water is met at: no water anywhere in the town has grass a step from it.
        // What may touch it is what the town laid over the shore afterwards — a bridge's own deck reaches
        // the water by design.
        var stepM = Config.Terrain.GroundStepM;
        for (var y = stepM * 0.5f; y < plan.WorldSizeM.Y; y += stepM)
        {
            for (var x = stepM * 0.5f; x < plan.WorldSizeM.X; x += stepM)
            {
                var atM = new Vector2(x, y);
                if (GroundAt(plan, atM) != Ground.Water) continue;

                foreach (var stride in (ReadOnlySpan<Vector2>)
                         [new(stepM, 0f), new(-stepM, 0f), new(0f, stepM), new(0f, -stepM)])
                {
                    var besideM = atM + stride;
                    if (besideM.X < 0f || besideM.Y < 0f) continue;
                    if (besideM.X > plan.WorldSizeM.X || besideM.Y > plan.WorldSizeM.Y) continue;

                    AssertNotGrass(plan, besideM);
                }
            }
        }
    }

    static void AssertNotGrass(CityPlan plan, Vector2 atM) =>
        Assert.True(GroundAt(plan, atM) != Ground.Grass, $"grass at {atM.X:F1},{atM.Y:F1} stands against the water");

    /// <summary>
    /// <b>A town with one-way streets in it can still be driven round</b> (GEN-18): from every lane there
    /// is, every junction the town has is reachable — turning round in the road is not a movement (TER-5f),
    /// so a block whose streets all ran inwards would pass GEN-5 and still be somewhere a car drives into
    /// and never leaves.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the lane graph the town is actually driven on</b> and not of the layout the generator
    /// settled its one-way streets against: the turn table drops a movement the drawn lines leave no room
    /// for as well as the one that turns round, so this is where a proposal that looked drivable on chords
    /// answers for the shapes it came out as.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryJunctionCanBeDrivenToFromEveryLane(ulong seed)
    {
        var roads = RoadGraph.Build(Lay(Brief(seed)), Config);
        Assert.Null(Drivable.Offence(roads));
    }

    /// <summary>
    /// <b>No lane dangles</b> (GEN-18a): every lane the town lays is one a car can be driven onto and one it
    /// can be driven off again. A node that forks nothing is where this is lost — a road of two lanes meeting
    /// a road of one leaves the lane back out of it with nothing that ever arrives on it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoLaneIsDrivenOntoByNothing(ulong seed)
    {
        var roads = RoadGraph.Build(Lay(Brief(seed)), Config);
        Assert.Null(Drivable.Dangling(roads));
    }

    /// <summary>
    /// <b>Nothing ends in nothing</b> (GEN-5a): a generated town carries no junction of one arm, since the
    /// road stage gives every junction the disc a crossing needs and a dead end is the one junction that has
    /// to hold a car turning round in it (TER-5a).
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoRoadEndsInNothing(ulong seed)
    {
        var plan = Lay(Brief(seed));
        var arms = ArmsOf(plan);

        var dangling = 0;
        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            if (arms[junction] == 1) dangling++;
        }

        Assert.True(dangling == 0, $"{dangling} of {plan.Junctions.Count} junctions are dead ends");
    }

    /// <summary>
    /// <b>A junction is the only place two roads touch</b> (GEN-17): every pair that meets at none stands at
    /// least one road's whole width apart, carriageway and walk, over the whole of both their lengths.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the shapes and not of the chords they were joined on</b>, which is the whole point: a
    /// street strays off its chord by its own wander and an arterial's arc by its sagitta, so the pair the
    /// layout measured is not the pair that was drawn. Roads that share a junction are left out — they touch
    /// there because that is what a junction is, and how square they stand is GEN-13's.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoTwoRoadsTouchAwayFromAJunction(ulong seed)
    {
        var plan = Lay(Brief(seed));
        var walked = Walked(plan);
        for (var road = 0; road < walked.Length; road++)
        {
            for (var other = road + 1; other < walked.Length; other++)
            {
                if (SharesAJunction(plan, road, other)) continue;

                foreach (var atM in walked[road])
                {
                    foreach (var elseM in walked[other])
                    {
                        var apartM = (atM - elseM).Length();
                        Assert.True(
                            apartM >= Config.RoadFootprintM,
                            $"roads {road} and {other} meet no junction yet pass {apartM:F1} m apart at " +
                            $"{atM.X:F0},{atM.Y:F0}, inside the {Config.RoadFootprintM:F1} m one road takes");
                    }
                }
            }
        }
    }

    /// <summary>Every road as the points it is actually drawn through, a stride apart along its own curve.</summary>
    static Vector2[][] Walked(CityPlan plan)
    {
        var walked = new Vector2[plan.Roads.Count][];
        for (var road = 0; road < walked.Length; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            var lengthM = Spline.TotalLengthM(arcs);
            var steps = Math.Max(1, (int)(lengthM / Config.Car.LengthM));
            walked[road] = new Vector2[steps + 1];
            for (var step = 0; step <= steps; step++)
            {
                walked[road][step] = Spline.SampleAt(arcs, lengthM * step / steps).PositionM;
            }
        }

        return walked;
    }

    static bool SharesAJunction(CityPlan plan, int road, int other) =>
        plan.Roads.FromJunction[road] == plan.Roads.FromJunction[other]
        || plan.Roads.FromJunction[road] == plan.Roads.ToJunction[other]
        || plan.Roads.ToJunction[road] == plan.Roads.FromJunction[other]
        || plan.Roads.ToJunction[road] == plan.Roads.ToJunction[other];

    /// <summary>
    /// <b>Two junctions inside a locality of each other are one junction</b> (GEN-16). The layout merges the
    /// cluster before anything is drawn from it, so what is asked of the plan is that none of the pairs the
    /// merge exists to remove is left in it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoTwoJunctionsStandInsideALocalityOfEachOther(ulong seed)
    {
        var plan = Lay(Brief(seed));
        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            for (var other = junction + 1; other < plan.Junctions.Count; other++)
            {
                var apartM = (plan.Junctions.CentreM[junction] - plan.Junctions.CentreM[other]).Length();
                Assert.True(
                    apartM >= Config.CityGen.LocalityM,
                    $"junctions {junction} and {other} stand {apartM:F1} m apart, inside a locality of " +
                    $"{Config.CityGen.LocalityM:F0} m");
            }
        }
    }

    /// <summary>
    /// <b>Every car park is a handful of bays</b> (GEN-4b): no lot in a town holds fewer than the fewest a
    /// lot may be or more than the most, whatever the frontage it was offered would have carried.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoCarParkHoldsMoreOrFewerBaysThanALotMay(ulong seed)
    {
        var lots = Lay(Brief(seed)).ParkingLots;
        for (var lot = 0; lot < lots.Count; lot++)
        {
            var bays = lots.SpaceOffsets[lot + 1] - lots.SpaceOffsets[lot];
            Assert.True(
                bays >= Config.CityGen.BaysPerLotFewest && bays <= Config.CityGen.BaysPerLotMost,
                $"lot {lot} holds {bays} bays, outside the {Config.CityGen.BaysPerLotFewest} to " +
                $"{Config.CityGen.BaysPerLotMost} a lot may be");
        }
    }

    /// <summary>
    /// <b>And two car parks sharing a kerb inside a locality are one car park</b> (GEN-16): the run of
    /// frontage that drew one is laid as a single rectangle of bays, so nothing in the town is two lots with
    /// a stride of pavement pinched between them.
    /// </summary>
    /// <remarks>
    /// <b>Measured along the kerb, which is where GEN-4d measures a lot's clearance.</b> Two lots facing each
    /// other across a carriageway are the two sides of one street and stay two, so what is asked about is the
    /// pairs that stand abeam of each other along their own bearing.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoTwoCarParksShareAKerbInsideALocality(ulong seed)
    {
        var lots = Lay(Brief(seed)).ParkingLots;
        for (var lot = 0; lot < lots.Count; lot++)
        {
            for (var other = lot + 1; other < lots.Count; other++)
            {
                var apartM = lots.CentreM[other] - lots.CentreM[lot];
                var acrossM = MathF.Abs(Spline.Cross(lots.Axis[lot], apartM));
                if (acrossM >= lots.HalfExtentM[lot].Y + lots.HalfExtentM[other].Y) continue;

                var alongM = MathF.Abs(Vector2.Dot(lots.Axis[lot], apartM))
                             - lots.HalfExtentM[lot].X - lots.HalfExtentM[other].X;
                Assert.True(
                    alongM >= Config.CityGen.LocalityM,
                    $"lots {lot} and {other} share a kerb with {alongM:F1} m of it between them, inside a " +
                    $"locality of {Config.CityGen.LocalityM:F0} m");
            }
        }
    }

    /// <summary>
    /// <b>Every road leaves a junction that forks straight</b>: that junction's own ground, the corner an arm
    /// turns through, the crossing and the bar behind it are all laid across a straight arm, and a road that
    /// started bending inside its own box would put every one of them on a curve. <b>A node that forks
    /// nothing is where it bends instead</b> (TER-5b) — the two arms there are one carriageway swept into one
    /// curve, and the paint stands past the end of it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryRoadLeavesItsJunctionsStraight(ulong seed)
    {
        var plan = Lay(Brief(seed));
        var arms = ArmsOf(plan);
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            if (arcs.Length < 2) continue;

            // A road of one arc is a straight or a piece of the orbital, which bends the whole way and is
            // the one road the stubs do not apply to.
            var from = plan.Roads.FromJunction[road];
            var to = plan.Roads.ToJunction[road];
            if (arms[from] > 2)
            {
                Assert.True(arcs[0].Curvature == 0f, $"road {road} bends out of junction {from}");
                Assert.True(
                    arcs[0].LengthM >= RoadStage.StubM(Config) * 0.5f,
                    $"road {road} leaves junction {from} on {arcs[0].LengthM:F1} m of straight");
            }

            if (arms[to] > 2) Assert.True(arcs[^1].Curvature == 0f, $"road {road} bends into junction {to}");
        }
    }

    /// <summary>
    /// <b>Nothing bends tighter than the street's own floor</b>, which is the radius the speed a street is
    /// laid for affords on tarmac (<see cref="SimConfig.CarCorneringRadiusM"/>).
    /// </summary>
    /// <remarks>
    /// <b>The corner a node with no fork was swept into is the exception</b> (TER-5b, GEN-12): that turn is
    /// the one the layout put there and a car slowed for it when it was a junction, so what the sweep changed
    /// is the shape of the ground and not the speed anything holds through it. It is still never tighter than
    /// the fillet the corner would have been turned on.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NothingBendsTighterThanItsOwnFloor(ulong seed)
    {
        var plan = Lay(Brief(seed));
        var arms = ArmsOf(plan);
        var floorM = RoadStage.FloorRadiusM(Config, RoadClass.Street);
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            for (var piece = 0; piece < arcs.Length; piece++)
            {
                if (arcs[piece].Curvature == 0f) continue;

                var sweptCorner = (piece == 0 && arms[plan.Roads.FromJunction[road]] == 2)
                                  || (piece == arcs.Length - 1 && arms[plan.Roads.ToJunction[road]] == 2);
                var radiusM = 1f / MathF.Abs(arcs[piece].Curvature);
                Assert.True(
                    radiusM >= (sweptCorner ? Config.RoadCornerRadiusM : floorM) - 0.01f,
                    $"an arc of {radiusM:F1} m against a floor of {floorM:F1} m");
            }
        }
    }

    /// <summary>
    /// <b>A road is one line and not a row of them</b> (GEN-12): every joint in its chain sets off on the
    /// bearing the piece before it arrives on, which is what a follower reads off the road and what lets the
    /// pavement beside it be one offset of one curve.
    /// </summary>
    /// <remarks>
    /// A crease is not a small thing beside the road. Offsetting moves every piece of a chain sideways by the
    /// same figure, so a joint open by an angle offsets open by that angle times the offset — a fifth of a
    /// turn at half a walk outside a carriageway is three and a half metres of pavement that was never laid,
    /// with a walking lane dead-ending either side of the hole.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryRoadIsOneLineWithNoCreaseInIt(ulong seed)
    {
        var plan = Lay(Brief(seed));
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            for (var joint = 1; joint < arcs.Length; joint++)
            {
                var arrives = arcs[joint - 1];
                var creaseRad = Spline.WrapRad(arcs[joint].HeadingRad - arrives.HeadingAtRad(arrives.LengthM));
                Assert.True(
                    MathF.Abs(creaseRad) <= CreaseRad,
                    $"road {road} creases by {creaseRad:F4} rad at {arcs[joint].StartM}");
            }
        }
    }

    /// <summary>
    /// How open a joint may read and still be one line: <b>the angle that moves the pavement's own line by
    /// the rounding two computations of one distance disagree by</b> (<see cref="Kerbs.RoundingM"/>), at the
    /// offset that line is laid at. It is float slop over a chain of arcs and nothing else — a road that
    /// really creases does so by tenths of a radian.
    /// </summary>
    static float CreaseRad => Kerbs.RoundingM / ((Config.LaneWidthM * SimConfig.LanesPerCarriageway * 0.5f)
                                                 + (Config.PavementWidthM * 0.5f));

    /// <summary>Nothing a town stands is laid on its water (GEN-5), which the ground is the authority on.</summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NothingStandsOnTheWater(ulong seed)
    {
        var plan = Lay(Brief(seed));
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            Assert.NotEqual(Ground.Water, GroundAt(plan, plan.Buildings.CentreM[building]));
        }

        for (var bay = 0; bay < plan.ParkingLots.SpaceCount; bay++)
        {
            Assert.NotEqual(Ground.Water, GroundAt(plan, plan.ParkingLots.SpacePositionM[bay]));
        }

        for (var spawn = 0; spawn < plan.Spawns.Count; spawn++)
        {
            Assert.NotEqual(Ground.Water, GroundAt(plan, plan.Spawns.PositionM[spawn]));
        }
    }

    /// <summary>
    /// <b>No junction stands on the water</b> (GEN-14). The ground cannot answer this once the town has been
    /// painted — a deck's cells say road — so it is asked of the outline the map carries.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoJunctionStandsInTheWater(ulong seed)
    {
        var plan = Lay(Brief(seed));
        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            Assert.False(
                InTheWater(plan, plan.Junctions.CentreM[junction]),
                $"junction {junction} stands at {plan.Junctions.CentreM[junction]}, in the water");
        }
    }

    /// <summary>
    /// <b>The only road over the water is a bridge</b> (GEN-14a): a street never crosses, and neither does a
    /// piece of an arterial the layout would not span.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryRoadOverTheWaterIsABridge(ulong seed)
    {
        var plan = Lay(Brief(seed));
        var bridged = new bool[plan.Roads.Count];
        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++) bridged[plan.Bridges.Road[bridge]] = true;

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (bridged[road]) continue;

            var chain = plan.Roads.SegmentsOf(road);
            var lengthM = Spline.TotalLengthM(chain);
            for (var alongM = 0f; alongM <= lengthM; alongM += 1f)
            {
                var atM = Spline.SampleAt(chain, alongM).PositionM;
                Assert.False(InTheWater(plan, atM), $"road {road} stands at {atM}, in the water, and is no bridge");
            }
        }
    }

    /// <summary>
    /// <b>A bridge is one straight span no longer than the deck a town builds, and the deck is the whole of
    /// it</b> (GEN-14a, TER-3b) — so what it carries reaches standable ground at both ends.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryBridgeIsOneStraightSpanTheDeckRunsTheWholeOf(ulong seed)
    {
        var plan = Lay(Brief(seed));
        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
        {
            var chain = plan.Roads.SegmentsOf(plan.Bridges.Road[bridge]);
            var lengthM = Spline.TotalLengthM(chain);

            Assert.Equal(1, chain.Length);
            Assert.Equal(0f, chain[0].Curvature);
            Assert.True(
                lengthM <= Config.CityGen.BridgeDeckLongestM,
                $"bridge {bridge} spans {lengthM:F0} m against a bound of {Config.CityGen.BridgeDeckLongestM:F0} m");
            Assert.Equal(0f, plan.Bridges.FromM[bridge]);
            Assert.Equal(lengthM, plan.Bridges.ToM[bridge], 0.01f);
        }
    }

    /// <summary>
    /// <b>A town on a river is bridged</b> (GEN-14b). The wheel is turned so a spoke runs down the river's
    /// own normal, which is what buys a crossing short enough to build — asked over every seed at once,
    /// because how many a town gets is a fact about where the banks fell.
    /// </summary>
    [Fact]
    public void ARiverIsCrossedAndTheSeaIsNot()
    {
        var bridges = 0;
        foreach (var seed in (ulong[])[1, 7, 4242, 0xDEADBEEF])
        {
            bridges += Lay(Brief(seed)).Bridges.Count;
            Assert.Empty(Lay(Coast(seed)).Bridges.Road);
        }

        Assert.True(bridges >= 4, $"four river towns carry {bridges} bridges between them");
    }

    /// <summary>
    /// <b>No two buildings stand in each other</b> (GEN-3), and each keeps the padding a walker gets past it
    /// by. The slots claim that ground before anything fills them, so this is the claim being checked rather
    /// than a search being repeated.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoBuildingStandsInsideAnother(ulong seed)
    {
        var plan = Lay(Brief(seed));
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            for (var other = building + 1; other < plan.Buildings.Count; other++)
            {
                var apartM = plan.Buildings.CentreM[building] - plan.Buildings.CentreM[other];
                var reachM = (plan.Buildings.SizeM[building] + plan.Buildings.SizeM[other]) * 0.5f;
                var clear = MathF.Abs(apartM.X) >= reachM.X || MathF.Abs(apartM.Y) >= reachM.Y;
                Assert.True(clear, $"buildings {building} and {other} stand in each other");
            }
        }
    }

    /// <summary>
    /// <b>A share of the junctions that could be lit is left to the ranking instead</b> (TER-5e). Only a
    /// junction of three arms or more can carry lights at all (TLT-3), so the share is of those.
    /// </summary>
    /// <remarks>
    /// <b>Over every seed at once and not one town at a time.</b> It is a draw, so what is asked is that the
    /// draw is at the share the brief states; a single town of a few dozen junctions is a sample too small
    /// to say anything, and a test that asserted it of one would fail on the seed rather than on the rule.
    /// </remarks>
    [Fact]
    public void SomeOfTheJunctionsAreLeftUnregulated()
    {
        var eligible = 0;
        var unregulated = 0;
        var brief = Brief(1);
        foreach (var seed in (ulong[])[1, 7, 4242, 0xDEADBEEF])
        {
            var plan = Lay(Brief(seed));
            var arms = ArmsOf(plan);
            for (var junction = 0; junction < plan.Junctions.Count; junction++)
            {
                if (arms[junction] < 3) continue;

                eligible++;
                if (!plan.Junctions.Lit[junction]) unregulated++;
            }
        }

        Assert.True(eligible > 40, $"only {eligible} junctions in four towns could be lit at all");
        Assert.InRange(
            unregulated / (float)eligible,
            brief.UnregulatedJunctionShare * 0.4f,
            brief.UnregulatedJunctionShare * 2f);
    }

    /// <summary>Nothing that is lit is a junction lights are about (TLT-3) — a light on two arms governs nothing.</summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NothingUnderThreeArmsIsLit(ulong seed)
    {
        var plan = Lay(Brief(seed));
        var arms = ArmsOf(plan);

        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            Assert.False(plan.Junctions.Lit[junction] && arms[junction] < 3, $"junction {junction} is lit on {arms[junction]} arm(s)");
        }
    }

    /// <summary>
    /// <b>A junction that admits no fork is crossed once, and barred either side of that crossing</b>
    /// (TER-6): its two arms are one road, so the node carries one zebra rather than one per arm, and the
    /// paint on it is that zebra with the bar of each of the two lanes running over it, clear of it and
    /// facing it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void AJunctionWithNoForkIsCrossedOnceAndBarredEitherSideOfIt(ulong seed)
    {
        var plan = Lay(Brief(seed));
        var arms = ArmsOf(plan);

        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            if (arms[junction] != 2) continue;

            var only = -1;
            for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
            {
                if (plan.Crosswalks.Junction[crossing] != junction) continue;

                Assert.True(only < 0, $"junction {junction} forks nothing and carries crossings {only} and {crossing}");
                only = crossing;
            }

            if (only < 0) continue;

            var axis = Vector2.Normalize(plan.Crosswalks.Axis[only]);
            var clearM = plan.Crosswalks.DepthM[only] * 0.5f;
            var sides = 0;
            for (var bar = 0; bar < plan.StopLines.Count; bar++)
            {
                if (plan.StopLines.Junction[bar] != junction) continue;

                Assert.Equal(plan.Crosswalks.Road[only], plan.StopLines.Road[bar]);

                // Which side of the paint the bar stands, and whether the traffic it stops is driving at
                // the paint from that side: a bar facing away is one the walkers are behind.
                var alongM = Vector2.Dot(plan.StopLines.CentreM[bar] - plan.Crosswalks.CentreM[only], axis);
                Assert.True(
                    MathF.Abs(alongM) > clearM,
                    $"junction {junction}: bar {bar} stands {alongM:F2} m off a crossing {clearM * 2f:F2} m deep");

                var side = alongM > 0f ? 1 : 2;
                Assert.True(
                    Vector2.Dot(plan.StopLines.Approach[bar], axis) * alongM < 0f,
                    $"junction {junction}: bar {bar} faces away from the crossing it belongs to");

                Assert.True((sides & side) == 0, $"junction {junction} carries two bars on one side of its crossing");
                sides |= side;
            }
        }
    }

    /// <summary>
    /// <b>Every corner a junction turns is turned</b> (TER-5). Two arms standing on the near side of a
    /// straight line leave their kerbs crossing outside the mouth, and what is not paved back to an arc
    /// tangent to both is a spike of pavement standing in the carriageway.
    /// </summary>
    /// <remarks>
    /// <b>The pairs are read off the geometry and not off the order the arms are stored in.</b> The corner
    /// a bend turns is the convex one of its two pairs, and a stage that took whichever pair came first
    /// filleted half of its right-angle bends and left the other half sharp.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryCornerAJunctionTurnsIsTurned(ulong seed)
    {
        var plan = Lay(Brief(seed));
        foreach (var (junction, apartRad, spikeM, cornerAtM) in Corners(plan))
        {
            Assert.True(
                Turned(plan, cornerAtM),
                $"junction {junction} leaves a {spikeM:F2} m spike at {cornerAtM.X:F1},{cornerAtM.Y:F1} " +
                $"between arms {apartRad * 180f / MathF.PI:F0} degrees apart");
        }
    }

    /// <summary>Whether the plan carries a kerb fillet at a corner, within the ground's own resolution of it.</summary>
    static bool Turned(CityPlan plan, Vector2 cornerAtM)
    {
        for (var corner = 0; corner < plan.JunctionCorners.Count; corner++)
        {
            if ((plan.JunctionCorners.CornerM[corner] - cornerAtM).Length() < Config.Terrain.GroundStepM) return true;
        }

        return false;
    }

    /// <summary>
    /// Every pair of arms that turns a corner worth filleting: where the two kerbs they carry cross, and
    /// how far outside the mouth that crossing stands.
    /// </summary>
    static List<(int Junction, float ApartRad, float SpikeM, Vector2 CornerM)> Corners(CityPlan plan)
    {
        var bearings = new List<(float Rad, float HalfM, float StandsOffM)>[plan.Junctions.Count];
        for (var junction = 0; junction < bearings.Length; junction++) bearings[junction] = [];

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var chain = plan.Roads.SegmentsOf(road);
            if (chain.Length == 0) continue;

            var halfM = plan.Roads.WidthM[road] * 0.5f;
            var from = Spline.SampleAt(chain, 0f);
            var to = Spline.SampleAt(chain, Spline.TotalLengthM(chain));
            var outOfFrom = from.Direction;
            var outOfTo = -to.Direction;
            bearings[plan.Roads.FromJunction[road]].Add((
                MathF.Atan2(outOfFrom.Y, outOfFrom.X), halfM,
                Vector2.Dot(
                    from.PositionM - plan.Junctions.CentreM[plan.Roads.FromJunction[road]],
                    Heading.RightOf(outOfFrom))));
            bearings[plan.Roads.ToJunction[road]].Add((
                MathF.Atan2(outOfTo.Y, outOfTo.X), halfM,
                Vector2.Dot(
                    to.PositionM - plan.Junctions.CentreM[plan.Roads.ToJunction[road]],
                    Heading.RightOf(outOfTo))));
        }

        var corners = new List<(int, float, float, Vector2)>();
        for (var junction = 0; junction < bearings.Length; junction++)
        {
            var round = bearings[junction];
            if (round.Count < 2) continue;

            round.Sort();
            for (var at = 0; at < round.Count; at++)
            {
                var next = (at + 1) % round.Count;
                var a = Heading.Unit(round[at].Rad);
                var b = Heading.Unit(round[next].Rad);
                var apartRad = round[next].Rad - round[at].Rad;
                if (apartRad <= 0f) apartRad += MathF.Tau;
                // Each kerb stands off the node by its own road's half and by however far off the node that
                // road stands (TER-4d) — which for a one-way street beside a full carriageway is neither the
                // bisector nor the node's own line.
                var kerbAtM = round[at].HalfM + round[at].StandsOffM;
                var kerbNextM = round[next].HalfM - round[next].StandsOffM;
                if (!Config.JunctionTurnsACorner(apartRad, kerbAtM, kerbNextM)) continue;

                var alongM = SimConfig.JunctionCornerAlongM(apartRad, kerbAtM, kerbNextM);
                var spikeM = MathF.Sqrt((alongM * alongM) + (kerbAtM * kerbAtM))
                             - MathF.Min(kerbAtM, kerbNextM);
                var intoTheWedge = Vector2.Normalize(b - (a * Vector2.Dot(a, b)));
                corners.Add((
                    junction, apartRad, spikeM,
                    plan.Junctions.CentreM[junction] + (a * alongM) + (intoTheWedge * kerbAtM)));
            }
        }

        return corners;
    }

    /// <summary>
    /// <b>A prop stands wholly on grass</b> (GEN-6a): its own girth and not its centre, because a bench half
    /// over a kerb is a bench in the road.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryPropStandsWhollyOnGrass(ulong seed)
    {
        var plan = Lay(Brief(seed));
        Assert.True(plan.Props.Count > 0, "a town with no props asks this of nothing");

        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            AllGrassWithin(plan, prop, plan.Props.RadiusM[prop]);
        }
    }

    /// <summary>
    /// <b>And one the sweep laid stands the walk's own corner reach clear of it too</b> (GEN-6a) — which
    /// follows from the stand-off that pass keeps and is asserted separately because the two are different
    /// figures: the day the stand-off drops below that reach, a wild prop can stand where the shell turns a
    /// corner (TER-3c.3). Which props those are is read back off where they stand: one on no paved edge's
    /// verge is one no edge walk put there.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryPropTheSweepLaidKeepsTheKerbsCornerClear(ulong seed)
    {
        var plan = Lay(Brief(seed));
        var edges = PavedEdge.Of(plan, Config);
        var swept = 0;

        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            if (edges.InAVerge(plan.Props.CentreM[prop], Config)) continue;

            swept++;
            AllGrassWithin(plan, prop, plan.Props.RadiusM[prop] + Config.PavementCornerReachM);
        }

        Assert.True(swept > 0, "a town whose props are all on a verge asks this of nothing");
    }

    static void AllGrassWithin(CityPlan plan, int prop, float standM)
    {
        var atM = plan.Props.CentreM[prop];
        for (var downM = -standM; downM <= standM; downM += Config.Terrain.GroundStepM)
        {
            for (var overM = -standM; overM <= standM; overM += Config.Terrain.GroundStepM)
            {
                var offsetM = new Vector2(overM, downM);
                if (offsetM.LengthSquared() > standM * standM) continue;

                var onM = atM + offsetM;
                Assert.True(
                    GroundAt(plan, onM) == Ground.Grass,
                    $"prop {prop} at {atM.X:F1},{atM.Y:F1} reaches {GroundAt(plan, onM)} at " +
                    $"{onM.X:F1},{onM.Y:F1} within {standM:F2} m");
            }
        }
    }

    /// <summary>
    /// <b>Every prop stands in a verge or well clear of every one, and never between the two</b> (GEN-6b).
    /// Measured off the paved edges themselves rather than off the cells, because the strip the passes
    /// leave between them is metres wide and a cell is one: what the ground is classified as cannot say
    /// where a verge ends to a metre, and the lines the paving was laid off can.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryPropIsInAVergeOrWellClearOfOne(ulong seed)
    {
        var plan = Lay(Brief(seed));
        Assert.True(plan.Props.Count > 0, "a town with no props asks this of nothing");

        var edges = PavedEdge.Of(plan, Config);
        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            var atM = plan.Props.CentreM[prop];

            // The wild pass keeps its stand-off off the ground's <em>cells</em>, whose outer centres stand
            // half a cell inside the edge this measures to; the verge band carries the sampling's own
            // tolerance. What is left between the two is still metres of strip.
            Assert.True(
                edges.InAVerge(atM, Config)
                || edges.NearestM(atM) > Config.CityGen.PropWildStandOffM - Config.Terrain.GroundStepM,
                $"the prop at {atM.X:F1},{atM.Y:F1} stands {edges.NearestM(atM):F2} m off the nearest " +
                $"paving, which is neither a verge nor clear of one " +
                $"({Config.CityGen.PropWildStandOffM:F1} m)");
        }
    }

    /// <summary>
    /// <b>A prop laid along a paved edge carries that edge's own bearing there</b> (GEN-6b), so a look with
    /// a front runs with the street or with the car park it stands beside. Asked of <em>an</em> edge beside
    /// it and not of the nearest: two can both pass within a verge of the same prop, and either bearing is
    /// the right answer.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryPropInTheVergeCarriesTheBearingOfAnEdgeBesideIt(ulong seed)
    {
        var plan = Lay(Brief(seed));
        var edges = PavedEdge.Of(plan, Config);
        var laid = 0;

        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            var kind = (PropKind)plan.Props.Kind[prop];
            if (kind == PropKind.WildNature) continue;

            laid++;
            var atM = plan.Props.CentreM[prop];
            var bearingRad = plan.Props.BearingRad[prop];
            Assert.True(
                edges.RunsOn(atM, bearingRad, Config),
                $"the {kind} at {atM.X:F1},{atM.Y:F1} carries {bearingRad:F3} rad, which is no paved " +
                "edge's bearing within a verge of it");
        }

        Assert.True(laid > 0, "a town with nothing planted or furnished asks this of nothing");
    }

    /// <summary>
    /// Every edge a verge is measured from — each road's two kerb lines and each car park's four sides —
    /// sampled a metre apart and bucketed, so what the paving was doing beside a prop is a look-up over
    /// nine squares rather than a sweep over every arc and rectangle in the town.
    /// </summary>
    /// <remarks>
    /// <b>A sample carries the walk that wraps its own edge</b>, because the two kinds of edge are measured
    /// from different places: a road's kerb line already has the pavement outside it, and a car park's
    /// tarmac has a claimed ring of walkable grass round it (GEN-4d) that its verge begins past. The stand
    /// -off the wild pass keeps is off the paving itself either way, which is what these lines are.
    /// </remarks>
    sealed class PavedEdge(float squareM)
    {
        /// <summary>How far off a sample's bearing the exact one beside a prop may be, a metre of arc apart on the tightest bend a road is laid on.</summary>
        const float ApartRad = 0.05f;

        /// <summary>How far a distance measured to the samples may stand off the distance to the shape they were taken from.</summary>
        const float NearEnoughM = 0.05f;

        /// <summary>
        /// How finely an edge is sampled. <b>It is what buys the tolerance above</b>: the nearest sample to
        /// a prop is up to half a step along the edge from the nearest point, which at a verge's own width
        /// puts the measured distance a step squared over the true one — a quarter of a metre keeps that
        /// inside the tolerance, where a whole one does not.
        /// </summary>
        const float SampledM = 0.25f;

        readonly Dictionary<(int Column, int Row), List<(Vector2 AtM, float Rad, float WalkM)>> _squares = [];

        public static PavedEdge Of(CityPlan plan, SimConfig config)
        {
            var edges = new PavedEdge(config.CityGen.PropWildStandOffM);

            for (var road = 0; road < plan.Roads.Count; road++)
            {
                var chain = plan.Roads.SegmentsOf(road).ToArray();
                if (chain.Length == 0) continue;

                // The road's own width and not the catalogue's: a one-way street's pavement stands half a
                // carriageway nearer its middle (TER-4d).
                var kerbM = (plan.Roads.WidthM[road] * 0.5f) + config.PavementWidthM;
                var lengthM = Spline.TotalLengthM(chain);
                for (var alongM = 0f; alongM <= lengthM; alongM += SampledM)
                {
                    var on = Spline.SampleAt(chain, alongM);
                    foreach (var hand in (ReadOnlySpan<int>)[-1, 1])
                    {
                        edges.Add(on.PositionM + (on.Right * hand * kerbM), on.HeadingRad, 0f);
                    }
                }
            }

            for (var lot = 0; lot < plan.ParkingLots.Count; lot++)
            {
                var centreM = plan.ParkingLots.CentreM[lot];
                var along = plan.ParkingLots.Axis[lot];
                var across = Heading.RightOf(along);
                var halfM = plan.ParkingLots.HalfExtentM[lot];

                foreach (var side in (ReadOnlySpan<int>)[-1, 1])
                {
                    edges.Side(centreM + (across * (halfM.Y * side)), along, halfM.X, config.PavementWidthM);
                    edges.Side(centreM + (along * (halfM.X * side)), across, halfM.Y, config.PavementWidthM);
                }
            }

            return edges;
        }

        /// <summary>How far the nearest paving is, whatever kind of edge it belongs to.</summary>
        public float NearestM(Vector2 atM)
        {
            var nearestM = float.PositiveInfinity;
            foreach (var (onM, _, _) in Around(atM)) nearestM = MathF.Min(nearestM, Vector2.Distance(onM, atM));

            return nearestM;
        }

        /// <summary>Whether the point stands in some edge's own verge — the band out past the walk that wraps it.</summary>
        public bool InAVerge(Vector2 atM, SimConfig config)
        {
            foreach (var (onM, _, walkM) in Around(atM))
            {
                var outM = Vector2.Distance(onM, atM) - walkM;
                if (outM <= config.CityGen.PropVergeFarM + NearEnoughM) return true;
            }

            return false;
        }

        /// <summary>Whether an edge within a verge of the point was running on the bearing given.</summary>
        public bool RunsOn(Vector2 atM, float bearingRad, SimConfig config)
        {
            var onIt = Heading.Unit(bearingRad);
            foreach (var (onM, rad, walkM) in Around(atM))
            {
                if (Vector2.Distance(onM, atM) - walkM > config.CityGen.PropVergeFarM + NearEnoughM) continue;
                if (Vector2.Dot(Heading.Unit(rad), onIt) >= MathF.Cos(ApartRad)) return true;
            }

            return false;
        }

        void Side(Vector2 middleM, Vector2 tangent, float halfM, float walkM)
        {
            for (var alongM = -halfM; alongM <= halfM; alongM += SampledM)
            {
                Add(middleM + (tangent * alongM), MathF.Atan2(tangent.Y, tangent.X), walkM);
            }

            Add(middleM + (tangent * halfM), MathF.Atan2(tangent.Y, tangent.X), walkM);
        }

        void Add(Vector2 atM, float bearingRad, float walkM)
        {
            var square = Square(atM);
            if (!_squares.TryGetValue(square, out var here)) _squares[square] = here = [];

            here.Add((atM, bearingRad, walkM));
        }

        IEnumerable<(Vector2 AtM, float Rad, float WalkM)> Around(Vector2 atM)
        {
            var (column, row) = Square(atM);
            for (var down = -1; down <= 1; down++)
            {
                for (var over = -1; over <= 1; over++)
                {
                    if (!_squares.TryGetValue((column + over, row + down), out var near)) continue;

                    foreach (var sample in near) yield return sample;
                }
            }
        }

        (int Column, int Row) Square(Vector2 atM) =>
            ((int)MathF.Floor(atM.X / squareM), (int)MathF.Floor(atM.Y / squareM));
    }

    /// <summary>
    /// <b>No two props share any ground</b> (GEN-6c). Asked over a grid of the widest prop's own diameter,
    /// so a pair that overlaps is a pair in the same square or the next one, and the town's whole scatter
    /// is walked once rather than squared.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoTwoPropsShareAnyGround(ulong seed)
    {
        var plan = Lay(Brief(seed));
        Assert.True(plan.Props.Count > 0, "a town with no props asks this of nothing");

        var squareM = 2f * plan.Props.RadiusM.Max();
        var laid = new Dictionary<(int Column, int Row), List<int>>();

        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            var atM = plan.Props.CentreM[prop];
            var reachM = plan.Props.RadiusM[prop];
            var square = ((int)MathF.Floor(atM.X / squareM), (int)MathF.Floor(atM.Y / squareM));

            for (var over = -1; over <= 1; over++)
            {
                for (var down = -1; down <= 1; down++)
                {
                    if (!laid.TryGetValue((square.Item1 + over, square.Item2 + down), out var near)) continue;

                    foreach (var other in near)
                    {
                        var apartM = reachM + plan.Props.RadiusM[other];
                        Assert.True(
                            Vector2.DistanceSquared(plan.Props.CentreM[other], atM) >= apartM * apartM,
                            $"prop {prop} at {atM.X:F1},{atM.Y:F1} reaches prop {other} at " +
                            $"{plan.Props.CentreM[other].X:F1},{plan.Props.CentreM[other].Y:F1}");
                    }
                }
            }

            if (!laid.TryGetValue(square, out var here)) laid[square] = here = [];
            here.Add(prop);
        }
    }


    /// <summary>How many roads meet at each junction, which is what decides whether it may be lit at all.</summary>
    static int[] ArmsOf(CityPlan plan)
    {
        var arms = new int[plan.Junctions.Count];
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            arms[plan.Roads.FromJunction[road]]++;
            arms[plan.Roads.ToJunction[road]]++;
        }

        return arms;
    }

    /// <summary>
    /// Whether a point stands inside any of the map's own water outlines, by the crossings a ray out of it
    /// makes. <b>The outline and not the cells</b>: a bridge's ground is painted road, so the raster has
    /// forgotten what the water was by the time a plan is finished.
    /// </summary>
    static bool InTheWater(CityPlan plan, Vector2 pointM)
    {
        for (var outline = 0; outline < plan.Water.Outline.Count; outline++)
        {
            var points = plan.Water.Outline.RingOf(outline);
            var inside = false;
            for (var edge = 0; edge < points.Length; edge++)
            {
                var a = points[edge];
                var b = points[(edge + 1) % points.Length];
                if (a.Y > pointM.Y == b.Y > pointM.Y) continue;

                if (pointM.X < a.X + ((pointM.Y - a.Y) / (b.Y - a.Y) * (b.X - a.X))) inside = !inside;
            }

            if (inside) return true;
        }

        return false;
    }

    /// <summary>
    /// What the ground is at a point, off the same locator the town itself reads. <b>Kept against the plan
    /// it was laid over</b> so a theory asking it a million times pays for the indexes once.
    /// </summary>
    static Ground GroundAt(CityPlan plan, Vector2 pointM) => Located(plan).GroundAt(pointM);

    static readonly ConditionalWeakTable<CityPlan, GroundLocator> Locators = [];

    static GroundLocator Located(CityPlan plan) => Locators.GetValue(plan, laid => new GroundLocator(laid, Config));

    /// <summary>
    /// What a town is, as one number: everything the plan carries, folded in the order it carries it.
    /// <b>The whole town and not a sample of it</b> — a hash over the roads alone would call two towns the
    /// same when only their people had moved.
    /// </summary>
    static int Shape(CityPlan plan)
    {
        var hash = new HashCode();
        hash.Add(plan.WorldSizeM);
        foreach (var arc in plan.Roads.Segments) hash.Add(arc);
        foreach (var centreM in plan.Junctions.CentreM) hash.Add(centreM);
        foreach (var lit in plan.Junctions.Lit) hash.Add(lit);
        foreach (var centreM in plan.Buildings.CentreM) hash.Add(centreM);
        foreach (var use in plan.Buildings.Use) hash.Add((byte)use);
        foreach (var centreM in plan.ParkingLots.SpacePositionM) hash.Add(centreM);
        foreach (var centreM in plan.Props.CentreM) hash.Add(centreM);
        foreach (var positionM in plan.Spawns.PositionM) hash.Add(positionM);
        return hash.ToHashCode();
    }

    static int Find(int[] root, int node)
    {
        while (root[node] != node)
        {
            root[node] = root[root[node]];
            node = root[node];
        }

        return node;
    }
}
