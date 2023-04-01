using AutumnStageEditor.Storage.StageObj.Interfaces;
using System.Numerics;

namespace AutumnStageEditor.Storage.StageObj {
    internal class GoalObj : IArgsObj, IIdentifiableObj, ISwitchableObj, ICameraObj, IClippableObj {
        public int[] Args { get; init; } = new int[8];

        public string LayerName { get; set; } = "共通";
        public string MultiFileName { get; set; } = string.Empty;
        public string Name { get; set; } = "Default";

        public int ID { get; set; } = -1;
        public int CameraId { get; set; } = -1;
        public int ClippingGroupId { get; set; } = -1;

        public Vector3 Translation { get; set; } = new(0);
        public Vector3 Rotation { get; set; } = new(0);
        public Vector3 Scale { get; set; } = new(0);

        public int SwitchA { get; set; } = -1;
        public int SwitchAppear { get; set; } = -1;
        public int SwitchB { get; set; } = -1;
        public int SwitchDeadOn { get; set; } = -1;
        public int SwitchKill { get; set; } = -1;

        public int ShapeModelNo { get; set; } = -1;
        public int ViewId { get; set; } = -1;

        // public Rail Rail { get; set; }

        public Dictionary<string, object?>? CustomProperties { get; set; }

        public bool TryParseProperty(string name, object? value) {
            // Try parse implemented interfaces:
            if(((IArgsObj) this).PropertyCheck(name, value)) return true;
            if(((IIdentifiableObj) this).PropertyCheck(name, value)) return true;
            if(((ISwitchableObj) this).PropertyCheck(name, value)) return true;
            if(((ICameraObj) this).PropertyCheck(name, value)) return true;
            if(((IClippableObj) this).PropertyCheck(name, value)) return true;
            if(((IBaseObj) this).PropertyCheck(name, value)) return true;

            if(value is not int iValue)
                return false;

            switch(name) {
                case "ShapeModelNo":
                    ShapeModelNo = iValue;
                    break;

                case "ViewId":
                    ViewId = iValue;
                    break;

                default: return false;
            }

            return true;
        }

        public GoalObj() => Array.Fill(Args, -1);
    }
}
