using System.Numerics;
using System.Reflection;
using Autumn.GUI.Windows;
using Autumn.Rendering;
using Autumn.Storage;
using Autumn.Utils;
using ImGuiNET;
using Silk.NET.Input;

namespace Autumn.GUI.Editors;

internal class CameraParamsWindow(MainWindowContext window)
{
    int selectedcam = -1;
    public bool _isOpen = true;

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

    private FieldInfo[] StageCameraFields = typeof(StageCamera.CameraProperties).GetFields();
    public void Render()
    {
        if (!_isOpen)
        {
            return;
        }
        unsafe
        {
            ImGuiWindowClass windowClass = new() { DockNodeFlagsOverrideSet = ImGuiDockNodeFlags.NoDockingOverCentralNode | ImGuiWidgets.NO_WINDOW_MENU_BUTTON}; // | ImGuiDockNodeFlags.NoUndocking };
            ImGuiWindowClass* tmp = &windowClass;
            ImGui.SetNextWindowClass(new ImGuiWindowClassPtr(tmp));
        }
        if (!ImGui.Begin("Cameras", ref _isOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.UnsavedDocument))
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

        InputFloat("Near Clip", ref scn.Stage.CameraParams.VisionParam.NearClipDistance, 1);
        InputFloat("Far Clip", ref scn.Stage.CameraParams.VisionParam.FarClipDistance, 1);
        InputFloat("3D Depth", ref scn.Stage.CameraParams.VisionParam.StereovisionDepth, 1);
        InputFloat("3D Distance", ref scn.Stage.CameraParams.VisionParam.StereovisionDistance, 1);
        InputFloat("FOV", ref scn.Stage.CameraParams.VisionParam.FovyDegree, 1);
        ImGui.NewLine();
        ImGui.Separator();

        ImGuiWidgets.TextHeader("Stage Cameras:");
        if (ImGui.BeginTable("CamSelect", 4, _stageTableFlags,
                new(default, ImGui.GetWindowHeight() / 5.1f / window.ScalingFactor)))
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
                }
                ImGui.PopID();

                ImGui.TableSetColumnIndex(1);
                ImGui.Text(scn.Stage.CameraParams.Cameras[_i].UserName);//scn.GetFogCount(_i).ToString());
                ImGui.TableSetColumnIndex(2);
                ImGui.Text(scn.Stage.CameraParams.Cameras[_i].Category.ToString());//scn.GetFogCount(_i).ToString());
                ImGui.TableSetColumnIndex(3);
                ImGui.Text(scn.Stage.CameraParams.Cameras[_i].Class.ToString());//scn.GetFogCount(_i).ToString());

            }
            ImGui.EndTable();
        }

        if (selectedcam > scn.Stage.CameraParams.Cameras.Count - 1)
            selectedcam = -1;
        if (selectedcam > -1)
        {
            ImGuiWidgets.TextHeader(scn.Stage.CameraParams.Cameras[selectedcam].CameraName());
        }

        if (selectedcam < 0)
            ImGui.BeginDisabled();
        if (ImGui.Button(IconUtils.MINUS + "## remcam", new Vector2(ImGui.GetWindowWidth() / 3 - 8, default)))
        {
            scn.Stage.CameraParams.Cameras.RemoveAt(selectedcam);
            selectedcam = -1;
        }


        ImGui.SameLine();
        if (ImGui.Button(IconUtils.PASTE + "## dupecam", new Vector2(ImGui.GetWindowWidth() / 3 - 8, default)))
        {
            scn.Stage.CameraParams.Cameras.Add(new(scn.Stage.CameraParams.Cameras[selectedcam]) { UserGroupId = 333 });
        }
        ImGui.SetItemTooltip("Duplicate Camera");

        if (selectedcam < 0)
            ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button(IconUtils.PLUS + "## addcam", new Vector2(ImGui.GetWindowWidth() / 3 - 8, default)))
        {
            scn.Stage.CameraParams.Cameras.Add(new() { UserGroupId = 333 });
        }

        if (selectedcam > -1)
        {
            ImGui.BeginChild("CmaeraPorps");
            //ImGuiWidgets.PrePropertyWidthName("Id");
            ImGui.Text("Id:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputInt("##Id", ref scn.Stage.CameraParams.Cameras[selectedcam].UserGroupId, 1);

            var op = ImGui.GetCursorPosX();
            ImGui.Text("UserName:");
            ImGui.SameLine();
            UserNameCombo.Use("UserName", ref scn.Stage.CameraParams.Cameras[selectedcam].UserName, UserNames, ImGuiWidgets.SetPropertyWidthGen("UserName", 20, 30));
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
            }
            ImGuiWidgets.TextHeader("General Properties");
            //typeof(StageCamera).GetField(s);
            foreach (var CamField in StageCameraFields)
            {
                var val = CamField.GetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties);
                if (!StageCamera.SpecialProperties.ContainsKey(CamField.Name))
                {
                    switch (val)
                    {
                        case float f:
                            ImGui.InputFloat(CamField.Name, ref f, 1);
                            CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, f);
                            break;
                        case int i:
                            ImGui.InputInt(CamField.Name, ref i, 1);
                            CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, i);
                            break;
                        case string f:
                            ImGui.InputText(CamField.Name, ref f, 128);
                            CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, f);
                            break;
                        case bool b:
                            ImGui.Checkbox(CamField.Name, ref b);
                            CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, b);
                            break;
                    }
                    if (Nullable.GetUnderlyingType(CamField.FieldType) != null)
                    {
                        if (val is null)
                        {
                            if (ImGui.Button("Add " + CamField.Name))
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
                            }
                        }
                        else
                        {
                            ImGui.SameLine();
                            if (ImGui.Button(IconUtils.MINUS + "##"+CamField)) // remove the property, only if nullable
                            {
                                CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, null);
                            }   
                        }
                    }
                }
            }

            ImGui.TextColored(new Vector4(0.4f, 1, 0, 1), "WEIRD TEST ENDS HERE!!!");
            
            bool js = false;
            foreach (var CamField in StageCameraFields)
            {
                if (!StageCamera.SpecialProperties.ContainsKey(CamField.Name)) continue;
                var val = CamField.GetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties);
                if (StageCamera.SpecialProperties[CamField.Name].Contains(scn.Stage.CameraParams.Cameras[selectedcam].Class))
                {

                    if (!js) 
                    {
                        ImGuiWidgets.TextHeader(scn.Stage.CameraParams.Cameras[selectedcam].Class.ToString() + " Class Properties");
                        js = true;
                    }
                    switch (val)
                    {
                        case float f:
                            ImGui.InputFloat(CamField.Name, ref f, 1);
                            CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, f);
                            break;
                        case int i:
                            ImGui.InputInt(CamField.Name, ref i, 1);
                            CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, i);
                            break;
                        case string f:
                            ImGui.InputText(CamField.Name, ref f, 128);
                            CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, f);
                            break;
                        case bool b:
                            ImGui.Checkbox(CamField.Name, ref b);
                            CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, b);
                            break;
                        case Vector3 v:
                            ImGui.InputFloat3(CamField.Name, ref v);
                            CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, v);
                            break;
                    }
                    if (Nullable.GetUnderlyingType(CamField.FieldType) != null)
                    {
                        if (val is null)
                        {
                            if (ImGui.Button("Add " + CamField.Name))
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
                        }
                        else
                        {
                            ImGui.SameLine();
                            if (ImGui.Button(IconUtils.MINUS + "##"+CamField)) // remove the property, only if nullable
                            {
                                CamField.SetValue(scn.Stage.CameraParams.Cameras[selectedcam].CamProperties, null);
                            }   
                        }
                    }
                        
                    }
            }

            ImGuiWidgets.TextHeader("Special Properties");


            if (ImGui.Checkbox("Limit Box##limbox", ref scn.Stage.CameraParams.Cameras[selectedcam].HasLimitBox))
            {
                if (!scn.Stage.CameraParams.Cameras[selectedcam].HasLimitBox)
                {
                    scn.Stage.CameraParams.Cameras[selectedcam].CamProperties.LimitBoxMin = null;
                    scn.Stage.CameraParams.Cameras[selectedcam].CamProperties.LimitBoxMax = null;
                }
                else
                {
                    scn.Stage.CameraParams.Cameras[selectedcam].CamProperties.LimitBoxMin = Vector3.Zero;
                    scn.Stage.CameraParams.Cameras[selectedcam].CamProperties.LimitBoxMax = Vector3.Zero;
                }
            }

            if (scn.Stage.CameraParams.Cameras[selectedcam].HasLimitBox)
            {
                if (ImGui.CollapsingHeader("Limit Box", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y);
                    ImGui.BeginChild("lbox", default, ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                    InputFloat3("LimitBoxMax", ref scn.Stage.CameraParams.Cameras[selectedcam].CamProperties.LimitBoxMax);
                    InputFloat3("LimitBoxMin", ref scn.Stage.CameraParams.Cameras[selectedcam].CamProperties.LimitBoxMin);
                    ImGui.EndChild();
                }
            }

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
                    InputFloat("AddAngleMax", ref scn.Stage.CameraParams.Cameras[selectedcam].DashAngleTuner!.AddAngleMax, 1);
                    InputFloat("ZoomOutOffsetMax", ref scn.Stage.CameraParams.Cameras[selectedcam].DashAngleTuner!.ZoomOutOffsetMax, 1);
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
                    InputFloat("AngleMax", ref scn.Stage.CameraParams.Cameras[selectedcam].Rotator!.AngleMax, 1);
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
                    InputFloat("3D Depth", ref scn.Stage.CameraParams.Cameras[selectedcam].VisionParam!.StereovisionDepth, 1);
                    InputFloat("3D Distance", ref scn.Stage.CameraParams.Cameras[selectedcam].VisionParam!.StereovisionDistance, 1);
                    InputFloat("FOV", ref scn.Stage.CameraParams.Cameras[selectedcam].VisionParam!.FovyDegree, 1);
                    ImGui.EndChild();
                }
            }
            ImGui.EndChild();
        }
        
        // window.CurrentScene!.Camera.Animate(0.1, out Vector3 eyeAnimated, out Quaternion rotAnimated);

        // Matrix4x4 viewMatrix =
        //     Matrix4x4.CreateTranslation(-eyeAnimated) * Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(rotAnimated));

        // Matrix4x4 projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
        //     (float)(Math.PI * 0.001),
        //     0.2f,
        //     1,
        //     100000f
        // );

        // window.SceneFramebuffer.SetSize((uint)ImGui.GetContentRegionAvail().X, (uint)ImGui.GetContentRegionAvail().Y);
        // window.SceneFramebuffer.Create(window.GL!);

        // ImGui.Image(
        //     new IntPtr(window.SceneFramebuffer.GetColorTexture(0)),
        //     ImGui.GetContentRegionAvail(),
        //     new Vector2(0, 1),
        //     new Vector2(1, 0)
        // );

        // window.SceneFramebuffer.Use(window.GL!);
        // window.GL!.Clear(Silk.NET.OpenGL.ClearBufferMask.ColorBufferBit | Silk.NET.OpenGL.ClearBufferMask.DepthBufferBit);
        // window.CurrentScene?.Render(window.GL, viewMatrix, projectionMatrix, window.CurrentScene.Camera.Rotation);

    }

    bool InputFloat(string str, ref float? val, int step)
    {
        ImGui.Text(str + ":");
        ImGui.SameLine();
        float rval = val ?? -1f;
        if (val is null)
            ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(ImGuiWidgets.SetPropertyWidthGen(str) - 40);
        if (ImGui.DragFloat("##" + str, ref rval, step) && val != null)
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
    bool InputFloat3(string str, ref Vector3? val)
    {
        Vector3 rval = val ?? Vector3.Zero;
        if (val is null)
            ImGui.BeginDisabled();
        if (ImGui.InputFloat3(str, ref rval) && val != null)
        {
            val = rval;
            return true;
        }
        if (val is null)
            ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button((val is null ? IconUtils.PLUS : IconUtils.MINUS) + "##" + str + "btn"))
        {
            val = val is null ? Vector3.Zero : null;
        }
        return false;
    }
    bool InputFloat2(string str, ref Vector2? val)
    {
        Vector2 rval = val ?? Vector2.Zero;
        if (val is null)
            ImGui.BeginDisabled();
        if (ImGui.InputFloat2(str, ref rval) && val != null)
        {
            val = rval;
            return true;
        }
        if (val is null)
            ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button((val is null ? IconUtils.PLUS : IconUtils.MINUS) + "##" + str + "btn"))
        {
            val = val is null ? Vector2.Zero : null;
        }
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

}