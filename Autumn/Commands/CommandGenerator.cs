using Autumn.Background;
using Autumn.GUI;
using Autumn.IO;
using Autumn.Storage;
using TinyFileDialogsSharp;

namespace Autumn.Commands;

internal static class CommandGenerator
{
    public static Command NewProject() =>
        new(
            displayName: "New Project",
            action: context =>
            {
                bool success = TinyFileDialogs.SelectFolderDialog(
                    out string? output,
                    title: "Select where to save the new Autumn project.",
                    defaultPath: RecentHandler.LastProjectSavePath
                );

                if (!success)
                    return;

                RecentHandler.LastProjectSavePath = output!;
                ProjectHandler.CreateNewProject(output!);
            },
            enabled: context => true
        );

    public static Command OpenProject() =>
        new(
            displayName: "Open Project",
            action: context =>
            {
                bool success = TinyFileDialogs.OpenFileDialog(
                    out string[]? output,
                    title: "Select the Autumn project file.",
                    defaultPath: RecentHandler.LastProjectOpenPath,
                    filterPatterns: ["*.yml", ".yaml"],
                    filterDescription: "YAML file"
                );

                if (!success)
                    return;

                string projectPath = output![0];
                RecentHandler.LastProjectOpenPath =
                    Path.GetDirectoryName(projectPath) ?? Directory.GetDirectoryRoot(projectPath);

                ProjectHandler.LoadProject(projectPath);
            },
            enabled: context => true
        );

    public static Command Exit() =>
        new(
            displayName: "Exit",
            action: context =>
            {
                var contexts = WindowManager.EnumerateContexts();

                foreach (var activeContext in contexts)
                    activeContext.Window.Close();
            },
            enabled: context => true
        );

    public static Command AddStage() =>
        new(
            displayName: "Add Stage",
            action: context =>
            {
                if (context is not MainWindowContext mainContext)
                    return;

                mainContext.OpenAddStageDialog();
            },
            enabled: context => context is MainWindowContext && ProjectHandler.ProjectLoaded
        );

    public static Command SaveStage() =>
        new(
            displayName: "Save Stage",
            action: context =>
            {
                if (context is not MainWindowContext mainContext)
                    return;

                Stage stage = mainContext.CurrentScene!.Stage;

                mainContext.BackgroundManager.Add(
                    $"Saving stage \"{stage.Name + stage.Scenario}\"...",
                    manager => StageHandler.SaveProjectStage(stage),
                    BackgroundTaskPriority.High
                );
            },
            enabled: context =>
                context is MainWindowContext mainContext && mainContext.CurrentScene is not null
        );

    public static Command Undo() =>
        new(
            displayName: "Undo",
            action: context =>
            {
                if (context is not MainWindowContext mainContext)
                    return;

                mainContext.ChangeHandler.History.Undo();
            },
            enabled: context =>
                context is MainWindowContext mainContext
                && mainContext.CurrentScene is not null
                && mainContext.ChangeHandler.History.CanUndo
        );

    public static Command Redo() =>
        new(
            displayName: "Redo",
            action: context =>
            {
                if (context is not MainWindowContext mainContext)
                    return;

                mainContext.ChangeHandler.History.Redo();
            },
            enabled: context =>
                context is MainWindowContext mainContext
                && mainContext.CurrentScene is not null
                && mainContext.ChangeHandler.History.CanRedo
        );

    public static Command ProjectProperties() =>
        new(
            displayName: "Project properties",
            action: context =>
            {
                if (context is not MainWindowContext mainContext)
                    return;

                mainContext.OpenProjectProperties();
            },
            enabled: context => context is MainWindowContext && ProjectHandler.ProjectLoaded
        );
}
