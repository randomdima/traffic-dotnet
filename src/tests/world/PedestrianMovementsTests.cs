using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The turns a walk is taken through a place on (WLK-13). The lines between two places are
/// <see cref="PedestrianWaysTests"/>' and where the places stand is <see cref="PedestrianNodesTests"/>'.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class PedestrianMovementsTests
{
    /// <summary>
    /// <b>A turn is laid for every lane arriving at a place onto every lane setting off from it, bar the one
    /// that goes back down the way it came and bar the ones no line joins</b> (WLK-13, WLK-14): the product
    /// of the two and not a ring round the place, so a walk reaching a corner off any way may leave it by any
    /// other it can be joined to.
    /// </summary>
    [Fact]
    public void ATurnIsLaidForEveryArrivalOntoEveryDepartureBarTheWayBack()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var turns = FootMovements.Lay(plan, config);
        var ways = turns.Ways;
        var connectors = ways.Connectors;

        var wanted = new HashSet<(int FromWay, int FromLane, int OntoWay, int OntoLane)>();
        for (var from = 0; from < ways.Count; from++)
        {
            for (var fromLane = 0; fromLane < FootConnectors.LanesPerWay; fromLane++)
            {
                var place = connectors.PlaceOf(ways.ArrivesAt(from, fromLane));
                for (var onto = 0; onto < ways.Count; onto++)
                {
                    if (onto == from) continue;

                    for (var ontoLane = 0; ontoLane < FootConnectors.LanesPerWay; ontoLane++)
                    {
                        if (connectors.PlaceOf(ways.SetsOffAt(onto, ontoLane)) != place) continue;

                        wanted.Add((from, fromLane, onto, ontoLane));
                    }
                }
            }
        }

        var laid = new HashSet<(int FromWay, int FromLane, int OntoWay, int OntoLane)>();
        for (var turn = 0; turn < turns.Count; turn++)
        {
            laid.Add((turns.FromWay(turn), turns.FromLane(turn), turns.OntoWay(turn), turns.OntoLane(turn)));
        }

        // The staging and not the claim: a fixture with no place walked through would leave both empty.
        Assert.NotEmpty(wanted);

        // Every pair laid is one the product asks for, and the ones missing are exactly the ones no line
        // could join without a pivot (WLK-14, FootMovements.Refused) — counted there rather than worked out
        // a second time here.
        Assert.Equal(wanted.Count, turns.Count + turns.Refused);
        Assert.Empty(laid.Except(wanted));
    }

    /// <summary>
    /// <b>A turn runs from where its lane arrives to where the next sets off</b> (WLK-13): the ways and the
    /// turns are one network, so a walk over them is a run of lines that meet end to end.
    /// </summary>
    [Fact]
    public void ATurnRunsFromWhereItsLaneArrivesToWhereTheNextSetsOff()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var turns = FootMovements.Lay(plan, config);
        var ways = turns.Ways;
        var connectors = ways.Connectors;

        var walked = 0;
        for (var turn = 0; turn < turns.Count; turn++)
        {
            var line = turns.ArcsOf(turn);
            if (line.Length == 0) continue;

            walked++;
            var fromWay = turns.FromWay(turn);
            var ontoWay = turns.OntoWay(turn);
            var fromM = connectors.PointM(
                ways.ArrivesAt(fromWay, turns.FromLane(turn)), ways.KindOf(fromWay), turns.FromLane(turn));
            var ontoM = connectors.PointM(
                ways.SetsOffAt(ontoWay, turns.OntoLane(turn)), ways.KindOf(ontoWay), turns.OntoLane(turn));

            Assert.Equal(0f, Vector2.Distance(line[0].StartM, fromM), LineTolerance.RoundingM);
            Assert.Equal(0f, Vector2.Distance(line[^1].EndM, ontoM), LineTolerance.RoundingM);
        }

        Assert.True(walked > 0, "the fixture town lays no turn with a line at all");
    }

    /// <summary>
    /// <b>No curve a turn is joined to its ends by is tighter than the circle the feet hold at pace</b>
    /// (WLK-13, <see cref="SimConfig.WalkerTightestTurnM"/>): a line tighter than that is a line nothing can
    /// walk, so a join that would need one is laid as the straight between its two points instead.
    /// </summary>
    /// <remarks>
    /// <b>Read on a turn's first and last piece, which are the two curves this construction lays.</b>
    /// Everything between them is the course the turn runs along, and a course carries the town's own corners
    /// at their own radius (WLK-11, TER-3c.3) — the driven ground's boundary turns inside a tenth of a metre
    /// at a sharp mouth, and a turn onto a crossing runs along that boundary. <b>What the bound is about is
    /// the curve, not the ground under it</b>, and a reading that mixed the two would be a claim about the
    /// map rather than about this rule.
    /// </remarks>
    [Fact]
    public void NoCurveATurnIsJoinedByIsTighterThanTheFeetCanHold()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var turns = FootMovements.Lay(plan, config);

        var bent = 0;
        for (var turn = 0; turn < turns.Count; turn++)
        {
            var line = turns.ArcsOf(turn);
            if (line.Length == 0 || !turns.Held(turn)) continue;

            foreach (var piece in (ReadOnlySpan<ArcSeg>)[line[0], line[^1]])
            {
                if (MathF.Abs(piece.Curvature) <= LineTolerance.RoundingM) continue;

                bent++;
                var radiusM = 1f / MathF.Abs(piece.Curvature);
                Assert.True(
                    radiusM >= config.WalkerTightestTurnM - LineTolerance.RoundingM,
                    $"a turn is joined on {radiusM:F3} m against the {config.WalkerTightestTurnM:F2} m the " +
                    $"feet hold, at {piece.StartM.X:F0},{piece.StartM.Y:F0}");
            }
        }

        Assert.True(bent > 0, "the fixture town lays no turn with a curve in it at all");
    }

    /// <summary>
    /// <b>A turn leaves the lane it arrives on along that lane, and joins the lane it sets off down along
    /// that one</b> (WLK-13): a walk reaching the seam between a way and a turn carries straight on rather
    /// than stopping to pivot, which is what the turn is curved for.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the turns that were laid as curves</b> (<see cref="FootMovements.Held"/>), and of those
    /// only where both lanes have a line to read a heading off: a turn no holdable curve would join is the
    /// straight between its two points and meets them at whatever angle they stand at, which is the pivot
    /// the instruments report rather than a claim broken here. A lane welded to one point (WLK-12) faces
    /// nowhere at all.
    /// </remarks>
    [Fact]
    public void ATurnLeavesAndJoinsItsLanesAlongThem()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var turns = FootMovements.Lay(plan, config);
        var ways = turns.Ways;

        var seams = 0;
        for (var turn = 0; turn < turns.Count; turn++)
        {
            var line = turns.ArcsOf(turn);
            var arriving = ways.LaneOf(turns.FromWay(turn), turns.FromLane(turn));
            var leaving = ways.LaneOf(turns.OntoWay(turn), turns.OntoLane(turn));
            if (line.Length == 0 || arriving.Length == 0 || leaving.Length == 0) continue;
            if (!turns.Held(turn)) continue;

            seams++;
            var onto = Spline.WrapRad(
                line[0].HeadingRad - arriving[^1].HeadingAtRad(arriving[^1].LengthM));
            var off = Spline.WrapRad(
                leaving[0].HeadingRad - line[^1].HeadingAtRad(line[^1].LengthM));

            Assert.Equal(0f, onto, TangentRad);
            Assert.Equal(0f, off, TangentRad);
        }

        Assert.True(seams > 0, "the fixture town lays no turn between two lanes with lines at all");
    }

    /// <summary>
    /// How far off a seam may read and still be one line carrying on
    /// (<see cref="LineTolerance.StraightOnRad"/>).
    /// </summary>
    const float TangentRad = LineTolerance.StraightOnRad;

    /// <summary>
    /// <b>No turn spends more than half a turn of heading over what its own two ends ask for</b> (WLK-13,
    /// <see cref="Spline.SweptRad"/>): a line that turns twice where once would do is not the line a walker
    /// takes, and at the limit it is a ring walked all the way round to arrive where the body already stood.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the whole line and of every turn, held or not.</b> The bound is on what is laid, however it
    /// was laid: a turn made of two corners onto a course with a stretch of course between them can wind while
    /// each of its three parts is inside the bound on its own, and a turn cut to the straight spends exactly
    /// what its ends ask for.
    /// </remarks>
    [Fact]
    public void NoTurnWindsPastHalfATurnMoreThanItsEndsAskFor()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var turns = FootMovements.Lay(plan, config);

        var read = 0;
        for (var turn = 0; turn < turns.Count; turn++)
        {
            var line = turns.ArcsOf(turn);
            if (line.Length == 0) continue;

            read++;
            var overRad = Spline.SweptRad(line) - Spline.AskedRad(line);
            Assert.True(
                overRad <= Spline.HalfATurnRad,
                $"a turn spends {overRad * 180f / MathF.PI:F0}° over what its ends ask for, at " +
                $"{line[0].StartM.X:F0},{line[0].StartM.Y:F0}");
        }

        Assert.True(read > 0, "the fixture town lays no turn with a line at all");
    }

    /// <summary>
    /// <b>No turn covers more of the straight between its own two ends than a half turn covers of its
    /// chord</b> (WLK-13, <see cref="Spline.HalfATurnOfItsChord"/>): a turn is ground a walker crosses, and an
    /// arc of any turn covers that much of its chord and no more — so a line over it bows out past the ground
    /// between its ends, which a course that meanders at a corner does without spending a degree of extra
    /// heading.
    /// </summary>
    [Fact]
    public void NoTurnCoversMoreThanAHalfTurnsWorthOfItsOwnStraight()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var turns = FootMovements.Lay(plan, config);

        var read = 0;
        for (var turn = 0; turn < turns.Count; turn++)
        {
            var line = turns.ArcsOf(turn);
            if (line.Length == 0) continue;

            read++;
            var straightM = Vector2.Distance(line[0].StartM, line[^1].EndM);
            var lengthM = Spline.TotalLengthM(line);
            Assert.True(
                lengthM <= (Spline.HalfATurnOfItsChord * straightM) + LineTolerance.JoinedM,
                $"a turn runs {lengthM:F1} m over a straight of {straightM:F1} m at " +
                $"{line[0].StartM.X:F0},{line[0].StartM.Y:F0}");
        }

        Assert.True(read > 0, "the fixture town lays no turn with a line at all");
    }

    /// <summary>
    /// <b>A walk carries on at every joint between a lane and the turn after it</b> (WLK-14): no turn is laid
    /// that a body would have to pivot onto or off, so every route over the network is one line from end to
    /// end. <b>What cannot be joined is refused</b>, which is why this is asked of every turn and not only of
    /// the ones laid as curves.
    /// </summary>
    /// <remarks>
    /// <b>A joint against a lane with no line is not read</b>: a lane welded to one point (WLK-12) faces
    /// nowhere, so there is no heading there to carry on from.
    /// </remarks>
    [Fact]
    public void AWalkCarriesOnAtEveryJointBetweenALaneAndItsTurn()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var turns = FootMovements.Lay(plan, config);

        var joints = 0;
        for (var turn = 0; turn < turns.Count; turn++)
        {
            var line = turns.ArcsOf(turn);
            if (line.Length == 0) continue;

            var arriving = turns.Ways.LaneOf(turns.FromWay(turn), turns.FromLane(turn));
            var leaving = turns.Ways.LaneOf(turns.OntoWay(turn), turns.OntoLane(turn));
            if (arriving.Length > 0)
            {
                joints++;
                Carries(arriving[^1].HeadingAtRad(arriving[^1].LengthM), line[0].HeadingRad, line[0].StartM);
            }

            if (leaving.Length == 0) continue;

            joints++;
            Carries(line[^1].HeadingAtRad(line[^1].LengthM), leaving[0].HeadingRad, line[^1].EndM);
        }

        Assert.True(joints > 0, "the fixture town lays no turn against a lane with a line at all");

        static void Carries(float oneRad, float otherRad, Vector2 atM)
        {
            var turnedRad = MathF.Abs(Spline.WrapRad(otherRad - oneRad));
            Assert.True(
                turnedRad <= TangentRad,
                $"a walk pivots {turnedRad * 180f / MathF.PI:F1}° at {atM.X:F0},{atM.Y:F0}");
        }
    }

    /// <summary>
    /// <b>A turn's own line never kinks inside itself</b> (WLK-13): every piece of it carries on from the one
    /// before, so the only place a walk taken through a place can pivot is a joint between the turn and a
    /// lane. <b>Which is what refusing a course the turn cannot be curved onto buys</b> — such a route was
    /// laid with the straight standing in for the corner, and that straight met the course at any angle at
    /// all.
    /// </summary>
    [Fact]
    public void ATurnNeverKinksInsideItsOwnLine()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var turns = FootMovements.Lay(plan, config);

        var joints = 0;
        for (var turn = 0; turn < turns.Count; turn++)
        {
            var line = turns.ArcsOf(turn);
            for (var piece = 1; piece < line.Length; piece++)
            {
                joints++;
                var turnedRad = MathF.Abs(
                    Spline.WrapRad(
                        line[piece].HeadingRad - line[piece - 1].HeadingAtRad(line[piece - 1].LengthM)));
                Assert.True(
                    turnedRad <= TangentRad,
                    $"a turn kinks {turnedRad * 180f / MathF.PI:F1}° at " +
                    $"{line[piece].StartM.X:F0},{line[piece].StartM.Y:F0}");
            }
        }

        Assert.True(joints > 0, "the fixture town lays no turn of more than one piece at all");
    }

    /// <summary>
    /// <b>A turn's own line is joined end to end</b> (WLK-13): it is laid in two parts — the step onto the
    /// departing lane's course and the stretch of that course — and the two have to meet, or what is handed
    /// on is a route with a hole in it.
    /// </summary>
    [Fact]
    public void ATurnsLineIsJoinedEndToEnd()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var turns = FootMovements.Lay(plan, config);

        var joints = 0;
        for (var turn = 0; turn < turns.Count; turn++)
        {
            var line = turns.ArcsOf(turn);
            for (var piece = 1; piece < line.Length; piece++)
            {
                joints++;
                var apartM = Vector2.Distance(line[piece - 1].EndM, line[piece].StartM);
                Assert.True(
                    apartM <= LineTolerance.JoinedM,
                    $"a turn stands {apartM:F3} m open at {line[piece].StartM.X:F0},{line[piece].StartM.Y:F0}");
            }
        }

        Assert.True(joints > 0, "the fixture town lays no turn of more than one piece at all");
    }
}
