using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.CityGen;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using static TrafficSimulation.App.DeviceTick.DeviceHoldings;
using static TrafficSimulation.Runtime.GpuBuffer;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>The engine's own tables, drawn on the device</b> (<c>kernels/engine_bodies.comp</c>, <c>kernels/engine_ways.comp</c>):
/// every body of the body table, every link, every way of the town's one numbering, every claim the claims table holds and
/// every stretch a holder means to travel — whatever kind each is, since nothing here asks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read where the engine keeps it, and never copied to be drawn</b> (the owner, 2026-10-19). On the device the kernels bind
/// the world's own buffers (<see cref="DeviceWorld.Bodies"/>); where the world steps here, its table kept as the view was
/// written (<see cref="KeptBodies"/>) goes up in the same rows each frame, and the claims and the intents, which only the
/// host holds, go up as the tables hold them. A kind of body, of way or of holder added to the engine is drawn by these
/// with no line written here for it.
/// </para>
/// <para>
/// <b>Recorded once a slot, steered by that slot's words</b> (<see cref="Write"/>): the view, and each layer's workgroups —
/// nought for a layer that is off — so switching one costs a frame nothing but the words. On the desktop a slot is a
/// swapchain image and the kernels ride in its frame; a page runs slot nought in a batch of its own.
/// </para>
/// </remarks>
internal sealed class EngineLayers : IEngineDraw, IDisposable
{
    /// <summary>How many quads each of the three runs holds; what a frame reserves past it is not drawn.</summary>
    public const int Room = 65536;

    /// <summary>
    /// Where the kinds of way start among <see cref="Theme.KindColour"/>'s colours: past every collision layer's, a byte's
    /// worth (<see cref="BodyRows.LayerByte"/>), so a body and a way are never one colour.
    /// </summary>
    public const int WayKindColourFrom = 8;

    /// <summary>The frame as the kernels read it (<c>engine.glsl</c>'s <c>Frame</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct FrameRow(
        Vector2 ViewCentreM, Vector2 ViewSpanM, Vector2 PointerM, Vector2 SolidUv, float HairlineM, float WayLineM,
        float AgentLineM, float PitchM, uint ViewSlot, uint BodyCount, uint LinkCount, uint ArcCount, uint ClaimCount,
        uint IntentCount, uint RunRoom, uint GroundChannels, uint PinnedPlusOne);

    /// <summary>A dispatch's workgroups, as an indirect dispatch reads them.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Groups3(uint X, uint Y = 1, uint Z = 1);

    /// <summary>A slot's words: its frame, then each pass's workgroups in the order they are recorded.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct FrameWords(FrameRow Frame, Groups3 Shapes, Groups3 Links, Groups3 Pointed, Groups3 Arcs, Groups3 Claims, Groups3 Intents);

    /// <summary>
    /// <b>One body named, as the table holds it</b> (<c>engine_bodies.comp</c>'s <c>Picked</c>): plus one, nought for none, and
    /// its row — what it is, its filter, where it stands and how it is turned, its shape, and its motion.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct PickRow(uint BodyPlusOne, int Tag, uint Filter, uint Pad, Vector4 Pose, Vector4 Shape, Vector4 Motion)
    {
        public bool Any => BodyPlusOne > 0;

        public int Body => (int)BodyPlusOne - 1;

        public BodyTag What => BodyTag.Unpack(Tag);

        public Vector2 PositionM => new(Pose.X, Pose.Y);

        public float HeadingRad => MathF.Atan2(Pose.W, Pose.Z);

        /// <summary>The core's half-size, round which <see cref="CornerRadiusM"/> is rolled; nought for a disc.</summary>
        public Vector2 ExtentM => new(Shape.X, Shape.Y);

        public float CornerRadiusM => Shape.Z;

        public Vector2 VelocityMps => new(Motion.X, Motion.Y);
    }

    /// <summary>What a frame's table found: the body under the pointer, and the body pinned.</summary>
    internal readonly record struct Picks(PickRow Pointed, PickRow Pinned);

    /// <summary>One way (<c>engine_ways.comp</c>'s <c>Way</c>): where its arcs begin, how many, its width, its kind and its channels.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct WayRow(uint FirstArc, uint ArcCount, float WidthM, uint KindAndChannels);

    /// <summary>One arc of a way (<c>Arc</c>): where it starts and which way, its length and bend, where on its way it starts, and its way.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ArcRow(Vector2 StartM, Vector2 StartUnit, float LengthM, float Curvature, float FromM, uint Way);

    /// <summary>The words each pass's workgroups stand at, in bytes from a slot's words.</summary>
    static readonly ulong[] PassAt = PassOffsets();

    const int Shapes = 0, Links = 1, Pointed = 2, Arcs = 3, Claims = 4, Intents = 5;

    readonly Vk _vk;
    readonly DeviceHoldings _held;
    readonly TownWorld _town;
    readonly DeviceWorld? _device;
    readonly ManagedWorld? _managed;
    readonly int _bodyCount, _wayCount, _arcCount;
    readonly ComputeKernel _bodies, _ways;
    readonly GpuBuffer _frame, _figures, _quads, _counts, _draws, _pick, _wayRows, _arcs;
    readonly WayIntents _intents = new();

    /// <summary>What each slot reads and writes of its own: its words, the managed world's rows, the claims and intents as sent, and its pick.</summary>
    readonly List<Slot> _slots = [];

    sealed class Slot
    {
        public required GpuBuffer Words, PickOut, Claims, Intents;
        public GpuBuffer? Poses, Shapes, Filters, Motion, Tags, Links;
        public BoundSet BodiesSet, WaysSet;
        public bool StaticsSent;
    }

    /// <summary>How many claims and intents a slot's buffers hold; grown, and the frames written down again, when a frame outruns them.</summary>
    int _claimRoom, _intentRoom;

    GpuBuffer? _noPoses;

    readonly int _slotCount;

    /// <param name="slotCount">
    /// How many slots will be named — a swapchain's images, one for a page. <b>Each slot binds a set of each kernel</b>, and a
    /// kernel's pool holds only the sets it was laid for.
    /// </param>
    public EngineLayers(Vk vk, TownWorld town, int slotCount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(slotCount, 1);
        _slotCount = slotCount;
        _vk = vk;
        _held = new DeviceHoldings(vk);
        _town = town;
        _device = town.World as DeviceWorld;
        _managed = town.World as ManagedWorld;
        town.World.KeepTheBodiesAtTheView();
        _bodyCount = _device?.Bodies.Count ?? town.PhysicsForInstruments?.Table.Count ?? 0;

        _frame = _held.Device<FrameRow>(1);
        _figures = _held.Laid<uint>(Figures());
        _quads = _held.Device<OverlayQuad>(3 * Room, BufferUse.Vertex);
        _counts = _held.Device<uint>(4);
        _draws = _held.Device<uint>(12, BufferUse.Indirect);
        _pick = _held.Device<PickRow>(2);

        (var ways, var arcs) = TheWays(town);
        (_wayCount, _arcCount) = (ways.Length, arcs.Length);
        _wayRows = _held.Laid<WayRow>(ways);
        _arcs = _held.Laid<ArcRow>(arcs);
        _claimRoom = Math.Max(1024, town.Occupancy.HeldClaims([]) * 2);
        _intentRoom = Math.Max(256, (town.Cars.Capacity + town.People.Capacity + town.Trains.Count) * WayIntents.MostEach);

        _bodies = _held.Kernel("engine_bodies.comp", 13, sizeof(uint), slotCount);
        _ways = _held.Kernel("engine_ways.comp", 8, sizeof(uint), slotCount);
    }

    public GpuBuffer Quads => _quads;

    public GpuBuffer Draws => _draws;

    public int RunRoom => Room;

    /// <summary>
    /// <b>Whether a frame outran the claims or intents its slots hold</b> and they were grown: every slot's frame is to be
    /// written down again before it reads them (<c>TownRenderer.DrawTheEngineFrom</c>).
    /// </summary>
    public bool Grown { get; private set; }

    public void Recorded() => Grown = false;

    /// <summary>
    /// <b>Slot <paramref name="slot"/>'s frame</b>: the view and what each layer that is on draws of it, and — where the world
    /// steps here — the rows the kernels read, written before the frame is submitted.
    /// </summary>
    public void Write(int slot, in EngineView view, DebugSwitches switches)
    {
        var at = For(slot);
        var bodyCount = 0;
        var linkCount = 0;
        var viewSlot = 0;
        if (_device is { } device && device.PosesKept)
        {
            var table = device.Bodies;
            (bodyCount, linkCount, viewSlot) = (table.Count, table.LinkCount, device.View.Slot);
        }
        else if ((switches.Collision || view.Picks || view.PinnedBody >= 0) && _managed?.BodiesAtTheView is { } kept)
        {
            SendTheBodies(at, kept);
            (bodyCount, linkCount) = (_bodyCount, kept.LinkCount);
        }

        var claimCount = switches.Claims ? SendTheClaims(at) : 0;
        var intentCount = switches.AgentLines ? SendTheIntents(at) : 0;
        var pixelsPerMetre = view.PixelsPerMetre;
        var bodies = switches.Collision ? bodyCount : 0;
        at.Words.Span<FrameWords>()[0] = new FrameWords(
            new FrameRow(
                view.CentreM, view.SpanM, view.PointerM, GlyphSheet.SolidUv, MathF.Max(HairlineM, HairlineFloorPx / pixelsPerMetre),
                MathF.Max(HairlineM, WayLineFloorPx / pixelsPerMetre), MathF.Max(PathMarks.PathLineM, AgentLineFloorPx / pixelsPerMetre),
                ChevronPitchPx / pixelsPerMetre, (uint)viewSlot, (uint)bodyCount, (uint)linkCount, (uint)_arcCount, (uint)claimCount,
                (uint)intentCount, Room, CityPlan.RoadArrays.GroundChannel, (uint)(view.PinnedBody + 1)),
            GroupsOf(bodies), GroupsOf(switches.Collision ? linkCount : 0), GroupsOf(view.Picks ? bodyCount : 0),
            GroupsOf(switches.Nodes ? _arcCount : 0), GroupsOf(claimCount), GroupsOf(intentCount));
    }

    /// <summary>What slot <paramref name="slot"/>'s last frame found under the pointer, once that frame is done.</summary>
    public Picks PickedIn(int slot)
    {
        if (slot >= _slots.Count) return default;

        var rows = _slots[slot].PickOut.Span<PickRow>();
        return new Picks(rows[0], rows[1]);
    }

    public void Record(Recording commands, int slot)
    {
        var at = For(slot);
        var vk = _vk;
        vk.AfterTheSubmitsBefore(commands);
        vk.Copy(commands, at.Words, _frame, Bytes<FrameRow>(1));
        vk.Fill(commands, _counts, 0);
        vk.Fill(commands, _pick, 0);
        vk.AfterTransfer(commands);

        Pass(commands, _bodies, at.BodiesSet, at.Words, Shapes, 0u);
        Pass(commands, _bodies, at.BodiesSet, at.Words, Links, 1u);
        Pass(commands, _bodies, at.BodiesSet, at.Words, Pointed, 2u);
        Pass(commands, _ways, at.WaysSet, at.Words, Arcs, 0u);
        Pass(commands, _ways, at.WaysSet, at.Words, Claims, 1u);
        Pass(commands, _ways, at.WaysSet, at.Words, Intents, 2u);
        vk.BetweenKernels(commands);
        _bodies.Dispatch(commands, at.BodiesSet, 1, 3u);
        vk.BeforeTransfer(commands);
        vk.Copy(commands, _pick, at.PickOut, Bytes<PickRow>(2));
    }

    static void Pass(Recording commands, ComputeKernel kernel, BoundSet set, GpuBuffer words, int pass, uint mode) =>
        kernel.DispatchIndirect(commands, set, words, PassAt[pass], mode);

    static Groups3 GroupsOf(int count) => new(count > 0 ? DeviceHoldings.Groups(count) : 0u);

    /// <summary>A slot's own buffers and sets, laid the first time it is named.</summary>
    Slot For(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, _slotCount);
        while (_slots.Count <= slot) _slots.Add(Lay());
        return _slots[slot];
    }

    Slot Lay()
    {
        var at = new Slot
        {
            Words = Streamed<FrameWords>(1, BufferUse.Indirect),
            PickOut = _held.Readback<PickRow>(2),
            Claims = Streamed<HeldClaim>(_claimRoom),
            Intents = Streamed<WayIntent>(_intentRoom),
        };
        at.Words.Span<FrameWords>()[0] = default;
        at.PickOut.Span<PickRow>().Clear();
        Bind(at);
        return at;
    }

    void Bind(Slot at)
    {
        GpuBuffer pose0, pose1, shape, filter, motion, tags, links;
        if (_device is { } device)
        {
            var bodies = device.Bodies;
            (pose0, pose1) = (bodies.PosesAtView[0], bodies.PosesAtView[1]);
            (shape, filter, motion, tags, links) = (bodies.Shape, bodies.Filter, bodies.Motion, bodies.Tags, bodies.Links);
        }
        else
        {
            // One slot's poses for both of the view's, which the frame names as the first: a page refuses one buffer bound
            // twice, so the second is a row of nothing.
            var linkRoom = Math.Max(1, _town.PhysicsForInstruments?.LinkSlotCount ?? 0);
            pose0 = at.Poses ??= Streamed<Vector4>(_bodyCount);
            pose1 = _noPoses ??= _held.Device<Vector4>(1);
            shape = at.Shapes ??= Streamed<Vector4>(_bodyCount);
            filter = at.Filters ??= Streamed<uint>(_bodyCount);
            motion = at.Motion ??= Streamed<Vector4>(_bodyCount);
            tags = at.Tags ??= Streamed<int>(_bodyCount);
            links = at.Links ??= Streamed<DeviceSolver.LinkGpuRow>(linkRoom);
        }

        at.BodiesSet = _bodies.Bind([_frame, _figures, pose0, pose1, shape, filter, tags, links, motion, _quads, _counts, _draws, _pick], at.BodiesSet);
        at.WaysSet = _ways.Bind([_frame, _figures, _wayRows, _arcs, at.Claims, at.Intents, _quads, _counts], at.WaysSet);
    }

    GpuBuffer Streamed<T>(int count, BufferUse also = 0) where T : unmanaged =>
        _held.Own(_vk.CreateBuffer(Bytes<T>(count), BufferUse.Storage | BufferUse.TransferSrc | also, HostAccess.Streams));

    /// <summary>The managed world's table as its view was written, into the rows the kernels read: the furniture once a slot.</summary>
    void SendTheBodies(Slot at, KeptBodies kept)
    {
        var from = at.StaticsSent ? kept.FurnitureCount : 0;
        kept.Pose.AsSpan(from).CopyTo(at.Poses!.Span<Vector4>()[from..]);
        kept.Shape.AsSpan(from).CopyTo(at.Shapes!.Span<Vector4>()[from..]);
        kept.Filter.AsSpan(from).CopyTo(at.Filters!.Span<uint>()[from..]);
        kept.Motion.AsSpan(from).CopyTo(at.Motion!.Span<Vector4>()[from..]);
        if (!at.StaticsSent) kept.Tag.CopyTo(at.Tags!.Span<int>());

        var links = at.Links!.Span<DeviceSolver.LinkGpuRow>();
        for (var link = 0; link < kept.LinkCount && link < links.Length; link++) links[link] = DeviceSolver.LinkGpuRow.Of(kept.Links[link]);
        at.StaticsSent = true;
    }

    int SendTheClaims(Slot at)
    {
        var count = _town.Occupancy.HeldClaims(at.Claims.Span<HeldClaim>()[.._claimRoom]);
        if (count <= _claimRoom) return count;

        _claimRoom = count * 2;
        GrowEach(slot => slot.Claims = Streamed<HeldClaim>(_claimRoom), slot => slot.Claims);
        return _town.Occupancy.HeldClaims(at.Claims.Span<HeldClaim>()[.._claimRoom]);
    }

    int SendTheIntents(Slot at)
    {
        _town.StateIntents(_intents);
        if (_intents.Count > _intentRoom)
        {
            _intentRoom = _intents.Count * 2;
            GrowEach(slot => slot.Intents = Streamed<WayIntent>(_intentRoom), slot => slot.Intents);
        }

        _intents.Rows.CopyTo(at.Intents.Span<WayIntent>());
        return _intents.Count;
    }

    /// <summary>One of every slot's buffers let go of and laid again larger, its sets bound over it, and the frames owed again.</summary>
    void GrowEach(Action<Slot> lay, Func<Slot, GpuBuffer> buffer)
    {
        _vk.WaitIdle();
        foreach (var slot in _slots)
        {
            _held.Release(buffer(slot));
            lay(slot);
            Bind(slot);
        }

        Grown = true;
    }

    /// <summary>
    /// <b>Every way of the town in its one numbering</b>, each down the line it is travelled on (<see cref="TownWorld.LineOfWay"/>)
    /// at its width, kind and level, and its arcs in order with where along it each starts — laid once.
    /// </summary>
    static (WayRow[] Ways, ArcRow[] Arcs) TheWays(TownWorld town)
    {
        var ways = town.Ways;
        var rows = new WayRow[Math.Max(1, ways.Count)];
        var arcCount = 0;
        for (var way = 0; way < ways.Count; way++) arcCount += town.LineOfWay(way, out _).Length;

        var arcs = new ArcRow[Math.Max(1, arcCount)];
        var next = 0;
        for (var way = 0; way < ways.Count; way++)
        {
            var line = town.LineOfWay(way, out var widthM);
            rows[way] = new WayRow((uint)next, (uint)line.Length, widthM, (uint)ways.KindOf(way) | ((uint)town.ChannelsOfWay(way) << 8));
            var alongM = 0f;
            foreach (ref readonly var arc in line)
            {
                arcs[next++] = new ArcRow(arc.StartM, arc.StartUnit, arc.LengthM, arc.Curvature, alongM, (uint)way);
                alongM += arc.LengthM;
            }
        }

        return (rows, arcs);
    }

    /// <summary>
    /// <b>What is fixed when the layers are laid</b> (<c>engine.glsl</c>'s <c>Figures</c>): a colour each collision layer and
    /// each kind of way off its own number (<see cref="Theme.KindColour"/>), the holders' hues, a wash each rung of the
    /// claims' ladder, and the few the layers share.
    /// </summary>
    static uint[] Figures()
    {
        const int wayKinds = 8, hues = 12, rungs = 16;
        var words = new List<uint>(160);
        for (var layer = 0; layer < WayKindColourFrom; layer++) Colour(Theme.KindColour(layer));
        for (var kind = 0; kind < wayKinds; kind++) Colour(Theme.KindColour(WayKindColourFrom + kind));
        for (var hue = 0; hue < hues; hue++) Colour(Theme.AgentLine(hue));
        for (var rung = 0; rung < rungs; rung++) words.Add(BitConverter.SingleToUInt32Bits(DebugOverlay.Wash((ClaimPriority)rung).W));
        Colour(Theme.HeldLine);
        Colour(Theme.LaneObstruction);
        Colour(Theme.Casing);
        words.AddRange([(uint)BodyFlags.Enabled, (uint)BodyFlags.Static, LaneOccupancy.RosterBits, BitConverter.SingleToUInt32Bits(DebugOverlay.EdgeOverWash)]);
        return [.. words];

        void Colour(Vector4 colour)
        {
            foreach (var channel in (ReadOnlySpan<float>)[colour.X, colour.Y, colour.Z, colour.W]) words.Add(BitConverter.SingleToUInt32Bits(channel));
        }
    }

    static ulong[] PassOffsets()
    {
        var frame = (ulong)Marshal.SizeOf<FrameRow>();
        var each = (ulong)Marshal.SizeOf<Groups3>();
        return [frame, frame + each, frame + (2 * each), frame + (3 * each), frame + (4 * each), frame + (5 * each)];
    }

    /// <summary>The least a shape's edge (<see cref="DebugOverlay.HairlineM"/>), a way and an agent's line are on the glass.</summary>
    const float HairlineFloorPx = DebugOverlay.HairlineFloorPx;

    const float HairlineM = DebugOverlay.HairlineM;
    const float WayLineFloorPx = DebugOverlay.HairlineFloorPx;
    const float AgentLineFloorPx = DebugOverlay.HairlineFloorPx;

    /// <summary>How long an arc must be on the glass before a chevron says which way it runs, and four times a chevron's size.</summary>
    const float ChevronPitchPx = 48f;

    public void Dispose() => _held.Dispose();
}

/// <summary>
/// What the engine layers draw a frame for: the view, the pointer and whether a body under it is asked for, and the body
/// pinned (−1 for none), whose row is read out with it.
/// </summary>
internal readonly record struct EngineView(
    Vector2 CentreM, Vector2 SpanM, float PixelsPerMetre, Vector2 PointerM, bool Picks, int PinnedBody = -1);
