using System.Numerics;
using Box2D.NET;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;
using Xunit;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Types;
using static Box2D.NET.B2Worlds;

namespace TrafficSimulation.Tests.Physics;

/// <summary>
/// This engine's cast against the incumbent's, over randomised rays through the same set of shapes.
/// <b>The package is a test-only reference and is in no shipped assembly</b> — it is here because a
/// second implementation of the same geometry is the only cheap way to know that a cast written from
/// scratch met the same things, and a disagreement is a red test rather than a silent divergence.
/// </summary>
/// <remarks>
/// <c>SOL-19</c> asks that <em>which</em> answer a ray gives for an origin inside a shape be stated and
/// tested rather than assumed, because the headway probe starts every ray inside its own caster and its
/// whole design rests on it. The answer, asserted below against the reference: <b>a shape whose interior
/// contains the origin is not reported at all</b>, and what the ray reports is the next thing in front
/// of it. It is the answer this test was written to settle — the wall's own page had the opposite
/// written down, from a reading of a newer upstream than the one this town was tuned against.
/// </remarks>
[Collection(Simulation.SolverCollection.Name)]
[Trait(Tier.Key, Tier.Unit)]
public class CastDifferenceTests
{
    const int Shapes = 40;
    const float FieldM = 60f;

    /// <summary>Half a millimetre. The two implementations do the same arithmetic in a different order, and nothing more.</summary>
    const float ToleranceM = 5e-4f;

    [Fact]
    public void EveryCastMeetsWhatTheReferenceMeets()
    {
        var config = SimConfig.Shipped();
        var draw = new Random(20260819);
        var mine = new PhysicsWorld(config);
        var reference = Reference(out var world);

        for (var shape = 0; shape < Shapes; shape++)
        {
            var centreM = new Vector2(Spread(draw), Spread(draw));
            if (shape % 2 == 0)
            {
                var radiusM = 0.4f + (float)draw.NextDouble() * 2f;
                mine.AddStaticCircle(centreM, radiusM);
                Circle(world, centreM, radiusM);
            }
            else
            {
                var sizeM = new Vector2(1f + (float)draw.NextDouble() * 8f, 1f + (float)draw.NextDouble() * 8f);
                var headingRad = (float)draw.NextDouble() * MathF.Tau - MathF.PI;
                mine.AddStaticBox(centreM, sizeM, headingRad);
                Box(world, centreM, sizeM, headingRad);
            }
        }

        mine.SettleStatics();

        var met = 0;
        for (var cast = 0; cast < 4_000; cast++)
        {
            var fromM = new Vector2(Spread(draw), Spread(draw));
            var toM = fromM + new Vector2(Spread(draw), Spread(draw)) * 0.5f;

            var found = mine.CastRay(fromM, toM, BodyId.None, statics: true, out var hit);
            var wanted = reference(fromM, toM, out var wantedM);

            Assert.True(
                found == wanted,
                $"cast {cast} from {fromM} to {toM}: this engine {(found ? "met" : "missed")}, the reference {(wanted ? "met" : "missed")}");
            if (!found) continue;

            met++;
            Assert.Equal(wantedM, hit.DistanceM, ToleranceM);
        }

        // A run of four thousand rays through forty shapes that met nothing would agree with anything.
        Assert.True(met > 500, $"only {met} of 4 000 casts met anything, so the agreement says little");
    }

    /// <summary>
    /// <c>SOL-19</c> as its own case, because it is the one the probe rests on and because a randomised
    /// run would meet it rarely enough to pass by luck.
    /// </summary>
    [Fact]
    public void ARayStartingInsideAShapePassesOutOfItAndReportsWhatIsBeyond()
    {
        var mine = new PhysicsWorld(SimConfig.Shipped());
        var reference = Reference(out var world);

        // A box at the origin and a circle 30 m along, so a ray started inside either one has something
        // in front of it to report instead.
        mine.AddStaticBox(Vector2.Zero, new Vector2(6f, 4f), 0.3f);
        Box(world, Vector2.Zero, new Vector2(6f, 4f), 0.3f);
        mine.AddStaticCircle(new Vector2(30f, 0f), 3f);
        Circle(world, new Vector2(30f, 0f), 3f);
        mine.SettleStatics();

        Assert.True(mine.CastRay(Vector2.Zero, new Vector2(50f, 0f), BodyId.None, statics: true, out var fromBox));
        Assert.True(reference(Vector2.Zero, new Vector2(50f, 0f), out var referenceFromBox));
        Assert.Equal(referenceFromBox, fromBox.DistanceM, ToleranceM);
        Assert.InRange(fromBox.DistanceM, 26.9f, 27.1f);

        // And from inside the circle, where there is nothing beyond: not a hit at no distance, no hit.
        var insideCircleM = new Vector2(30f, 0f);
        Assert.False(mine.CastRay(insideCircleM, insideCircleM + new Vector2(50f, 0f), BodyId.None, statics: true, out _));
        Assert.False(reference(insideCircleM, insideCircleM + new Vector2(50f, 0f), out _));
    }

    /// <summary>The one body a probe may not find is the one it names, which is what the exclusion is for.</summary>
    [Fact]
    public void TheExcludedBodyIsNeverWhatACastMeets()
    {
        var mine = new PhysicsWorld(SimConfig.Shipped());
        mine.AddCar(Vector2.Zero, 0f);
        var ahead = mine.AddCar(new Vector2(20f, 0f), 0f);
        mine.SettleStatics();

        Assert.True(mine.CastRay(new Vector2(5f, 0f), new Vector2(40f, 0f), BodyId.None, statics: false, out var hit));
        Assert.InRange(hit.DistanceM, 12.9f, 13.1f);

        Assert.False(mine.CastRay(new Vector2(5f, 0f), new Vector2(40f, 0f), ahead, statics: false, out _));
    }

    /// <summary>
    /// Why the differential test above asks the reference what angle it actually used. <b>This is a
    /// difference in the reference and not a defect in either</b>, and it is recorded as a test because
    /// it is the sort of thing that otherwise reads as a bug in a new solver for a day.
    /// </summary>
    [Fact]
    public void TheReferenceRoundsAnAngleAndThisEngineDoesNot()
    {
        var worst = 0f;
        for (var step = 0; step < 1_000; step++)
        {
            var headingRad = step / 1_000f * MathF.Tau - MathF.PI;
            var theirs = B2MathFunction.b2MakeRot(headingRad);
            var mine = Shape.Rotation(headingRad);
            worst = MathF.Max(worst, (new Vector2(theirs.c, theirs.s) - mine).Length());
        }

        // This engine's is the library's cosine and sine, so it is exact to the float; the reference's
        // is a polynomial, and a thousandth of a radian is four millimetres at five metres.
        Assert.InRange(worst, 1e-5f, 1e-2f);
    }

    static float Spread(Random draw) => (float)draw.NextDouble() * FieldM - FieldM * 0.5f;

    /// <summary>The incumbent's nearest hit, as a distance in metres, over a world of its own.</summary>
    delegate bool NearestHit(Vector2 fromM, Vector2 toM, out float distanceM);

    static NearestHit Reference(out B2WorldId world)
    {
        var def = b2DefaultWorldDef();
        def.gravity = new B2Vec2(0f, 0f);
        def.workerCount = 0;
        var made = b2CreateWorld(in def);
        world = made;

        var filter = b2DefaultQueryFilter();
        return (Vector2 fromM, Vector2 toM, out float distanceM) =>
        {
            var travel = new B2Vec2(toM.X - fromM.X, toM.Y - fromM.Y);
            var result = b2World_CastRayClosest(made, new B2Vec2(fromM.X, fromM.Y), travel, in filter);
            distanceM = result.fraction * MathF.Sqrt(travel.X * travel.X + travel.Y * travel.Y);
            return result.hit;
        };
    }

    static void Circle(B2WorldId world, Vector2 centreM, float radiusM)
    {
        var bodyDef = b2DefaultBodyDef();
        bodyDef.type = B2BodyType.b2_staticBody;
        bodyDef.position = new B2Vec2(centreM.X, centreM.Y);
        var body = b2CreateBody(world, in bodyDef);
        var shapeDef = b2DefaultShapeDef();
        b2CreateCircleShape(body, in shapeDef, new B2Circle(new B2Vec2(0f, 0f), radiusM));
    }

    static void Box(B2WorldId world, Vector2 centreM, Vector2 sizeM, float headingRad)
    {
        var bodyDef = b2DefaultBodyDef();
        bodyDef.type = B2BodyType.b2_staticBody;
        bodyDef.position = new B2Vec2(centreM.X, centreM.Y);
        // The rotation is handed over as the pair of numbers rather than as the angle, because 3.1 turns
        // an angle into a rotation with a polynomial rather than with the library's cosine: the two
        // differ by enough to move a hit five metres away by four millimetres, and a differential test
        // that let that in would be comparing two trigonometries rather than two casts. See
        // TheReferenceRoundsAnAngleAndThisEngineDoesNot.
        var rotation = Shape.Rotation(headingRad);
        bodyDef.rotation = new B2Rot(rotation.X, rotation.Y);
        var body = b2CreateBody(world, in bodyDef);
        var shapeDef = b2DefaultShapeDef();
        var box = b2MakeBox(sizeM.X * 0.5f, sizeM.Y * 0.5f);
        b2CreatePolygonShape(body, in shapeDef, in box);
    }
}
