using AutumnSceneGL.GUI.Rendering;
using AutumnSceneGL.Storage;
using SceneGL;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System.Collections.ObjectModel;
using System.Numerics;

using Framebuffer = SceneGL.GLWrappers.Framebuffer;

namespace AutumnSceneGL.GUI {
    internal class StageEditorContext {
        public readonly ObservableCollection<Scene> Scenes = new();
        public Scene? CurrentScene;

        public IWindow? Window;
        public GL? GL;
        public IInputContext? Input;
        public ImGuiController? ImguiController;

        public DockSpace? MainDock;

        public bool IsFirstFrame = true;

        public IKeyboard? Keyboard;

        public Camera Camera = new(new Vector3(-10, 7, 10), Vector3.Zero);
        public Framebuffer Framebuffer = new(null, InternalFormat.DepthStencil, InternalFormat.Rgb);
        public Matrix4x4 ViewProjection;


        public Material? Material;
    }
}
