using System.Text.Json.Nodes;
using WorldSeed.SchemaValidation;

namespace WorldSeed.SchemaValidation.Tests;

public class ChangeLedgerValidatorTests
{
    [Fact]
    public void Validates_the_change_ledger_example()
    {
        var ledger = ReadJson("schemas", "examples", "change-ledger.v0.1.example.json");
        var result = new ChangeLedgerValidator().Validate(ledger);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
    }

    [Fact]
    public void Rejects_broken_provenance_and_applied_changes_without_human_decisions()
    {
        var ledger = ReadJson("schemas", "examples", "change-ledger.v0.1.example.json")!.AsObject();
        ledger["interpretationProposals"]!.AsArray()[0]! ["sourceMaterialIds"] = new JsonArray("missing-source");
        ledger["interpretationProposals"]!.AsArray()[0]!.AsObject().Remove("proposedText");
        ledger["decisions"] = new JsonArray();
        var result = new ChangeLedgerValidator().Validate(ledger);
        Assert.Contains(result.Issues, issue => issue.Message.Contains("Unknown sourceMaterialIds reference 'missing-source'"));
        Assert.Contains(result.Issues, issue => issue.Message.Contains("require human-readable proposed text"));
        Assert.Contains(result.Issues, issue => issue.Message.Contains("requires a human apply decision"));
    }

    [Fact]
    public void Rejects_a_revision_chain_that_changes_artifacts()
    {
        var ledger = JsonNode.Parse("""
        {"id":"ledger","projectId":"project","sourceMaterials":[],"interpretationProposals":[],"revisions":[{"id":"one","artifactKind":"gameSchema","artifactId":"first","createdAt":"2026-09-16T10:00:00Z","createdBy":{"kind":"human","id":"person"},"snapshot":{}},{"id":"two","artifactKind":"gameSchema","artifactId":"second","previousRevisionId":"one","createdAt":"2026-09-16T10:01:00Z","createdBy":{"kind":"human","id":"person"},"snapshot":{}}],"changeSets":[],"decisions":[]}
        """);
        var result = new ChangeLedgerValidator().Validate(ledger);
        Assert.Contains(result.Issues, issue => issue.Message.Contains("previous revision must belong to the same artifact"));
    }

    private static JsonNode? ReadJson(params string[] parts) => JsonNode.Parse(File.ReadAllText(FindFile(parts)));

    private static string FindFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Repository fixture was not found.");
    }
}
