using WorldSeed.SourceIngestion;
namespace WorldSeed.SourceIngestion.Tests;
public class PlainTextSourceImporterTests
{
    [Fact] public async Task Preserves_multiple_text_files_without_absolute_paths()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try { var first = Path.Combine(directory, "combat.txt"); var second = Path.Combine(directory, "travel.txt"); await File.WriteAllTextAsync(first, "Original combat note."); await File.WriteAllTextAsync(second, "Original travel note."); var notes = await new PlainTextSourceImporter().ImportAsync([new("combat", first), new("travel", second)]); Assert.Equal(2, notes.Count); Assert.Equal("Original combat note.", notes[0].OriginalText); var origin = Assert.IsType<WorldSeed.DesignSessions.SourceNoteOrigin>(notes[0].Origin); Assert.Equal("combat.txt", origin.DisplayName); Assert.DoesNotContain(directory, origin.DisplayName, StringComparison.OrdinalIgnoreCase); }
        finally { Directory.Delete(directory, true); }
    }
}
