using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace NewGear.IO {
    public unsafe class BinaryStream : IDisposable, IEnumerable<byte> {
        // Each data block is 1MB long. (1048576 bytes)
        private const int blockLength = 1048576;

        private readonly List<byte[]> dataBlocks = new();
        private ulong position = 0;
        
        private byte* PointerAt(ulong position) {
            int blockIndex = (int) (position / blockLength);

            if(dataBlocks.Count - 1 < blockIndex)
                ExtendMemory();

            return (byte*) Marshal.UnsafeAddrOfPinnedArrayElement(
                dataBlocks[blockIndex], (int) position - blockLength * blockIndex);
        }

        private void ExtendMemory() {
            byte[] block = new byte[blockLength];

            if(FillingNumber != 0x00)
                Array.Fill(block, FillingNumber);

            dataBlocks.Add(block);
        }



        // public --------------------------------

        /// <summary>
        /// Creates a new empty stream.
        /// </summary>
        /// <param name="fillingNumber">The number that will be written to all new empty spaces.</param>
        public BinaryStream(byte fillingNumber = 0x00) {
            FillingNumber = fillingNumber;
            
            ExtendMemory();
        }

        /// <summary>
        /// Creates a stream from a copy of an existing array.
        /// </summary>
        /// <param name="fillingNumber">The number that will be written to all new empty spaces.</param>
        public BinaryStream(byte[] data, byte fillingNumber = 0x00) {
            FillingNumber = fillingNumber;

            int partitionCount = data.Length / blockLength + 1;

            for(int i = 0; i < partitionCount; i++) {
                byte[] temp = new byte[blockLength];
                int length = Math.Min(data.Length - i * blockLength, blockLength);

                if(length == blockLength && fillingNumber != 0x00)
                    Array.Fill(temp, fillingNumber);

                Array.Copy(data, i * blockLength, temp, 0, length);

                Length += (uint) length;
                dataBlocks.Add(temp);
            }
        }

        /// <summary>
        /// The position of the stream. The next value will be read from this position.
        /// </summary>
        public ulong Position {
            get => position;
            set {
                // Reserves more memory if required.
                if((ulong) dataBlocks.Count * blockLength < value) {
                    int newAmount = (int) (value / blockLength) - dataBlocks.Count;

                    for(int i = 0; i < newAmount; i++)
                        ExtendMemory();
                }

                if(Length < value)
                    Length = value;

                position = value;
            }
        }

        /// <summary>
        /// The length of the data inside the stream.
        /// </summary>
        public ulong Length = 0;

        /// <summary>
        /// Specifies the number that will be written to all new empty spaces.
        /// </summary>
        public byte FillingNumber { get; set; } = 0x00;

        /// <summary>
        /// Specifies the order of the bytes when reading or writing a numeric value.
        /// </summary>
        public ByteOrder ByteOrder = BitConverter.IsLittleEndian ? ByteOrder.LittleEndian : ByteOrder.BigEndian;

        /// <summary>
        /// The default <see cref="Encoding"/> that will be used when reading strings if none is specified.
        /// </summary>
        public Encoding DefaultEncoding = Encoding.ASCII;


        // Reading ----------------

        /// <summary>
        /// Read a value of the given type from the stream taking in mind <see cref="ByteOrder"/>.
        /// </summary>
        public T Read<T>() where T : unmanaged {
            int size = sizeof(T);
            byte[] bytes = new byte[size];
            int pos = 0;
            bool needsReading = true;

            if(typeof(T) != typeof(byte) && // Skip byte (no byte order).
               (!BitConverter.IsLittleEndian != (ByteOrder == ByteOrder.BigEndian))) { // ByteOrder does not match.

                if(!typeof(T).IsPrimitive && !typeof(T).IsEnum) {
                    foreach(FieldInfo field in typeof(T).GetFields()) {
                        if(field.IsLiteral /* (const) */) continue;
                        needsReading = false;

                        int fieldSize;

                        if(field.FieldType.IsEnum)
                            fieldSize = Marshal.SizeOf(Enum.GetUnderlyingType(field.FieldType));
                        else
                            fieldSize = Marshal.SizeOf(field.FieldType);

                        for(int i = fieldSize - 1; i > -1; i--)
                            bytes[i + pos] = *PointerAt(position++);

                        pos += fieldSize;
                    }
                }

                if(needsReading) {
                    for(uint i = 0; i < size; i++)
                        bytes[i] = *PointerAt(position + (uint) size - i - 1);

                    position += (uint) size;
                    needsReading = false;
                }
            }

            if(needsReading)
                for(int i = 0; i < bytes.Length; i++)
                    bytes[i] = *PointerAt(position++);

            fixed(byte* ptr = bytes)
                return *(T*) ptr;
        }

        /// <summary>
        /// Reads an element from an absolute offset. The stream will restore its position upon return. 
        /// </summary>
        public T ReadAt<T>(ulong offset) where T : unmanaged {
            using SeekTask task = TemporarySeek();

            position = offset;

            return Read<T>();
        }

        /// <summary>
        /// Reads an array from the stream and advances the position.
        /// </summary>
        public T[] Read<T>(int amount) where T : unmanaged {
            T[] array = new T[amount];

            for(int i = 0; i < amount; i++)
                array[i] = Read<T>();

            return array;
        }

        /// <summary>
        /// Reads an array of element from an absolute offset. The stream will restore its position upon return. 
        /// </summary>
        public T[] ReadAt<T>(ulong offset, int amount) where T : unmanaged {
            using SeekTask task = TemporarySeek();

            position = offset;

            return Read<T>(amount);
        }

        /// <summary>
        /// Reads a string from the stream using <see cref="DefaultEncoding"/>.
        /// </summary>
        public string ReadString(int length) => ReadString(length, DefaultEncoding);

        /// <summary>
        /// Reads a string from the stream using a given <see cref="Encoding"/>.
        /// </summary>
        public string ReadString(int length, Encoding encoding) {
            byte[] bytes = Read<byte>(length);

            return encoding.GetString(bytes);
        }

        /// <summary>
        /// Reads a string that ends with a given byte (0 by default).
        /// </summary>
        public string ReadStringUntil(byte end = 0x00) => ReadStringUntil(DefaultEncoding, end);

        /// <summary>
        /// Reads a string that ends with a given byte using a given <see cref="Encoding"/>.
        /// </summary>
        public string ReadStringUntil(Encoding encoding, byte end = 0x00) {
            int length = 0;

            while(*PointerAt(position++) != end)
                length++;

            return encoding.GetString(PointerAt(position - (uint) length - 1), length);
        }

        /// <summary>
        /// Reads a 24-bit unsigned integer and returns it as a 32-bit unsigned integer.
        /// </summary>
        public unsafe uint ReadUInt24() {
            byte[] number = new byte[4];

            byte[] read = Read<byte>(3);

            if(!BitConverter.IsLittleEndian != (ByteOrder == ByteOrder.BigEndian)) // ByteOrder does not match.
                Array.Reverse(read);

            if(BitConverter.IsLittleEndian)
                read.CopyTo(number, 0);
            else
                read.CopyTo(number, 1);

            return *(uint*) Marshal.UnsafeAddrOfPinnedArrayElement(number, 0);
        }


        // Writing ----------------

        /// <summary>
        /// Writes any given value to the stream taking in mind <see cref="ByteOrder"/>.
        /// </summary>
        public void Write<T>(T value) where T : unmanaged {
            if(typeof(T) != typeof(byte) && // Skip byte (no byte order).
               (BitConverter.IsLittleEndian != (ByteOrder == ByteOrder.LittleEndian))) { // ByteOrder does not match.

                byte* ptr = (byte*) &value;
                uint pos = 0;

                bool needsReversion = true;

                if(!typeof(T).IsPrimitive && !typeof(T).IsEnum) {
                    foreach(FieldInfo field in typeof(T).GetFields()) {
                        object? child = field.GetValue(value);

                        if(child is null || field.IsLiteral /* (const) */) continue;
                        needsReversion = false;

                        GCHandle handle = GCHandle.Alloc(child, GCHandleType.Pinned);
                        byte* childPtr = (byte*) handle.AddrOfPinnedObject();

                        int size = Marshal.SizeOf(child);
                        for(int i = size - 1; i > -1; i--, pos++)
                            ptr[pos] = childPtr[i];

                        handle.Free();
                    }
                }

                if(needsReversion) {
                    T temp = value;
                    byte* tempPtr = (byte*) &temp;

                    for(int i = sizeof(T) - 1, j = 0; i > -1; i--, j++)
                        ptr[i] = tempPtr[j];
                }

            }

            Write((byte*) &value, sizeof(T));
        }

        /// <summary>
        /// Writes an element to an absolute offset. The stream will restore its position upon return. 
        /// </summary>
        public void Write<T>(T value, ulong offset) where T : unmanaged {
            using SeekTask task = TemporarySeek();

            position = offset;

            Write(value);
        }

        /// <summary>
        /// Writes an array to the stream.
        /// </summary>
        public void Write<T>(T[] array) where T : unmanaged {
            foreach(T value in array)
                Write(value);
        }

        /// <summary>
        /// Writes an array of element to an absolute offset. The stream will restore its position upon return. 
        /// </summary>
        public void Write<T>(T[] array, ulong offset) where T : unmanaged {
            using SeekTask task = TemporarySeek();

            position = offset;

            Write(array);
        }

        /// <summary>
        /// Writes a string to the stream using <see cref="DefaultEncoding"/>.
        /// </summary>
        public void Write(string value) => Write(value, DefaultEncoding);

        /// <summary>
        /// Writes a string to the stream with a set <see cref="Encoding"/>.
        /// </summary>
        public void Write(string value, Encoding encoding) => Write(encoding.GetBytes(value));

        /// <summary>
        /// Writes an object of unknown type to the stream. The length in bytes of this object has to be specified.
        /// </summary>
        /// <param name="length">The length in bytes of this object.</param>
        public void Write(object value, int length) {
            GCHandle handle = GCHandle.Alloc(value, GCHandleType.Pinned);

            Write((byte*) handle.AddrOfPinnedObject(), length);

            handle.Free();
        }

        /// <summary>
        /// Writes a given number of bytes from a pointer. This does not take in mind <see cref="ByteOrder"/>.
        /// </summary>
        public void Write(byte* pointer, int length) {
            for(int i = 0; i < length; i++)
                *PointerAt(position++) = pointer[i];

            if(Length < position)
                Length = position;
        }


        // Others ----------------

        /// <summary>
        /// Packs the contents of the stream into a byte array.
        /// </summary>
        public byte[] ToArray() {
            byte[] result = new byte[Length];
            int index = 0;
            ulong pos = 0;
            
            while(pos < position) {
                ulong current = Math.Min(Length - blockLength * (uint) index, blockLength);

                Array.Copy(dataBlocks[index], result, (int) current);

                pos += current;
            }

            return result;
        }

        /// <summary>
        /// Creates a <see cref="SeekTask"/> that returns the stream to its past position once it is disposed.
        /// </summary>
        public SeekTask TemporarySeek() => new(this);

        /// <summary>
        /// Checks what the next byte is without moving the position.
        /// </summary>
        public byte Peek() => *PointerAt(position);

        /// <summary>
        /// Sets the position of the stream to a multiple of the given number.
        /// </summary>
        public void Align(uint amount) {
            if(position % amount == 0)
                return; // The stream is aligned already.

            uint last = (uint) (position / amount);

            Position = amount * ++last;
        }

        /// <summary>
        /// Copies all the contents of the stream into another one at the given stream's position.
        /// </summary>
        public void CopyTo(BinaryStream stream) {
            int count = dataBlocks.Count;

            ulong beginning;
            int length;

            for(uint i = 0; i < count; i++) {
                beginning = i * blockLength;
                length = blockLength;

                if((beginning + (uint) length) < Length)
                    length = (int) (Length - beginning);

                stream.Write(PointerAt(beginning), length);
            }
        }

        /// <summary>
        /// Removes all data within the stream and frees memory.
        /// </summary>
        public void Dispose() {
            dataBlocks.Clear();
            GC.SuppressFinalize(this);
        }

        public IEnumerator<byte> GetEnumerator() {
            for(ulong i = 0; i < Length; i++) {
                int blockIndex = (int) (i / blockLength);

                yield return dataBlocks[blockIndex][i - (ulong) blockLength * (uint) blockIndex];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
