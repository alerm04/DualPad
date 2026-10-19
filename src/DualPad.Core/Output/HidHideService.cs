using System.Diagnostics;
using System.ComponentModel;

namespace DualPad.Core.Output;

/// <summary>
/// Optional integration with Nefarius HidHide: hides the physical DualSense from games so they only
/// see the virtual Xbox controller, while DualPad itself stays allowed to read it.
/// </summary>
public static class HidHideService
{
    public const string DownloadUrl = "https://github.com/nefarius/HidHide/releases/latest";

    private static readonly string[] CliCandidates =
    [
        @"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe",
        @"C:\Program Files\Nefarius Software Solutions e.U\HidHide\x64\HidHideCLI.exe",
    ];

    public static string? CliPath => CliCandidates.FirstOrDefault(File.Exists);

    public static bool IsInstalled => CliPath is not null;

    /// <summary>Whitelists DualPad, hides the given device instances and turns cloaking on (asks for administrator rights).</summary>
    public static bool Hide(IEnumerable<string> instanceIds, string appPath)
    {
        var args = new List<string> { "--app-reg", Quote(appPath) };
        foreach (var id in instanceIds.Distinct()) args.AddRange(["--dev-hide", Quote(id)]);
        args.Add("--cloak-on");
        return Run(string.Join(' ', args));
    }

    /// <summary>Makes the given devices visible again (asks for administrator rights).</summary>
    public static bool Unhide(IEnumerable<string> instanceIds)
    {
        var args = new List<string>();
        foreach (var id in instanceIds.Distinct()) args.AddRange(["--dev-unhide", Quote(id)]);
        return args.Count == 0 || Run(string.Join(' ', args));
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "") + "\"";

    private static bool Run(string arguments)
    {
        var cli = CliPath;
        if (cli is null) return false;
        try
        {
            using var process = Process.Start(new ProcessStartInfo(cli, arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
            });
            if (process is null) return false;
            process.WaitForExit(15000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            // The user declined the UAC prompt.
            return false;
        }
    }
}
