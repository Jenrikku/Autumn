using NewGear.IO;
using System.Collections;
using System.Diagnostics;

namespace Jenrikku.BYAML.Nodes {
    internal static class Dictionary {
        public static ICollection Read(BinaryStream stream, uint count, ref BYAML byml) {
            if(byml.DictKeyTable is null)
                throw new("The key table is missing.");

            Dictionary<string, object?> dictionary = new();

            if(!BYAMLParser.ReferenceTracker.TryAdd((uint) stream.Position, dictionary))
                return BYAMLParser.ReferenceTracker[(uint) stream.Position];

            for(uint i = 0; i < count; i++) {
                string key = byml.DictKeyTable[stream.ReadUInt24()];

                if(dictionary.ContainsKey(key)) {
                    Debug.WriteLine("Found duplicated key: " + key);
                    continue;
                }

                BYAMLParser.NodeType type = stream.Read<BYAMLParser.NodeType>();

                if(((byte) type >> 4) == 0xC) // If the type is from group C (Collections)
                    dictionary.Add(key, BYAMLParser.ReadCollectionNode(stream, stream.Read<uint>(), ref byml));
                else
                    dictionary.Add(key, BYAMLParser.ReadInlinedNode(type, stream, ref byml));
            }

            return dictionary;
        }
    }
}
