using Silk.NET.Core.Contexts;
using Silk.NET.Windowing;

// Based on SceneGL.Testing: https://github.com/jupahe64/SceneGL/blob/master/SceneGL.Testing/WindowManager.cs

namespace AutumnSceneGL.GUI {    
    internal static class WindowManager {
        public static IGLContext? SharedContext { get; private set; } = null;

        private static bool s_isRunning = false;

        private static readonly List<IWindow> s_windows = new();
        private static readonly List<IWindow> s_pendingInits = new();

        public static void Add(IWindow window) {
            if(s_windows.Contains(window))
                return;

            s_pendingInits.Add(window);
        }

        public static void Run() {
            if(s_isRunning)
                return;

            s_isRunning = true;

            do {

                if(s_pendingInits.Count > 0) {
                    foreach(IWindow window in s_pendingInits) {
                        window.Initialize();
                        s_windows.Add(window);

                        SharedContext ??= window.GLContext;
                    }

                    s_pendingInits.Clear();
                }


                for(int i = 0; i < s_windows.Count; i++) {
                    IWindow window = s_windows[i];

                    window.DoEvents();

                    if(!window.IsClosing) {
                        window.DoUpdate();
                        window.DoRender();
                    } else {
                        s_windows.RemoveAt(i);

                        if(window.GLContext == SharedContext && s_windows.Count > 0) SharedContext = s_windows[0].GLContext;

                        window.DoEvents();
                        window.Reset();

                        i--;
                    }
                }

            } while(s_windows.Count > 0);
        }
    }
}
