using Silk.NET.Core.Contexts;

namespace Autumn.GUI;

// Based on SceneGL.Testing: https://github.com/jupahe64/SceneGL/blob/master/SceneGL.Testing/WindowManager.cs
internal static class WindowManager {
    public static IGLContext? SharedContext { get; private set; } = null;

    private static bool s_isRunning = false;

    private static readonly List<WindowContext> s_contexts = new();
    private static readonly List<WindowContext> s_pendingInits = new();

    public static void Add(WindowContext context) {
        if(!s_contexts.Contains(context))
            s_pendingInits.Add(context);
    }

    public static void Run() {
        if(s_isRunning)
            return;

        s_isRunning = true;

        do {

            if(s_pendingInits.Count > 0) {
                foreach(WindowContext context in s_pendingInits) {
                    context.Window.Initialize();
                    s_contexts.Add(context);

                    SharedContext ??= context.Window.GLContext;
                }

                s_pendingInits.Clear();
            }


            for(int i = 0; i < s_contexts.Count; i++) {
                WindowContext context = s_contexts[i];

                context.Window.DoEvents();

                if(!context.Window.IsClosing) {
                    context.Window.DoUpdate();
                    context.Window.DoRender();
                } else {
                    s_contexts.RemoveAt(i);

                    if(context.Window.GLContext == SharedContext && s_contexts.Count > 0)
                        SharedContext = s_contexts[0].Window.GLContext;

                    context.Window.DoEvents();
                    context.Window.Reset();

                    i--;
                }
            }

        } while(s_contexts.Count > 0);
    }

    public static void Stop() {
        if(!s_isRunning)
            return;

        while(s_contexts.Count > 0) {
            WindowContext context = s_contexts[0];

            if(!context.Close())
                break;

            RemoveContextAt(0);
        }
    }

    private static void RemoveContextAt(int index) {
        WindowContext context = s_contexts[index];

        s_contexts.RemoveAt(index);

        if(context.Window.GLContext == SharedContext && s_contexts.Count > 0)
            SharedContext = s_contexts[0].Window.GLContext;

        context.Window.DoEvents();
        context.Window.Reset();
    }
}
