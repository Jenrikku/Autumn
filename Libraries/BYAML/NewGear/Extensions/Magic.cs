namespace NewGear.Extensions {
    public static class Magic {
        /// <summary>
        /// Checks if the magic number is correct at the given position.
        /// </summary>
        public static bool CheckMagic(this byte[] data, string magic, int offset = 0) {
            if(magic.Length > data.Length)
                return false;

            int length = magic.Length;

            for(int i = 0; i < length; i++)
                if(data[offset + i] != magic[i])
                    return false;

            return true;
        }

        /// <summary>
        /// Checks if the magic number is correct at the given position.
        /// </summary>
        public static unsafe bool CheckMagic(this byte[] data, byte* magicPtr, int length, int offset = 0) {
            for(int i = 0; i < length; i++)
                if(data[offset + i] != magicPtr[i])
                    return false;

            return true;
        }
    }
}
