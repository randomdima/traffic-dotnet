namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The art a town is sized by</b>: the footprints its buildings are stood at (GEN-54, GEN-58), the looks its props
/// are laid as (GEN-6e) and how many person looks there are to stand, read off the art by the catalogues above this
/// slice and handed down as data (<c>World.Town.ShippedArt</c>).
/// </summary>
/// <remarks>
/// <b>One record for whoever opens a map, and its halves for the stages that need one</b>: a building's stages are
/// handed <see cref="Buildings"/> and never learn that props are drawn.
/// </remarks>
/// <param name="PersonLooks">Every person look, the walkers' and the uniforms': what the showroom stands once each.</param>
internal readonly record struct ArtSizes(BuildingSizes Buildings, PropSizes Props, int PersonLooks = 0)
{
    /// <summary>No art at all — a town laid in code, which stands nothing that wears a roof or a look.</summary>
    public static ArtSizes None => new(BuildingSizes.None, PropSizes.None);
}
