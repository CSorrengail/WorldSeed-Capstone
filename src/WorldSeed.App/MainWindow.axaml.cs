using Avalonia.Controls;
using WorldSeed.LlmIntegration;

namespace WorldSeed.App;

public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient = new();
    private readonly OllamaModelCatalog _ollamaModels;

    public MainWindow()
    {
        _ollamaModels = new OllamaModelCatalog(_httpClient);
        InitializeComponent();
    }

    private async void CheckLocalModels_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LocalModelStatus.Text = "Checking local Ollama…";
        try
        {
            var models = await _ollamaModels.GetInstalledModelsAsync();
            LocalModelStatus.Text = models.Count == 0
                ? "Ollama is available, but it has no installed models yet. WorldSeed will not download one automatically."
                : $"Installed local models: {string.Join(", ", models.Select(model => model.Name))}.";
        }
        catch (LlmClientException)
        {
            LocalModelStatus.Text = "WorldSeed could not reach local Ollama. Start Ollama, then try again.";
        }
        catch (HttpRequestException)
        {
            LocalModelStatus.Text = "WorldSeed could not reach local Ollama. Start Ollama, then try again.";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _httpClient.Dispose();
        base.OnClosed(e);
    }
}
