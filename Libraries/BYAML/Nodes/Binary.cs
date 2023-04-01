using NewGear.IO;
using System.Diagnostics;

namespace Jenrikku.BYAML.Nodes {
    internal static class Binary {
        public static byte[] Read(BinaryStream stream, ref BYAML byml) {
            if(byml.Version == 1) {
                int index = stream.Read<int>();

                if(byml.BinaryDataTable is null)
                    throw new InvalidDataException("Binary data table missing.");

                if(byml.BinaryDataTable.Length > index)
                    throw new IndexOutOfRangeException("The requested binary data was out of bounds.");

                return byml.BinaryDataTable[index];
            }
            
            if(byml.Version < 4)
                Debug.WriteLine("The byaml contains a node that is not supported by its version.");

            // Version 4+

            uint offset = stream.Read<uint>();

            using(stream.TemporarySeek()) {
                stream.Position = offset;

                int size = stream.Read<int>();

                return stream.Read<byte>(size);
            }
        }
    }
}
