using System.Runtime.InteropServices;

namespace Autumn.Wrappers;

// Code based on EveryFileExplorer (https://github.com/Gericom/EveryFileExplorer/blob/master/CommonCompressors/YAZ0.cs)
internal static class Yaz0Wrapper
{
    /// <summary>
    /// Defines the level of compression used when compressing.
    /// </summary>
    public static byte? Level { get; set; } = 10;

    public static unsafe byte[] Compress(byte[] data)
    {
        int maxBackLevel = Level is null ? 256 : (int)(0x10e0 * (Level / 9.0) - 0x0e0);

        byte* dataptr = (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(data, 0);

        byte[] result = new byte[data.Length + data.Length / 8 + 0x10];
        byte* resultptr = (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(result, 0);
        *resultptr++ = (byte)'Y';
        *resultptr++ = (byte)'a';
        *resultptr++ = (byte)'z';
        *resultptr++ = (byte)'0';
        *resultptr++ = (byte)(data.Length >> 24 & 0xFF);
        *resultptr++ = (byte)(data.Length >> 16 & 0xFF);
        *resultptr++ = (byte)(data.Length >> 8 & 0xFF);
        *resultptr++ = (byte)(data.Length >> 0 & 0xFF);

        resultptr += 8;

        int length = data.Length;
        int dstoffs = 16;
        int Offs = 0;
        while (true)
        {
            int headeroffs = dstoffs++;
            resultptr++;
            byte header = 0;
            for (int i = 0; i < 8; i++)
            {
                int comp = 0;
                int back = 1;
                int nr = 2;
                {
                    byte* ptr = dataptr - 1;
                    int maxnum = 0x111;
                    if (length - Offs < maxnum)
                        maxnum = length - Offs;
                    //Use a smaller amount of bytes back to decrease time
                    int maxback = maxBackLevel; //0x1000;
                    if (Offs < maxback)
                        maxback = Offs;
                    maxback = (int)dataptr - maxback;
                    int tmpnr;
                    while (maxback <= (int)ptr)
                    {
                        if (*(ushort*)ptr == *(ushort*)dataptr && ptr[2] == dataptr[2])
                        {
                            tmpnr = 3;
                            while (tmpnr < maxnum && ptr[tmpnr] == dataptr[tmpnr])
                                tmpnr++;
                            if (tmpnr > nr)
                            {
                                if (Offs + tmpnr > length)
                                {
                                    nr = length - Offs;
                                    back = (int)(dataptr - ptr);
                                    break;
                                }
                                nr = tmpnr;
                                back = (int)(dataptr - ptr);
                                if (nr == maxnum)
                                    break;
                            }
                        }
                        --ptr;
                    }
                }
                if (nr > 2)
                {
                    Offs += nr;
                    dataptr += nr;
                    if (nr >= 0x12)
                    {
                        *resultptr++ = (byte)(back - 1 >> 8 & 0xF);
                        *resultptr++ = (byte)(back - 1 & 0xFF);
                        *resultptr++ = (byte)(nr - 0x12 & 0xFF);
                        dstoffs += 3;
                    }
                    else
                    {
                        *resultptr++ = (byte)(back - 1 >> 8 & 0xF | (nr - 2 & 0xF) << 4);
                        *resultptr++ = (byte)(back - 1 & 0xFF);
                        dstoffs += 2;
                    }
                    comp = 1;
                }
                else
                {
                    *resultptr++ = *dataptr++;
                    dstoffs++;
                    Offs++;
                }
                header = (byte)(header << 1 | (comp == 1 ? 0 : 1));
                if (Offs >= length)
                {
                    header = (byte)(header << 7 - i);
                    break;
                }
            }
            result[headeroffs] = header;
            if (Offs >= length)
                break;
        }
        while (dstoffs % 4 != 0)
            dstoffs++;
        byte[] realresult = new byte[dstoffs];
        Array.Copy(result, realresult, dstoffs);
        return realresult;
    }

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
