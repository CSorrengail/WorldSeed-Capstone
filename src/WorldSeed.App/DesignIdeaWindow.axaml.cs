using Avalonia.Controls;

namespace WorldSeed.App;

public partial class DesignIdeaWindow : Window
{
    public string? IdeaTitle { get; private set; }
    public string? IdeaText { get; private set; }

    public DesignIdeaWindow() => InitializeComponent();

    internal bool TryAccept()
    {
        var title = TitleInput.Text?.Trim();
        var text = IdeaInput.Text;
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(text))
        {
            ValidationText.Text = "Enter Both A Short Title And The Design Idea.";
            return false;
        }
        IdeaTitle = title;
        IdeaText = text;
        return true;
    }

    private void Preserve_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TryAccept()) Close(true);
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
