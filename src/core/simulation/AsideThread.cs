using System.Runtime.ExceptionServices;

// A browser's runtime has no second thread, and nothing stands one of these up there (ManagedWorld.StepsAsideWherever).
#pragma warning disable CA1416

namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// <b>One job run on a thread of its own while the caller goes on, and waited for</b>: a long-lived thread woken by an
/// event, so a run allocates nothing.
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
/// </remarks>
internal sealed class AsideThread(Action job, string name) : IDisposable
{
    readonly ManualResetEventSlim _go = new(false), _done = new(false);
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
        _go.Set();
    }

    /// <summary>The run under way waited for; nothing where none is.</summary>
    public void Wait()
    {
        if (!_running) return;

        _done.Wait();
        _done.Reset();
        _running = false;
        if (_failed is not { } failed) return;

        _failed = null;
        failed.Throw();
    }

    Thread Start()
    {
        var thread = new Thread(Loop) { IsBackground = true, Name = name };
        thread.Start();
        return thread;
    }

    void Loop()
    {
        while (true)
        {
            _go.Wait();
            _go.Reset();
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
            _done.Set();
        }
    }

    /// <summary>The run under way waited for, without what it threw, and the thread ended.</summary>
    public void Dispose()
    {
        if (_stopping) return;

        // Waited for before the thread is told to stop: told first, it would read the stop in place of the run.
        if (_running) _done.Wait();
        _running = false;
        _stopping = true;
        if (_thread is { } thread)
        {
            _go.Set();
            thread.Join();
        }

        _go.Dispose();
        _done.Dispose();
    }
}
