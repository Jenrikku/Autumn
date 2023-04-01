using AutumnSceneGL.GUI.ImGUIWindows;
using AutumnSceneGL.GUI.Rendering;
using AutumnSceneGL.Storage;
using AutumnSceneGL.Utils;
using AutumnStageEditor.Storage.StageObj.Interfaces;
using ImGuiNET;
using SceneGL;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;


namespace AutumnSceneGL.GUI {
    internal class MainWindow {
        private static readonly object _glLock = new();

        private StageEditorContext _context = new();
        
        public MainWindow() {
            WindowOptions options = WindowOptions.Default;

            options.VSync = true;
            options.SharedContext = WindowManager.SharedContext;

            options.API = new GraphicsAPI(
                ContextAPI.OpenGL,
                ContextProfile.Core,
#if DEBUG
                ContextFlags.Debug |
#endif
                ContextFlags.ForwardCompatible,
                new APIVersion(3, 3));

            _context.Window = Window.Create(options);
            _context.Window.Load += OnLoad;
            _context.Window.Update += (double delta) => _context.ImguiController?.Update((float) delta);
            _context.Window.Render += OnRender;
            _context.Window.Closing += OnClose;

            _context.Scenes.CollectionChanged += (sender, args) => {
                if(args.Action == NotifyCollectionChangedAction.Add) {
                    object? item = args.NewItems?[args.NewItems.Count - 1];

                    Debug.Assert(item is Scene);

                    if(item is Scene scene)
                        _context.CurrentScene = scene;
                } else if(args.Action == NotifyCollectionChangedAction.Remove)
                    if(_context.Scenes.Count > 0)
                        _context.CurrentScene = _context.Scenes.Last();
                    else
                        _context.CurrentScene = null;
            };
        }

        public void AddToWindowManager() => WindowManager.Add(_context.Window!);

        protected void OnLoad() {
            _context.GL = _context.Window.CreateOpenGL();
            _context.Input = _context.Window!.CreateInput();

            _context.Keyboard = _context.Input.Keyboards[0];

            _context.Window!.Title = "Autumn: Stage Editor";

            lock(_glLock)
                _context.ImguiController = new(_context.GL, _context.Window, _context.Input);

            var win32 = _context.Window.Native!.Win32;

            if(win32 is not null)
                WindowsColorMode.Init(win32.Value.Hwnd);

            // Prevent window from freezing when resizing or moving:
            _context.Window.Resize += (size) => {
                _context.Window.DoUpdate();
                _context.Window.DoRender();
            };

            _context.Window.Move += (size) => {
                _context.Window.DoUpdate();
                _context.Window.DoRender();
            };

            _context.MainDock = new("MainDockSpace");

            _context.GL.ClearColor(0.059f, 0.059f, 0.059f, 1f);
            _context.GL.ClearDepth(0);

            _context.GL.Enable(EnableCap.CullFace);
            _context.GL.Enable(EnableCap.DepthTest);
            _context.GL.DepthFunc(DepthFunction.Gequal);

            InfiniteGrid.Initialize(_context.GL);
            
#if DEBUG
            if(_context.GL.IsExtensionPresent("GL_ARB_debug_output")) {
                _context.GL.Enable(EnableCap.DebugOutput);
                _context.GL.DebugMessageCallback(
                (source, type, id, severity, length, message, _) => {
                    string? messageContent = Marshal.PtrToStringAnsi(new IntPtr(message), length);

                    Debug.WriteLine(messageContent);

                    if(severity != GLEnum.DebugSeverityNotification)
                        Debugger.Break();
                },
                ReadOnlySpan<byte>.Empty);

                // Prevent Buffer info message from spamming the debug console:
                _context.GL.DebugMessageControl(DebugSource.DebugSourceApi, DebugType.DebugTypeOther, DebugSeverity.DontCare,
                    stackalloc[] { (uint) 131185 }, false);
            }
#endif
        }

        protected void OnRender(double deltaSeconds) {
            if(_context.ImguiController is null)
                return;

            _context.ImguiController.MakeCurrent();

            if(_context.IsFirstFrame) {
                OnFirstRender();
                _context.IsFirstFrame = false;
            }

            if(ImGui.BeginMainMenuBar()) {
                if(ImGui.BeginMenu("Project")) {
                    if(ImGui.MenuItem("New"))
                        Project.Unload();

                    ImGui.MenuItem("Open");
                    ImGui.MenuItem("Save");
                    ImGui.MenuItem("Save as...");

                    ImGui.Separator();

                    if(ImGui.MenuItem("Exit") && Project.Unload())
                        _context.Window!.Close();

                    ImGui.EndMenu();
                }

                if(ImGui.BeginMenu("Stage")) {
                    ImGui.MenuItem("Import from romfs");
                    //ImGui.MenuItem("Import through world map selector");
                }

                ImGui.EndMenuBar();
            }

            ImGuiViewportPtr viewport = ImGui.GetMainViewport();
            Vector2 menuBar = new(0, 17);

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

            ImGui.SetNextWindowPos(viewport.Pos + menuBar);
            ImGui.SetNextWindowSize(viewport.Size - menuBar * 2);
            ImGui.SetNextWindowViewport(viewport.ID);

            ImGui.Begin("MainDockSpace",
                ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBringToFrontOnFocus);
            ImGui.PopStyleVar(2);

            ImGui.DockSpace(_context.MainDock!.DockId);
            ImGui.End();

            StageWindow.Render(_context);
            ObjectWindow.Render(_context);
            SceneWindow.Render(_context, deltaSeconds);

            _context.GL?.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _context.GL?.Clear(ClearBufferMask.ColorBufferBit);
            _context.GL?.Viewport(_context.Window!.FramebufferSize);
            _context.ImguiController.Render();
        }

        protected void OnClose() {
            if(!Project.Unload())
                _context.Window!.IsClosing = false;
        }

        protected void OnFirstRender() {
            ImGuiIOPtr io = ImGui.GetIO();

            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            io.ConfigWindowsMoveFromTitleBarOnly = true;
            //io.ConfigWindowsResizeFromEdges = true;

            //_mainDock!.Setup(new("Project"));

            _context.MainDock!.Setup(new DockLayout(
                ImGuiDir.Left, 0.8f,
                new DockLayout("Scene"),
                new DockLayout(
                    ImGuiDir.Up, 0.3f,
                    new DockLayout("Stages"),
                    new DockLayout("Objects")
                )));

            //_mainDock!.Setup(new DockLayout(
            //    ImGuiDir.Left, 0.25f,
            //    new DockLayout("Project", "Stage"),
            //    new DockLayout("Scene")
            //));
        }

        //protected void UpdateTitle(string name) =>
        //    _window.Title = name + " | Autumn: Stage Editor";
    }
}
