using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>Cars on the decks over other roads, and what of them stands past a deck's edge</b> (PHY-1b): a wheel there stands
/// on nothing — it carries nothing, grips nothing and the body leans over it (<see cref="ControlAction.Hang"/>) — and a
/// car with its centre of mass there has gone over and falls.
/// </summary>
/// <remarks>
/// <b>A car stands on the deck's own width and not on the ways on it</b>: a deck carries its pavement out to its edges, and
/// its walk is a walker's (WLK-1b), so a car on its pavement is still on the deck.
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>The bridges whose decks stand over other roads, by their place in the plan's; a deck on the ground has no edge to fall from.</summary>
    readonly int[] _raisedDecks;

    /// <summary>The length of each bridge's road, by its place in the plan's: the line its deck is measured across from.</summary>
    readonly float[] _deckLineM;

    /// <summary>Which of each car's wheels the world was last told stand on nothing (<see cref="ControlAction.Hang"/>).</summary>
    readonly byte[] _sentOverNothing;

    static int[] RaisedDecks(CityPlan plan)
    {
        var raised = new List<int>();
        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
        {
            if (plan.Roads.LevelOf(plan.Bridges.Road[bridge]) != CityPlan.RoadArrays.Ground) raised.Add(bridge);
        }

        return [.. raised];
    }

    static float[] DeckLinesM(CityPlan plan)
    {
        var linesM = new float[plan.Bridges.Count];
        for (var bridge = 0; bridge < linesM.Length; bridge++) linesM[bridge] = Spline.TotalLengthM(plan.Roads.SegmentsOf(plan.Bridges.Road[bridge]));

        return linesM;
    }

    /// <summary>
    /// <b>Every car on a deck's channel alone told which of its wheels stand past the deck's edge, and every one gone over it
    /// fallen</b>: put down on the ground's channel, which is what stands under it, pushed off the deck, and a wreck —
    /// whatever cannot break is not — whose driver is thrown clear onto the ground.
    /// </summary>
    void HangOverTheDecks()
    {
        if (_raisedDecks.Length == 0) return;

        var signalsTaken = _world.View.SignalsTaken;
        for (var car = 0; car < Cars.Count; car++)
        {
            // A car set somewhere the view has not read yet is not where the fleet says it is in the world.
            var onADeck = (Cars.Channels[car] & CityPlan.RoadArrays.GroundChannel) == 0 && _carSetBy[car] <= signalsTaken;
            var side = onADeck ? DeckSideOf(Cars.PositionM[car]) : default;
            var overNothing = onADeck ? OverNothing(car, side) : (byte)0;
            var gone = overNothing != 0 && HasGoneOverTheEdge(car, side, overNothing);
            if (gone)
            {
                PutOnChannels(car, CityPlan.RoadArrays.GroundChannel);
                Shove(AgentOfCar(car), OffTheDeckMps(Cars.PositionM[car]));
                if (!CarCatalog.Shared.UnbreakableOf(Cars.Variant[car])) Wreck(car);
                overNothing = 0;
            }

            if (overNothing == _sentOverNothing[car]) continue;

            _sentOverNothing[car] = overNothing;
            Signal(ControlSignal.Hang(AgentOfCar(car), overNothing));
        }
    }

    /// <summary>
    /// <b>Every walker put on the channels of the walk it stands on</b>, as a car is on its ways' (PHY-1a) — so a joint at
    /// a deck's end takes it up onto the deck's walk and back down off it (WLK-1b) — <b>and every one on a deck's channel
    /// alone standing on no way at all fallen</b> (PHY-1b): off the deck's walk and its lanes is over its edge. Put on the
    /// ground's channel, pushed off the deck, and down.
    /// </summary>
    /// <remarks>
    /// <b>Only the walk takes a walker off the ground.</b> Off it, on a carriageway, the ways under a walker put it on their
    /// channels only where they hold the ground's — off a deck's end down its road — or one crossing at a bridgehead, over
    /// its join onto a deck's lane, would be on the deck's channel alone and fall at the far kerb.
    /// </remarks>
    void FallOffTheDecks()
    {
        if (_raisedDecks.Length == 0) return;

        // A view whose step went without its covers (a page's) shows every walker on nothing.
        var view = _world.View;
        if (!view.CoversRead) return;

        for (var person = 0; person < People.Count; person++)
        {
            if (People.Inside[person].Any || _walkerSetBy[person] > view.SignalsTaken) continue;

            byte walk = 0, under = 0;
            foreach (ref readonly var cover in view.CoversOfWalker(person))
            {
                var channels = ChannelsOfWay(cover.Way);
                under |= channels;
                if (_ways.IsWalked(cover.Way)) walk |= channels;
            }

            var on = walk != 0 ? walk : (under & CityPlan.RoadArrays.GroundChannel) != 0 ? under : People.Channels[person];
            if (on != People.Channels[person]) PutTheWalkerOnChannels(person, on);
            if (under != 0 || (on & CityPlan.RoadArrays.GroundChannel) != 0) continue;

            PutTheWalkerOnChannels(person, CityPlan.RoadArrays.GroundChannel);
            Shove(Roster.AgentOfPerson(person), OffTheDeckMps(People.PositionM[person]));
            RaiseTheCall(person);
        }
    }

    /// <summary>
    /// <b>What a body falling off the deck nearest it is pushed by</b> (<see cref="RoadFigures.DeckFallShoveMps"/>):
    /// straight out from the deck's line, and nothing where it stands on the line.
    /// </summary>
    Vector2 OffTheDeckMps(Vector2 atM)
    {
        var (deck, alongM) = DeckNearest(atM);
        var offM = atM - Spline.SampleAt(LineOf(deck), alongM).PositionM;
        var offLengthM = offM.Length();
        return offLengthM > 0f ? offM * (_config.Road.DeckFallShoveMps / offLengthM) : Vector2.Zero;
    }

    /// <summary>
    /// <b>Where a body stands against the deck nearest it</b>: the place on the deck's line square to it, and the line's
    /// right there — what its parts are measured across from (<see cref="DeckSide.IsPastTheEdge"/>).
    /// </summary>
    DeckSide DeckSideOf(Vector2 atM)
    {
        var (deck, alongM) = DeckNearest(atM);
        var line = Spline.SampleAt(LineOf(deck), alongM);
        var halfM = _plan.Bridges.DeckWidthM[deck] * 0.5f;
        return new DeckSide(line.PositionM, line.Right, alongM <= 0f || alongM >= _deckLineM[deck], halfM * halfM);
    }

    /// <summary>
    /// <b>Which of a car's wheels stand past the edge of the deck it is on</b>, a bit a wheel: further off its line than
    /// half its width. Past an end is its bridgehead, which holds a wheel off the line by no more than that either; a car on
    /// a deck's channel far from every deck stands on nothing at all.
    /// </summary>
    byte OverNothing(int car, in DeckSide side)
    {
        var atM = Cars.PositionM[car];
        ref readonly var build = ref Cars.BuildOf(car);
        var forward = Cars.Forward[car];
        var right = Heading.RightOf(forward);
        byte over = 0;
        for (var wheel = 0; wheel < build.WheelCount; wheel++)
        {
            var onBodyM = TyreModel.WheelAtM(build, wheel);
            if (side.IsPastTheEdge(atM + (forward * onBodyM.X) + (right * onBodyM.Y))) over |= (byte)(1 << wheel);
        }

        return over;
    }

    /// <summary>The raised deck whose line is nearest a point, and how far along that line the point stands square to it.</summary>
    (int Deck, float AlongM) DeckNearest(Vector2 pointM)
    {
        var (nearest, nearestAlongM, nearestSq) = (_raisedDecks[0], 0f, float.MaxValue);
        foreach (var deck in _raisedDecks)
        {
            var lineM = _deckLineM[deck];
            var alongM = Spline.ProjectM(LineOf(deck), pointM, lineM * 0.5f, lineM, out var offSq);
            if (offSq >= nearestSq) continue;

            (nearest, nearestAlongM, nearestSq) = (deck, alongM, offSq);
        }

        return (nearest, nearestAlongM);
    }

    /// <summary>A deck's line: the whole of its road, so past its ends is past its bridgeheads.</summary>
    ReadOnlySpan<ArcSeg> LineOf(int deck) => _plan.Roads.SegmentsOf(_plan.Bridges.Road[deck]);

    /// <summary>
    /// <b>Whether a car on a deck has gone over its edge</b>: its centre of mass past it, where nothing holds it up. Asked
    /// only once a whole side of it — every wheel ahead of its middle, behind it, to its left or to its right — stands on
    /// nothing; short of either it only hangs there.
    /// </summary>
    bool HasGoneOverTheEdge(int car, in DeckSide side, byte overNothing) =>
        HasASideOverNothing(car, overNothing)
        && side.IsPastTheEdge(Cars.PositionM[car] + (Cars.Forward[car] * Cars.BuildOf(car).CgAheadOfCentreM));

    bool HasASideOverNothing(int car, byte overNothing)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        bool ahead = false, behind = false, left = false, right = false;
        for (var wheel = 0; wheel < build.WheelCount; wheel++)
        {
            if (((overNothing >> wheel) & 1) != 0) continue;

            var atM = TyreModel.WheelAtM(build, wheel);
            ahead |= atM.X > 0f;
            behind |= atM.X < 0f;
            right |= atM.Y > 0f;
            left |= atM.Y < 0f;
        }

        return !(ahead && behind && left && right);
    }
}

/// <summary>
/// <b>Where a body stands against a deck</b> (<see cref="TownWorld"/>'s <c>DeckSideOf</c>): the place on the deck's line
/// square to it, the line's right there, and half the deck's width squared — so a body's wheels and its centre of mass are
/// measured across off one projection of it rather than one each.
/// </summary>
/// <remarks>
/// <b>The edge is taken as straight along a body's length</b>, which a deck's bend is far too gentle to tell from. Past an
/// end of its line nothing is square to it, and a point is measured straight to that end.
/// </remarks>
readonly record struct DeckSide(Vector2 LineM, Vector2 Right, bool PastAnEnd, float HalfWidthSq)
{
    public bool IsPastTheEdge(Vector2 pointM)
    {
        var offM = pointM - LineM;
        var acrossM = Vector2.Dot(offM, Right);
        return (PastAnEnd ? offM.LengthSquared() : acrossM * acrossM) > HalfWidthSq;
    }
}
