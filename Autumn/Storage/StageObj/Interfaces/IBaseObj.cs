using System.Numerics;

namespace AutumnStageEditor.Storage.StageObj.Interfaces {
    internal interface IBaseObj {
        public string LayerName { get; set; }
        public string MultiFileName { get; set; }
        public string Name { get; set; }

        public Vector3 Translation { get; set; }
        public Vector3 Rotation { get; set; }
        public Vector3 Scale { get; set; }

        public Dictionary<string, object?>? CustomProperties { get; set; }

        public bool TryParseProperty(string name, object? value);

        public bool PropertyCheck(string name, object? value) {
            if(value is string sValue) {
                switch(name) {
                    case "LayerName":
                        LayerName = sValue;
                        break;

                    case "MultiFileName":
                        MultiFileName = sValue;
                        break;

                    case "name":
                        Name = sValue;
                        break;

                    default: return false;
                }

                return true;
            }

            // Vectors:

            if(value is not float fValue)
                return false;

            if(name.StartsWith("pos_")) {
                Vector3 vec = Translation;

                switch(name[4]) {
                    case 'x':
                        vec.X = fValue / 100;
                        break;
                    case 'y':
                        vec.Y = fValue / 100;
                        break;
                    case 'z':
                        vec.Z = fValue / 100;
                        break;

                    default: return false;
                }

                Translation = vec;
                return true;
            } else if(name.StartsWith("dir_")) {
                Vector3 vec = Rotation;

                switch(name[4]) {
                    case 'x':
                        vec.X = fValue;
                        break;
                    case 'y':
                        vec.Y = fValue;
                        break;
                    case 'z':
                        vec.Z = fValue;
                        break;

                    default: return false;
                }

                Rotation = vec;
                return true;
            } else if(name.StartsWith("scale_")) {
                Vector3 vec = Scale;

                switch(name[6]) {
                    case 'x':
                        vec.X = fValue;
                        break;
                    case 'y':
                        vec.Y = fValue;
                        break;
                    case 'z':
                        vec.Z = fValue;
                        break;

                    default: return false;
                }

                Scale = vec;
                return true;
            }

            return false;
        }

        public void SetCustomProperty(string name, object? value) {
            CustomProperties ??= new(); // Create dictionary if null.

            if(CustomProperties.ContainsKey(name))
                CustomProperties[name] = value;
            else
                CustomProperties.Add(name, value);
        }

        //public void ParseProperties(Dictionary<string, object?> properties) {
        //    properties.TryGetValue("LayerName", out object? _layerName);
        //    properties.TryGetValue("MultiFileName", out object? _multiFileName);
        //    properties.TryGetValue("dir_x", out object? _dir_x);
        //    properties.TryGetValue("dir_y", out object? _dir_y);
        //    properties.TryGetValue("dir_z", out object? _dir_z);
        //    properties.TryGetValue("name", out object? _name);
        //    properties.TryGetValue("pos_x", out object? _pos_x);
        //    properties.TryGetValue("pos_y", out object? _pos_y);
        //    properties.TryGetValue("pos_z", out object? _pos_z);
        //    properties.TryGetValue("scale_x", out object? _scale_x);
        //    properties.TryGetValue("scale_y", out object? _scale_y);
        //    properties.TryGetValue("scale_z", out object? _scale_z);


        //    if(_layerName is string _cLayerName)
        //        LayerName = _cLayerName;

        //    if(_multiFileName is string _cMultiFileName)
        //        MultiFileName = _cMultiFileName;

        //    if(_name is string _cName)
        //        Name = _cName;


        //    Vector3 _translation = new(0);
        //    Vector3 _rotation = new(0);
        //    Vector3 _scale = new(0);

        //    if(_pos_x is float _cPosX)
        //        _translation.X = _cPosX;

        //    if(_pos_y is float _cPosY)
        //        _translation.Y = _cPosY;

        //    if(_pos_z is float _cPosZ)
        //        _translation.Z = _cPosZ;

        //    if(_dir_x is float _cDir_x)
        //        _rotation.X = _cDir_x;

        //    if(_dir_y is float _cDir_y)
        //        _rotation.Y = _cDir_y;

        //    if(_dir_z is float _cDir_z)
        //        _rotation.Z = _cDir_z;

        //    if(_scale_x is float _cScale_x)
        //        _scale.X = _cScale_x;

        //    if(_scale_y is float _cScale_y)
        //        _scale.Y = _cScale_y;

        //    if(_scale_z is float _cScale_z)
        //        _scale.Z = _cScale_z;

        //    Translation = _translation;
        //    Rotation = _rotation;
        //    Scale = _scale;
        //}
    }
}
