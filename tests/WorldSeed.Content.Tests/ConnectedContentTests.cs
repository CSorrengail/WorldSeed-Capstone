using System.Text.Json.Nodes;
using WorldSeed.Content;
using WorldSeed.GameData;
using WorldSeed.LlmIntegration;
using WorldSeed.WorkingRules;

namespace WorldSeed.Content.Tests;

public sealed class ConnectedContentTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-20T12:00:00Z");
    [Fact]
    public async Task Related_canonical_context_and_reviewed_relationships_are_applied_together()
    {
        var (active, data) = Fixture();
        var schema = JsonNode.Parse(active.SchemaJson)!.AsObject();
        var related = ContentReferenceContext.Select(schema, "venue", JsonNode.Parse(data.DataJson)!.AsObject());
        var model = new Model();
        var rule = new WorkingRule("rule", "Customs", WorkingRuleKind.Rule, "Venues observe an established custom.", [], [], [], [], null, [], []);
        var result = await new ContentProposalGenerationService(model).GenerateAsync(new("project", "venue", schema, [rule], "Create a new venue that observes the existing custom.", RelatedEntries: related));
        Assert.Contains("custom-private", string.Join("\n", model.Request!.Messages.Select(message => message.Content)));
        Assert.Equal("custom-private", Assert.Single(result.GenerationContext!.RelatedEntityIds!));
        var proposal = ContentProposalLifecycleService.Create("project", "venue", result.Candidate, active.Id, 1, "rules", 1, Now,
            proposedRelationships: result.ProposedRelationships);
        Assert.True(GameDataService.Validate(active, ContentApplicationService.Preview(proposal, data, Now)).IsValid);
        Assert.DoesNotContain("venue-private", data.DataJson);
        var approval = ContentProposalLifecycleService.Decide(proposal, ContentProposalDecisionKind.Approved, "QA fixture approval.", Now);
        var prepared = ContentApplicationService.Prepare(proposal, approval, active, data, Now);
        Assert.True(ContentApplicationService.CanonicalEntryMatches(prepared.Application, prepared.UpdatedGameData));
        var lostLink = JsonNode.Parse(prepared.UpdatedGameData.DataJson)!.AsObject();
        lostLink["relationshipInstances"] = new JsonArray();
        Assert.False(ContentApplicationService.CanonicalEntryMatches(prepared.Application, prepared.UpdatedGameData with { DataJson = lostLink.ToJsonString() }));
        Assert.NotNull(prepared.Application.RelationshipInstancesJson);
        var nextCandidate = (JsonObject)result.Candidate.DeepClone(); nextCandidate["id"] = "venue-second";
        var nextLink = (JsonObject)result.ProposedRelationships![0].DeepClone(); nextLink["id"] = "link-second"; nextLink["fromEntityInstanceId"] = "venue-second";
        var next = ContentProposalLifecycleService.Create("project", "venue", nextCandidate, active.Id, 1, "rules", 1, Now, proposedRelationships: [nextLink]);
        var nextApproval = ContentProposalLifecycleService.Decide(next, ContentProposalDecisionKind.Approved, "Invalid cardinality fixture.", Now);
        Assert.Throws<InvalidOperationException>(() => ContentApplicationService.Prepare(next, nextApproval, active, prepared.UpdatedGameData, Now));
    }

    [Theory]
    [InlineData("missing-reference", "observes")]
    [InlineData("custom-private", "invented-relationship")]
    public void Unresolved_references_and_undeclared_relationships_never_become_canonical(string target, string definition)
    {
        var (active, data) = Fixture();
        var candidate = JsonNode.Parse(CandidateJson)!.AsObject();
        candidate["values"]!["custom"] = target;
        var link = JsonNode.Parse(LinkJson)!.AsObject(); link["relationshipDefinitionId"] = definition;
        var proposal = ContentProposalLifecycleService.Create("project", "venue", candidate, active.Id, 1, "rules", 1, Now, proposedRelationships: [link]);
        var approval = ContentProposalLifecycleService.Decide(proposal, ContentProposalDecisionKind.Approved, "Invalid fixture.", Now);
        Assert.Throws<InvalidOperationException>(() => ContentApplicationService.Prepare(proposal, approval, active, data, Now));
        Assert.DoesNotContain("venue-private", data.DataJson);
    }

    [Fact]
    public void Generator_cannot_propose_unrelated_links_between_other_canonical_entries()
    {
        var response = "{\"candidate\":" + CandidateJson + ",\"relationshipInstances\":[" + LinkJson.Replace("venue-private", "other-venue") + "]}";
        Assert.Throws<ContentProposalGenerationFormatException>(() => ContentProposalGenerationService.ParseResponse(response, "venue"));
    }

    private const string CandidateJson = """{"id":"venue-private","entityTypeId":"venue","values":{"name":"Gathering Hall","custom":"custom-private"}}""";
    private const string LinkJson = """{"id":"link-private","relationshipDefinitionId":"observes","fromEntityInstanceId":"venue-private","toEntityInstanceId":"custom-private","attributeValues":{}}""";
    private static (ActiveGameSchema, ProjectGameData) Fixture()
    {
        const string schema = """
        {"id":"customs","name":"Customs","version":"1","entityTypes":[
          {"metadata":{"id":"custom","name":"Custom"},"properties":[{"metadata":{"id":"name","name":"Name"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false}]},
          {"metadata":{"id":"venue","name":"Venue"},"properties":[{"metadata":{"id":"name","name":"Name"},"valueType":{"kind":"primitive","name":"string"},"required":true,"nullable":false},
          {"metadata":{"id":"custom","name":"Custom"},"valueType":{"kind":"reference","allowedEntityTypeIds":["custom"]},"required":true,"nullable":false}]}],
         "relationships":[{"metadata":{"id":"observes","name":"Observes"},"from":{"entityTypeId":"venue","minimumPerOppositeInstance":0,"maximumPerOppositeInstance":1},"to":{"entityTypeId":"custom","minimumPerOppositeInstance":1,"maximumPerOppositeInstance":1},"attributes":[]}]}
        """;
        var active = new ActiveGameSchema("active", "project", "0.1", "Customs", schema, "proposal", "rules", 1, 1, Now, Now);
        var data = GameDataService.CreateEmpty(active, Now);
        var root = JsonNode.Parse(data.DataJson)!.AsObject();
        root["entityInstances"]!.AsArray().Add(JsonNode.Parse("""{"id":"custom-private","entityTypeId":"custom","values":{"name":"Welcome Ceremony"}}"""));
        return (active, data with { DataJson = root.ToJsonString() });
    }
    private sealed class Model : ILanguageModelClient
    {
        public LlmChatRequest? Request { get; private set; }
        public Task<LlmChatResponse> CompleteAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new LlmChatResponse("{\"candidate\":" + CandidateJson + ",\"relationshipInstances\":[" + LinkJson + "],\"schemaGaps\":[]}", "test", null));
        }
    }
}
