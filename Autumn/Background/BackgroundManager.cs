using System.ComponentModel;
using Autumn.Enums;

namespace Autumn.Background;

internal class BackgroundManager
{
    private readonly List<BackgroundTask> _tasks = new();

    private readonly BackgroundWorker _worker = new() { WorkerSupportsCancellation = true };

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
        Action<BackgroundManager> action,
        BackgroundTaskPriority priority = default
    ) => Add(new(message, action, priority));

    /// <summary>
    /// Adds a task to the queue and executes it last. Calls <see cref="Run"/>.
    /// </summary>
    public void Add(BackgroundTask task)
    {
        lock (_tasks)
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

        lock (_tasks)
        {
            if (!force && _tasks.Find(task => task.Priority >= BackgroundTaskPriority.High) is not null)
                return false;
        }

        _worker.CancelAsync();
        return true;
    }

    /// <param name="lowestPriority">Only the priorities equal or higher to this will be enumerated.</param>
    public List<BackgroundTask> GetRemainingTasks(
        BackgroundTaskPriority lowestPriority = default
    )
    {
        // Create a list rather than enumerating to minimize the time within the lock.
        lock (_tasks)
            return _tasks.FindAll(task => task.Priority >= lowestPriority);
    }

    private void BackgroundWork(object? sender, DoWorkEventArgs e)
    {
        while (_tasks.Count > 0) // No lock needed: Only written to by this method
        {
            if (sender is BackgroundWorker worker && worker.CancellationPending)
            {
                e.Cancel = true;
                break;
            }

            BackgroundTask? nextTask;

            // FIXME: Algorithm not optimal to be used within a lock.
            lock (_tasks)
            {
                nextTask = _tasks.Find(task => task.Priority == BackgroundTaskPriority.Highest);
                nextTask ??= _tasks.Find(task => task.Priority == BackgroundTaskPriority.High);
                nextTask ??= _tasks[0];
            }

            StatusMessage = nextTask.Message;
            nextTask.Action.Invoke(this);

            lock(_tasks)
                _tasks.RemoveAt(0);
        }

        StatusMessage = string.Empty;
        StatusMessageSecondary = string.Empty;
    }
}
