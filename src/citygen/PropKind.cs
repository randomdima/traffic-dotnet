namespace TrafficSimulation.CityGen;

/// <summary>
/// The three kinds of prop a town scatters, in the order the <c>.town</c> file's prop bytes carry them
/// and the order the catalogue's sets are laid out in.
/// </summary>
/// <remarks>
/// <para>
/// <b>A kind is a placement and not a picture</b> (GEN-6b). It says which ground a prop belongs on — the
/// open country between the built parts of a town, the verge along a street, the grass beside a car park
/// — and the catalogue holds one set of looks for each. Two things that look nothing alike share a kind
/// where they stand in the same place, and a stump and a planter are different kinds for the same reason.
/// </para>
/// <para>
/// <b>It is the plan's vocabulary and lives with the plan</b>, as <see cref="Ground"/> does, so that
/// <see cref="CityPlan"/> stays pure data that everything else reads.
/// </para>
/// </remarks>
internal enum PropKind : byte
{
    /// <summary>What grows where nobody tends it: trees, thickets, rocks, meadow.</summary>
    WildNature = 0,

    /// <summary>What a town plants along a walk or a lot: beds, planters, hedges, mown weeds.</summary>
    UrbanNature = 1,

    /// <summary>What a town stands beside its cars: bins, bollards, crates, the things a park is furnished with.</summary>
    UrbanFurniture = 2,
}
