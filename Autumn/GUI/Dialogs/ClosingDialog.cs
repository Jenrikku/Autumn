using System.Numerics;
using Autumn.Enums;
using ImGuiNET;

namespace Autumn.GUI.Dialogs;

/// <summary>
/// A dialog that prompts the user to wait for the background tasks to finish.<br />
/// This dialog is should only be rendered when the window is waiting to close.
/// </summary>
internal class ClosingDialog
{
    private MainWindowContext _context;

    private bool _isOpened = false;

    public ClosingDialog(MainWindowContext context) => _context = context;

    public void Open() => _isOpened = true;

    public void Render()
    {
        if (!_isOpened)
            return;

        ImGui.OpenPopup("##ClosingDialog");

        Vector2 dimensions = new(450 * _context.ScalingFactor, 0);
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Always,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "##ClosingDialog",
                ref _isOpened,
                ImGuiWindowFlags.NoResize
                    | ImGuiWindowFlags.NoMove
                    | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        ImGui.TextWrapped(
            "Please wait for the following tasks to finish before exiting the program:"
        );

        ImGui.Spacing();

        BackgroundTaskPriority lowestPriority = BackgroundTaskPriority.High;
        var tasks = _context.BackgroundManager.GetRemainingTasks(lowestPriority);

        foreach (var (message, _, _) in tasks)
        {
            if (message is null)
                ImGui.BulletText("Undefined task.");
            else
                ImGui.BulletText(message);
        }

        Vector2 buttonSize = new(50 * _context.ScalingFactor, 0);
        ImGui.SetCursorPosX(dimensions.X - buttonSize.X - ImGui.GetStyle().WindowPadding.X);

        if (ImGui.Button("Cancel", buttonSize))
        {
            ImGui.CloseCurrentPopup();
            _isOpened = false;
        }

        if (!tasks.Any())
        {
            _context.BackgroundManager.Stop();
            _context.Window.Close();
        }

        ImGui.EndPopup();
    }
}
