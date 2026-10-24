using Avalonia.Controls;
using System.Globalization;

namespace WorldSeed.App;

public partial class SettingsWindow : Window
{
    public int IdleTimeoutMinutes { get; private set; }

    public SettingsWindow() : this(WorldSeedSettings.DefaultIdleTimeoutMinutes) { }

    public SettingsWindow(int idleTimeoutMinutes)
    {
        IdleTimeoutMinutes = idleTimeoutMinutes;
        InitializeComponent();
        IdleTimeoutInput.Text = idleTimeoutMinutes.ToString(CultureInfo.InvariantCulture);
    }

    private void Save_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!int.TryParse(IdleTimeoutInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes < 0 || minutes > WorldSeedSettings.MaximumIdleTimeoutMinutes)
        {
            ValidationText.Text = $"Enter a whole number from 0 to {WorldSeedSettings.MaximumIdleTimeoutMinutes}.";
            return;
        }
        IdleTimeoutMinutes = minutes;
        Close(true);
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
