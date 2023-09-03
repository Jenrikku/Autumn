using System.Runtime.InteropServices;
using System.Text;

namespace TinyFileDialogsSharp;

internal static unsafe class Internal
{
    public static void* NullPtr => (void*)0;

    public static void* GetStringPtr(string? input, bool forceEmpty = false)
    {
        if (!forceEmpty && string.IsNullOrEmpty(input))
            return NullPtr;

        input ??= string.Empty;
        input = input.Replace("\"", null).Replace("\'", null);

        fixed (void* ptr = input)
            return ptr;
    }

    public static void** GetStringArrayPtr(string[]? input)
    {
        if (input is null || input.Length == 0)
            return (void**)0;

        for (uint i = 0; i < input.Length; i++)
            input[i] = input[i].Replace("\"", null).Replace("\'", null);

        nint ptr = Marshal.UnsafeAddrOfPinnedArrayElement(input, 0);

        return (void**)ptr;
    }

    public static string GetUTF8StringFromPtr(void* ptr)
    {
        if (ptr == NullPtr)
            return string.Empty;

        byte* byteptr = (byte*)ptr;
        int length = 0;

        while (*byteptr++ != 0)
            length++;

        return new((sbyte*)ptr, 0, length, Encoding.UTF8);
    }

    public static string GetUTF16StringFromPtr(char* ptr) => new(ptr);
}
