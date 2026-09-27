using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// TER-7's claim, asked of every shipped map: <b>the ground drawn and the ground answered for are one
/// geometry</b>. Every assertion here is a shape read off the plan and asked of the locator — and asked
/// <em>exactly</em>, with no allowance anywhere, because there is no longer a second representation for
/// the answer to be off by half of.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P4)]
public class GroundLocatorTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    public static TheoryData<string> Maps => Towns.EveryTown();

    static GroundLocator GroundOf(string map) => new(Towns.Of(map), Config);

    [Theory]
    [MemberData(nameof(Maps))]
    public void AJunctionIsGroundACarMayBeOn(string map)
    {
        var plan = Towns.Of(map);
        var config = SimConfig.Shipped();
        var ground = GroundOf(map);
        var lanes = plan.Paving(config).Lanes;

        // How many movements cross each node, which is the whole of what a junction is on the ground
        // (TER-5): the tarmac inside one is the band those movements sweep and nothing else.
        var crossed = new int[plan.Junctions.Count];
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var junction = lanes.JunctionOfConnector(connector);
            if (junction >= 0) crossed[junction]++;
        }

        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            // <b>A junction is the movements that cross in it</b> (TER-5), so a node nothing is turned
            // through is a node with no ground of its own: the end of a road, where the lane stops short of
            // the node the way it does at every other junction. The ground there is what the town lays
            // beside a kerb, and the node is a place in the plan rather than a piece of tarmac.
            //
            // <b>And a node two movements cross is asked nothing either</b>: they are the two ways of one
            // road bending through it, each laid half a lane off the node's own line, so what they leave
            // uncovered is the line itself. A box is three arms and up, where the movements weave.
            if (crossed[junction] <= 2) continue;

            var centreM = plan.Junctions.CentreM[junction];
            Assert.True(ground.At(centreM).Drivable,
                $"{map}: junction {junction} at {centreM} stands on {ground.GroundAt(centreM)}");
        }
    }

    /// <summary>
    /// A crossing is a stretch of carriageway a pedestrian may use and <em>not</em> a break in it, so both
    /// permissions hold at once (TER-6).
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ACrossingIsCarriagewayAPersonMayUse(string map)
    {
        var plan = Towns.Of(map);
        var ground = GroundOf(map);

        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            var centreM = plan.Crosswalks.CentreM[crossing];
            var at = ground.At(centreM);
            Assert.True(at.Walkable && at.Drivable,
                $"{map}: crossing {crossing} at {centreM} stands on {at.Ground}");
        }
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void ABayIsGroundBothKindsMayBeOn(string map)
    {
        var plan = Towns.Of(map);
        var ground = GroundOf(map);

        for (var space = 0; space < plan.ParkingLots.SpaceCount; space++)
        {
            var poseM = plan.ParkingLots.SpacePositionM[space];
            var at = ground.At(poseM);
            Assert.True(at.Walkable && at.Drivable, $"{map}: bay {space} at {poseM} stands on {at.Ground}");
        }
    }

    /// <summary>OBJ-4: a building exposes at least one point on walkable ground for people to enter by.</summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryWayIntoABuildingIsOnGroundAPersonMayWalk(string map)
    {
        var plan = Towns.Of(map);
        var ground = GroundOf(map);

        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var from = plan.Buildings.EntryOffsets[building];
            var to = plan.Buildings.EntryOffsets[building + 1];
            Assert.True(to > from, $"{map}: building {building} has no way in");

            for (var entry = from; entry < to; entry++)
            {
                var pointM = plan.Buildings.EntryPointM[entry];
                Assert.True(ground.At(pointM).Walkable,
                    $"{map}: building {building}'s way in at {pointM} stands on {ground.GroundAt(pointM)}");
            }
        }
    }

    /// <summary>
    /// The strongest form of the claim: walk each road's own arcs and the locator answers carriageway under
    /// every one of them, on the curve as well as on the straight.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheGroundUnderEveryRoadsOwnCurveIsCarriageway(string map)
    {
        var plan = Towns.Of(map);
        var config = SimConfig.Shipped();
        var ground = GroundOf(map);
        var lanes = plan.Paving(config).Lanes;

        // <b>Asked of the lanes and not of the roads.</b> The ground a car may be on is the ground a car is
        // driven over, and that is the band every lane lays — a road's own band runs on to the junctions at
        // its ends while its lanes are cut back from them, so its last metres are a fact about the plan
        // rather than about the tarmac.
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            foreach (var segment in lanes.ArcsOf(lane))
            {
                for (var distanceM = 0f; distanceM <= segment.LengthM; distanceM += 2f)
                {
                    var onTheLaneM = segment.PointAtM(distanceM);
                    Assert.True(ground.At(onTheLaneM).Drivable,
                        $"{map}: lane {lane} at {onTheLaneM} runs over {ground.GroundAt(onTheLaneM)}");
                }
            }
        }
    }

    /// <summary>
    /// <b>The claim the cells could not make.</b> A road's kerb is exactly where the road says it is: a
    /// hair inside the half-width is carriageway, and a hair outside it is not — on the curve, where the
    /// old classifier was allowed to be half a cell out and a corner fillet was the worst of it.
    /// </summary>
    /// <remarks>
    /// Only the stretches of road no other shape reaches are asked, because everything else is a shape
    /// laid <em>over</em> the carriageway and the answer there is that shape's: what is tested is the road
    /// against its own edge, not the precedence, which the claims above already walk.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ARoadsKerbIsWhereTheRoadSaysItIs(string map)
    {
        var plan = Towns.Of(map);
        var ground = GroundOf(map);

        const float hairM = 0.01f;
        var asked = 0;
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            var halfM = plan.Roads.WidthM[road] * 0.5f;
            var lengthM = Spline.TotalLengthM(arcs);
            for (var alongM = 0f; alongM <= lengthM; alongM += 2f)
            {
                var on = Spline.SampleAt(arcs, MathF.Min(alongM, lengthM));
                foreach (var side in (ReadOnlySpan<float>)[1f, -1f])
                {
                    var insideM = on.PositionM + (on.Right * side * (halfM - hairM));
                    var outsideM = on.PositionM + (on.Right * side * (halfM + hairM));

                    // A junction disc, a kerb fillet or a car park standing over this piece of kerb owns the
                    // ground either side of it, and what it answers is that shape's business.
                    if (ground.GroundAt(insideM) != Ground.Road) continue;
                    if (ground.GroundAt(outsideM) is not (Ground.Sidewalk or Ground.Grass)) continue;

                    asked++;
                    Assert.False(ground.At(outsideM).Drivable,
                        $"{map}: road {road} at {alongM:F0} m is still drivable {hairM * 100f:F0} cm past " +
                        $"its own kerb, on {ground.GroundAt(outsideM)}");
                }
            }
        }

        Assert.True(asked > 0, $"{map}: no stretch of kerb stands clear of every other shape to be asked about");
    }

    /// <summary>
    /// <b>The concrete is the ground within a walk of the tarmac and nothing beyond it</b> (TER-7b,
    /// TER-3c.3). A layer is the town's own boundary moved by one figure, so every point the answer calls
    /// pavement stands within the pavement's outer face of that boundary, and every point that does not is
    /// the verge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured off the boundary the walking network reads</b> (<see cref="KerbLines"/>) and not off the
    /// rings the answer reads, so what is weighed here is two constructions against one another rather than
    /// one against itself.
    /// </para>
    /// <para>
    /// <b>The slack is the rounding and not a tolerance</b> (<see cref="RoadFigures.LineRoundedM"/>,
    /// TER-3c.10): the walk's rings are rounded at their corners where the merge the kerb is read off is not,
    /// so the two stand up to that radius apart at a corner and nowhere else.
    /// </para>
    /// <para>
    /// <b>A deck's margin and a shore are the two grounds that are concrete away from any kerb</b> — one a
    /// ribbon about a road's own line and one a ring the water is set in — so they are excluded by name
    /// rather than by a distance, which is what keeps this a claim about the layer.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoConcreteStandsFurtherOffTheBoundaryThanTheWalkReaches(string map)
    {
        var plan = Towns.Of(map);
        var ground = GroundOf(map);
        var kerbs = KerbLines.Of(plan, Config);
        var reachM = Config.WalkOuterM + Config.Road.LineRoundedM;
        var stepM = Config.Terrain.GroundStepM * 3f;

        var onTheWalk = 0;
        for (var y = stepM * 0.5f; y < plan.WorldSizeM.Y; y += stepM)
        {
            for (var x = stepM * 0.5f; x < plan.WorldSizeM.X; x += stepM)
            {
                // Everything else in the town answers walkable-but-not-preferred or nothing at all, so the
                // ground the claim is about is the only ground this costs anything to ask about.
                var pointM = new Vector2(x, y);
                if (!ground.At(pointM).Preferred) continue;

                if (kerbs.NearestTo(pointM, out var kerb)
                    && Vector2.Distance(pointM, kerb.PositionM) <= reachM)
                {
                    onTheWalk++;
                    continue;
                }

                if (UnderABridge(plan, pointM) || InsideAnyRing(plan.Water.Shore, pointM)) continue;

                Assert.Fail(
                    $"{map}: {pointM.X:F0},{pointM.Y:F0} is answered {ground.GroundAt(pointM)} and stands " +
                    $"more than {reachM:F2} m off the town's boundary");
            }
        }

        Assert.True(onTheWalk > 0, $"{map}: no point of the town is answered as the pavement at all");
    }

    /// <summary>
    /// <b>The lanes a walker is held on stand on the concrete</b> (TER-3c.3, WLK-1). The walking network is
    /// the boundary moved off itself and reads no ground at all; the walk the ground answers is that same
    /// boundary moved by its own figure. What the two owe each other is exactly this — every stretch of
    /// pavement lane runs over ground a walker prefers — and it is owed because both are sized off the
    /// carriageway's own width, not because either asked the other.
    /// </summary>
    /// <remarks>
    /// A crossing is not asked: it is paint over a carriageway on purpose (TER-6), so the ground under one
    /// is the road's and a walker is admitted to it by the crossing rather than by the surface.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryPavementLaneRunsOverGroundAWalkerPrefers(string map)
    {
        var plan = Towns.Of(map);
        var ground = GroundOf(map);
        var ways = FootWays.Lay(plan, Config);

        var walked = 0;
        for (var way = 0; way < ways.Count; way++)
        {
            if (ways.KindOf(way) == FootConnectorKind.Crossing) continue;

            for (var lane = 0; lane < FootConnectors.LanesPerWay; lane++)
            {
                var line = ways.LaneOf(way, lane);
                if (line.Length == 0) continue;

                walked++;
                var lengthM = Spline.TotalLengthM(line);
                for (var alongM = 0f; alongM <= lengthM; alongM += 2f)
                {
                    var atM = Spline.SampleAt(line, MathF.Min(alongM, lengthM)).PositionM;
                    Assert.True(ground.At(atM).Preferred,
                        $"{map}: a walk {ways.KindOf(way)} runs over {ground.GroundAt(atM)} at " +
                        $"{atM.X:F0},{atM.Y:F0}, {alongM:F0} m along its own lane");
                }
            }
        }

        Assert.True(walked > 0, $"{map}: no walk is laid along the boundary at all");
    }

    /// <summary>
    /// <b>The pavement is the same surface as the carriageway to a wheel</b> (TER-2): everything inside the
    /// town's boundary is asphalt, so a car that puts two wheels over the kerb keeps its grip, its drag and
    /// its mark threshold and loses only its legality. What it is <em>permitted</em> to do there is the
    /// catalogue's and is a different answer.
    /// </summary>
    /// <remarks>
    /// Compared against the tarmac this same town answers rather than against a figure, so the claim is that
    /// the two grounds are one surface and not that either is worth some number.
    /// </remarks>
    [Fact]
    public void ThePavementIsTheSameSurfaceAsTheCarriagewayToAWheel()
    {
        var plan = Towns.Of(Towns.Fixture);
        var ground = GroundOf(Towns.Fixture);
        var paving = plan.Paving(Config);
        var ways = FootWays.Lay(plan, Config);

        var onALaneM = Spline.SampleAt(paving.ArcsOfDriven(0), paving.DrivenLengthM(0) * 0.5f).PositionM;
        Assert.Equal(Ground.Road, ground.GroundAt(onALaneM));
        var tarmac = ground.EffectAt(onALaneM);

        var walked = 0;
        for (var way = 0; way < ways.Count; way++)
        {
            if (ways.KindOf(way) == FootConnectorKind.Crossing) continue;

            for (var lane = 0; lane < FootConnectors.LanesPerWay; lane++)
            {
                var line = ways.LaneOf(way, lane);
                if (line.Length == 0) continue;

                walked++;
                var atM = Spline.SampleAt(line, Spline.TotalLengthM(line) * 0.5f).PositionM;
                Assert.Equal(tarmac, ground.EffectAt(atM));
            }
        }

        Assert.True(walked > 0, "the fixture town lays no walk along its boundary at all");
    }

    /// <summary>
    /// Ground legal to nobody is terrain and not a hole in the map (TER-3a): inside a water outline the
    /// locator answers ground nobody is permitted on, everywhere the town has not deliberately carried a
    /// bridge over it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void WhatIsInsideAWaterOutlineIsGroundPermittedToNobody(string map)
    {
        var plan = Towns.Of(map);
        var ground = GroundOf(map);

        for (var outline = 0; outline < plan.Water.Outline.Count; outline++)
        {
            var points = plan.Water.Outline.RingOf(outline);
            var inside = 0;
            foreach (var pointM in SampleInside(points, samples: 400, marginM: Config.Terrain.GroundStepM))
            {
                // An outline may run off the edge of the map, and what is out there is not the town's.
                if (!ground.Contains(pointM) || UnderABridge(plan, pointM)) continue;

                inside++;
                var at = ground.At(pointM);
                Assert.True(!at.Walkable && !at.Drivable,
                    $"{map}: {pointM} is inside water outline {outline} and reads {at.Ground}");
            }

            Assert.True(inside > 0, $"{map}: water outline {outline} has no wet interior to sample");
        }
    }

    /// <summary>
    /// TER-1: the town is fully covered, with no empty spaces and no holes. Every point of every map
    /// answers a kind the catalogue knows, which is what "no hole" means once the ground is a set of
    /// shapes — grass is the answer where nothing was laid, and there is no other.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheTownIsFullyCoveredByGroundTheCatalogueKnows(string map)
    {
        var plan = Towns.Of(map);
        var ground = GroundOf(map);
        var stepM = Config.Terrain.GroundStepM;

        for (var y = stepM * 0.5f; y < plan.WorldSizeM.Y; y += stepM)
        {
            for (var x = stepM * 0.5f; x < plan.WorldSizeM.X; x += stepM)
            {
                Assert.InRange((int)ground.GroundAt(new Vector2(x, y)), 0, GroundCatalog.Kinds - 1);
            }
        }
    }

    /// <summary>
    /// <b>One question, one answer, however often it is put.</b> The indexes carry scratch a query writes
    /// through, so a point asked twice — and a point asked after its neighbours have been — has to come
    /// back the same, or the physics caller and the permission caller are reading different towns.
    /// </summary>
    [Fact]
    public void TheSamePointAnswersTheSameThingHoweverOftenItIsAsked()
    {
        var plan = Towns.Of(Towns.Fixture);
        var ground = GroundOf(Towns.Fixture);

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            var lengthM = Spline.TotalLengthM(arcs);
            for (var alongM = 0f; alongM <= lengthM; alongM += 5f)
            {
                var on = Spline.SampleAt(arcs, MathF.Min(alongM, lengthM));
                var once = ground.GroundAt(on.PositionM);

                // Everything the query touches is walked over by other queries in between.
                foreach (var strideM in (ReadOnlySpan<float>)[-40f, -2f, 2f, 40f])
                {
                    ground.EffectAt(on.PositionM + (on.Right * strideM));
                }

                Assert.Equal(once, ground.GroundAt(on.PositionM));
                Assert.Equal(once, ground.At(on.PositionM).Ground);
            }
        }
    }

    /// <summary>
    /// PHY-9 makes being pushed always possible, and a tick has nowhere to put an exception: a query
    /// beyond the town's own box is answered rather than refused. <b>And answered with what is actually
    /// out there</b> — nothing is clamped back onto the town, so a body pushed off the map is on grass and
    /// not on whatever the edge of the map happened to be.
    /// </summary>
    [Fact]
    public void AQueryOffTheEdgeOfTheTownIsAnsweredRatherThanThrown()
    {
        var plan = Towns.Of(Towns.Fixture);
        var ground = GroundOf(Towns.Fixture);

        foreach (var pointM in (ReadOnlySpan<Vector2>)
                 [new(-1e6f, -1e6f), new(1e6f, 1e6f), new(-1f, plan.WorldSizeM.Y * 0.5f), new(float.MaxValue, 0f)])
        {
            Assert.False(ground.Contains(pointM));
            Assert.Equal(Ground.Grass, ground.GroundAt(pointM));
        }
    }

    /// <summary>Whether the point stands inside any of a set of closed rings, by the same crossing count.</summary>
    static bool InsideAnyRing(CityPlan.RingArrays rings, Vector2 pointM)
    {
        for (var ring = 0; ring < rings.Count; ring++)
        {
            if (Contains(rings.RingOf(ring), pointM)) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a deck stands over the point: the stretch of its road the bridge spans, half a deck
    /// wide either side. A deck runs the whole road rather than only the wet part, so this is
    /// deliberately generous — it is excluding ground from a claim about water, not making one.
    /// </summary>
    static bool UnderABridge(CityPlan plan, Vector2 pointM)
    {
        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
        {
            var reachM = (plan.Bridges.DeckWidthM[bridge] * 0.5f) + plan.Bridges.PavementWidthM[bridge];
            var alongM = 0f;
            foreach (var segment in plan.Roads.SegmentsOf(plan.Bridges.Road[bridge]))
            {
                for (var distanceM = 0f; distanceM <= segment.LengthM; distanceM += 1f)
                {
                    var atM = alongM + distanceM;
                    if (atM < plan.Bridges.FromM[bridge] || atM > plan.Bridges.ToM[bridge]) continue;
                    if (Vector2.DistanceSquared(segment.PointAtM(distanceM), pointM) <= reachM * reachM) return true;
                }

                alongM += segment.LengthM;
            }
        }

        return false;
    }

    /// <summary>
    /// Points genuinely inside a polygon, found by even-odd crossing over a lattice across its box — and
    /// no nearer its edge than <paramref name="marginM"/>, which keeps the sample off the boundary itself
    /// rather than granting the answer any room.
    /// </summary>
    static List<Vector2> SampleInside(ReadOnlySpan<Vector2> polygon, int samples, float marginM)
    {
        var min = polygon[0];
        var max = polygon[0];
        foreach (var pointM in polygon)
        {
            min = Vector2.Min(min, pointM);
            max = Vector2.Max(max, pointM);
        }

        var side = (int)MathF.Sqrt(samples * 4);
        var found = new List<Vector2>();
        for (var y = 1; y < side && found.Count < samples; y++)
        {
            for (var x = 1; x < side && found.Count < samples; x++)
            {
                var pointM = min + ((max - min) * new Vector2(x / (float)side, y / (float)side));
                if (Contains(polygon, pointM) && DistanceToEdgeM(polygon, pointM) > marginM) found.Add(pointM);
            }
        }

        return found;
    }

    static float DistanceToEdgeM(ReadOnlySpan<Vector2> polygon, Vector2 pointM)
    {
        var nearestM = float.PositiveInfinity;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var edge = polygon[i] - polygon[j];
            var lengthSquared = edge.LengthSquared();
            var along = lengthSquared > 0f ? Math.Clamp(Vector2.Dot(pointM - polygon[j], edge) / lengthSquared, 0f, 1f) : 0f;
            nearestM = MathF.Min(nearestM, Vector2.Distance(pointM, polygon[j] + (edge * along)));
        }

        return nearestM;
    }

    static bool Contains(ReadOnlySpan<Vector2> polygon, Vector2 pointM)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if (polygon[i].Y > pointM.Y == polygon[j].Y > pointM.Y) continue;

            var crossingX = polygon[i].X + ((pointM.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) * (polygon[j].X - polygon[i].X));
            if (pointM.X < crossingX) inside = !inside;
        }

        return inside;
    }
}
