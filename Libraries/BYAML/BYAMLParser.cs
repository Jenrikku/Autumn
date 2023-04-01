using NewGear.Extensions;
using NewGear.IO;
using System.Collections;
using System.Text;

namespace Jenrikku.BYAML {
    public static class BYAMLParser {
        public static bool Identify(byte[] data) => data.CheckMagic("BY") || data.CheckMagic("YB");

        public static BYAML Read(byte[] data, Encoding? encoding = null) {
            encoding ??= Encoding.UTF8;

            using BinaryStream stream = new(data) { Position = 2, DefaultEncoding = encoding };

            BYAML byml = new() { Encoding = encoding };

            // Magic and byte order check:
            if(data.CheckMagic("BY"))
                stream.ByteOrder = ByteOrder.BigEndian;
            else if(data.CheckMagic("YB"))
                stream.ByteOrder = ByteOrder.LittleEndian;
            else
                throw new ArgumentException("The given data does not correspond to a BYAML.");

            byml.ByteOrder = stream.ByteOrder;
            byml.Version = stream.Read<ushort>();

            uint dictKeyTableOffset = stream.Read<uint>(),
                 stringTableOffset = stream.Read<uint>(),
                 binaryDataTableOffset = 0,
                 rootOffset;

            // The binary data table is only present in some byamls from version 1.
            byml.HasBinaryDataTable = dictKeyTableOffset != 0x10 && byml.Version == 1;

            if(byml.HasBinaryDataTable)
                binaryDataTableOffset = stream.Read<uint>();

            rootOffset = stream.Read<uint>();


            // Read nodes ------------------

            byml.DictKeyTable = (string[]?) ReadCollectionNode(stream, dictKeyTableOffset, ref byml);

            byml.StringTable = (string[]?) ReadCollectionNode(stream, stringTableOffset, ref byml);

            byml.BinaryDataTable = (byte[][]?) ReadCollectionNode(stream, binaryDataTableOffset, ref byml);

            byml.Root = ReadCollectionNode(stream, rootOffset, ref byml);


            // -----------------------------

            ReferenceTracker.Clear();
            return byml;
        }

        public static byte[] Write(BYAML byml) {

            // Create streams --------------

            using BinaryStream stream = new() { ByteOrder = byml.ByteOrder, DefaultEncoding = byml.Encoding };

            using BinaryStream keyTableStream = new() { ByteOrder = byml.ByteOrder, DefaultEncoding = byml.Encoding };
            using BinaryStream stringTableStream = new() { ByteOrder = byml.ByteOrder, DefaultEncoding = byml.Encoding };
            using BinaryStream rootStream = new() { ByteOrder = byml.ByteOrder, DefaultEncoding = byml.Encoding };


            stream.Write<ushort>(0x4259); // Magic.
            stream.Write(byml.Version);   // Version.

            if(byml.HasBinaryDataTable)
                stream.Length += 16; // 4 * 4 -> 16. Reserve 4 offsets without moving position.
            else
                stream.Length += 12; // 4 * 3 -> 12. Reserve 3 offsets without moving position.


            WriteNode(IdentifyNode(byml.Root), rootStream, byml.Root, ref byml);


            //WriteNode(NodeType.StringTable, stream, byml.DictKeyTable, ref byml);

            return stream.ToArray();
        }


        // Internal read -------------------

        /// <summary>
        /// Dictionary used to avoid duplicates and circular references.
        /// </summary>
        internal readonly static Dictionary<uint, ICollection> ReferenceTracker = new();

        internal static ICollection? ReadCollectionNode(BinaryStream stream, uint offset, ref BYAML byml) {
            if(offset == 0) // 0 points to nothing (null).
                return null;

            using SeekTask task = stream.TemporarySeek();

            stream.Position = offset;

            switch(stream.Read<NodeType>()) {
                case NodeType.Array:
                    return Nodes.Array.Read(stream, stream.ReadUInt24(), ref byml);

                case NodeType.Dictionary:
                    return Nodes.Dictionary.Read(stream, stream.ReadUInt24(), ref byml);

                case NodeType.StringTable:
                    return Nodes.StringTable.Read(stream, stream.ReadUInt24());

                case NodeType.BinaryTable:
                    return Nodes.BinaryTable.Read(stream, stream.ReadUInt24());

                default:
                    throw new NotSupportedException("A node was not properly identified as a collection.");
            }
        }

        internal static object? ReadInlinedNode(NodeType type, BinaryStream stream, ref BYAML byml) {
            switch(type) {

                // A -------------------------

                case NodeType.String:
                    return Nodes.String.Read(stream, ref byml);

                case NodeType.Binary:
                    return Nodes.Binary.Read(stream, ref byml);

                case NodeType.BinaryWithParam:
                    throw new NotImplementedException("This kind of binary data is still not implemented.");

                // D -------------------------

                case NodeType.Bool:
                    return stream.Read<uint>() == 1;

                case NodeType.Int:
                    return stream.Read<int>();

                case NodeType.Float:
                    return stream.Read<float>();

                case NodeType.UInt:
                    return stream.Read<uint>();


                case NodeType.Int64:
                    return stream.ReadAt<long>(stream.Read<uint>());

                case NodeType.UInt64:
                    return stream.ReadAt<ulong>(stream.Read<uint>());

                case NodeType.Double:
                    return stream.ReadAt<double>(stream.Read<uint>());

                // ---------------------------

                case NodeType.Null:
                    stream.Position += 4;
                    return null;

                default:
                    throw new NotSupportedException("An unknown node type was found.");
            }
        }


        // Internal write --------------------

        internal readonly static Dictionary<uint, object> WrittenTracker = new();
        internal readonly static List<string> KeyTable = new();
        internal readonly static List<string> StringTable = new();

        internal static void WriteNode(NodeType type, BinaryStream stream, object? value, ref BYAML byml) {            
            if(type == NodeType.Null || value is null) {
                stream.Write(0);
                return;
            }

            if(IsReferenceNode(type, ref byml)) {
                //using(stream.TemporarySeek()) {
                //    case NodeType.Binary:
                //}
            }

            switch(type) { // Inlined nodes.
                case NodeType.String:
                    Nodes.String.Write(stream, (string) value, ref byml);
                    break;

                //case NodeType.Binary:
                //    Nodes.
            }
        }

        internal static NodeType IdentifyNode(object? value) {
            if(value is null)
                return NodeType.Null;

            return value.GetType() switch {
                Type t when t == typeof(string) => NodeType.String,
                Type t when t == typeof(byte[]) => NodeType.Binary,
                Type t when t == typeof(object?[]) => NodeType.Array,
                Type t when t == typeof(Dictionary<string, object?>) => NodeType.Dictionary,
                Type t when t == typeof(string[]) => NodeType.StringTable,
                Type t when t == typeof(byte[][]) => NodeType.BinaryTable,
                Type t when t == typeof(bool) => NodeType.Bool,
                Type t when t == typeof(int) => NodeType.Int,
                Type t when t == typeof(float) => NodeType.Float,
                Type t when t == typeof(uint) => NodeType.UInt,
                Type t when t == typeof(long) => NodeType.Int64,
                Type t when t == typeof(ulong) => NodeType.UInt64,
                Type t when t == typeof(double) => NodeType.Double,
                _ => throw new NotSupportedException($"A node could not be identified. ({value.GetType().FullName})")
            };
        }

        internal static bool IsReferenceNode(NodeType type, ref BYAML byml) {
            if(((byte) type >> 4) == 0xC)
                return true;

            if(byml.Version >= 4 && (type == NodeType.Binary || type == NodeType.BinaryWithParam))
                return true;
                
            if(type >= NodeType.Int64 && type != NodeType.Null)
                return true;

            return false;
        }


        // -----------------------------------

        internal enum NodeType : byte {
            String = 0xA0,
            Binary = 0xA1,
            BinaryWithParam = 0xA2,
            Array = 0xC0,
            Dictionary = 0xC1,
            StringTable = 0xC2,
            BinaryTable = 0xC3,
            Bool = 0xD0,
            Int = 0xD1,
            Float = 0xD2,
            UInt = 0xD3,
            Int64 = 0xD4,
            UInt64 = 0xD5,
            Double = 0xD6,
            Null = 0xFF
        }
    }
}