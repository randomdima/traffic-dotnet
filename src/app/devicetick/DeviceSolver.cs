using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Physics;
using Vk = TrafficSimulation.Runtime.Vk;
using static TrafficSimulation.Runtime.GpuBuffer;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The solver's step on the device</b>: the body table laid into storage buffers, the furniture's grid filed once,
/// and a step recorded as the CPU's own passes — the moving grid, the pairs each owner gathers, the manifolds, the
/// contacts packed in the CPU's order, damping, the coloured solve and the integration (<c>world/physics/kernels/</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Run in shadow</b> (<see cref="Lay"/>): every step is handed the CPU's table as it stands before the CPU's own
/// step, so the two are asked the same question and can be compared. What crosses is the moving rows, never the
/// furniture's, and one submit a step.
/// </para>
/// <para>
/// <b>Or in the town's place</b> (<see cref="LayWhole"/>, <see cref="LayRows"/>): the table laid once, and after that
/// only the rows the town changed, each written by a kernel (<c>kernels/laid_bodies.comp</c>) — and every moving body
/// an owner whether or not it is in the world, which the kernels pass over, so a body going into a container changes
/// no roster. What the step found is not handed back: what froze, woke and began touching is
/// (<see cref="Settles"/>).
/// </para>
/// <para>
/// <b>A step is also recorded into another's submit</b> (<see cref="Lay"/>, <see cref="Send"/>, <see cref="Record"/>),
/// between kernels that read and write the same table (<see cref="Bodies"/>) — the bodies' phase
/// (<see cref="DeviceBodyPhase"/>), which spends the tick's impulses on it first and reads the cars off it after.
/// </para>
/// <para>
/// <b>Every enabled moving body owns its pairs every step, frozen or not</b> (SOL-37): on the CPU freezing saves the
/// broad phase and changes no answer, since a standing body is held at exactly nothing. In the town's place the step
/// still works out what the CPU's would have frozen and woken (<see cref="Settles"/>), keeping the rest counts itself.
/// </para>
/// <para>
/// <b>The grids' window is the town's at load</b>, capped as <see cref="CellGrid"/> caps one and with the same rim:
/// a body past it is filed in the edge cell, so the pairs are the same and only what they cost moves.
/// </para>
/// </remarks>
internal sealed class DeviceSolver : IDisposable
{
    /// <summary>The push constants every kernel of the step reads (<c>kernels/step.glsl</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct StepPush
    {
        public int WindowFromX;
        public int WindowFromY;
        public int WindowWidth;
        public int WindowHeight;
        public float PerCellM;
        public float DtS;
        public float SpeculativeM;
        public float Friction;
        public uint RosterCount;
        public uint Mode;
        public uint Capacity;
        public uint EnabledBit;
        public float LinearKeep;
        public float AngularKeep;
        public float AllowedPenetrationM;
        public float ContactBias;
        public float MaxPushOutMps;
        public float InverseStep;
        public uint VelocityIterations;
        public uint PositionIterations;
        public uint StaticBit;
        public uint FiledFrozenBit;
        public uint FrozenBit;
        public uint TicksToFreeze;
        public float RestSpeedMpsSquared;
        public uint AfreshCount;
        public uint BegunRoom;
        public uint Pad0;
    }

    /// <summary>One body's row as the host lays it (<c>LaidBody</c>, <c>kernels/laid_bodies.comp</c>): every field the step reads.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct LaidBodyRow(
        Vector4 Pose, Vector4 Motion, Vector4 Shape, Vector4 Bounds, UInt4 Filter, Vector2 Mass, float HeadingRad, int Body);

    /// <summary>Where the step keeps its running totals (<c>kernels/step.glsl</c>).</summary>
    internal static class Totals
    {
        public const int CandidatesAt = 0;
        public const int ContactsAt = 1;
        public const int OverflowAt = 2;
        public const int CandidateGroupsAt = 4;
        public const int ContactGroupsAt = 8;
        public const int ColoursAt = 12;
        public const int LeftOverAt = 13;
        public const int MovingCellsAt = 14;
        public const int StaticCellsAt = 15;
        public const int WokenAt = 16;
        public const int UnsettledAt = 17;
        public const int WokeCountAt = 18;
        public const int FrozeCountAt = 19;
        public const int BegunCountAt = 20;
        public const int Count = 21;
    }

    /// <summary>The stamps a step's device time is read between.</summary>
    internal enum Stage
    {
        Began,
        Grid,
        Pairs,
        Manifolds,
        Prepared,
        Solved,
        Integrated,
        Settled,
    }

    readonly Vk _vk;
    readonly SimConfig _config;
    readonly ScanKernels _scans;
    readonly GridWindow _window;
    readonly int _bodies;
    readonly int[] _moving;
    readonly int _firstMoving;
    readonly int _movingRows;
    readonly int _movingItems;
    readonly int _staticItems;
    readonly int _statics;

    readonly List<GpuBuffer> _owned = [];
    readonly List<ComputeKernel> _kernels = [];

    // The body table, one buffer a field.
    readonly GpuBuffer _pose;
    readonly GpuBuffer _heading;
    readonly GpuBuffer _motion;
    readonly GpuBuffer _push;
    readonly GpuBuffer _mass;
    readonly GpuBuffer _shape;
    readonly GpuBuffer _bounds;
    readonly GpuBuffer _filter;
    readonly GpuBuffer _owners;
    readonly GpuBuffer _overlap;

    // What the step settled (kernels/settle.comp).
    readonly GpuBuffer _moved;
    readonly GpuBuffer _restTicks;
    readonly GpuBuffer _restIn;
    readonly GpuBuffer _settling;
    readonly GpuBuffer _afresh;
    readonly GpuBuffer _settledOut;
    readonly GpuBuffer _wokeAt;
    readonly GpuBuffer _frozeAt;

    // What was touching and what began (kernels/touches.comp): the last step's pairs and this one's, swapped each step.
    readonly GpuBuffer[] _touched;
    readonly GpuBuffer[] _touchedCount;
    readonly GpuBuffer _begunGroups;
    readonly GpuBuffer _begunOut;
    GpuBuffer _touchedIn;

    // What the host writes them from.
    readonly GpuBuffer _poseIn;
    readonly GpuBuffer _headingIn;
    readonly GpuBuffer _motionIn;
    readonly GpuBuffer _massIn;
    readonly GpuBuffer _shapeIn;
    readonly GpuBuffer _boundsIn;
    readonly GpuBuffer _filterIn;
    readonly GpuBuffer _ownersIn;

    // The grids.
    readonly GpuBuffer _movingCounts;
    readonly GpuBuffer _movingStarts;
    readonly GpuBuffer _movingCursor;
    readonly GpuBuffer _movingItemsBuffer;
    readonly GpuBuffer _staticCounts;
    readonly GpuBuffer _staticStarts;

    // The pairs and the contacts.
    readonly GpuBuffer _candidateCounts;
    readonly GpuBuffer _candidateStarts;
    readonly GpuBuffer _candidates;
    readonly GpuBuffer _met;
    readonly GpuBuffer _contactStarts;
    readonly GpuBuffer _manifoldA;
    readonly GpuBuffer _manifoldB;
    readonly GpuBuffer _contact;
    readonly GpuBuffer _contactNormal;
    readonly GpuBuffer _contactPoints;
    readonly GpuBuffer _arms;
    readonly GpuBuffer _pointMass;
    readonly GpuBuffer _impulses;
    readonly GpuBuffer _lowest;
    readonly GpuBuffer _order;
    readonly GpuBuffer _totals;
    readonly GpuBuffer _blockSums;

    // What is read back.
    readonly GpuBuffer _poseOut;
    readonly GpuBuffer _headingOut;
    readonly GpuBuffer _motionOut;
    readonly GpuBuffer _overlapOut;
    readonly GpuBuffer _totalsOut;
    readonly GpuBuffer _contactOut;
    readonly GpuBuffer _contactNormalOut;
    readonly GpuBuffer _contactPointsOut;

    readonly ComputeKernel _gridCount;
    readonly ComputeKernel _gridFill;
    readonly ComputeKernel _pairs;
    readonly ComputeKernel _narrow;
    readonly ComputeKernel _groups;
    readonly ComputeKernel _compact;
    readonly ComputeKernel _damp;
    readonly ComputeKernel _prepare;
    readonly ComputeKernel _solve;
    readonly ComputeKernel _integrate;
    readonly ComputeKernel _laidBodies;
    readonly ComputeKernel _settle;
    readonly ComputeKernel _settleSpread;
    readonly ComputeKernel _touches;

    readonly DescriptorSet _movingCountSet;
    readonly DescriptorSet _movingFillSet;
    readonly DescriptorSet _pairsSet;
    readonly DescriptorSet _narrowSet;
    readonly DescriptorSet _groupsSet;
    readonly DescriptorSet _compactSet;
    readonly DescriptorSet _dampSet;
    readonly DescriptorSet _prepareSet;
    readonly DescriptorSet _solveSet;
    readonly DescriptorSet _integrateSet;
    readonly DescriptorSet _laidBodiesSet;
    readonly DescriptorSet _settleSet;
    readonly DescriptorSet _settleSpreadSet;

    /// <summary>The touches' sets, one a way round: the step reading the pairs in <c>_touched[n]</c> uses set n.</summary>
    readonly DescriptorSet[] _touchesSets;

    /// <summary>The rows the town changed, as the host lays them and the laid-rows kernel reads them, straight from mapped memory.</summary>
    readonly GpuBuffer _laidRows;

    int _laidRowCount;

    readonly PrefixScan _movingScan;
    readonly PrefixScan _candidateScan;
    readonly PrefixScan _contactScan;
    readonly PrefixScan _wokeScan;
    readonly PrefixScan _frozeScan;
    readonly PrefixScan _begunScan;

    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;

    /// <summary>Each field's staging buffer and the device's, in one list so a step walks it without an iterator.</summary>
    readonly (GpuBuffer From, GpuBuffer To)[] _uploads;

    /// <summary>Whether the moving rows were laid whole since the last send.</summary>
    bool _wholeLaid;

    /// <summary>Whether the roster of owners was laid since the last send.</summary>
    bool _ownersLaid;

    /// <summary>Whether the rest counts were laid whole since the last send (<see cref="LayTheRestCounts"/>).</summary>
    bool _restLaid;

    int _afreshCount;

    /// <summary>The roster the last settling was worked over, which says where its frozen list begins.</summary>
    int _settledOver;

    /// <summary>Which of <c>_touched</c> holds the pairs the next step reads as touching before it.</summary>
    int _touchedWas;

    /// <summary>How many of the table's touching pairs were laid since the last send (<see cref="LayTheTouching"/>), or none.</summary>
    int _touchingLaid = -1;

    /// <param name="candidatesPerOwner">How many candidates a step may hold per moving body before it is full.</param>
    /// <param name="room">
    /// How many bodies the table may come to hold (<see cref="PhysicsWorld.RoomForBodies"/>), every one past what it holds
    /// now a moving body — or none, for a table laid as it stands.
    /// </param>
    public DeviceSolver(Vk vk, SimConfig config, BodyTable table, int candidatesPerOwner = 48, int room = 0)
    {
        _vk = vk;
        _config = config;
        _bodies = Math.Max(table.Count, room);
        _scans = new ScanKernels(vk);
        var level = config.Grid.Main;

        var moving = new List<int>();
        for (var body = 0; body < _bodies; body++)
        {
            if (body >= table.Count || (table.Flags[body] & BodyFlags.Static) == 0) moving.Add(body);
        }

        _moving = [.. moving];
        _statics = table.Statics.Length;
        _firstMoving = _moving.Length == 0 ? 0 : _moving[0];
        _movingRows = _moving.Length == 0 ? 0 : _moving[^1] - _firstMoving + 1;
        _window = TownWindow(level, table);
        _movingItems = Math.Max(1, MostCells(level, table, _moving));
        _staticItems = Math.Max(1, CellsCovered(table, table.Statics));
        CandidateCapacity = Math.Max(1024, _moving.Length * candidatesPerOwner);
        var cells = _window.Count;

        _pose = Device<Vector4>(_bodies);
        _heading = Device<float>(_bodies);
        _motion = Device<Vector4>(_bodies);
        _push = Device<Vector4>(_bodies);
        _mass = Device<Vector2>(_bodies);
        _shape = Device<Vector4>(_bodies);
        _bounds = Device<Vector4>(_bodies);
        _filter = Device<UInt4>(_bodies);
        _owners = Device<int>(Math.Max(1, _moving.Length));
        _overlap = Device<uint>(_bodies);
        _moved = Device<uint>(_bodies);
        _restTicks = Device<uint>(_bodies);
        _restIn = Staging<uint>(_bodies);
        _settling = Device<uint>(_bodies);
        _afresh = Own(_vk.CreateBuffer(Bytes<int>(Math.Max(1, _moving.Length)), BufferUsageFlags.StorageBufferBit, HostAccess.Writes));
        _settledOut = Readback<uint>(2 + (2 * _moving.Length));
        _wokeAt = Device<uint>(Math.Max(1, _moving.Length));
        _frozeAt = Device<uint>(Math.Max(1, _moving.Length));
        _touched = [Device<UInt2>(CandidateCapacity), Device<UInt2>(CandidateCapacity)];
        _touchedCount = [Device<uint>(1), Device<uint>(1)];
        _begunGroups = Device<uint>(BegunGroups);
        BegunRoom = Math.Max(1024, _moving.Length);
        _begunOut = Readback<UInt4>(BegunRoom);
        _touchedIn = Staging<UInt2>(Math.Max(1, _moving.Length));

        _poseIn = Staging<Vector4>(_bodies);
        _headingIn = Staging<float>(_bodies);
        _motionIn = Staging<Vector4>(_bodies);
        _massIn = Staging<Vector2>(_bodies);
        _shapeIn = Staging<Vector4>(_bodies);
        _boundsIn = Staging<Vector4>(_bodies);
        _filterIn = Staging<UInt4>(_bodies);
        _ownersIn = Staging<int>(Math.Max(1, _moving.Length));

        _movingCounts = Device<uint>(cells);
        _movingStarts = Device<uint>(cells);
        _movingCursor = Device<uint>(cells);
        _movingItemsBuffer = Device<int>(_movingItems);
        _staticCounts = Device<uint>(cells);
        _staticStarts = Device<uint>(cells);
        var staticCursor = Device<uint>(cells);
        var staticItems = Device<int>(_staticItems);
        var statics = Device<int>(Math.Max(1, _statics));

        _candidateCounts = Device<uint>(Math.Max(1, _moving.Length));
        _candidateStarts = Device<uint>(Math.Max(1, _moving.Length));
        _candidates = Device<UInt2>(CandidateCapacity);
        _met = Device<uint>(CandidateCapacity);
        _contactStarts = Device<uint>(CandidateCapacity);
        _manifoldA = Device<Vector4>(CandidateCapacity);
        _manifoldB = Device<Vector4>(CandidateCapacity);
        _contact = Device<UInt4>(CandidateCapacity);
        _contactNormal = Device<Vector4>(CandidateCapacity);
        _contactPoints = Device<Vector4>(CandidateCapacity);
        _arms = Device<Vector4>(CandidateCapacity * 2);
        _pointMass = Device<Vector2>(CandidateCapacity * 2);
        _impulses = Device<Vector4>(CandidateCapacity * 2);
        _lowest = Device<uint>(_bodies);
        _order = Device<uint>(CandidateCapacity);
        _totals = Device<uint>(Totals.Count, BufferUsageFlags.IndirectBufferBit);
        _blockSums = Device<uint>(ScanKernels.Block);

        _poseOut = Readback<Vector4>(_bodies);
        _headingOut = Readback<float>(_bodies);
        _motionOut = Readback<Vector4>(_bodies);
        _overlapOut = Readback<uint>(_bodies);
        _totalsOut = Readback<uint>(Totals.Count);
        _contactOut = Readback<UInt4>(CandidateCapacity);
        _contactNormalOut = Readback<Vector4>(CandidateCapacity);
        _contactPointsOut = Readback<Vector4>(CandidateCapacity);
        _laidRows = Own(_vk.CreateBuffer(
            Bytes<LaidBodyRow>(_moving.Length), BufferUsageFlags.StorageBufferBit, HostAccess.Writes));

        _gridCount = Kernel("grid_count.comp", 3, sets: 2);
        _gridFill = Kernel("grid_fill.comp", 5, sets: 2);
        _pairs = Kernel("pairs.comp", 13);
        _narrow = Kernel("narrow.comp", 8);
        _groups = Kernel("groups.comp", 1);
        _compact = Kernel("compact.comp", 11);
        _damp = Kernel("damp.comp", 2);
        _prepare = Kernel("prepare.comp", 9);
        _solve = Kernel("solve.comp", 13);
        _integrate = Kernel("integrate.comp", 9);
        _laidBodies = Kernel("laid_bodies.comp", 8);
        _settle = Kernel("settle.comp", 14);
        _settleSpread = Kernel("settle_spread.comp", 14);
        _touches = Kernel("touches.comp", 10, sets: 2);

        _movingCountSet = _gridCount.Bind([_owners, _bounds, _movingCounts]);
        _movingFillSet = _gridFill.Bind([_owners, _bounds, _movingStarts, _movingCursor, _movingItemsBuffer]);
        var staticCountSet = _gridCount.Bind([statics, _bounds, _staticCounts]);
        var staticFillSet = _gridFill.Bind([statics, _bounds, _staticStarts, staticCursor, staticItems]);
        _pairsSet = _pairs.Bind(
        [
            _owners, _bounds, _filter, _movingCounts, _movingStarts, _movingItemsBuffer, _staticCounts, _staticStarts,
            staticItems, _candidateCounts, _candidateStarts, _candidates, _totals,
        ]);
        _narrowSet = _narrow.Bind([_candidates, _pose, _shape, _met, _contactStarts, _manifoldA, _manifoldB, _totals]);
        _groupsSet = _groups.Bind([_totals]);
        _compactSet = _compact.Bind(
        [
            _candidates, _met, _contactStarts, _manifoldA, _manifoldB, _contact, _contactNormal, _contactPoints, _overlap,
            _filter, _totals,
        ]);
        _dampSet = _damp.Bind([_owners, _motion]);
        _prepareSet = _prepare.Bind([_contact, _contactNormal, _contactPoints, _pose, _mass, _arms, _pointMass, _impulses, _totals]);
        _solveSet = _solve.Bind(
        [
            _contact, _contactNormal, _arms, _pointMass, _impulses, _motion, _push, _mass, _filter, _lowest, _order, _owners,
            _totals,
        ]);
        _integrateSet = _integrate.Bind([_owners, _pose, _heading, _motion, _push, _shape, _bounds, _filter, _moved]);
        GpuBuffer[] settling =
        [
            _owners, _filter, _motion, _shape, _overlap, _moved, _restTicks, _settling, _contact, _totals, _afresh, _settledOut,
            _wokeAt, _frozeAt,
        ];
        _settleSet = _settle.Bind(settling);
        _settleSpreadSet = _settleSpread.Bind(settling);
        _touchesSets =
        [
            _touches.Bind(
                [_contact, _contactNormal, _totals, _settling, _touched[0], _touchedCount[0], _touched[1], _touchedCount[1], _begunGroups, _begunOut]),
            _touches.Bind(
                [_contact, _contactNormal, _totals, _settling, _touched[1], _touchedCount[1], _touched[0], _touchedCount[0], _begunGroups, _begunOut]),
        ];
        _laidBodiesSet = _laidBodies.Bind([_laidRows, _pose, _heading, _motion, _mass, _shape, _bounds, _filter]);

        _movingScan = new PrefixScan(vk, _scans, _movingStarts, _blockSums, _totals, Totals.MovingCellsAt);
        _candidateScan = new PrefixScan(vk, _scans, _candidateStarts, _blockSums, _totals, Totals.CandidatesAt);
        _contactScan = new PrefixScan(vk, _scans, _contactStarts, _blockSums, _totals, Totals.ContactsAt);
        _wokeScan = new PrefixScan(vk, _scans, _wokeAt, _blockSums, _totals, Totals.WokeCountAt);
        _frozeScan = new PrefixScan(vk, _scans, _frozeAt, _blockSums, _totals, Totals.FrozeCountAt);
        _begunScan = new PrefixScan(vk, _scans, _begunGroups, _blockSums, _totals, Totals.BegunCountAt);
        var staticScan = new PrefixScan(vk, _scans, _staticStarts, _blockSums, _totals, Totals.StaticCellsAt);

        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, Enum.GetValues<Stage>().Length);

        // The whole table once, the furniture's rows for good, and the furniture filed. A row to come is out of the world.
        _uploads = Uploads();
        WriteRows(table, 0, table.Count);
        ClearRows(table.Count, _bodies - table.Count);
        table.Statics.CopyTo(StagingFor(table.Statics.Length, out var staticsIn));
        var commands = _batch.Begin();
        foreach (var (from, to) in _uploads) vk.Copy(commands, from, to, from.SizeBytes);
        vk.Copy(commands, staticsIn, statics, staticsIn.SizeBytes);
        vk.Fill(commands, _push, 0);
        vk.Fill(commands, _restTicks, 0);
        vk.Fill(commands, _settling, 0);
        vk.Fill(commands, _touchedCount[0], 0);
        vk.Fill(commands, _touchedCount[1], 0);
        vk.Fill(commands, _staticCounts, 0);
        vk.Fill(commands, staticCursor, 0);
        vk.Fill(commands, _lowest, uint.MaxValue);
        vk.AfterTransfer(commands);
        var push = Push(0f) with { RosterCount = (uint)_statics, Capacity = (uint)_staticItems };
        _gridCount.Dispatch(commands, staticCountSet, Groups(_statics), push);
        vk.BeforeTransfer(commands);
        vk.Copy(commands, _staticCounts, _staticStarts, _staticCounts.SizeBytes);
        vk.AfterTransfer(commands);
        staticScan.Record(commands, cells);
        _gridFill.Dispatch(commands, staticFillSet, Groups(_statics), push);
        _batch.SubmitAndWait();
        staticsIn.Dispose();
    }

    /// <summary>How many candidates a step may hold; past it a step's pairs are cut, and <see cref="Overflowed"/> says so.</summary>
    public int CandidateCapacity { get; }

    /// <summary>
    /// <b>Every contact solved by one thread in the CPU's own order</b> rather than in colours — an instrument, for an
    /// answer that differs from the CPU's by the device's arithmetic alone.
    /// </summary>
    public bool SolveInContactOrder { get; set; }

    /// <summary>The cells the grids are filed over.</summary>
    public GridWindow Window => _window;

    /// <summary>How many bodies the table holds, the furniture's among them.</summary>
    public int BodyCount => _bodies;

    /// <summary>
    /// The roster of owners the last lay sent: the moving bodies in the world, in shadow (<see cref="Lay"/>); every
    /// moving body the table holds, in the town's place (<see cref="LayWhole"/>).
    /// </summary>
    public int Owners { get; private set; }

    public int Candidates => (int)Math.Min(TotalsRead[Totals.CandidatesAt], (uint)CandidateCapacity);

    public int ContactCount => (int)Math.Min(TotalsRead[Totals.ContactsAt], (uint)CandidateCapacity);

    public int Colours => (int)TotalsRead[Totals.ColoursAt];

    /// <summary>The contacts no colour was left for, which one thread ran in contact order.</summary>
    public int LeftOver => (int)TotalsRead[Totals.LeftOverAt];

    /// <summary>Whether the last step found more candidates than <see cref="CandidateCapacity"/>, and so lost some.</summary>
    public bool Overflowed => TotalsRead[Totals.OverflowAt] > CandidateCapacity;

    ReadOnlySpan<uint> TotalsRead => _totalsOut.Span<uint>()[..Totals.Count];

    /// <summary>The device's time between two stages of the last step, in milliseconds.</summary>
    public double BetweenMs(Stage from, Stage to) => _timer.BetweenMs((int)from, (int)to);

    /// <summary>Where the last step left each body: position and rotation, as the device keeps them.</summary>
    public ReadOnlySpan<Vector4> Pose => _poseOut.Span<Vector4>()[.._bodies];

    public ReadOnlySpan<float> HeadingRad => _headingOut.Span<float>()[.._bodies];

    /// <summary>Velocity and yaw rate after the last step's solve.</summary>
    public ReadOnlySpan<Vector4> Motion => _motionOut.Span<Vector4>()[.._bodies];

    /// <summary>How deep each body was into whatever it touched, as a float's bits (SOL-9).</summary>
    public ReadOnlySpan<uint> OverlapBits => _overlapOut.Span<uint>()[.._bodies];

    /// <summary>The last step's contacts in its order: the pair, how many points, and the colour it was solved in.</summary>
    public ReadOnlySpan<UInt4> Contacts => _contactOut.Span<UInt4>()[..ContactCount];

    public ReadOnlySpan<Vector4> ContactNormals => _contactNormalOut.Span<Vector4>()[..ContactCount];

    public ReadOnlySpan<Vector4> ContactPoints => _contactPointsOut.Span<Vector4>()[..ContactCount];

    /// <summary>
    /// <b>Whether a step settles what it stepped</b> (<c>kernels/settle.comp</c>, SOL-37): which frozen bodies it took in,
    /// each body's rest count, and what froze — stilled and flagged in the table, and handed back as <see cref="Woke"/>
    /// and <see cref="Froze"/> — and which pairs began touching (<see cref="Begun"/>, SOL-6). In the town's place; a step
    /// in shadow is laid the host's freezing whole every time.
    /// </summary>
    public bool Settles { get; set; }

    /// <summary>The frozen bodies the last settling step took in (SOL-37a), refrozen or not, in index order.</summary>
    public ReadOnlySpan<int> Woke => MemoryMarshal.Cast<uint, int>(_settledOut.Span<uint>().Slice(2, (int)_settledOut.Span<uint>()[0]));

    /// <summary>The bodies the last settling step froze, in index order.</summary>
    public ReadOnlySpan<int> Froze =>
        MemoryMarshal.Cast<uint, int>(_settledOut.Span<uint>().Slice(2 + _settledOver, (int)_settledOut.Span<uint>()[1]));

    /// <summary>How many pairs a settling step lists as begun touching (<see cref="Begun"/>).</summary>
    public int BegunRoom { get; }

    /// <summary>
    /// <b>The pairs the last settling step began touching</b> (SOL-6, <c>kernels/touches.comp</c>), in its contacts' order:
    /// the two bodies, then the normal's two figures as a float's bits.
    /// </summary>
    public ReadOnlySpan<UInt4> Begun
    {
        get
        {
            var count = TotalsRead[Totals.BegunCountAt];
            return count <= BegunRoom
                ? _begunOut.Span<UInt4>()[..(int)count]
                : throw new InvalidOperationException($"{count} pairs began touching in one step, past the {BegunRoom} the device lists.");
        }
    }

    /// <summary>How many workgroups of 64 the step's contacts can take, each counting its own begun.</summary>
    int BegunGroups => (CandidateCapacity + 63) / 64;

    /// <summary>
    /// <b>The body table as the device keeps it from one step to the next</b>, for kernels recorded around a step
    /// (<see cref="Record"/>): position and rotation, heading, velocity and yaw rate, the two inverses, and the filter
    /// whose flags say whether a body is in the world (<c>kernels/step.glsl</c>'s layout).
    /// </summary>
    public (GpuBuffer Pose, GpuBuffer Heading, GpuBuffer Motion, GpuBuffer Mass, GpuBuffer Filter) Bodies =>
        (_pose, _heading, _motion, _mass, _filter);

    /// <summary>
    /// <b>One step of the table the CPU is about to step</b>: its moving rows sent, the step run, and — where asked — the
    /// answer and the contacts read back. One submit and one wait.
    /// </summary>
    public void Step(BodyTable table, float dtS, bool readBack)
    {
        Lay(table);
        var commands = _batch.Begin();
        Send(commands);
        Record(commands, dtS);
        _vk.BeforeTransfer(commands);
        RecordReadBack(commands, readBack);
        _batch.SubmitAndWait();
        ReadTimes();
    }

    /// <summary>
    /// The CPU's table as it stands — its moving rows and which of them own pairs — written to go with the next
    /// <see cref="Send"/>. Unlaid, a step runs on what the device's last step left.
    /// </summary>
    public void Lay(BodyTable table)
    {
        WriteTheMovingRows(table);
        var owners = _ownersIn.Span<int>();
        var count = 0;
        foreach (var body in _moving)
        {
            if (body < table.Count && (table.Flags[body] & BodyFlags.Enabled) != 0) owners[count++] = body;
        }

        Owners = count;
        _ownersLaid = true;
    }

    /// <summary>
    /// <b>The table laid whole for the town's place</b>, to go with the next <see cref="Send"/>: every moving row as it
    /// stands, and every moving body it holds an owner — in the world or not, which the kernels pass over.
    /// </summary>
    public void LayWhole(BodyTable table)
    {
        WriteTheMovingRows(table);
        _laidRowCount = 0;
        LayEveryOwner(table.Count, always: true);
    }

    /// <summary>
    /// <b>Only these rows laid</b>, as the table holds them, each written into the device's table by the laid-rows kernel
    /// (<see cref="RecordLaidRows"/>) — and the owners laid again where the table has grown since. A body is named once.
    /// </summary>
    public void LayRows(BodyTable table, ReadOnlySpan<int> bodies)
    {
        if (table.Count > _bodies) throw new InvalidOperationException($"The device's table holds {_bodies} bodies, not {table.Count}.");

        var rows = _laidRows.Span<LaidBodyRow>();
        foreach (var body in bodies)
        {
            rows[_laidRowCount++] = new LaidBodyRow(
                new Vector4(table.PositionM[body], table.Rotation[body].X, table.Rotation[body].Y),
                new Vector4(table.VelocityMps[body], table.YawRateRadPerS[body], 0f),
                new Vector4(table.ExtentM[body], table.CornerRadiusM[body], table.ReachM[body]),
                new Vector4(table.LeastM[body], table.MostM[body].X, table.MostM[body].Y),
                FilterOf(table, body),
                new Vector2(table.InverseMass[body], table.InverseInertia[body]),
                table.HeadingRad[body],
                body);
        }

        LayEveryOwner(table.Count, always: false);
    }

    /// <summary>
    /// <b>The table's rest counts laid whole</b>, to go with the next <see cref="Send"/> — where the table's are the ones
    /// that stand: before the first settling step, or after the town has stepped itself. From then on the device keeps them.
    /// </summary>
    public void LayTheRestCounts(BodyTable table)
    {
        var rest = _restIn.Span<uint>();
        for (var body = _firstMoving; body < Math.Min(table.Count, _firstMoving + _movingRows); body++) rest[body] = (uint)table.RestTicks[body];

        _restLaid = true;
    }

    /// <summary>
    /// <b>The table's touching pairs laid</b> (<see cref="PhysicsWorld.TouchingKeys"/>), to go with the next <see cref="Send"/>
    /// as what was touching before the next step — where the table's are the ones that stand, as its rest counts are.
    /// </summary>
    public void LayTheTouching(ReadOnlySpan<ulong> keys)
    {
        if (keys.Length > CandidateCapacity) throw new InvalidOperationException($"{keys.Length} pairs touching, past the {CandidateCapacity} the device holds.");

        if ((ulong)keys.Length > _touchedIn.SizeBytes / (ulong)Unsafe.SizeOf<UInt2>())
        {
            _owned.Remove(_touchedIn);
            _touchedIn.Dispose();
            _touchedIn = Staging<UInt2>(keys.Length);
        }

        var pairs = _touchedIn.Span<UInt2>();
        for (var at = 0; at < keys.Length; at++) pairs[at] = new UInt2((uint)(keys[at] >> 32), (uint)keys[at] | TouchingBit);

        _touchingLaid = keys.Length;
    }

    /// <summary>A listed pair's mark that it is touching (<c>kernels/touches.comp</c>).</summary>
    const uint TouchingBit = 0x8000_0000u;

    /// <summary>A body whose rest the town counted afresh since the last step (<see cref="PhysicsWorld.CountsRestAfresh"/>), counted afresh by the next.</summary>
    public void CountAfresh(int body) => _afresh.Span<int>()[_afreshCount++] = body;

    /// <summary>
    /// Every moving body the table holds an owner — laid <paramref name="always"/>, or otherwise only where the table has
    /// grown past the owners the last lay sent, which were every moving body it held then.
    /// </summary>
    void LayEveryOwner(int bodies, bool always)
    {
        var count = always ? 0 : Owners;
        while (count < _moving.Length && _moving[count] < bodies) count++;
        if (count == Owners && !always) return;

        _moving.AsSpan(0, count).CopyTo(_ownersIn.Span<int>());
        Owners = count;
        _ownersLaid = true;
    }

    /// <summary>What was laid since the last send copied across, recorded; nothing if nothing was. The caller barriers after.</summary>
    public void Send(CommandBuffer commands)
    {
        SentBytes = (ulong)((_laidRowCount * Unsafe.SizeOf<LaidBodyRow>()) + (_afreshCount * sizeof(int)));
        if (_wholeLaid)
        {
            foreach (var (from, to) in _uploads) SentBytes += CopyRows(commands, from, to);
            _wholeLaid = false;
        }

        if (_restLaid)
        {
            SentBytes += CopyRows(commands, _restIn, _restTicks);
            _restLaid = false;
        }

        if (_touchingLaid >= 0)
        {
            var bytes = (ulong)(_touchingLaid * Unsafe.SizeOf<UInt2>());
            if (bytes > 0) _vk.Copy(commands, _touchedIn, _touched[_touchedWas], bytes);
            _vk.Fill(commands, _touchedCount[_touchedWas], (uint)_touchingLaid);
            SentBytes += bytes;
            _touchingLaid = -1;
        }

        if (!_ownersLaid) return;

        _vk.Copy(commands, _ownersIn, _owners, (ulong)(Owners * sizeof(int)));
        SentBytes += (ulong)(Owners * sizeof(int));
        _ownersLaid = false;
    }

    /// <summary>
    /// <b>The rows laid one by one written into the table</b>, recorded after <see cref="Send"/>'s copies are made
    /// visible (<see cref="Vk.AfterTransfer"/>); whether anything was, so the caller barriers before the kernels that
    /// read the table.
    /// </summary>
    public bool RecordLaidRows(CommandBuffer commands)
    {
        if (_laidRowCount == 0) return false;

        _laidBodies.Dispatch(commands, _laidBodiesSet, Groups(_laidRowCount), Push(0f) with { RosterCount = (uint)_laidRowCount });
        _laidRowCount = 0;
        return true;
    }

    /// <summary>How many bytes the last <see cref="Send"/> took in, the rows the laid-rows kernel reads among them.</summary>
    public ulong SentBytes { get; private set; }

    /// <summary>
    /// <b>The step recorded</b> over the table as the device holds it, timed stage by stage (<see cref="BetweenMs"/>, read
    /// once the submit is waited for by <see cref="ReadTimes"/>). What came before it is made visible to it here; the
    /// caller barriers what reads its answer.
    /// </summary>
    public void Record(CommandBuffer commands, float dtS)
    {
        var count = Owners;
        var push = Push(dtS) with { RosterCount = (uint)count, Capacity = (uint)CandidateCapacity };
        var vk = _vk;
        _timer.Reset(commands);
        vk.Fill(commands, _movingCounts, 0);
        vk.Fill(commands, _movingCursor, 0);
        vk.Fill(commands, _overlap, 0);
        vk.Fill(commands, _totals, 0);
        vk.Fill(commands, _contactStarts, 0);
        if (Settles) vk.Fill(commands, _begunGroups, 0);
        vk.AfterTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Began);

        _gridCount.Dispatch(commands, _movingCountSet, Groups(count), push);
        vk.BeforeTransfer(commands);
        vk.Copy(commands, _movingCounts, _movingStarts, _movingCounts.SizeBytes);
        vk.AfterTransfer(commands);
        _movingScan.Record(commands, _window.Count);
        _gridFill.Dispatch(commands, _movingFillSet, Groups(count), push with { Capacity = (uint)_movingItems });
        vk.BetweenKernels(commands);
        _timer.Stamp(commands, (int)Stage.Grid);

        _pairs.Dispatch(commands, _pairsSet, Groups(count), push with { Mode = 0 });
        vk.BeforeTransfer(commands);
        vk.Copy(commands, _candidateCounts, _candidateStarts, (ulong)(count * sizeof(uint)));
        vk.AfterTransfer(commands);
        _candidateScan.Record(commands, count);
        _pairs.Dispatch(commands, _pairsSet, Groups(count), push with { Mode = 1 });
        vk.BetweenKernels(commands);
        _groups.Dispatch(commands, _groupsSet, 1, push);
        vk.BetweenKernels(commands);
        _timer.Stamp(commands, (int)Stage.Pairs);

        _narrow.DispatchIndirect(commands, _narrowSet, _totals, Totals.CandidateGroupsAt * sizeof(uint), push);
        vk.BetweenKernels(commands);
        _contactScan.Record(commands, CandidateCapacity);
        _groups.Dispatch(commands, _groupsSet, 1, push);
        vk.BetweenKernels(commands);
        _compact.DispatchIndirect(commands, _compactSet, _totals, Totals.CandidateGroupsAt * sizeof(uint), push);
        vk.BetweenKernels(commands);
        _timer.Stamp(commands, (int)Stage.Manifolds);

        _damp.Dispatch(commands, _dampSet, Groups(count), push);
        _prepare.DispatchIndirect(commands, _prepareSet, _totals, Totals.ContactGroupsAt * sizeof(uint), push);
        vk.BetweenKernels(commands);
        _timer.Stamp(commands, (int)Stage.Prepared);

        _solve.Dispatch(commands, _solveSet, 1, push with { Mode = SolveInContactOrder ? 2u : 0u });
        vk.BetweenKernels(commands);
        _timer.Stamp(commands, (int)Stage.Solved);

        _integrate.Dispatch(commands, _integrateSet, Groups(count), push);
        _timer.Stamp(commands, (int)Stage.Integrated);
        if (Settles) RecordTheSettling(commands, push with { AfreshCount = (uint)_afreshCount });

        _afreshCount = 0;
        _timer.Stamp(commands, (int)Stage.Settled);
    }

    /// <summary>
    /// <b>What the step froze and woke worked out</b> (<c>kernels/settle.comp</c>): the owners, then waking along the
    /// contacts, each owner's rest, unsettling along the contacts, the freezing and the two lists — a pass passed on in
    /// one workgroup (<c>settle_spread.comp</c>) only where its parallel pass changed anything.
    /// </summary>
    void RecordTheSettling(CommandBuffer commands, StepPush push)
    {
        var vk = _vk;
        var roster = Owners;
        const ulong perContact = Totals.ContactGroupsAt * sizeof(uint);
        vk.BetweenKernels(commands);
        _settle.Dispatch(commands, _settleSet, Groups(Math.Max(roster, _afreshCount)), push with { Mode = SettleOwning });
        vk.BetweenKernels(commands);
        _settle.DispatchIndirect(commands, _settleSet, _totals, perContact, push with { Mode = SettleWaking });
        vk.BetweenKernels(commands);
        _settleSpread.Dispatch(commands, _settleSpreadSet, 1, push with { Mode = SettleWaking });
        vk.BetweenKernels(commands);
        _settle.Dispatch(commands, _settleSet, Groups(roster), push with { Mode = SettleResting });
        vk.BetweenKernels(commands);
        _settle.DispatchIndirect(commands, _settleSet, _totals, perContact, push with { Mode = SettleUnsettling });
        vk.BetweenKernels(commands);
        _settleSpread.Dispatch(commands, _settleSpreadSet, 1, push with { Mode = SettleUnsettling });
        vk.BetweenKernels(commands);
        _settle.Dispatch(commands, _settleSet, Groups(roster), push with { Mode = SettleFreezing });
        vk.BetweenKernels(commands);
        _wokeScan.Record(commands, roster);
        _frozeScan.Record(commands, roster);
        _settle.Dispatch(commands, _settleSet, Groups(roster), push with { Mode = SettleListing });
        _settledOver = roster;

        // What began touching, once the owners are known (kernels/touches.comp); the pairs written are the next step's.
        vk.BetweenKernels(commands);
        _touches.DispatchIndirect(commands, _touchesSets[_touchedWas], _totals, perContact, push with { Mode = 0 });
        vk.BetweenKernels(commands);
        _begunScan.Record(commands, BegunGroups);
        _touches.DispatchIndirect(commands, _touchesSets[_touchedWas], _totals, perContact, push with { Mode = 1, BegunRoom = (uint)BegunRoom });
        _touchedWas ^= 1;
    }

    // The settling's passes (kernels/settle.glsl).
    const uint SettleOwning = 0;
    const uint SettleWaking = 1;
    const uint SettleResting = 2;
    const uint SettleUnsettling = 3;
    const uint SettleFreezing = 4;
    const uint SettleListing = 5;

    /// <summary>
    /// The last step's totals recorded to be handed back, and — where <paramref name="everything"/> — the poses, the
    /// motion and the contacts; read once the submit is waited for. The caller barriers before it.
    /// </summary>
    public void RecordReadBack(CommandBuffer commands, bool everything)
    {
        var vk = _vk;
        vk.Copy(commands, _totals, _totalsOut, _totals.SizeBytes);
        if (!everything) return;

        CopyRows(commands, _pose, _poseOut);
        CopyRows(commands, _heading, _headingOut);
        CopyRows(commands, _motion, _motionOut);
        CopyRows(commands, _overlap, _overlapOut);
        vk.Copy(commands, _contact, _contactOut, _contact.SizeBytes);
        vk.Copy(commands, _contactNormal, _contactNormalOut, _contactNormal.SizeBytes);
        vk.Copy(commands, _contactPoints, _contactPointsOut, _contactPoints.SizeBytes);
    }

    /// <summary>
    /// How deep each moving body was into what it touched recorded to be handed back (<see cref="OverlapBits"/>); how many
    /// bytes that is. The caller barriers before it.
    /// </summary>
    public ulong RecordTheOverlapRead(CommandBuffer commands) => CopyRows(commands, _overlap, _overlapOut);

    /// <summary>The step's stamps read, once the submit it was recorded into is waited for.</summary>
    public void ReadTimes() => _timer.Read();

    StepPush Push(float dtS)
    {
        var solver = _config.Solver;
        return new StepPush
        {
            WindowFromX = _window.FromX,
            WindowFromY = _window.FromY,
            WindowWidth = _window.Width,
            WindowHeight = _window.Height,
            PerCellM = _window.Level.PerCellM,
            DtS = dtS,
            SpeculativeM = _config.SolverSpeculativeM,
            Friction = solver.Friction,
            EnabledBit = (uint)BodyFlags.Enabled,
            LinearKeep = 1f / (1f + dtS * solver.LinearDamping),
            AngularKeep = 1f / (1f + dtS * solver.AngularDamping),
            AllowedPenetrationM = solver.AllowedPenetrationM,
            ContactBias = dtS > 0f ? solver.ContactBias / dtS : 0f,
            MaxPushOutMps = solver.MaxPushOutMps,
            InverseStep = dtS > 0f ? 1f / dtS : 0f,
            VelocityIterations = (uint)solver.VelocityIterations,
            PositionIterations = (uint)solver.PositionIterations,
            StaticBit = (uint)BodyFlags.Static,
            FiledFrozenBit = (uint)BodyFlags.FiledFrozen,
            FrozenBit = (uint)BodyFlags.Frozen,
            TicksToFreeze = (uint)_config.SolverRestTicks,
            RestSpeedMpsSquared = solver.RestSpeedMps * solver.RestSpeedMps,
        };
    }

    /// <summary>Every moving row the table holds written to go with the next send, the rows to come as they were left.</summary>
    void WriteTheMovingRows(BodyTable table)
    {
        if (table.Count > _bodies) throw new InvalidOperationException($"The device's table holds {_bodies} bodies, not {table.Count}.");

        WriteRows(table, _firstMoving, Math.Clamp(table.Count - _firstMoving, 0, _movingRows));
        _wholeLaid = true;
    }

    /// <summary>Rows <paramref name="from"/> onwards of the CPU's table, written into what the device is sent.</summary>
    void WriteRows(BodyTable table, int from, int rows)
    {
        var pose = _poseIn.Span<Vector4>();
        var heading = _headingIn.Span<float>();
        var motion = _motionIn.Span<Vector4>();
        var mass = _massIn.Span<Vector2>();
        var shape = _shapeIn.Span<Vector4>();
        var bounds = _boundsIn.Span<Vector4>();
        var filter = _filterIn.Span<UInt4>();
        for (var body = from; body < from + rows; body++)
        {
            pose[body] = new Vector4(table.PositionM[body], table.Rotation[body].X, table.Rotation[body].Y);
            heading[body] = table.HeadingRad[body];
            motion[body] = new Vector4(table.VelocityMps[body], table.YawRateRadPerS[body], 0f);
            mass[body] = new Vector2(table.InverseMass[body], table.InverseInertia[body]);
            shape[body] = new Vector4(table.ExtentM[body], table.CornerRadiusM[body], table.ReachM[body]);
            bounds[body] = new Vector4(table.LeastM[body], table.MostM[body].X, table.MostM[body].Y);
            filter[body] = FilterOf(table, body);
        }
    }

    /// <summary>Rows no body holds yet, written as nothing: out of the world, and on no layer.</summary>
    void ClearRows(int from, int rows)
    {
        _poseIn.Span<Vector4>().Slice(from, rows).Clear();
        _headingIn.Span<float>().Slice(from, rows).Clear();
        _motionIn.Span<Vector4>().Slice(from, rows).Clear();
        _massIn.Span<Vector2>().Slice(from, rows).Clear();
        _shapeIn.Span<Vector4>().Slice(from, rows).Clear();
        _boundsIn.Span<Vector4>().Slice(from, rows).Clear();
        _filterIn.Span<UInt4>().Slice(from, rows).Clear();
    }

    /// <summary>A body's flags, category, mask and channels, as the kernels read its filter.</summary>
    static UInt4 FilterOf(in BodyTable table, int body) =>
        new((uint)table.Flags[body], LowWord(table.Category[body]), LowWord(table.Mask[body]), table.Channels[body]);

    /// <summary>A layer mask in the one word the kernels read it in: every layer this town has is in its low bits.</summary>
    static uint LowWord(ulong mask) =>
        mask >> 32 == 0 ? (uint)mask : throw new InvalidOperationException("A collision layer past the 32nd is not laid on the device.");

    (GpuBuffer From, GpuBuffer To)[] Uploads() =>
    [
        (_poseIn, _pose), (_headingIn, _heading), (_motionIn, _motion), (_massIn, _mass), (_shapeIn, _shape),
        (_boundsIn, _bounds), (_filterIn, _filter),
    ];

    /// <summary>The moving rows of one field copied across, which is every row the step can have changed; how many bytes they are.</summary>
    unsafe ulong CopyRows(CommandBuffer commands, GpuBuffer from, GpuBuffer to)
    {
        var stride = from.SizeBytes / (ulong)_bodies;
        var region = new BufferCopy((ulong)_firstMoving * stride, (ulong)_firstMoving * stride, (ulong)_movingRows * stride);
        if (region.Size == 0) return 0;

        Vk.Count();
        _vk.Api.CmdCopyBuffer(commands, from.Handle, to.Handle, 1, &region);
        return region.Size;
    }

    static uint Groups(int count) => (uint)Math.Max(1, (count + 63) / 64);

    ComputeKernel Kernel(string shader, int buffers, int sets = 1)
    {
        var kernel = new ComputeKernel(_vk, shader, buffers, (uint)Marshal.SizeOf<StepPush>(), sets);
        _kernels.Add(kernel);
        return kernel;
    }

    GpuBuffer Device<T>(int count, BufferUsageFlags extra = 0) where T : unmanaged => Own(
        _vk.CreateBuffer(
            Bytes<T>(count),
            BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit | extra,
            HostAccess.None));

    GpuBuffer Staging<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferSrcBit, HostAccess.Writes));

    /// <summary>Memory the host reads back, written by a copy or — a kernel handing back only what it found — by a kernel.</summary>
    GpuBuffer Readback<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit, HostAccess.Reads));

    /// <summary>A staging buffer for <paramref name="count"/> ints, handed back as the span to write them into.</summary>
    Span<int> StagingFor(int count, out GpuBuffer staging)
    {
        staging = _vk.CreateBuffer(Bytes<int>(Math.Max(1, count)), BufferUsageFlags.TransferSrcBit, HostAccess.Writes);
        return staging.Span<int>()[..count];
    }

    GpuBuffer Own(GpuBuffer buffer)
    {
        _owned.Add(buffer);
        return buffer;
    }

    /// <summary>
    /// The window both grids are filed over: every body's box at load, capped about its middle as
    /// <see cref="CellGrid"/> caps one, so what stands past it is filed in the rim.
    /// </summary>
    static GridWindow TownWindow(GridLevel level, BodyTable table)
    {
        var least = new Vector2(float.MaxValue);
        var most = new Vector2(float.MinValue);
        for (var body = 0; body < table.Count; body++)
        {
            least = Vector2.Min(least, table.LeastM[body]);
            most = Vector2.Max(most, table.MostM[body]);
        }

        if (table.Count == 0) return GridWindow.Of(level, 0, 0, 1, 1);

        var window = GridWindow.Over(level, least, most, marginCells: 2);
        if ((long)window.Width * window.Height <= ScanKernels.MostCounts) return window;

        var width = Math.Min(window.Width, ScanKernels.Block);
        var height = Math.Min(window.Height, ScanKernels.Block);
        return GridWindow.Of(
            level, window.FromX + ((window.Width - width) / 2), window.FromY + ((window.Height - height) / 2), width, height);
    }

    /// <summary>How many cells the bodies' boxes cover as they stand, which for the furniture is for good.</summary>
    int CellsCovered(BodyTable table, ReadOnlySpan<int> bodies)
    {
        var cells = 0;
        foreach (var body in bodies)
        {
            _window.TryRange(table.LeastM[body], table.MostM[body], out var range);
            cells += (range.ToX - range.FromX + 1) * (range.ToY - range.FromY + 1);
        }

        return cells;
    }

    /// <summary>
    /// The most cells the moving bodies' boxes can cover however they turn: a box no wider than its own diagonal and
    /// radius twice over, set down anywhere on the grid — and a body still to come as much as the most any covers.
    /// </summary>
    /// <param name="bodies">In index order, so every body still to come is after every body the table holds.</param>
    static int MostCells(GridLevel level, BodyTable table, ReadOnlySpan<int> bodies)
    {
        var cells = 0;
        var most = 0;
        foreach (var body in bodies)
        {
            if (body >= table.Count)
            {
                cells += most;
                continue;
            }

            var reachM = table.ExtentM[body].Length() + table.CornerRadiusM[body];
            var across = (int)MathF.Ceiling(2f * reachM * level.PerCellM) + 1;
            cells += across * across;
            most = Math.Max(most, across * across);
        }

        return cells;
    }

    public void Dispose()
    {
        _timer.Dispose();
        _batch.Dispose();
        foreach (var kernel in _kernels) kernel.Dispose();
        _scans.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
    }
}

/// <summary>Two words, as a kernel's <c>uvec2</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UInt2(uint X, uint Y);

/// <summary>Four words, as a kernel's <c>uvec4</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UInt4(uint X, uint Y, uint Z, uint W);
