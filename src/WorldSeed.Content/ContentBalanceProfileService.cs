namespace WorldSeed.Content;

public static class ContentBalanceProfileService
{
    public static ProjectContentBalanceProfile Create(string projectId, string activeSchemaId, int activeSchemaRevision, ContentBalanceProfile profile, DateTimeOffset now, ProjectContentBalanceProfile? replaces = null)
    {
        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(activeSchemaId)) throw new ArgumentException("A project and active schema are required.");
        if (activeSchemaRevision < 1) throw new ArgumentException("The active schema revision must be positive.");
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.EntityTypeId) || profile.SourceWorkingRuleIds.Count == 0 || profile.Signals.Count == 0)
            throw new ArgumentException("A balance profile needs an id, content type, source Working Rules, and at least one signal.", nameof(profile));
        if (profile.Signals.Any(signal => string.IsNullOrWhiteSpace(signal.Id) || string.IsNullOrWhiteSpace(signal.PropertyId) || string.IsNullOrWhiteSpace(signal.Message)))
            throw new ArgumentException("Every balance signal needs an id, property, and explanation.", nameof(profile));
        if (replaces is not null && (!string.Equals(replaces.ProjectId, projectId, StringComparison.Ordinal) || !string.Equals(replaces.Profile.EntityTypeId, profile.EntityTypeId, StringComparison.Ordinal)))
            throw new ArgumentException("A replacement profile must belong to this project and content type.", nameof(replaces));
        return new ProjectContentBalanceProfile("balance-profile-" + Guid.NewGuid().ToString("N"), projectId, activeSchemaId, activeSchemaRevision, profile, (replaces?.Revision ?? 0) + 1, now, now);
    }
}
