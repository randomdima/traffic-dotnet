using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.App.Camera;
using TrafficSimulation.App.PlayerControl;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Multiplayer.Client;
using TrafficSimulation.Multiplayer.Wire;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Main;

/// <summary>
/// <b>A run that is a player of a served town</b> (MP-1…15): it lays the town the server names and steps none of it, draws
/// what it is sent where the server's clock says it stood, chases the player with a camera of its own, draws its map
/// (MP-12) and the other players' names (MP-15), and offers nothing but the walking and driving keys and the one to get in or out — no menu, no panel, no
/// layer, no selection, no pace (MP-9).
/// </summary>
/// <remarks>
/// <b>The town laid here is the statics and the clock's own pieces</b> — the ground, the buildings, the heads and what
/// they show at an instant (MP-5, MP-6) — a <see cref="TownScene"/> and never a <see cref="TownWorld"/>: no fleet, route
/// network, atlas or world is laid for a town nothing here steps. What moves is the <see cref="Replica"/>'s.
/// </remarks>
internal sealed partial class Game
{
    JoinedTown? _joined;

    /// <summary>What the banner last said of the join, so it is told only when that changes.</summary>
    string _joinSaid = string.Empty;

    /// <summary>The instant the replica was posed at this frame, in the server's seconds, or NaN.</summary>
    double _joinedDrawnS = double.NaN;

    /// <summary>The player's camera, which nothing but its own car moves (MP-9).</summary>
    Chase? _chase;

    /// <summary>
    /// The town at <paramref name="url"/> joined as <paramref name="card"/> says the player is (MP-15): a link asked for,
    /// which the frames that follow open and read.
    /// </summary>
    public void Join(string url, SeatCard card)
    {
        _joined = new JoinedTown(_window.Link, _config, card);
        _chase = new Chase(_config);
        _window.Link.Open(url);
    }

    /// <summary>The town this run is a player of, if it is one.</summary>
    public JoinedTown? Joined => _joined;

    /// <summary>The digest of the plan standing, which a player holds up against the server's (MP-5).</summary>
    public ulong? StandingDigest => _scene?.Plan.Digest();

    /// <summary>
    /// <b>The town a player draws, opened a stage at a time</b> (WEB-8): the plan, its scene (<see cref="TownWorld.LayingTheScene"/>)
    /// and its ground — and stood in place of whatever stood before.
    /// </summary>
    public IEnumerable<string> OpenTheSceneInStages(string map)
    {
        var planning = CityGen.Maps.Planning(map, _config, ShippedArt.Sizes);
        foreach (var stage in planning.Stages) yield return stage;

        var plan = planning.Made;
        var laying = TownWorld.LayingTheScene(plan, _config);
        foreach (var stage in laying.Stages) yield return stage;

        var ground = GroundMesh.Build(plan, _config);
        plan.Paving(_config).LetGoOfTheLayingIndexes();
        yield return OpenWatch.Passed("the ground");
        StandTheScene(plan, ground, laying.Made);
    }

    /// <summary>A player's town put on screen: the renderer laid for its ground with room for an area and not a town (MP-1), and whatever stood before let go of.</summary>
    void StandTheScene(CityGen.CityPlan plan, GroundMesh ground, TownScene scene)
    {
        _map = plan.Name;
        _looks ??= TownSprites.Load(_config);
        _renderer.Dispose();
        LetGoOfTheEngine();
        _movingOnTheDevice = false;
        LayTheRenderer(plan, ground, scene.Uses, player: true);

        _world?.Dispose();
        _world = null;
        _loop = null;
        _scenario = [];
        _scene = scene;
        _ground = ground;
        _camera = new Camera2D(_config, plan.WorldSizeM, _uiPx) { DevicePxPerUiPx = _window.UiScale };
        _camera.LookAt(plan.WorldSizeM * 0.5f);
        _ui.TownChanged(_map, behindTheMenu: false);
        _hands.TownChanged();
        _follow.Stop();

        OpenWatch.Passed("stood: the renderer and the sprites");
        LetGoOfTheTriangles(ground);
        HandBackTheOpen();
    }

    /// <summary>One frame of a player's: what arrived read, the area posed at the instant drawn, and the banner kept.</summary>
    void AdvanceJoined(JoinedTown joined)
    {
        var localS = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        joined.Read(localS);
        _joinedDrawnS = joined.Pose(localS);
        SayTheLatency(joined.Latency, localS);

        var said = joined.Said;
        if (!string.Equals(said, _joinSaid, StringComparison.Ordinal))
        {
            _joinSaid = said;
            Banner(said);
        }
    }

    /// <summary>When the latency was last said, in local seconds, or NaN before the first frame of a join.</summary>
    double _latencySaidS = double.NaN;

    /// <summary>The latency's line, laid once: written into rather than built, so the saying allocates nothing.</summary>
    readonly char[] _latencyLine = new char[192];

    /// <summary>
    /// How long the keys took to come back since it was last said, on the console every <c>LatencyReportS</c> where
    /// anything was timed (<see cref="InputLatency"/>).
    /// </summary>
    void SayTheLatency(InputLatency latency, double localS)
    {
        if (double.IsNaN(_latencySaidS)) _latencySaidS = localS;
        if (localS - _latencySaidS < _config.Multiplayer.LatencyReportS) return;

        _latencySaidS = localS;
        var (roundTrip, toPicture) = latency.Take();
        if (toPicture.Count == 0) return;

        var line = _latencyLine.AsSpan();
        if (!line.TryWrite(
                $"latency, {toPicture.Count} inputs timed: keys to the snapshot answering them {roundTrip.MeanS * 1000d:0} ms " +
                $"({roundTrip.LeastS * 1000d:0}–{roundTrip.MostS * 1000d:0}), to the frame drawing it {toPicture.MeanS * 1000d:0} ms " +
                $"({toPicture.LeastS * 1000d:0}–{toPicture.MostS * 1000d:0})",
                out var written))
            return;

        Console.Out.WriteLine(_latencyLine, 0, written);
    }

    /// <summary>A line on the page's banner, or the console's.</summary>
    partial void Banner(string line);

    /// <summary>
    /// <b>A player's hands</b> (MP-9, MP-12, MP-13): the walking and driving keys sent as held, Shift's boost with them
    /// (CTL-5e), <c>E</c> or <c>Enter</c>
    /// to get in or out, the wheel zooming the camera, the left button dragging it a little off the player, the screen
    /// key, and the map opened whole and shut again by <c>M</c> or a click on it, or shut by <c>Escape</c>. Nothing turns
    /// the camera, and nothing picks or orders anything.
    /// </summary>
    /// <remarks>
    /// A press that lands on the map is the map's, so holding it drags nothing; nor does anything while the whole map is
    /// open over the town.
    /// </remarks>
    void ReadJoinedInput(JoinedTown joined)
    {
        if (_window.TakePress(Silk.NET.Input.Key.F11)) _window.ToggleFullscreen();

        var left = Silk.NET.Input.MouseButton.Left;
        var onTheMap = _window.TakeClick(out var button, out var clickPx) && button == left && _minimap is { } map &&
                       (_wholeMap || map.Corner(_uiPx, default).Holds(clickPx));
        if (_window.TakePress(Silk.NET.Input.Key.M) || onTheMap) _wholeMap = !_wholeMap;
        if (_window.TakePress(Silk.NET.Input.Key.Escape)) _wholeMap = false;

        // Both keys asked every frame, so a press of one is never left waiting behind the other.
        var getIn = _window.TakePress(Silk.NET.Input.Key.E);
        if (_window.TakePress(Silk.NET.Input.Key.Enter) || getIn) joined.Act();

        var (throttle, steer, handbrake) = PlayerHands.Pedals(_window);
        joined.Hold(throttle, steer, handbrake, PlayerHands.Boost(_window));

        var chase = _chase!;
        var scrolled = _window.TakeScroll();
        if (!_wholeMap) chase.Zoom(scrolled);

        var pointerPx = _window.PointerPx;
        var held = _window.IsMouseDown(left);
        _heldOnTheMap = held && (_heldOnTheMap || onTheMap);
        if (held && !_heldOnTheMap && !_wholeMap) chase.Drag(pointerPx - _dragFromPx, _camera.PixelsPerMetre);
        else chase.Release();

        _dragFromPx = pointerPx;
    }

    /// <summary>Where the pointer was last frame, which a drag is measured from.</summary>
    Vector2 _dragFromPx;

    /// <summary>Whether the whole town's map is open over the picture (MP-12).</summary>
    bool _wholeMap;

    /// <summary>Whether the left button held now went down on the map, which keeps it from dragging the camera.</summary>
    bool _heldOnTheMap;

    /// <summary>The camera chasing the player — its body, or what it is in (MP-9); standing where it was while it is drawn as nothing.</summary>
    void FollowThePlayer(JoinedTown joined, float sinceLastFrameS)
    {
        if (joined.Replica is not { } replica || !replica.Own(out var atM, out var velocityMps, out _))
        {
            _chase!.Stop();
            return;
        }

        _chase!.Step(_camera, _uiPx, atM, velocityMps, sinceLastFrameS);
    }

    /// <summary>What moves, from the replica, over the statics of the town laid here — the clock at the instant drawn.</summary>
    (int Over, int Above) FillJoined(TownSprites looks, TownScene scene, Replica replica, Vector2 viewCentreM, Vector2 viewSpanM)
    {
        var ringWords = SpriteRings.Length(replica.People.Capacity, replica.Cars.Capacity, replica.Wagons.Length);
        if (_ownRing.Length < ringWords) _ownRing = new uint[ringWords];
        SpriteRings.MarkOne(_ownRing.AsSpan(0, ringWords), replica.People.Capacity, replica.Cars.Capacity, OwnUnit(replica));

        return looks.FillMoving(
            new MovingScene
            {
                Cars = replica.Cars,
                People = replica.People,
                CarsDrawn = replica.CarsDrawn,
                PeopleDrawn = replica.PeopleDrawn,
                Wagons = replica.Wagons,
                Hitches = replica.Hitches,
                Levelled = scene.Levelled,
                ElapsedS = (float)_joinedDrawnS,
                Heads = scene.Heads,
                Signals = scene.Signals,
                CrossingGear = scene.CrossingGear,
                Rings = new SpriteRings(_ownRing, replica.People.Capacity, replica.Cars.Capacity),
            },
            _config, viewCentreM, viewSpanM, _camera.PixelsPerMetre, _renderer.SpritesOver, _renderer.SpritesAbove);
    }

    /// <summary>The ring words marking the player (MP-9), laid the replica's length the first time.</summary>
    uint[] _ownRing = [];

    /// <summary>What the player is drawn as, as the unit a ring is put round: a walker, a car, or a rail vehicle by its slot.</summary>
    static World.Town.Selection OwnUnit(Replica replica)
    {
        var own = replica.OwnMover;
        var index = replica.Slot(own);
        if (index < 0) return default;

        return replica.Movers.KindOf(own) switch
        {
            MoverKind.Person => new(World.Town.SelectionKind.Person, index),
            MoverKind.Car => new(World.Town.SelectionKind.Car, index),
            _ => new(World.Town.SelectionKind.Wagon, index),
        };
    }

    /// <summary>The player's map (MP-12), laid off the town the first frame one stands.</summary>
    Minimap? _minimap;

    /// <summary>
    /// <b>A player's overlay is its map and the other players' names, and nothing else</b> (MP-9, MP-12, MP-15): each
    /// other player's name over what it is drawn as; then the corner's map centred on the player, or on the camera while it
    /// is drawn as nothing, or the whole town over a dimmed picture — with every player's arrow in its colour, on foot or in
    /// what it drives, the others' named beside them, and its own drawn last so it is never under one.
    /// </summary>
    int DrawTheMap(JoinedTown joined)
    {
        var draw = new ScreenDraw(_renderer.Overlay);
        if (_scene is not { } scene) return 0;

        var map = _minimap ??= new Minimap(scene.Plan, _config);
        var replica = joined.Replica;
        Vector2 ownM = default, ownForward = default;
        var drawn = replica is not null && replica.Own(out ownM, out _, out ownForward);
        var centreM = drawn ? ownM : _camera.CentreM;
        if (replica is not null)
        {
            foreach (var mover in replica.MoversDrawn) NameOver(ref draw, replica, mover);
        }

        if (_wholeMap) draw.Rect(Vector2.Zero, _uiPx, Theme.Shadow);

        var frame = _wholeMap ? map.Whole(_uiPx) : map.Corner(_uiPx, centreM);
        map.Draw(ref draw, frame);
        if (replica is null) return draw.Written;

        foreach (var mover in replica.MoversDrawn) ArrowOf(ref draw, map, frame, replica, mover);

        if (drawn && replica.SeatOf(replica.OwnMover) is var ownSeat and >= 0)
        {
            var own = ColourOf(replica, ownSeat);
            _renderer.RingHue = own;
            map.Arrow(ref draw, frame, centreM, ownForward, own);
        }

        return draw.Written;
    }

    /// <summary>Another player's arrow and name, where a mover drawn this frame is one (MP-12, MP-15); the player's own arrow is drawn last, by its caller.</summary>
    static void ArrowOf(ref ScreenDraw draw, Minimap map, in MapFrame frame, Replica replica, int mover)
    {
        if (mover == replica.OwnMover || replica.SeatOf(mover) is not (var seat and >= 0)) return;
        if (!replica.Drawn(mover, out var atM, out _, out var forward)) return;

        var colour = ColourOf(replica, seat);
        map.Arrow(ref draw, frame, atM, forward, colour);
        if (replica.Card(seat, out var card)) map.Name(ref draw, frame, atM, card.Name, colour);
    }

    /// <summary>
    /// <b>Another player's name over what it is drawn as</b> (MP-15), centred <see cref="MultiplayerFigures.NameLiftM"/>
    /// and <see cref="MultiplayerFigures.NameLiftPx"/> over its middle, in its colour; the player's own is never written.
    /// </summary>
    void NameOver(ref ScreenDraw draw, Replica replica, int mover)
    {
        if (mover == replica.OwnMover || replica.SeatOf(mover) is not (var seat and >= 0) || !replica.Card(seat, out var card)) return;
        if (card.Name.Length == 0 || !replica.Drawn(mover, out var atM, out _, out _)) return;

        var figures = _config.Multiplayer;
        var heightPx = figures.NamePx;
        var widthPx = GlyphSheet.AdvancePx(heightPx) * card.Name.Length;
        var middlePx = _camera.ScreenAt(atM, _uiPx);
        var atPx = middlePx - new Vector2(widthPx * 0.5f, figures.NameLiftM * _camera.PixelsPerMetre + figures.NameLiftPx + heightPx);
        if (atPx.X + widthPx < 0f || atPx.Y + heightPx < 0f || atPx.X > _uiPx.X || atPx.Y > _uiPx.Y) return;

        draw.OutlinedText(atPx, card.Name, heightPx, ColourOf(replica, seat));
    }

    /// <summary>The colour <paramref name="seat"/>'s player is drawn in: the one it chose, or its seat's until the server has said (MP-15).</summary>
    static Vector4 ColourOf(Replica replica, int seat) => replica.Card(seat, out var card) ? Theme.Rgb(card.Rgb) : Theme.Player(seat);

    /// <summary>The tick a player's frame shows the clock's own pieces at (MP-6): the server's, at the instant drawn.</summary>
    long JoinedTick => double.IsNaN(_joinedDrawnS) ? 0 : (long)(_joinedDrawnS * _config.Sim.TickRateHz);
}
