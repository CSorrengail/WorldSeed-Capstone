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

        var balance = evaluation is null
            ? "Unavailable: Re-Evaluate This Proposal With The Current Active Schema Before Running Mechanical Or Balance Evaluation."
            : profile is null
            ? "Not Configured: This Content Type And Schema Revision Have No Game-Specific Balance Profile. Mechanical Validation Still Ran, But WorldSeed Will Not Invent A Universal Score. Open Game-Specific Balance Profile Under Proposal Setup To Add This Game's Reference Values."
            : !evaluation.IsMechanicallyValid
                    ? $"Not Run: Fix Mechanical Violations Before Measuring Profile {profile.Profile.Id}."
                    : evaluation.BalanceConcerns.Count == 0
                        ? $"Passed: Profile {profile.Profile.Id} Found No Advisory Balance Concerns."
                        : $"Advisory Concerns From {profile.Profile.Id}:\n" + string.Join("\n", evaluation.BalanceConcerns.Select(concern => $"- {concern.Message} Observed {concern.ObservedValue}; References: {string.Join(", ", concern.References.Select(reference => reference.Label + "=" + reference.Value))}."));

        return new ContentEvaluationPresentation(mechanical, balance);
    }
}
