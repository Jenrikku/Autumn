using NewGear.IO;
using System.Collections;

namespace Jenrikku.BYAML.Nodes {
    internal static class Array {
        public static ICollection Read(BinaryStream stream, uint count, ref BYAML byml) {
            object?[] array = new object?[count];

            if(!BYAMLParser.ReferenceTracker.TryAdd((uint) stream.Position, array))
                return BYAMLParser.ReferenceTracker[(uint) stream.Position];

            BYAMLParser.NodeType[] types = stream.Read<BYAMLParser.NodeType>((int) count);

            stream.Align(4);

            for(int i = 0; i < types.Length; i++) {
                BYAMLParser.NodeType type = types[i];

                if(((byte) type >> 4) == 0xC) // If the type is from group C (Collections)
                    array[i] = BYAMLParser.ReadCollectionNode(stream, stream.Read<uint>(), ref byml);
                else
                    array[i] = BYAMLParser.ReadInlinedNode(type, stream, ref byml);
            }

            return array;
        }
    }
}
