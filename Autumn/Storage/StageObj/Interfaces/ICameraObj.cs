namespace AutumnStageEditor.Storage.StageObj.Interfaces {
    internal interface ICameraObj : IBaseObj {
        public int CameraId { get; set; }

        public new bool PropertyCheck(string name, object? value) {
            if(name != "CameraId" || value is not int cameraId)
                return false;

            CameraId = cameraId;
            return true;
        }
    }
}
