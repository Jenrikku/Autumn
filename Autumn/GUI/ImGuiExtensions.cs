using System.Numerics;
using Hexa.NET.ImGui;

namespace Autumn.GUI;

internal static class ImGuiExtensions
{
    public static Vector2 GetCenter(this ImGuiViewportPtr viewport)
    {
        return viewport.Pos + (viewport.Size * 0.5f);
    }
}