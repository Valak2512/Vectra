using DualTune.Core;

namespace DualTune.Tests;

public sealed class ProfileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "DualTuneTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void ProfileStore_RoundTrip()
    {
        var store = new ProfileStore(_dir);
        var curve = new ResponseCurve(new[] { new CurvePoint(0.4f, 0.1f) });
        var p = Profile.CreateDefault() with
        {
            Name = "Rocket League",
            LeftStick = new StickSettings { InnerShape = InnerShape.Square, OuterShape = OuterShape.Square, InnerDeadzone = 0.12f, Curve = curve },
            R2 = new TriggerSettings { AntiDeadzone = 0.15f },
            ButtonMap = new Dictionary<Buttons, XboxTarget>(ButtonMapper.Default) { [Buttons.LeftPaddle] = XboxTarget.Y },
        };
        store.Save(p);

        var loaded = Assert.Single(store.LoadAll(out var errors), x => x.Name == "Rocket League");
        Assert.Empty(errors);
        Assert.Equal(p.LeftStick, loaded.LeftStick);       // inclut la courbe (égalité par séquence de points)
        Assert.Equal(p.RightStick, loaded.RightStick);
        Assert.Equal(p.R2, loaded.R2);
        Assert.Equal(XboxTarget.Y, loaded.ButtonMap[Buttons.LeftPaddle]);
        Assert.Equal(p.ButtonMap.Count, loaded.ButtonMap.Count);
    }

    [Fact]
    public void LoadAll_EmptyDir_CreatesDefault()
    {
        var all = new ProfileStore(_dir).LoadAll(out var errors);
        Assert.Equal("Défaut", Assert.Single(all).Name);
        Assert.Empty(errors);
        Assert.True(File.Exists(Path.Combine(_dir, "profiles", "Défaut.json")));
    }

    [Fact]
    public void LoadAll_CorruptFile_SkipsAndRecreatesDefault()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "profiles"));
        File.WriteAllText(Path.Combine(_dir, "profiles", "Cassé.json"), "{pas du json");
        var all = new ProfileStore(_dir).LoadAll(out var errors);
        Assert.Single(errors);
        Assert.Contains(all, p => p.Name == "Défaut");
    }

    [Fact]
    public void LoadAll_FutureSchemaVersion_IsSkippedWithError()
    {
        var store = new ProfileStore(_dir);
        store.Save(Profile.CreateDefault() with { Name = "Futur", SchemaVersion = 99 });
        var all = store.LoadAll(out var errors);
        Assert.Single(errors);
        Assert.DoesNotContain(all, p => p.Name == "Futur");
    }

    private void WriteProfileJson(string file, string json)
    {
        Directory.CreateDirectory(Path.Combine(_dir, "profiles"));
        File.WriteAllText(Path.Combine(_dir, "profiles", file), json);
    }

    [Theory]
    [InlineData("""{ "Name": "A", "LeftStick": null }""")]
    [InlineData("""{ "Name": "A", "ButtonMap": null }""")]
    [InlineData("""{ "Name": "A", "R2": { "Curve": null } }""")]
    [InlineData("""{ "Name": "A", "RightStick": { "Curve": null } }""")]
    [InlineData("""{ "Name": "" }""")]
    [InlineData("""{ "Name": null }""")]
    public void LoadAll_IncompleteProfile_IsSkippedWithError(string json)
    {
        WriteProfileJson("A.json", json);
        var all = new ProfileStore(_dir).LoadAll(out var errors);
        Assert.Single(errors);
        Assert.Equal("Défaut", Assert.Single(all).Name);
    }

    [Fact]
    public void LoadAll_DuplicateName_SecondIsSkippedWithError()
    {
        var store = new ProfileStore(_dir);
        store.Save(Profile.CreateDefault());
        File.Copy(Path.Combine(_dir, "profiles", "Défaut.json"), Path.Combine(_dir, "profiles", "Défaut - Copie.json"));
        var all = store.LoadAll(out var errors);
        Assert.Single(all);
        Assert.Single(errors);
    }

    [Fact]
    public void LoadAll_OutOfRangeValues_AreClamped()
    {
        WriteProfileJson("A.json", """
            { "Name": "A",
              "LeftStick": { "AxisSnapAngle": 90, "InnerDeadzone": 5,"OuterDeadzone": -1, "AxialDeadzone": "NaN", "Sensitivity": 10, "CenterOffsetX": 3, "AntiDeadzone": 2 },
              "L2": { "InnerDeadzone": 9, "OuterDeadzone": 9, "AntiDeadzone": -3 } }
            """);
        var store = new ProfileStore(_dir);
        var p = Assert.Single(store.LoadAll(out var errors), x => x.Name == "A");
        Assert.Empty(errors);
        Assert.Equal(0.5f, p.LeftStick.InnerDeadzone);
        Assert.Equal(30f, p.LeftStick.AxisSnapAngle);
        Assert.Equal(0f, p.LeftStick.OuterDeadzone);
        Assert.Equal(0f, p.LeftStick.AxialDeadzone);
        Assert.Equal(2f, p.LeftStick.Sensitivity);
        Assert.Equal(0.2f, p.LeftStick.CenterOffsetX);
        Assert.Equal(0.5f, p.LeftStick.AntiDeadzone);
        Assert.Equal(0.5f, p.L2.InnerDeadzone);
        Assert.Equal(0.5f, p.L2.OuterDeadzone);
        Assert.Equal(0f, p.L2.AntiDeadzone);
    }

    [Fact]
    public void Save_SanitizesFileName()
    {
        var store = new ProfileStore(_dir);
        store.Save(Profile.CreateDefault() with { Name = "a/b:c" });
        Assert.True(File.Exists(Path.Combine(_dir, "profiles", "a_b_c.json")));
        Assert.Contains(store.LoadAll(out _), p => p.Name == "a/b:c");
    }

    [Fact]
    public void RenameAndDelete()
    {
        var store = new ProfileStore(_dir);
        store.Save(Profile.CreateDefault() with { Name = "Un" });
        store.Rename("Un", "Deux");
        var names = store.LoadAll(out _).Select(p => p.Name).ToList();
        Assert.Contains("Deux", names);
        Assert.DoesNotContain("Un", names);
        store.Delete("Deux");
        Assert.DoesNotContain(store.LoadAll(out _), p => p.Name == "Deux");
    }

    [Fact]
    public void Settings_MissingGuideDone_IsFalse()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "LastProfile": "x", "HideController": true, "OutputEnabled": true }""");
        var s = new ProfileStore(_dir).LoadSettings();
        Assert.Equal("x", s.LastProfile);
        Assert.False(s.GuideDone);
    }

    [Fact]
    public void Settings_GuideDone_RoundTrip()
    {
        var store = new ProfileStore(_dir);
        store.SaveSettings(new AppSettings() with { GuideDone = true });
        Assert.True(store.LoadSettings().GuideDone);
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        var store = new ProfileStore(_dir);
        Assert.Equal(new AppSettings(), store.LoadSettings());
        var s = new AppSettings("Rocket League", HideController: false, OutputEnabled: true);
        store.SaveSettings(s);
        Assert.Equal(s, store.LoadSettings());
    }
}
