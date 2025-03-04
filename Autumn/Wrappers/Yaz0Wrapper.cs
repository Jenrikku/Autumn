namespace Autumn.Wrappers;

internal static class Yaz0Wrapper
{
    /// <summary>
    /// Defines the level of compression used when compressing.
    /// </summary>
    public static CompressionLevel Level { get; set; } = CompressionLevel.Medium;

    /// <summary>
    /// Recommended compression levels.
    /// </summary>
    public enum CompressionLevel
    {
        Small = 0x1ff,
        Medium = 0x4c0,
        Great = 0xAff,
        Max = 0xfff,
        None = 0,
    }
    public static unsafe byte[] Compress(byte[] data)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        MemoryStream resultStream = new();
        MemoryStream dataStream = new(data);
        BinaryWriter bw = new(resultStream);
        bw.Write((byte)'Y');
        bw.Write((byte)'a');
        bw.Write((byte)'z');
        bw.Write((byte)'0');
        bw.Write((byte)(data.Length >> 24 & 0xFF));
        bw.Write((byte)(data.Length >> 16 & 0xFF));
        bw.Write((byte)(data.Length >> 8 & 0xFF));
        bw.Write((byte)(data.Length >> 0 & 0xFF));
        bw.BaseStream.Seek(8, SeekOrigin.Current);

        const int max_2Byte_length = 0x11;  // -2
        const int max_length = 0x111; // -0x12
        int max_back_distance = (int)Level;
        bool bad_compression = Level == CompressionLevel.None; // if enabled, nothing actually gets compressed and the yaz0 file ends up being bigger than the original, but the resulting file is still valid

        while (dataStream.Position < dataStream.Length)
        {
            if (bad_compression)
            {
                bw.Write((byte)0xFF);
                bw.Write(ReadBytes(8, dataStream));
                if (dataStream.Position >= dataStream.Length)
                    break;
                continue;
            }

            byte header = 0; // 0b11111111 -> all identical, 0b00000000 -> all look ahead
            var basepos = bw.BaseStream.Position;
            bw.BaseStream.Seek(1, SeekOrigin.Current); // skip first byte of group before data is ready
            for (byte chunk = 0; chunk < 8; chunk++)
            {
                if (dataStream.Position >= dataStream.Length)
                    break;

                var chunkpos = dataStream.Position;
                byte current = (byte)dataStream.ReadByte();
                dataStream.Position = chunkpos;
                byte[] checkbytes = ReadBytes(3, dataStream);

                var checkpos = chunkpos - max_back_distance > 0 ? chunkpos - max_back_distance : 0; // if we're not over the lookback limit position we start at 0  
                bool matched = false;
                int matchlength = 0;
                ushort distance = 0;
                dataStream.Flush();
                while (checkpos < chunkpos && checkpos < dataStream.Length)
                {
                    dataStream.Position = checkpos;
                    if (checkbytes.SequenceEqual(ReadBytes(3, dataStream)))
                    {
                        matched = true;
                        int oldmatchlength = matchlength;
                        matchlength = 3;
                        var tmpnext = dataStream.ReadByte();
                        dataStream.Position = chunkpos + matchlength;
                        while (tmpnext == dataStream.ReadByte() && dataStream.Position <= dataStream.Length && matchlength < max_length)
                        {
                            matchlength++;
                            dataStream.Position = checkpos + matchlength;
                            tmpnext = dataStream.ReadByte();
                            dataStream.Position = chunkpos + matchlength;
                        }
                        if (oldmatchlength < matchlength) // maintain the result until we find a longer match
                        {
                            dataStream.Position -= 1;
                            distance = (ushort)(chunkpos - checkpos - 1);
                        }
                        else matchlength = oldmatchlength; 
                    } 
                    // Keep checking in case there's a better match
                    checkpos++;
                    dataStream.Flush();
                }
                dataStream.Position = chunkpos + matchlength;

                if (!matched)// write as is, 1:1
                {
                    dataStream.Position = chunkpos + 1;
                    bw.Write(current);
                    header |= (byte)(1 << 7 - chunk);  // tell the header this chunk is 1:1
                }
                else // look ahead
                {
                    if (matchlength - 2 <= max_2Byte_length - 2) // write 2 bytes if it's smaller than or equal to 0xf, else write 3 bytes
                    {
                        // lookAheadLength; // value between 1 and 0xf

                        bw.Write((byte)(((distance & 0x0f00) >> 8) + (matchlength - 2) * 0x10));
                        bw.Write((byte)(distance & 0x00ff));
                        //bw.Write();
                    }
                    else // write 3 bytes
                    {
                        bw.Write((byte)((distance & 0x0f00) >> 8));
                        bw.Write((byte)(distance & 0xff));
                        bw.Write((byte)(matchlength - 0x12));
                    }

                }
            }
            var nextpos = bw.BaseStream.Position;
            bw.BaseStream.Position = basepos;
            bw.Write(header); // write group header byte
            bw.BaseStream.Position = nextpos;
        }
        bw.Close();
        watch.Stop();
        Console.WriteLine($"File compressed in {watch.ElapsedMilliseconds}");
        return resultStream.ToArray();
    }
    private static byte[] ReadBytes(int num, MemoryStream s)
    {
        byte[] ret = new byte[num];
        for (int i = 0; i < num; i++)
        {
            ret[i] = (byte)s.ReadByte();
        }
        return ret;
    }

    // Code based on EveryFileExplorer (https://github.com/Gericom/EveryFileExplorer/blob/master/CommonCompressors/YAZ0.cs)
    public static byte[] Decompress(byte[] Data)
    {
        uint leng = (uint)(Data[4] << 24 | Data[5] << 16 | Data[6] << 8 | Data[7]);
        byte[] Result = new byte[leng];
        int Offs = 16;
        int dstoffs = 0;

        while (true)
        {
            byte header = Data[Offs++];
            for (int i = 0; i < 8; i++)
            {
                if ((header & 0x80) != 0)
                    Result[dstoffs++] = Data[Offs++];
                else
                {
                    byte b = Data[Offs++];
                    int offs = ((b & 0xF) << 8 | Data[Offs++]) + 1;
                    int length = (b >> 4) + 2;
                    if (length == 2)
                        length = Data[Offs++] + 0x12;
                    for (int j = 0; j < length; j++)
                    {
                        Result[dstoffs] = Result[dstoffs - offs];
                        dstoffs++;
                    }
                }
                if (dstoffs >= leng)
                    return Result;
                header <<= 1;
            }
        }
    }
}
