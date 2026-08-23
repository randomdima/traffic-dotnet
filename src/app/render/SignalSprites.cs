using System.Numerics;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Render;

/// <summary>
/// The signal heads, as instances of the same pipeline everything else standing on the ground uses.
/// A head does not move, but <b>what it shows changes every few seconds</b>, so unlike a building it is
/// filled per frame — from the one town-wide lookup, exactly as an agent would read it.
/// </summary>
/// <remarks>
/// <para>
/// Each head shows exactly one lit lamp: the colour is a column of the head's
/// own strip, and the strip's first column — every lamp dark — is drawn by nothing. A dark head would be
/// a bundle publishing no colour, which the cycle table cannot produce.
/// </para>
/// <para>
/// The two arts carry different numbers of frames — a car head has an amber and a walker's has not,
/// because "do not <em>begin</em> crossing" carries the whole of the warning — so the column is read off
/// two small tables rather than off the colour's own number.
/// </para>
/// </remarks>
internal static class SignalSprites
{
    /// <summary>Dark, red, amber, green.</summary>
    public const int CarFrames = 4;

    /// <summary>Dark, red, green.</summary>
    public const int WalkFrames = 3;

    public static int Fill(
        TownWorld world, SimConfig config, int firstSheet, Vector2 viewCentreM, Vector2 viewSpanM,
        Span<SpriteInstance> into)
    {
        var heads = world.Heads.Heads;
        if (heads.Length == 0) return 0;

        var timeS = world.ElapsedS;
        var halfView = viewSpanM * 0.5f;
        var carHalfM = new Vector2(config.Signals.CarHeadLengthM, config.Signals.CarHeadWidthM) * 0.5f;
        var walkHalfM = new Vector2(config.Signals.WalkHeadWidthM, config.Signals.WalkHeadLengthM) * 0.5f;
        var reachM = MathF.Max(carHalfM.Length(), walkHalfM.Length());

        var written = 0;
        for (var head = 0; head < heads.Length && written < into.Length; head++)
        {
            var centreM = heads[head].CentreM;
            var offset = centreM - viewCentreM;
            if (MathF.Abs(offset.X) > halfView.X + reachM || MathF.Abs(offset.Y) > halfView.Y + reachM) continue;

            var forCars = heads[head].ForCars;
            var colour = forCars
                ? world.Signals.ForApproach(heads[head].Subject, timeS)
                : world.Signals.ForCrossing(heads[head].Subject, timeS);

            var frames = forCars ? CarFrames : WalkFrames;
            var column = forCars ? CarColumn(colour) : WalkColumn(colour);
            into[written++] = new SpriteInstance(
                centreM, forCars ? carHalfM : walkHalfM, new Vector2(column / (float)frames, 0f),
                new Vector2(1f / frames, 1f), PersonSprites.Plain, (uint)(firstSheet + (forCars ? 0 : 1)),
                heads[head].HeadingRad);
        }

        return written;
    }

    static int CarColumn(SignalColour colour) => colour switch
    {
        SignalColour.Red => 1,
        SignalColour.Amber => 2,
        _ => 3,
    };

    static int WalkColumn(SignalColour colour) => colour == SignalColour.Green ? 2 : 1;
}
