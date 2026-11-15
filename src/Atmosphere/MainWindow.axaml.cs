using System.Diagnostics;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using Atmosphere.Models;
using Atmosphere.Services;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace Atmosphere;

public partial class MainWindow : Window
{
    private readonly GameLocator _locator = new();
    private readonly LauncherSettingsStore _settingsStore = new();
    private readonly ProfileStore _profiles = new();
    private readonly ProfileStorageService _storage;
    private readonly GitHubReleaseService _github = new();
    private readonly ModSourceStore _sources;
    private readonly ProfileModInstaller _installer;
    private readonly ProfileGameLauncher _profileLauncher;
    private readonly LauncherSettings _settings;

    private readonly TextBox _gamePath, _newProfileName, _renameProfile, _versionNote, _githubUrl, _catalogSearch;
    private readonly TextBlock _gameStatus, _gameDetails, _globalStatus, _selectedProfileText, _profileStorageText;
    private readonly TextBlock _modProfileLabel, _modDetails, _releaseInfo, _selectedAsset, _onlineStatus;
    private readonly Button _vanillaButton, _openFolderButton, _downloadButton, _cancelButton;
    private readonly ListBox _profilesList, _archivedProfilesList, _modsList, _gameModsList, _assetsList, _catalogList;
    private readonly CheckBox _noEac, _replace;
    private readonly ProgressBar _progress;
    private readonly TextBlock _gameCopyStatus;
    private readonly Button _cancelGameCopyButton;
    private List<ModProfile> _profileRows = new();
    private List<ModProfile> _archivedRows = new();
    private List<(ModRecord Mod, bool Disabled)> _modRows = new();
    private GitHubRelease? _currentRelease;
    private IReadOnlyList<GitHubCatalogEntry> _catalogRows = Array.Empty<GitHubCatalogEntry>();
    private CancellationTokenSource? _downloadToken;
    private CancellationTokenSource? _copyToken;
    private bool _busy;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        T Find<T>(string name) where T : Control => this.FindControl<T>(name)
            ?? throw new InvalidOperationException("UI element missing: " + name);

        _gamePath = Find<TextBox>("GamePathTextBox");
        _gameStatus = Find<TextBlock>("GameStatusText");
        _gameDetails = Find<TextBlock>("GameDetailsText");
        _globalStatus = Find<TextBlock>("GlobalStatusText");
        _vanillaButton = Find<Button>("LaunchButton");
        _openFolderButton = Find<Button>("OpenFolderButton");
        _profilesList = Find<ListBox>("ProfileList");
        _archivedProfilesList = Find<ListBox>("ArchivedProfilesList");
        _newProfileName = Find<TextBox>("NewProfileNameTextBox");
        _renameProfile = Find<TextBox>("RenameProfileTextBox");
        _versionNote = Find<TextBox>("VersionNoteTextBox");
        _selectedProfileText = Find<TextBlock>("SelectedProfileText");
        _profileStorageText = Find<TextBlock>("ProfileStorageText");
        _noEac = Find<CheckBox>("NoEacCheckBox");
        _modsList = Find<ListBox>("ModsList");
        _gameModsList = Find<ListBox>("GameModList");
        _modProfileLabel = Find<TextBlock>("ModProfileLabel");
        _modDetails = Find<TextBlock>("ModDetailsText");
        _replace = Find<CheckBox>("ReplaceExistingCheckBox");
        _githubUrl = Find<TextBox>("GitHubUrlTextBox");
        _catalogSearch = Find<TextBox>("CatalogSearchTextBox");
        _catalogList = Find<ListBox>("CatalogResultsList");
        _releaseInfo = Find<TextBlock>("ReleaseInfoText");
        _assetsList = Find<ListBox>("ReleaseAssetsList");
        _selectedAsset = Find<TextBlock>("SelectedAssetText");
        _onlineStatus = Find<TextBlock>("OnlineStatusText");
        _progress = Find<ProgressBar>("DownloadProgressBar");
        _downloadButton = Find<Button>("DownloadInstallButton");
        _cancelButton = Find<Button>("CancelDownloadButton");
        _gameCopyStatus = Find<TextBlock>("GameCopyStatusText");
        _cancelGameCopyButton = Find<Button>("CancelGameCopyButton");

        _storage = new ProfileStorageService(_profiles);
        _installer = new ProfileModInstaller(_profiles);
        _sources = new ModSourceStore(_profiles);
        _profileLauncher = new ProfileGameLauncher(_profiles);
        _settings = _settingsStore.Load();
        _gamePath.Text = GameLocator.IsValidGameDirectory(_settings.GameDirectory)
            ? _settings.GameDirectory : _locator.TryFindGameDirectory();
        RefreshGameStatus();
        RefreshGameMods();
        RefreshProfiles();
    }

    private ModProfile? SelectedProfile =>
        _profilesList.SelectedIndex is var index && index >= 0 && index < _profileRows.Count
            ? _profileRows[index] : null;

    private (ModRecord Mod, bool Disabled)? SelectedMod =>
        _modsList.SelectedIndex is var index && index >= 0 && index < _modRows.Count
            ? _modRows[index] : null;

    private GitHubReleaseAsset? SelectedAsset =>
        _currentRelease is not null && _assetsList.SelectedIndex is var index &&
        index >= 0 && index < _currentRelease.ZipAssets.Count
            ? _currentRelease.ZipAssets[index] : null;

    private void Report(string message) => _globalStatus.Text = message;
    private void Error(string label, Exception exception) =>
        Report(label + ": " + exception.Message);

    private void OnGamePathChanged(object? sender, TextChangedEventArgs e)
    {
        if (_gameStatus is null || _gameModsList is null) return;
        RefreshGameStatus();
        RefreshGameMods();
    }

    private void RefreshGameStatus()
    {
        var valid = GameLocator.IsValidGameDirectory(_gamePath.Text?.Trim());
        _vanillaButton.IsEnabled = valid;
        _openFolderButton.IsEnabled = valid;
        _gameStatus.Text = valid ? "Valid game installation detected." : "Game installation not selected.";
        _gameDetails.Text = valid
            ? _gamePath.Text
            : "Choose a folder containing 7DaysToDie.exe, 7DaysToDie.x86_64, or a macOS app bundle.";
    }

    private void RefreshGameMods()
    {
        var game = GameLocator.IsValidGameDirectory(_gamePath.Text)
            ? ModInventory.Scan(Path.Combine(_gamePath.Text!, "Mods")) : Array.Empty<ModRecord>();
        var user = ModInventory.Scan(ModInventory.GetDefaultUserModsDirectory());
        _gameModsList.ItemsSource = game.Select(FormatMod).Select(x => "Game: " + x)
            .Concat(user.Select(FormatMod).Select(x => "User data: " + x)).DefaultIfEmpty("No existing modlets found.").ToArray();
    }

    private static string FormatMod(ModRecord mod) =>
        (mod.IsValid ? "✓ " : "⚠ ") + (string.IsNullOrWhiteSpace(mod.DisplayName) ? mod.FolderName : mod.DisplayName) +
        "  " + mod.Version + (mod.ContainsDll ? "  [DLL]" : "") +
        (mod.IsValid ? "" : "  — " + mod.Warning);

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose your 7 Days to Die installation",
                AllowMultiple = false
            });
            if (folders.Count > 0)
            {
                var path = folders[0].TryGetLocalPath();
                if (path is not null) _gamePath.Text = path;
            }
        }
        catch (Exception ex) { Error("Could not select game folder", ex); }
    }

    private void OnDetectClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var found = _locator.TryFindGameDirectory();
            if (found is not null) _gamePath.Text = found;
            else Report("No Steam installation detected. Browse to the game folder manually.");
        }
        catch (Exception ex) { Error("Game discovery failed", ex); }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var path = _gamePath.Text?.Trim();
        if (!GameLocator.IsValidGameDirectory(path)) { Report("Select a valid game folder first."); return; }
        try
        {
            _settingsStore.Save(_settings with { GameDirectory = Path.GetFullPath(path!) });
            Report("Game directory saved.");
        }
        catch (Exception ex) { Error("Could not save game directory", ex); }
    }

    private void OnLaunchClick(object? sender, RoutedEventArgs e)
    {
        if (!GameLocator.IsValidGameDirectory(_gamePath.Text)) return;
        try
        {
            SteamLauncher.LaunchVanilla();
            Report("Vanilla game launch sent to Steam (uses your Steam launch options).");
        }
        catch (Exception ex) { Error("Steam launch failed", ex); }
    }

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        if (!GameLocator.IsValidGameDirectory(_gamePath.Text)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _gamePath.Text!,
                UseShellExecute = true
            });
        }
        catch (Exception ex) { Error("Could not open folder", ex); }
    }

    private void OnRefreshGameModsClick(object? sender, RoutedEventArgs e)
    {
        try { RefreshGameMods(); Report("Existing mods refreshed (read only)."); }
        catch (Exception ex) { Error("Could not scan game mods", ex); }
    }

    private void RefreshProfiles(string? selectId = null)
    {
        try
        {
            selectId ??= SelectedProfile?.Id;
            _profileRows = _profiles.List().ToList();
            _archivedRows = _profiles.ListArchived().ToList();
            _archivedProfilesList.ItemsSource = _archivedRows.Select(p => p.Name).ToArray();
            _profilesList.ItemsSource = _profileRows.Select(p => p.Name +
                (string.IsNullOrWhiteSpace(p.GameVersionNote) ? "" : "  [" + p.GameVersionNote + "]")).ToArray();
            var selectedIndex = _profileRows.FindIndex(p => p.Id == selectId);
            _profilesList.SelectedIndex = selectedIndex >= 0 ? selectedIndex : (_profileRows.Count > 0 ? 0 : -1);
            RefreshSelectedProfile();
        }
        catch (Exception ex) { Error("Unable to load profiles", ex); }
    }

    private void OnProfileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_selectedProfileText is null || _modsList is null) return;
        RefreshSelectedProfile();
    }

    private void RefreshSelectedProfile()
    {
        var p = SelectedProfile;
        _selectedProfileText.Text = p is null
            ? "Create a profile to manage modlets."
            : p.Name + " — " + p.Id[..8] + "\nGame: " +
              (p.GameDirectory ?? "(default game directory)") + "\nData: " + _profiles.GetDirectory(p.Id);
        _modProfileLabel.Text = p is null ? "No selected profile." : "Selected: " + p.Name;
        _renameProfile.Text = p?.Name;
        _versionNote.Text = p?.GameVersionNote;
        _profileStorageText.Text = p is null
            ? "Select a profile first."
            : "Select Refresh storage usage to measure this profile's space.";
        RefreshMods();
    }

    private void OnCreateProfileClick(object? sender, RoutedEventArgs e)
    {
        if (_busy) { Report("Finish the current operation first."); return; }
        try
        {
            var p = _profiles.Create(_newProfileName.Text ?? "");
            _newProfileName.Text = "";
            RefreshProfiles(p.Id);
            Report("Created isolated profile " + p.Name + ".");
        }
        catch (Exception ex) { Error("Could not create profile", ex); }
    }

    private void OnRestoreProfileClick(object? sender, RoutedEventArgs e)
    {
        if (_busy) { Report("Finish the current operation before restoring a profile."); return; }
        var index = _archivedProfilesList.SelectedIndex;
        if (index < 0 || index >= _archivedRows.Count)
        {
            Report("Choose an archived profile first.");
            return;
        }
        try
        {
            var profile = _profiles.Restore(_archivedRows[index].Id);
            RefreshProfiles(profile.Id);
            Report("Restored profile " + profile.Name + " with its mods and saves.");
        }
        catch (Exception ex) { Error("Unable to restore profile", ex); }
    }

    private void OnRenameProfileClick(object? sender, RoutedEventArgs e)
    {
        if (_busy) { Report("Finish the current operation first."); return; }
        var p = SelectedProfile;
        if (p is null) { Report("Select a profile first."); return; }
        try
        {
            _profiles.Rename(p.Id, _renameProfile.Text ?? "");
            RefreshProfiles(p.Id);
            Report("Profile renamed.");
        }
        catch (Exception ex) { Error("Could not rename profile", ex); }
    }

    private void OnSetProfileGameClick(object? sender, RoutedEventArgs e)
    {
        if (_busy) { Report("Finish the current operation first."); return; }
        var p = SelectedProfile;
        if (p is null || !GameLocator.IsValidGameDirectory(_gamePath.Text))
        {
            Report("Select a profile and a valid game directory first.");
            return;
        }
        try
        {
            _profiles.SetGameDirectory(p.Id, _gamePath.Text, _versionNote.Text?.Trim());
            RefreshProfiles(p.Id);
            Report("Profile now uses the selected game copy.");
        }
        catch (Exception ex) { Error("Could not update profile game", ex); }
    }

    private async void OnCreateGameCopyClick(object? sender, RoutedEventArgs e)
    {
        var profile = SelectedProfile;
        var source = _gamePath.Text;
        if (profile is null || !GameLocator.IsValidGameDirectory(source))
        {
            Report("Choose a profile and the game to copy on the Game tab first.");
            return;
        }
        if (_busy) { Report("Another operation is already running."); return; }
        _busy = true;
        _copyToken = new CancellationTokenSource();
        _cancelGameCopyButton.IsEnabled = true;
        try
        {
            _gameCopyStatus.Text = "Scanning files and checking disk space...";
            var progress = new Progress<GameCopyProgress>(p =>
            {
                var percent = p.TotalBytes == 0 ? 100 : p.BytesCopied * 100.0 / p.TotalBytes;
                _gameCopyStatus.Text = percent.ToString("F1") + "%  •  " +
                    (p.BytesCopied / 1048576.0).ToString("F0") + " / " +
                    (p.TotalBytes / 1048576.0).ToString("F0") + " MiB copied";
            });
            var copy = new GameCopyService(_profiles);
            var destination = await copy.CreateCopyAsync(profile.Id, source!, progress, _copyToken.Token);
            RefreshProfiles(profile.Id);
            _gameCopyStatus.Text = "Isolated game copy ready: " + destination;
            Report("Game copy created and assigned to profile " + profile.Name + ".");
        }
        catch (OperationCanceledException)
        {
            _gameCopyStatus.Text = "Game copying cancelled; incomplete staging files cleaned up.";
            Report("Game copy cancelled.");
        }
        catch (Exception ex)
        {
            Error("Could not create isolated game copy", ex);
            _gameCopyStatus.Text = ex.Message;
        }
        finally
        {
            _copyToken?.Dispose();
            _copyToken = null;
            _cancelGameCopyButton.IsEnabled = false;
            _busy = false;
        }
    }

    private void OnCancelGameCopyClick(object? sender, RoutedEventArgs e) => _copyToken?.Cancel();

    private void OnLaunchProfileClick(object? sender, RoutedEventArgs e)
    {
        var p = SelectedProfile;
        if (p is null) { Report("Select a profile first."); return; }
        if (_busy) { Report("Finish the current operation first."); return; }
        try
        {
            var gamePath = p.GameDirectory ?? _gamePath.Text;
            var extraMods = ProfileGameLauncher.CountGameRootMods(gamePath);
            _profileLauncher.Launch(p.Id, _gamePath.Text, _noEac.IsChecked == true);
            Report("Launched " + p.Name + " with profile user data." +
                (extraMods > 0 ? " Warning: " + extraMods + " game-root mod folders may also load." : ""));
        }
        catch (Exception ex) { Error("Could not launch profile", ex); }
    }

    private void OnArchiveProfileClick(object? sender, RoutedEventArgs e)
    {
        var p = SelectedProfile;
        if (p is null) { Report("Select a profile first."); return; }
        if (_busy) { Report("Finish the current installation first."); return; }
        try
        {
            _profiles.Archive(p.Id);
            RefreshProfiles();
            Report("Archived " + p.Name + ". No profile files were deleted.");
        }
        catch (Exception ex) { Error("Could not archive profile", ex); }
    }

    private static string DisplayBytes(long bytes)
    {
        double value = bytes;
        var unit = "B";
        foreach (var next in new[] { "KiB", "MiB", "GiB", "TiB" })
        {
            if (value < 1024) break;
            value /= 1024;
            unit = next;
        }
        return value.ToString("F1") + " " + unit;
    }

    private async Task<bool> ConfirmPermanentDeleteAsync(string title, string profileName,
        string consequences)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 560,
            Height = 315,
            MinWidth = 450,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var explanation = new TextBlock
        {
            Text = consequences + "\n\nType the profile name exactly to confirm: " + profileName,
            TextWrapping = TextWrapping.Wrap
        };
        var typed = new TextBox { Watermark = "Type the profile name here" };
        var cancel = new Button { Content = "Cancel", MinWidth = 100 };
        var confirm = new Button
        {
            Content = "Delete permanently",
            MinWidth = 150,
            Background = Brushes.IndianRed,
            Foreground = Brushes.White,
            IsEnabled = false
        };
        typed.TextChanged += (_, _) =>
            confirm.IsEnabled = string.Equals(typed.Text, profileName, StringComparison.Ordinal);
        cancel.Click += (_, _) => dialog.Close(false);
        confirm.Click += (_, _) => dialog.Close(true);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        actions.Children.Add(cancel);
        actions.Children.Add(confirm);
        var content = new StackPanel { Margin = new Thickness(22), Spacing = 14 };
        content.Children.Add(new TextBlock
        {
            Text = title, FontSize = 20, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(explanation);
        content.Children.Add(typed);
        content.Children.Add(actions);
        dialog.Content = content;
        return await dialog.ShowDialog<bool>(this);
    }

    private async void OnRefreshStorageClick(object? sender, RoutedEventArgs e)
    {
        var selected = SelectedProfile;
        if (selected is null) { Report("Select a profile first."); return; }
        if (_busy) return;
        _busy = true;
        try
        {
            _profileStorageText.Text = "Calculating profile storage usage...";
            var usage = await Task.Run(() => _storage.GetUsage(selected.Id));
            _profileStorageText.Text =
                "Total: " + DisplayBytes(usage.TotalBytes) +
                "\nGameCopy: " + DisplayBytes(usage.GameCopyBytes) +
                "\nBackups: " + DisplayBytes(usage.BackupBytes) +
                "\nOther (mods, saves, worlds): " +
                DisplayBytes(usage.TotalBytes - usage.GameCopyBytes - usage.BackupBytes);
        }
        catch (Exception ex) { Error("Unable to measure storage", ex); }
        finally { _busy = false; }
    }

    private void OnOpenProfileFolderClick(object? sender, RoutedEventArgs e)
    {
        var p = SelectedProfile;
        if (p is null) { Report("Select a profile first."); return; }
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _profiles.GetDirectory(p.Id), UseShellExecute = true
            });
        }
        catch (Exception ex) { Error("Could not open profile folder", ex); }
    }

    private async void OnDeleteGameCopyClick(object? sender, RoutedEventArgs e)
    {
        var p = SelectedProfile;
        if (p is null) { Report("Select a profile first."); return; }
        if (_busy) return;
        var path = Path.Combine(_profiles.GetDirectory(p.Id), "GameCopy");
        if (!Directory.Exists(path)) { Report("This profile has no GameCopy to delete."); return; }
        _busy = true;
        try
        {
            var confirmed = await ConfirmPermanentDeleteAsync(
                "Delete this profile's GameCopy?", p.Name,
                "Permanently removes only the game copy stored at:\n" + path +
                "\nProfile mods, saves, and backups stay. External Steam/game folders are not touched. Close the game first.");
            if (!confirmed) return;
            await Task.Run(() => _storage.DeleteGameCopy(p.Id));
            RefreshProfiles(p.Id);
            _profileStorageText.Text = "GameCopy deleted. Profile data retained. Refresh to see storage usage.";
            Report("Removed the GameCopy owned by " + p.Name + "; original game files are untouched.");
        }
        catch (Exception ex) { Error("Could not delete GameCopy; check whether the game is running", ex); }
        finally { _busy = false; }
    }

    private async void OnClearProfileBackupsClick(object? sender, RoutedEventArgs e)
    {
        var p = SelectedProfile;
        if (p is null) { Report("Select a profile first."); return; }
        if (_busy) return;
        _busy = true;
        try
        {
            var confirmed = await ConfirmPermanentDeleteAsync(
                "Clear this profile's backups?", p.Name,
                "Permanently removes all Backups for this profile, including replaced and archived mods. " +
                "Current mods, saves, GameCopy, and external game folders stay. This cannot be undone.");
            if (!confirmed) return;
            await Task.Run(() => _storage.ClearBackups(p.Id));
            _profileStorageText.Text = "Backup cleanup complete. Refresh to see storage usage.";
            Report("Removed stored backups for " + p.Name + ".");
        }
        catch (Exception ex) { Error("Could not clear profile backups", ex); }
        finally { _busy = false; }
    }

    private async void OnDeleteProfileClick(object? sender, RoutedEventArgs e)
    {
        var p = SelectedProfile;
        if (p is null) { Report("Select a profile first."); return; }
        if (_busy) return;
        _busy = true;
        try
        {
            var confirmed = await ConfirmPermanentDeleteAsync(
                "Permanently delete profile?", p.Name,
                "Permanently removes the selected profile, its Mods, Saves, GeneratedWorlds, Backups, " +
                "and any owned GameCopy. This action cannot be undone. Only Atmosphere's profile folder is deleted; " +
                "your Steam game installation is never deleted. Close the game first.");
            if (!confirmed) return;
            await Task.Run(() => _storage.DeleteProfile(p.Id));
            RefreshProfiles();
            Report("Permanently deleted profile " + p.Name + " and its owned files.");
        }
        catch (Exception ex)
        {
            RefreshProfiles();
            Error("Profile deletion failed. Any remaining files may be in Archived Profiles", ex);
        }
        finally { _busy = false; }
    }

    private async void OnDeleteArchivedProfileClick(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var index = _archivedProfilesList.SelectedIndex;
        if (index < 0 || index >= _archivedRows.Count)
        {
            Report("Choose an archived profile first."); return;
        }
        var p = _archivedRows[index];
        _busy = true;
        try
        {
            var confirmed = await ConfirmPermanentDeleteAsync(
                "Permanently delete archived profile?", p.Name,
                "Permanently removes this archived profile's mods, saves, backups, and any owned GameCopy. " +
                "This cannot be undone. No external game installation is deleted.");
            if (!confirmed) return;
            await Task.Run(() => _storage.DeleteArchivedProfile(p.Id));
            RefreshProfiles();
            Report("Permanently deleted archived profile " + p.Name + ".");
        }
        catch (Exception ex) { Error("Unable to delete archived profile", ex); }
        finally { _busy = false; }
    }

    private void RefreshMods()
    {
        var p = SelectedProfile;
        _modRows = new List<(ModRecord, bool)>();
        try
        {
            if (p is not null)
            {
                _modRows.AddRange(_installer.Installed(p.Id).Select(mod => (mod, false)));
                _modRows.AddRange(_installer.Disabled(p.Id).Select(mod => (mod, true)));
            }
            _modsList.ItemsSource = _modRows.Select(row =>
                (row.Item2 ? "○ Disabled   " : "● Enabled   ") + FormatMod(row.Item1))
                .DefaultIfEmpty("No mods installed for this profile.").ToArray();
            _modDetails.Text = p is null ? "Select or create a profile first." :
                "Found " + _modRows.Count + " modlets in this profile.";
        }
        catch (Exception ex) { Error("Could not read profile mods", ex); }
    }

    private void OnModSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_modDetails is null) return;
        var row = SelectedMod;
        if (row is null) return;
        var m = row.Value.Mod;
        _modDetails.Text = m.DisplayName + "  •  Version " + m.Version +
            "\nAuthor: " + m.Author + "\n" + (row.Value.Disabled ? "Disabled" : "Enabled") +
            (m.ContainsDll ? "  •  Code mod: EAC must be off" : "") +
            (m.IsValid ? "" : "\nWarning: " + m.Warning) +
            "\n" + m.Description;
        try
        {
            var source = SelectedProfile is { } p
                ? _sources.List(p.Id).FirstOrDefault(s => s.InternalName == m.Name) : null;
            if (source is not null)
                _modDetails.Text += "\nGitHub: " + source.Repository +
                    " @ " + source.ReleaseTag + " (download SHA-256: " + source.DownloadSha256[..12] + "…)";
        }
        catch (Exception ex) { Error("Could not read mod source metadata", ex); }
    }

    private void OnRefreshModsClick(object? sender, RoutedEventArgs e)
    {
        RefreshMods();
        Report("Profile mod list refreshed.");
    }

    private async void OnInstallZipClick(object? sender, RoutedEventArgs e)
    {
        var p = SelectedProfile;
        if (p is null) { Report("Select a profile first."); return; }
        if (_busy) return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose one or more mod ZIP archives",
                AllowMultiple = true,
                FileTypeFilter = new[] { new FilePickerFileType("ZIP archive") { Patterns = new[] { "*.zip" } } }
            });
            if (files.Count == 0) return;

            var replace = _replace.IsChecked == true;
            var installedNames = new List<string>();
            var failures = new List<string>();
            _busy = true;
            foreach (var selected in files)
            {
                var path = selected.TryGetLocalPath();
                var filename = path is null ? selected.Name : Path.GetFileName(path);
                try
                {
                    if (string.IsNullOrWhiteSpace(path))
                        throw new InvalidOperationException("Choose a locally accessible ZIP file.");

                    Report("Installing " + filename + " (" + (installedNames.Count + failures.Count + 1) +
                        "/" + files.Count + ")...");
                    // Each ZIP is validated and rolled back independently. Earlier successful
                    // ZIPs are retained if a later archive fails; report every failure.
                    var installed = await Task.Run(() => _installer.InstallZip(p.Id, path, replace));
                    _sources.Forget(p.Id, installed.Select(x => x.Name));
                    installedNames.AddRange(installed.Select(x => x.DisplayName));
                }
                catch (Exception ex)
                {
                    failures.Add(filename + ": " + ex.Message);
                }
            }

            RefreshMods();
            Report("Installed " + installedNames.Count + " mod(s) from " +
                files.Count + " ZIP(s)." +
                (failures.Count == 0 ? "" :
                    " Failed ZIP(s): " + string.Join(" | ", failures.Take(3)) +
                    (failures.Count > 3 ? $" (and {failures.Count - 3} more)" : "")));
        }
        catch (Exception ex) { Error("Could not select mod ZIPs", ex); }
        finally { _busy = false; }
    }

    private void OnEnableModClick(object? sender, RoutedEventArgs e) => ToggleSelectedMod(true);
    private void OnDisableModClick(object? sender, RoutedEventArgs e) => ToggleSelectedMod(false);

    private void ToggleSelectedMod(bool enabled)
    {
        var p = SelectedProfile;
        var row = SelectedMod;
        if (p is null || row is null) { Report("Select a mod and profile first."); return; }
        if (_busy) return;
        try
        {
            if (row.Value.Disabled == enabled)
            {
                _installer.SetEnabled(p.Id, row.Value.Mod.FolderName, enabled);
                RefreshMods();
                Report((enabled ? "Enabled " : "Disabled ") + row.Value.Mod.DisplayName + ".");
            }
            else Report("Selected mod is already in the requested state.");
        }
        catch (Exception ex) { Error("Could not change mod state", ex); }
    }

    private void OnArchiveModClick(object? sender, RoutedEventArgs e)
    {
        var p = SelectedProfile;
        var row = SelectedMod;
        if (p is null || row is null) { Report("Select a mod and profile first."); return; }
        if (_busy) return;
        try
        {
            var path = _installer.ArchiveMod(p.Id, row.Value.Mod.FolderName, row.Value.Disabled);
            _sources.Forget(p.Id, new[] { row.Value.Mod.Name });
            RefreshMods();
            Report("Archived mod to " + path + ". Nothing was permanently deleted.");
        }
        catch (Exception ex) { Error("Could not archive mod", ex); }
    }

    private void OnFindModSourceClick(object? sender, RoutedEventArgs e)
    {
        var p = SelectedProfile;
        var mod = SelectedMod;
        if (p is null || mod is null) { Report("Select a mod first."); return; }
        try
        {
            var source = _sources.List(p.Id).FirstOrDefault(x => x.InternalName == mod.Value.Mod.Name);
            if (source is null) { Report("No GitHub source recorded; this may be a manually installed ZIP."); return; }
            _githubUrl.Text = "https://github.com/" + source.Repository;
            Report("Source: " + source.Repository + " @ " + source.ReleaseTag +
                ". Open Online and check the latest release to update.");
        }
        catch (Exception ex) { Error("Could not locate mod source", ex); }
    }

    private async void OnCatalogSearchClick(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            Report("Searching public GitHub repositories...");
            _catalogRows = await _github.SearchCatalogAsync(_catalogSearch.Text ?? "");
            _catalogList.ItemsSource = _catalogRows
                .Select(x => x.Repository + "  ★" + x.Stars + "  — " + x.Description)
                .DefaultIfEmpty("No matching GitHub repositories.").ToArray();
            _catalogList.SelectedIndex = _catalogRows.Count > 0 ? 0 : -1;
            Report("Found " + _catalogRows.Count + " public repositories. Verify each author before installing.");
        }
        catch (Exception ex) { Error("GitHub repository search failed", ex); }
        finally { _busy = false; }
    }

    private void OnUseCatalogResultClick(object? sender, RoutedEventArgs e)
    {
        var index = _catalogList.SelectedIndex;
        if (index < 0 || index >= _catalogRows.Count) { Report("Select a repository first."); return; }
        _githubUrl.Text = "https://github.com/" + _catalogRows[index].Repository;
        Report("Repository selected. Click 'Check latest release' to inspect ZIP assets.");
    }

    private async void OnCheckReleaseClick(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        try
        {
            _busy = true;
            _releaseInfo.Text = "Checking GitHub for the latest release...";
            _currentRelease = await _github.GetLatestAsync(_githubUrl.Text ?? "");
            _releaseInfo.Text = _currentRelease.Repository + " • " + _currentRelease.Tag +
                "\n" + _currentRelease.Title +
                "\n" + _currentRelease.Description;
            _assetsList.ItemsSource = _currentRelease.ZipAssets
                .Select(a => a.Name + " (" + (a.Size / 1048576.0).ToString("F1") + " MiB)")
                .DefaultIfEmpty("No ZIP release assets available.").ToArray();
            _assetsList.SelectedIndex = _currentRelease.ZipAssets.Count > 0 ? 0 : -1;
            Report("GitHub release metadata loaded. No files have been downloaded.");
        }
        catch (Exception ex)
        {
            _currentRelease = null;
            _assetsList.ItemsSource = Array.Empty<string>();
            Error("Could not check GitHub releases", ex);
            _releaseInfo.Text = "Unable to read latest release.";
        }
        finally { _busy = false; }
    }

    private void OnReleaseAssetSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_selectedAsset is null) return;
        var asset = SelectedAsset;
        _selectedAsset.Text = asset is null ? "Choose a ZIP asset." :
            asset.Name + " — " + (asset.Size / 1048576.0).ToString("F1") + " MiB";
    }

    private async void OnDownloadInstallClick(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var p = SelectedProfile;
        var asset = SelectedAsset;
        if (p is null || asset is null)
        {
            Report("Choose a profile and ZIP release asset before downloading.");
            return;
        }

        string? downloadedFile = null;
        _busy = true;
        _downloadToken = new CancellationTokenSource();
        _cancelButton.IsEnabled = true;
        _downloadButton.IsEnabled = false;
        _progress.Value = 0;
        try
        {
            var progress = new Progress<long>(bytes =>
            {
                _progress.Value = asset.Size > 0 ? Math.Min(100, bytes * 100.0 / asset.Size) : 0;
                _onlineStatus.Text = "Downloaded " + (bytes / 1048576.0).ToString("F1") + " MiB...";
            });
            var downloads = Path.Combine(_profiles.DataRoot, "downloads");
            var result = await _github.DownloadZipAsync(asset, downloads, progress, _downloadToken.Token);
            downloadedFile = result.FilePath;
            _onlineStatus.Text = "Download complete. SHA-256: " + result.Sha256 +
                "\nValidating and installing...";
            _cancelButton.IsEnabled = false; // Install phase is not cancellable.
            var replace = _replace.IsChecked == true;
            var installed = await Task.Run(() =>
                _installer.InstallZip(p.Id, downloadedFile, replace));
            _sources.Record(p.Id, installed.Select(x => x.Name), asset.Repository,
                _currentRelease?.Tag ?? "", asset.Name, result.Sha256);
            RefreshMods();
            _progress.Value = 100;
            _onlineStatus.Text = "Installed: " + string.Join(", ", installed.Select(x => x.DisplayName)) +
                "\nSHA-256: " + result.Sha256;
            Report("Downloaded and installed " + installed.Count + " modlet(s) in " + p.Name + ".");
        }
        catch (OperationCanceledException)
        {
            _onlineStatus.Text = "Download cancelled. No mod changes made.";
            Report("Download cancelled.");
        }
        catch (Exception ex) { Error("Download/install failed", ex); _onlineStatus.Text = ex.Message; }
        finally
        {
            if (downloadedFile is not null && File.Exists(downloadedFile))
            {
                try { File.Delete(downloadedFile); }
                catch (IOException) { /* A temporary download can be deleted manually. */ }
            }
            _downloadToken?.Dispose();
            _downloadToken = null;
            _busy = false;
            _cancelButton.IsEnabled = false;
            _downloadButton.IsEnabled = true;
        }
    }

    private void OnCancelDownloadClick(object? sender, RoutedEventArgs e) =>
        _downloadToken?.Cancel();
}
