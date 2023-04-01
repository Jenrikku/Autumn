using AutumnStageEditor.Storage.StageObj.Interfaces;
using System.Numerics;

namespace AutumnStageEditor.Storage.StageObj {
    internal class StartEventObj : IArgsObj {
        public int[] Args { get; init; } = new int[8];

        public string LayerName { get; set; } = "共通";
        public string MultiFileName { get; set; } = string.Empty;
        public string Name { get; set; } = "Default";

        public Vector3 Translation { get; set; } = new(0);
        public Vector3 Rotation { get; set; } = new(0);
        public Vector3 Scale { get; set; } = new(0);

        public Dictionary<string, object?>? CustomProperties { get; set; }

        public bool TryParseProperty(string name, object? value) {
            // Try parse implemented interfaces:
            if(((IArgsObj) this).PropertyCheck(name, value)) return true;
            if(((IBaseObj) this).PropertyCheck(name, value)) return true;

            return false;
        }

        public StartEventObj() => Array.Fill(Args, -1);
    }
}
