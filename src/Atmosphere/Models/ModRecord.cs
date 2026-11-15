namespace Atmosphere.Models;

public sealed record ModRecord(
    string FolderName,
    string DirectoryPath,
    string Name,
    string DisplayName,
    string Version,
    string Author,
    string Description,
    bool IsValid,
    bool IsLegacy,
    bool ContainsDll,
    string Warning);
