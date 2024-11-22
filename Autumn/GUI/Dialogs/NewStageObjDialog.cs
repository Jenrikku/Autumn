using System.Numerics;
using Autumn.Enums;
using Autumn.Storage;
using Autumn.Utils;
using Autumn.Wrappers;
using ImGuiNET;

namespace Autumn.GUI.Dialogs;

internal class NewStageObjDialog(MainWindowContext window)
{
    private bool _isOpened = false;

    private string _name = "";
    private string _class = "";
    private string _classSearchQuery = "";
    private bool _prevClassValid = false;
    private int[] _args = [-1, -1, -1, -1, -1, -1, -1, -1];
    private int _objectType = 0;
    private string[] _objectTypeNames = Enum.GetNames<StageObjType>();
    private const ImGuiTableFlags _newObjectClassTableFlags =
        ImGuiTableFlags.ScrollY
        | ImGuiTableFlags.RowBg
        | ImGuiTableFlags.BordersOuter
        | ImGuiTableFlags.BordersV
        | ImGuiTableFlags.Resizable;

    public void Open() => _isOpened = true;

    public void Render()
    {
        if (!_isOpened)
            return;

        ImGui.OpenPopup("Add New Object");

        Vector2 dimensions = new(800, 0);
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Appearing,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Add New Object",
                ref _isOpened,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        if (ImGui.BeginTabBar("ObjectType"))
        {
            if (ImGui.BeginTabItem("Object"))
            {
                bool databaseHasEntry = ClassDatabaseWrapper.DatabaseEntries.TryGetValue(
                    _class,
                    out ClassDatabaseWrapper.DatabaseEntry dbEntry
                );

                ImGui.SetNextItemWidth(400);
                ImGui.InputText("Search", ref _classSearchQuery, 128);
                if (
                    ImGui.BeginTable(
                        "ClassTable",
                        2,
                        _newObjectClassTableFlags,
                        new Vector2(400, 200)
                    )
                )
                {
                    ImGui.TableSetupColumn("ClassName", ImGuiTableColumnFlags.None, 0.5f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 0.5f);
                    ImGui.TableHeadersRow();

                    foreach (var pair in ClassDatabaseWrapper.DatabaseEntries)
                    {
                        if (
                            _classSearchQuery != string.Empty
                            && !pair.Key.ToLower().Contains(_classSearchQuery.ToLower())
                            && !pair.Value.Name.Contains(_classSearchQuery.ToLower())
                        )
                            continue;
                        ImGui.TableNextRow();

                        ImGui.TableSetColumnIndex(0);
                        if (ImGui.Selectable(pair.Key))
                        {
                            _class = pair.Key;
                            databaseHasEntry = false;
                            ResetArgs();
                        }
                        ImGui.TableSetColumnIndex(1);
                        ImGui.Text(pair.Value.Name);
                    }

                    ImGui.EndTable();
                }
                ImGui.SameLine();

                {
                    ImGui.BeginChild("##Desc_Args", new Vector2(380, 210));
                    string description = dbEntry.Description ?? "No Description";
                    if (dbEntry.DescriptionAdditional is not null)
                        description += $"\n{dbEntry.DescriptionAdditional}";
                    ImGui.SetWindowFontScale(1.3f);
                    if (databaseHasEntry)
                        ImGui.Text(dbEntry.Name == " " ? _class : dbEntry.Name);
                    else
                        ImGui.Text(_class);
                    ImGui.SetWindowFontScale(1.0f);
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), _class);

                    ImGui.BeginChild(
                        "##Description",
                        new Vector2(380, 40),
                        ImGuiChildFlags.None,
                        ImGuiWindowFlags.AlwaysVerticalScrollbar
                    );
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    ImGui.TextWrapped(description);
                    ImGui.EndChild();
                    if (
                        ImGui.BeginTable(
                            "ArgTable",
                            4,
                            _newObjectClassTableFlags,
                            new Vector2(380, 130)
                        )
                    )
                    {
                        ImGui.TableSetupColumn("Arg", ImGuiTableColumnFlags.None, 0.2f);
                        ImGui.TableSetupColumn("Val", ImGuiTableColumnFlags.None, 0.35f);
                        ImGui.TableSetupColumn("Name");
                        ImGui.TableSetupColumn("Desc");
                        ImGui.TableHeadersRow();

                        for (int i = 0; i < 8; i++)
                        {
                            string arg = $"Arg{i}";
                            string name = "";
                            string argDescription = "";
                            if (
                                databaseHasEntry
                                && dbEntry.Args is not null
                                && dbEntry.Args.TryGetValue(arg, out var argData)
                            )
                            {
                                if (argData.Name is not null)
                                    name = argData.Name;
                                if (argData.Description is not null)
                                    argDescription = argData.Description;
                                if (!_prevClassValid)
                                    _args[i] = (int)argData.Default;
                            }

                            ImGui.TableNextRow();

                            ImGui.TableSetColumnIndex(0);
                            ImGui.Text(arg);
                            ImGui.TableSetColumnIndex(1);
                            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                            ImGui.DragInt($"##{arg}", ref _args[i]);
                            ImGui.TableSetColumnIndex(2);
                            ImGui.Text(name);
                            ImGui.TableSetColumnIndex(3);
                            bool needScrollbar =
                                ImGui.CalcTextSize(argDescription).X
                                > ImGui.GetContentRegionAvail().X;
                            float ysize =
                                ImGui.GetFont().FontSize
                                * (ImGui.GetFont().Scale * (needScrollbar ? 1.8f : 1.0f));
                            ImGui.BeginChild(
                                $"##ArgDescription{i}",
                                new Vector2(0, ysize),
                                ImGuiChildFlags.None,
                                ImGuiWindowFlags.HorizontalScrollbar
                            );
                            ImGui.Text(argDescription);
                            ImGui.EndChild();
                        }

                        ImGui.EndTable();
                    }

                    ImGui.EndChild();
                }

                float width = ImGui.GetContentRegionAvail().X;
                float spacingX = ImGui.GetStyle().ItemSpacing.X;
                float paddingX = ImGui.GetStyle().FramePadding.X;
                ImGui.PushItemWidth(width * 0.5f);
                ImGui.Text("ObjectName");
                ImGui.SameLine();
                ImGui.SetCursorPosX(width * 0.5f);
                ImGui.Text("ClassName");
                ImGui.PopItemWidth();

                float buttonTextSizeX = ImGui.CalcTextSize("<-").X;
                float objectNameWidth =
                    width * 0.5f - (paddingX * 2 + spacingX * 2 + buttonTextSizeX);
                ImGui.PushItemWidth(objectNameWidth);
                ImGuiWidgets.InputTextRedWhenEmpty("##ObjectName", ref _name, 128);
                ImGui.PopItemWidth();
                ImGui.SameLine();
                if (ImGui.Button("<-"))
                    _name = _class;
                ImGui.SameLine();
                ImGui.PushItemWidth(width * 0.5f);
                if (ImGuiWidgets.InputTextRedWhenEmpty("##ClassName", ref _class, 128))
                    ResetArgs();
                ImGui.PopItemWidth();

                ImGui.SetNextItemWidth(100);
                ImGui.Combo(
                    "Object Type",
                    ref _objectType,
                    _objectTypeNames,
                    _objectTypeNames.Length
                );

                _prevClassValid = databaseHasEntry;
                bool canCreate = _name != string.Empty && _class != string.Empty;
                if (canCreate)
                    ImGui.SameLine();
                if (canCreate && ImGui.Button("Add"))
                {
                    window.AddSceneMouseClickAction(AddQueuedObject);

                    _isOpened = false;
                }

                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Area"))
            {
                ImGui.Text("Currently unsupported");
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Rail"))
            {
                ImGui.Text("Currently unsupported");
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void ResetArgs()
    {
        for (int i = 0; i < 8; i++)
            _args[i] = -1;
    }

    private void AddQueuedObject(MainWindowContext window, Vector4 trans)
    {
        if (window.CurrentScene is null || window.GL is null)
            return;

        StageObj newObj =
            new()
            {
                Type = (StageObjType)_objectType,
                Name = _name,
                ClassName = _class,
                Translation = new(trans.X * 100, trans.Y * 100, trans.Z * 100)
            };

        for (int i = 0; i < 8; i++)
            newObj.Properties.Add($"Arg{i}", _args[i]);

        window.CurrentScene.AddObject(newObj, window.ContextHandler.FSHandler, window.GL);

        if (window.Keyboard?.IsShiftPressed() ?? false)
            window.AddSceneMouseClickAction(AddQueuedObject);
    }
}
