namespace TrafficSimulation.Core.Config;

/// <summary>
/// <b>The figures a debug session may move while a town is standing</b>, each as a share of what the build
/// ships. One is a run exactly as it shipped; the panel offers a tenth to ten times.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the one group that can be set after it is loaded, and that is what it is for.</b> Every other
/// figure is authored once and injected; these are turned while somebody watches the town answer, which is
/// the whole instrument. Every one of them defaults to <c>1</c>, so a run that never opens the panel is the
/// run the build ships — bit for bit, since a multiply by one is exact.
/// </para>
/// <para>
/// <b>Only what the whole town shares is here, and a car's own figures are not.</b> What a panel may move
/// is the road: the coefficient between rubber and tarmac, and what each ground costs a wheel simply going
/// round. Both are properties of a <em>surface</em>, true of every body standing on it. A steering lock, a
/// centre of gravity, a mass, an engine — those belong to one car and are stated in that car's own file
/// (<see cref="Agents.Car.Body.CarVariantFile"/>), where a fleet of nineteen bodies keeps nineteen answers.
/// A dial over them is one figure pretending to speak for all of them.
/// </para>
/// <para>
/// <b>Each is spent at the one site that figure is read into a body or a surface</b> — the build's own
/// resolve and the ground catalogue — so nothing on a hot path asks whether a session is being debugged.
/// Both of those are re-read on the town that is standing (<c>TownWorld.FiguresChanged</c>), so a figure
/// takes hold without the map being laid again: the marks stay on the road and the cars stay where they
/// are, which is the whole of what makes this an instrument rather than a restart.
/// </para>
/// </remarks>
internal sealed class TrimFigures
{
    /// <summary>
    /// The coefficient of friction between rubber and road — and so, once it has met a weight, the speed
    /// every corner is taken at and the ground every stop takes.
    /// </summary>
    public float Friction { get; set; } = 1f;

    /// <summary>
    /// The ground's own resistance to a wheel simply going round. <b>It is the only thing a held throttle
    /// settles against</b>: with no speed-squared term in the model, a fixed lock and a fixed pedal come to
    /// rest where the drive meets this, so it is what a skidpad row's equilibrium is made of.
    /// </summary>
    public float RollingResistance { get; set; } = 1f;

    /// <summary>How many trims there are, which is how many rows the panel lays.</summary>
    public const int Count = 2;

    /// <summary>
    /// What each is called on the panel. <b>Both name the raw term they move</b> and not what that term
    /// comes to — a coefficient rather than a grip — because a dial on a derived figure is a dial that has
    /// to be solved backwards to know what it did.
    /// </summary>
    /// <remarks><b>Printable ASCII only</b>, as every string the interface draws is: the glyph sheet carries that range and nothing else.</remarks>
    public static readonly string[] Names = ["Tyre friction", "Rolling resistance"];

    /// <summary>
    /// One trim by its place in the panel. <b>Read and written in one place each</b>, so the row that is
    /// drawn, the row that is dragged and the figure that moves cannot come apart — which is the mistake
    /// the debug switches were written to stop making a second time.
    /// </summary>
    public float Of(int trim) => trim == 0 ? Friction : RollingResistance;

    public void Set(int trim, float share)
    {
        var held = Math.Clamp(share, Least, Most);
        if (trim == 0) Friction = held;
        else RollingResistance = held;
    }

    /// <summary>
    /// How far either way a trim may be taken. <b>A tenth and ten times, which is a decade each side of
    /// shipped</b> — far enough that a figure being wrong by a factor shows up as a town behaving unlike
    /// itself, and bounded so that a slider cannot put a number somewhere arithmetic stops meaning
    /// anything.
    /// </summary>
    public const float Least = 0.1f;

    public const float Most = 10f;

    /// <summary>Whether every trim is where the build shipped it, which is what the panel says in its heading.</summary>
    public bool Untouched
    {
        get
        {
            for (var trim = 0; trim < Count; trim++)
            {
                if (Of(trim) != 1f) return false;
            }

            return true;
        }
    }

    public void Reset()
    {
        for (var trim = 0; trim < Count; trim++) Set(trim, 1f);
    }
}
