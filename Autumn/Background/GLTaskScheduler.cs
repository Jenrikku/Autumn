using System.Diagnostics;
using Silk.NET.OpenGL;

namespace Autumn.Background;

internal class GLTaskScheduler
{
    private readonly Queue<Action<GL>> _glQueue = new();

    public int TasksLeft => _glQueue.Count;

    public void EnqueueGLTask(Action<GL> glTask)
    {
        lock (_glQueue)
        {
            // Improves performance by resetting the queue so that it does not grow too much.
            // Effectively puts head and tail to 0.
            if (_glQueue.Count == 0)
                _glQueue.Clear();

            _glQueue.Enqueue(glTask);
        }
    }

    public void DoTasks(GL gl, double deltaSeconds)
    {
        long startTime = Stopwatch.GetTimestamp();
        TimeSpan delta = TimeSpan.FromSeconds(deltaSeconds);

        int count = _glQueue.Count;

        if (count == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            if (!_glQueue.TryDequeue(out Action<GL>? glTask))
                break;

            glTask(gl);

            if (Stopwatch.GetElapsedTime(startTime) >= delta)
                break;
        }
    }
}
