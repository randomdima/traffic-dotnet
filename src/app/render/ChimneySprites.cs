using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>The smoke off a roof's chimneys</b> (OBJ-7): a slow plume off the pots of whichever of the town's chimneys are
/// burning, laid out downwind — <b>drawn on the device alone</b>, by the emit (<c>sprites.glsl</c>'s
/// <c>spritesChimneyPuff</c>), through the exhausts' picture (<see cref="SmokeSprites.Sheet"/>). This is what it is laid
/// from.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every chimney can smoke, and a share of them is burning at any one time</b>
/// (<see cref="ChimneyFigures.SmokingShare"/>): each stack's fire is lit or let out afresh every
/// <see cref="ChimneyFigures.FirePeriodS"/>, off nothing but its own key and the clock (<c>spritesChimneyBurning</c>), so
/// nothing is kept for it. Whether a puff is drawn is asked at the moment it left the pot, so a fire lit grows its plume out
/// of the pot and one let out sees its last smoke carried off.
/// </para>
/// <para>
/// <b>A chimney stands still, so nothing is kept for its smoke</b>: the puff that left a pot <c>a</c> seconds ago rose out of
/// it for <see cref="ChimneyFigures.RiseS"/> — standing on the pot, seen from above, so the plume's root is the pot whatever
/// the wind — and is where the wind has carried it since, and the eddy it left on, given up to the wind over
/// <see cref="ChimneyFigures.DragS"/>.
/// Which puff it is is the clock's, and what tells it from the next a hash of which it is, as a car's are
/// (<see cref="SmokeSprites"/>). Where the air took a puff is the air of every moment since it left (<c>Wind.Back</c>), so a
/// turn bends the plume from the pot out and a gust is a stretch of it carried further.
/// </para>
/// <para>
/// <b>A wider pot puts out a thicker, wider and longer plume</b> than a house's (<see cref="ChimneyFigures.PotWidthM"/>),
/// <c>k</c> times as wide: a puff leaves it as wide as the pot; it is as opaque as <c>k</c> of a house's laid one over the
/// other, the depth of smoke a look down through it crosses; and it lives <c>√k</c> times as long, its smoke spread over
/// more air before it is thin.
/// </para>
/// </remarks>
internal static class ChimneySprites
{
    /// <summary>The draw each stack's fire is keyed from, apart from every other the seed feeds.</summary>
    const ulong Stream = 0x63686d6e;

    /// <summary>
    /// How many of a row's <see cref="ChimneyRow.Seed"/>'s low bits are its pot within its stack; the bits above them are
    /// the stack's fire, which every pot of it shares. <c>sprites.glsl</c>'s <c>ChimneyPotBits</c> is this.
    /// </summary>
    public const int PotBits = 4;

    /// <summary>
    /// <b>Every pot of every chimney in the town</b>, where it stands, how wide it is there and its key: a draw of the
    /// plan's seed a stack, building by building, so the same town's fires burn in the same turns, and every pot of a stack
    /// puffs a plume of its own while its fire burns — a twin stack's two, each a pot wide.
    /// </summary>
    public static ChimneyRow[] Pots(CityPlan plan, BuildingCatalog catalogue, BuildingUses uses, ChimneyFigures figures)
    {
        var draw = new Rng(plan.Seed, Stream);
        var rows = new List<ChimneyRow>();
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var roof = BuildingRoofs.Of(plan, catalogue, uses, building);
            ref readonly var variant = ref catalogue.Variants[roof.Variant];
            var scale = roof.FootprintM / variant.FootprintM;
            var potM = (variant.PotWidthM ?? figures.PotWidthM) * (scale.X + scale.Y) * 0.5f;
            for (var chimney = 0; chimney < variant.ChimneysM.Length; chimney++)
            {
                var pots = variant.ChimneysM[chimney].Length;
                if (pots > 1 << PotBits)
                    throw new InvalidDataException($"{variant.Id}'s chimney {chimney} carries {pots} pots, and a key holds {1 << PotBits}.");

                var fire = draw.NextUint() << PotBits;
                for (var pot = 0; pot < pots; pot++)
                {
                    rows.Add(new ChimneyRow(PotAtM(plan, catalogue, roof, building, chimney, pot), fire | (uint)pot, potM));
                }
            }
        }

        return [.. rows];
    }

    /// <summary>The stack a row's pot belongs to, as its fire is keyed (<see cref="PotBits"/>).</summary>
    public static uint FireOf(in ChimneyRow row) => row.Seed >> PotBits;

    /// <summary>
    /// Where a building's <paramref name="chimney"/>-th chimney's <paramref name="pot"/>-th pot stands in the town: the
    /// picture's own axes turned onto the roof's heading and scaled with it, as the quad it is drawn on is (<c>quad.glsl</c>).
    /// </summary>
    public static Vector2 PotAtM(CityPlan plan, BuildingCatalog catalogue, BuildingRoof roof, int building, int chimney, int pot)
    {
        ref readonly var variant = ref catalogue.Variants[roof.Variant];
        var atM = variant.ChimneysM[chimney][pot] * (roof.FootprintM / variant.FootprintM);
        var (sin, cos) = MathF.SinCos(roof.HeadingRad);
        return plan.Buildings.CentreM[building] + new Vector2((atM.X * cos) - (atM.Y * sin), (atM.X * sin) + (atM.Y * cos));
    }

    /// <summary>
    /// The chimneys' smoke's figures as the device reads them, laid once with the rates divided out, and what its widest
    /// pot of the <paramref name="catalogue"/> makes of them — the most puffs, the widest and the furthest any chimney's
    /// are drawn.
    /// </summary>
    public static ChimneyLook Look(SimConfig config, BuildingCatalog catalogue)
    {
        var chimneys = config.Chimneys;
        var wind = config.Wind;
        var widestPotM = chimneys.PotWidthM;
        foreach (var variant in catalogue.Variants)
        {
            if (variant.ChimneysM.Length > 0) widestPotM = MathF.Max(widestPotM, variant.PotWidthM ?? chimneys.PotWidthM);
        }

        var longestS = LifeS(chimneys, widestPotM);
        var widestM = widestPotM + (chimneys.SpreadMps * longestS);

        // As far as the hardest gust carries a puff over its life, the eddy's run on top, and half its widest.
        var reachM = (wind.SpeedMps * (1f + wind.GustShare) * longestS) + (chimneys.ScatterMps * chimneys.DragS) + (widestM * 0.5f);
        return new ChimneyLook(
            chimneys.PuffEveryS, 1f / chimneys.PuffEveryS, (int)MathF.Floor(longestS / chimneys.PuffEveryS) + 1,
            chimneys.PuffLifeS, chimneys.RiseS, 1f / chimneys.PotWidthM, chimneys.SpreadMps, chimneys.ScatterMps,
            chimneys.DragS, chimneys.Opacity, chimneys.Shade, chimneys.BreathShare, 1f / chimneys.BreathPeriodS,
            chimneys.SmokingShare, 1f / chimneys.FirePeriodS, widestM, reachM, chimneys.MostPuffsDrawn);
    }

    /// <summary>How long a puff off a pot <paramref name="potM"/> wide is drawn for: a house's, as the root of how much wider it is (<c>spritesChimneyPot</c>).</summary>
    static float LifeS(ChimneyFigures chimneys, float potM) => chimneys.PuffLifeS * MathF.Sqrt(potM / chimneys.PotWidthM);
}

/// <summary>
/// One pot of a chimney (<c>ChimneyRow</c>): where it stands in the town, its key — its stack's fire and which pot of it
/// this is (<see cref="ChimneySprites.PotBits"/>) — and how wide the pot is there.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ChimneyRow(Vector2 AtM, uint Seed, float PotWidthM);

/// <summary>
/// <b>How the chimneys' smoke is drawn</b> (<see cref="ChimneySprites.Look"/>): <see cref="ChimneyFigures"/> as the device
/// reads them, the rates already divided out, a house's pot's figures for every pot to be scaled from.
/// </summary>
/// <param name="PuffsEach">The most puffs a chimney has out at once, the widest pot's: one a <see cref="EveryS"/> over its life.</param>
/// <param name="LifeS">How long a puff off a house's pot is drawn for.</param>
/// <param name="PerPotWidthM">One over a house's pot's width.</param>
/// <param name="PerFireS">One over <see cref="ChimneyFigures.FirePeriodS"/>.</param>
/// <param name="WidestM">The widest any puff is drawn, at the end of its life: a frame whose least part is wider draws none.</param>
/// <param name="ReachM">How far from its pot a puff may be drawn: a chimney further off the view is left out.</param>
internal readonly record struct ChimneyLook(
    float EveryS, float PerEveryS, int PuffsEach, float LifeS, float RiseS, float PerPotWidthM, float SpreadMps,
    float ScatterMps, float DragS, float Opacity, float Shade, float BreathShare, float PerBreathS, float SmokingShare,
    float PerFireS, float WidestM, float ReachM, int MostPuffsDrawn);
