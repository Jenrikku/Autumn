using AutumnStageEditor.Storage.StageObj.Interfaces;
using System.Numerics;

namespace AutumnStageEditor.Storage.StageObj {
    internal class CameraAreaObj : IIdentifiableObj, ISwitchableObj, ICameraObj {
        public string LayerName { get; set; } = "共通";
        public string MultiFileName { get; set; } = string.Empty;
        public string Name { get; set; } = "Default";

        public int ID { get; set; } = -1;
        public int CameraId { get; set; } = -1;

        public Vector3 Translation { get; set; } = new(0);
        public Vector3 Rotation { get; set; } = new(0);
        public Vector3 Scale { get; set; } = new(0);

        public int SwitchA { get; set; }
        public int SwitchAppear { get; set; }
        public int SwitchB { get; set; }
        public int SwitchDeadOn { get; set; }
        public int SwitchKill { get; set; }

        public int Priority { get; set; } = -1;
        public int ShapeModelNo { get; set; } = -1;

        public Dictionary<string, object?>? CustomProperties { get; set; }

        public bool TryParseProperty(string name, object? value) {
            // Try parse implemented interfaces:
            if(((IIdentifiableObj) this).PropertyCheck(name, value)) return true;
            if(((ISwitchableObj) this).PropertyCheck(name, value)) return true;
            if(((ICameraObj) this).PropertyCheck(name, value)) return true;
            if(((IBaseObj) this).PropertyCheck(name, value)) return true;

            if(value is not int iValue)
                return false;

            switch(name) {
                case "Priority":
                    Priority = iValue;
                    break;

                case "ShapeModelNo":
                    ShapeModelNo = iValue;
                    break;

                default: return false;
            }

            return true;
        }
    }
}
