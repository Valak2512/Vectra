using DualTune.App.Engine;

namespace DualTune.Tests;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2", "1.2.0")]
    [InlineData("V2.0.0", "2.0.0")]
    [InlineData("v1.2.3-beta", "1.2.3")]
    public void ParseTag_Valid(string tag, string expected) => Assert.Equal(Version.Parse(expected), UpdateChecker.ParseTag(tag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    public void ParseTag_Invalid_IsNull(string? tag) => Assert.Null(UpdateChecker.ParseTag(tag));

    [Fact]
    public void NewerVersion_FromReleaseJson()
    {
        const string json = """{ "tag_name": "v1.1.0", "html_url": "https://example" }""";
        Assert.Equal(new Version(1, 1, 0), UpdateChecker.NewerVersion(json, new Version(1, 0, 0)));
        Assert.Null(UpdateChecker.NewerVersion(json, new Version(1, 1, 0)));
        Assert.Null(UpdateChecker.NewerVersion(json, new Version(1, 2, 0)));
    }

    [Theory]
    [InlineData("pas du json")]
    [InlineData("""{ "message": "Not Found" }""")]
    public void NewerVersion_BadAnswer_IsNull(string json) => Assert.Null(UpdateChecker.NewerVersion(json, new Version(1, 0, 0)));

    [Fact]
    public void AppVersion_IsThreeParts() => Assert.Matches(@"^\d+\.\d+\.\d+$", AppInfo.VersionText);
}
