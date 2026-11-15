using Atmosphere.Services;
using Xunit;

namespace Atmosphere.Tests;

public sealed class SteamLibraryParserTests
{
    [Fact]
    public void ExtractPaths_ReadsNewSteamLibraries_AndUnescapesWindowsPaths()
    {
        const string content = """
            "libraryfolders"
            {
                "0" { "path" "C:\\Program Files (x86)\\Steam" }
                "1" { "path" "D:\\SteamLibrary" }
            }
            """;

        var paths = SteamLibraryParser.ExtractPaths(content);

        Assert.Equal(2, paths.Count);
        Assert.Contains(@"C:\Program Files (x86)\Steam", paths);
        Assert.Contains(@"D:\SteamLibrary", paths);
    }

    [Fact]
    public void ExtractPaths_EmptyFileReturnsNoLibraries()
    {
        Assert.Empty(SteamLibraryParser.ExtractPaths(""));
    }
}
