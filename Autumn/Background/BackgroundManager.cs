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
    /// Detailed status message for the task that is currently being executed, to indicate progress.
    /// </summary>
    public string StatusMessageSecondary = string.Empty;

    /// <summary>
    /// Whether the BackgroundWorker is executing a task.
    /// </summary>
    public bool IsBusy => _worker.IsBusy;

    public BackgroundManager() => _worker.DoWork += BackgroundWork;

    /// <summary>
    /// Adds a task to the queue and executes it last. Calls <see cref="Run"/>.
    /// </summary>
    public void Add(
        string message,
        Action action,
        BackgroundTaskPriority priority = BackgroundTaskPriority.Regular
    ) => Add(new(message, action, priority));

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
    /// Requests the BackgroundWorker to stop before executing the next task.<br />
    /// This will fail if there are High or higher priority tasks to be done and <see cref="force"/> is set to false.
    /// </summary>
    /// <returns>If the operation was successful.</returns>
    public bool Stop(bool force = false)
    {
        if (!IsBusy)
            return false;

        Predicate<BackgroundTask> predicate = (task) =>
            task.Priority > BackgroundTaskPriority.Regular;

        if (!force && _tasks.Find(predicate) is not null)
            return false;

        _worker.CancelAsync();
        _resetEvent.WaitOne();
        return true;
    }

    public IEnumerable<BackgroundTask> GetRemainingTasks()
    {
        // This is done using a for loop because _tasks may be changed by the background worker.

        for (int i = 0; i < _tasks.Count; i++)
            yield return _tasks[i];
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

            BackgroundTask nextTask = _tasks[0];

            foreach (BackgroundTask task in _tasks)
            {
                if (task.Priority > nextTask.Priority)
                    nextTask = task;

                if (nextTask.Priority == BackgroundTaskPriority.Highest)
                    break;
            }

            StatusMessage = nextTask.Message;
            nextTask.Action.Invoke();

            _tasks.RemoveAt(0);
        }

        StatusMessage = string.Empty;
        StatusMessageSecondary = string.Empty;
    }
}
