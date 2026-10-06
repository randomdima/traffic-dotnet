using System.Numerics;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.TrafficLight.Body;

/// <summary>One head standing in the town: where it is, which way round it is, and what it is showing.</summary>
/// <remarks>
/// <para>
/// A head is <b>the bundle's visual and nothing else</b> — no agent reads one, nor the colour it shows: what
/// holds anybody is the light's hold on the road (<see cref="SignalHolds"/>). What it carries is therefore what
/// a picture of it needs: a place, a bearing, and the thing to ask for its colour — a lane for a car head, a
/// crossing for a walker's.
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
/// centre line — which is the middle of the approaching lane, and therefore on the tarmac.
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
    SignalHeads(SignalHead[] heads, GridLevel level)
    {
        Heads = heads;
        var centresM = new Vector2[heads.Length];
        for (var head = 0; head < heads.Length; head++) centresM[head] = heads[head].CentreM;
        ByRow = RowFile.Of(centresM, level);
    }

    public static SignalHeads Nothing { get; } = new([], new WorldGrid(1f).Main);

    public SignalHead[] Heads { get; }

    /// <summary>The heads filed by the row of the grid they stand in, for a picture of part of the town.</summary>
    public RowFile ByRow { get; }

    public int Count => Heads.Length;

    /// <summary>
    /// <b>The most heads a plan can stand</b>, known before the town is: one car head and two pedestrian heads
    /// at every road end a lit junction stands at, since an end has one lane arriving and one crossing at most.
    /// A bound on what the picture lays room for, and not a count.
    /// </summary>
    public static int MostFor(CityPlan plan)
    {
        var ends = 0;
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Junctions.Lit[plan.Roads.FromJunction[road]]) ends++;
            if (plan.Junctions.Lit[plan.Roads.ToJunction[road]]) ends++;
        }

        return ends * HeadsAtAnEnd;
    }

    /// <summary>A car head over the one lane arriving, and a pedestrian head each way over the one crossing.</summary>
    const int HeadsAtAnEnd = 3;

    /// <param name="bars">The town's own bars (<see cref="StopBars"/>): a car head stands past every one a lit junction's lane arrives at.</param>
    /// <param name="zebras">The town's own crossings, which <paramref name="signals"/> numbers its crossings by.</param>
    public static SignalHeads Place(
        StopBars bars, Crossings zebras, RoadGraph roads, SignalService signals, SimConfig config)
    {
        var heads = new List<SignalHead>();

        for (var bar = 0; bar < bars.Count; bar++)
        {
            var lane = bars.Lane[bar];
            if (signals.AxisOfLane(lane) == SignalService.NoAxis) continue;

            var approach = bars.Approach[bar];
            if (approach.LengthSquared() <= 0f) continue;

            // The lamps run along the driver's right, which with +y down is the heading turned a
            // quarter turn the way curvature counts positive.
            approach = Vector2.Normalize(approach);
            var lamps = new Vector2(-approach.Y, approach.X);
            heads.Add(new SignalHead(
                bars.CentreM[bar] + (approach * config.Signals.HeadSetbackM), MathF.Atan2(lamps.Y, lamps.X),
                ForCars: true, lane));
        }

        for (var crossing = 0; crossing < zebras.Count; crossing++)
        {
            if (!signals.CrossingIsLit(crossing)) continue;

            var axis = zebras.Axis[crossing];
            if (axis.LengthSquared() <= 0f) continue;

            var along = Vector2.Normalize(axis);
            var walked = new Vector2(-along.Y, along.X);

            // Upright: the art is turned to lie along the way the crossing is walked, and the half of
            // the line that keeps red above green — or left of it — is the half that is used.
            var upright = walked.Y > 0f || (walked.Y == 0f && walked.X > 0f) ? walked : -walked;

            // A quarter turn back off the lamp direction, because this art stacks its lamps down the
            // frame where the car head runs them across it.
            var headingRad = MathF.Atan2(upright.Y, upright.X) - (MathF.PI * 0.5f);

            var alongM = (zebras.DepthM[crossing] * 0.5f) + (config.Signals.WalkHeadWidthM * 0.5f) +
                         config.Signals.HeadClearanceM;
            var acrossM = (zebras.SpanM[crossing] * 0.5f) + (config.Signals.WalkHeadLengthM * 0.5f) +
                          config.Signals.HeadClearanceM;

            // Diagonally opposite, because the near-left corner of one direction is the far-right of
            // the other: a crossing walked both ways needs one head each way and never four. It is the
            // walker's <em>left</em>, so the two offsets disagree in sign across the carriageway and
            // agree along it — the other diagonal is the same two corners read as near-right.
            foreach (var corner in (ReadOnlySpan<float>)[-1f, 1f])
            {
                heads.Add(new SignalHead(
                    zebras.CentreM[crossing] + (along * (alongM * corner)) - (walked * (acrossM * corner)),
                    headingRad, ForCars: false, crossing));
            }
        }

        return new SignalHeads([.. heads], config.Grid.Main);
    }
}
