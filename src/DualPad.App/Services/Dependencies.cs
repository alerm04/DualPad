using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using DualPad.Core.Output;
using Microsoft.Win32;

namespace DualPad.App.Services;

public enum DependencyKind { DotNetRuntime, ViGEmBus, HidHide }

/// <summary>Something DualPad needs (or benefits from) that is installed separately.</summary>
public sealed record Dependency(
    DependencyKind Kind,
    string Name,
    string DescriptionKey,
    bool Required,
    string WingetId,
    string InfoUrl,
    string AfterInstallKey);

public sealed record DependencyStatus(bool Installed, string? Version);

public sealed record InstallResult(bool Success, string Message);

/// <summary>Detects and installs DualPad's dependencies through winget.</summary>
public static class Dependencies
{
    // winget exit code for "already installed / no newer version available".
    private const int NoApplicableUpgrade = unchecked((int)0x8A15002B);
    private const int AlreadyInstalled = unchecked((int)0x8A150061);

    private static readonly Dependency[] Catalog =
    [
        new(DependencyKind.ViGEmBus, "ViGEmBus", "dep.vigem.desc",
            Required: true, "ViGEm.ViGEmBus", "https://github.com/nefarius/ViGEmBus/releases/latest", "dep.vigem.after"),
        new(DependencyKind.DotNetRuntime, ".NET 9 Desktop Runtime", "dep.dotnet.desc",
            Required: true, "Microsoft.DotNet.DesktopRuntime.9", "https://dotnet.microsoft.com/download/dotnet/9.0", "dep.dotnet.after"),
        new(DependencyKind.HidHide, "HidHide", "dep.hidhide.desc",
            Required: false, "Nefarius.HidHide", "https://github.com/nefarius/HidHide/releases/latest", "dep.hidhide.after"),
    ];

    /// <summary>
    /// True when DualPad runs on the machine-wide .NET runtime. The standalone build carries its own
    /// runtime, so installing or updating .NET does not affect it.
    /// </summary>
    public static bool UsesSharedRuntime =>
        RuntimeEnvironment.GetRuntimeDirectory().Contains(@"\dotnet\shared\", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<Dependency> All =>
        Catalog.Where(d => d.Kind != DependencyKind.DotNetRuntime || UsesSharedRuntime).ToList();

    public static bool IsWingetAvailable => FindWinget() is not null;

    public static DependencyStatus GetStatus(Dependency dependency) => dependency.Kind switch
    {
        DependencyKind.DotNetRuntime => new(true, Environment.Version.ToString()),
        DependencyKind.ViGEmBus => new(XboxOutput.IsDriverInstalled(), FindInstalledVersion("ViGEm Bus Driver")),
        DependencyKind.HidHide => new(HidHideService.IsInstalled, FindInstalledVersion("HidHide")),
        _ => new(false, null),
    };

    /// <summary>Installs or updates a dependency with winget. Windows asks for administrator rights when the installer needs them.</summary>
    public static async Task<InstallResult> InstallAsync(Dependency dependency, IProgress<string>? progress = null, CancellationToken token = default)
    {
        var winget = FindWinget();
        if (winget is null)
        {
            OpenUrl(dependency.InfoUrl);
            return new(false, Loc.T("dep.noWinget"));
        }

        var info = new ProcessStartInfo(winget)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in new[]
                 {
                     "install", "--id", dependency.WingetId, "--exact", "--source", "winget", "--silent",
                     "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity",
                 })
            info.ArgumentList.Add(arg);

        try
        {
            using var process = Process.Start(info)!;
            var lastLine = "";
            process.OutputDataReceived += (_, e) =>
            {
                var line = e.Data?.Trim();
                // winget draws spinners and progress bars with control characters; keep only readable lines.
                if (string.IsNullOrEmpty(line) || line.Length < 4 || line.All(c => "-\\|/ █▒".Contains(c))) return;
                lastLine = line;
                progress?.Report(line);
            };
            process.BeginOutputReadLine();
            await process.WaitForExitAsync(token);

            return process.ExitCode switch
            {
                0 => new(true, Loc.T("dep.success", Loc.T(dependency.AfterInstallKey))),
                NoApplicableUpgrade or AlreadyInstalled => new(true, Loc.T("dep.upToDate")),
                _ => new(false, Loc.T("dep.failed", process.ExitCode.ToString("X8"), lastLine).Trim()),
            };
        }
        catch (Win32Exception ex)
        {
            return new(false, Loc.T("dep.cantStart", ex.Message));
        }
    }

    public static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private static string? FindWinget()
    {
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\winget.exe");
        if (File.Exists(local)) return local;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), "winget.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>Looks up a product's version in the Windows "installed apps" list.</summary>
    private static string? FindInstalledVersion(string displayNamePrefix)
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall is null) continue;
            foreach (var name in uninstall.GetSubKeyNames())
            {
                using var key = uninstall.OpenSubKey(name);
                if (key?.GetValue("DisplayName") is string display && display.StartsWith(displayNamePrefix, StringComparison.OrdinalIgnoreCase))
                    return key.GetValue("DisplayVersion") as string;
            }
        }
        return null;
    }

    /// <summary>The process architecture, shown next to the runtime version.</summary>
    public static string Architecture => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
}
