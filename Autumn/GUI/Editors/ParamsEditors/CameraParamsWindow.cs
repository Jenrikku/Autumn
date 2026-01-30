using System.Numerics;
using System.Reflection;
using Autumn.GUI.Windows;
using Autumn.Rendering;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using Autumn.Utils;
using ImGuiNET;

namespace Autumn.GUI.Editors;

internal class CameraParamsWindow(MainWindowContext window)
{
    public bool IsOpen = false;
    private int selectedcam = -1;
    private const ImGuiTableFlags _stageTableFlags = ImGuiTableFlags.RowBg
                | ImGuiTableFlags.BordersOuter
                | ImGuiTableFlags.ScrollY
                | ImGuiTableFlags.Resizable;

    ImGuiWidgets.InputComboBox UserNameCombo = new();
    private List<string> UserNames = ["CameraArea",
                                "CameraOriginArea",
                                "Entrance",
                                "Dokan_出口カメラ",
                                "DokanOutOnly_出口カメラ",
                                "WarpDoor_出口カメラ",
                                "WarpCube_ワープアウトカメラ",
                                "WarpCubeOutOnly_ワープアウトカメラ",
                                "WarpCubeOnce_ワープアウトカメラ",
                                "WarpPorter_ワープアウトカメラ",
                                "WarpAreaPoint",
                                "Pole",
                                "TreeA",
                                "TrickHintPanel",
                                //"BlockNoteSuper_着地",       // Unused object that doesn't exist
                                //"BlockNoteSuper_ジャンプ",        
                                "BlockNoteSuperWide_着地",
                                "BlockNoteSuperWide_ジャンプ",
                                "GyroLauncher_俯瞰",
                                "Punpun_Demo",
                                "Punpun_Default",
                                "Punpun_ShellAttack",
                                "Bunbun_Demo",
                                "Bunbun_Main",
                                "Bunbun_ShellAttack",
                                "BunbunAndPunpunTagObj_Demo",
                                "BunbunVs2_ShellAttack",
                                "BunbunVs2_Main",
                                "BunbunVs2_Demo",
                                "PunpunVs2_ShellAttack",
                                "PunpunVs2_Default",
                                "Koopa_クッパ戦闘終了デモ",
                                "Koopa_クッパ戦闘中",
                                "KoopaLast_クッパダッシュカメラ",
                                "KoopaLast_クッパ最終デモ",
                                "KoopaLast_クッパ戦闘終了デモ",
                                "KoopaLast_クッパ戦闘中",
                                "ゴールポール_DemoGoal",
                                "DemoCamera_DemoOpeningC",
                                "DemoCamera_DemoOpeningB",
                                "DemoCamera_DemoOpeningA",
                                "DemoCamera_DemoEndRollC",
                                "DemoCamera_DemoEndRollB",
                                "DemoCamera_DemoEndRollA"];

    private bool fakebool = false;
    private int fakeint = -1;
    private float fakefl = -1;
    private Vector3 fakev3 = Vector3.Zero;

    private FieldInfo[] StageCameraFields = typeof(StageCamera.CameraProperties).GetFields();
    ImGuiWindowClass windowClass = new() { DockNodeFlagsOverrideSet = ImGuiDockNodeFlags.NoDockingOverCentralNode | ImGuiWidgets.NO_WINDOW_MENU_BUTTON }; // | ImGuiDockNodeFlags.NoUndocking };
    ImGuiWindowClass prevWindowClass = new() { DockNodeFlagsOverrideSet = ImGuiDockNodeFlags.AutoHideTabBar }; // | ImGuiDockNodeFlags.NoUndocking };

    private int previewReference = 2; // Origin, Selection, Mario
    private string[] previewOptions = ["Origin", "Selection", "Mario"];
    private int ratioA = 12;
    private int ratioB = 22;
    float r = 400f / 240f; // 3ds -> 400x240

    public void Render()
    {
        if (!IsOpen)
        {
            return;
        }
        unsafe
        {
            fixed (ImGuiWindowClass* tmp = &windowClass)
                ImGui.SetNextWindowClass(new ImGuiWindowClassPtr(tmp));
        }
        if (!ImGui.Begin("Cameras", ref IsOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.UnsavedDocument))
            return;

        if (window.CurrentScene == null)
        {
            ImGui.TextDisabled("Please load a stage first");
            ImGui.End();
            return;
        }
        var style = ImGui.GetStyle();
        float prevW = ImGui.GetWindowWidth();
        Scene scn = window.CurrentScene;

        ImGuiWidgets.TextHeader("General Vision Params:");

        DragFloat("Near Clip", ref scn.Stage.CameraParams.VisionParam.NearClipDistance, 1, reset_val: 100f, padding: 80);
        DragFloat("Far Clip", ref scn.Stage.CameraParams.VisionParam.FarClipDistance, 1, max: 999999, reset_val: 10000f);
        DragFloat("3D Depth", ref scn.Stage.CameraParams.VisionParam.StereovisionDepth, 0.01f, 0, 1, reset_val: 0.8f);
        DragFloat("3D Distance", ref scn.Stage.CameraParams.VisionParam.StereovisionDistance, 1, reset_val: 350);
        DragFloat("FOV", ref scn.Stage.CameraParams.VisionParam.FovyDegree, 0.1f, 0.1f, 179.9f, reset_val: 45);
        ImGui.NewLine();
        ImGui.Separator();

        float wh = ImGui.GetWindowHeight() / 5.1f / window.ScalingFactor;
        wh = wh < 180 ? 180 : wh;

        ImGuiWidgets.TextHeader("Stage Cameras:");
        if (ImGui.BeginTable("CamSelect", 4, _stageTableFlags,
                new(default, wh)))
        {
            ImGui.TableSetupScrollFreeze(0, 1); // Makes top row always visible.
            ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthStretch, 0.3f);
            ImGui.TableSetupColumn("User", ImGuiTableColumnFlags.None, 1f);
            ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.None, 0.5f);
            ImGui.TableSetupColumn("Class", ImGuiTableColumnFlags.None, 0.5f);
            ImGui.TableHeadersRow();
            for (int _i = 0; _i < scn.Stage.CameraParams.Cameras.Count; _i++)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.PushID("camselect" + _i);
                var st = scn.Stage.CameraParams.Cameras[_i].UserGroupId.ToString();

                if (ImGui.Selectable(st, _i == selectedcam, ImGuiSelectableFlags.SpanAllColumns))
                {
                    selectedcam = _i;
                    scn.SelectedCam = selectedcam;
                }
                ImGui.PopID();

                ImGui.TableSetColumnIndex(1);
                ImGui.Text(scn.Stage.CameraParams.Cameras[_i].UserName);
                ImGui.TableSetColumnIndex(2);
                ImGui.Text(scn.Stage.CameraParams.Cameras[_i].Category.ToString());
                ImGui.TableSetColumnIndex(3);
                ImGui.Text(scn.Stage.CameraParams.Cameras[_i].Class.ToString());

            }
            ImGui.EndTable();
        }

        if (selectedcam != scn.SelectedCam) selectedcam = scn.SelectedCam; // idx in list vs UserGroupId

        if (selectedcam > scn.Stage.CameraParams.Cameras.Count - 1)
            selectedcam = -1;
        if (selectedcam > -1)
        {
            ImGuiWidgets.TextHeader(scn.Stage.CameraParams.Cameras[selectedcam].CameraName());
        }

        if (selectedcam < 0)
            ImGui.BeginDisabled();
        if (ImGui.Button(IconUtils.MINUS + "## remcam", new Vector2(ImGui.GetContentRegionAvail().X / 3, default)))
        {
            scn.Stage.CameraParams.Cameras.RemoveAt(selectedcam);
            selectedcam = -1;
            window.UpdateCameraList();
        }


        ImGui.SameLine();
        if (ImGui.Button(IconUtils.PASTE + "## dupecam", new Vector2(ImGui.GetContentRegionAvail().X / 2, default)))
        {
            scn.Stage.CameraParams.AddCamera(new(scn.Stage.CameraParams.Cameras[selectedcam]));
            window.UpdateCameraList();
        }
        ImGui.SetItemTooltip("Duplicate Camera");

        if (selectedcam < 0)
            ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button(IconUtils.PLUS + "## addcam", new Vector2(ImGui.GetContentRegionAvail().X, default)))
        {
            scn.Stage.CameraParams.AddCamera(new());
            window.UpdateCameraList();
        }

        if (selectedcam > -1)
        {
            ImGuiWidgets.PrePropertyWidthName("Id");
            ImGui.InputInt("##Id", ref scn.Stage.CameraParams.Cameras[selectedcam].UserGroupId, 1);

            var op = ImGui.GetCursorPosX();
            ImGui.Text("UserName:");
            ImGui.SameLine();
            UserNameCombo.Use("UserName", ref scn.Stage.CameraParams.Cameras[selectedcam].UserName, UserNames, ImGuiWidgets.SetPropertyWidthGen("UserName") - 14);
            //ImGui.InputText("##UserName", ref scn.Stage.CameraParams.Cameras[selectedcam].UserName, 128);
            ImGui.SetCursorPosX(op);
            var cls = Enum.GetNames<StageCamera.CameraClass>().ToList().IndexOf(scn.Stage.CameraParams.Cameras[selectedcam].Class.ToString());
            var cat = Enum.GetNames<StageCamera.CameraCategory>().ToList().IndexOf(scn.Stage.CameraParams.Cameras[selectedcam].Category.ToString());

            ImGuiWidgets.PrePropertyWidthName("Class");
            if (ImGui.Combo("##ClassCombo", ref cls, Enum.GetNames<StageCamera.CameraClass>(), 12))
            {
                scn.Stage.CameraParams.Cameras[selectedcam].Class = Enum.Parse<StageCamera.CameraClass>(Enum.GetNames<StageCamera.CameraClass>().ToList()[cls]);
            }

            ImGuiWidgets.PrePropertyWidthName("Category");
            if (ImGui.Combo("##CategoryCombo", ref cat, Enum.GetNames<StageCamera.CameraCategory>(), 4))
            {
                scn.Stage.CameraParams.Cameras[selectedcam].Category = Enum.Parse<StageCamera.CameraCategory>(Enum.GetNames<StageCamera.CameraCategory>().ToList()[cat]);
                window.UpdateCameraList();
            }
            ImGuiWidgets.TextHeader("General Properties");
            List<FieldInfo> renderAfter = [];
            foreach (var CamField in StageCameraFields)
            {
                bool skip = false;
                if (!StageCamera.SpecialProperties.ContainsKey(CamField.Name))
                {
                    CheckField(CamField, scn, skip);
                }
                else if (StageCamera.SpecialProperties.ContainsKey(CamField.Name) && StageCamera.SpecialProperties[CamField.Name].Contains(scn.Stage.CameraParams.Cameras[selectedcam].Class))
                {
                    renderAfter.Add(CamField);
                }
            }

            if (renderAfter.Count > 0)
            {
                ImGuiWidgets.TextHeader(scn.Stage.CameraParams.Cameras[selectedcam].Class.ToString() + " Class Properties");
                foreach (var Camfield in renderAfter)
                {
                    bool skip = false;
                    CheckField(Camfield, scn, skip);
                }
            }

            ImGuiWidgets.TextHeader("Special Properties");

            bool dt = scn.Stage.CameraParams.Cameras[selectedcam].DashAngleTuner != null;

            if (ImGui.Checkbox("Dash Angle Tuner##DATt", ref dt))
            {
                scn.Stage.CameraParams.Cameras[selectedcam].DashAngleTuner = dt ? new() : null;
            }
            if (dt)
            {
                if (ImGui.CollapsingHeader("Dash Angle Tuner", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                    ImGui.BeginChild("dat", default, ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                    InputFloat("AddAngleMax", ref scn.Stage.CameraParams.Cameras[selectedcam].DashAngleTuner!.AddAngleMax, 1, -360, 360);
                    InputFloat("ZoomOutOffsetMax", ref scn.Stage.CameraParams.Cameras[selectedcam].DashAngleTuner!.ZoomOutOffsetMax, 1, min: -10000);
                    ImGui.EndChild();
                }
            }

            bool vo = scn.Stage.CameraParams.Cameras[selectedcam].VelocityOffsetter != null;

            if (ImGui.Checkbox("Velocity Offsetter##Voffs", ref vo))
            {
                scn.Stage.CameraParams.Cameras[selectedcam].VelocityOffsetter = vo ? new() : null;
            }
            if (vo)
            {
                if (ImGui.CollapsingHeader("Velocity Offsetter", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                    ImGui.BeginChild("vot", default, ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                    InputFloat("MaxOffset", ref scn.Stage.CameraParams.Cameras[selectedcam].VelocityOffsetter!.MaxOffset, 1);
                    InputFloat2("MaxOffsetAxisTwo", ref scn.Stage.CameraParams.Cameras[selectedcam].VelocityOffsetter!.MaxOffsetAxisTwo);
                    ImGui.EndChild();
                }
            }

            bool va = scn.Stage.CameraParams.Cameras[selectedcam].VerticalAbsorber != null;

            if (ImGui.Checkbox("Vertical Absorber##Vabs", ref va))
            {
                scn.Stage.CameraParams.Cameras[selectedcam].VerticalAbsorber = va ? new() : null;
            }
            if (va)
            {
                if (ImGui.CollapsingHeader("Vertical Absorber", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                    ImGui.BeginChild("vab", default, ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                    ImGui.Checkbox("IsInvalidate", ref scn.Stage.CameraParams.Cameras[selectedcam].VerticalAbsorber!.IsInvalidate);
                    ImGui.EndChild();
                }
            }

            bool rt = scn.Stage.CameraParams.Cameras[selectedcam].Rotator != null;

            if (ImGui.Checkbox("Rotator##rtt", ref rt))
            {
                scn.Stage.CameraParams.Cameras[selectedcam].Rotator = rt ? new() : null;
            }
            if (rt)
            {
                if (ImGui.CollapsingHeader("Rotator", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                    ImGui.BeginChild("rttts", default, ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                    InputFloat("AngleMax", ref scn.Stage.CameraParams.Cameras[selectedcam].Rotator!.AngleMax, 1, -360, 360);
                    bool b = scn.Stage.CameraParams.Cameras[selectedcam].Rotator!.IsEnable != null ? (bool)scn.Stage.CameraParams.Cameras[selectedcam].Rotator!.IsEnable! : false;
                    ImGui.Checkbox("IsEnable", ref b);
                    scn.Stage.CameraParams.Cameras[selectedcam].Rotator!.IsEnable = b;
                    ImGui.EndChild();
                }
            }


            bool hasVisParam = scn.Stage.CameraParams.Cameras[selectedcam].VisionParam is not null;
            if (ImGui.Checkbox("Vision Params##vpss", ref hasVisParam))
            {
                scn.Stage.CameraParams.Cameras[selectedcam].VisionParam = hasVisParam ? new() : null;

            }
            if (hasVisParam)
            {
                if (ImGui.CollapsingHeader("Vision Params", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                    ImGui.BeginChild("vp", default, ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);

                    DragFloat("Camera 3D Depth", ref scn.Stage.CameraParams.Cameras[selectedcam].VisionParam!.StereovisionDepth, 0.01f, 0, 1, reset_val: 0.8f);
                    DragFloat("Camera 3D Distance", ref scn.Stage.CameraParams.Cameras[selectedcam].VisionParam!.StereovisionDistance, 10, reset_val: 350);
                    DragFloat("Camera FOV", ref scn.Stage.CameraParams.Cameras[selectedcam].VisionParam!.FovyDegree, 0.1f, 0.1f, 179.9f, reset_val: 45);

                    ImGui.EndChild();
                }
            }

            CameraPreviewWindow(scn);
        }
        ImGui.End();

    }

    bool InputFloat(string str, ref float? val, float step, float min = -1, float max = 10000, float reset_val = -1, float padding = 40)
    {
        if (ImGui.Button((val is null ? IconUtils.PLUS : IconUtils.MINUS) + "##" + str + "btn"))
        {
            val = val is null ? reset_val : null;
        }
        ImGui.SameLine();
        ImGui.Text(str + ":");
        ImGui.SameLine();
        float rval = val ?? -1f;
        if (val is null)
            ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen(str, padding: padding));
        if (ImGui.InputFloat("##" + str, ref rval, step) && val != null)
        {
            if (rval <= min) rval = min;
            else if (rval >= max) rval = max;
            val = rval;
            return true;
        }
        if (val is null)
            ImGui.EndDisabled();
        return false;
    }
    bool DragFloat(string str, ref float? val, float step, float min = -1, float max = 10000, float reset_val = -1, float padding = 40)
    {
        if (ImGui.Button((val is null ? IconUtils.PLUS : IconUtils.MINUS) + "##" + str + "btn"))
        {
            val = val is null ? reset_val : null;
        }
        ImGui.SameLine();
        ImGui.Text(str + ":");
        ImGui.SameLine();
        float rval = val ?? -1f;
        if (val is null)
            ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen(str, padding: padding));
        if (ImGui.DragFloat("##" + str, ref rval, step) && val != null)
        {
            if (rval <= min) rval = min;
            else if (rval >= max) rval = max;
            val = rval;
            return true;
        }
        if (val is null)
            ImGui.EndDisabled();
        return false;
    }
    bool InputFloat3(string str, ref Vector3? val)
    {
        if (ImGui.Button((val is null ? IconUtils.PLUS : IconUtils.MINUS) + "##" + str + "btn"))
        {
            val = val is null ? Vector3.Zero : null;
        }
        ImGui.SameLine();
        ImGui.Text(str + ":");
        ImGui.SameLine();
        Vector3 rval = val ?? Vector3.Zero;
        if (val is null)
            ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen(str, padding: 40));
        if (ImGui.DragFloat3("##" + str, ref rval) && val != null)
        {
            val = rval;
            return true;
        }
        if (val is null)
            ImGui.EndDisabled();
        return false;
    }
    bool InputFloat2(string str, ref Vector2? val)
    {
        if (ImGui.Button((val is null ? IconUtils.PLUS : IconUtils.MINUS) + "##" + str + "btn"))
        {
            val = val is null ? Vector2.Zero : null;
        }
        ImGui.SameLine();
        ImGui.Text(str + ":");
        ImGui.SameLine();
        Vector2 rval = val ?? Vector2.Zero;
        if (val is null)
            ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen(str, padding: 40));
        if (ImGui.DragFloat2("##" + str, ref rval) && val != null)
        {
            val = rval;
            return true;
        }
        if (val is null)
            ImGui.EndDisabled();
        return false;
    }
    bool InputInt(string str, ref int? val, int step)
    {
        ImGui.Text(str + ":");
        ImGui.SameLine();
        int rval = val ?? -1;
        if (val is null)
            ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen(str) - 40);
        if (ImGui.InputInt("##" + str, ref rval, step) && val != null)
        {
            val = rval;
            return true;
        }
        if (val is null)
            ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button((val is null ? IconUtils.PLUS : IconUtils.MINUS) + "##" + str + "btn"))
        {
            val = val is null ? -1 : null;
        }
        return false;
    }

    bool CopyVec3Button(bool isCamera)
    {
        bool rb = false;
        if (!isCamera && window.CurrentScene.SelectedObjects.Count() < 1)
            ImGui.BeginDisabled();
        rb = ImGui.Button(isCamera ? IconUtils.CAMERA : IconUtils.USER);
        ImGui.SetItemTooltip(isCamera ? "Copy Camera position" : "Copy selected object position");
        if (!isCamera && window.CurrentScene.SelectedObjects.Count() < 1)
            ImGui.EndDisabled();
        return rb;
    }

    void CheckField(FieldInfo CamField, Scene scn, bool skip)
    {

        var val = CamField.GetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties);

        if (Nullable.GetUnderlyingType(CamField.FieldType) != null)
        {
            if (val is null)
            {
                if (ImGui.Button(IconUtils.PLUS + "##A" + CamField))
                {
                    if (CamField.FieldType == typeof(float?))
                    {
                        CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, 1.0f);
                    }
                    else if (CamField.FieldType == typeof(int?))
                    {
                        CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, -1);
                    }
                    else if (CamField.FieldType == typeof(bool?))
                    {
                        CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, true);
                    }
                    else if (CamField.FieldType == typeof(Vector3?))
                    {
                        CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, Vector3.Zero);
                    }
                }
                ImGui.SameLine();
                ImGui.BeginDisabled();
                if (CamField.FieldType == typeof(float?))
                {
                    ImGuiWidgets.InputFloat(CamField.Name, ref fakefl, 1, ratioA, ratioB);
                }
                else if (CamField.FieldType == typeof(int?))
                {
                    ImGuiWidgets.InputInt(CamField.Name, ref fakeint, 1, ratioA, ratioB);
                }
                else if (CamField.FieldType == typeof(bool?))
                {
                    ImGuiWidgets.PrePropertyWidthName(CamField.Name, ratioA, ratioB);
                    ImGui.Checkbox("##a" + CamField.Name, ref fakebool);
                }
                else if (CamField.FieldType == typeof(Vector3?))
                {
                    ImGuiWidgets.PrePropertyWidthName(CamField.Name, ratioA, ratioB);
                    ImGui.DragFloat3("##fv3", ref fakev3);
                }

                ImGui.EndDisabled();
            }
            else
            {
                if (ImGui.Button(IconUtils.MINUS + "##" + CamField)) // remove the property, only if nullable
                {
                    CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, null);
                    skip = true;
                }
                ImGui.SetItemTooltip("Remove property");
                ImGui.SameLine();
            }
        }
        switch (val)
        {
            case float f:
                ImGuiWidgets.InputFloat(CamField.Name, ref f, 5, ratioA, ratioB);
                if (skip) break;
                CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, f);
                break;
            case int i:
                ImGuiWidgets.InputInt(CamField.Name, ref i, 5, ratioA, ratioB);
                if (skip) break;
                CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, i);
                break;
            case string f:
                ImGuiWidgets.InputText(CamField.Name, ref f, 128);
                if (skip) break;
                CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, f);
                break;
            case bool b:
                ImGuiWidgets.PrePropertyWidthName(CamField.Name, ratioA, ratioB);
                ImGui.Checkbox("##chk" + CamField.Name, ref b);
                if (skip) break;
                CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, b);
                break;
            case Vector3 v:
                ImGuiWidgets.PrePropertyWidthName(CamField.Name, ratioA, ratioB);
                ImGui.DragFloat3("##v3" + CamField.Name, ref v);
                if (skip) break;
                CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, v);
                break;
        }
    }

    void CameraPreviewWindow(Scene scn)
    {
        unsafe
        {
            fixed (ImGuiWindowClass* tmp = &prevWindowClass)
                ImGui.SetNextWindowClass(new ImGuiWindowClassPtr(tmp));
        }
        if (ImGui.Begin("CameraPreview"))
        {

            //ImGui.EndChild();
            StageCamera currcam = scn.Stage.CameraParams.Cameras[selectedcam];
            float mul = 0.01f;
            float addangle = 0;
            bool disrot = false;

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.98f, 0.79f, 0, 1));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.88f, 0.69f, 0, 1));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.98f, 0.89f, 0.4f, 1));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.79f, 0.50f, 0, 1));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 20);
            
            // Camera controls

            // if (currcam.Rotator != null && currcam.Rotator.IsEnable != null && currcam.Rotator.IsEnable == false)
            // {
            //    disrot = true; // This doesn't actually seem to do anything, the only time the rotator is disabled is on AngleMax = 0
            // }
            ImGui.BeginDisabled(disrot);
            ImGui.Button(IconUtils.ARROW_LEFT);
            if (ImGui.IsItemActive())
            {
                if (currcam.Rotator != null && currcam.Rotator.AngleMax != null)
                    addangle = (float)(currcam.Rotator.AngleMax * Math.PI / 180);
                else
                    addangle = (float)(15 * Math.PI / 180);
            }
            if (disrot) ImGui.EndDisabled();
            ImGui.SameLine();

            float dashangle = 0;
            float dashdistance = 0;

            ImGui.Button(IconUtils.DASH);
            if (ImGui.IsItemActive())
            {
                if (currcam.DashAngleTuner == null)
                {
                    dashangle = 15;
                    dashdistance = 100;
                }
                else
                {
                    dashangle = currcam.DashAngleTuner!.AddAngleMax ?? 15;
                    dashdistance = currcam.DashAngleTuner!.ZoomOutOffsetMax ?? 100;
                }
            }
            ImGui.SetItemTooltip("Preview dashing camera");
            ImGui.SameLine();

            ImGui.BeginDisabled(disrot);
            ImGui.Button(IconUtils.ARROW_RIGHT);
            if (ImGui.IsItemActive())
            {
                if (currcam.Rotator != null && currcam.Rotator.AngleMax != null)
                    addangle = -(float)(currcam.Rotator.AngleMax * Math.PI / 180);
                else
                    addangle = -(float)(15 * Math.PI / 180);
            }
            if (disrot) ImGui.EndDisabled();
            ImGui.PopStyleColor(4);
            ImGui.PopStyleVar();
            ImGui.SameLine();
            
            ImGui.Text("Reference:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.Combo("##Camera Reference", ref previewReference, previewOptions, 3);

            Vector3 pos = Vector3.Zero;

            switch (previewReference)
            {
                case 1:
                    var selobj = (IStageSceneObj?)scn.SelectedObjects.FirstOrDefault(x => x is IStageSceneObj);
                    if (selobj != null) pos = selobj.StageObj.Translation * mul;
                    break;
                case 2:
                    var mario = scn.Stage.GetStageFile(Enums.StageFileType.Map).GetObjInfos(Enums.StageObjType.Start).FirstOrDefault();
                    if (mario != null) pos = mario.Translation * mul;
                    break;
            }

            // Preview starts

            var camera = window.CurrentScene!.PreviewCamera;

            //TODO - Make these rotations only in the necessary classes
            camera.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)((currcam.CamProperties.AngleH ?? 0) * Math.PI / 180)); //  + addangle); // ADD ANGLE SHOULD ROTATE THE CAMERA, NOT MOVE IT, SAME PIVOT POINT
            camera.Rotation = Quaternion.Concatenate(Quaternion.CreateFromAxisAngle(-Vector3.UnitX, (float)(((currcam.CamProperties.AngleV ?? 0) + dashangle) * Math.PI / 180)), camera.Rotation);

            float sideangle = currcam.CamProperties.SideDegree ?? 0;
            float sideoff = currcam.CamProperties.SideOffset ?? 0;

            if (currcam.Class == StageCamera.CameraClass.FixAll)
            {
                Vector3 eye = (currcam.CamProperties.CameraPos ?? Vector3.Zero) * mul;
                Vector3 tgt = (currcam.CamProperties.LookAtPos ?? Vector3.Zero) * mul;
                camera.LookAt(eye, tgt);
                camera.LookFrom(new Vector3(
                    eye.X + (sideoff * mul * (float)Math.Cos(sideangle * Math.PI / 180)),
                    eye.Y - 0.5f /*Mario height*/ - (-currcam.CamProperties.UpOffset * mul ?? 0),
                    eye.Z + (sideoff * mul * (float)Math.Sin(sideangle * Math.PI / 180))
                ),
                (currcam.CamProperties.Distance + dashdistance) * mul ?? 0);

                camera.Rotation = Quaternion.Concatenate(Quaternion.CreateFromAxisAngle(-Vector3.UnitX, (float)(dashangle * Math.PI / 180)), camera.Rotation);
            }
            else if (currcam.Class == StageCamera.CameraClass.FixPos)
            {
                // UpOffset does something to this camera type, seems to move the camera but not the pivot? very weird
                Vector3 eye = (currcam.CamProperties.CameraPos ?? Vector3.Zero) * mul;
                camera.LookAt(eye + new Vector3(0f, (-currcam.CamProperties.UpOffset * mul ?? -1.50f), 0f), pos);
                camera.Rotation = Quaternion.Concatenate(Quaternion.CreateFromAxisAngle(-Vector3.UnitX, (float)(dashangle * Math.PI / 180)), camera.Rotation);
                camera.LookFrom(new Vector3(
                    eye.X + (sideoff * mul * (float)Math.Cos(sideangle * Math.PI / 180)),
                    eye.Y - 0.5f /*Mario height*/,
                    eye.Z + (sideoff * mul * (float)Math.Sin(sideangle * Math.PI / 180))
                ),
                dashdistance * mul);

            }
            else if (currcam.Class == StageCamera.CameraClass.FixPosSpot)
            {
                // UpOffset does something to this camera type, seems to move the camera but not the pivot? very weird
                Vector3 eye = (currcam.CamProperties.CameraPos ?? Vector3.Zero) * mul;
                camera.LookAt(eye + new Vector3(0f, (-currcam.CamProperties.UpOffset * mul ?? -1.50f), 0f), pos);
                camera.Rotation = Quaternion.Concatenate(Quaternion.CreateFromAxisAngle(-Vector3.UnitX, (float)(dashangle * Math.PI / 180)), camera.Rotation);
                camera.LookFrom(new Vector3(
                    eye.X + (sideoff * mul * (float)Math.Cos(sideangle * Math.PI / 180)),
                    eye.Y - 0.5f /*Mario height*/,
                    eye.Z + (sideoff * mul * (float)Math.Sin(sideangle * Math.PI / 180))
                ),
                dashdistance * mul);

            }
            else if (currcam.Class == StageCamera.CameraClass.Tower)
            {
                Vector3 tgt = (currcam.CamProperties.Position ?? Vector3.Zero) * mul;
                tgt = new(tgt.X, pos.Y, tgt.Z);
                camera.LookAt(pos, tgt);

                camera.Rotation = Quaternion.Concatenate(Quaternion.CreateFromAxisAngle(-Vector3.UnitX, (float)(((currcam.CamProperties.AngleV ?? 0) + dashangle) * Math.PI / 180)), camera.Rotation);
                camera.LookFrom(new Vector3(
                    pos.X + (sideoff * mul * (float)Math.Cos(sideangle * Math.PI / 180)),
                    pos.Y - 0.5f /*Mario height*/ - (-currcam.CamProperties.UpOffset * mul ?? 0),
                    pos.Z + (sideoff * mul * (float)Math.Sin(sideangle * Math.PI / 180))
                ),
                (currcam.CamProperties.Distance + dashdistance) * mul ?? 0);
            }
            else
            {
                camera.LookFrom(new Vector3(
                    pos.X + (sideoff * mul * (float)Math.Cos(sideangle * Math.PI / 180)),
                    pos.Y - 0.5f /*Mario height*/ - (-currcam.CamProperties.UpOffset * mul ?? 0),
                    pos.Z + (sideoff * mul * (float)Math.Sin(sideangle * Math.PI / 180))
                ),
                (currcam.CamProperties.Distance + dashdistance) * mul ?? 0);
            }

            // apply extra rotations from rotator
            camera.Rotation = Quaternion.Concatenate(camera.Rotation, Quaternion.CreateFromAxisAngle(Vector3.UnitY, addangle));
            camera.Animate(0.01, out Vector3 eyeAnimated, out Quaternion rotAnimated);
            Matrix4x4 viewMatrix = Matrix4x4.CreateTranslation(-eyeAnimated) * Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(rotAnimated));

            float fv = scn.Stage.CameraParams.VisionParam.FovyDegree == null ? (float)(45 * Math.PI / 180) : (float)(scn.Stage.CameraParams.VisionParam.FovyDegree * Math.PI / 180);
            if (currcam.VisionParam != null && currcam.VisionParam.FovyDegree != null)
                fv = (float)(currcam.VisionParam.FovyDegree * Math.PI / 180);
            Matrix4x4 projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
                fv, // set by the camera or globally (ViewParams)
                r,
                1,
                100000f
            );

            // Draw preview
            float ww = ImGui.GetContentRegionAvail().Y > 100 ? ImGui.GetContentRegionAvail().Y : 100;
            window.CameraFramebuffer.SetSize((uint)(ww * r), (uint)(ww));
            window.CameraFramebuffer.Create(window.GL!);

            ImGui.Image(
                new IntPtr(window.CameraFramebuffer.GetColorTexture(0)),
                new Vector2(ww * r, ww),
                new Vector2(0, 1),
                new Vector2(1, 0)
            );

            window.CameraFramebuffer.Use(window.GL!);
            window.GL!.Clear(Silk.NET.OpenGL.ClearBufferMask.ColorBufferBit | Silk.NET.OpenGL.ClearBufferMask.DepthBufferBit);
            window.CurrentScene?.Render(window.GL, viewMatrix, projectionMatrix, camera.Rotation, camera.Eye);
            ImGui.End();
        }
    }
}