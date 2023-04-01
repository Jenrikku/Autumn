using ImGuiNET;
using System.Runtime.InteropServices;

// Based on SceneGL.Testing: https://github.com/jupahe64/SceneGL/blob/master/SceneGL.Testing/DockSpace.cs

namespace AutumnSceneGL.GUI {
    class DockLayout {
        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint igDockBuilderGetNode(uint node_id);


        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint igDockBuilderSplitNode(uint node_id, ImGuiDir split_dir,
            float size_ratio_for_node_at_dir, out uint out_id_at_dir, out uint out_id_at_opposite_dir);


        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern void igDockBuilderDockWindow(string window_name, uint node_id);



        private (ImGuiDir splitDir, float splitPos, DockLayout childA, DockLayout childB)? _splitInfo;
        private readonly string[]? _windows;

        public DockLayout(ImGuiDir splitDir, float splitPos, DockLayout childA, DockLayout childB) {
            _splitInfo = (splitDir, splitPos, childA, childB);
            _windows = null;
        }

        public DockLayout(params string[] windows) {
            _splitInfo = null;
            _windows = windows;
        }

        public void ApplyTo(uint dockNode) {
            if(_splitInfo == null) {
                for(int i = 0; i < _windows!.Length; i++) {
                    igDockBuilderDockWindow(_windows![i], dockNode);
                }
            } else {
                var (splitDr, splitPos, childA, childB) = _splitInfo.Value;

                _ = igDockBuilderSplitNode(dockNode, splitDr, splitPos, out uint childNodeA, out uint childNodeB);

                childA.ApplyTo(childNodeA);
                childB.ApplyTo(childNodeB);
            }
        }
    }

    internal class DockSpace {
        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint igDockBuilderAddNode(uint node_id, ImGuiDockNodeFlags flags);


        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
        private static extern void igDockBuilderDeleteNode(uint node_id);


        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
        private static extern void igDockBuilderFinish(uint node_id);



        private uint _id;

        public DockSpace(string label) {
            _id = ImGui.GetID(label);
        }

        public uint DockId => _id;

        public void Setup(DockLayout layout) {
            _ = igDockBuilderAddNode(_id, ImGuiDockNodeFlags.None);
            layout.ApplyTo(_id);
            igDockBuilderFinish(_id);
        }
    }
}
