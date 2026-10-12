using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;

namespace MemoryMap.Windows.Startup;

/// <summary>Reads .lnk shortcuts through the Shell's own ShellLink object.</summary>
[SupportedOSPlatform("windows")]
internal static class Shortcut
{
    // Room well beyond MAX_PATH (260) and INFOTIPSIZE (1024), the usual limits for both.
    private const int BufferSize = 4096;

    /// <summary>The shortcut's target and arguments; null when the file is not a readable shortcut.</summary>
    public static (string Target, string Arguments)? Read(string lnkPath)
    {
        object? link = null;
        try
        {
            link = new ShellLink();
            ((IPersistFile)link).Load(lnkPath, 0);
            var shell = (IShellLinkW)link;
            var target = new StringBuilder(BufferSize);
            shell.GetPath(target, target.Capacity, 0, 0);
            var arguments = new StringBuilder(BufferSize);
            shell.GetArguments(arguments, arguments.Capacity);
            return (Environment.ExpandEnvironmentVariables(target.ToString()), arguments.ToString());
        }
        catch (Exception e) when (e is COMException or FileNotFoundException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
        finally
        {
            if (link is not null)
            {
                Marshal.ReleaseComObject(link);
            }
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink;

#pragma warning disable IDE1006 // COM methods keep the Windows SDK's names and order.
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int size, nint findData, uint flags);
        void GetIDList(out nint list);
        void SetIDList(nint list);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int size);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int size);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int size);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int size, out int icon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int icon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(nint hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
#pragma warning restore IDE1006
}
