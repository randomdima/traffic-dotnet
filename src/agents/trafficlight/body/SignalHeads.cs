using System.Numerics;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.TrafficLight.Body;

/// <summary>One head standing in the town: where it is, which way round it is, and what it is showing.</summary>
/// <remarks>
/// <para>
/// A head is <b>the bundle's visual and nothing else</b> — no agent reads one, because both agent kinds
/// read <see cref="SignalService"/>. What it carries is therefore what a picture of it needs: a place, a
/// bearing, and the thing to ask for its colour — a lane for a car head, a crossing for a walker's.
/// </para>
/// <para>
/// <b><see cref="HeadingRad"/> is the quad's own rotation and not the head's bearing</b>, because the two
/// arts run their lamps different ways: a car head's frame is drawn with its lamps left to right, and a
/// pedestrian head's stacked top to bottom. Turning them here is what lets the renderer draw both with
/// one instance and no branch.
/// </para>
/// </remarks>
internal readonly record struct SignalHead(Vector2 CentreM, float HeadingRad, bool ForCars, int Subject);

/// <summary>
/// Every signal head of a town, placed once when the plan is opened: one per painted stop bar at a lit
/// junction, and one per direction of every lit crossing.
/// </summary>
/// <remarks>
/// <para>
/// A car head stands a fixed distance past its own stop bar, measured along the road, on the bar's own
/// centre line — which is the middle of the approaching lane, and therefore on the tarmac. The rule
/// "never out on the tarmac" contradicts that; the placement rule is what is reproduced.
/// </para>
/// <para>
/// <b>A car head's lamps run along the driver's right</b>, red first, so a head governing the opposite
/// arm of the same road is the same head turned half round — which is what makes "heads facing opposite
/// arms of the same axis show the same colour" visible rather than merely true.
/// </para>
/// <para>
/// <b>A crossing carries two pedestrian heads, at diagonally opposite corners</b>: one per direction it
/// is walked, each at that direction's own near-left corner, standing clear of the paint and off the
/// carriageway. Their lamps run along the walked direction, and the art is never turned upside down —
/// a head with its green above its red is a head nobody can read.
/// </para>
/// </remarks>
internal sealed class SignalHeads
{
    SignalHeads(SignalHead[] heads) => Heads = heads;

    public static SignalHeads Nothing { get; } = new([]);

    public SignalHead[] Heads { get; }

    public int Count => Heads.Length;

    public static SignalHeads Place(CityPlan plan, RoadGraph roads, SignalService signals, SimConfig config)
    {
        var heads = new List<SignalHead>();
        var bars = plan.StopLines;

        for (var bar = 0; bar < bars.Count; bar++)
        {
            var junction = bars.Junction[bar];
            if (!signals.Lit(junction)) continue;

            var approach = bars.Approach[bar];
            if (approach.LengthSquared() <= 0f) continue;

            approach = Vector2.Normalize(approach);
            var lane = ApproachLane(roads, junction, bars.Road[bar], approach);
            if (lane < 0) continue;

            // The lamps run along the driver's right, which with +y down is the heading turned a
            // quarter turn the way curvature counts positive.
            var lamps = new Vector2(-approach.Y, approach.X);
            heads.Add(new SignalHead(
                bars.CentreM[bar] + (approach * config.Signals.HeadSetbackM), MathF.Atan2(lamps.Y, lamps.X),
                ForCars: true, lane));
        }

        var crossings = plan.Crosswalks;
        for (var crossing = 0; crossing < crossings.Count; crossing++)
        {
            if (!signals.CrossingIsLit(crossing)) continue;

            var axis = crossings.Axis[crossing];
            if (axis.LengthSquared() <= 0f) continue;

            var along = Vector2.Normalize(axis);
            var walked = new Vector2(-along.Y, along.X);

            // Upright: the art is turned to lie along the way the crossing is walked, and the half of
            // the line that keeps red above green — or left of it — is the half that is used.
            var upright = walked.Y > 0f || (walked.Y == 0f && walked.X > 0f) ? walked : -walked;

            // A quarter turn back off the lamp direction, because this art stacks its lamps down the
            // frame where the car head runs them across it.
            var headingRad = MathF.Atan2(upright.Y, upright.X) - (MathF.PI * 0.5f);

            var alongM = (crossings.DepthM[crossing] * 0.5f) + (config.Signals.WalkHeadWidthM * 0.5f) +
                         config.Signals.HeadClearanceM;
            var acrossM = (plan.CrossingSpanM(crossing) * 0.5f) + (config.Signals.WalkHeadLengthM * 0.5f) +
                          config.Signals.HeadClearanceM;

            // Diagonally opposite, because the near-left corner of one direction is the far-right of
            // the other: a crossing walked both ways needs one head each way and never four. It is the
            // walker's <em>left</em>, so the two offsets disagree in sign across the carriageway and
            // agree along it — the other diagonal is the same two corners read as near-right.
            foreach (var corner in (ReadOnlySpan<float>)[-1f, 1f])
            {
                heads.Add(new SignalHead(
                    crossings.CentreM[crossing] + (along * (alongM * corner)) - (walked * (acrossM * corner)),
                    headingRad, ForCars: false, crossing));
            }
        }

        return new SignalHeads([.. heads]);
    }

    /// <summary>
    /// Which lane a painted bar governs: the one arriving at that junction, on that road, going the way
    /// the bar's own approach points. A bar on a road that runs <em>through</em> a junction has an
    /// opposite number on the same road, and the approach is what tells the two apart.
    /// </summary>
    static int ApproachLane(RoadGraph roads, int junction, int road, Vector2 approach)
    {
        var best = -1;
        var bestAgreement = 0f;
        foreach (var lane in roads.LanesIn(junction))
        {
            if (roads.LaneRoad[lane] != road) continue;

            var agreement = Vector2.Dot(roads.EndOf(lane).Direction, approach);
            if (agreement <= bestAgreement) continue;

            (best, bestAgreement) = (lane, agreement);
        }

        return best;
    }
}
