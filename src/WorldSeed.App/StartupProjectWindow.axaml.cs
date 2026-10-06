using Avalonia.Controls;
using WorldSeed.DesignSessions;

namespace WorldSeed.App;

public partial class StartupProjectWindow : Window
{
    private readonly IReadOnlyList<DesignProjectSummary> _projects;
    public string? SelectedProjectId { get; private set; }
    public bool CreateNew { get; private set; }

    public StartupProjectWindow() : this([]) { }

    public StartupProjectWindow(IReadOnlyList<DesignProjectSummary> projects)
    {
        _projects = projects;
        InitializeComponent();
        ProjectList.ItemsSource = projects.Select(project => project.Name).ToArray();
        if (projects.Count > 0) ProjectList.SelectedIndex = 0;
        else { HintText.Text = "No saved projects were found in the current project folder. Create a new one to begin."; OpenButton.IsEnabled = false; }
    }

    private void Open_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var index = ProjectList.SelectedIndex;
        if (index < 0 || index >= _projects.Count) return;
        SelectedProjectId = _projects[index].Id;
        Close(true);
    }

    private void Create_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { CreateNew = true; Close(true); }
    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
