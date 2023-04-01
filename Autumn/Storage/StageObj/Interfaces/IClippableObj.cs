namespace AutumnStageEditor.Storage.StageObj.Interfaces {
    internal interface IClippableObj : IBaseObj {
        public int ClippingGroupId { get; set; }

        public new bool PropertyCheck(string name, object? value) {
            if(name != "ClippingGroupId" || value is not int clippingGroupId)
                return false;

            ClippingGroupId = clippingGroupId;
            return true;
        }
    }
}
