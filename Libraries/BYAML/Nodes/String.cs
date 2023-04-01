using NewGear.IO;

namespace Jenrikku.BYAML.Nodes {
    internal static class String {
        public static string Read(BinaryStream stream, ref BYAML byml) {
            int index = stream.Read<int>();

            if(byml.StringTable is null)
                throw new InvalidDataException("String table missing.");

            if(byml.StringTable.Length < index)
                throw new IndexOutOfRangeException("The requested string was out of bounds.");

            return byml.StringTable[index];
        }

        public static void Write(BinaryStream stream, string value, ref BYAML byml) {
            int index = BYAMLParser.StringTable.IndexOf(value);

            if(index == -1) {
                BYAMLParser.StringTable.Add(value);
                index = BYAMLParser.StringTable.Count;
            }

            stream.Write(index);
        }
    }
}
