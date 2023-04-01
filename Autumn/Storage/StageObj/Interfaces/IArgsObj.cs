namespace AutumnStageEditor.Storage.StageObj.Interfaces {
    internal interface IArgsObj : IBaseObj {
        public int[] Args { get; init; }

        public new bool PropertyCheck(string name, object? value) {
            if(Args is null || name.Length != 4 || !name.StartsWith("Arg"))
                return false;

            bool result = int.TryParse(name.AsSpan(3, 1), out int index);

            if(!result || Args.Length <= index || value is not int parsed)
                return false;

            Args[index] = parsed;
            return true;
        }
    }
}
