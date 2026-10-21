using System.Globalization;
using System.Text.Json;
using System.Windows.Markup;

namespace DualPad.App.Services;

/// <summary>A language shipped with DualPad, named in its own language.</summary>
public sealed record LanguageInfo(string Code, string Name);

/// <summary>
/// Translated UI text. Each language is a flat JSON file in Localization\ (key → text) embedded in the exe;
/// keys missing from a translation fall back to English.
/// </summary>
public static class Loc
{
    private const string Fallback = "en";
    private const string ResourcePrefix = "DualPad.App.Localization.";

    private static Dictionary<string, string> _strings = [];
    private static Dictionary<string, string> _fallback = [];

    /// <summary>The language in use, e.g. "en" or "pt-BR".</summary>
    public static string Current { get; private set; } = Fallback;

    public static IReadOnlyList<LanguageInfo> Languages { get; } = LoadLanguages();

    /// <summary>Loads <paramref name="code"/>, or the Windows display language when it is empty.</summary>
    public static void Init(string? code)
    {
        Current = Resolve(string.IsNullOrEmpty(code) ? CultureInfo.CurrentUICulture.Name : code);
        _fallback = Load(Fallback);
        _strings = Current == Fallback ? _fallback : Load(Current);
    }

    public static string T(string key) =>
        _strings.TryGetValue(key, out var text) || _fallback.TryGetValue(key, out text) ? text : key;

    public static string T(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);

    /// <summary>Picks the closest shipped language: exact match, then same base language, then English.</summary>
    private static string Resolve(string requested)
    {
        var exact = Languages.FirstOrDefault(l => l.Code.Equals(requested, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact.Code;
        var baseCode = requested.Split('-')[0];
        return Languages.FirstOrDefault(l => l.Code.Split('-')[0].Equals(baseCode, StringComparison.OrdinalIgnoreCase))?.Code ?? Fallback;
    }

    private static List<LanguageInfo> LoadLanguages()
    {
        var assembly = typeof(Loc).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .Select(n => n[ResourcePrefix.Length..^".json".Length])
            .Select(code => new LanguageInfo(code, Load(code).GetValueOrDefault("_language", code)))
            .OrderBy(l => l.Code == Fallback ? "" : l.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    private static Dictionary<string, string> Load(string code)
    {
        using var stream = typeof(Loc).Assembly.GetManifestResourceStream(ResourcePrefix + code + ".json");
        if (stream is null) return [];
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
    }
}

/// <summary>XAML access to translations: <c>Text="{l:T home.title}"</c>.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
