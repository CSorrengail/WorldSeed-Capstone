using System.Text.RegularExpressions;

namespace WorldSeed.RuleDrafting;

/// <summary>A literal, stable citation target derived from one saved source note.</summary>
public sealed record SourcePassage(string Id, string SourceNoteId, string Text);

public static class SourcePassageCatalog
{
    public static IReadOnlyList<SourcePassage> Create(IReadOnlyDictionary<string, string> sourceTextById)
    {
        ArgumentNullException.ThrowIfNull(sourceTextById);
        var passages = new List<SourcePassage>();
        foreach (var source in sourceTextById.OrderBy(source => source.Key, StringComparer.Ordinal))
        {
            var parts = Regex.Split(source.Value.Trim(), @"(?<=[.!?])\s+")
                .Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
            if (parts.Length == 0) parts = [source.Value];
            for (var index = 0; index < parts.Length; index++)
                passages.Add(new SourcePassage($"{source.Key}:p{index + 1:D3}", source.Key, parts[index]));
        }
        return passages;
    }
}
