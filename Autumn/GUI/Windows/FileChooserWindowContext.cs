using Autumn.Context;
using ImGuiNET;

namespace Autumn.GUI.Windows;

internal abstract class FileChooserWindowContext : WindowContext
{
    public string Title { get; set; } = "Autumn: File Chooser";
    public string DefaultPath { get; set; } = s_home;

    private readonly Queue<string> _history = new();

    private static readonly string s_root = OperatingSystem.IsWindows() ? "C:\\" : "/";
    private static readonly string s_home = Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile
    );

    private const ImGuiWindowFlags _mainWindowFlags =
        ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoScrollWithMouse
        | ImGuiWindowFlags.NoSavedSettings;

    public FileChooserWindowContext(ContextHandler contextHandler, WindowManager windowManager)
        : base(contextHandler, windowManager)
    {
        Window.Load += () =>
        {
            Window.Title = Title;
            Window.Size = new(640, 480);
        };

        Window.Render += (deltaSeconds) =>
        {
            if (ImGuiController is null)
                return;

            ImGuiController.MakeCurrent();

            ImGuiViewportPtr viewport = ImGui.GetMainViewport();

            ImGui.SetNextWindowPos(new(0, 0));
            ImGui.SetNextWindowSize(viewport.Size);

            if (!ImGui.Begin("##FileChooser", _mainWindowFlags))
                return;

            RenderFileChoosePanel();

            GL!.Viewport(Window.FramebufferSize);
            ImGuiController.Render();
        };
    }

    public abstract void RenderFileChoosePanel();
}
