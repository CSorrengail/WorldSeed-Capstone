using System.Text.Json.Nodes;
using WorldSeed.SchemaTranslation;

namespace WorldSeed.SchemaTranslation.Tests;

public class SchemaChangeRequestServiceTests
{
    [Fact]
    public void Creates_and_tracks_a_designer_approved_schema_change()
    {
        var now = DateTimeOffset.Parse("2026-10-28T12:00:00Z");
        var request = SchemaChangeRequestService.Create("project", "Add Weapon Reach", "Weapons may have an optional reach value.", SchemaChangeRequestOrigin.Designer, now);

        Assert.Equal(SchemaChangeRequestStatus.Pending, request.Status);
        Assert.StartsWith("schema-change-", request.Id, StringComparison.Ordinal);
        Assert.Equal("Weapons may have an optional reach value.", request.ProposedRule);

        var applied = SchemaChangeRequestService.MarkApplied(SchemaChangeRequestService.MarkApplying(request, now.AddMinutes(1)), now.AddMinutes(2));
        Assert.Equal(SchemaChangeRequestStatus.Applied, applied.Status);
        Assert.Equal(now.AddMinutes(2), applied.AppliedAt);
        Assert.Null(applied.FailureDetail);
    }

    [Fact]
    public void Allows_an_approved_change_to_replace_only_an_existing_definition()
    {
        var baseSchema = JsonNode.Parse("""{"id":"game","name":"Game","version":"0.5","entityTypes":[{"metadata":{"id":"weapon","name":"Weapon"},"properties":[]}]}""")!.AsObject();
        var changed = JsonNode.Parse("""{"id":"game","name":"Game","version":"0.5","entityTypes":[{"metadata":{"id":"weapon","name":"Weapon"},"properties":[{"metadata":{"id":"reach","name":"Reach"},"valueType":{"kind":"primitive","name":"integer"},"required":false,"nullable":true}]}]}""")!.AsObject();

        var exception = Assert.Throws<SchemaTranslationFormatException>(() => SchemaProposalMerger.MergeSchemas(baseSchema, changed));
        Assert.Contains("conflicts", exception.Message, StringComparison.OrdinalIgnoreCase);

        var merged = SchemaProposalMerger.MergeSchemas(baseSchema, changed, allowExistingDefinitionUpdates: true);
        var weapon = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(merged["entityTypes"])));
        var property = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(weapon["properties"])));
        Assert.Equal("reach", Assert.IsType<JsonObject>(property["metadata"])["id"]!.GetValue<string>());
    }

    [Fact]
    public void Rejects_a_schema_change_that_omits_existing_members()
    {
        var baseSchema = JsonNode.Parse("""{"id":"game","name":"Game","version":"0.5","entityTypes":[{"metadata":{"id":"weapon","name":"Weapon"},"properties":[{"metadata":{"id":"name","name":"Name"}},{"metadata":{"id":"weight","name":"Weight"}}]}]}""")!.AsObject();
        var incomplete = JsonNode.Parse("""{"id":"game","name":"Game","version":"0.5","entityTypes":[{"metadata":{"id":"weapon","name":"Weapon"},"properties":[{"metadata":{"id":"reach","name":"Reach"}}]}]}""")!.AsObject();

        var exception = Assert.Throws<SchemaTranslationFormatException>(() => SchemaProposalMerger.MergeSchemas(baseSchema, incomplete, allowExistingDefinitionUpdates: true));

        Assert.Contains("omits existing properties", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
