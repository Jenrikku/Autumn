using Autumn.ActionSystem;
using ImGuiNET;
using Silk.NET.Core.Contexts;

namespace Autumn.GUI;

// Based on SceneGL.Testing: https://github.com/jupahe64/SceneGL/blob/master/SceneGL.Testing/WindowManager.cs
internal class WindowManager
{
    public IGLContext? SharedContext { get; private set; } = null;

    private bool s_isRunning = false;

    private readonly List<WindowContext> s_contexts = new();
    private readonly List<WindowContext> s_pendingInits = new();

    public bool IsEmpty => s_contexts.Count <= 0 && s_pendingInits.Count <= 0;
    public int Count => s_contexts.Count + s_pendingInits.Count;

    public bool Add(WindowContext context)
    {
        if (!s_contexts.Contains(context))
        {
            s_pendingInits.Add(context);
            return true;
        }

        return false;
    }

    public void Remove(WindowContext context)
    {
        s_contexts.Remove(context);

        if (context.Window.GLContext == SharedContext && s_contexts.Count > 0)
            SharedContext = s_contexts[0].Window.GLContext;

        context.Window.DoEvents();
        context.Window.Reset();
    }

    public void RemoveAt(int index)
    {
        WindowContext context = s_contexts[index];

        s_contexts.RemoveAt(index);

        if (context.Window.GLContext == SharedContext && s_contexts.Count > 0)
            SharedContext = s_contexts[0].Window.GLContext;

        context.Window.DoEvents();
        context.Window.Reset();
    }

    public void Run(ActionHandler actionHandler)
    {
        if (s_isRunning)
            return;

        s_isRunning = true;

        while (Count > 0)
        {
            if (s_pendingInits.Count > 0)
            {
                foreach (WindowContext context in s_pendingInits)
                {
                    context.Window.Initialize();
                    s_contexts.Add(context);

                    SharedContext ??= context.Window.GLContext;
                }

                s_pendingInits.Clear();
            }

            for (int i = 0; i < s_contexts.Count; i++)
            {
                WindowContext context = s_contexts[i];

                context.Window.DoEvents();

                if (!context.Window.IsClosing)
                {
                    context.Window.DoUpdate();
                    context.Window.DoRender();
                }
                else
                {
                    s_contexts.RemoveAt(i);

                    if (context.Window.GLContext == SharedContext && s_contexts.Count > 0)
                        SharedContext = s_contexts[0].Window.GLContext;

                    context.Window.DoEvents();
                    context.Window.Reset();

                    i--;
                }
            }

            if (!ImGui.GetIO().WantTextInput)
                actionHandler.ExecuteShortcuts(GetFocusedWindow());
        }
    }

    public void Stop()
    {
        if (!s_isRunning)
            return;

        while (s_contexts.Count > 0)
        {
            WindowContext context = s_contexts[0];

            if (!context.Close())
                break;

            RemoveAt(0);
        }
    }

    /// <returns>The context of the focused Window.</returns>
    public WindowContext? GetFocusedWindow()
    {
        foreach (WindowContext context in s_contexts)
        {
            if (context.IsFocused)
                return context;
        }

        return null;
    }

    public IEnumerable<WindowContext> EnumerateContexts()
    {
        foreach (WindowContext context in s_contexts)
            yield return context;
    }
}
