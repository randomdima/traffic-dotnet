using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// The proving ground, held to the figures it was laid against. <b>Its shapes are chosen for the car</b> —
/// a straight long enough to reach the gear's own cap and stop again, corners tighter than any speed that
/// straight builds — so a change to the car or to the road width is a track that has to be laid again, and
/// this is what says so rather than a stale file quietly being measured.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class TrackPlanTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>The spawn kinds the file format carries, which the plan writes and the town reads back.</summary>
    const byte SpawnKindPerson = 0;

    const byte SpawnKindCar = 1;

    public static TheoryData<int> Shapes
    {
        get
        {
            var shapes = new TheoryData<int>();
            foreach (var section in TrackPlan.Sections)
            {
                if (section.IsShape) shapes.Add(section.Road);
            }

            return shapes;
        }
    }

    /// <summary>
    /// <b>The tracks on disk are the tracks this build lays.</b> Byte for byte, because the file is what
    /// every reader gets: the game opening it, the sweeps asking questions of it, and the probe measuring on
    /// it. <b>Both crowds</b>, since either file going stale is a probe quoting a road the build no longer
    /// has.
    /// </summary>
    /// <remarks>
    /// A red here is not a bug in the track — it is <c>--lay-track</c> not having been run since a figure
    /// the track is laid against moved.
    /// </remarks>
    [Theory]
    [InlineData((int)TrackCrowd.Pacing)]
    [InlineData((int)TrackCrowd.Drunk)]
    public void TheTrackOnDiskIsTheTrackThisBuildLays(int crowd)
    {
        var onDisk = File.ReadAllBytes(ProjectPaths.TownFile(TrackPlan.NameOf((TrackCrowd)crowd)));

        Assert.Equal(onDisk, TownWriter.Write(TrackPlan.Lay(Config, (TrackCrowd)crowd)));
    }

    /// <summary>
    /// <b>The two grounds differ in their people and in nothing else</b>, which is the whole reason their
    /// tables are read against each other: the same lap, the same cars in the same poses, and fifteen bodies
    /// either side of the kerb line.
    /// </summary>
    [Fact]
    public void TheTwoProvingGroundsAreTheSameLapWithDifferentPeopleOnIt()
    {
        var pacing = TrackPlan.Lay(Config);
        var drunk = TrackPlan.Lay(Config, TrackCrowd.Drunk);

        Assert.Equal(pacing.WorldSizeM, drunk.WorldSizeM);
        Assert.Equal(pacing.Roads.Segments, drunk.Roads.Segments);
        Assert.Equal(pacing.Spawns.Count, drunk.Spawns.Count);

        for (var spawn = 0; spawn < pacing.Spawns.Count; spawn++)
        {
            Assert.Equal(pacing.Spawns.Kind[spawn], drunk.Spawns.Kind[spawn]);
            if (pacing.Spawns.Kind[spawn] != SpawnKindCar) continue;

            Assert.Equal(pacing.Spawns.PositionM[spawn], drunk.Spawns.PositionM[spawn]);
            Assert.Equal(pacing.Spawns.HeadingRad[spawn], drunk.Spawns.HeadingRad[spawn]);
        }
    }

    /// <summary>
    /// <b>Every drunk is put down in a carriageway and every pacer beside one</b> — which is the only thing
    /// that tells the two rules apart at run time, so a body on the wrong side of that line is a body
    /// following the wrong one.
    /// </summary>
    [Theory]
    [InlineData((int)TrackCrowd.Pacing, false)]
    [InlineData((int)TrackCrowd.Drunk, true)]
    public void ThePeopleAreStoodWhereTheirOwnRuleWillFindThem(int crowd, bool inTheRoad)
    {
        var plan = TrackPlan.Lay(Config, (TrackCrowd)crowd);
        var roads = RoadGraph.Build(plan, Config);

        var people = 0;
        for (var spawn = 0; spawn < plan.Spawns.Count; spawn++)
        {
            if (plan.Spawns.Kind[spawn] != SpawnKindPerson) continue;

            people++;
            Assert.Equal(inTheRoad, Reel.InTheCarriageway(roads, plan.Spawns.PositionM[spawn]));
        }

        Assert.Equal(TrackPlan.Pacers, people);
    }

    /// <summary>
    /// <b>The lap closes</b>, in position and in heading: the last piece arrives where the first one left,
    /// facing the way it faced. A car drives it for as long as it is watched, and a lap that did not quite
    /// come round would be a car sent off the end of the road mid-measurement.
    /// </summary>
    [Fact]
    public void TheLapComesBackToWhereItStarted()
    {
        var chain = Whole();

        var last = chain[^1];
        Assert.Equal(0f, (last.EndM - chain[0].StartM).Length(), 0.01f);
        Assert.Equal(0f, Spline.WrapRad(last.HeadingAtRad(last.LengthM) - chain[0].HeadingRad), 0.01f);

        // And it is one line: each piece leaves where the one before it arrived, heading the same way.
        for (var arc = 1; arc < chain.Count; arc++)
        {
            Assert.Equal(0f, (chain[arc].StartM - chain[arc - 1].EndM).Length(), 0.01f);
            Assert.Equal(
                0f, Spline.WrapRad(chain[arc].HeadingRad - chain[arc - 1].HeadingAtRad(chain[arc - 1].LengthM)),
                0.01f);
        }
    }

    /// <summary>
    /// <b>Every bend on the lap is one of the five shapes</b>, and a link never bends at all. There is no
    /// neutral corner anywhere — the arc's sweep is what pays for the half turn and the quarter turn back —
    /// which is what makes a figure taken between two shapes a figure about nothing but the road's length.
    /// </summary>
    [Fact]
    public void NothingBendsOnThisLapExceptTheShapes()
    {
        var lap = TrackPlan.Lap();
        foreach (var section in TrackPlan.Sections)
        {
            var bends = 0;
            foreach (var arc in lap[section.Road])
            {
                if (arc.Curvature == 0f) continue;

                bends++;
                Assert.Equal(section.RadiusM, 1f / MathF.Abs(arc.Curvature), 3);
            }

            // A section bends exactly when it names a radius: the links and the straight do not, and every
            // shape that does bends at its own radius and at no other.
            Assert.True(
                section.RadiusM > 0f == bends > 0,
                $"{section.Name} carries {bends} bends at a stated radius of {section.RadiusM:F0} m");
        }
    }

    /// <summary>
    /// <b>The lap never comes near itself except where it is continuous.</b> Two stretches of road sharing
    /// ground would be one car's measurement taken in another's traffic, and the one place the lap folds
    /// back on itself — the half turn — holds its two ends a whole turn's diameter apart.
    /// </summary>
    [Fact]
    public void NoTwoStretchesOfTheLapComeNearOneAnother()
    {
        var apartM = Config.RoadWidthM * 2f;
        var walked = Walked();

        // A pair of points is only a finding if the road between them is long enough that the lap has left
        // the neighbourhood: everything nearer than that along the line is the line itself.
        var alongM = TrackPlan.Turn180RadiusM * MathF.PI;
        for (var here = 0; here < walked.Count; here++)
        {
            for (var there = here + 1; there < walked.Count; there++)
            {
                var stepM = MathF.Min(there - here, walked.Count - (there - here)) * Config.Car.LengthM;
                if (stepM <= alongM) continue;

                Assert.True(
                    Vector2.Distance(walked[here], walked[there]) > apartM,
                    $"the lap passes within {Vector2.Distance(walked[here], walked[there]):F1} m of itself "
                    + $"{stepM:F0} m further along");
            }
        }
    }

    /// <summary>
    /// <b>Every one of them stands on paving, clear of the carriageway and beside the lane the lap is
    /// driven on, and every shape has one of its own.</b> That is the whole of what stops a car here —
    /// there is no light and no paint — so a pacer that could not reach the road, or that stood in it to
    /// begin with, is a shape with nothing to measure and a lap with a body loose on it.
    /// </summary>
    [Fact]
    public void EveryShapeEndsAtSomebodyStandingBesideTheRoad()
    {
        var plan = TrackPlan.Lay(Config);
        var terrain = new TerrainGrid(plan, Config);
        var roads = RoadGraph.Build(plan, Config);

        Assert.All(plan.Junctions.Lit, Assert.False);
        Assert.Equal(0, plan.StopLines.Count);

        var pacers = 0;
        var onShape = new HashSet<int>();
        for (var spawn = 0; spawn < plan.Spawns.Count; spawn++)
        {
            if (plan.Spawns.Kind[spawn] != SpawnKindPerson) continue;

            pacers++;
            var standM = plan.Spawns.PositionM[spawn];
            Assert.True(terrain.At(standM).Walkable, $"pacer {spawn} stands on ground nobody may walk on");
            Assert.False(terrain.At(standM).Drivable, $"pacer {spawn} stands in the carriageway");

            // And there is a lane beside it to step into, running the way the lap is driven.
            var lane = roads.NearestLane(standM, out var alongM);
            var on = Spline.SampleAt(roads.ArcsOf(lane), alongM);
            Assert.InRange((on.PositionM - standM).Length(), Config.PersonDiameterM, Config.RoadWidthM);
            Assert.True(
                Vector2.Dot(Heading.RightOf(on.Direction) * Config.RoadSideSign, standM - on.PositionM) > 0f,
                $"pacer {spawn} stands across the road from the lane it would step into");

            onShape.Add(roads.LaneRoad[lane]);
        }

        Assert.Equal(TrackPlan.Pacers, pacers);

        // And every shape has one of its own: a shape nobody stops a car on is a shape with no stop in its
        // figures, and the braking distance is half of what this map is for.
        foreach (var section in TrackPlan.Sections)
        {
            Assert.True(!section.IsShape || onShape.Contains(section.Road), $"nobody paces the {section.Name}");
        }
    }

    /// <summary>
    /// <b>The cars on the lap differ in one figure and nothing else.</b> They are the nominal car's
    /// footprint, wheelbase and mass throughout; what the fleet gives each of them is which wheels the
    /// engine reaches, and the lap carries the same number of each — so the rear, front and all-wheel rows
    /// of the probe are a comparison and not three anecdotes.
    /// </summary>
    [Fact]
    public void TheLapCarriesTheSameNumberOfEachDrivetrain()
    {
        var plan = TrackPlan.Lay(Config);
        var fleet = CarCatalog.Shared;

        var counted = new int[3];
        var cars = 0;
        for (var spawn = 0; spawn < plan.Spawns.Count; spawn++)
        {
            if (plan.Spawns.Kind[spawn] != SpawnKindCar) continue;

            counted[fleet.Variants[cars++ % fleet.Count].Drivetrain]++;
        }

        Assert.Equal(TrackPlan.Cars, cars);
        Assert.Equal(TrackPlan.Cars + TrackPlan.Pacers, plan.Spawns.Count);
        Assert.All(counted, of => Assert.Equal(TrackPlan.Cars / counted.Length, of));
    }

    /// <summary>
    /// <b>No car is parked in a node.</b> A disc is ground no lane is laid over, so a car standing in one
    /// is a car off its line before it has moved — and a car that never gets a line is a car that never
    /// drives the lap it was put on.
    /// </summary>
    [Fact]
    public void EveryCarStandsClearOfEveryNode()
    {
        var plan = TrackPlan.Lay(Config);
        var clearM = plan.Junctions.RadiusM[0] + Config.Car.LengthM;

        for (var spawn = 0; spawn < plan.Spawns.Count; spawn++)
        {
            if (plan.Spawns.Kind[spawn] != SpawnKindCar) continue;

            for (var node = 0; node < plan.Junctions.Count; node++)
            {
                Assert.True(
                    Vector2.Distance(plan.Spawns.PositionM[spawn], plan.Junctions.CentreM[node]) > clearM,
                    $"car {spawn} is parked {Vector2.Distance(plan.Spawns.PositionM[spawn], plan.Junctions.CentreM[node]):F1} m "
                    + $"from node {node}");
            }
        }
    }

    /// <summary>The whole lap as one chain, in the order it is driven.</summary>
    static List<ArcSeg> Whole()
    {
        var chain = new List<ArcSeg>();
        foreach (var road in TrackPlan.Lap()) chain.AddRange(road);

        return chain;
    }

    /// <summary>The lap sampled a car's length at a time, which is fine enough to catch two roads sharing ground.</summary>
    static List<Vector2> Walked()
    {
        var chain = CollectionsMarshal.AsSpan(Whole());
        var lengthM = Spline.TotalLengthM(chain);

        var walked = new List<Vector2>();
        for (var atM = 0f; atM < lengthM; atM += Config.Car.LengthM) walked.Add(Spline.SampleAt(chain, atM).PositionM);

        return walked;
    }
}
