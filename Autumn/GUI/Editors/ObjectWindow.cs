using Autumn.Scene;
using Autumn.Storage.StageObjs;
using ImGuiNET;

namespace Autumn.GUI.Editors;

internal class ObjectWindow
{
    private static int _objectFilterCurrent = 0;

    private const ImGuiTableFlags _objectTableFlags =
        ImGuiTableFlags.ScrollY
        | ImGuiTableFlags.RowBg
        | ImGuiTableFlags.BordersOuter
        | ImGuiTableFlags.BordersV
        | ImGuiTableFlags.Resizable;

    public static void Render(MainWindowContext context)
    {
        if (!ImGui.Begin("Objects"))
            return;

        if (context.CurrentScene is null)
        {
            ImGui.TextDisabled("Please open a stage.");
            return;
        }

        ImGui.SetNextItemWidth(ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X * 2);

        ImGui.Combo(
            "",
            ref _objectFilterCurrent,
            "All Objects\0Regular Objects\0Areas\0Camera Areas\0Goals\0Event Starts\0Start Objects",
            7
        );

        if (ImGui.BeginTable("objectTable", 2, _objectTableFlags))
        {
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            ImGui.TableSetupColumn("Object");
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.35f);
            ImGui.TableHeadersRow();

            foreach (SceneObj obj in context.CurrentScene.SceneObjects)
            {
                IStageObj stageObj = obj.StageObj;
                var (index, typeName) = GetStageObjectType(stageObj);

                if (_objectFilterCurrent != 0 && _objectFilterCurrent != index)
                    continue;

                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);

                ImGui.Selectable(stageObj.Name);

                ImGui.TableNextColumn();

                ImGui.Text(typeName);
            }

            ImGui.EndTable();
        }

        ImGui.End();
    }

    private static (int index, string name) GetStageObjectType(IStageObj obj) =>
        obj switch
        {
            IStageObj o when o is RegularStageObj => (1, "Regular Object"),
            IStageObj o when o is AreaStageObj => (2, "Area"),
            IStageObj o when o is CameraAreaStageObj => (3, "Camera Area"),
            IStageObj o when o is GoalStageObj => (4, "Goal"),
            IStageObj o when o is StartEventStageObj => (5, "Event Starter"),
            IStageObj o when o is StartStageObj => (6, "Start Object"),
            _ => (0, "Unknown")
        };
}
