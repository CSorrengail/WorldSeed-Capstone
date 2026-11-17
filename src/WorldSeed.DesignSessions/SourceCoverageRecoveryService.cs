using System.Security.Cryptography;
using System.Text;
using WorldSeed.RuleDrafting;

namespace WorldSeed.DesignSessions;

public static class SourceCoverageRecoveryService
{
    public static DesignSourceNote CreateSource(DesignProject project, string sourceId, string passageId, DateTimeOffset now)
    {
        var source = project.SourceNotes.SingleOrDefault(note => note.Id == sourceId)
            ?? throw new ArgumentException("The original source is no longer available.");
        if (source.Origin?.ParentPassageId is not null)
            throw new ArgumentException("Open the original source passage to recover this material.");
        var passage = SourcePassageCatalog.Create(new Dictionary<string, string> { [source.Id] = source.OriginalText })
            .SingleOrDefault(item => item.Id == passageId)
            ?? throw new ArgumentException("The original source passage is no longer available. Rebuild the inventory.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passage.Id + "\n" + passage.Text))).ToLowerInvariant();
        return new DesignSourceNote("recovery-" + fingerprint, passage.Text, now,
            new SourceNoteOrigin("Coverage Review — " + (source.Origin?.DisplayName ?? source.Id),
                ParentSourceNoteId: source.Id, ParentPassageId: passage.Id));
    }
}
