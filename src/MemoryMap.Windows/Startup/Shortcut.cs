using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;

namespace MemoryMap.Windows.Startup;

/// <summary>Reads .lnk shortcuts through the Shell's own ShellLink object.</summary>
/// <remarks>COM goes through source-generated interfaces, which trimming and AOT keep (D042).</remarks>
[SupportedOSPlatform("windows")]
internal static partial class Shortcut
{
    // Room well beyond MAX_PATH (260) and INFOTIPSIZE (1024), the usual limits for both.
    private const int BufferSize = 4096;
    private const int ClsctxInprocServer = 1;
    private static readonly Guid ShellLinkClass = new("00021401-0000-0000-C000-000000000046");
    private static readonly Guid ShellLinkInterface = new(IShellLinkW.Iid);

    /// <summary>The shortcut's target and arguments; null when the file is not a readable shortcut.</summary>
    public static unsafe (string Target, string Arguments)? Read(string lnkPath)
    {
        if (NewShellLink() is not { } link)
        {
            return null;
        }
        try
        {
            ((IPersistFile)(object)link).Load(lnkPath, 0);
            var shell = (IShellLinkW)(object)link;
            var buffer = stackalloc char[BufferSize];
            shell.GetPath(buffer, BufferSize, 0, 0);
            var target = new string(buffer);
            shell.GetArguments(buffer, BufferSize);
            return (Environment.ExpandEnvironmentVariables(target), new string(buffer));
        }
        catch (Exception e) when (e is COMException or FileNotFoundException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
        finally
        {
            link.FinalRelease();
        }
    }

    /// <summary>A new, empty ShellLink object (cast it to <see cref="IShellLinkW"/> or
    /// <see cref="IPersistFile"/>); null when COM cannot create one. The caller calls FinalRelease.</summary>
    internal static ComObject? NewShellLink()
    {
        var clsid = ShellLinkClass;
        var iid = ShellLinkInterface;
        if (CoCreateInstance(ref clsid, 0, ClsctxInprocServer, ref iid, out var unknown) != 0)
        {
            return null;
        }
        try
        {
            return (ComObject)Com.Wrappers.GetOrCreateObjectForComInstance(unknown, CreateObjectFlags.UniqueInstance);
        }
        finally
        {
            Marshal.Release(unknown); // the wrapper holds its own reference
        }
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(ref Guid clsid, nint outer, int context, ref Guid iid, out nint instance);

#pragma warning disable IDE1006 // COM methods keep the Windows SDK's names and order.
    [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    internal partial interface IPersistFile
    {
        // IPersist's one method comes first, as in the vtable.
        void GetClassID(out Guid classId);
        [PreserveSig] int IsDirty();
        void Load(string fileName, uint mode);
        void Save(string? fileName, int remember);
        void SaveCompleted(string fileName);
        void GetCurFile(out nint fileName);
    }

    [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
    [Guid(Iid)]
    internal unsafe partial interface IShellLinkW
    {
        const string Iid = "000214F9-0000-0000-C000-000000000046";

        void GetPath(char* file, int size, nint findData, uint flags);
        void GetIDList(out nint list);
        void SetIDList(nint list);
        void GetDescription(char* name, int size);
        void SetDescription(string name);
        void GetWorkingDirectory(char* dir, int size);
        void SetWorkingDirectory(string dir);
        void GetArguments(char* args, int size);
        void SetArguments(string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation(char* path, int size, out int icon);
        void SetIconLocation(string path, int icon);
        void SetRelativePath(string path, uint reserved);
        void Resolve(nint hwnd, uint flags);
        void SetPath(string file);
    }
#pragma warning restore IDE1006
}
