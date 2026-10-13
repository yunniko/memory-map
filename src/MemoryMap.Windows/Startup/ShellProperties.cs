using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;

namespace MemoryMap.Windows.Startup;

/// <summary>
/// A file's description and company as Explorer, Task Manager and Settings read them, through
/// the Shell's property system. .NET's FileVersionInfo misses some files' descriptions that the
/// Shell finds (CCXProcess.exe on this PC), and the names must match what Windows shows.
/// </summary>
/// <remarks>COM goes through source-generated interfaces, which trimming and AOT keep (D042).</remarks>
[SupportedOSPlatform("windows")]
internal static partial class ShellProperties
{
    private static readonly Guid PropertyStoreId = new(IPropertyStore.Iid);
    private static readonly PropertyKey FileDescription = new(new Guid("0CEF7D53-FA64-11D1-A203-0000F81FEDEE"), 3);
    private static readonly PropertyKey Company = new(new Guid("D5CDD502-2E9C-101B-9397-08002B2CF9AE"), 15);

    /// <summary>Description and company, each null when the file has none; both null when unreadable.</summary>
    public static (string? Description, string? Company) Read(string path)
    {
        var iid = PropertyStoreId;
        if (SHGetPropertyStoreFromParsingName(path, 0, 0, ref iid, out var unknown) != 0 || unknown == 0)
        {
            return (null, null);
        }
        object? store = null;
        try
        {
            store = Com.Wrappers.GetOrCreateObjectForComInstance(unknown, CreateObjectFlags.UniqueInstance);
            var properties = (IPropertyStore)store;
            return (Text(properties, FileDescription), Text(properties, Company));
        }
        finally
        {
            (store as ComObject)?.FinalRelease();
            Marshal.Release(unknown);
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
    internal readonly struct PropertyKey(Guid format, uint id)
    {
        public readonly Guid Format = format;
        public readonly uint Id = id;
    }

    // PROPVARIANT: a type tag, padding, then a pointer-sized value (plus room for larger ones).
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public nint Pointer;
    }

    [GeneratedComInterface]
    [Guid(Iid)]
    internal partial interface IPropertyStore
    {
        const string Iid = "886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99";

        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHGetPropertyStoreFromParsingName(string path, nint bindContext, int flags, ref Guid iid,
        out nint store);

    [LibraryImport("ole32.dll")]
    private static partial int PropVariantClear(ref PropVariant value);
}
