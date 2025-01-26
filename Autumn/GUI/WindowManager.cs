using Autumn.ActionSystem;
using Autumn.GUI.Windows;
using ImGuiNET;
using Silk.NET.Core.Contexts;

namespace Autumn.GUI;

// Based on SceneGL.Testing: https://github.com/jupahe64/SceneGL/blob/master/SceneGL.Testing/WindowManager.cs
internal class WindowManager
{
    public IGLContext? SharedContext { get; private set; } = null;

    private bool _isRunning = false;

    private readonly List<WindowContext> _contexts = new();
    private readonly List<WindowContext> _pendingInits = new();

    public bool IsEmpty => _contexts.Count <= 0 && _pendingInits.Count <= 0;
    public int Count => _contexts.Count + _pendingInits.Count;

    public bool Add(WindowContext context)
    {
        if (!_contexts.Contains(context))
        {
            _pendingInits.Add(context);
            return true;
        }

        return false;
    }

    public void Remove(WindowContext context)
    {
        _contexts.Remove(context);

        if (context.Window.GLContext == SharedContext && _contexts.Count > 0)
            SharedContext = _contexts[0].Window.GLContext;

        context.Window.DoEvents();
        context.Window.Reset();
        context.InputContext?.Dispose();
    }

    public void RemoveAt(int index)
    {
        WindowContext context = _contexts[index];

        _contexts.RemoveAt(index);

        if (context.Window.GLContext == SharedContext && _contexts.Count > 0)
            SharedContext = _contexts[0].Window.GLContext;

        context.Window.DoEvents();
        context.Window.Reset();
        context.InputContext?.Dispose();
    }

    public void Run(ActionHandler actionHandler)
    {
        if (_isRunning)
            return;

        _isRunning = true;

        while (Count > 0 && _isRunning)
        {
            if (_pendingInits.Count > 0)
            {
                foreach (WindowContext context in _pendingInits)
                {
                    context.Window.Initialize();
                    _contexts.Add(context);

                    SharedContext ??= context.Window.GLContext;
                }

                _pendingInits.Clear();
            }

            for (int i = 0; i < _contexts.Count; i++)
            {
                WindowContext context = _contexts[i];

                context.Window.DoEvents();

                if (!context.Window.IsClosing)
                {
                    context.Window.DoUpdate();
                    context.Window.DoRender();
                }
                else
                {
                    RemoveAt(i);
                    i--;
                }
            }

            if (!ImGui.GetIO().WantTextInput)
                actionHandler.ExecuteShortcuts(GetFocusedWindow());
        }

        while (Count > 0)
            RemoveAt(Count - 1);
    }

    public void Stop()
    {
        if (!_isRunning)
            return;

        foreach (WindowContext windowContext in _contexts)
        {
            if (!windowContext.Close())
                return;
        }

        _isRunning = false;
    }

    /// <returns>The context of the focused Window.</returns>
    public WindowContext? GetFocusedWindow()
    {
        foreach (WindowContext context in _contexts)
        {
            if (context.IsFocused)
                return context;
        }

        return null;
    }

    public IEnumerable<WindowContext> EnumerateContexts()
    {
        foreach (WindowContext context in _contexts)
            yield return context;
    }
}
