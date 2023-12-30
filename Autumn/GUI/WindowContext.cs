using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace Autumn.GUI;

/// <summary>
/// This class serves as a base for all windows.<br />
/// A window context contains all the necessary data related to the window.
/// </summary>
/// <seealso cref="WindowManager" />
internal abstract class WindowContext
{
    public IWindow Window { get; protected set; }
    public ImGuiController? ImGuiController { get; protected set; }

    public GL? GL { get; protected set; }

    public IInputContext? InputContext { get; protected set; }
    public IKeyboard? Keyboard { get; protected set; }

    /// <summary>
    /// Specifies where the "imgui.ini" file is stored in.
    /// By default, it will be stored within Autumn's settings path.
    /// </summary>
    protected readonly string ImguiSettingsFile = Path.Join(
        Path.GetDirectoryName(SettingsHandler.SettingsPath),
        "imgui.ini"
    );

    public WindowContext()
    {
        WindowOptions options = WindowOptions.Default;

        options.VSync = true;
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

            lock (new object())
                ImGuiController = new(GL, Window, InputContext);

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
                // Copy the default imgui settings file.
                File.Copy(Path.Join("Resources", "DefaultLayout.ini"), ImguiSettingsFile);

                unsafe
                {
                    // Set the settings file's path in imgui.
                    ImGui.GetIO().NativePtr->IniFilename = (byte*)
                        Marshal.StringToCoTaskMemUTF8(ImguiSettingsFile);
                }
            }

            // Set the clear color and depth.
            GL.ClearColor(0.059f, 0.059f, 0.059f, 1f);
            GL.ClearDepth(1);

            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
        };

        Window.Update += (delta) => ImGuiController?.Update((float)delta);

        Window.Closing += () => Window.IsClosing = Close();
    }

    /// <summary>
    /// A method that is meant to be overriden in order to make any operations
    /// before the window gets closed.
    /// </summary>
    /// <returns>Whether the window can be safely closed.</returns>
    public virtual bool Close() => true;
}
