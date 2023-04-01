using AutumnStageEditor.Storage.StageObj.Interfaces;
using System.Numerics;

namespace AutumnStageEditor.Storage.StageObj {
    internal class StartObj : IBaseObj {
        public string LayerName { get; set; } = "共通";
        public string MultiFileName { get; set; } = string.Empty;
        public string Name { get; set; } = "Default";

        public int MarioNo { get; set; } = -1;

        public Vector3 Translation { get; set; } = new(0);
        public Vector3 Rotation { get; set; } = new(0);
        public Vector3 Scale { get; set; } = new(0);

        public Dictionary<string, object?>? CustomProperties { get; set; }

        public bool TryParseProperty(string name, object? value) {
            // Try parse implemented interfaces:
            if(((IBaseObj) this).PropertyCheck(name, value)) return true;

            if(name != "MarioNo" || value is not int marioNo)
                return false;

            MarioNo = marioNo;
            return true;
        }
    }
}
