using System.Numerics;
using Autumn.GUI.Windows;
using Autumn.Storage;
using ImGuiNET;

namespace Autumn.GUI.Dialogs;

/// <summary>
/// A dialog that allows the user to edit properties for an object.
/// </summary>
internal class EditExtraPropsDialog(MainWindowContext _window)
{

    private bool _isOpened = false;
    private bool _isnew = false;
    private string _oname = "";
    private string _name = "NewProperty";
    private int _type = 0;
    private string[] _types = ["int", "float", "bool", "string"]; // int, float, bool, string?
    private object _value = -1f;
    private StageObj obj;
    private string[] _properties;
    Vector2 dimensions = new(340, 170);


    public void Open(StageObj stageObj, string propName)
    {

        Reset();

        _name = propName;
        _oname = _name;
        _value = stageObj.Properties[propName]!;
        _properties = stageObj.Properties.Keys.ToArray();
        switch (_value)
        {
            case int:
                _type = 0;
                break;
            case float:
                _type = 1;

                break;
            case bool:
                _type = 2;

                break;
            case string:
                _type = 3;

                break;
            default:
                throw new NotImplementedException(
                    "The property type " + _value?.GetType().FullName
                        ?? "null" + " is not supported."
                );
        };
        _isOpened = true;
        obj = stageObj;
    }
    public void New(StageObj stageObj)
    {

        Reset();
        _properties = stageObj.Properties.Keys.ToArray();
        _isOpened = true;
        _isnew = true;
        obj = stageObj;
    }

    /// <summary>
    /// Resets all values from this dialog to their defaults.
    /// </summary>
    public void Reset()
    {
        dimensions = new Vector2(340, 170) * _window.ScalingFactor;
        _name = "NewProperty";
        _oname = "NewProperty";
        _value = -1f;
        _isnew = false;
        _isOpened = false;
    }

    public void Render()
    {
        if (!_isOpened)
            return;

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Reset();
            ImGui.CloseCurrentPopup();
        }
        ImGui.OpenPopup(_isnew ? "Add property" : "Modify property");

        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, new(0.5f, 0.5f));

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Always,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(_isnew ? "Add property" : "Modify property",
                ref _isOpened)
        )
            return;

        dimensions = ImGui.GetWindowSize();

        ImGui.Text("Property name:"); ImGui.SameLine(); 
        ImGuiWidgets.SetPropertyWidth("Property name");
        ImGui.InputText("##nmed", ref _name, 128);
        bool repeat = _isnew ? _properties.Contains(_name) : _properties.Contains(_name) && _name != _oname;
        

        if (repeat)
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "This object already contains a property with that name!");
        }

        ImGui.Text("Property type:"); ImGui.SameLine(); 
        ImGuiWidgets.SetPropertyWidth("Property type");
        ImGui.Combo("##ncom", ref _type, _types, 4);

        ImGui.Text("Property value:"); ImGui.SameLine();
        ImGuiWidgets.SetPropertyWidth("Property value");
        switch (_type)
        {
            case 0:
            int v = -1;
            if (_value is int) v = (int)_value;
            ImGui.InputInt("##intput", ref v, 1);
            _value = v;
            break;
            case 1:
            float f = -1;
            if (_value is float) f = (float)_value;
            ImGui.InputFloat("##flput", ref f, 1);
            _value = f;
            break;
            case 2:
            bool b = false;
            if (_value is bool) b = (bool)_value;
            ImGui.Checkbox("##chput", ref b);
            _value = b;
            break;
            case 3:
            string s = "-";
            if (_value is string) s = (string)_value;
            ImGui.InputText("##txtput", ref s, 128);
            _value = s;
            break;
        }

        ImGui.SetCursorPosX(dimensions.X - 90);
        ImGui.SetCursorPosY(dimensions.Y - ImGui.GetTextLineHeight() - 14);

        if (repeat) ImGui.BeginDisabled();

        if (ImGui.Button("Ok", new(80, 0)))
        {   
            if (!_isnew && !_properties.Contains(_name)) obj.Properties.Remove(_oname); 
            obj.Properties[_name] = _value;
            Reset();

            _isOpened = false;
            ImGui.CloseCurrentPopup();
        }

        if (repeat) ImGui.EndDisabled();

        ImGui.EndPopup();
    }
}
