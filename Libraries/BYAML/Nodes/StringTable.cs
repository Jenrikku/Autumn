using NewGear.IO;

namespace Jenrikku.BYAML.Nodes {
    internal static class StringTable {
        public static string[]? Read(BinaryStream stream, uint count) {
            string[] table = new string[count];

            uint stringOffset = 4;

            for(uint i = 0; i < count; i++) {
                using(stream.TemporarySeek()) {
                    stream.Position += stream.Read<uint>() - stringOffset;

                    table[i] = stream.ReadStringUntil();
                }

                stream.Position += 4;
                stringOffset += 4;
            }

            return table;
        }
    }
}
