using MemoryMap.Linux.Startup;

namespace MemoryMap.Linux.Tests;

/// <summary>Reading enabled systemd user services from systemctl's output (recorded on WSL Ubuntu 24.04).</summary>
public sealed class SystemdUserServicesTests
{
    internal const string Listing = """
        session-migration.service enabled enabled
        syncthing@.service        enabled enabled
        pipewire.service          enabled enabled

        """;

    internal const string Show = """
        Id=session-migration.service
        Description=Session Migration
        ExecStart={ path=/usr/bin/session-migration ; argv[]=/usr/bin/session-migration ; ignore_errors=no ; start_time=[n/a] ; stop_time=[n/a] ; pid=0 ; code=(null) ; status=0/0 }
        FragmentPath=/usr/lib/systemd/user/session-migration.service

        Id=pipewire.service
        Description=PipeWire Multimedia Service
        ExecStart={ path=/usr/bin/pipewire ; argv[]=/usr/bin/pipewire ; ignore_errors=no ; start_time=[n/a] ; stop_time=[n/a] ; pid=0 ; code=(null) ; status=0/0 }
        FragmentPath=/home/user/.config/systemd/user/pipewire.service

        """;

    [Fact]
    public void Enabled_services_are_read_but_templates_are_left_out()
    {
        Assert.Equal(["session-migration.service", "pipewire.service"], SystemdUserServices.UnitNames(Listing));
    }

    [Fact]
    public void Each_unit_gives_its_name_program_and_file()
    {
        var units = SystemdUserServices.Units(Show.Replace("\n", "\r\n", StringComparison.Ordinal));

        Assert.Equal(2, units.Count);
        Assert.Equal(new ServiceUnit("pipewire.service", "PipeWire Multimedia Service", "/usr/bin/pipewire", "/usr/bin/pipewire",
            "/home/user/.config/systemd/user/pipewire.service"), units[1]);
    }

    [Fact]
    public void Show_is_asked_once_for_all_listed_units()
    {
        var calls = new List<string[]>();
        var units = SystemdUserServices.Read(args =>
        {
            calls.Add(args);
            return new CommandResult(0, args.Contains("show") ? Show : Listing, "");
        });

        Assert.Equal(2, units.Count);
        Assert.Equal(2, calls.Count);
        Assert.Equal(["session-migration.service", "pipewire.service"], calls[1][^2..]);
    }

    [Fact]
    public void Without_systemctl_there_are_no_services_and_no_complaint() =>
        Assert.Empty(SystemdUserServices.Read(_ => null));

    [Fact]
    public void A_systemctl_that_cannot_reach_the_user_manager_is_reported()
    {
        var error = Assert.Throws<IOException>(() =>
            SystemdUserServices.Read(_ => new CommandResult(1, "", "Failed to connect to bus: No medium found\n")));

        Assert.Equal("Failed to connect to bus: No medium found", error.Message);
    }

    [Fact]
    public void A_systemctl_that_ran_out_of_time_is_reported_as_such()
    {
        var error = Assert.Throws<IOException>(() =>
            SystemdUserServices.Read(_ => new CommandResult(-1, "", "systemctl did not answer within 5 s.", TimedOut: true)));

        Assert.Equal("systemctl did not answer within 5 s.", error.Message);
    }

    [Fact]
    public void An_argument_holding_a_semicolon_is_kept_whole()
    {
        var units = SystemdUserServices.Units(
            "Id=x.service\nExecStart={ path=/bin/sh ; argv[]=/bin/sh -c a ; b ; ignore_errors=no ; start_time=[n/a] }\n");

        Assert.Equal(("/bin/sh", "/bin/sh -c a ; b"), (units[0].Program, units[0].Arguments));
    }
}
