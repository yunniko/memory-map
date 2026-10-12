using System.Globalization;
using System.Text;

namespace MemoryMap.Linux.Startup;

/// <summary>
/// The <c>[Desktop Entry]</c> group of a .desktop file, read by the Desktop Entry Specification
/// (1.5): its keys, and an edit that sets one key while every other line stays as it was.
/// </summary>
internal sealed class DesktopEntry
{
    private const string Group = "[Desktop Entry]";
    private readonly string[] _lines;
    private readonly int _header; // the group's header line
    private readonly int _end; // the line after the group
    private readonly Dictionary<string, (int Line, string Value)> _keys;

    private DesktopEntry(string[] lines, int header, int end, Dictionary<string, (int, string)> keys)
    {
        (_lines, _header, _end, _keys) = (lines, header, end, keys);
    }

    /// <summary>The entry; null when the text has no <c>[Desktop Entry]</c> group.</summary>
    public static DesktopEntry? Parse(string text)
    {
        var lines = text.Split('\n');
        var header = Array.FindIndex(lines, l => l.TrimEnd('\r').Trim() == Group);
        if (header < 0)
        {
            return null;
        }
        var keys = new Dictionary<string, (int, string)>(StringComparer.Ordinal);
        var end = header + 1;
        for (; end < lines.Length; end++)
        {
            var line = lines[end].TrimEnd('\r').Trim();
            if (line.StartsWith('['))
            {
                break;
            }
            var equals = line.IndexOf('=');
            if (line.StartsWith('#') || equals <= 0)
            {
                continue;
            }
            // Spaces around '=' are ignored; a repeated key is invalid, so the first one stands.
            keys.TryAdd(line[..equals].TrimEnd(), (end, line[(equals + 1)..].TrimStart()));
        }
        return new DesktopEntry(lines, header, end, keys);
    }

    /// <summary>The key's value with the string escapes (\s \n \t \r \\) applied; null if absent.</summary>
    public string? this[string key] => _keys.TryGetValue(key, out var found) ? Unescape(found.Value) : null;

    /// <summary>The group's keys with their values as written (escapes kept), in file order.</summary>
    public IEnumerable<(string Key, string Value)> RawKeys => _keys.OrderBy(k => k.Value.Line).Select(k => (k.Key, k.Value.Value));

    /// <summary>A boolean key: only the literal "true" is true.</summary>
    public bool IsTrue(string key) => this[key] == "true";

    /// <summary>Whether the session starts it: not Hidden, and not switched off by GNOME's own key.</summary>
    public bool StartsAtLogin => !IsTrue("Hidden") && this["X-GNOME-Autostart-enabled"] != "false";

    /// <summary>A list key (OnlyShowIn=GNOME;KDE;), its items without empty ones.</summary>
    public IReadOnlyList<string> List(string key) =>
        this[key] is { } value ? value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [];

    /// <summary>A localestring key in <paramref name="culture"/>: Name[cs_CZ], then Name[cs], then Name.</summary>
    public string? Localized(string key, CultureInfo culture)
    {
        if (culture.Name.Length > 0)
        {
            var language = culture.TwoLetterISOLanguageName;
            if (this[$"{key}[{culture.Name.Replace('-', '_')}]"] is { } exact)
            {
                return exact;
            }
            if (this[$"{key}[{language}]"] is { } general)
            {
                return general;
            }
        }
        return this[key];
    }

    /// <summary>The file's text with <paramref name="key"/> set to <paramref name="value"/>: the
    /// key's line replaced, or a new line added at the end of the group. Nothing else changes.</summary>
    public string With(string key, string value)
    {
        var lines = _lines.ToList();
        var crlf = lines[_header].EndsWith('\r') ? "\r" : "";
        if (_keys.TryGetValue(key, out var found))
        {
            lines[found.Line] = $"{key}={value}{(lines[found.Line].EndsWith('\r') ? "\r" : "")}";
        }
        else
        {
            // After the group's last non-blank line, so a blank line before the next group stays.
            var at = _end;
            while (at > _header + 1 && lines[at - 1].TrimEnd('\r').Trim().Length == 0)
            {
                at--;
            }
            lines.Insert(at, $"{key}={value}{crlf}");
        }
        return string.Join('\n', lines);
    }

    /// <summary>The string escapes every value may carry.</summary>
    public static string Unescape(string value)
    {
        if (!value.Contains('\\'))
        {
            return value;
        }
        var text = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                // Anything else (\" in an Exec value) is not a string escape: kept for the quoting rules.
                text.Append(value[++i] switch
                {
                    's' => " ",
                    'n' => "\n",
                    't' => "\t",
                    'r' => "\r",
                    '\\' => "\\",
                    var c => $"\\{c}",
                });
            }
            else
            {
                text.Append(value[i]);
            }
        }
        return text.ToString();
    }

    /// <summary>
    /// The arguments of an (already unescaped) Exec value: split at spaces outside double quotes;
    /// inside quotes a backslash escapes " ` $ and \. Field codes (%f, %U, …) are dropped and %%
    /// is a percent sign. Null when a quote is left open.
    /// </summary>
    public static IReadOnlyList<string>? Arguments(string exec)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var started = false;
        for (var i = 0; i < exec.Length; i++)
        {
            var c = exec[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    inQuotes = false;
                }
                else if (c == '\\' && i + 1 < exec.Length && exec[i + 1] is '"' or '`' or '$' or '\\')
                {
                    current.Append(exec[++i]);
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c is ' ' or '\t')
            {
                if (started)
                {
                    args.Add(current.ToString());
                    current.Clear();
                    started = false;
                }
            }
            else if (c == '"')
            {
                inQuotes = started = true;
            }
            else if (c == '%' && i + 1 < exec.Length)
            {
                started = true;
                if (exec[++i] == '%')
                {
                    current.Append('%');
                }
            }
            else
            {
                current.Append(c);
                started = true;
            }
        }
        if (inQuotes)
        {
            return null;
        }
        if (started)
        {
            args.Add(current.ToString());
        }
        // A field code on its own leaves an empty argument: it stood for files, not a word.
        return args.Where(a => a.Length > 0).ToList();
    }
}
