using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace Autumn.GUI;

internal class WindowContext
{
    public IWindow Window { get; protected set; }
    public ImGuiController? ImGuiController { get; protected set; }

    public GL? GL { get; protected set; }

    public IInputContext? InputContext { get; protected set; }
    public IKeyboard? Keyboard { get; protected set; }

    protected readonly string ImguiSettingsFile = Path.Join(
        Path.GetDirectoryName(SettingsHandler.SettingsPath),
        "imgui.ini"
    );

    public WindowContext()
    {
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
            new APIVersion(3, 3)
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

            if (!File.Exists(ImguiSettingsFile))
            {
                File.Copy(Path.Join("Resources", "DefaultLayout.ini"), ImguiSettingsFile);

                unsafe
                {
                    ImGui.GetIO().NativePtr->IniFilename = (byte*)
                        Marshal.StringToCoTaskMemUTF8(ImguiSettingsFile);
                }
            }

            GL.ClearColor(0.059f, 0.059f, 0.059f, 1f);
            GL.ClearDepth(1);

            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
        };

        Window.Update += (delta) => ImGuiController?.Update((float)delta);

        Window.Closing += () => Window.IsClosing = Close();
    }

    public virtual bool Close() => true;
}
