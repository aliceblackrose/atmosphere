using System.IO;
using Atmosphere.Services;

namespace Atmosphere.Services;

public sealed record GameCopyProgress(long BytesCopied, long TotalBytes);

/// <summary>
/// Optional full independent game copy for binary overhauls or separate game versions.
/// Source installation is always opened read-only; destination is exclusively owned by
/// the selected profile, and is published only after every file has been copied.
/// </summary>
public sealed class GameCopyService
{
    private readonly ProfileStore _profiles;
    public GameCopyService(ProfileStore profiles) => _profiles = profiles;

    public Task<string> CreateCopyAsync(string profileId, string sourceGame,
        IProgress<GameCopyProgress>? progress = null, CancellationToken token = default) =>
        Task.Run(() => CopyAsync(profileId, sourceGame, progress, token), token);

    private async Task<string> CopyAsync(string profileId, string sourceGame,
        IProgress<GameCopyProgress>? progress, CancellationToken token)
    {
        if (!GameLocator.IsValidGameDirectory(sourceGame))
            throw new ArgumentException("Choose a valid source game installation.");

        var source = Path.GetFullPath(sourceGame);
        var profileDirectory = Path.GetFullPath(_profiles.GetDirectory(profileId));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        bool Descendant(string a, string b) => a.StartsWith(b.TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar, comparison);
        if (source.Equals(profileDirectory, comparison) ||
            Descendant(profileDirectory, source) || Descendant(source, profileDirectory))
            throw new IOException("Game source and target profile directories must not overlap.");

        if (!Directory.Exists(profileDirectory) ||
            (File.GetAttributes(profileDirectory) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Profile working directory is missing or linked.");

        var destination = Path.Combine(profileDirectory, "GameCopy");
        if (Directory.Exists(destination)) throw new IOException(
            "This profile already has a GameCopy. Choose or archive another profile to make another copy.");

        var files = new List<(string Source, string Relative, long Bytes)>();
        var directories = new List<string>();
        var pending = new Stack<string>();
        pending.Push(source);
        long total = 0;

        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var current = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                token.ThrowIfCancellationRequested();
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Game contains a symbolic link or junction that cannot be copied safely: " + entry);
                var relative = Path.GetRelativePath(source, entry);
                if (Directory.Exists(entry))
                {
                    directories.Add(relative);
                    pending.Push(entry);
                }
                else
                {
                    var length = new FileInfo(entry).Length;
                    checked { total += length; }
                    files.Add((entry, relative, length));
                }
            }
        }

        // Do not start copying a large game if the destination drive is already too full.
        var driveRoot = Path.GetPathRoot(profileDirectory);
        if (driveRoot is not null)
        {
            var free = new DriveInfo(driveRoot).AvailableFreeSpace;
            var reserve = Math.Min(total / 20, 1024L * 1024 * 1024);
            if (free < total + reserve)
                throw new IOException("Not enough free disk space for an independent game copy.");
        }

        var staging = Path.Combine(profileDirectory, ".gamecopy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            foreach (var directory in directories)
            {
                token.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.Combine(staging, directory));
            }

            long copied = 0;
            progress?.Report(new GameCopyProgress(0, total));
            var buffer = new byte[1024 * 1024];
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                var target = Path.Combine(staging, file.Relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var input = new FileStream(file.Source, FileMode.Open,
                    FileAccess.Read, FileShare.Read, buffer.Length, useAsync: true);
                await using var output = new FileStream(target, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, buffer.Length, useAsync: true);
                int length;
                while ((length = await input.ReadAsync(buffer, token)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, length), token);
                    copied += length;
                    progress?.Report(new GameCopyProgress(copied, total));
                }

                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(target, File.GetUnixFileMode(file.Source));
            }
            token.ThrowIfCancellationRequested();

            // Publish only a fully copied tree. It remains separate from user-data Mods.
            Directory.Move(staging, destination);
            _profiles.SetGameDirectory(profileId, destination,
                _profiles.Get(profileId).GameVersionNote);
            return destination;
        }
        catch
        {
            if (Directory.Exists(staging))
            {
                try { Directory.Delete(staging, recursive: true); }
                catch (IOException) { /* Leave partial staging for manual recovery if locked. */ }
            }
            throw;
        }
    }
}
