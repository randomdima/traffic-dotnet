namespace TrafficSimulation.CityGen;

/// <summary>
/// Which of the proving ground's shapes a stretch of it is. <b>There are five, and everything else on the
/// lap is a <see cref="Link"/></b> — the ground between two measurements, which exists so that no figure
/// taken on one shape is a fact about the shape before it.
/// </summary>
internal enum TrackShape
{
    /// <summary>Neutral ground: the run away from the end of one shape and up to the next.</summary>
    Link,

    /// <summary>The one stretch long enough to be worth accelerating down and braking for.</summary>
    Straight,

    /// <summary>A half turn tighter than any corner the shipped towns hold.</summary>
    Turn180,

    /// <summary>One corner taken alternately in and out and never let go of.</summary>
    Snake,

    /// <summary>A long sweep at one radius: what a speed <em>held</em> is measured on.</summary>
    Arc,

    /// <summary>A single quarter turn, the corner an ordinary street junction is.</summary>
    Turn90,
}

/// <summary>
/// One stretch of the proving ground, which is also one <em>road</em> of the map it is written out as.
/// </summary>
/// <remarks>
/// <b>A section is a road because that is what makes a measurement local.</b> Every consumer already
/// knows which road a car is on — a lane carries its road, and a car carries its lane — so asking which
/// shape a car is driving costs two loads rather than a search of the geometry, and a figure can never
/// be quoted against a shape the car had already left.
/// </remarks>
/// <param name="Road">The map's own road index, which is also this section's place in the lap.</param>
/// <param name="Name">What the panel and the probe call it. Short, because it is a column heading.</param>
/// <param name="RadiusM">The radius the shape is laid at, or zero where it does not bend.</param>
internal readonly record struct TrackSection(int Road, string Name, TrackShape Shape, float RadiusM)
{
    /// <summary>Whether anything is measured here, which is everything that is not neutral ground.</summary>
    public bool IsShape => Shape != TrackShape.Link;
}
