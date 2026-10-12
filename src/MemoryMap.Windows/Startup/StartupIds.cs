using MemoryMap.Core;

namespace MemoryMap.Windows.Startup;

/// <summary>Which registration an entry id names: a Run value or a Startup-folder file, in which scope.</summary>
/// <param name="Scope">"user", "machine" or "machine32" (the 32-bit Run key).</param>
/// <param name="Name">The Run value's name, or the file's name in its Startup folder.</param>
internal sealed record StartupTarget(StartupSource Source, string Scope, string Name)
{
    /// <summary>The StartupApproved subkey that holds this registration's on/off flag.</summary>
    public string ApprovalPath => Source == StartupSource.RunKey ? StartupApproval.Run : StartupApproval.Folder;
}

/// <summary>
/// The one place entry ids for Run values and Startup-folder files are built and taken apart,
/// so the reader and the switch agree on them. Store tasks have their own ids and no switch here.
/// </summary>
internal static class StartupIds
{
    public const string User = "user";
    public const string Machine = "machine";
    public const string Machine32 = "machine32";

    public static string Run(string scope, string valueName) => $"run/{scope}/{valueName}";

    public static string Folder(string scope, string fileName) => $"folder/{scope}/{fileName}";

    /// <summary>The registration behind <paramref name="id"/>; throws naming the id when it is not a Run or folder id.</summary>
    public static StartupTarget Parse(string id)
    {
        var parts = id.Split('/', 3);
        return parts switch
        {
            ["run", User or Machine or Machine32, { Length: > 0 } name] => new(StartupSource.RunKey, parts[1], name),
            ["folder", User or Machine, { Length: > 0 } name] => new(StartupSource.StartupFolder, parts[1], name),
            _ => throw new ArgumentException($"'{id}' is not the id of a Run value or a Startup-folder file.", nameof(id)),
        };
    }
}
