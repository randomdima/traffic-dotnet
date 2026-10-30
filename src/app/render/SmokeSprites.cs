using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>What a running car's exhaust puts out</b> (CAR-54): a run of soft grey puffs off each tailpipe, a fifth car part
/// laid over every car, so a car's own rear and one driving through its trail are hazed by it — <b>drawn on the device alone</b>, off the track of
/// its car's way the device's step keeps (<c>smoke_track.comp</c>), by the emit (<c>sprites.glsl</c>'s <c>spritesPuff</c>).
/// A world stepped in managed code, and so a picture's, draws none; nor does a page, whose world may be the device's but
/// whose moving is filled on the host. This is what both are laid from.
/// </summary>
/// <remarks>
/// <para>
/// <b>A puff is where the car was when it left, and the run it has made since.</b> A puff leaves each pipe every
/// <see cref="SmokeFigures.PuffEveryS"/>, and halved again up to <see cref="SmokeFigures.PuffSplits"/> times over where the
/// car's speed would leave them further apart than <see cref="SmokeFigures.PuffGapM"/> (a stack's wider puffs
/// <see cref="SmokeFigures.StackPuffGapM"/>); the one that left <c>a</c> seconds ago left the pipe where the car's track has
/// it then — in a straight line between the two points of its way either side, or the newest and the car now — with the
/// car's velocity then and the pipe's outflow on it, and has been giving that up to the air since
/// (<see cref="SmokeFigures.DragS"/>), so it runs on after its car a little, is left behind on the road the car drove, and
/// goes with the wind (<c>Wind</c>). How thick, how wide, how long-lived and how hard blown out a puff is is the throttle
/// the car was asked for when it left (<see cref="ExhaustLook"/>), and it is nothing off a car that was not running then:
/// one that stops or is wrecked leaves what it put out hanging until it is gone.
/// </para>
/// <para>
/// <b>A puff's place in its pipe's run is the clock's</b>, so it ages from frame to frame as one puff rather than
/// flickering, and what varies between puffs — how far the eddies blow one across, how it is turned — is hashed off
/// which puff it is and never drawn at random. Each pipe of each car is staggered by a hash of its own, so a queue at a
/// light does not breathe in step.
/// </para>
/// </remarks>
internal static class SmokeSprites
{
    /// <summary>The side of the puff's picture in texels. Small: it is a soft blot read through a linear sampler.</summary>
    const int PuffPx = 32;

    /// <summary>
    /// The smoke's figures as the device reads them, laid once with the rates divided out.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A puff outlasts the track it is laid along, is thickest at either end of its life, or a puff's time is split into
    /// a number of puffs no power of two.
    /// </exception>
    public static SmokeLook Look(SimConfig config)
    {
        var smoke = config.Smoke;
        if (!BitOperations.IsPow2(smoke.PuffSplits))
        {
            throw new ArgumentException(
                $"A puff is split into {smoke.PuffSplits}: a puff drawn between two is one of a halving, so a split is a power of two.");
        }

        var finestS = smoke.PuffEveryS / smoke.PuffSplits;
        var tailpipe = Kind(
            "car's", finestS, smoke.IdleOpacity, smoke.FullOpacity, smoke.IdleBirthWidthM, smoke.FullBirthWidthM,
            smoke.IdleLifeS, smoke.FullLifeS, smoke.IdleOutflowMps, smoke.FullOutflowMps, smoke.SpreadMps, smoke.ThickestAt,
            smoke.PuffGapM, smoke.Shade, smoke.ReachM, smoke.TrackPoints, smoke.TrackEveryS, smoke.FullThrottleMps2);

        // A stack blows straight up, which from above is no way at all.
        var stack = Kind(
            "rail vehicle's", finestS, smoke.StackIdleOpacity, smoke.StackFullOpacity, smoke.StackIdleBirthWidthM,
            smoke.StackFullBirthWidthM, smoke.StackIdleLifeS, smoke.StackFullLifeS, 0f, 0f, smoke.StackSpreadMps,
            smoke.StackThickestAt, smoke.StackPuffGapM, smoke.StackShade, smoke.StackReachM, smoke.StackTrackPoints,
            smoke.StackTrackEveryS, smoke.FullRailThrottleMps2);
        return new SmokeLook(finestS, 1f / finestS, smoke.PuffSplits, smoke.ScatterMps, smoke.DragS, smoke.ResetMps, tailpipe, stack);
    }

    static ExhaustLook Kind(
        string whose, float finestS, float idleOpacity, float fullOpacity, float idleBirthWidthM, float fullBirthWidthM,
        float idleLifeS, float fullLifeS, float idleOutflowMps, float fullOutflowMps, float spreadMps, float thickestAt,
        float puffGapM, float shade, float reachM, int trackPoints, float trackEveryS, float fullThrottleMps2)
    {
        var longestS = MathF.Max(idleLifeS, fullLifeS);
        var trackS = (trackPoints - 1) * trackEveryS;
        if (longestS > trackS)
        {
            throw new ArgumentException(
                $"A {whose} puff lasts {longestS} s and its track reaches back {trackS} s: past it, it has nowhere to have left from.");
        }

        if (thickestAt is <= 0f or >= 1f)
        {
            throw new ArgumentException($"A {whose} puff is thickest {thickestAt} of the way through its life: it is clear as it leaves and gone at the end, so between.");
        }

        return new ExhaustLook(
            idleOpacity, fullOpacity, idleBirthWidthM, fullBirthWidthM, idleLifeS, fullLifeS, idleOutflowMps, fullOutflowMps,
            spreadMps, thickestAt, puffGapM, shade, reachM, MathF.Max(idleBirthWidthM, fullBirthWidthM) + (spreadMps * longestS), longestS,
            (int)MathF.Floor(longestS / finestS) + 1, trackPoints, 1f / trackEveryS, 1f / fullThrottleMps2);
    }

    /// <summary>
    /// The puff's picture: white, its opacity a soft blot a little lumpier than a disc — a few overlapping bumps, fixed,
    /// so the turn each puff is drawn at is what tells one from the next.
    /// </summary>
    public static SheetSource Sheet()
    {
        ReadOnlySpan<Vector3> bumps = [new(0f, 0f, 0.42f), new(0.22f, -0.12f, 0.3f), new(-0.2f, 0.16f, 0.28f), new(0.05f, 0.25f, 0.24f)];
        var rgba = new byte[PuffPx * PuffPx * 4];
        for (var y = 0; y < PuffPx; y++)
        {
            for (var x = 0; x < PuffPx; x++)
            {
                // Across the picture from its middle, −1..1 each way.
                var at = new Vector2(((x + 0.5f) / PuffPx * 2f) - 1f, ((y + 0.5f) / PuffPx * 2f) - 1f);
                var density = 0f;
                foreach (var bump in bumps)
                {
                    var d2 = Vector2.DistanceSquared(at, new Vector2(bump.X, bump.Y)) / (bump.Z * bump.Z);
                    density = MathF.Max(density, MathF.Exp(-d2));
                }

                // Nothing at the quad's edge whatever the bumps, so no puff shows its square.
                var edge = Math.Clamp(1f - at.Length(), 0f, 1f);
                var alpha = density * MathF.Min(1f, edge * 4f);
                var texel = ((y * PuffPx) + x) * 4;
                rgba[texel] = rgba[texel + 1] = rgba[texel + 2] = byte.MaxValue;
                rgba[texel + 3] = (byte)Math.Clamp((alpha * byte.MaxValue) + 0.5f, 0f, byte.MaxValue);
            }
        }

        return SheetSource.Generated(rgba, PuffPx, PuffPx);
    }
}

/// <summary>
/// <b>How the smoke is drawn</b> (<see cref="SmokeSprites.Look"/>): <see cref="SmokeFigures"/> as the device reads them,
/// the rates already divided out — what every pipe shares, and each kind's own.
/// </summary>
/// <param name="EveryS">The finest a pipe's puffs are laid at: <see cref="SmokeFigures.PuffEveryS"/> split <see cref="Splits"/> ways.</param>
/// <param name="Tailpipe">A car's (CAR-54).</param>
/// <param name="Stack">A diesel's stack (TRN-15).</param>
internal readonly record struct SmokeLook(
    float EveryS, float PerEveryS, int Splits, float ScatterMps, float DragS, float ResetMps, ExhaustLook Tailpipe,
    ExhaustLook Stack);

/// <summary>
/// <b>One kind of pipe's smoke</b> (<c>SmokeKind</c>), field for field: each pair idling and at full throttle, a puff laid at
/// the share of the way between them its driver's throttle was when it left; how its body's track is laid; and what those
/// come to at their most.
/// </summary>
/// <param name="ThickestAt">How far through its life a puff is thickest, as a share of it.</param>
/// <param name="PuffGapM">The widest gap its puffs are left at before one is split again (<see cref="SmokeLook.Splits"/>).</param>
/// <param name="ReachM">How far from its body's middle a puff may be drawn.</param>
/// <param name="WidestM">The widest a puff is drawn, at the end of its longest life: a frame whose least part is wider draws none.</param>
/// <param name="LongestS">The longest a puff lives, idling or at full throttle.</param>
/// <param name="PuffsEach">The most puffs a pipe has out at once: one a <see cref="SmokeLook.EveryS"/> over its longest life.</param>
/// <param name="TrackPoints">The points of its way a body keeps, laid one a <c>1 / PerTrackEveryS</c>.</param>
/// <param name="PerFullThrottle">One over the throttle at which it is thickest.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ExhaustLook(
    float IdleOpacity, float FullOpacity, float IdleBirthWidthM, float FullBirthWidthM, float IdleLifeS, float FullLifeS,
    float IdleOutflowMps, float FullOutflowMps, float SpreadMps, float ThickestAt, float PuffGapM, float Shade, float ReachM, float WidestM,
    float LongestS, int PuffsEach, int TrackPoints, float PerTrackEveryS, float PerFullThrottle);
