using System.Numerics;
using ImGuiNET;

namespace Autumn.GUI.Theming;

internal class Theme
{
    public ImGuiStyle? ImGuiStyle;

    public Vector4 AxisXColor;
    public Vector4 AxisYColor;
    public Vector4 AxisZColor;

    /// <summary>
    /// Updates the native ImGui style based on <see cref="ImGuiStyle"/>.
    /// </summary>
    public unsafe void UpdateImGuiTheme()
    {
        if (!ImGuiStyle.HasValue) return;
        *ImGui.GetStyle().NativePtr = ImGuiStyle.Value;
    }
}
