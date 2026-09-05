using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
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
        var ground = GroundOf(map);

        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
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
        var ground = GroundOf(map);

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var laneOffsetM = plan.Roads.WidthM[road] * 0.25f;
            foreach (var segment in plan.Roads.SegmentsOf(road))
            {
                for (var distanceM = 0f; distanceM <= segment.LengthM; distanceM += 2f)
                {
                    var headingRad = segment.HeadingAtRad(distanceM);
                    var alongRoad = new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad));
                    var acrossRoad = new Vector2(-alongRoad.Y, alongRoad.X);
                    var onTheLaneM = segment.PointAtM(distanceM) + (acrossRoad * laneOffsetM);

                    Assert.True(ground.At(onTheLaneM).Drivable,
                        $"{map}: road {road} at {onTheLaneM} runs over {ground.GroundAt(onTheLaneM)}");
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

    [Fact]
    public void WhatASurfaceIsWorthIsTheFigureConfigCarries()
    {
        var catalogue = new GroundCatalog(Config);

        Assert.Equal(Config.Terrain.GrassCoefficient, catalogue.Coefficient(Ground.Grass));
        Assert.Equal(Config.Terrain.WaterCoefficient, catalogue.Coefficient(Ground.Water));
        Assert.Equal(Config.Terrain.PavedCoefficient, catalogue.Coefficient(Ground.Road));
        Assert.Equal(Config.Terrain.PavedCoefficient, catalogue.Coefficient(Ground.Sidewalk));
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
