using WorldSeed.SourceIngestion;

namespace WorldSeed.SourceIngestion.Tests;

public sealed class DesignIdeaSourceServiceTests
{
    [Fact]
    public void Preserves_exact_idea_text_and_identifies_its_designer_origin()
    {
        const string original = "  Creatures may inherit a habitat move.\r\nThe exact rule is undecided.  ";
        var capturedAt = DateTimeOffset.Parse("2026-11-11T12:00:00Z");

        var note = DesignIdeaSourceService.Create(" idea-1 ", "  Habitat Moves  ", original, capturedAt);

        Assert.Equal("idea-1", note.Id);
        Assert.Equal(original, note.OriginalText);
        Assert.Equal(capturedAt, note.CapturedAt);
        Assert.Equal("Designer Idea: Habitat Moves", note.Origin?.DisplayName);
    }

    [Theory]
    [InlineData("", "An idea")]
    [InlineData("Title", "   ")]
    public void Rejects_incomplete_ideas(string title, string text) =>
        Assert.Throws<ArgumentException>(() => DesignIdeaSourceService.Create("idea-1", title, text, DateTimeOffset.UnixEpoch));
}
