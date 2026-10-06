using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen.Traced;

internal static partial class TracedStreets
{
    /// <summary>
    /// <b>What gathering junctions came to</b> (<see cref="Gathered"/>): how many junctions were gathered into how many,
    /// how many of those were places of two arms inside another's disc, how many roads between them went, and how many
    /// movements through one none of those roads made.
    /// </summary>
    internal readonly record struct GatheredLaid(int Junctions, int Into, int Places, int Roads, int Unmade);

    /// <summary>A movement off one road onto another through a gathered junction that the roads it was gathered over made no way for.</summary>
    readonly record struct Unmade(int Junction, Road From, Road To);

    /// <summary>
    /// The roads between two junctions of three arms or more, run on through every place of two arms between them, and
    /// how long they run.
    /// </summary>
    sealed record Link(int From, int To, float LengthM, List<Road> Roads, List<int> Between);

    /// <summary>
    /// <b>Junctions of three arms or more standing nearer each other than
    /// <see cref="CityGenFigures.TracedJunctionsMergedM"/> are one junction</b> (GEN-57): where a road between two runs
    /// shorter than that, through nothing but places of two arms, the two are gathered into one standing amid them, and
    /// so is every junction gathered with either while every one of them stays that near every other. The roads between
    /// them go, their ground the junction's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Each road keeps the disc of the junction it was surveyed to</b>, and leaves it where it did: the gathered
    /// junction's box is theirs together, its movements drawn lane end to lane end across it, and the place each is laid
    /// as is the one standing amid them.
    /// </para>
    /// <para>
    /// <b>So is a place of two arms standing inside a junction's own disc</b> (<see cref="SimConfig.JunctionRadiusAcrossM"/>),
    /// reached through nothing but such places: a way OSM changes a few metres past the node it crosses at, or a flared mouth
    /// tagged with lanes of its own. Kept apart, the road between is too short for either disc, its lanes end as near the
    /// middle as it squeezes them (<see cref="Standoffs"/>), and every turn onto its far lanes begins past where their lines
    /// cross.
    /// </para>
    /// <para>
    /// <b>What the roads between them made is what the box makes</b>: a movement off one road onto another is made
    /// across it only where the roads it gathered drove a way from the one to the other — a gap in a median a one-way
    /// link runs across one way.
    /// </para>
    /// <para>
    /// <b>Not gathered</b>: a junction on a bridge or on a roundabout, whose ring is its own; two whose gathering would
    /// leave a road between them running out and back to the one junction; and a gathering that would leave fewer than
    /// two roads into it, a yard's loop at the end of a street.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The junction each junction is laid as — itself, or the one it was gathered into — and the movements across those
    /// gathered none of their roads made.
    /// </returns>
    static (int[] Into, List<Unmade> Unmade, GatheredLaid Tally) Gathered(List<Road> roads, List<Vector2> centreM, SimConfig config)
    {
        var withinM = config.CityGen.TracedJunctionsMergedM;
        var junctions = centreM.Count;
        var at = RoadEndsAt(roads, junctions);
        var gatherable = new bool[junctions];
        for (var junction = 0; junction < junctions; junction++)
        {
            gatherable[junction] = at[junction].TrueForAll(end =>
                end.Road.Carriage.Level == CityPlan.RoadArrays.Ground && !end.Road.Carriage.Circulates);
        }

        var links = new List<Link>();
        for (var junction = 0; junction < junctions; junction++)
        {
            if (at[junction].Count < 3 || !gatherable[junction]) continue;

            foreach (var end in at[junction])
            {
                if (Linked(at, gatherable, junction, end, withinM) is { } link && link.To > junction) links.Add(link);
            }
        }

        links.Sort((one, other) => one.LengthM.CompareTo(other.LengthM));
        var group = new Groups(at, centreM, withinM);
        foreach (var link in links) group.Joined(link);

        var places = 0;
        for (var junction = 0; junction < junctions; junction++)
        {
            if (at[junction].Count < 3 || !gatherable[junction]) continue;

            var discM = config.JunctionRadiusAcrossM(at[junction].Max(end => end.Road.Carriage.WidthM));
            foreach (var end in at[junction]) places += group.Spurred(junction, PlacesInside(at, gatherable, junction, end, discM));
        }

        var into = new List<int>(junctions);
        for (var junction = 0; junction < junctions; junction++) into.Add(junction);

        var unmade = new List<Unmade>();
        var (gathered, dropped) = (0, 0);
        foreach (var members in group.Gatherings())
        {
            var outer = new List<(Road Road, bool AtTo)>();
            foreach (var member in members)
            {
                foreach (var end in at[member])
                {
                    if (!group.Inside(end.Road)) outer.Add(end);
                }
            }

            if (outer.Count < 2) continue;

            var junction = centreM.Count;
            var middleM = Vector2.Zero;
            var corners = 0;
            foreach (var member in members)
            {
                into[member] = junction;
                if (at[member].Count < 3) continue;

                middleM += centreM[member];
                corners++;
            }

            centreM.Add(middleM / corners);
            into.Add(junction);
            gathered += members.Count;

            foreach (var (arriving, atTo) in outer)
            {
                if (!Arrives(arriving, atTo)) continue;

                var reached = Reached(at, group, (arriving, atTo));
                foreach (var (leaving, leavingAtTo) in outer)
                {
                    if (leaving != arriving && Leaves(leaving, leavingAtTo) && !reached.Contains(leaving)) unmade.Add(new Unmade(junction, arriving, leaving));
                }
            }

            foreach (var member in members)
            {
                foreach (var (road, _) in at[member])
                {
                    if (!group.Inside(road) || road.Gone) continue;

                    road.Gone = true;
                    dropped++;
                }
            }
        }

        roads.RemoveAll(road => road.Gone);
        return ([.. into], unmade, new GatheredLaid(gathered, into.Count - junctions, places, dropped, unmade.Count));
    }

    /// <summary>
    /// The places of two arms run through from one junction along one of its roads while the way there stays shorter than
    /// <paramref name="discM"/> — none past a bridge, a roundabout or a dead end, or once the way comes back to where it set off.
    /// </summary>
    static List<int> PlacesInside(List<(Road Road, bool AtTo)>[] at, bool[] gatherable, int from, (Road Road, bool AtTo) end, float discM)
    {
        var inside = new List<int>();
        var lengthM = 0f;
        var (road, atTo) = end;
        while (true)
        {
            lengthM += LengthOf(road);
            var reached = atTo ? road.From : road.To;
            if (lengthM >= discM || reached == from || !gatherable[reached] || at[reached].Count != 2) return inside;

            inside.Add(reached);
            (road, atTo) = at[reached][0].Road == road ? at[reached][1] : at[reached][0];
        }
    }

    /// <summary>Whether any of a road's lanes run into the junction at one of its ends.</summary>
    static bool Arrives(Road road, bool atTo) => (atTo ? road.Carriage.Lanes.With : road.Carriage.Lanes.Against) > 0;

    /// <summary>Whether any of a road's lanes set off from the junction at one of its ends.</summary>
    static bool Leaves(Road road, bool atTo) => (atTo ? road.Carriage.Lanes.Against : road.Carriage.Lanes.With) > 0;

    /// <summary>Every road end at each junction, as (road, whether it is the road's <see cref="Road.To"/>).</summary>
    static List<(Road Road, bool AtTo)>[] RoadEndsAt(List<Road> roads, int junctions)
    {
        var at = new List<(Road Road, bool AtTo)>[junctions];
        for (var junction = 0; junction < junctions; junction++) at[junction] = [];
        foreach (var road in roads)
        {
            at[road.From].Add((road, false));
            at[road.To].Add((road, true));
        }

        return at;
    }

    /// <summary>
    /// The link setting off from one junction along one of its roads, run on through every place of two arms to the next
    /// junction of three or more — or none, where it runs as far as <paramref name="withinM"/>, back to where it set off,
    /// to a dead end, or onto a bridge or a roundabout.
    /// </summary>
    static Link? Linked(List<(Road Road, bool AtTo)>[] at, bool[] gatherable, int from, (Road Road, bool AtTo) end, float withinM)
    {
        var roads = new List<Road>();
        var between = new List<int>();
        var lengthM = 0f;
        var (road, atTo) = end;
        while (true)
        {
            roads.Add(road);
            lengthM += LengthOf(road);
            var reached = atTo ? road.From : road.To;
            if (lengthM >= withinM || reached == from || !gatherable[reached]) return null;
            if (at[reached].Count != 2) return at[reached].Count > 2 ? new Link(from, reached, lengthM, roads, between) : null;

            between.Add(reached);
            (road, atTo) = at[reached][0].Road == road ? at[reached][1] : at[reached][0];
        }
    }

    /// <summary>
    /// The junctions gathered so far, each gathering under one of them: a link joins the two it runs between where every
    /// junction of three arms or more in the one stands within <paramref name="withinM"/> of every one in the other, and no
    /// road between them runs that far.
    /// </summary>
    sealed class Groups(List<(Road Road, bool AtTo)>[] at, List<Vector2> centreM, float withinM)
    {
        readonly int[] _under = Identity(at.Length);
        readonly Dictionary<int, List<int>> _members = [];

        public void Joined(Link link)
        {
            var (one, other) = (Under(link.From), Under(link.To));
            if (one != other && (!Within(one, other) || RunsFar(one, other))) return;

            if (one != other)
            {
                var kept = Members(one);
                kept.AddRange(Members(other));
                _members.Remove(other);
                _under[other] = one;
            }

            foreach (var between in link.Between)
            {
                _under[between] = one;
                Members(one).Add(between);
            }
        }

        /// <summary>
        /// The places of two arms inside a junction's disc gathered with it, in order out from it, as far as the first one
        /// already gathered with another — and how many were.
        /// </summary>
        public int Spurred(int junction, List<int> places)
        {
            var under = Under(junction);
            var spurred = 0;
            foreach (var place in places)
            {
                if (Under(place) != place || _members.ContainsKey(place)) break;

                _under[place] = under;
                Members(under).Add(place);
                spurred++;
            }

            return spurred;
        }

        /// <summary>Every gathering of two junctions or more, its members, in the order of the junction each is under.</summary>
        public IEnumerable<List<int>> Gatherings()
        {
            for (var junction = 0; junction < _under.Length; junction++)
            {
                if (_under[junction] == junction && _members.TryGetValue(junction, out var members) && members.Count > 1) yield return members;
            }
        }

        /// <summary>Whether both of a road's ends are in one gathering, which makes it the gathered junction's ground.</summary>
        public bool Inside(Road road)
        {
            var under = Under(road.From);
            return under == Under(road.To) && _members.TryGetValue(under, out var members) && members.Count > 1;
        }

        int Under(int junction)
        {
            while (_under[junction] != junction) junction = _under[junction] = _under[_under[junction]];
            return junction;
        }

        List<int> Members(int under)
        {
            if (!_members.TryGetValue(under, out var members)) _members[under] = members = [under];
            return members;
        }

        bool Within(int one, int other)
        {
            foreach (var near in Members(one))
            {
                if (at[near].Count < 3) continue;

                foreach (var far in Members(other))
                {
                    if (at[far].Count > 2 && Vector2.Distance(centreM[near], centreM[far]) >= withinM) return false;
                }
            }

            return true;
        }

        bool RunsFar(int one, int other)
        {
            foreach (var member in Members(one))
            {
                foreach (var (road, atTo) in at[member])
                {
                    if (Under(atTo ? road.From : road.To) == other && LengthOf(road) >= withinM) return true;
                }
            }

            return false;
        }

        static int[] Identity(int count)
        {
            var identity = new int[count];
            for (var at = 0; at < count; at++) identity[at] = at;
            return identity;
        }
    }

    /// <summary>
    /// <b>Every road out of a gathering that traffic arriving on one road can reach</b> along the roads inside it, each
    /// turn at each of its places made only where the road it turns onto sets off from there.
    /// </summary>
    static HashSet<Road> Reached(List<(Road Road, bool AtTo)>[] at, Groups group, (Road Road, bool AtTo) arriving)
    {
        var reached = new HashSet<Road>();
        var seen = new HashSet<(Road Road, bool AtTo)> { arriving };
        var next = new Queue<(Road Road, bool AtTo)>();
        next.Enqueue(arriving);
        while (next.TryDequeue(out var arrival))
        {
            var junction = arrival.AtTo ? arrival.Road.To : arrival.Road.From;
            var here = at[junction];
            foreach (var leaving in here)
            {
                if (leaving.Road == arrival.Road || !Leaves(leaving.Road, leaving.AtTo)) continue;

                if (!group.Inside(leaving.Road))
                {
                    reached.Add(leaving.Road);
                    continue;
                }

                var onward = (leaving.Road, !leaving.AtTo);
                if (seen.Add(onward)) next.Enqueue(onward);
            }
        }

        return reached;
    }
}
