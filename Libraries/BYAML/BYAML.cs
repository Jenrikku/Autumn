using NewGear.IO;
using System.Collections;
using System.Text;

namespace Jenrikku.BYAML {
    public struct BYAML {
        public ByteOrder ByteOrder;
        public Encoding Encoding;

        public ushort Version;

        public string[]? DictKeyTable { get; internal set; }
        public string[]? StringTable { get; internal set; }
        public byte[][]? BinaryDataTable { get; internal set; }

        public ICollection? Root { get; internal set; }

        public bool HasBinaryDataTable { get; internal set; }

        public BYAML() {
            ByteOrder = ByteOrder.LittleEndian;
            Encoding = Encoding.UTF8;
            Version = 1;
        }
    }
}
