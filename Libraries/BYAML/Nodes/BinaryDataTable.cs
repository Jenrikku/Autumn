using NewGear.IO;

namespace Jenrikku.BYAML.Nodes {
    internal static class BinaryTable {
        public static byte[][]? Read(BinaryStream stream, uint count) {
            byte[][] table = new byte[count][];

            uint beginning;
            uint end;

            for(uint i = 0; i < count; i++) {
                beginning = stream.Read<uint>();
                end = stream.Read<uint>();

                stream.Position -= 4;

                using(stream.TemporarySeek()) {
                    stream.Position = beginning;

                    table[i] = stream.Read<byte>((int) (end - beginning));
                }
            }

            return table;
        }
    }
}
