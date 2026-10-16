using System.Runtime.ExceptionServices;

// A browser's runtime has no second thread, and nothing stands one of these up there (ManagedWorld.StepsAsideWherever).
#pragma warning disable CA1416

namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// <b>One job run on a thread of its own while the caller goes on, and waited for</b>: a long-lived thread woken by a
/// signal, so a run allocates nothing.
/// </summary>
/// <remarks>
/// <para>
/// The thread is started on the first <see cref="Run"/>, is a background thread, and ends with <see cref="Dispose"/>. What
/// the job throws is thrown again by the <see cref="Wait"/> that ends its run.
/// </para>
/// <para>
/// <b>The caller keeps off what the job touches between <see cref="Run"/> and <see cref="Wait"/></b>; the two calls are the
/// only fences between them. A browser's runtime has one thread, and a caller there runs the job itself.
/// </para>
/// <para>
/// <b>Not a pair of <see cref="ManualResetEventSlim"/>s</b>, because the first wait that cannot spin through and has to
/// block allocates — the event's lock and the runtime's waiter for that lock, and for the thread — and a caller that
/// always found its run finished first meets that wait on whichever tick the machine is slow, inside the steady state.
/// Here both sides wait on one gate this type owns, and each blocks on it once, for no time, before its first run:
/// the aside thread in <see cref="Loop"/>, and the thread that first calls <see cref="Run"/>. Another caller thread
/// pays its own waiter on its first block.
/// </para>
/// </remarks>
internal sealed class AsideThread(Action job, string name) : IDisposable
{
    /// <summary>
    /// How long a wait spins before it blocks: <see cref="ManualResetEventSlim"/>'s own figure, because a step is about
    /// as long as the work beside it and a wake from a block costs more than the spin.
    /// </summary>
    const int SpinsBeforeBlocking = 35;

    readonly object _gate = new();
    bool _go, _done;
    Thread? _thread;
    bool _running, _stopping;
    ExceptionDispatchInfo? _failed;
    long _allocatedBytes;

    /// <summary>Whether a run has been begun and not yet waited for.</summary>
    public bool Running => _running;

    /// <summary>
    /// What the job has allocated on its thread, all told, as of the last run waited for — what a count of the caller's
    /// own thread does not see.
    /// </summary>
    public long AllocatedBytes => _allocatedBytes;

    /// <exception cref="InvalidOperationException">A run is under way and has not been waited for.</exception>
    public void Run()
    {
        if (_running) throw new InvalidOperationException("A job is waited for before it is run again.");

        _thread ??= Start();
        _running = true;
        Signal(ref _go);
    }

    /// <summary>The run under way waited for; nothing where none is.</summary>
    public void Wait()
    {
        if (!_running) return;

        Await(ref _done);
        _running = false;
        if (_failed is not { } failed) return;

        _failed = null;
        failed.Throw();
    }

    Thread Start()
    {
        BlockOnce();
        var thread = new Thread(Loop) { IsBackground = true, Name = name };
        thread.Start();
        return thread;
    }

    void Loop()
    {
        BlockOnce();
        while (true)
        {
            Await(ref _go);
            if (_stopping) return;

            try
            {
                job();
            }
            catch (Exception broke)
            {
                _failed = ExceptionDispatchInfo.Capture(broke);
            }

            _allocatedBytes = GC.GetAllocatedBytesForCurrentThread();
            Signal(ref _done);
        }
    }

    /// <summary>What the gate's first block allocates, paid on this thread now rather than in a run.</summary>
    void BlockOnce()
    {
        lock (_gate) Monitor.Wait(_gate, 0);
    }

    void Signal(ref bool flag)
    {
        lock (_gate)
        {
            flag = true;
            Monitor.PulseAll(_gate);
        }
    }

    /// <summary>The flag waited for and taken down: spun for first, then blocked on.</summary>
    void Await(ref bool flag)
    {
        var spinner = new SpinWait();
        while (!Volatile.Read(ref flag) && spinner.Count < SpinsBeforeBlocking) spinner.SpinOnce(sleep1Threshold: -1);

        lock (_gate)
        {
            while (!flag) Monitor.Wait(_gate);

            flag = false;
        }
    }

    /// <summary>The run under way waited for, without what it threw, and the thread ended.</summary>
    public void Dispose()
    {
        if (_stopping) return;

        // Waited for before the thread is told to stop: told first, it would read the stop in place of the run.
        if (_running) Await(ref _done);
        _running = false;
        _stopping = true;
        if (_thread is { } thread)
        {
            Signal(ref _go);
            thread.Join();
        }
    }
}
