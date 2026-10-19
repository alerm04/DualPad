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
    string Description,
    bool Required,
    string WingetId,
    string InfoUrl,
    string AfterInstall);

public sealed record DependencyStatus(bool Installed, string? Version);

public sealed record InstallResult(bool Success, string Message);

/// <summary>Detects and installs DualPad's dependencies through winget.</summary>
public static class Dependencies
{
    // winget exit code for "already installed / no newer version available".
    private const int NoApplicableUpgrade = unchecked((int)0x8A15002B);
    private const int AlreadyInstalled = unchecked((int)0x8A150061);

    public static IReadOnlyList<Dependency> All { get; } =
    [
        new(DependencyKind.ViGEmBus, "ViGEmBus",
            "Driver che crea il controller Xbox 360 virtuale. Senza di esso i giochi non vedono il controller.",
            Required: true, "ViGEm.ViGEmBus", "https://github.com/nefarius/ViGEmBus/releases/latest",
            "Riavvia DualPad per attivare il controller Xbox."),
        new(DependencyKind.DotNetRuntime, ".NET 9 Desktop Runtime",
            "Il runtime Microsoft su cui gira DualPad. Aggiornarlo porta correzioni di sicurezza e prestazioni.",
            Required: true, "Microsoft.DotNet.DesktopRuntime.9", "https://dotnet.microsoft.com/download/dotnet/9.0",
            "Riavvia DualPad per usare la nuova versione."),
        new(DependencyKind.HidHide, "HidHide",
            "Nasconde il DualSense ai giochi che lo leggono anche direttamente, così non ricevono il doppio input.",
            Required: false, "Nefarius.HidHide", "https://github.com/nefarius/HidHide/releases/latest",
            "Riavvia il PC per completare l'installazione del driver."),
    ];

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
            return new(false, "winget non è disponibile: ho aperto la pagina di download.");
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
                0 => new(true, "Installazione completata. " + dependency.AfterInstall),
                NoApplicableUpgrade or AlreadyInstalled => new(true, "È già installata l'ultima versione."),
                _ => new(false, $"Installazione non riuscita (codice 0x{process.ExitCode:X8}). {lastLine}".Trim()),
            };
        }
        catch (Win32Exception ex)
        {
            return new(false, "Impossibile avviare winget: " + ex.Message);
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
