using System.Globalization;
using System.Text.RegularExpressions;
using DualTune.App.Localization;
using DualTune.Core;

namespace DualTune.Tests;

public class LocTests
{
    [Theory]
    [InlineData("fr", "en-US", "fr")]
    [InlineData("en", "fr-FR", "en")]
    [InlineData(null, "fr-FR", "fr")]
    [InlineData(null, "fr-CA", "fr")]
    [InlineData(null, "en-GB", "en")]
    [InlineData(null, "de-DE", "en")]
    [InlineData("zz", "fr-FR", "fr")] // valeur inconnue = automatique
    public void Resolve_SavedChoiceElseWindowsLanguage(string? saved, string ui, string expected) =>
        Assert.Equal(expected, Loc.Resolve(saved, new CultureInfo(ui)));

    [Fact]
    public void EveryKey_HasBothTranslations()
    {
        foreach (var (key, (fr, en)) in Strings.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(fr), $"{key} : français manquant");
            Assert.False(string.IsNullOrWhiteSpace(en), $"{key} : anglais manquant");
        }
    }

    [Fact]
    public void Indexer_ReturnsCurrentLanguage_AndNotifiesOnChange()
    {
        var loc = new Loc("fr");
        var raised = new List<string?>();
        int changed = 0;
        loc.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        loc.LanguageChanged += () => changed++;

        Assert.Equal("PARAMÈTRES", loc["tab.settings"]);
        loc.Language = "en";
        Assert.Equal("SETTINGS", loc["tab.settings"]);
        Assert.Contains("Item[]", raised);
        Assert.Equal(1, changed);
        Assert.True(loc.IsEnglish);
        Assert.False(loc.IsFrench);

        loc.Language = "en"; // inchangé : pas de notification
        Assert.Equal(1, changed);
        loc.IsFrench = true;
        Assert.Equal("fr", loc.Language);
    }

    [Fact]
    public void UnknownKey_IsVisibleNotThrown() => Assert.Equal("‹nope›", new Loc("fr")["nope"]);

    [Fact]
    public void Format_UsesTranslation() =>
        Assert.Equal("Delete the profile “A”?", new Loc("en").Format("profiles.confirmDelete", "A"));

    [Fact]
    public void DynamicKeys_AllExist()
    {
        var keys = new List<string>();
        keys.AddRange(Enum.GetValues<Buttons>().Where(b => b != Buttons.None).Select(b => "button." + b));
        keys.AddRange(Enum.GetValues<XboxTarget>().Select(t => "target." + t));
        keys.AddRange(ResponseCurve.Presets.Keys.Select(n => "preset." + n));
        foreach (var key in keys) Assert.True(Strings.All.ContainsKey(key), $"clé manquante : {key}");
    }

    [Fact]
    public void KeysUsedInSource_AllExist()
    {
        var app = Path.Combine(RepoRoot(), "src", "DualTune.App");
        var used = new HashSet<string>();
        foreach (var file in Directory.EnumerateFiles(app, "*.xaml", SearchOption.AllDirectories).Where(NotBuildOutput))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"\{l:Tr ([A-Za-z0-9_.]+)\}")) used.Add(m.Groups[1].Value);
        foreach (var file in Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories).Where(NotBuildOutput))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"\b(?:T|F|Format)\(""([A-Za-z0-9_.]+)""\s*[,)]")) used.Add(m.Groups[1].Value);

        Assert.True(used.Count > 50, $"seulement {used.Count} clés trouvées : la recherche est cassée");
        var missing = used.Where(k => !Strings.All.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, "clés manquantes : " + string.Join(", ", missing));
    }

    private static bool NotBuildOutput(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "DualTune.App"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("racine du dépôt introuvable");
    }
}
