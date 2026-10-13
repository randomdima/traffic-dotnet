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
/// <b>In the town's place</b> (<see cref="LayWhole"/>): the table laid once, and every moving body an owner whether or
/// not it is in the world, which the kernels pass over, so a body going into a container changes no roster. What the
/// step found is not handed back: what froze, woke and began touching is (<see cref="Settles"/>). The step is recorded
/// into <see cref="DeviceWorld"/>'s submit (<see cref="Send"/>, <see cref="Record"/>), between kernels that read and
/// write the same table (<see cref="Bodies"/>).
/// </para>
/// <para>
/// <b>Or held to the CPU's step</b> (<see cref="Lay"/>, <see cref="Step"/>): handed the CPU's table as it stands before
/// the CPU's own step, so the two are asked the same question and can be compared. What crosses is the moving rows,
/// never the furniture's, and one submit a step.
/// </para>
/// <para>
/// <b>A frozen body owns no pairs</b> (SOL-37, <c>kernels/owned.glsl</c>): the pairs are the CPU's, pair for pair and in
/// its order — what the awake bodies meet, and what the frozen bodies a manifold wakes meet in turn
/// (<c>kernels/woken.comp</c>). In the town's place the step also works out what the CPU's would have frozen
/// (<see cref="Settles"/>), keeping the rest counts itself.
/// </para>
/// <para>
/// <b>The grids' window is the town's at load</b>, capped as <see cref="CellGrid"/> caps one and with the same rim:
/// a body past it is filed in the edge cell, so the pairs are the same and only what they cost moves. The furniture's
/// grid keeps the window cell for cell; the moving grid hashes its cells into buckets as many as the moving bodies can
/// cover, so a step costs what moves and not the town.
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
        public uint Buckets;
        public uint ContactCapacity;
        public uint SideRoom;
    }

    /// <summary>Where the step keeps its running totals (<c>kernels/step.glsl</c>).</summary>
    internal static class Totals
    {
        public const int CandidatesAt = 0;
        public const int ContactsAt = 1;
        public const int OverflowAt = 2;

        /// <summary>How many contacts the bodies the step woke found (<c>kernels/woken.comp</c>), already in <see cref="ContactsAt"/>.</summary>
        public const int SideContactsAt = 3;

        public const int CandidateGroupsAt = 4;
        public const int ContactGroupsAt = 8;
        public const int ColoursAt = 12;
        public const int LeftOverAt = 13;
        public const int MovingCellsAt = 14;
        public const int StaticCellsAt = 15;

        /// <summary>How many frozen bodies the step woke into itself (<c>kernels/narrow.comp</c>, <c>kernels/woken.comp</c>).</summary>
        public const int WokenAt = 16;

        public const int UnsettledAt = 17;
        public const int WokeCountAt = 18;
        public const int FrozeCountAt = 19;

        /// <summary>The pairs that began touching and the pairs held (<c>kernels/touches.comp</c>); handed back as the begun alone.</summary>
        public const int BegunCountAt = 20;

        /// <summary>The two indirect dispatches the scan of the met flags is sized by, six words (<see cref="PrefixScan.RecordIndirect"/>).</summary>
        public const int MetScanAt = 21;

        public const int Count = 27;
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

    /// <summary>
    /// How many buckets the moving grid is hashed into (<c>kernels/step.glsl</c>'s <c>slotOf</c>): the most cells the moving
    /// bodies can cover, to the next power of two — never more entries than buckets, and in a town about a third as many —
    /// rather than the town's window, which a step would otherwise scan whole.
    /// </summary>
    readonly int _buckets;

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

    /// <summary>
    /// Each body's motion as the next step finds it before anything is spent on it: the last step's, and what the town
    /// set since — but not a push. What a pair's
    /// closing speed is read off (<c>kernels/touches.comp</c>, <c>TownWorld.VelocityIntoTickMps</c>).
    /// </summary>
    readonly GpuBuffer _motionIntoTick;

    // Each body's state through the step (kernels/owned.glsl), the frozen bodies it woke in the order it woke them, and
    // the contacts those found (kernels/woken.comp) — as found, and in their pairs' order.
    readonly GpuBuffer _state;
    readonly GpuBuffer _woken;
    readonly GpuBuffer _side;
    readonly GpuBuffer _sideInOrder;

    // What the step settled (kernels/settle.comp).
    readonly GpuBuffer _restTicks;
    readonly GpuBuffer _restIn;
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

    // The grids. The moving grid's counts are left at nought by every step's fill (kernels/grid_fill.comp).
    readonly GpuBuffer _movingCounts;
    readonly GpuBuffer _movingStarts;
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

    // What is read back: the totals every step, and the rest only by a step held to the CPU's that asks for it.
    readonly GpuBuffer _totalsOut;
    ReadBacks? _readBacks;

    readonly ComputeKernel _gridCount;
    readonly ComputeKernel _gridFill;
    readonly ComputeKernel _pairs;
    readonly ComputeKernel _narrow;
    readonly ComputeKernel _wokenKernel;
    readonly ComputeKernel _compact;
    readonly ComputeKernel _solve;
    readonly ComputeKernel _integrate;
    readonly ComputeKernel _settle;
    readonly ComputeKernel _settleSpread;
    readonly ComputeKernel _touches;

    readonly DescriptorSet _movingCountSet;
    readonly DescriptorSet _movingFillSet;
    readonly DescriptorSet _pairsSet;
    readonly DescriptorSet _narrowSet;
    readonly DescriptorSet _wokenSet;
    readonly DescriptorSet _compactSet;
    readonly DescriptorSet _solveSet;
    readonly DescriptorSet _integrateSet;
    readonly DescriptorSet _settleSet;
    readonly DescriptorSet _settleSpreadSet;

    /// <summary>The touches' sets, one a way round: the step reading the pairs in <c>_touched[n]</c> uses set n.</summary>
    readonly DescriptorSet[] _touchesSets;

    readonly PrefixScan _movingScan;
    readonly PrefixScan _candidateScan;
    readonly PrefixScan _contactScan;
    readonly PrefixScan _wokeScan;
    readonly PrefixScan _frozeScan;
    readonly PrefixScan _begunScan;

    readonly ComputeBatch _batch;

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

    /// <param name="room">
    /// How many bodies the table may come to hold (<see cref="PhysicsWorld.RoomForBodies"/>), every one past what it holds
    /// now a moving body — or none, for a table laid as it stands.
    /// </param>
    public DeviceSolver(Vk vk, SimConfig config, BodyTable table, int room = 0)
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
        _buckets = (int)BitOperations.RoundUpToPowerOf2((uint)_movingItems);
        _staticItems = Math.Max(1, CellsCovered(table, table.Statics));
        CandidateCapacity = (int)Math.Min(ScanKernels.MostCounts, Math.Max(1024L, (long)_moving.Length * CandidatesPerOwner));
        ContactCapacity = (int)Math.Min(CandidateCapacity, Math.Max(1024L, (long)_moving.Length * ContactsPerOwner));
        WithinTheScans();
        var cells = _window.Count;

        _pose = Device<Vector4>(_bodies);
        _heading = Device<float>(_bodies);
        _motion = Device<Vector4>(_bodies);
        _push = Device<Vector4>(_bodies);
        _mass = Device<Vector2>(_bodies);
        _shape = Device<Vector4>(_bodies);
        _bounds = Device<Vector4>(_bodies);
        _filter = Device<uint>(_bodies);
        _owners = Device<int>(Math.Max(1, _moving.Length));
        _overlap = Device<uint>(_bodies);
        _motionIntoTick = Device<Vector4>(_bodies);
        _state = Device<uint>(_bodies);
        _woken = Device<uint>(Math.Max(1, _moving.Length));
        SideRoom = Math.Max(1024, _moving.Length / SideShare);
        _side = Device<SideContact>(SideRoom);
        _sideInOrder = Device<SideContact>(SideRoom);
        _restTicks = Device<uint>(_bodies);
        _restIn = Staging<uint>(_bodies);
        _afresh = Own(_vk.CreateBuffer(Bytes<int>(Math.Max(1, _moving.Length)), BufferUsageFlags.StorageBufferBit, HostAccess.Writes));
        _listedAfresh = new bool[_bodies];
        _settledOut = Readback<uint>(2 + (2 * _moving.Length));
        _wokeAt = Device<uint>(Math.Max(1, _moving.Length));
        _frozeAt = Device<uint>(Math.Max(1, _moving.Length));
        _touched = [Device<UInt2>(ContactCapacity), Device<UInt2>(ContactCapacity)];
        _touchedCount = [Device<uint>(1), Device<uint>(1)];
        _begunGroups = Device<uint>(2 * BegunGroups);
        BegunRoom = Math.Max(1024, _moving.Length);
        _begunOut = Readback<BegunRow>(BegunRoom);
        _touchedIn = Staging<UInt2>(Math.Max(1, _moving.Length));

        _poseIn = Staging<Vector4>(_bodies);
        _headingIn = Staging<float>(_bodies);
        _motionIn = Staging<Vector4>(_bodies);
        _massIn = Staging<Vector2>(_bodies);
        _shapeIn = Staging<Vector4>(_bodies);
        _boundsIn = Staging<Vector4>(_bodies);
        _filterIn = Staging<uint>(_bodies);
        _ownersIn = Staging<int>(Math.Max(1, _moving.Length));

        // A slot past the last bucket, never counted, so a bucket's run ends where the next one's starts.
        _movingCounts = Device<uint>(_buckets + 1);
        _movingStarts = Device<uint>(_buckets + 1);
        _movingItemsBuffer = Device<UInt2>(_movingItems);
        _staticCounts = Device<uint>(cells);
        _staticStarts = Device<uint>(cells);
        var staticItems = Device<int>(_staticItems);

        // Only the furniture's filing reads these, once.
        using var staticCursor = _vk.CreateBuffer(Bytes<uint>(cells), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None);
        using var statics = _vk.CreateBuffer(Bytes<int>(_statics), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None);

        _candidateCounts = Device<uint>(Math.Max(1, _moving.Length));
        _candidateStarts = Device<uint>(Math.Max(1, _moving.Length));
        _candidates = Device<UInt2>(CandidateCapacity);
        _met = Device<uint>(CandidateCapacity);
        _contactStarts = Device<uint>(CandidateCapacity);
        _manifoldA = Device<Vector4>(CandidateCapacity);
        _manifoldB = Device<Vector4>(CandidateCapacity);
        _contact = Device<UInt4>(ContactCapacity);
        _contactNormal = Device<Vector4>(ContactCapacity);
        _contactPoints = Device<Vector4>(ContactCapacity);
        _arms = Device<Vector4>(ContactCapacity * 2);
        _pointMass = Device<Vector2>(ContactCapacity * 2);
        _impulses = Device<Vector4>(ContactCapacity * 2);
        _lowest = Device<uint>(_bodies);
        _order = Device<uint>(ContactCapacity);
        _totals = Device<uint>(Totals.Count, BufferUsageFlags.IndirectBufferBit);
        _blockSums = Device<uint>(ScanKernels.Block);
        _totalsOut = Readback<uint>(Totals.Count);

        _gridCount = Kernel("grid_count.comp", 7, sets: 2);
        _gridFill = Kernel("grid_fill.comp", 6, sets: 2);
        _pairs = Kernel("pairs.comp", 13);
        _narrow = Kernel("narrow.comp", 11);
        _wokenKernel = Kernel("woken.comp", 15);
        _compact = Kernel("compact.comp", 17);
        _solve = Kernel("solve.comp", 13);
        _integrate = Kernel("integrate.comp", 10);
        _settle = Kernel("settle.comp", 12);
        _settleSpread = Kernel("settle_spread.comp", 12);
        _touches = Kernel("touches.comp", 12, sets: 2);

        _movingCountSet = _gridCount.Bind([_owners, _bounds, _movingCounts, _motion, _filter, _state, _overlap]);
        _movingFillSet = _gridFill.Bind([_owners, _bounds, _movingStarts, _movingCounts, _movingItemsBuffer, _filter]);
        var staticCountSet = _gridCount.Bind([statics, _bounds, _staticCounts, _motion, _filter, _state, _overlap]);
        var staticFillSet = _gridFill.Bind([statics, _bounds, _staticStarts, staticCursor, staticItems, _filter]);
        _pairsSet = _pairs.Bind(
        [
            _owners, _bounds, _filter, _movingStarts, _movingItemsBuffer, _staticCounts, _staticStarts, staticItems,
            _candidateCounts, _candidateStarts, _candidates, _totals, _state,
        ]);
        _narrowSet = _narrow.Bind(
            [_candidates, _pose, _shape, _met, _contactStarts, _manifoldA, _manifoldB, _totals, _state, _overlap, _woken]);
        _wokenSet = _wokenKernel.Bind(
        [
            _bounds, _filter, _pose, _shape, _state, _overlap, _movingStarts, _movingItemsBuffer, _staticCounts, _staticStarts,
            staticItems, _woken, _totals, _side, _sideInOrder,
        ]);
        _compactSet = _compact.Bind(
        [
            _candidates, _met, _contactStarts, _manifoldA, _manifoldB, _contact, _contactNormal, _contactPoints, _overlap,
            _filter, _totals, _pose, _mass, _arms, _pointMass, _impulses, _sideInOrder,
        ]);
        _solveSet = _solve.Bind(
        [
            _contact, _contactNormal, _arms, _pointMass, _impulses, _motion, _push, _mass, _filter, _lowest, _order, _owners,
            _totals,
        ]);
        _integrateSet = _integrate.Bind(
            [_owners, _pose, _heading, _motion, _push, _shape, _bounds, _filter, _restTicks, _afresh]);
        GpuBuffer[] settling =
        [
            _owners, _filter, _motion, _shape, _overlap, _restTicks, _state, _contact, _totals, _settledOut, _wokeAt, _frozeAt,
        ];
        _settleSet = _settle.Bind(settling);
        _settleSpreadSet = _settleSpread.Bind(settling);
        _touchesSets =
        [
            _touches.Bind(
            [
                _contact, _contactNormal, _totals, _state, _touched[0], _touchedCount[0], _touched[1], _touchedCount[1], _begunGroups,
                _begunOut, _motionIntoTick, _totalsOut,
            ]),
            _touches.Bind(
            [
                _contact, _contactNormal, _totals, _state, _touched[1], _touchedCount[1], _touched[0], _touchedCount[0], _begunGroups,
                _begunOut, _motionIntoTick, _totalsOut,
            ]),
        ];

        _movingScan = new PrefixScan(vk, _scans, _movingCounts, _movingStarts, _blockSums, _totals, Totals.MovingCellsAt);
        _candidateScan = new PrefixScan(
            vk, _scans, _candidateCounts, _candidateStarts, _blockSums, _totals, Totals.CandidatesAt, Totals.CandidateGroupsAt,
            (uint)CandidateCapacity, Totals.MetScanAt);
        _contactScan = new PrefixScan(
            vk, _scans, _contactStarts, _contactStarts, _blockSums, _totals, Totals.ContactsAt, Totals.ContactGroupsAt,
            (uint)ContactCapacity);
        _wokeScan = new PrefixScan(vk, _scans, _wokeAt, _blockSums, _totals, Totals.WokeCountAt);
        _frozeScan = new PrefixScan(vk, _scans, _frozeAt, _blockSums, _totals, Totals.FrozeCountAt);
        _begunScan = new PrefixScan(vk, _scans, _begunGroups, _blockSums, _totals, Totals.BegunCountAt);
        var staticScan = new PrefixScan(vk, _scans, _staticCounts, _staticStarts, _blockSums, _totals, Totals.StaticCellsAt);

        _batch = new ComputeBatch(vk);

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
        vk.Fill(commands, _state, 0);
        vk.Fill(commands, _touchedCount[0], 0);
        vk.Fill(commands, _touchedCount[1], 0);
        vk.Fill(commands, _staticCounts, 0);
        vk.Fill(commands, staticCursor, 0);
        vk.Fill(commands, _movingCounts, 0);
        vk.Fill(commands, _lowest, uint.MaxValue);
        FillRows(commands, _overlap, 0);
        vk.AfterTransfer(commands);
        var push = Push(0f) with { RosterCount = (uint)_statics, Capacity = (uint)_staticItems };
        _gridCount.Dispatch(commands, staticCountSet, Groups(_statics), push);
        vk.BetweenKernels(commands);
        staticScan.Record(commands, cells);
        _gridFill.Dispatch(commands, staticFillSet, Groups(_statics), push);
        _batch.SubmitAndWait();
        staticsIn.Dispose();
    }

    /// <summary>
    /// How many candidates a step may hold (<see cref="CandidatesPerOwner"/>); past it a step's pairs are cut, and
    /// <see cref="Overflowed"/> says so. Never more than one scan covers (<see cref="ScanKernels.MostCounts"/>).
    /// </summary>
    public int CandidateCapacity { get; }

    /// <summary>
    /// The candidates a step has room for per moving body, frozen or not. Only an awake body gathers more than the awake
    /// bodies it meets, so a town gathers far fewer: OdesaOsm's 80 000 bodies a few thousand, Odesa's 1 476 about a hundred,
    /// a yard of cars shoved into each other a handful each.
    /// </summary>
    const int CandidatesPerOwner = 16;

    /// <summary>
    /// How many contacts a step may hold, and pairs it may list as touching: fewer than its candidates, since a pair whose
    /// boxes meet is seldom one that touches. Past it a step's contacts are cut, and <see cref="Overflowed"/> says so.
    /// </summary>
    public int ContactCapacity { get; }

    /// <summary>
    /// The contacts a step has room for per moving body (<see cref="ContactCapacity"/>): a body packed in a crowd touches
    /// about six others, and a pair is one contact, so a crowd comes to three a body and its furniture to one more — this
    /// is twice that.
    /// </summary>
    const int ContactsPerOwner = 8;

    /// <summary>
    /// How many contacts the frozen bodies a step wakes may find (<c>kernels/woken.comp</c>): one for every
    /// <see cref="SideShare"/> moving bodies, and never fewer than a crowd's worth. Past it a step's contacts are cut, and
    /// <see cref="Overflowed"/> says so.
    /// </summary>
    int SideRoom { get; }

    /// <summary>How many moving bodies share one contact of <see cref="SideRoom"/>: a pile-up wakes a row, not a car park.</summary>
    const int SideShare = 4;

    /// <summary>What the step's own buffers hold, device-local and mapped together — its share of <see cref="Vk.BufferBytes"/>.</summary>
    public long BytesHeld
    {
        get
        {
            var bytes = 0L;
            foreach (var buffer in _owned) bytes += (long)buffer.SizeBytes;
            return bytes;
        }
    }

    /// <summary>One contact the woken bodies' pairs met (<c>kernels/woken.comp</c>): the pair and how many points, the normal and the two separations, the two points.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct SideContact(UInt4 Pair, Vector4 NormalAndDepth, Vector4 Points);

    /// <summary>
    /// <b>Every scan a step records whose length grows with the town is one scan long</b> — the moving grid's buckets and the
    /// roster — refused here, at laying, rather than as the first step is recorded. The candidates are held to it by
    /// their capacity and the furniture's grid by its window.
    /// </summary>
    void WithinTheScans()
    {
        if (_buckets + 1 > ScanKernels.MostCounts || _moving.Length > ScanKernels.MostCounts)
        {
            throw new InvalidOperationException(
                $"{_moving.Length} moving bodies covering up to {_movingItems} cells are past the {ScanKernels.MostCounts} counts " +
                "one scan of the device's step covers.");
        }
    }

    /// <summary>
    /// <b>Every contact solved by one thread in the CPU's own order</b> rather than in colours — an instrument, for an
    /// answer that differs from the CPU's by the device's arithmetic alone.
    /// </summary>
    public bool SolveInContactOrder { get; set; }

    /// <summary>
    /// The roster of owners the last lay sent: the moving bodies in the world, held to the CPU's step (<see cref="Lay"/>);
    /// every moving body the table holds, in the town's place (<see cref="LayWhole"/>).
    /// </summary>
    public int Owners { get; private set; }

    public int ContactCount => (int)Math.Min(TotalsRead[Totals.ContactsAt], (uint)ContactCapacity);

    public int Colours => (int)TotalsRead[Totals.ColoursAt];

    /// <summary>The contacts no colour was left for, which one thread ran in contact order.</summary>
    public int LeftOver => (int)TotalsRead[Totals.LeftOverAt];

    /// <summary>
    /// Whether the last step found more candidates than <see cref="CandidateCapacity"/> or more contacts than
    /// <see cref="ContactCapacity"/>, and so lost some.
    /// </summary>
    public bool Overflowed =>
        TotalsRead[Totals.OverflowAt] > CandidateCapacity || TotalsRead[Totals.ContactsAt] > ContactCapacity;

    ReadOnlySpan<uint> TotalsRead => _totalsOut.Span<uint>()[..Totals.Count];

    /// <summary>Where the last step left each body: position and rotation, as the device keeps them. Read back where asked.</summary>
    public ReadOnlySpan<Vector4> Pose => ReadBack.Pose.Span<Vector4>()[.._bodies];

    /// <summary>Velocity and yaw rate after the last step's solve. Read back where asked.</summary>
    public ReadOnlySpan<Vector4> Motion => ReadBack.Motion.Span<Vector4>()[.._bodies];

    ReadBacks ReadBack => _readBacks ?? throw new InvalidOperationException("No step asked for its answer to be read back.");

    /// <summary>
    /// <b>What a step held to the CPU's reads back beside its totals</b> (<see cref="Step"/>): poses, motion and contacts —
    /// mapped memory the size of the table and the contacts, made only once a step asks for it.
    /// </summary>
    sealed record ReadBacks(GpuBuffer Pose, GpuBuffer Motion, GpuBuffer Contact, GpuBuffer ContactNormal, GpuBuffer ContactPoints);

    /// <summary>
    /// How deep each body was into whatever it touched, as a float's bits (SOL-9), as the last step left it on the device
    /// until the next begins, for a kernel reading it there.
    /// </summary>
    public GpuBuffer Overlap => _overlap;

    /// <summary>The last step's contacts in its order: the pair, how many points, and the colour it was solved in.</summary>
    public ReadOnlySpan<UInt4> Contacts => ReadBack.Contact.Span<UInt4>()[..ContactCount];

    public ReadOnlySpan<Vector4> ContactNormals => ReadBack.ContactNormal.Span<Vector4>()[..ContactCount];

    public ReadOnlySpan<Vector4> ContactPoints => ReadBack.ContactPoints.Span<Vector4>()[..ContactCount];

    /// <summary>
    /// <b>Whether a step settles what it stepped</b> (<c>kernels/settle.comp</c>, SOL-37): which frozen bodies it took in,
    /// each body's rest count, and what froze — stilled and flagged in the table, and listed where
    /// <see cref="ListsWhatSettled"/> asks — and which pairs began touching (<see cref="Begun"/>, SOL-6). In the town's place; a step
    /// held to the CPU's is laid the host's freezing whole every time.
    /// </summary>
    public bool Settles { get; set; }

    /// <summary>
    /// Whether a settling step lists what it woke and froze (<see cref="Woke"/>, <see cref="Froze"/>) — an instrument: the
    /// town reads neither, and the two scans and the pass that lists them are a third of the settling's dispatches.
    /// </summary>
    public bool ListsWhatSettled { get; set; }

    /// <summary>The frozen bodies the last settling step took in (SOL-37a), refrozen or not, in index order.</summary>
    public ReadOnlySpan<int> Woke => MemoryMarshal.Cast<uint, int>(_settledOut.Span<uint>().Slice(2, (int)_settledOut.Span<uint>()[0]));

    /// <summary>The bodies the last settling step froze, in index order.</summary>
    public ReadOnlySpan<int> Froze =>
        MemoryMarshal.Cast<uint, int>(_settledOut.Span<uint>().Slice(2 + _settledOver, (int)_settledOut.Span<uint>()[1]));

    /// <summary>How many pairs a settling step lists as begun touching (<see cref="Begun"/>).</summary>
    int BegunRoom { get; }

    /// <summary>
    /// <b>The pairs the last settling step began touching</b> (SOL-6, <c>kernels/touches.comp</c>), in its contacts' order.
    /// </summary>
    public ReadOnlySpan<BegunRow> Begun
    {
        get
        {
            var count = TotalsRead[Totals.BegunCountAt];
            return count <= BegunRoom
                ? _begunOut.Span<BegunRow>()[..(int)count]
                : throw new InvalidOperationException($"{count} pairs began touching in one step, past the {BegunRoom} the device lists.");
        }
    }

    /// <summary>
    /// One pair that began touching (<c>Begun</c>, <c>kernels/touches.comp</c>): its two bodies, the normal from the first
    /// to the second, and how fast the two closed along it on the motion they brought into the tick (<c>ContactArbiter</c>).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct BegunRow(
        uint First, uint Second, Vector2 Normal, float ClosingMps, float Pad0 = 0f, float Pad1 = 0f, float Pad2 = 0f);

    /// <summary>How many workgroups of 64 the step's contacts can take, each counting its own begun.</summary>
    int BegunGroups => (ContactCapacity + 63) / 64;

    /// <summary>
    /// <b>The body table as the device keeps it from one step to the next</b>, for kernels recorded around a step
    /// (<see cref="Record"/>): position and rotation, heading, velocity and yaw rate, the two inverses, and the filter
    /// whose flags say whether a body is in the world (<c>kernels/step.glsl</c>'s layout).
    /// </summary>
    public (GpuBuffer Pose, GpuBuffer Heading, GpuBuffer Motion, GpuBuffer Mass, GpuBuffer Filter) Bodies =>
        (_pose, _heading, _motion, _mass, _filter);

    /// <summary>
    /// <b>The rest of a body's row</b>, for a kernel that puts a body somewhere between steps: its shape (core half-size,
    /// corner radius, reach), its bounds, and the motion the next step reads its touches' closing speed off.
    /// </summary>
    public (GpuBuffer Shape, GpuBuffer Bounds, GpuBuffer MotionIntoTick) Rows => (_shape, _bounds, _motionIntoTick);

    /// <summary>A layer mask in the byte of a body's filter the kernels read it in (<see cref="LayerByte"/>).</summary>
    public static uint FilterWordOf(ulong mask) => LayerByte(mask);

    /// <summary>
    /// <b>One step of the table the CPU is about to step</b>: its moving rows sent, the step run, and — where asked — the
    /// answer and the contacts read back. One submit and one wait.
    /// </summary>
    public void Step(BodyTable table, float dtS, bool readBack)
    {
        if (readBack)
        {
            _readBacks ??= new ReadBacks(
                Readback<Vector4>(_bodies), Readback<Vector4>(_bodies), Readback<UInt4>(ContactCapacity),
                Readback<Vector4>(ContactCapacity), Readback<Vector4>(ContactCapacity));
        }

        Lay(table);
        var commands = _batch.Begin();
        Send(commands);
        Record(commands, dtS);
        _vk.BeforeTransfer(commands);
        RecordReadBack(commands, readBack);
        _batch.SubmitAndWait();
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
        LayEveryOwner(table.Count);
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
        if (keys.Length > ContactCapacity) throw new InvalidOperationException($"{keys.Length} pairs touching, past the {ContactCapacity} the device holds.");

        if ((ulong)keys.Length > _touchedIn.SizeBytes / (ulong)Unsafe.SizeOf<UInt2>())
        {
            _owned.Remove(_touchedIn);
            _touchedIn.Dispose();
            _touchedIn = Staging<UInt2>(keys.Length);
        }

        var pairs = _touchedIn.Span<UInt2>();
        for (var at = 0; at < keys.Length; at++) pairs[at] = new UInt2((uint)(keys[at] >> 32), (uint)keys[at]);

        _touchingLaid = keys.Length;
    }

    /// <summary>A body whose rest the town counted afresh since the last step — put down or contained — counted afresh by the next.</summary>
    /// <remarks>Listed once however often it is named, so the list holds at most every moving body.</remarks>
    public void CountAfresh(int body)
    {
        if (_listedAfresh[body]) return;

        _listedAfresh[body] = true;
        _afresh.Span<int>()[_afreshCount++] = body;
    }

    /// <summary>Which bodies the afresh list holds since the last step (<see cref="CountAfresh"/>).</summary>
    readonly bool[] _listedAfresh;

    void ForgetTheAfresh()
    {
        foreach (var body in _afresh.Span<int>()[.._afreshCount]) _listedAfresh[body] = false;
        _afreshCount = 0;
    }

    /// <summary>Every moving body the table holds an owner.</summary>
    void LayEveryOwner(int bodies)
    {
        var count = 0;
        while (count < _moving.Length && _moving[count] < bodies) count++;
        _moving.AsSpan(0, count).CopyTo(_ownersIn.Span<int>());
        Owners = count;
        _ownersLaid = true;
    }

    /// <summary>What was laid since the last send copied across, recorded; nothing if nothing was. The caller barriers after.</summary>
    public void Send(CommandBuffer commands)
    {
        if (_wholeLaid)
        {
            foreach (var (from, to) in _uploads) CopyRows(commands, from, to);
            _wholeLaid = false;
        }

        if (_restLaid)
        {
            CopyRows(commands, _restIn, _restTicks);
            _restLaid = false;
        }

        if (_touchingLaid >= 0)
        {
            var bytes = (ulong)(_touchingLaid * Unsafe.SizeOf<UInt2>());
            if (bytes > 0) _vk.Copy(commands, _touchedIn, _touched[_touchedWas], bytes);
            _vk.Fill(commands, _touchedCount[_touchedWas], (uint)_touchingLaid);
            _touchingLaid = -1;
        }

        if (!_ownersLaid) return;

        _vk.Copy(commands, _ownersIn, _owners, (ulong)(Owners * sizeof(int)));
        _ownersLaid = false;
    }

    /// <summary>
    /// <b>The step recorded</b> over the table as the device holds it. What came before it is made visible to it here; the
    /// caller barriers what reads its answer.
    /// </summary>
    public void Record(CommandBuffer commands, float dtS)
    {
        var count = Owners;
        var push = Push(dtS) with { RosterCount = (uint)count };
        var vk = _vk;
        vk.Fill(commands, _totals, 0);
        if (Settles) vk.Fill(commands, _begunGroups, 0);
        vk.AfterTransfer(commands);

        var hashed = push with { Buckets = (uint)_buckets };
        _gridCount.Dispatch(commands, _movingCountSet, Groups(count), hashed with { Mode = CountAndDamp });
        vk.BetweenKernels(commands);
        _movingScan.Record(commands, _buckets + 1);
        _gridFill.Dispatch(commands, _movingFillSet, Groups(count), hashed with { Capacity = (uint)_movingItems });
        vk.BetweenKernels(commands);

        _pairs.Dispatch(commands, _pairsSet, Groups(count), hashed with { Mode = 0 });
        vk.BetweenKernels(commands);
        _candidateScan.Record(commands, count);
        _pairs.Dispatch(commands, _pairsSet, Groups(count), hashed with { Mode = 1 });
        vk.BetweenKernels(commands);

        _narrow.DispatchIndirect(commands, _narrowSet, _totals, Totals.CandidateGroupsAt * sizeof(uint), push);
        vk.BetweenKernels(commands);
        _contactScan.RecordIndirect(commands, Totals.CandidatesAt, Totals.MetScanAt, CandidateCapacity);
        _wokenKernel.Dispatch(commands, _wokenSet, 1, hashed);
        vk.BetweenKernels(commands);
        _compact.DispatchIndirect(commands, _compactSet, _totals, Totals.CandidateGroupsAt * sizeof(uint), push);
        vk.BetweenKernels(commands);

        _solve.Dispatch(commands, _solveSet, 1, push with { Mode = SolveInContactOrder ? 2u : 0u });
        vk.BetweenKernels(commands);

        var settles = push with { Mode = IntegrateAndRest, AfreshCount = (uint)_afreshCount };
        _integrate.Dispatch(
            commands, _integrateSet, Groups(Settles ? Math.Max(count, _afreshCount) : count), Settles ? settles : push);
        if (Settles) RecordTheSettling(commands, settles);

        ForgetTheAfresh();
    }

    /// <summary>
    /// <c>grid_count.comp</c>'s mode for the step's roster: each body's state as the step begins written
    /// (<c>kernels/owned.glsl</c>), and each damped, as it is counted.
    /// </summary>
    const uint CountAndDamp = 1;

    /// <summary><c>integrate.comp</c>'s mode for a settling step: the rest counted afresh where the host reset it, as the step integrates.</summary>
    const uint IntegrateAndRest = 1;

    /// <summary>
    /// <b>What the step froze worked out</b> (<c>kernels/settle.comp</c>), over the owners the step's pairs settled
    /// (<c>kernels/owned.glsl</c>): each owner's rest, unsettling along the contacts — passed on in one workgroup
    /// (<c>settle_spread.comp</c>) only where its parallel pass changed anything — the freezing, and the two lists where
    /// asked for (<see cref="ListsWhatSettled"/>).
    /// </summary>
    void RecordTheSettling(CommandBuffer commands, StepPush push)
    {
        var vk = _vk;
        var roster = Owners;
        const ulong perContact = Totals.ContactGroupsAt * sizeof(uint);
        vk.BetweenKernels(commands);
        _settle.Dispatch(commands, _settleSet, Groups(roster), push with { Mode = SettleResting });
        vk.BetweenKernels(commands);
        _settle.DispatchIndirect(commands, _settleSet, _totals, perContact, push with { Mode = SettleUnsettling });
        vk.BetweenKernels(commands);
        _settleSpread.Dispatch(commands, _settleSpreadSet, 1, push with { Mode = SettleUnsettling });
        vk.BetweenKernels(commands);
        _settle.Dispatch(commands, _settleSet, Groups(roster), push with { Mode = SettleFreezing });

        // What began touching and what is held (kernels/touches.comp), beside the freezing: it reads only whether a body
        // asked what it touches, which the pairs settled; the pairs written are the next step's.
        _touches.DispatchIndirect(commands, _touchesSets[_touchedWas], _totals, perContact, push with { Mode = 0 });
        vk.BetweenKernels(commands);
        if (ListsWhatSettled)
        {
            _wokeScan.Record(commands, roster);
            _frozeScan.Record(commands, roster);
            _settle.Dispatch(commands, _settleSet, Groups(roster), push with { Mode = SettleListing });
            vk.BetweenKernels(commands);
            _settledOver = roster;
        }

        _begunScan.Record(commands, 2 * BegunGroups);
        _touches.DispatchIndirect(commands, _touchesSets[_touchedWas], _totals, perContact, push with { Mode = 1, BegunRoom = (uint)BegunRoom });
        _touchedWas ^= 1;

        // The motion the next step finds, before anything is spent on it: this one's, the frozen stilled.
        vk.BeforeTransfer(commands);
        CopyRows(commands, _motion, _motionIntoTick);
    }

    // The settling's passes (kernels/settle.glsl).
    const uint SettleResting = 2;
    const uint SettleUnsettling = 3;
    const uint SettleFreezing = 4;
    const uint SettleListing = 5;

    /// <summary>
    /// The last step's totals recorded to be handed back, and — where <paramref name="everything"/> — the poses, the
    /// motion and the contacts; read once the submit is waited for. The caller barriers before it.
    /// </summary>
    /// <remarks>A settling step hands its totals back from its last kernel (<c>kernels/touches.comp</c>), so only the rest is copied.</remarks>
    public void RecordReadBack(CommandBuffer commands, bool everything)
    {
        var vk = _vk;
        if (!Settles) vk.Copy(commands, _totals, _totalsOut, _totals.SizeBytes);
        if (!everything) return;

        var into = ReadBack;
        CopyRows(commands, _pose, into.Pose);
        CopyRows(commands, _motion, into.Motion);
        vk.Copy(commands, _contact, into.Contact, _contact.SizeBytes);
        vk.Copy(commands, _contactNormal, into.ContactNormal, _contactNormal.SizeBytes);
        vk.Copy(commands, _contactPoints, into.ContactPoints, _contactPoints.SizeBytes);
    }

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
            Capacity = (uint)CandidateCapacity,
            ContactCapacity = (uint)ContactCapacity,
            SideRoom = (uint)SideRoom,
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
        var filter = _filterIn.Span<uint>();
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
        _filterIn.Span<uint>().Slice(from, rows).Clear();
    }

    /// <summary>
    /// A body's filter in the one word the kernels read it in (<c>kernels/filter.glsl</c>): its flags, then its category,
    /// its mask and its channels, a byte each.
    /// </summary>
    static uint FilterOf(in BodyTable table, int body) =>
        (uint)table.Flags[body] | (LayerByte(table.Category[body]) << 8) | (LayerByte(table.Mask[body]) << 16)
        | ((uint)table.Channels[body] << 24);

    /// <summary>A layer mask in the byte the kernels read it in: every layer this town has is in its low bits.</summary>
    static uint LayerByte(ulong mask) =>
        mask >> 8 == 0 ? (uint)mask : throw new InvalidOperationException("A collision layer past the 8th is not laid on the device.");

    (GpuBuffer From, GpuBuffer To)[] Uploads() =>
    [
        (_poseIn, _pose), (_headingIn, _heading), (_motionIn, _motion), (_motionIn, _motionIntoTick), (_massIn, _mass),
        (_shapeIn, _shape), (_boundsIn, _bounds), (_filterIn, _filter),
    ];

    /// <summary>The moving rows of one field filled with one word: the furniture's are never written, and never read.</summary>
    void FillRows(CommandBuffer commands, GpuBuffer buffer, uint word)
    {
        var stride = buffer.SizeBytes / (ulong)_bodies;
        if (_movingRows > 0) _vk.Fill(commands, buffer, (ulong)_firstMoving * stride, (ulong)_movingRows * stride, word);
    }

    /// <summary>The moving rows of one field copied across, which is every row the step can have changed.</summary>
    unsafe void CopyRows(CommandBuffer commands, GpuBuffer from, GpuBuffer to)
    {
        var stride = from.SizeBytes / (ulong)_bodies;
        var region = new BufferCopy((ulong)_firstMoving * stride, (ulong)_firstMoving * stride, (ulong)_movingRows * stride);
        if (region.Size == 0) return;

        Vk.Count();
        _vk.Api.CmdCopyBuffer(commands, from.Handle, to.Handle, 1, &region);
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
