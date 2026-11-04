using Avalonia.Controls;
using WorldSeed.SchemaTranslation;

namespace WorldSeed.App;

/// <summary>An owned dialog that resumes one persisted schema-change request after designer input.</summary>
public partial class SchemaChangeClarificationWindow : Window
{
    public string? Answer { get; private set; }

    public SchemaChangeClarificationWindow()
    {
        InitializeComponent();
    }

    public SchemaChangeClarificationWindow(SchemaChangeRequest request) : this()
    {
        RequestTitleText.Text = request.Title;
        QuestionText.Text = request.PendingClarificationQuestion ?? "WorldSeed needs more detail before continuing.";
    }

    private void Continue_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AnswerInput.Text)) return;
        Answer = AnswerInput.Text.Trim();
        Close(true);
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
