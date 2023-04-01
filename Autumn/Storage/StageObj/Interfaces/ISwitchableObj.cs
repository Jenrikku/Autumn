namespace AutumnStageEditor.Storage.StageObj.Interfaces {
    internal interface ISwitchableObj : IBaseObj {
        public int SwitchA { get; set; }
        public int SwitchAppear { get; set; }
        public int SwitchB { get; set; }
        public int SwitchDeadOn { get; set; }
        public int SwitchKill { get; set; }

        public new bool PropertyCheck(string name, object? value) {
            if(value is not int iValue)
                return false;

            switch(name) {
                case "SwitchA":
                    SwitchA = iValue;
                    break;
                case "SwitchAppear":
                    SwitchAppear = iValue;
                    break;

                case "SwitchB":
                    SwitchB = iValue;
                    break;

                case "SwitchDeadOn":
                    SwitchDeadOn = iValue;
                    break;

                case "SwitchKill":
                    SwitchKill = iValue;
                    break;

                default: return false;
            }

            return true;
        }
    }
}
