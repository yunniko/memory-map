using System.Globalization;
using MemoryMap.Linux.Startup;

namespace MemoryMap.Linux.Tests;

/// <summary>The Desktop Entry Specification's rules, as the autostart reader and switch use them.</summary>
public sealed class DesktopEntryTests
{
    private const string Sample = """
        # A comment line
        [Desktop Entry]
        Type=Application
        Name = Tool
        Name[cs]=Nástroj
        Name[cs_CZ]=Nástroj (CZ)
        Exec=tool --start
        Exec=ignored, a repeated key
        Comment=One\stwo\nthree\\four

        [Desktop Action New]
        Name=New window
        Hidden=true
        """;

    [Fact]
    public void Keys_are_read_from_the_desktop_entry_group_only()
    {
        var entry = DesktopEntry.Parse(Sample)!;

        Assert.Equal("Tool", entry["Name"]);
        Assert.Equal("tool --start", entry["Exec"]);
        Assert.Null(entry["Hidden"]); // it belongs to the action group
        Assert.False(entry.IsTrue("Hidden"));
        Assert.Equal("One two\nthree\\four", entry["Comment"]);
        Assert.Null(DesktopEntry.Parse("[Other Group]\nName=x\n"));
    }

    [Theory]
    [InlineData("cs-CZ", "Nástroj (CZ)")]
    [InlineData("cs", "Nástroj")]
    [InlineData("de-DE", "Tool")]
    [InlineData("", "Tool")]
    public void The_name_is_taken_in_the_users_language_where_the_file_has_it(string culture, string expected) =>
        Assert.Equal(expected, DesktopEntry.Parse(Sample)!.Localized("Name", CultureInfo.GetCultureInfo(culture)));

    [Fact]
    public void Setting_a_key_changes_its_line_and_nothing_else()
    {
        var text = DesktopEntry.Parse(Sample)!.With("Name", "Renamed");

        Assert.Equal(Sample.Replace("Name = Tool", "Name=Renamed", StringComparison.Ordinal), text);
    }

    [Fact]
    public void A_new_key_goes_at_the_end_of_the_group_before_the_next_one()
    {
        var text = DesktopEntry.Parse(Sample)!.With("Hidden", "true");
        var reread = DesktopEntry.Parse(text)!;

        Assert.True(reread.IsTrue("Hidden"));
        Assert.Contains("Comment=One\\stwo\\nthree\\\\four\nHidden=true\n\n[Desktop Action New]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Windows_line_endings_are_kept()
    {
        var text = "[Desktop Entry]\r\nName=Tool\r\nHidden=false\r\n";

        Assert.Equal("[Desktop Entry]\r\nName=Tool\r\nHidden=true\r\n", DesktopEntry.Parse(text)!.With("Hidden", "true"));
        Assert.Equal("[Desktop Entry]\r\nName=Tool\r\nX=1\r\n", DesktopEntry.Parse("[Desktop Entry]\r\nName=Tool\r\n")!.With("X", "1"));
    }

    [Theory]
    [InlineData("tool --start", new[] { "tool", "--start" })]
    [InlineData("\"/opt/My App/app\" %U --flag", new[] { "/opt/My App/app", "--flag" })]
    [InlineData("sh -c \"echo \\\"hi\\\" \\$HOME\"", new[] { "sh", "-c", "echo \"hi\" $HOME" })]
    [InlineData("tool --level=100%%", new[] { "tool", "--level=100%" })]
    [InlineData("  tool   --a  ", new[] { "tool", "--a" })]
    public void Exec_is_split_by_the_specs_quoting_rules(string exec, string[] expected) =>
        Assert.Equal(expected, DesktopEntry.Arguments(exec));

    [Fact]
    public void An_open_quote_makes_the_command_unreadable() =>
        Assert.Null(DesktopEntry.Arguments("\"/opt/app --flag"));
}
