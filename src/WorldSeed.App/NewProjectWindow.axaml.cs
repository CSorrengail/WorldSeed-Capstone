using Avalonia.Controls;

namespace WorldSeed.App;

public partial class NewProjectWindow : Window
{
    public string? ProjectName { get; private set; }
    public string? Description { get; private set; }

    public NewProjectWindow() => InitializeComponent();

    private void Create_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameInput.Text)) return;
        ProjectName = NameInput.Text.Trim();
        Description = DescriptionInput.Text;
        Close(true);
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
