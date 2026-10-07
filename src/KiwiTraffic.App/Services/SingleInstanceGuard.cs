namespace KiwiTraffic.App.Services;

/// <summary>
/// One instance per Windows session. A second launch asks the first one to
/// show itself instead of starting a second widget that would fight over the
/// same settings file.
/// </summary>
/// <remarks>
/// The request travels over a named event rather than a broadcast window
/// message: no window handle has to be published, nothing can be filtered out
/// by message filtering, and no P/Invoke is needed.
/// </remarks>
public sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>
    /// No <c>Global\</c> prefix on purpose: the names live in the session
    /// namespace, which is exactly the "current user" scope the plan asks for.
    /// </summary>
    private const string MutexName = "KiwiTraffic.SingleInstance.v1";

    private const string ActivationEventName = "KiwiTraffic.ActivateWindow.v1";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private RegisteredWaitHandle? _registration;
    private bool _disposed;

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle activationEvent, bool isFirstInstance)
    {
        _mutex = mutex;
        _activationEvent = activationEvent;
        IsFirstInstance = isFirstInstance;
    }

    /// <summary>False when another instance is already running.</summary>
    public bool IsFirstInstance { get; }

    public static SingleInstanceGuard Acquire()
    {
        // Ownership is irrelevant - only whether the name already existed.
        var mutex = new Mutex(initiallyOwned: false, MutexName, out var createdNew);
        var activationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            ActivationEventName,
            out _);

        return new SingleInstanceGuard(mutex, activationEvent, createdNew);
    }

    /// <summary>
    /// Invokes <paramref name="onActivation"/> - on a thread-pool thread - each
    /// time a later launch asks this instance to come forward.
    /// </summary>
    public void ListenForActivation(Action onActivation)
    {
        ArgumentNullException.ThrowIfNull(onActivation);

        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            (_, timedOut) =>
            {
                if (!timedOut)
                {
                    onActivation();
                }
            },
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);
    }

    /// <summary>Asks whichever instance is already running to show its window.</summary>
    public void SignalExistingInstance() => _activationEvent.Set();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registration?.Unregister(null);
        _registration = null;
        _activationEvent.Dispose();
        _mutex.Dispose();
    }
}
