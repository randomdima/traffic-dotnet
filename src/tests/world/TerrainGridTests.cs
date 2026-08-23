using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// TER-7's claim, asked of every shipped map: <b>the ground drawn and the ground classified are the
/// same ground</b>. The plan's shapes are the town, the cell grid is a classifier over them, and the
/// two agree to within half a cell — so every assertion here is a shape read off the plan and asked
/// of the query, never a cell compared with another cell.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class TerrainGridTests
{
    /// <summary>TER-7's own tolerance: half a cell, which on every shipped map is 0.5 m.</summary>
    const float HalfACell = 0.5f;

    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    static TerrainGrid GridOf(string map) => new(Towns.Of(map), SimConfig.Shipped());

    [Theory]
    [MemberData(nameof(Maps))]
    public void AJunctionIsGroundACarMayBeOn(string map)
    {
        var plan = Towns.Of(map);
        var grid = GridOf(map);

        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            var centreM = plan.Junctions.CentreM[junction];
            Assert.True(NearbyGround(grid, centreM, ground => ground.Drivable),
                $"{map}: junction {junction} at {centreM} stands on {grid.GroundAt(centreM)}");
        }
    }

    /// <summary>
    /// A crossing is a stretch of carriageway a pedestrian may use and <em>not</em> a break in it, so
    /// the lane runs underneath: both permissions and a direction, all three at once.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ACrossingIsCarriagewayAPersonMayUse(string map)
    {
        var plan = Towns.Of(map);
        var grid = GridOf(map);

        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            var centreM = plan.Crosswalks.CentreM[crossing];
            Assert.True(NearbyGround(grid, centreM, ground => ground.Walkable && ground.Drivable && ground.Directional),
                $"{map}: crossing {crossing} at {centreM} stands on {grid.GroundAt(centreM)}");
        }
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void ABayIsGroundBothKindsMayBeOn(string map)
    {
        var plan = Towns.Of(map);
        var grid = GridOf(map);

        for (var space = 0; space < plan.ParkingLots.SpaceCount; space++)
        {
            var poseM = plan.ParkingLots.SpacePositionM[space];
            Assert.True(NearbyGround(grid, poseM, ground => ground.Walkable && ground.Drivable),
                $"{map}: bay {space} at {poseM} stands on {grid.GroundAt(poseM)}");
        }
    }

    /// <summary>OBJ-4: a building exposes at least one point on walkable ground for people to enter by.</summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryWayIntoABuildingIsOnGroundAPersonMayWalk(string map)
    {
        var plan = Towns.Of(map);
        var grid = GridOf(map);

        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var from = plan.Buildings.EntryOffsets[building];
            var to = plan.Buildings.EntryOffsets[building + 1];
            Assert.True(to > from, $"{map}: building {building} has no way in");

            for (var entry = from; entry < to; entry++)
            {
                var pointM = plan.Buildings.EntryPointM[entry];
                Assert.True(NearbyGround(grid, pointM, ground => ground.Walkable),
                    $"{map}: building {building}'s way in at {pointM} stands on {grid.GroundAt(pointM)}");
            }
        }
    }

    /// <summary>
    /// The strongest form of the claim: walk each road's own arcs, and the classifier answers
    /// carriageway under every one of them. Away from the junction discs the arms run into, it
    /// answers a lane direction that lies along the road as well.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheClassifierFindsCarriagewayUnderEveryRoadsOwnCurve(string map)
    {
        var plan = Towns.Of(map);
        var grid = GridOf(map);

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
                    var onTheLaneM = segment.PointAtM(distanceM) + acrossRoad * laneOffsetM;

                    Assert.True(NearbyGround(grid, onTheLaneM, ground => ground.Drivable),
                        $"{map}: road {road} at {onTheLaneM} runs over {grid.GroundAt(onTheLaneM)}");

                    if (InsideAJunction(plan, onTheLaneM)) continue;

                    var lane = grid.At(onTheLaneM);
                    if (!lane.Directional || lane.LaneDirection == Vector2.Zero) continue;

                    var alongTheLane = MathF.Abs(Vector2.Dot(Vector2.Normalize(lane.LaneDirection), alongRoad));
                    Assert.True(alongTheLane > 0.9f,
                        $"{map}: road {road}'s lane at {onTheLaneM} runs {alongTheLane:F2} along the road it is on");
                }
            }
        }
    }

    /// <summary>
    /// Ground legal to nobody is terrain and not a hole in the map (TER-3a): inside a water outline
    /// the classifier answers ground nobody is permitted on, everywhere the town has not deliberately
    /// carried a bridge over it. An unstamped hole would read as the default verge, and this is what
    /// says it does not.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void WhatIsInsideAWaterOutlineIsGroundPermittedToNobody(string map)
    {
        var plan = Towns.Of(map);
        var grid = GridOf(map);

        for (var outline = 0; outline < plan.Water.Count; outline++)
        {
            var points = plan.Water.OutlineOf(outline);
            var inside = 0;
            foreach (var pointM in SampleInside(points, samples: 400, marginM: HalfACell * plan.CellSizeM))
            {
                // An outline may run off the edge of the map, and a query out there is answered with
                // the nearest ground the town has rather than with what is drawn beyond it.
                if (!grid.Contains(pointM) || UnderABridge(plan, pointM)) continue;

                inside++;
                var ground = grid.At(pointM);
                Assert.True(!ground.Walkable && !ground.Drivable,
                    $"{map}: {pointM} is inside water outline {outline} and reads {ground.Ground}");
            }

            Assert.True(inside > 0, $"{map}: water outline {outline} has no wet interior to sample");
        }
    }

    /// <summary>
    /// Continuous position in, no snapping out: anywhere inside one cell reads that cell, and what
    /// comes back is a classification rather than a position, so nothing downstream can be nudged
    /// onto a grid it was never on.
    /// </summary>
    [Fact]
    public void TheQueryTakesAContinuousPositionAndSnapsNothing()
    {
        var plan = Towns.Of(Towns.Fixture);
        var grid = GridOf(Towns.Fixture);

        for (var cell = 0; cell < plan.CellCount; cell += 997)
        {
            var centreM = grid.CellCentreM(cell);
            var sample = grid.At(centreM);

            foreach (var cornerM in (ReadOnlySpan<Vector2>)
                     [new(-0.49f, -0.49f), new(0.49f, -0.49f), new(-0.49f, 0.49f), new(0.49f, 0.49f)])
            {
                Assert.Equal(sample, grid.At(centreM + cornerM * plan.CellSizeM));
            }
        }
    }

    /// <summary>
    /// PHY-9 makes being pushed always possible, and a tick has nowhere to put an exception: a query
    /// beyond the town's own box is answered with the nearest ground it has.
    /// </summary>
    [Fact]
    public void AQueryOffTheEdgeOfTheTownIsAnsweredRatherThanThrown()
    {
        var plan = Towns.Of(Towns.Fixture);
        var grid = GridOf(Towns.Fixture);

        foreach (var pointM in (ReadOnlySpan<Vector2>)
                 [new(-1e6f, -1e6f), new(1e6f, 1e6f), new(-1f, plan.WorldSizeM.Y * 0.5f), new(float.MaxValue, 0f)])
        {
            Assert.False(grid.Contains(pointM));
            Assert.Equal(grid.At(grid.CellCentreM(grid.CellIndexAt(pointM))), grid.At(pointM));
        }
    }

    /// <summary>
    /// TER-1: the town is fully covered, with no empty spaces and no holes. Every cell of every map
    /// carries a kind the catalogue knows, which is what "no hole" means once the ground is a byte.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheTownIsFullyCoveredByGroundTheCatalogueKnows(string map)
    {
        foreach (var ground in Towns.Of(map).Cells)
        {
            Assert.InRange((int)ground, 0, GroundCatalog.Kinds - 1);
        }
    }

    [Fact]
    public void WhatASurfaceIsWorthIsTheFigureConfigCarries()
    {
        var config = SimConfig.Shipped();
        var catalogue = new GroundCatalog(config);

        Assert.Equal(config.Terrain.GrassCoefficient, catalogue.Coefficient(Ground.Grass));
        Assert.Equal(config.Terrain.WaterCoefficient, catalogue.Coefficient(Ground.Water));
        Assert.Equal(config.Terrain.PavedCoefficient, catalogue.Coefficient(Ground.Road));
        Assert.Equal(config.Terrain.PavedCoefficient, catalogue.Coefficient(Ground.Sidewalk));
    }

    /// <summary>
    /// Footway and Sidewalk declare the same permissions plus the pavement's preference: stating them
    /// twice with different values is how the two would drift.
    /// </summary>
    [Fact]
    public void TheTwoPavedWalksDifferOnlyInBeingPreferred()
    {
        Assert.Equal(GroundCatalog.RulesOf(Ground.Footway) | GroundRules.Preferred, GroundCatalog.RulesOf(Ground.Sidewalk));
        Assert.Equal(GroundRules.None, GroundCatalog.RulesOf(Ground.Water));
    }

    /// <summary>The shape is right if the classifier agrees with it here or within half a cell of here (TER-7).</summary>
    static bool NearbyGround(TerrainGrid grid, Vector2 pointM, Func<GroundSample, bool> agrees)
    {
        if (agrees(grid.At(pointM))) return true;

        foreach (var offset in (ReadOnlySpan<Vector2>)[new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f)])
        {
            if (agrees(grid.At(pointM + offset * HalfACell * grid.CellSizeM))) return true;
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
            var reachM = plan.Bridges.DeckWidthM[bridge] * 0.5f + plan.Bridges.PavementWidthM[bridge];
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

    static bool InsideAJunction(CityPlan plan, Vector2 pointM)
    {
        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            var reachM = plan.Junctions.RadiusM[junction];
            if (Vector2.DistanceSquared(plan.Junctions.CentreM[junction], pointM) <= reachM * reachM) return true;
        }

        return false;
    }

    /// <summary>
    /// Points genuinely inside a polygon, found by even-odd crossing over a lattice across its box —
    /// and no nearer its edge than <paramref name="marginM"/>, because the edge is exactly where the
    /// classifier is allowed to disagree with the shape by half a cell.
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
                var pointM = min + (max - min) * new Vector2(x / (float)side, y / (float)side);
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
            nearestM = MathF.Min(nearestM, Vector2.Distance(pointM, polygon[j] + edge * along));
        }

        return nearestM;
    }

    static bool Contains(ReadOnlySpan<Vector2> polygon, Vector2 pointM)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if (polygon[i].Y > pointM.Y == polygon[j].Y > pointM.Y) continue;

            var crossingX = polygon[i].X + (pointM.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) * (polygon[j].X - polygon[i].X);
            if (pointM.X < crossingX) inside = !inside;
        }

        return inside;
    }
}
