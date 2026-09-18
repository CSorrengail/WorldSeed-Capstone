using Avalonia.Controls;
using Avalonia.Platform.Storage;
using WorldSeed.DesignSessions;
using WorldSeed.LlmIntegration;
using WorldSeed.RuleDrafting;
using WorldSeed.SourceIngestion;
namespace WorldSeed.App;
public partial class MainWindow : Window {
 readonly HttpClient http=new(); readonly List<DesignSourceNote> notes=[];
 public MainWindow(){InitializeComponent();}
 async void CheckLocalModels_Click(object? s,Avalonia.Interactivity.RoutedEventArgs e){try{var m=await new OllamaModelCatalog(http).GetInstalledModelsAsync();ModelSelector.ItemsSource=m.Select(x=>x.Name).ToArray();StatusText.Text="Select an installed model.";}catch{StatusText.Text="Could not reach Ollama.";}}
 async void AddNotes_Click(object? s,Avalonia.Interactivity.RoutedEventArgs e){var f=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{AllowMultiple=true,FileTypeFilter=[new FilePickerFileType("Plain text"){Patterns=["*.txt"]}]});var r=f.Select((x,i)=>new PlainTextSourceRequest($"source-{notes.Count+i+1:000}",x.TryGetLocalPath()??"")).Where(x=>x.FilePath.Length>0).ToArray();if(r.Length==0)return;try{notes.AddRange(await new PlainTextSourceImporter().ImportAsync(r));SourceNotesList.ItemsSource=notes.Select(x=>x.Origin!.DisplayName).ToArray();StatusText.Text=$"{notes.Count} notes preserved.";}catch(Exception ex){StatusText.Text=ex.Message;}}
 async void CreateSession_Click(object? s,Avalonia.Interactivity.RoutedEventArgs e){if(notes.Count==0||ModelSelector.SelectedItem is not string m){StatusText.Text="Add notes and select a model.";return;}try{var svc=new DesignSessionService(new RuleDraftingService(new OllamaChatClient(http,OllamaDefaults.CreateProfile("local","Local",m))));var session=svc.Start($"session-{Guid.NewGuid():N}","local-project","local",notes[0]);foreach(var n in notes.Skip(1))session=svc.AddDesignerMessage(session,n.OriginalText,n);var result=await svc.AdvanceAsync(session);StatusText.Text="Draft complete.";DraftText.Text=result.LatestTurn!.Action==RuleDraftAction.AskClarifyingQuestion?result.LatestTurn.ClarifyingQuestion:string.Join("\n\n",result.LatestTurn.Draft!.Rules.Select(r=>$"{r.Name}: {r.Text}\nEvidence: {string.Join(" | ",r.SourceSupport.Select(x=>x.Excerpt))}"));}catch(Exception ex){StatusText.Text="Drafting failed: "+ex.Message;}}
 protected override void OnClosed(EventArgs e){http.Dispose();base.OnClosed(e);}
}
