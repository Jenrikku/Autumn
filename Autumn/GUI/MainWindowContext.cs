using Autumn.GUI.Editors;
using Autumn.IO;
using Autumn.Scene;
using Autumn.Storage;
using ImGuiNET;
using Silk.NET.OpenGL;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using TinyFileDialogsSharp;

namespace Autumn.GUI;

internal class MainWindowContext : WindowContext
{
    public List<Scene.Scene> Scenes { get; } = new();
    public Scene.Scene? CurrentScene { get; set; }

    public SceneGL.GLWrappers.Framebuffer SceneFramebuffer { get; }
    public Camera Camera { get; }

    private bool _isFirstFrame = false;

    public MainWindowContext()
        : base()
    {
        SceneFramebuffer = new(
            null,
            SceneGL.PixelFormat.D24_UNorm_S8_UInt,
            SceneGL.PixelFormat.R8_G8_B8_A8_UNorm,
            SceneGL.PixelFormat.R32_UInt
        );
        Camera = new(new Vector3(-10, 7, 10), Vector3.Zero);

        Window.Load += () =>
        {
            Window.Title = "Autumn: Stage Editor";

            InfiniteGrid.Initialize(GL!);
            ModelRenderer.Initialize(GL!);

            ImGuiIOPtr io = ImGui.GetIO();

            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            io.ConfigWindowsMoveFromTitleBarOnly = true;
        };

        Window.Render += (deltaSeconds) =>
        {
            if (ImGuiController is null)
                return;

            ImGuiController.MakeCurrent();

            float h = RenderMainMenuBar();

            #region DockSpace

            ImGuiViewportPtr viewport = ImGui.GetMainViewport();
            Vector2 menuBar = new(0, h);

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

            ImGui.SetNextWindowPos(viewport.Pos + menuBar);
            ImGui.SetNextWindowSize(viewport.Size - menuBar * 2);
            ImGui.SetNextWindowViewport(viewport.ID);

            ImGui.Begin(
                "mainDockSpaceWindow",
                ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBringToFrontOnFocus
            );

            ImGui.DockSpace(ImGui.GetID("mainDockSpace"));
            ImGui.End();
            ImGui.PopStyleVar(2);

            #endregion

            if (ProjectHandler.ActiveProject.Stages.Count <= 0)
                RenderNoProjectScreen();
            else
                RenderEditors(deltaSeconds);

            GL!.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL!.Clear(ClearBufferMask.ColorBufferBit);
            GL!.Viewport(Window.FramebufferSize);
            ImGuiController.Render();
        };
    }

    private float RenderMainMenuBar()
    {
        if (!ImGui.BeginMainMenuBar())
            return 0;

        if (ImGui.BeginMenu("Project"))
        {
            if (ImGui.MenuItem("New") && SaveAsDialog(out string? createDir))
                ProjectHandler.CreateNew(createDir);

            if (ImGui.MenuItem("Open"))
            {
                bool success = TinyFileDialogs.OpenFileDialog(
                    out string[]? output,
                    title: "Select the Autumn project file.",
                    filterPatterns: new string[] { "autumnproj.yml" },
                    filterDescription: "Autumn project file (autumnproj.yml)"
                );

                if (success)
                {
                    string projectPath = output![0];

                    ProjectHandler.LoadProject(projectPath);
                }
            }

            if (ImGui.MenuItem("Save"))
            {
                if (string.IsNullOrEmpty(ProjectHandler.ActiveProject.SavePath))
                {
                    if (SaveAsDialog(out string? saveDir))
                        ProjectHandler.SaveProject(saveDir);
                }
                else
                    ProjectHandler.SaveProject();
            }

            if (ImGui.MenuItem("Save as...") && SaveAsDialog(out string? saveAsDir))
                ProjectHandler.SaveProject(saveAsDir);

            static bool SaveAsDialog([NotNullWhen(true)] out string? output)
            {
                bool success = TinyFileDialogs.SelectFolderDialog(
                    out output,
                    title: "Select where to save the Autumn project."
                );

                output = Path.Join(output, "autumnproj.yml");
                return success;
            }

            ImGui.Separator();

            if (
                ImGui.MenuItem("Exit") /* && Project.Unload() */
            )
                Window.Close();

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Stage"))
        {
            ImGui.MenuItem("Import from romfs");
            //ImGui.MenuItem("Import through world map selector");

            ImGui.EndMenu();
        }

        #region SceneTabs

        ImGuiTabBarFlags barFlags = ImGuiTabBarFlags.AutoSelectNewTabs;
        int sceneCount = Scenes.Count;

        if (sceneCount > 0 && ImGui.BeginTabBar("sceneTabs", barFlags))
        {
            for (int i = 0; i < sceneCount; i++)
            {
                ImGuiTabItemFlags flags = ImGuiTabItemFlags.NoPushId;

                Scene.Scene scene = Scenes[i];

                //if(!scene.Saved)
                //    flags |= ImGuiTabItemFlags.UnsavedDocument;

                bool opened = true;

                ImGui.PushID("Scene" + i);

                if (
                    ImGui.BeginTabItem(
                        scene.Stage.Name + "Stage" + scene.Stage.Scenario ?? string.Empty,
                        ref opened,
                        flags
                    )
                    && CurrentScene != scene
                )
                {
                    CurrentScene = scene;
                }

                ImGui.EndTabItem();

                ImGui.PopID();

                //if(!opened && CloseSceneAt(i))
                //    i--;
            }

            ImGui.EndTabBar();
        }

        #endregion

        float height = ImGui.GetItemRectSize().Y;

        ImGui.EndMainMenuBar();

        return height;
    }

    private void RenderEditors(double deltaSeconds)
    {
        StageWindow.Render(this);
        ObjectWindow.Render(this);
        SceneWindow.Render(this, deltaSeconds);
    }

    private static void RenderNoProjectScreen()
    {
        ImGuiWindowFlags flags =
            ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoBackground
            | ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.NoInputs
            | ImGuiWindowFlags.NoSavedSettings;

        ImGui.SetNextWindowPos(
            ImGui.GetWindowViewport().GetCenter(),
            ImGuiCond.Always,
            new(0.5f, 0.5f)
        );

        if (!ImGui.Begin("##", flags))
            return;

        ImGui.TextDisabled("Please, open a project from the menu or drop a folder here.");

        ImGui.End();
    }

    //private void OpenProject() {
    //    if(NFDEx.OpenDialogMultiple(out string[] paths) == NFDExResult.Ok)
    //        // [!] To-Do
    //}

    //private bool SaveProject() {
    //    if(string.IsNullOrEmpty(FileHolder.CurrentMetadata!.Value.Path))
    //        return SaveProjectAs();

    //    // [!] To-Do
    //}

    //private bool SaveProjectAs() {
    //    // [!] To-Do
    //}

    //private bool CloseProjectAt(int index) {
    //    // [!] To-Do
    //}
}
