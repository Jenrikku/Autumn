namespace NewGear.IO {
    public struct SeekTask : IDisposable {
        private BinaryStream stream;
        private ulong position;

        internal SeekTask(BinaryStream stream) {
            this.stream = stream;
            position = stream.Position;
        }

        /// <summary>
        /// Returns the stream to the position it was when this instance was created.
        /// </summary>
        public void Dispose() => stream.Position = position;
    }
}
