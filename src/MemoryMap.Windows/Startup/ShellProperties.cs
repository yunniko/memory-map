using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MemoryMap.Windows.Startup;

/// <summary>
/// A file's description and company as Explorer, Task Manager and Settings read them, through
/// the Shell's property system. .NET's FileVersionInfo misses some files' descriptions that the
/// Shell finds (CCXProcess.exe on this PC), and the names must match what Windows shows.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ShellProperties
{
    private static readonly Guid PropertyStoreId = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    private static readonly PropertyKey FileDescription = new(new Guid("0CEF7D53-FA64-11D1-A203-0000F81FEDEE"), 3);
    private static readonly PropertyKey Company = new(new Guid("D5CDD502-2E9C-101B-9397-08002B2CF9AE"), 15);

    /// <summary>Description and company, each null when the file has none; both null when unreadable.</summary>
    public static (string? Description, string? Company) Read(string path)
    {
        var iid = PropertyStoreId;
        if (SHGetPropertyStoreFromParsingName(path, 0, 0, ref iid, out var store) != 0 || store is null)
        {
            return (null, null);
        }
        try
        {
            return (Text(store, FileDescription), Text(store, Company));
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    private static string? Text(IPropertyStore store, PropertyKey key)
    {
        var k = key;
        var value = default(PropVariant);
        try
        {
            if (store.GetValue(ref k, out value) != 0 || value.Type != VtLpwstr || value.Pointer == 0)
            {
                return null;
            }
            return Marshal.PtrToStringUni(value.Pointer)?.Trim() is { Length: > 0 } s ? s : null;
        }
        finally
        {
            _ = PropVariantClear(ref value);
        }
    }

    private const ushort VtLpwstr = 31;

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PropertyKey(Guid format, uint id)
    {
        public readonly Guid Format = format;
        public readonly uint Id = id;
    }

    // PROPVARIANT: a type tag, padding, then a pointer-sized value (plus room for larger ones).
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public nint Pointer;
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHGetPropertyStoreFromParsingName(string path, nint bindContext, int flags, ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore? store);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int PropVariantClear(ref PropVariant value);
}
