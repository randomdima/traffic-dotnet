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
/// <b>This is where a shipped city used to be asked its questions</b>, and it is why nothing else in the
/// suite asks one (<see cref="Tier.Maps"/>). Whether a brief and a seed make a sound town is answered here,
/// over four unrelated seeds and both kinds of water, of towns laid for the purpose — so a build may ship
/// any number of cities without any of them being a subject the suite has an opinion about.
/// </para>
/// <para>
/// <b>The town is <see cref="Towns.LaidFrom"/>'s and is laid once a seed.</b> Twenty-six properties over
/// four seeds is a hundred and nine cases and eight towns; a lay per case was three quarters of the tier
/// this project runs after every edit, which is why the class is in the town tier now — eight towns is not
/// engine-free arithmetic however carefully it is memoised.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P3)]
public class GeneratorTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// The brief and the town are <see cref="Towns"/>'s: the suite lays one town per seed and every property
    /// below reads it. <b>Twenty-six properties over four seeds is a hundred and nine cases and eight
    /// towns</b>, and a lay per case was three quarters of the tier this project runs after every edit.
    /// </summary>
    static CityPlan Lay(TownBrief brief) => Towns.LaidFrom(brief.Seed, brief.Water);

    static TownBrief Brief(ulong seed) => Towns.Brief(seed);

    /// <summary>The same town on a coast, which is the water a town may not bridge (GEN-14b).</summary>
    static TownBrief Coast(ulong seed) => Towns.Brief(seed, WaterKind.Coast);

    public static TheoryData<ulong> Seeds() => Towns.Seeds();

    /// <summary>
    /// <b>Laid twice on purpose</b> (GEN-1). This is the one question in the file the shared town cannot
    /// answer: handed it, both sides would be the same object and the case would pass whatever the
    /// generator did.
    /// </summary>
    [Fact]
    public void OneSeedLaysOneTown()
    {
        var once = Towns.LayFresh(Brief(99));
        var again = Towns.LayFresh(Brief(99));

        Assert.Equal(Shape(once), Shape(again));
    }

    [Fact]
    public void AnotherSeedLaysAnotherTown()
    {
        Assert.NotEqual(
            Shape(Towns.LayFresh(Brief(99))),
            Shape(Towns.LayFresh(Brief(100))));
    }

    /// <summary>
    /// <b>A stage's draws are its own</b>: what a later stage is asked for cannot move where the roads went,
    /// because each stage draws on its own stream of the seed. It is what makes a stage worth retuning.
    /// </summary>
    [Fact]
    public void RetuningALaterStageLeavesTheRoadsWhereTheyWere()
    {
        var town = Lay(Brief(3));
        var other = Towns.LayFresh(Towns.Brief(3, cars: 40));

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
        // Swept over the water and not over the town: only a cell that is water can have grass against it,
        // and the water of a river town is a twentieth of its ground. A whole-map sweep at the ground's own
        // step is four million classifications a seed, and all but a few thousand of them are inland.
        var stepM = Config.Terrain.GroundStepM;
        var (fromM, toM) = WetBounds(plan, stepM);
        for (var y = fromM.Y; y < toM.Y; y += stepM)
        {
            for (var x = fromM.X; x < toM.X; x += stepM)
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

    static void AssertNotGrass(CityPlan plan, Vector2 atM)
    {
        if (GroundAt(plan, atM) != Ground.Grass) return;

        Assert.Fail($"grass at {atM.X:F1},{atM.Y:F1} stands against the water");
    }

    /// <summary>
    /// The box every wet cell stands inside, a step proud of the water's own outline so the dry side of the
    /// edge is swept too. A town with no water at all has no box and nothing to sweep.
    /// </summary>
    static (Vector2 FromM, Vector2 ToM) WetBounds(CityPlan plan, float stepM)
    {
        var fromM = new Vector2(float.PositiveInfinity);
        var toM = new Vector2(float.NegativeInfinity);
        foreach (var (_, rings) in Towns.WaterRingsOf(plan.Water))
        {
            for (var ring = 0; ring < rings.Count; ring++)
            {
                foreach (var pointM in rings.RingOf(ring))
                {
                    fromM = Vector2.Min(fromM, pointM);
                    toM = Vector2.Max(toM, pointM);
                }
            }
        }

        if (float.IsInfinity(fromM.X)) return (Vector2.Zero, Vector2.Zero);

        var proudM = new Vector2(stepM * 2f);
        return (Vector2.Max(fromM - proudM, new Vector2(stepM * 0.5f)),
                Vector2.Min(toM + proudM, plan.WorldSizeM));
    }

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
    /// <b>No lane dangles</b> (GEN-50): every lane the town lays is one a car can be driven onto and one it
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
    /// <b>No one-way street meets another</b> (GEN-18): every one of them is entered and left by roads that
    /// admit both ways, so a driver is never handed a junction whose way on is one way too.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoJunctionHasTwoOneWayRoadsAtIt(ulong seed) => Assert.Null(OneWays.Meeting(Lay(Brief(seed))));

    /// <summary>
    /// <b>One-way streets are scattered over the town</b> (GEN-18): no two of them stand within
    /// <see cref="CityGenFigures.OneWayApartMinM"/> of each other, which is what spreads them across every
    /// district rather than gathering them into one.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void OneWayRoadsStandTheirSpacingApart(ulong seed) =>
        Assert.Null(OneWays.Crowding(Lay(Brief(seed)), Config.CityGen.OneWayApartMinM));

    /// <summary>
    /// <b>A one-way street arrives where there is still a choice</b> (GEN-18): at a junction of four arms or
    /// more, so the approaches it meets keep two ways out apiece rather than being driven through a junction
    /// they decide nothing at.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoOneWayStreetArrivesWhereItTakesTheLastChoice(ulong seed) =>
        Assert.Null(OneWays.Starving(Lay(Brief(seed))));

    /// <summary>
    /// <b>A roundabout is a closed ring driven one way round</b> (GEN-19): every road of it runs one way,
    /// each of its junctions is left by exactly one of them and arrived at by exactly one, and the ring
    /// closes — so a car on it comes back to where it entered rather than running out of circle.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryRoundaboutIsAClosedRingDrivenOneWayRound(ulong seed)
    {
        var plan = Lay(Brief(seed));
        for (var ring = 0; ring < plan.Roundabouts.Count; ring++)
        {
            var roads = plan.Roundabouts.RoadsOf(ring);
            var leaving = new Dictionary<int, int>();
            var arriving = new Dictionary<int, int>();
            foreach (var road in roads)
            {
                Assert.True(
                    plan.Roads.Flow[road] != RoadFlow.BothWays,
                    $"road {road} circulates on roundabout {ring} and runs both ways");

                var from = plan.Roads.Flow[road] == RoadFlow.WithTheRoad
                    ? plan.Roads.FromJunction[road]
                    : plan.Roads.ToJunction[road];
                var to = plan.Roads.FromJunction[road] == from
                    ? plan.Roads.ToJunction[road]
                    : plan.Roads.FromJunction[road];
                leaving[from] = leaving.GetValueOrDefault(from) + 1;
                arriving[to] = arriving.GetValueOrDefault(to) + 1;
            }

            Assert.Equal(roads.Length, leaving.Count);
            Assert.Equal(roads.Length, arriving.Count);
            foreach (var (junction, count) in leaving)
            {
                Assert.True(count == 1, $"junction {junction} of roundabout {ring} is left by {count} of its roads");
                Assert.True(
                    arriving.GetValueOrDefault(junction) == 1,
                    $"junction {junction} of roundabout {ring} is arrived at by " +
                    $"{arriving.GetValueOrDefault(junction)} of its roads");
            }
        }
    }

    /// <summary>
    /// <b>A roundabout's ring is smooth</b> (GEN-19): every piece of it is one arc of one circle from node
    /// to node, so there is no straight in it and no join to find — and every piece of one ring is drawn on
    /// the same radius, which is what makes it a circle rather than a run of bends.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryRoundaboutIsOneCircle(ulong seed)
    {
        var plan = Lay(Brief(seed));
        for (var ring = 0; ring < plan.Roundabouts.Count; ring++)
        {
            var radiusM = 0f;
            foreach (var road in plan.Roundabouts.RoadsOf(ring))
            {
                var arcs = plan.Roads.SegmentsOf(road);
                Assert.True(arcs.Length == 1, $"road {road} of roundabout {ring} is {arcs.Length} pieces");
                Assert.True(arcs[0].Curvature != 0f, $"road {road} of roundabout {ring} is straight");

                var onM = 1f / MathF.Abs(arcs[0].Curvature);
                radiusM = radiusM == 0f ? onM : radiusM;
                Assert.True(
                    MathF.Abs(onM - radiusM) <= LineTolerance.OnePlaceM,
                    $"road {road} of roundabout {ring} bends on {onM:F1} m where the ring is {radiusM:F1} m");
            }
        }
    }

    /// <summary>
    /// <b>And it is driven through the junctions it is made of</b> (GEN-19): each piece of the ring starts at
    /// the centre of the junction it leaves and finishes at the centre of the one it arrives at, the way an
    /// arm at any other junction does. A ring carried off that circle — onto the driven half of a corridor it
    /// is the whole of — ends its arms on its own far kerb, and the pavement round the island then runs
    /// exactly half a walk from the end of every one of them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryRoundaboutRunsThroughItsOwnJunctions(ulong seed)
    {
        var plan = Lay(Brief(seed));
        for (var ring = 0; ring < plan.Roundabouts.Count; ring++)
        {
            foreach (var road in plan.Roundabouts.RoadsOf(ring))
            {
                // <b>A standoff along the ring's own circle</b> (TER-5d): every arm's lanes end a standoff
                // out from the node, and on a ring that standoff is a piece of the circle rather than a
                // straight off the tangent to it — so a piece sets off exactly that far from its node and
                // no further.
                var arcs = plan.Roads.SegmentsOf(road);
                var fromM = plan.Junctions.CentreM[plan.Roads.FromJunction[road]];
                var toM = plan.Junctions.CentreM[plan.Roads.ToJunction[road]];
                var standoffM = Config.CityGen.ConnectionStandoffM;
                Assert.True(
                    Vector2.Distance(arcs[0].StartM, fromM) <= standoffM,
                    $"road {road} of roundabout {ring} sets off at {arcs[0].StartM}, " +
                    $"{Vector2.Distance(arcs[0].StartM, fromM):F3} m off the junction it leaves");
                Assert.True(
                    Vector2.Distance(arcs[^1].EndM, toM) <= standoffM,
                    $"road {road} of roundabout {ring} finishes at {arcs[^1].EndM}, " +
                    $"{Vector2.Distance(arcs[^1].EndM, toM):F3} m off the junction it arrives at");
            }
        }
    }

    /// <summary>
    /// <b>A roundabout is driven with its island on the side the traffic does not keep</b> (GEN-19): a car
    /// goes round turning away from the kerb it drives against, which is anticlockwise where traffic keeps
    /// right.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryRoundaboutTurnsAwayFromTheSideTheTrafficKeeps(ulong seed)
    {
        var plan = Lay(Brief(seed));
        for (var ring = 0; ring < plan.Roundabouts.Count; ring++)
        {
            foreach (var road in plan.Roundabouts.RoadsOf(ring))
            {
                // Read the way it is driven: a road driven against its own line is turning the other way
                // from the way its arcs are written (TER-4d).
                var driven = plan.Roads.Flow[road] == RoadFlow.WithTheRoad ? 1f : -1f;
                foreach (var arc in plan.Roads.SegmentsOf(road))
                {
                    if (arc.Curvature == 0f) continue;

                    Assert.True(
                        MathF.Sign(arc.Curvature * driven) == -MathF.Sign(Config.RoadSideSign),
                        $"road {road} of roundabout {ring} bends towards the side the traffic keeps");
                }
            }
        }
    }

    /// <summary>
    /// <b>Nothing on a roundabout's ring is lit</b> (GEN-19): its circulating traffic is driven over what is
    /// entering by the ranking alone (TER-5e), and a timetable there would stop the circle to let an arm in.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoJunctionOnARoundaboutIsLit(ulong seed)
    {
        var plan = Lay(Brief(seed));
        var ringOf = RingOf(plan);
        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            Assert.False(
                ringOf[junction] >= 0 && plan.Junctions.Lit[junction],
                $"junction {junction} stands on a roundabout and is lit");
        }
    }

    /// <summary>
    /// <b>No zebra is painted on a roundabout's circulating carriageway</b> (GEN-19): a walk laid across it
    /// is a walk across the traffic a roundabout exists to keep moving, and the ring's entries carry the
    /// crossings somebody getting round one uses.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoCrossingIsPaintedOnARoundabout(ulong seed)
    {
        var plan = Lay(Brief(seed));
        var circulating = OneWays.Circulating(plan);
        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            Assert.False(
                circulating[plan.Crosswalks.Road[crossing]],
                $"crossing {crossing} at {plan.Crosswalks.CentreM[crossing]} is painted on a roundabout");
        }
    }

    /// <summary>
    /// <b>The brief's share of the junctions that can carry lights is left unlit, to the junction</b> (TLT-3):
    /// every junction of three arms or more that is on no ring and is no car park's is in the draw, and nothing
    /// else is ever lit.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheBriefsShareOfTheJunctionsThatCanBeLitIsLit(ulong seed)
    {
        var brief = Brief(seed);
        var plan = Lay(brief);
        var drawn = Lightable(plan);

        var candidates = 0;
        var lit = 0;
        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            if (!drawn[junction])
            {
                Assert.False(plan.Junctions.Lit[junction], $"junction {junction} cannot carry lights and is lit");
                continue;
            }

            candidates++;
            if (plan.Junctions.Lit[junction]) lit++;
        }

        Assert.Equal((int)MathF.Round(candidates * (1f - brief.UnregulatedJunctionShare)), lit);
    }

    /// <summary>
    /// <b>A junction is lit more often the more arms it has</b> (TLT-3): the draw is weighted by the movements
    /// each admits, so of the junctions that can carry lights a larger share of the crossroads is lit than of
    /// the tees.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void ACrossroadsIsLitMoreOftenThanATee(ulong seed)
    {
        var plan = Lay(Brief(seed));
        var drawn = Lightable(plan);
        var arms = ArmsOf(plan);

        var (tees, teesLit, crossroads, crossroadsLit) = (0, 0, 0, 0);
        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            if (!drawn[junction]) continue;

            var isLit = plan.Junctions.Lit[junction] ? 1 : 0;
            if (arms[junction] == 3) (tees, teesLit) = (tees + 1, teesLit + isLit);
            else (crossroads, crossroadsLit) = (crossroads + 1, crossroadsLit + isLit);
        }

        Assert.True(tees > 0 && crossroads > 0, $"seed {seed} lays {tees} tees and {crossroads} crossroads to draw from");
        Assert.True(
            (float)crossroadsLit / crossroads > (float)teesLit / tees,
            $"{crossroadsLit} of {crossroads} crossroads lit against {teesLit} of {tees} tees");
    }

    /// <summary>
    /// The junctions that can carry lights (TLT-3): three arms or more, on no roundabout's ring (GEN-19) and
    /// cut for no car park (GEN-53).
    /// </summary>
    static bool[] Lightable(CityPlan plan)
    {
        var arms = ArmsOf(plan);
        var ringOf = RingOf(plan);
        var drawn = new bool[plan.Junctions.Count];
        for (var junction = 0; junction < drawn.Length; junction++) drawn[junction] = arms[junction] >= 3 && ringOf[junction] < 0;

        return drawn;
    }

    /// <summary>
    /// <b>A roundabout is a junction of four arms or more</b> (GEN-19): a node of three is a junction the
    /// ranking settles standing still, so what the town lays there is the junction and not a circle. Its
    /// ring carries one node for every arm, so four arms are four roads circulating.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoRoundaboutIsLaidWhereAJunctionWouldDo(ulong seed)
    {
        var plan = Lay(Brief(seed));
        for (var roundabout = 0; roundabout < plan.Roundabouts.Count; roundabout++)
        {
            Assert.True(
                plan.Roundabouts.RoadsOf(roundabout).Length >= Roundabouts.ArmsLeast,
                $"roundabout {roundabout} circulates on {plan.Roundabouts.RoadsOf(roundabout).Length} roads, "
                + $"so it opened out a junction of fewer than {Roundabouts.ArmsLeast} arms");
        }
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
    /// <b>A junction is the only place two roads touch</b> (GEN-49): every pair that meets at none stands at
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
        var footprintM = Config.RoadFootprintM;

        // Every pair of points on every pair of roads is a hundred million comparisons on a town this size,
        // and all but a handful of them are half a kilometre apart. The points are dropped into squares of
        // the footprint being asked about, so a point is compared against the nine squares that could hold
        // anything close enough to be an offence and against nothing else.
        var squares = new Dictionary<(int Column, int Row), List<(int Road, Vector2 AtM)>>();
        for (var road = 0; road < walked.Length; road++)
        {
            foreach (var atM in walked[road])
            {
                var key = ((int)MathF.Floor(atM.X / footprintM), (int)MathF.Floor(atM.Y / footprintM));
                if (!squares.TryGetValue(key, out var here)) squares[key] = here = [];
                here.Add((road, atM));
            }
        }

        for (var road = 0; road < walked.Length; road++)
        {
            foreach (var atM in walked[road])
            {
                var column = (int)MathF.Floor(atM.X / footprintM);
                var row = (int)MathF.Floor(atM.Y / footprintM);
                for (var overM = -1; overM <= 1; overM++)
                {
                    for (var downM = -1; downM <= 1; downM++)
                    {
                        if (!squares.TryGetValue((column + overM, row + downM), out var here)) continue;

                        foreach (var (other, elseM) in here)
                        {
                            if (other <= road || SharesAJunction(plan, road, other)) continue;

                            var apartM = (atM - elseM).Length();
                            if (apartM >= footprintM) continue;

                            Assert.Fail(
                                $"roads {road} and {other} meet no junction yet pass {apartM:F1} m apart at " +
                                $"{atM.X:F0},{atM.Y:F0}, inside the {footprintM:F1} m one road takes — "
                                + $"{road} joins {plan.Roads.FromJunction[road]} to {plan.Roads.ToJunction[road]} "
                                + $"passing {plan.Roads.ThroughOf(road).Length}, {other} joins "
                                + $"{plan.Roads.FromJunction[other]} to {plan.Roads.ToJunction[other]} passing "
                                + $"{plan.Roads.ThroughOf(other).Length}");
                        }
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
        var ringOf = RingOf(plan);
        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            for (var other = junction + 1; other < plan.Junctions.Count; other++)
            {
                // <b>Two nodes of one roundabout are exempt</b> (GEN-19): they are one junction laid out as
                // a circle rather than two spacings that happened to collide, and what they owe each other
                // is the road between them (TER-5a).
                if (ringOf[junction] >= 0 && ringOf[junction] == ringOf[other]) continue;

                var apartM = (plan.Junctions.CentreM[junction] - plan.Junctions.CentreM[other]).Length();
                Assert.True(
                    apartM >= Config.CityGen.LocalityM,
                    $"junctions {junction} at {plan.Junctions.CentreM[junction]} (ring {ringOf[junction]}, " +
                    $"{ArmsOf(plan)[junction]} arms) and {other} at {plan.Junctions.CentreM[other]} " +
                    $"(ring {ringOf[other]}, {ArmsOf(plan)[other]} arms) stand {apartM:F1} m apart, inside " +
                    $"a locality of {Config.CityGen.LocalityM:F0} m; the town has {plan.Roundabouts.Count} rings");
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
    /// <b>A road is one line and not a row of them</b> (GEN-47): every joint in its chain sets off on the
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
    /// How open a joint may read and still be one line, which is the road stage's own figure and not a
    /// second one: <b>what says a road is one line has to be what says two arms are one road</b>
    /// (<see cref="RoadStage.CreaseRad"/>).
    /// </summary>
    static float CreaseRad => RoadStage.CreaseRad(Config);

    /// <summary>
    /// <b>No junction is a place an ordinary road merely carries on through</b> (GEN-51). A node whose two
    /// arms leave it half a turn apart is one road cut at a point nothing meets at — a standoff, a pair of
    /// movements and a claim laid in the middle of a carriageway — and the town joins those into the one
    /// road they are.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the deflection alone, and of the junctions GEN-51 does not exempt.</b> Every reason a
    /// junction of two arms may stand between two ordinary roads is a corner — the place a loop was
    /// shortened at, or the place a joined road the stage would not lay was cut in two — and a corner
    /// deflects. A bridgehead and a ring node are the two that need not, being where the carriageway itself
    /// changes, and the rule names both.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoJunctionIsAPlaceARoadCarriesOnThrough(ulong seed)
    {
        var plan = Lay(Brief(seed));
        var ordinary = new bool[plan.Roads.Count];
        Array.Fill(ordinary, true);
        foreach (var road in plan.Bridges.Road) ordinary[road] = false;
        for (var ring = 0; ring < plan.Roundabouts.Count; ring++)
        {
            foreach (var road in plan.Roundabouts.RoadsOf(ring)) ordinary[road] = false;
        }

        var arms = new List<float>[plan.Junctions.Count];
        var ordinaryArms = new int[plan.Junctions.Count];
        for (var junction = 0; junction < arms.Length; junction++) arms[junction] = [];

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var chain = plan.Roads.SegmentsOf(road);
            if (chain.Length == 0) continue;

            foreach (var (junction, bearingRad) in (ReadOnlySpan<(int, float)>)[
                         (plan.Roads.FromJunction[road], chain[0].HeadingRad),
                         (plan.Roads.ToJunction[road], chain[^1].HeadingAtRad(chain[^1].LengthM) + MathF.PI)])
            {
                arms[junction].Add(bearingRad);
                if (ordinary[road]) ordinaryArms[junction]++;
            }
        }

        for (var junction = 0; junction < arms.Length; junction++)
        {
            if (arms[junction].Count != 2 || ordinaryArms[junction] != 2) continue;

            var throughRad = MathF.PI - MathF.Abs(Spline.WrapRad(arms[junction][0] - arms[junction][1]));
            Assert.True(
                MathF.Abs(throughRad) > CreaseRad,
                $"junction {junction} at {plan.Junctions.CentreM[junction]} is one road carrying straight "
                + $"on, its two arms {throughRad:F4} rad off half a turn");
        }
    }

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
    /// <b>A road laid straight that passes nowhere is straight end to end</b> (GEN-47): its arms stand on the
    /// chord and it wanders nowhere, so nothing is left to bend it — cut into car parks or not.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryRoadLaidStraightThatPassesNowhereIsStraight(ulong seed)
    {
        var plan = Lay(Brief(seed));
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (!plan.Roads.IsLaidStraight(road) || plan.Roads.ThroughOf(road).Length > 0) continue;

            foreach (var piece in plan.Roads.SegmentsOf(road)) Assert.Equal(0f, piece.Curvature);
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

        var edges = PavedEdge.Of(plan, Config);
        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            AllGrassWithin(plan, prop, plan.Props.RadiusM[prop], edges);
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
            if (edges.InAVerge(plan.Props.CentreM[prop], plan.Props.RadiusM[prop], Config)) continue;

            swept++;
            AllGrassWithin(plan, prop, plan.Props.RadiusM[prop] + Config.PavementCornerReachM, edges);
        }

        Assert.True(swept > 0, "a town whose props are all on a verge asks this of nothing");
    }

    static void AllGrassWithin(CityPlan plan, int prop, float standM, PavedEdge edges)
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
                    $"{onM.X:F1},{onM.Y:F1} within {standM:F2} m — r {plan.Props.RadiusM[prop]:F2}, " +
                    $"{(PropKind)plan.Props.Kind[prop]}, nearest face {edges.NearestM(atM):F2} m");
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
                edges.InAVerge(atM, plan.Props.RadiusM[prop], Config)
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
                edges.RunsOn(atM, plan.Props.RadiusM[prop], bearingRad, Config),
                $"the {kind} at {atM.X:F1},{atM.Y:F1} carries {bearingRad:F3} rad, which is no paved " +
                "edge's bearing within a verge of it");
        }

        Assert.True(laid > 0, "a town with nothing planted or furnished asks this of nothing");
    }

    /// <summary>
    /// The line a verge is measured from — the walk's outer face, which is the whole of the town's paved
    /// edge (GEN-6b) — sampled finely and bucketed, so what the paving was doing beside a prop is a look-up
    /// over nine squares rather than a sweep over every arc in the town's boundary.
    /// </summary>
    /// <remarks>
    /// <b>It is the plan's own rings and not a line laid again here</b> (<c>GroundRings.WalkEdge</c>). What
    /// the pass is asked is whether it placed against the face the town was drawn with, so a second
    /// construction of that face would be asking whether two constructions agree.
    /// </remarks>
    sealed class PavedEdge(float squareM)
    {
        /// <summary>
        /// How far off a sample's bearing the exact one beside a prop may be. <b>It is also what bounds the
        /// step</b>: the face is rounded at a radius of its own (TER-3c.10) and spends a quarter of a turn
        /// over a metre of it, so a step fixed in metres would put a corner's samples further off each other
        /// in bearing than this whole tolerance.
        /// </summary>
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

        readonly Dictionary<(int Column, int Row), List<(Vector2 AtM, float Rad)>> _squares = [];

        public static PavedEdge Of(CityPlan plan, SimConfig config)
        {
            var edges = new PavedEdge(config.CityGen.PropWildStandOffM);

            // Piece by piece and never over the chain, because the step is the piece's own: a bend a walk
            // of the whole ring stepped into would spend its heading inside one step of a straight.
            foreach (var face in plan.Paving(config).Rings(config).WalkEdge)
            {
                foreach (var arc in face)
                {
                    var stepM = StepM(arc.Curvature);
                    for (var alongM = 0f; alongM < arc.LengthM; alongM += stepM)
                    {
                        edges.Add(arc.PointAtM(alongM), arc.HeadingAtRad(alongM));
                    }

                    edges.Add(arc.PointAtM(arc.LengthM), arc.HeadingAtRad(arc.LengthM));
                }
            }

            return edges;
        }

        /// <summary>
        /// How far along one piece the next sample stands: <see cref="SampledM"/>, or the arc a bend of this
        /// curvature spends half of <see cref="ApartRad"/> over, whichever is shorter.
        /// </summary>
        static float StepM(float curvature)
        {
            var turningM = MathF.Abs(curvature) <= float.Epsilon
                ? SampledM
                : ApartRad * 0.5f / MathF.Abs(curvature);

            return MathF.Max(MathF.Min(SampledM, turningM), 1e-3f);
        }

        /// <summary>How far the nearest paved edge is.</summary>
        public float NearestM(Vector2 atM)
        {
            var nearestM = float.PositiveInfinity;
            foreach (var (onM, _) in Around(atM)) nearestM = MathF.Min(nearestM, Vector2.Distance(onM, atM));

            return nearestM;
        }

        /// <summary>
        /// Whether a prop of this girth stands in the verge — <b>its near rim in the band</b> and not its
        /// centre, which is how the pass places one (GEN-6b).
        /// </summary>
        public bool InAVerge(Vector2 atM, float reachM, SimConfig config)
        {
            foreach (var (onM, _) in Around(atM))
            {
                var outM = Vector2.Distance(onM, atM) - reachM;
                if (outM <= config.CityGen.PropVergeFarM + NearEnoughM) return true;
            }

            return false;
        }

        /// <summary>Whether an edge within a verge of the prop was running on the bearing given.</summary>
        public bool RunsOn(Vector2 atM, float reachM, float bearingRad, SimConfig config)
        {
            var onIt = Heading.Unit(bearingRad);
            foreach (var (onM, rad) in Around(atM))
            {
                if (Vector2.Distance(onM, atM) - reachM > config.CityGen.PropVergeFarM + NearEnoughM) continue;
                if (Vector2.Dot(Heading.Unit(rad), onIt) >= MathF.Cos(ApartRad)) return true;
            }

            return false;
        }

        void Add(Vector2 atM, float bearingRad)
        {
            var square = Square(atM);
            if (!_squares.TryGetValue(square, out var here)) _squares[square] = here = [];

            here.Add((atM, bearingRad));
        }

        IEnumerable<(Vector2 AtM, float Rad)> Around(Vector2 atM)
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

    /// <summary>Which roundabout each junction stands on, or <c>−1</c> where it stands on none (GEN-19).</summary>
    static int[] RingOf(CityPlan plan)
    {
        var ringOf = new int[plan.Junctions.Count];
        Array.Fill(ringOf, -1);
        for (var ring = 0; ring < plan.Roundabouts.Count; ring++)
        {
            foreach (var road in plan.Roundabouts.RoadsOf(ring))
            {
                ringOf[plan.Roads.FromJunction[road]] = ring;
                ringOf[plan.Roads.ToJunction[road]] = ring;
            }
        }

        return ringOf;
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
