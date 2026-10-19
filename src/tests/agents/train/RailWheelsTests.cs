using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Rail;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Train;

/// <summary>
/// <b>A rail vehicle on its wheels</b> (TRN-8, TRN-9): one body on the solver, its bogies' impulses spent on it each tick
/// as a car's tyres are, on a track of its own — held to its rail, rolling, pulling and braking, and through a switch.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class RailWheelsTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Vector2 WorldSizeM = new(4000f, 4000f);

    static TrainVehicle Coach => TrainCatalog.Shared.ShapeOf(VehicleKind.Coach);

    static TrainVehicle Diesel => TrainCatalog.Shared.ShapeOf(VehicleKind.Diesel);

    static CityPlan.RailArrays Straight => Rails([new(-1000f, 2000f), new(5000f, 2000f)]);

    static CityPlan.RailArrays Rails(Vector2[] points) => new()
    {
        Line = [0], Runs = [TownMap.TrackRuns.Both], PointOffsets = [0, points.Length], PointM = points, Level = new byte[points.Length],
        Ballast = CityPlan.RingArrays.None,
    };

    /// <summary>
    /// How far round its curve either way of its middle the curve rig runs: as near a whole turn as leaves its ends apart,
    /// further than a coach is watched going.
    /// </summary>
    const float HalfArcRad = 0.95f * MathF.PI;

    /// <summary>A circle of a radius, as a track is laid: chords a tolerance off it, <see cref="HalfArcRad"/> of it either side of its top.</summary>
    static CityPlan.RailArrays Curve(float radiusM)
    {
        var stepRad = MathF.Sqrt(8f * Config.Rail.ChordToleranceM / radiusM);
        var points = new List<Vector2>();
        var centreM = new Vector2(2000f, 2000f + radiusM);
        for (var angle = -MathF.PI / 2f - HalfArcRad; angle <= -MathF.PI / 2f + HalfArcRad; angle += stepRad)
        {
            points.Add(centreM + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radiusM));
        }

        return Rails([.. points]);
    }

    /// <summary>One vehicle on one track: the body, its build, its cursors, and the switches as set.</summary>
    sealed class Rig
    {
        public readonly PhysicsWorld World = new(Config);
        public readonly RailGraph Graph;
        public readonly RailBuild Build;
        public readonly BodyId Body;
        public readonly RailCursor[] Cursors = [RailCursor.Nowhere, RailCursor.Nowhere, RailCursor.Nowhere, RailCursor.Nowhere];
        public readonly byte[] Switches;
        public DriveCommand Command = DriveCommand.Parked with { Handbrake = false };

        public Rig(CityPlan.RailArrays rails, in TrainVehicle vehicle, Vector2 atM, float headingRad, float speedMps)
        {
            Graph = RailGraph.Of(rails, WorldSizeM);
            Switches = new byte[Math.Max(1, Graph.SwitchCount)];
            Build = RailBuild.Of(vehicle);
            Body = World.AddCar(atM, headingRad, vehicle.FootprintM * 0.5f, vehicle.CornerRadiusM, vehicle.MassKg, onRails: true);
            World.ApplyCentralImpulse(Body, new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad)) * speedMps * vehicle.MassKg);
        }

        public Vector2 PositionM => World.PositionOf(Body);

        public Vector2 Forward => World.RotationOf(Body);

        /// <param name="pushNs">A shove each tick, as a contact in the step before would have left it, which the wheels answer.</param>
        public void Advance(int ticks, Vector2 pushNs = default)
        {
            Span<WheelImpulse> spent = stackalloc WheelImpulse[TrainVehicle.MostBogies];
            for (var tick = 0; tick < ticks; tick++)
            {
                World.ApplyCentralImpulse(Body, pushNs);
                RailWheels.Step(
                    Config, Config.TickSeconds, Graph, Switches, Build, World.PositionOf(Body), World.RotationOf(Body), World.VelocityOf(Body),
                    World.YawRateOf(Body), World.InverseMassOf(Body), World.InverseInertiaOf(Body), Command, Cursors, spent);
                for (var bogie = 0; bogie < Build.Bogies; bogie++) World.ApplyImpulseAt(Body, spent[bogie].ImpulseNs, spent[bogie].AtM);
                World.Step(Config.TickSeconds);
            }
        }

        /// <summary>How far each bogie's pivot stands off the track's line, the worst of them.</summary>
        public float WorstOffM()
        {
            var worstM = 0f;
            for (var bogie = 0; bogie < Build.Bogies; bogie++)
            {
                var pivotM = PositionM + (Forward * Build.BogieM(bogie));
                var nearest = Graph.Nearest(pivotM);
                worstM = MathF.Max(worstM, Vector2.Distance(Graph.Segments[nearest.Segment].PlaceAt(nearest.AlongM), pivotM));
            }

            return worstM;
        }
    }

    /// <summary>A coach put down beside its line is brought onto it by its wheels, and not set rocking about it on the way.</summary>
    [Fact]
    public void AVehicleOffItsLineIsBroughtOntoItWithoutRocking()
    {
        var rig = new Rig(Straight, Coach, new Vector2(1000f, 2000.3f), 0f, 10f);

        var worstYawRad = 0f;
        for (var tick = 0; tick < 120; tick++)
        {
            rig.Advance(1);
            worstYawRad = MathF.Max(worstYawRad, MathF.Abs(rig.World.HeadingOf(rig.Body)));
        }

        Assert.True(rig.WorstOffM() < 0.005f, $"a bogie stood {rig.WorstOffM():F3} m off its line");
        Assert.True(worstYawRad < 0.01f, $"the coach was turned {worstYawRad:F4} rad bringing it back square");
    }

    /// <summary>Rolling free on a straight, a coach loses its rolling resistance and nothing else (SOL-38: no damping).</summary>
    [Fact]
    public void ARollingVehicleLosesItsRollingResistanceAlone()
    {
        var rig = new Rig(Straight, Coach, new Vector2(500f, 2000f), 0f, 22f);
        rig.Advance(600);

        var lostMps = Config.Train.RollingResistance * Config.Tyre.StandardGravityMps2 * 10f;
        Assert.Equal(22f - lostMps, Vector2.Dot(rig.World.VelocityOf(rig.Body), Vector2.UnitX), 2);
    }

    /// <summary>A locomotive asked for more than it has pulls with what its engine and steel on steel give, and no more.</summary>
    [Fact]
    public void ALocomotivePullsWithNoMoreThanItsEngineAndItsGrip()
    {
        var rig = new Rig(Straight, Diesel, new Vector2(500f, 2000f), 0f, 0f);
        rig.Command = rig.Command with { ThrottleMps2 = 100f };
        rig.Advance(60);

        var pullN = MathF.Min(Diesel.TractiveEffortN, Config.Train.WheelRailFriction * Diesel.MassKg * Config.Tyre.StandardGravityMps2);
        var rollingN = Config.Train.RollingResistance * Diesel.MassKg * Config.Tyre.StandardGravityMps2;
        var expectedMps = (pullN - rollingN) / Diesel.MassKg;
        Assert.Equal(expectedMps, rig.World.VelocityOf(rig.Body).X, 2);
    }

    /// <summary>A braked wagon is not shoved along its rail by a push its wheels' friction holds.</summary>
    [Fact]
    public void ABrakedVehicleHoldsAgainstAPushItsFrictionHolds()
    {
        var rig = new Rig(Straight, Coach, new Vector2(500f, 2000f), 0f, 0f);
        rig.Command = rig.Command with { Handbrake = true };
        var holdsN = Config.Train.WheelRailFriction * Coach.MassKg * Config.Tyre.StandardGravityMps2;
        rig.Advance(60, new Vector2(holdsN * 0.8f * Config.TickSeconds, 0f));

        Assert.True(MathF.Abs(rig.PositionM.X - 500f) < 0.01f, $"the braked coach was shoved {rig.PositionM.X - 500f:F3} m");
    }

    /// <summary>
    /// On a curve as tight as a track is laid, every bogie stays on its line and the body lies along the chord between
    /// them (TRN-8) — nothing but its wheels laying it there.
    /// </summary>
    [Fact]
    public void OnTheTightestCurveTheBogiesStayOnTheLineAndTheBodyAlongTheirChord()
    {
        var radiusM = Config.Rail.LeastRadiusM;
        var rails = Curve(radiusM);
        var startM = rails.PointM[2];
        var run = rails.PointM[3] - rails.PointM[2];
        var rig = new Rig(rails, Coach, startM + (Vector2.Normalize(run) * 20f), MathF.Atan2(run.Y, run.X), 22f);

        // Put down on the chord's line, a coach this long stands off the curve at its ends until its wheels lay it on.
        rig.Advance(60);
        var worstM = 0f;
        var worstSkewRad = 0f;
        for (var tick = 0; tick < 600; tick++)
        {
            rig.Advance(1);
            worstM = MathF.Max(worstM, rig.WorstOffM());
            var front = rig.PositionM + (rig.Forward * rig.Build.BogieM(0));
            var rear = rig.PositionM + (rig.Forward * rig.Build.BogieM(1));
            var chord = Vector2.Normalize(front - rear);
            worstSkewRad = MathF.Max(worstSkewRad, MathF.Abs(MathF.Asin(Math.Clamp((chord.X * rig.Forward.Y) - (chord.Y * rig.Forward.X), -1f, 1f))));
        }

        Assert.True(worstM < 0.02f, $"a bogie stood {worstM:F3} m off its line on the curve");
        Assert.True(worstSkewRad < 0.002f, $"the body lay {worstSkewRad:F4} rad off its bogies' chord");
        Assert.True(rig.World.VelocityOf(rig.Body).Length() > 21.5f, "the curve took the coach's speed");
    }

    /// <summary>
    /// <b>A rail vehicle runs through a car standing on its track</b> (TRN-3): the car shoved aside by its weight,
    /// the vehicle slowed by no more than the momentum it gave the car, and still on its line.
    /// </summary>
    [Fact]
    public void AVehicleRunsThroughACarOnItsTrackLosingOnlyWhatItGaveTheCar()
    {
        var rig = new Rig(Straight, Coach, new Vector2(1000f, 2000f), 0f, 22f);
        var car = rig.World.AddNominalCar(new Vector2(1060f, 2000.5f), MathF.PI / 2f);

        rig.Advance(180);

        var speedMps = rig.World.VelocityOf(rig.Body).X;
        var gaveMps = 22f * Config.Car.MassKg / (Config.Car.MassKg + Coach.MassKg);
        var rollingMps = Config.Train.RollingResistance * Config.Tyre.StandardGravityMps2 * 3f;
        Assert.True(rig.World.PositionOf(car).X > 1060f + 1f, "the car was not shoved along by the coach");
        Assert.InRange(speedMps, 22f - (2f * gaveMps) - rollingMps, 22f - rollingMps + 0.01f);
        Assert.True(rig.WorstOffM() < 0.02f, $"a bogie stood {rig.WorstOffM():F3} m off its line after the car");
    }

    /// <summary>
    /// <b>Facing a switch a wheel takes the branch it is set to</b>, and trailing back through it the common end however
    /// it is set (TRN-9): a coach run east across a fork set each way ends on that way.
    /// </summary>
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    public void AVehicleFacingASwitchTakesTheWayItIsSet(byte position)
    {
        var rails = new CityPlan.RailArrays
        {
            Line = [0, 0], Runs = [TownMap.TrackRuns.Both, TownMap.TrackRuns.Both], PointOffsets = [0, 2, 5],
            PointM = [new(-1000f, 2000f), new(5000f, 2000f), new(1500f, 2000f), new(1700f, 2004f), new(5000f, 2700f)],
            Level = new byte[5], Ballast = CityPlan.RingArrays.None,
        };
        var rig = new Rig(rails, Coach, new Vector2(1300f, 2000f), 0f, 15f);
        rig.Switches[0] = position;

        rig.Advance(60 * 40);

        var node = rig.Graph.NodeOfSwitch[0];
        var branch = rig.Graph.Ends[rig.Graph.EndOffsets[node] + 1 + position] / 2;
        Assert.All(rig.Cursors.AsSpan(0, rig.Build.Bogies).ToArray(), cursor => Assert.Equal(branch, cursor.Segment));
        Assert.True(rig.WorstOffM() < 0.02f, $"a bogie stood {rig.WorstOffM():F3} m off its line past the switch");

        // And back the other way, the switch thrown under it: trailing, it runs onto the common end whichever way it is set.
        rig.Switches[0] = (byte)(1 - position);
        rig.World.ApplyCentralImpulse(rig.Body, -rig.World.VelocityOf(rig.Body) * 2f * Coach.MassKg);
        rig.Advance(60 * 40);
        var common = rig.Graph.Ends[rig.Graph.EndOffsets[node]] / 2;
        Assert.All(rig.Cursors.AsSpan(0, rig.Build.Bogies).ToArray(), cursor => Assert.Equal(common, cursor.Segment));
    }
}
