using System.Numerics;
using TrafficSimulation.CityGen.Exam;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What a card of the scenario map came to</b>: the claims every card owes, then the card's own, each read
/// off what the harness recorded (<see cref="ExamDrive"/>) — and the first one broken, said in a sentence.
/// </summary>
/// <remarks>
/// <para>
/// <b>Giving way is judged by the ground and not by the box.</b> A car turning across may drive onto the box
/// and wait there, and a car emerging may creep up to the mouth — so a claim that one car gives way to another
/// is read off the two paths as they were driven: the first place the yielder came within a car's width of
/// anywhere the other went is the ground they share.
/// </para>
/// <para>
/// <b>And what is asked of that ground is that the car with the right of way was never made to wait for
/// it</b> (TER-5e: a right of way orders who waits). Which of the two was over it first is not asked: a
/// yielder that went while the other was still far off took nothing from it, and one the other met already
/// committed is ground nothing takes back.
/// </para>
/// </remarks>
internal static class ExamJudge
{
    /// <summary>What was wrong with one card, or nothing.</summary>
    public static string? Judge(ExamDrive drive, SimConfig config, int card)
    {
        var of = drive.Lattice.Card(card);
        var hz = (float)config.Sim.TickRateHz;
        var held = Math.Max(1, (int)(config.Sim.AgentDecisionIntervalS * hz));

        for (var driver = 0; driver < of.Drivers.Length; driver++)
        {
            var log = drive.Car(card, driver);
            if (log.Wrecked) return $"driver {driver} was wrecked";
            if (log.Touched) return $"driver {driver} touched something at {log.TouchedAt / hz:F1} s";
        }

        for (var walker = 0; walker < of.Walkers.Length; walker++)
        {
            var log = drive.Walker(card, walker);
            if (log.Down) return $"walker {walker} was knocked down";
            if (log.Touched) return $"walker {walker} touched something";
        }

        for (var driver = 0; driver < of.Drivers.Length; driver++)
        {
            var log = drive.Car(card, driver);
            if (!log.Drives.Parked && log.StartedAt < 0) return $"driver {driver} was never sent: its light never turned";
        }

        // <b>The card's own claims before whether everybody got there</b>: a car that went first when it had to
        // give way leaves the other queued behind it in the lane they both leave by, and "never got there"
        // is then the consequence and the order the finding.
        foreach (var claim in of.Claims)
        {
            var wrong = Claim(drive, config, card, claim, hz, held);
            if (wrong is not null) return wrong;
        }

        for (var driver = 0; driver < of.Drivers.Length; driver++)
        {
            var log = drive.Car(card, driver);
            if (log.Drives.Parked) continue;

            if (log.ArrivedAt < 0)
            {
                return log.GaveUpAt >= 0
                    ? $"driver {driver} ({Movement(log)}) gave its leg up at {log.GaveUpAt / hz:F1} s, held by {log.GaveUpHeld}"
                    : $"driver {driver} ({Movement(log)}) never got where it was sent, {Where(log)}";
            }

            // <b>And got there by the movement the card is about.</b> The lattice is a grid, so the place a
            // driver is sent to is also reachable round the block — an arrival on its own says the car got there
            // and not that it ever went through the junction the card was written for.
            if (log.ClearedAt < 0 && !log.Drives.Outbound)
            {
                return $"driver {driver} ({Movement(log)}) got there without coming through the box onto its arm";
            }
        }

        for (var walker = 0; walker < of.Walkers.Length; walker++)
        {
            var log = drive.Walker(card, walker);
            if (log.Walks.Strolls)
            {
                if (log.ArrivedAt < 0) return $"walker {walker} never got round the corner";
                continue;
            }

            if (log.OnThePaintAt < 0) return $"walker {walker} never stepped onto the zebra it was sent over";
            if (log.ArrivedAt < 0) return $"walker {walker} never got over";
        }

        return null;
    }

    static string? Claim(ExamDrive drive, SimConfig config, int card, ExamClaim claim, float hz, int held)
    {
        var subject = drive.Car(card, claim.Subject);
        var name = $"driver {claim.Subject} ({Movement(subject)})";
        switch (claim.Rule)
        {
            case ExamRule.YieldsTo:
                return Yields(drive.Car(card, claim.Other), subject, config, drive.SampleTicks / hz);

            case ExamRule.Before:
                var other = drive.Car(card, claim.Other);
                if (subject.EnteredAt < 0 || other.EnteredAt < 0) return $"{name} or driver {claim.Other} never reached the box";

                return subject.EnteredAt < other.EnteredAt
                    ? null
                    : $"{name} was on the box at {subject.EnteredAt / hz:F1} s, after driver {claim.Other} at {other.EnteredAt / hz:F1} s";

            case ExamRule.Unhindered:
                return subject.HeldTicks > held
                    ? $"{name} was held {subject.HeldTicks / hz:F1} s on its way through with nothing it owed anybody"
                    : null;

            case ExamRule.NeverOnRed:
                return subject.CrossedTheBarOnARed ? $"{name} went past its bar on a red" : null;

            case ExamRule.Waits:
                if (subject.CrossedTheBarOnARed) return $"{name} went past its bar on a red";

                return subject.StoodTicks > held ? null : $"{name} never waited short of the box";

            case ExamRule.ForWalker:
                var walker = drive.Walker(card, claim.Other);
                if (subject.SharedThePaintFor[claim.Other] > 0)
                {
                    return $"{name} was on the zebra for {subject.SharedThePaintFor[claim.Other] / hz:F1} s with walker {claim.Other} in its half of it";
                }

                if (subject.OnThePaintAt[claim.Other] >= 0 && subject.OnThePaintAt[claim.Other] < walker.OnThePaintAt)
                {
                    return $"{name} was over the zebra at {subject.OnThePaintAt[claim.Other] / hz:F1} s, before walker {claim.Other} stepped on at {walker.OnThePaintAt / hz:F1} s";
                }

                return null;

            default:
                return $"no judge for {claim.Rule}";
        }
    }

    /// <summary>
    /// <b>Whether a car gave way to one with the right of way</b>: the one with the right was never held
    /// short of the ground the two paths share.
    /// </summary>
    /// <remarks>
    /// Read off the samples the harness wrote once a decision: the first place the yielder came within a car's
    /// width of anywhere the other went is the ground they share, and the other is said to reach it at the
    /// first sample it was within that width of it.
    /// </remarks>
    static string? Yields(ExamCarLog right, ExamCarLog yielder, SimConfig config, float perSampleS)
    {
        var shareM = config.Car.WidthM;
        var sharedM = shareM * shareM;
        var at = -1;
        for (var i = 0; i < yielder.Samples && at < 0; i++)
        {
            var p = new Vector2(yielder.X[i], yielder.Y[i]);
            for (var j = 0; j < right.Samples; j++)
            {
                if ((new Vector2(right.X[j], right.Y[j]) - p).LengthSquared() > sharedM) continue;

                at = i;
                break;
            }
        }

        var yieldName = $"driver {yielder.Driver} ({Movement(yielder)})";
        var rightName = $"driver {right.Driver} ({Movement(right)})";

        // Two paths that never met are a card staged wrong — unless one of the two never got far enough to
        // meet anything, which is what the card's own arrival says, in its own words.
        if (at < 0 && (yielder.ArrivedAt < 0 || right.ArrivedAt < 0)) return null;
        if (at < 0) return $"{yieldName} and {rightName} never shared any ground";

        var sharedAtM = new Vector2(yielder.X[at], yielder.Y[at]);
        var rightAt = -1;
        for (var j = 0; j < right.Samples; j++)
        {
            if ((new Vector2(right.X[j], right.Y[j]) - sharedAtM).LengthSquared() > sharedM) continue;

            rightAt = j;
            break;
        }

        var heldSamples = 0;
        for (var j = 0; j < rightAt; j++)
        {
            if (right.Held[j]) heldSamples++;
        }

        return heldSamples > 1
            ? $"{rightName} had the right of way and was held {heldSamples * perSampleS:F1} s before the shared ground"
            : null;
    }

    /// <summary>The movement, as the two arms it joins in the card's frame.</summary>
    public static string Movement(ExamCarLog log) => $"{log.Drives.From}-{log.Drives.To}";

    static string Where(ExamCarLog log) =>
        log.EnteredAt < 0 ? "never on the box"
        : log.ClearedAt < 0 ? "on the box and never out of it"
        : "out of the box and short of its place";
}
