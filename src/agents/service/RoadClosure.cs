using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Service;

/// <summary>
/// <b>Which lanes one closure holds, and where its entrance is</b> (SRV-9): the lane a scene stands on, every lane
/// upstream of it that leads nowhere else, and every lane that leads only into those — so no car coming down the
/// road is left a lane whose one way on is closed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The entrance is walked back to a junction a driver chooses at</b>, and never stopped at a bend: a lane whose
/// only way on is into the closure is a lane a car turned back at the mouth would be trapped in, since no junction
/// turns a car round (TER-5f). A bay is not a way on (GEN-4h). <b>Where more than one lane feeds the entrance</b>, each that leads nowhere else is closed with
/// it, walked back the same way: the officer stands at the one mouth, and the rest is out of every route (SRV-10).
/// </para>
/// <para>
/// <b>Bounded by the room it is laid into</b>, which is a stretch of a street and never a district: a road with no
/// junction for that many lanes back is closed from as far back as the room reaches.
/// </para>
/// </remarks>
internal static class RoadClosure
{
    /// <summary>
    /// The lanes a closure of <paramref name="sceneLane"/> holds, written into <paramref name="into"/>: <b>the
    /// entrance first, then down to the scene's own lane, then the lanes that lead only into those</b>; how many
    /// were written.
    /// </summary>
    public static int Stretch(RoadGraph roads, int sceneLane, Span<int> into)
    {
        if (into.Length == 0 || sceneLane < 0) return 0;

        var chain = TheChain(roads, sceneLane, into);
        var count = chain;
        for (var at = 0; at < count && count < into.Length; at++)
        {
            foreach (var arriving in roads.Places.LanesArriving(roads.Places.Starting(into[at])))
            {
                if (count == into.Length) break;
                if (roads.IsABayArm(arriving) || into[..count].Contains(arriving)) continue;
                if (roads.ConnectorBetween(arriving, into[at]) == RoadGraph.NoConnector) continue;
                if (!LeadsOnlyInto(roads, arriving, into[..count])) continue;

                into[count++] = arriving;
            }
        }

        return count;
    }

    /// <summary>
    /// <b>The scene's lane and the single file of lanes that lead only to it</b>, entrance first — the stretch the
    /// officer stands at the head of.
    /// </summary>
    static int TheChain(RoadGraph roads, int sceneLane, Span<int> into)
    {
        var count = 1;
        into[into.Length - 1] = sceneLane;
        var lane = sceneLane;
        while (count < into.Length && TheOnlyFeeder(roads, lane) is var feeder and >= 0
               && LeadsOnlyInto(roads, feeder, into[(into.Length - count)..]))
        {
            // A feeder already held is a ring closing on itself, which a closure never walks round.
            if (into[(into.Length - count)..].Contains(feeder)) break;

            lane = feeder;
            count++;
            into[into.Length - count] = lane;
        }

        into[(into.Length - count)..].CopyTo(into);
        return count;
    }

    /// <summary>
    /// <b>The one lane of the carriageway that feeds this one</b>, or −1 where there is none or more than one — a
    /// junction whose traffic reaches the lane from several arms is a junction to stand at.
    /// </summary>
    static int TheOnlyFeeder(RoadGraph roads, int lane)
    {
        var feeder = -1;
        foreach (var arriving in roads.Places.LanesArriving(roads.Places.Starting(lane)))
        {
            if (roads.IsABayArm(arriving) || roads.ConnectorBetween(arriving, lane) == RoadGraph.NoConnector) continue;
            if (feeder >= 0) return -1;

            feeder = arriving;
        }

        return feeder;
    }

    /// <summary>Whether a car on <paramref name="lane"/> has no way on but into <paramref name="closed"/>, bays not counted.</summary>
    static bool LeadsOnlyInto(RoadGraph roads, int lane, ReadOnlySpan<int> closed)
    {
        foreach (var onward in roads.LanesFrom(lane))
        {
            if (!closed.Contains(onward) && !roads.IsABayArm(onward)) return false;
        }

        return true;
    }
}
