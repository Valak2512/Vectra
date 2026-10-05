using DualTune.App.ViewModels;
using DualTune.Core;

namespace DualTune.Tests;

public sealed class ProfilesViewModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "DualTuneTests", Guid.NewGuid().ToString("N"));
    private readonly ProfileStore _store;
    private Profile _active;
    private string? _nextName;
    private bool _confirm = true;

    public ProfilesViewModelTests()
    {
        _store = new ProfileStore(_dir);
        _store.Save(Profile.CreateDefault() with { Name = "Rocket League", LeftStick = new StickSettings { InnerShape = InnerShape.Square } });
        _active = Profile.CreateDefault();
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private ProfilesViewModel Make()
    {
        var all = _store.LoadAll(out _);
        _active = all.Single(p => p.Name == "Défaut" || all.Count == 1);
        return new ProfilesViewModel(_store, all, _active.Name, p => _active = p, _ => _nextName, _ => _confirm);
    }

    [Fact]
    public void Constructor_ListsNames_SelectsActive()
    {
        _store.Save(Profile.CreateDefault());
        var vm = Make();
        Assert.Equal(new[] { "Défaut", "Rocket League" }, vm.Names.OrderBy(n => n));
        Assert.Equal("Défaut", vm.Selected);
    }

    [Fact]
    public void Select_ActivatesThatProfile()
    {
        _store.Save(Profile.CreateDefault());
        var vm = Make();
        vm.Selected = "Rocket League";
        Assert.Equal("Rocket League", _active.Name);
        Assert.Equal(InnerShape.Square, _active.LeftStick.InnerShape);
    }

    [Fact]
    public void Select_ReturnsLatestEdits_OfPreviouslyActiveProfile()
    {
        _store.Save(Profile.CreateDefault());
        var vm = Make();
        vm.OnActiveChanged(_active with { RightStick = new StickSettings { OuterShape = OuterShape.Square } });
        vm.Selected = "Rocket League";
        vm.Selected = "Défaut";
        Assert.Equal(OuterShape.Square, _active.RightStick.OuterShape);
    }

    [Fact]
    public void New_AddsSavesAndActivates()
    {
        _store.Save(Profile.CreateDefault());
        var vm = Make();
        _nextName = "  FPS  ";
        vm.NewCommand.Execute(null);
        Assert.Contains("FPS", vm.Names);
        Assert.Equal("FPS", vm.Selected);
        Assert.Equal("FPS", _active.Name);
        Assert.Equal(new StickSettings(), _active.LeftStick);
        Assert.Contains(_store.LoadAll(out _), p => p.Name == "FPS");
    }

    [Fact]
    public void New_ExistingName_SetsErrorAndDoesNothing()
    {
        _store.Save(Profile.CreateDefault());
        var vm = Make();
        _nextName = "rocket league";
        vm.NewCommand.Execute(null);
        Assert.NotNull(vm.Error);
        Assert.Equal(2, vm.Names.Count);
    }

    [Fact]
    public void New_Cancelled_DoesNothing()
    {
        _store.Save(Profile.CreateDefault());
        var vm = Make();
        _nextName = null;
        vm.NewCommand.Execute(null);
        Assert.Equal(2, vm.Names.Count);
        Assert.Null(vm.Error);
    }

    [Fact]
    public void Duplicate_CopiesActiveSettings()
    {
        _store.Save(Profile.CreateDefault());
        var vm = Make();
        vm.Selected = "Rocket League";
        _nextName = "RL 2";
        vm.DuplicateCommand.Execute(null);
        Assert.Equal("RL 2", _active.Name);
        Assert.Equal(InnerShape.Square, _active.LeftStick.InnerShape);
        Assert.Equal(3, vm.Names.Count);
    }

    [Fact]
    public void Rename_RenamesFileAndActive()
    {
        _store.Save(Profile.CreateDefault());
        var vm = Make();
        vm.Selected = "Rocket League";
        _nextName = "RL";
        vm.RenameCommand.Execute(null);
        Assert.Equal("RL", vm.Selected);
        Assert.Equal("RL", _active.Name);
        Assert.Equal(InnerShape.Square, _active.LeftStick.InnerShape);
        var names = _store.LoadAll(out _).Select(p => p.Name).ToList();
        Assert.Contains("RL", names);
        Assert.DoesNotContain("Rocket League", names);
    }

    [Fact]
    public void Delete_RemovesAndActivatesAnother()
    {
        _store.Save(Profile.CreateDefault());
        var vm = Make();
        vm.Selected = "Rocket League";
        vm.DeleteCommand.Execute(null);
        Assert.DoesNotContain("Rocket League", vm.Names);
        Assert.Equal("Défaut", _active.Name);
        Assert.DoesNotContain(_store.LoadAll(out _), p => p.Name == "Rocket League");
    }

    [Fact]
    public void Delete_NotConfirmed_DoesNothing()
    {
        _store.Save(Profile.CreateDefault());
        var vm = Make();
        _confirm = false;
        vm.DeleteCommand.Execute(null);
        Assert.Equal(2, vm.Names.Count);
    }

    [Fact]
    public void Delete_LastProfile_IsRefused()
    {
        var vm = Make(); // seul « Rocket League » existe
        Assert.False(vm.DeleteCommand.CanExecute(null));
        vm.DeleteCommand.Execute(null);
        Assert.Single(vm.Names);
    }
}

public sealed class ProfileExportImportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "DualTuneTests", Guid.NewGuid().ToString("N"));
    private readonly ProfileStore _store;
    private Profile _active = Profile.CreateDefault();
    private string? _savePath;
    private string? _openPath;

    public ProfileExportImportTests()
    {
        _store = new ProfileStore(Path.Combine(_dir, "data"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private ProfilesViewModel Make()
    {
        var all = _store.LoadAll(out _);
        _active = all[0];
        return new ProfilesViewModel(_store, all, _active.Name, p => _active = p, _ => null, _ => true,
            _ => _savePath, () => _openPath);
    }

    [Fact]
    public void Store_ExportThenImport_RoundTrips()
    {
        var p = Profile.CreateDefault() with { Name = "FPS", LeftStick = new StickSettings { InnerShape = InnerShape.Square, InnerDeadzone = 0.12f } };
        var file = Path.Combine(_dir, "fps.json");
        _store.Export(p, file);
        var back = _store.Import(file);
        Assert.Equal("FPS", back.Name);
        Assert.Equal(InnerShape.Square, back.LeftStick.InnerShape);
        Assert.Equal(0.12f, back.LeftStick.InnerDeadzone);
    }

    [Fact]
    public void Store_Import_InvalidFile_Throws()
    {
        var file = Path.Combine(_dir, "bad.json");
        File.WriteAllText(file, "pas du json");
        Assert.ThrowsAny<Exception>(() => _store.Import(file));
    }

    [Fact]
    public void Export_WritesActiveProfile()
    {
        var vm = Make();
        _savePath = Path.Combine(_dir, "out.json");
        vm.ExportCommand.Execute(null);
        Assert.Equal(_active.Name, _store.Import(_savePath).Name);
        Assert.Null(vm.Error);
    }

    [Fact]
    public void Import_AddsAndSelects_WithUniqueName()
    {
        var vm = Make();
        _openPath = Path.Combine(_dir, "in.json");
        _store.Export(_active with { RightStick = new StickSettings { OuterShape = OuterShape.Square } }, _openPath);

        vm.ImportCommand.Execute(null);

        Assert.Null(vm.Error);
        var newName = "Défaut (2)";
        Assert.Contains(newName, vm.Names);
        Assert.Equal(newName, vm.Selected);
        Assert.Equal(OuterShape.Square, _active.RightStick.OuterShape);
        Assert.Contains(_store.LoadAll(out _), p => p.Name == newName);
    }

    [Fact]
    public void Import_Cancelled_DoesNothing()
    {
        var vm = Make();
        int before = vm.Names.Count;
        vm.ImportCommand.Execute(null);
        Assert.Equal(before, vm.Names.Count);
    }

    [Fact]
    public void Import_InvalidFile_ShowsError()
    {
        var vm = Make();
        _openPath = Path.Combine(_dir, "bad.json");
        File.WriteAllText(_openPath, """{ "Name": "" }""");
        vm.ImportCommand.Execute(null);
        Assert.NotNull(vm.Error);
        Assert.Single(vm.Names);
    }
}
