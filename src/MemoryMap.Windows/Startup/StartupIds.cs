using System.Runtime.Versioning;
using MemoryMap.Core;
using Microsoft.Win32;

namespace MemoryMap.Windows.Startup;

/// <summary>Which registration an entry id names: a Run value or a Startup-folder file, in which scope.</summary>
/// <param name="Scope">"user", "machine" or "machine32" (the 32-bit Run key).</param>
/// <param name="Name">The Run value's name, or the file's name in its Startup folder.</param>
[SupportedOSPlatform("windows")]
internal sealed record StartupTarget(StartupSource Source, string Scope, string Name)
{
    public bool IsUser => Scope == StartupIds.User;

    private bool Is32Bit => Scope == StartupIds.Machine32;

    /// <summary>The Run key holding a Run value, under <see cref="Root"/>.</summary>
    public string RunKey => Is32Bit ? WindowsStartupReader.Run32Key : WindowsStartupReader.RunKey;

    /// <summary>The StartupApproved subkey, under <see cref="Root"/>, that holds this registration's on/off flag.</summary>
    public string ApprovalPath => Source == StartupSource.StartupFolder ? StartupApproval.Folder
        : Is32Bit ? StartupApproval.Run32
        : StartupApproval.Run;

    public RegistryKey Root(StartupLocations locations) => IsUser ? locations.User : locations.Machine;

    public string FolderFile(StartupLocations locations) =>
        Path.Combine(IsUser ? locations.UserFolder : locations.MachineFolder, Name);
}

/// <summary>
/// The one place entry ids for Run values and Startup-folder files are built and taken apart,
/// so the reader, the switch and the elevated helper agree on them. Store tasks have their own
/// ids and are switched in Windows Settings.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class StartupIds
{
    public const string User = "user";
    public const string Machine = "machine";
    public const string Machine32 = "machine32";

    public static string Run(string scope, string valueName) => $"run/{scope}/{valueName}";

    public static string Folder(string scope, string fileName) => $"folder/{scope}/{fileName}";

    /// <summary>
    /// The registration behind <paramref name="id"/>; throws naming the id when it is not a Run or
    /// folder id. A folder id names a file in the folder, never a path out of it (the elevated
    /// helper takes ids from its command line).
    /// </summary>
    public static StartupTarget Parse(string id)
    {
        var parts = id.Split('/', 3);
        return parts switch
        {
            ["run", User or Machine or Machine32, { Length: > 0 } name] => new(StartupSource.RunKey, parts[1], name),
            ["folder", User or Machine, { Length: > 0 } name] when Path.GetFileName(name) == name && name is not ("." or "..")
                => new(StartupSource.StartupFolder, parts[1], name),
            _ => throw new ArgumentException($"'{id}' is not the id of a Run value or a Startup-folder file.", nameof(id)),
        };
    }
}
