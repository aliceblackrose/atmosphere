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
    private readonly GameLocator _gameLocator = new();
    private readonly LauncherSettingsStore _settingsStore = new();

    private readonly TextBox _gamePathTextBox;
    private readonly TextBlock _gameStatusText;
    private readonly TextBlock _gameDetailsText;
    private readonly Button _launchButton;
    private readonly Button _openFolderButton;

    private LauncherSettings _settings;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        _gamePathTextBox = this.FindControl<TextBox>("GamePathTextBox")!;
        _gameStatusText = this.FindControl<TextBlock>("GameStatusText")!;
        _gameDetailsText = this.FindControl<TextBlock>("GameDetailsText")!;
        _launchButton = this.FindControl<Button>("LaunchButton")!;
        _openFolderButton = this.FindControl<Button>("OpenFolderButton")!;

        _settings = _settingsStore.Load();
        _gamePathTextBox.Text = GameLocator.IsValidGameDirectory(_settings.GameDirectory)
            ? _settings.GameDirectory
            : _gameLocator.TryFindGameDirectory();
        RefreshStatus();
    }

    private void OnGamePathChanged(object? sender, TextChangedEventArgs e) => RefreshStatus();

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose the 7 Days to Die installation directory",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                var directory = folders[0].TryGetLocalPath();
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    _gamePathTextBox.Text = directory;
                }
            }
        }
        catch (Exception ex)
        {
            DisplayError("Could not open folder picker", ex);
        }
    }

    private void OnDetectClick(object? sender, RoutedEventArgs e)
    {
        var found = _gameLocator.TryFindGameDirectory();
        if (found is null)
        {
            _gameStatusText.Text = "No Steam installation found automatically.";
            _gameDetailsText.Text = "Use Browse or enter your game's installation folder.";
            return;
        }

        _gamePathTextBox.Text = found;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var directory = _gamePathTextBox.Text?.Trim();
        if (!GameLocator.IsValidGameDirectory(directory))
        {
            RefreshStatus();
            return;
        }

        try
        {
            _settings = new LauncherSettings { GameDirectory = directory };
            _settingsStore.Save(_settings);
            _gameStatusText.Text = "Game path saved.";
            _gameDetailsText.Text = "Atmosphere will remember this location for future sessions.";
        }
        catch (Exception ex)
        {
            DisplayError("Could not save preferences", ex);
        }
    }

    private void OnLaunchClick(object? sender, RoutedEventArgs e)
    {
        if (!GameLocator.IsValidGameDirectory(_gamePathTextBox.Text))
        {
            RefreshStatus();
            return;
        }

        try
        {
            SteamLauncher.LaunchVanilla();
            _gameStatusText.Text = "Sent launch request to Steam.";
            _gameDetailsText.Text = "Steam will start your installed vanilla game using its normal settings.";
        }
        catch (Exception ex)
        {
            DisplayError("Could not open Steam", ex);
        }
    }

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        var directory = _gamePathTextBox.Text?.Trim();
        if (!GameLocator.IsValidGameDirectory(directory))
        {
            RefreshStatus();
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = directory!,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DisplayError("Could not open the game folder", ex);
        }
    }

    private void RefreshStatus()
    {
        // During XAML loading a TextChanged event can fire before controls are assigned.
        if (_gameStatusText is null || _gameDetailsText is null ||
            _launchButton is null || _openFolderButton is null)
        {
            return;
        }

        var directory = _gamePathTextBox?.Text?.Trim();
        var valid = GameLocator.IsValidGameDirectory(directory);

        _launchButton.IsEnabled = valid;
        _openFolderButton.IsEnabled = valid;

        _gameStatusText.Text = valid ? "Game installation detected." : "Game installation not selected.";
        _gameDetailsText.Text = valid
            ? directory
            : "Choose a folder containing 7DaysToDie.exe, 7DaysToDie.x86_64, or the macOS app bundle.";
    }

    private void DisplayError(string message, Exception ex)
    {
        _gameStatusText.Text = message;
        _gameDetailsText.Text = ex.Message;
    }
}
