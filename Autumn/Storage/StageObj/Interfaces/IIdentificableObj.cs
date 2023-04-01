namespace AutumnStageEditor.Storage.StageObj.Interfaces {
    internal interface IIdentifiableObj : IBaseObj {
        public int ID { get; set; }

        public new bool PropertyCheck(string name, object? value) {
            if(name != "l_id" || value is not int l_id)
                return false;

            ID = l_id;
            return true;
        }

        //public new void ParseProperties(Dictionary<string, object?> properties) {
        //    properties.TryGetValue("l_id", out object? _l_id);


        //    if(_l_id is int _cL_id)
        //        ID = _cL_id;
        //}
    }
}
