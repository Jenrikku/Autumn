using System.Numerics;
using Autumn.ActionSystem;
using Autumn.Enums;
using Autumn.GUI.Windows;
using ImGuiNET;
using Silk.NET.Input.Extensions;
using Silk.NET.SDL;

namespace Autumn.GUI.Dialogs;

internal class ShortcutsDialog(MainWindowContext window)
{
    private bool _isOpened = false;
    public bool IsOpen => _isOpened;
    private bool changingKey = false;
    private CommandID? changingCommand = null;
    public void Open()
    {
        _isOpened = true;
        categories = new();
        foreach (CommandID cid in window.ContextHandler.ActionHandler.Actions.Keys)
        {
            var c = window.ContextHandler.ActionHandler.Actions[cid].Command.Category;
            if (!categories.ContainsKey(c))
            {
                categories.Add(c, new());
            }
            categories[c].Add(cid);
        }

    }
    Dictionary<Command.CommandCategory, List<CommandID>> categories = new();
    public void Render()
    {
        if (!_isOpened)
            return;

        ImGui.OpenPopup("Autumn Shortcuts");

        Vector2 dimensions = new(450 * window.ScalingFactor, 300);
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Always,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Autumn Shortcuts",
                ref _isOpened,
                ImGuiWindowFlags.NoResize
                    | ImGuiWindowFlags.NoMove
                    | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;
        if (ImGui.BeginTabBar("ShortcutsTabs"))
        {
            foreach (Command.CommandCategory cat in categories.Keys)
            {
                if (ImGui.BeginTabItem($"{cat}"))
                {
                    if (ImGui.BeginTable("TabTable", 2, ImGuiTableFlags.Resizable 
                                        | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter
                                        | ImGuiTableFlags.BordersV | ImGuiTableFlags.ScrollY))
                    {
                        ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None);
                        ImGui.TableSetupColumn("Keys", ImGuiTableColumnFlags.None);
                        ImGui.TableHeadersRow();
                        foreach (CommandID id in categories[cat])
                        {
                            ImGui.TableNextRow();
                            ShortcutText(id);
                        }
                        ImGui.EndTable();
                    }
                    ImGui.EndTabItem();
                }
            }
        }
        if (changingKey)
        {
            var st = window.Keyboard.CaptureState();
            if (st.GetPressedKeys().Length > 0)
            {
                foreach (Silk.NET.Input.Key k in st.GetPressedKeys())
                {
                    if (k == Silk.NET.Input.Key.Escape)
                    {
                        changingKey = false;
                        changingCommand = null;
                        break;
                    }
                    window.ImGuiController!.TryMapKey(k, out ImGuiKey ik);
                    if (ik == ImGuiKey.LeftCtrl || ik == ImGuiKey.RightCtrl) continue;
                    if (ik == ImGuiKey.LeftShift|| ik == ImGuiKey.RightShift) continue;
                    if (ik == ImGuiKey.LeftAlt  || ik == ImGuiKey.RightAlt) continue;
                    window.ContextHandler.ActionHandler.SetShortcut(changingCommand!.Value, new Shortcut(ImGui.IsKeyDown(ImGuiKey.ModCtrl), 
                                                                                        ImGui.IsKeyDown(ImGuiKey.ModShift), 
                                                                                        ImGui.IsKeyDown(ImGuiKey.ModAlt), ik));
                    changingKey = false;
                    changingCommand = null;
                    break;
                }
            }
        }


        ImGui.EndPopup();
    }

    void ShortcutText(CommandID command)
    {
        var act = window.ContextHandler.ActionHandler.GetAction(command);
        ImGui.TableSetColumnIndex(0);
        if (act.Command != null)
        {
            ImGui.Text(act.Command.DisplayName);
        }
        else return;
        ImGui.TableSetColumnIndex(1);
        string display;
        if (changingKey && changingCommand == command)
        {
            display = "Waiting for key...";
        }
        else
        {
            if (act.Shortcut != null)
            {
                display = act.Shortcut.DisplayString;
            }
            else
            {
                display = "No shortcut assigned.";
            }
        }
        ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, Vector2.One*0.5f);
        if (ImGui.Selectable($"{display}##commandSelectable{command}", false, ImGuiSelectableFlags.SpanAllColumns))
        {
            changingKey = true;
            changingCommand = command;
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Middle))
        {
            window.ContextHandler.ActionHandler.SetShortcut(command, null);
        }
        ImGui.SetItemTooltip(act.Command.DisplayName);
        ImGui.PopStyleVar();
    }
}
