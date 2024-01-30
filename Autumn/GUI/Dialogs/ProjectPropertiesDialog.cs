using System.Numerics;
using Autumn.IO;
using ImGuiNET;

namespace Autumn.GUI.Dialogs;

internal class ProjectPropertiesDialog
{
    private WindowContext _context;

    private bool _isOpened = false;

    private const string _buildPathTooltip =
        "The build path is where the mod files will be saved to.\n"
        + "If you use an emulator, we recommend you to set this to the emulator's mod directory.";

    private const string _useClassNamesTooltip =
        "If enabled, the project will replace use of the CreatorClassNameTable with"
        + "ObjectName/ClassName values individual to objects.\n"
        + "Useful for convenience, but requires an ExeFS patch.";

    private string _name = string.Empty;
    private string _buildPath = string.Empty;
    private bool _buildPathValid = true;
    private bool _useClassNames = false;

    public ProjectPropertiesDialog(WindowContext context) => _context = context;

    public void Open()
    {
        _isOpened = true;

        _name = ProjectHandler.ProjectName;
        _buildPath = ProjectHandler.ProjectBuildOutput;
        _useClassNames = ProjectHandler.UseClassNames;
    }

    public void Render()
    {
        if (!_isOpened)
            return;

        ImGui.OpenPopup("Project Properties");

        Vector2 dimensions = new(480 * _context.ScalingFactor, 0);
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Appearing,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Project Properties",
                ref _isOpened,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        ImGui.TextWrapped("Project: " + ProjectHandler.ProjectSavePath);

        ImGui.Separator();
        ImGui.Spacing();

        // Correctly aline first field:
        float size1 = ImGui.CalcTextSize("Name").X;
        float size2 = ImGui.CalcTextSize("Build path").X;
        float cursorX = ImGui.GetCursorPosX();

        ImGui.SetCursorPosX(cursorX + size2 - size1);

        ImGui.Text("Name: ");
        ImGui.SameLine();
        ImGui.InputText("", ref _name, 255);

        ImGui.Text("Build path: ");
        ImGui.SameLine();

        bool pathExists = Path.Exists(ProjectHandler.ProjectSavePath);

        ImGuiWidgets.DirectoryPathSelector(
            ref _buildPath,
            ref _buildPathValid,
            dialogTitle: "Please specify the project build path",
            dialogDefaultPath: pathExists ? ProjectHandler.ProjectSavePath : null
        );

        ImGui.SameLine();
        ImGuiWidgets.HelpTooltip(_buildPathTooltip);

        ImGui.Spacing();

        ImGui.Text("Use class names: ");
        ImGui.SameLine();
        ImGui.Checkbox("", ref _useClassNames);

        ImGui.SameLine();
        ImGuiWidgets.HelpTooltip(_useClassNamesTooltip);

        ImGuiStylePtr style = ImGui.GetStyle();
        Vector2 buttonSize = new(50 * _context.ScalingFactor, 0);
        ImGui.SetCursorPosX(
            dimensions.X - buttonSize.X * 2 - style.ItemSpacing.X - style.WindowPadding.X
        );

        if (ImGui.Button("Save", buttonSize))
        {
            ImGui.CloseCurrentPopup();
            _isOpened = false;

            ProjectHandler.ProjectName = _name;

            // Only set it if it's different, this allows to keep using the global default if unmodified.
            if (ProjectHandler.UseClassNames != _useClassNames)
                ProjectHandler.UseClassNames = _useClassNames;

            if (_buildPathValid)
                ProjectHandler.ProjectBuildOutput = _buildPath;

            ProjectHandler.SaveProjectFile();
        }

        ImGui.SameLine();

        if (ImGui.Button("Cancel", buttonSize))
        {
            ImGui.CloseCurrentPopup();
            _isOpened = false;
        }

        ImGui.EndPopup();
    }
}
