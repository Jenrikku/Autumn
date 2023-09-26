using System.ComponentModel;

namespace Autumn.Background;

internal class BackgroundManager
{
    private readonly List<BackgroundTask> _tasks = new();

    private readonly BackgroundWorker _worker = new();
    private readonly AutoResetEvent _resetEvent = new(false);

    /// <summary>
    /// The specified status message for the task that is currently being executed.
    /// </summary>
    public string StatusMessage { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the BackgroundWorker is executing a task.
    /// </summary>
    public bool IsBusy => _worker.IsBusy;

    public BackgroundManager() => _worker.DoWork += BackgroundWork;

    /// <summary>
    /// Adds a task to the queue and executes it last. Calls <see cref="Run"/>.
    /// </summary>
    public void Add(string message, Action action) => Add(new(message, action));

    /// <summary>
    /// Adds a task to the queue and executes it last. Calls <see cref="Run"/>.
    /// </summary>
    public void Add(BackgroundTask task)
    {
        _tasks.Add(task);

        Run();
    }

    /// <summary>
    /// Puts the BackgroundWorker to work. This does nothing if the worker is already executing.
    /// </summary>
    public void Run()
    {
        if (!IsBusy)
            _worker.RunWorkerAsync();
    }

    /// <summary>
    /// Requests the BackgroundWorker to stop before executing the next task.
    /// </summary>
    public void Stop()
    {
        if (!IsBusy)
            return;

        _worker.CancelAsync();
        _resetEvent.WaitOne();
    }

    private void BackgroundWork(object? sender, DoWorkEventArgs e)
    {
        while (_tasks.Count > 0)
        {
            if (sender is BackgroundWorker worker && worker.CancellationPending)
            {
                e.Cancel = true;
                _resetEvent.Set();
                break;
            }

            BackgroundTask task = _tasks[0];

            StatusMessage = task.Message;
            task.Action.Invoke();

            _tasks.RemoveAt(0);
        }

        StatusMessage = string.Empty;
    }
}
