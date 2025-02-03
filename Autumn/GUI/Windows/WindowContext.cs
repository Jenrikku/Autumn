using System.Runtime.InteropServices;
using Autumn.Context;
using ImGuiNET;
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
    public List<ImFontPtr> FontPointers { get; set; } = new();
    public IWindow Window { get; protected set; }
    public ImGuiController? ImGuiController { get; protected set; }

    public GL? GL { get; protected set; }

    public IInputContext? InputContext { get; protected set; }
    public IKeyboard? Keyboard { get; protected set; }
    public IMouse? Mouse { get; protected set; }

    public bool IsFocused { get; private set; }

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

            // Set scaling factor:
            unsafe
            {
                Glfw glfw = Glfw.GetApi();
                Silk.NET.GLFW.Monitor* monitor = glfw.GetPrimaryMonitor();

                glfw.GetMonitorContentScale(monitor, out _scalingFactor, out _);
            }

            ImGuiController = new(GL, Window, InputContext, SetFont);

            var win32 = Window.Native?.Win32;

            if (win32 is not null)
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

            // If no imgui settings file exists,
            if (!File.Exists(ImguiSettingsFile))
            {
                // Load the default imgui settings file.
                ImGui.LoadIniSettingsFromDisk(Path.Join("Resources", "DefaultLayout.ini"));

                unsafe
                {
                    // Set the settings file's path in imgui.
                    ImGui.GetIO().NativePtr->IniFilename = (byte*)Marshal.StringToCoTaskMemUTF8(ImguiSettingsFile);
                }
            }

            // Set the clear color and depth.
            GL.ClearColor(0.059f, 0.059f, 0.059f, 1f);
            GL.ClearDepth(1);

            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
        };

        Window.Update += delta => ImGuiController?.Update((float)delta);

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

    /// <summary>
    /// A method that is meant to be overriden in order to make any operations
    /// before the window gets closed.
    /// </summary>
    /// <returns>Whether the window can be safely closed.</returns>
    public virtual bool Close() => true;

    private unsafe void SetFont()
    {
        var io = ImGui.GetIO();

        const float sizeScalar = 1.5f; // Render a higher quality font texture for when we want to size up the font

        FontPointers.Add(
            io.Fonts.AddFontFromFileTTF(
                Path.Join("Resources", "NotoSansJP-Regular.ttf"),
                size_pixels: 18 * _scalingFactor * sizeScalar,
                font_cfg: new ImFontConfigPtr(nint.Zero),
                io.Fonts.GetGlyphRangesJapanese()
            )
        );

        FontPointers[0].Scale = 1 / sizeScalar;

        char[] ch = [(char)0xe005, (char)0xf8ff, (char)0];
        fixed (char* glypths = &ch[0])
        {
            FontPointers.Add(
                io.Fonts.AddFontFromFileTTF(
                    Path.Join("Resources", "fa-solid-900.ttf"),
                    size_pixels: 18 * _scalingFactor * sizeScalar,
                    font_cfg: new ImFontConfigPtr(IntPtr.Zero),
                    (nint)glypths
                )
            );
        }

        FontPointers[1].Scale = 1 / sizeScalar;
    }
}
