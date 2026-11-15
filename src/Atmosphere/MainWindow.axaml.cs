using System.Diagnostics;
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
    private readonly GitHubReleaseService _github = new();
    private readonly ProfileModInstaller _installer;
    private readonly ProfileGameLauncher _profileLauncher;
    private readonly LauncherSettings _settings;

    private readonly TextBox _gamePath, _newProfileName, _renameProfile, _versionNote, _githubUrl;
    private readonly TextBlock _gameStatus, _gameDetails, _globalStatus, _selectedProfileText;
    private readonly TextBlock _modProfileLabel, _modDetails, _releaseInfo, _selectedAsset, _onlineStatus;
    private readonly Button _vanillaButton, _openFolderButton, _downloadButton, _cancelButton;
    private readonly ListBox _profilesList, _modsList, _gameModsList, _assetsList;
    private readonly CheckBox _noEac, _replace;
    private readonly ProgressBar _progress;
    private List<ModProfile> _profileRows = new();
    private List<(ModRecord Mod, bool Disabled)> _modRows = new();
    private GitHubRelease? _currentRelease;
    private CancellationTokenSource? _downloadToken;
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
        _newProfileName = Find<TextBox>("NewProfileNameTextBox");
        _renameProfile = Find<TextBox>("RenameProfileTextBox");
        _versionNote = Find<TextBox>("VersionNoteTextBox");
        _selectedProfileText = Find<TextBlock>("SelectedProfileText");
        _noEac = Find<CheckBox>("NoEacCheckBox");
        _modsList = Find<ListBox>("ModsList");
        _gameModsList = Find<ListBox>("GameModList");
        _modProfileLabel = Find<TextBlock>("ModProfileLabel");
        _modDetails = Find<TextBlock>("ModDetailsText");
        _replace = Find<CheckBox>("ReplaceExistingCheckBox");
        _githubUrl = Find<TextBox>("GitHubUrlTextBox");
        _releaseInfo = Find<TextBlock>("ReleaseInfoText");
        _assetsList = Find<ListBox>("ReleaseAssetsList");
        _selectedAsset = Find<TextBlock>("SelectedAssetText");
        _onlineStatus = Find<TextBlock>("OnlineStatusText");
        _progress = Find<ProgressBar>("DownloadProgressBar");
        _downloadButton = Find<Button>("DownloadInstallButton");
        _cancelButton = Find<Button>("CancelDownloadButton");

        _installer = new ProfileModInstaller(_profiles);
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
        RefreshMods();
    }

    private void OnCreateProfileClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var p = _profiles.Create(_newProfileName.Text ?? "");
            _newProfileName.Text = "";
            RefreshProfiles(p.Id);
            Report("Created isolated profile " + p.Name + ".");
        }
        catch (Exception ex) { Error("Could not create profile", ex); }
    }

    private void OnRenameProfileClick(object? sender, RoutedEventArgs e)
    {
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
                Title = "Choose modlet ZIP archive",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("ZIP archive") { Patterns = new[] { "*.zip" } } }
            });
            if (files.Count == 0) return;
            var path = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Please choose a local ZIP file.");
            var replace = _replace.IsChecked == true;
            _busy = true;
            Report("Validating and installing mod ZIP...");
            var installed = await Task.Run(() =>
                _installer.InstallZip(p.Id, path, replace));
            RefreshMods();
            Report("Installed " + string.Join(", ", installed.Select(m => m.DisplayName)) + " into " + p.Name + ".");
        }
        catch (Exception ex) { Error("ZIP installation failed; existing files were preserved where possible", ex); }
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
            RefreshMods();
            Report("Archived mod to " + path + ". Nothing was permanently deleted.");
        }
        catch (Exception ex) { Error("Could not archive mod", ex); }
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
