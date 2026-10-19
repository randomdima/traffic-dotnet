using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Rail;

namespace TrafficSimulation.App.Render;

/// <summary>The sheets a level crossing's gear is drawn from: its arm, the housing it pivots at, and its X light dark and lit.</summary>
internal readonly record struct CrossingSheets(int Barrier, int Drive, int Light, int Lit);

/// <summary>
/// <b>The level crossings' gear, as instances</b> (RAIL-7): on each approach the arm as it stands, over the drive it pivots
/// at, and then an X light over every lane coming in, dark or lit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every piece is the railway's art</b> (<see cref="RailLooks"/>): the drive and the lights at their own footprints, a
/// light turned with its lane as a junction's head is; the arm's picture laid along what is seen of it.
/// </para>
/// <para>
/// <b>An arm is drawn standing off the ground</b>: a place on it a height above its pivot is drawn
/// <see cref="ViewFigures.HeightDrawnAside"/> of that along the road, away from the rails, so a raised arm reads as one
/// standing up from its drive beside the kerb rather than as nothing, and every crossing's arm stands the same way whatever
/// its road's heading. Lowered, it is drawn exactly where its body lies.
/// </para>
/// </remarks>
internal static class CrossingSprites
{
    /// <summary>The instances one approach's gear draws besides its lights: its arm and its housing.</summary>
    public const int EachGear = 2;

    /// <summary>The most instances a plan's crossings draw: their approaches' gear, and a light over every lane a road could hold.</summary>
    public static int MostFor(CityPlan plan, SimConfig config)
    {
        var crossings = LevelCrossings.Of(plan, config);
        var most = 2 * crossings.Crossings * EachGear;
        for (var each = 0; each < crossings.Crossings; each++) most += (int)MathF.Ceiling(plan.Roads.WidthM[crossings.OfRoad[each]] / config.Car.WidthM);

        return most;
    }

    /// <summary>
    /// <b>Where an approach's arm is drawn now</b>, pivot end to tip (RAIL-7): swung up from lying across its lanes by
    /// <see cref="CrossingGear.UpRad"/>, and each place on it drawn aside by how high above its pivot it stands — what the
    /// host's fill draws and what the device's emit is handed each frame, so the two draw one arm.
    /// </summary>
    public static (Vector2 FromM, Vector2 ToM) ArmDrawn(CrossingGear gear, int each, float nowS, SimConfig config)
    {
        var (upSin, upCos) = MathF.SinCos(gear.UpRad(each, nowS));
        var (pivotM, down, armM) = (gear.PivotM[each], gear.DownAlong[each], gear.ArmM[each]);
        return (pivotM, pivotM + (down * (armM * upCos)) + (AwayFromTheRails(down) * (armM * upSin * config.View.HeightDrawnAside)));
    }

    /// <summary>Along the road, back the way its traffic comes in: <see cref="CrossingEdge.Travel"/> turned back from the arm's way down.</summary>
    static Vector2 AwayFromTheRails(Vector2 downAlong) => -Heading.RightOf(downAlong);

    /// <summary>How far past its pivot any of an approach's gear is drawn, either way, for a view's cull.</summary>
    public static float ReachM(CrossingGear gear, int each, SimConfig config) =>
        gear.ArmM[each] + RailLooks.Shared.Drive.FootprintM.X;

    public static int Fill(
        CrossingGear gear, float nowS, SimConfig config, CrossingSheets sheets, Vector2 viewCentreM, Vector2 viewSpanM,
        Span<SpriteInstance> into)
    {
        var written = 0;
        var halfSpanM = viewSpanM * 0.5f;
        var widthM = config.Rail.BarrierWidthM;
        var looks = RailLooks.Shared;
        var (driveHalfM, lightHalfM) = (looks.Drive.FootprintM * 0.5f, looks.Light.FootprintM * 0.5f);
        for (var each = 0; each < gear.Count && written + EachGear <= into.Length; each++)
        {
            var reachM = ReachM(gear, each, config);
            var offM = Vector2.Abs(gear.PivotM[each] - viewCentreM);
            if (offM.X > halfSpanM.X + reachM || offM.Y > halfSpanM.Y + reachM) continue;

            var (fromM, toM) = ArmDrawn(gear, each, nowS, config);
            var armM = toM - fromM;
            var drawnM = armM.Length();
            if (drawnM > widthM)
            {
                into[written++] = new SpriteInstance(
                    (fromM + toM) * 0.5f, new Vector2(drawnM, widthM) * 0.5f, SpriteCell.Whole, SpriteTint.Plain, (uint)sheets.Barrier,
                    MathF.Atan2(armM.Y, armM.X));
            }

            into[written++] = new SpriteInstance(
                fromM - (gear.DownAlong[each] * driveHalfM.X), driveHalfM, SpriteCell.Whole, SpriteTint.Plain, (uint)sheets.Drive,
                gear.ArmHeadingRad[each]);
        }

        var lightReachM = lightHalfM.Length();
        foreach (var light in gear.Lights)
        {
            if (written >= into.Length) break;

            var offM = Vector2.Abs(light.CentreM - viewCentreM);
            if (offM.X > halfSpanM.X + lightReachM || offM.Y > halfSpanM.Y + lightReachM) continue;

            into[written++] = new SpriteInstance(
                light.CentreM, lightHalfM, SpriteCell.Whole, SpriteTint.Plain, (uint)(gear.Lit(light.Approach, nowS) ? sheets.Lit : sheets.Light),
                light.HeadingRad);
        }

        return written;
    }
}
