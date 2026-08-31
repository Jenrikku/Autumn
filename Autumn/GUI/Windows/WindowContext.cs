using System.Diagnostics;
using System.Runtime.InteropServices;
using Autumn.Context;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.GLFW;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Silk.NET.Core;
using Silk.NET.GLFW;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SixLabors.ImageSharp.PixelFormats;

namespace Autumn.GUI.Windows;

/// <summary>
/// This class serves as a base for all windows.<br />
/// A window context contains all the necessary data related to the window.
/// </summary>
/// <seealso cref="WindowManager" />
internal abstract class WindowContext
{
    public IWindow Window { get; protected set; }
    public GLFWwindowPtr WindowNative { get; protected set; } // From Hexa
    public ImGuiContextPtr ImGuiContext { get; protected set; }

    public GL? GL { get; protected set; }

    public IInputContext? InputContext { get; protected set; }
    public IKeyboard? Keyboard { get; protected set; }
    public IMouse? Mouse { get; protected set; }

    public bool IsFocused { get; private set; } = true;

    // Prevents the user to interact with the window when true.
    public bool Disabled { get; set; } = false;

    public ContextHandler ContextHandler { get; }
    public WindowManager WindowManager { get; }

    private float _scalingFactor = 1;
    public float ScalingFactor => _scalingFactor;

    /// <summary>
    /// Specifies where the "imgui.ini" file is stored in.
    /// By default, it will be stored within Autumn's settings path.
    /// </summary>
    protected readonly string ImguiSettingsFile;

    private static RawImage[]? s_iconCache;

    private bool _themeChanged = false;
    private bool _currentFrameDisabled = false; // Prevents issues when changing Disabled property mid-frame.

    public WindowContext(ContextHandler contextHandler, WindowManager windowManager)
    {
        ContextHandler = contextHandler;
        WindowManager = windowManager;

        ImguiSettingsFile = Path.Join(contextHandler.SettingsPath, "imgui.ini");

        WindowOptions options = WindowOptions.Default;

        options.VSync = contextHandler.SystemSettings.EnableVSync;
        options.SharedContext = WindowManager.SharedContext;

        ContextFlags contextFlags = ContextFlags.ForwardCompatible;

#if DEBUG
        contextFlags |= ContextFlags.Debug;
#endif

        options.API = new GraphicsAPI(
            ContextAPI.OpenGL,
            ContextProfile.Core,
            contextFlags,
            new APIVersion(3, 3) // OpenGL 3.3
        );

        Window = Silk.NET.Windowing.Window.Create(options);

        Window.Load += () =>
        {
            GL = Window.CreateOpenGL();

            InputContext = Window.CreateInput();
            Keyboard = InputContext.Keyboards[0];
            Mouse = InputContext.Mice[0];

            #region Load icons

            if (s_iconCache is null)
            {
                byte[] iconSizes = [16, 32, 64];
                s_iconCache = new RawImage[3];
                for (int i = 0; i < iconSizes.Length; i++)
                {
                    string path = Path.Join("Resources", "Icons", $"autumn{iconSizes[i]}.png");
                    var image = SixLabors.ImageSharp.Image.Load<Rgba32>(path);

                    byte[] pixels = new byte[image.Width * image.Height * 4];
                    image.CopyPixelDataTo(pixels);

                    s_iconCache[i] = new(image.Width, image.Height, pixels);
                }
            }

            Window.SetWindowIcon(s_iconCache);

            #endregion

            unsafe
            {
                nint? glfwWin = Window.Native?.Glfw;
                Debug.Assert(glfwWin.HasValue);
                WindowNative = (GLFWwindow*)glfwWin.Value;
            }

            ImGuiContext = ImGui.CreateContext();

            ImGuiImplGLFW.SetCurrentContext(ImGuiContext);
            ImGuiImplGLFW.InitForOpenGL(WindowNative, true);
            _scalingFactor = ImGuiImplGLFW.GetContentScaleForWindow(WindowNative);

            ImGuiImplOpenGL3.SetCurrentContext(ImGuiContext);
            ImGuiImplOpenGL3.Init("#version 330");

            ImGuiAddFonts();

            WindowManager.GlobalTheme.UpdateImGuiTheme();

            var win32 = Window.Native?.Win32;

            if (win32.HasValue)
                WindowsColorMode.Init(win32.Value.Hwnd);

            // Prevent window from freezing when resizing or moving:
            Window.Resize += (size) =>
            {
                Window.DoUpdate();
                Window.DoRender();
            };

            Window.Move += (size) =>
            {
                Window.DoUpdate();
                Window.DoRender();
            };

            ImGuiIOPtr imguiIO = ImGui.GetIO();

            unsafe
            {
                // Set the settings file's path in imgui.
                imguiIO.Handle->IniFilename = (byte*)Marshal.StringToCoTaskMemUTF8(ImguiSettingsFile);
            }

            imguiIO.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            imguiIO.ConfigDragClickToInputText = true;
            imguiIO.ConfigWindowsMoveFromTitleBarOnly = true;
            imguiIO.ConfigWindowsResizeFromEdges = false;

            // If no imgui settings file exists or remember layout is set to false, load the default layout
            if (!ContextHandler.SystemSettings.RememberLayout || !File.Exists(ImguiSettingsFile))
            {
                // Load the default imgui settings file.
                ImGui.LoadIniSettingsFromDisk(Path.Join("Resources", "DefaultLayout.ini"));
            }

            // Set the clear color and depth.
            GL.ClearColor(0.059f, 0.059f, 0.059f, 1f);
            GL.ClearDepth(1);

            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
        };

        Window.Update += delta =>
        {
            if (_themeChanged)
            {
                WindowManager.GlobalTheme.UpdateImGuiTheme();
                _themeChanged = false;
            }
        };

        Window.FocusChanged += focused => IsFocused = focused;

        Window.Closing += () =>
        {
            Window.IsClosing = Close();

            if (!Window.IsClosing)
                return;

            // Stop windows color mode check:

            var win32 = Window.Native?.Win32;

            if (win32 is not null)
                WindowsColorMode.Stop(win32.Value.Hwnd);
        };
    }

    public void Reset()
    {
        Window.DoEvents();
        Window.Reset();

        ImGui.SetCurrentContext(ImGuiContext);
        ImGui.GetFont().Destroy();

        ImGuiImplGLFW.SetCurrentContext(ImGuiContext);
        ImGuiImplGLFW.Shutdown();
        ImGuiImplGLFW.SetCurrentContext(null);

        GL?.Dispose();
        InputContext?.Dispose();
    }

    /// <summary>
    /// A method that is meant to be overriden in order to make any operations
    /// before the window gets closed.
    /// </summary>
    /// <returns>Whether the window can be safely closed.</returns>
    public virtual bool Close() => true;

    public void RefreshTheme() => _themeChanged = true;

    protected void ImGuiMakeCurrentContext()
    {
        ImGuiImplOpenGL3.SetCurrentContext(ImGuiContext);
        ImGuiImplGLFW.SetCurrentContext(ImGuiContext);
        ImGui.SetCurrentContext(ImGuiContext);
    }

    protected void ImGuiNewFrame()
    {
        ImGuiImplOpenGL3.NewFrame();
        ImGuiImplGLFW.NewFrame();
        ImGui.NewFrame();

        _currentFrameDisabled = Disabled;

        if (_currentFrameDisabled) // Do not use Disable property here as a race condition may happen.
            ImGui.BeginDisabled();
    }

    protected void ImGuiEndFrame()
    {
        if (_currentFrameDisabled)
        {
            ImGui.EndDisabled();

            var drawList = ImGui.GetForegroundDrawList();
            var max = ImGui.GetMainViewport().Size;
            ImGui.AddRectFilled(drawList, new(0), max, ImGui.GetColorU32(ImGuiCol.ModalWindowDimBg));
        }

        ImGui.Render();
        ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());
    }

    public static ImGuiKey MapImGuiKey(Silk.NET.Input.Key key)
    {
        static ImGuiKey KeyToImGuiKeyShortcut(Key keyToConvert, Key startKey1, ImGuiKey startKey2)
        {
            int changeFromStart1 = (int)keyToConvert - (int)startKey1;
            return startKey2 + changeFromStart1;
        }

        if (Key.A <= key && key <= Key.Z)
        {
            Glfw glfw = Glfw.GetApi();

            int glfwKey = (int)Silk.NET.GLFW.Keys.A + (key - Key.A);
            int scanCode = glfw.GetKeyScancode(glfwKey);
            char keyNameChar = glfw.GetKeyName(glfwKey, scanCode)[0];

            key = Key.A + (char.ToLower(keyNameChar) - 'a');
        }

        return key switch
        {
            >= Key.F1 and <= Key.F24 => KeyToImGuiKeyShortcut(key, Key.F1, ImGuiKey.F1),
            >= Key.Keypad0
            and <= Key.Keypad9
                => KeyToImGuiKeyShortcut(key, Key.Keypad0, ImGuiKey.Keypad0),
            >= Key.A and <= Key.Z => KeyToImGuiKeyShortcut(key, Key.A, ImGuiKey.A),
            >= Key.Number0
            and <= Key.Number9
                => KeyToImGuiKeyShortcut(key, Key.Number0, ImGuiKey.Key0),
            Key.ShiftLeft => ImGuiKey.LeftShift,
            Key.ShiftRight => ImGuiKey.RightShift,
            Key.ControlLeft => ImGuiKey.LeftCtrl,
            Key.ControlRight => ImGuiKey.RightCtrl,
            Key.AltLeft => ImGuiKey.LeftAlt,
            Key.AltRight => ImGuiKey.RightAlt,
            Key.SuperLeft => ImGuiKey.LeftCtrl,
            Key.SuperRight => ImGuiKey.RightCtrl,
            Key.Menu => ImGuiKey.Menu,
            Key.Up => ImGuiKey.UpArrow,
            Key.Down => ImGuiKey.DownArrow,
            Key.Left => ImGuiKey.LeftArrow,
            Key.Right => ImGuiKey.RightArrow,
            Key.Enter => ImGuiKey.Enter,
            Key.Escape => ImGuiKey.Escape,
            Key.Space => ImGuiKey.Space,
            Key.Tab => ImGuiKey.Tab,
            Key.Backspace => ImGuiKey.Backspace,
            Key.Insert => ImGuiKey.Insert,
            Key.Delete => ImGuiKey.Delete,
            Key.PageUp => ImGuiKey.PageUp,
            Key.PageDown => ImGuiKey.PageDown,
            Key.Home => ImGuiKey.Home,
            Key.End => ImGuiKey.End,
            Key.CapsLock => ImGuiKey.CapsLock,
            Key.ScrollLock => ImGuiKey.ScrollLock,
            Key.PrintScreen => ImGuiKey.PrintScreen,
            Key.Pause => ImGuiKey.Pause,
            Key.NumLock => ImGuiKey.NumLock,
            Key.KeypadDivide => ImGuiKey.KeypadDivide,
            Key.KeypadMultiply => ImGuiKey.KeypadMultiply,
            Key.KeypadSubtract => ImGuiKey.KeypadSubtract,
            Key.KeypadAdd => ImGuiKey.KeypadAdd,
            Key.KeypadDecimal => ImGuiKey.KeypadDecimal,
            Key.KeypadEnter => ImGuiKey.KeypadEnter,
            Key.GraveAccent => ImGuiKey.GraveAccent,
            Key.Minus => ImGuiKey.Minus,
            Key.Equal => ImGuiKey.Equal,
            Key.LeftBracket => ImGuiKey.LeftBracket,
            Key.RightBracket => ImGuiKey.RightBracket,
            Key.Semicolon => ImGuiKey.Semicolon,
            Key.Apostrophe => ImGuiKey.Apostrophe,
            Key.Comma => ImGuiKey.Comma,
            Key.Period => ImGuiKey.Period,
            Key.Slash => ImGuiKey.Slash,
            Key.BackSlash => ImGuiKey.Backslash,
            _ => ImGuiKey.None
        };
    }

    // FIXME: Fonts are read from disk every time a window is opened.
    // Potentially move font reading and managing into WindowManager.
    private unsafe void ImGuiAddFonts()
    {
        ImGui.SetCurrentContext(ImGuiContext);

        var io = ImGui.GetIO();

        io.Fonts.AddFontFromFileTTF(
            Path.Join("Resources", "NotoSansJP-Regular.ttf"),
            18
        );

        ImFontConfig* cfg = ImGui.ImFontConfig();
        cfg->MergeMode = 1;

        io.Fonts.AddFontFromFileTTF(
            Path.Join("Resources", "fa-solid-900.otf"),
            18,
            cfg
        );
    }
}
