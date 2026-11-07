using WorldSeed.Content;

namespace WorldSeed.Content.Tests;

public class ContentProposalGenerationServiceTests
{
    [Fact]
    public void Parses_only_the_requested_entity_type()
    {
        var candidate = ContentProposalGenerationService.ParseCandidate("""{"candidate":{"id":"wisp","entityTypeId":"creature","values":{"name":"Wisp"}}}""", "creature");
        Assert.Equal("wisp", candidate["id"]!.GetValue<string>());
    }

    [Fact]
    public void Rejects_wrong_entity_type_and_non_json()
    {
        Assert.Throws<ContentProposalGenerationFormatException>(() => ContentProposalGenerationService.ParseCandidate("not json", "creature"));
        Assert.Throws<ContentProposalGenerationFormatException>(() => ContentProposalGenerationService.ParseCandidate("""{"candidate":{"id":"item","entityTypeId":"item","values":{}}}""", "creature"));
    }
}
