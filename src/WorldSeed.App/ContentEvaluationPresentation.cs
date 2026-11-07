using WorldSeed.Content;

namespace WorldSeed.App;

internal sealed record ContentEvaluationPresentation(string Mechanical, string Balance);

internal static class ContentEvaluationPresenter
{
    public static ContentEvaluationPresentation Present(ContentEvaluationResult? evaluation, ProjectContentBalanceProfile? profile)
    {
        var mechanical = evaluation is null
            ? "Unavailable: This Proposal Uses A Historical Or Unavailable Active Schema Revision."
            : evaluation.IsMechanicallyValid
                ? "Passed: No Mechanical Violations Were Found In The Active Schema."
                : "Failed:\n" + string.Join("\n", evaluation.MechanicalViolations.Select(issue => $"- {issue.Path}: {issue.Message}"));

        var balance = profile is null
            ? "Not Configured: This Content Type And Schema Revision Have No Game-Specific Balance Profile. WorldSeed Will Not Invent A Universal Score."
            : evaluation is null
                ? $"Unavailable: Profile {profile.Profile.Id} Cannot Be Applied Without Its Matching Active Schema Revision."
                : !evaluation.IsMechanicallyValid
                    ? $"Not Run: Fix Mechanical Violations Before Measuring Profile {profile.Profile.Id}."
                    : evaluation.BalanceConcerns.Count == 0
                        ? $"Passed: Profile {profile.Profile.Id} Found No Advisory Balance Concerns."
                        : $"Advisory Concerns From {profile.Profile.Id}:\n" + string.Join("\n", evaluation.BalanceConcerns.Select(concern => $"- {concern.Message} Observed {concern.ObservedValue}; References: {string.Join(", ", concern.References.Select(reference => reference.Label + "=" + reference.Value))}."));

        return new ContentEvaluationPresentation(mechanical, balance);
    }
}
