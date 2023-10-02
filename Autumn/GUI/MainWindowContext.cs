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
        Window.Title = "Autumn: Stage Editor";

        SceneFramebuffer = new(
            null,
            SceneGL.PixelFormat.D24_UNorm_S8_UInt,
            SceneGL.PixelFormat.R8_G8_B8_A8_UNorm,
            SceneGL.PixelFormat.R32_UInt
        );
        Camera = new(new Vector3(-10, 7, 10), Vector3.Zero);

        Window.Load += () =>
        {
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

            if (!ProjectHandler.ProjectLoaded)
                RenderNoProjectScreen();
            else
                RenderEditors(deltaSeconds);

            if (_stageSelectOpen)
                RenderStageSelectPopup();

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
                    defaultPath: RecentHandler.LastProjectOpenPath,
                    filterPatterns: new string[] { "*.yml", ".yaml" },
                    filterDescription: "YAML file"
                );

                if (success)
                {
                    string projectPath = output![0];
                    RecentHandler.LastProjectOpenPath =
                        Path.GetDirectoryName(projectPath)
                        ?? Directory.GetDirectoryRoot(projectPath);

                    ProjectHandler.LoadProject(projectPath);
                }
            }

            if (!ProjectHandler.ProjectLoaded)
                ImGui.BeginDisabled();

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
                    title: "Select where to save the Autumn project.",
                    defaultPath: RecentHandler.LastProjectSavePath
                );

                if (success)
                {
                    RecentHandler.LastProjectSavePath = output!;
                    output = Path.Join(output, "autumnproj.yml");
                }

                return success;
            }

            ImGui.EndDisabled();

            if (ImGui.BeginMenu("Recent"))
            {
                if (RecentHandler.RecentOpenedPaths.Count <= 0)
                    ImGui.TextDisabled("There are no recent entries.");
                else
                {
                    foreach (string path in RecentHandler.RecentOpenedPaths)
                        if (ImGui.Selectable(path))
                            ProjectHandler.LoadProject(path);
                }

                ImGui.EndMenu();
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
            if (ImGui.MenuItem("Import from romfs"))
                _stageSelectOpen |= true;

            //ImGui.MenuItem("Import through world map selector");

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Help"))
        {
            if (ImGui.MenuItem("Show welcome window"))
                WindowManager.Add(new WelcomeWindowContext());

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

    private bool _stageSelectOpen = false;
    private string _stageSearchInput = string.Empty;

    private void RenderStageSelectPopup()
    {
        if (!RomFSHandler.RomFSAvailable)
        {
            _stageSelectOpen = false;
            return;
        }

        ImGui.OpenPopup("Stage selector");

        Vector2 dimensions = new Vector2(450, 185) + ImGui.GetStyle().ItemSpacing;
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Appearing,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Stage selector",
                ref _stageSelectOpen,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        ImGui.Text("Search:");
        ImGui.SameLine();

        ImGui.SetNextItemWidth(450 - ImGui.GetCursorPosX());

        ImGui.InputTextWithHint(
            "",
            "Insert the name of the stage here.",
            ref _stageSearchInput,
            128
        );

        ImGui.SetNextItemWidth(450 - ImGui.GetCursorPosX());

        if (ImGui.BeginListBox(""))
        {
            foreach (var (name, scenario) in RomFSHandler.StageNames)
            {
                if (
                    !string.IsNullOrEmpty(_stageSearchInput)
                    && !name.Contains(
                        _stageSearchInput,
                        StringComparison.InvariantCultureIgnoreCase
                    )
                )
                    continue;

                // Does not show the stage if there is an already opened stage that matches the name:
                // foreach (
                //     var stage in ProjectHandler.ActiveProject.Stages.Where(
                //         stage => stage.Name == name && stage.Scenario == scenario
                //     )
                // )
                //     continue;

                if (ImGui.Selectable(name + scenario, false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    // [!] This should be handled by the ProjectHandler in the future.

                    StageHandler.TryImportStage(name, scenario, out Stage stage);
                    ProjectHandler.ActiveProject.Stages.Add(stage);

                    _stageSelectOpen = false;
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        ImGui.EndPopup();
    }
}
