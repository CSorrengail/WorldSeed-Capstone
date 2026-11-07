using WorldSeed.App;
using WorldSeed.Content;

namespace WorldSeed.Qa.Tests;

public sealed class ContentEvaluationPresentationTests
{
    private static readonly ProjectContentBalanceProfile Profile = new(
        "stored-profile",
        "project",
        "schema",
        1,
        new ContentBalanceProfile("local-band", "creature", ["rule-1"], []),
        1,
        DateTimeOffset.Parse("2026-11-07T00:00:00Z"),
        DateTimeOffset.Parse("2026-11-07T00:00:00Z"));

    [Fact]
    public void Separates_mechanical_failure_from_blocked_balance_measurement()
    {
        var presentation = ContentEvaluationPresenter.Present(
            new ContentEvaluationResult([new MechanicalViolation("entityInstances[0].values.vitality", "Value is below its minimum.")], []),
            Profile);

        Assert.Contains("Failed", presentation.Mechanical);
        Assert.Contains("vitality", presentation.Mechanical);
        Assert.Contains("Not Run", presentation.Balance);
        Assert.Contains("local-band", presentation.Balance);
    }

    [Fact]
    public void Explains_advisory_concerns_without_calling_them_mechanical_failures()
    {
        var concern = new BalanceConcern("vitality-band", "creature-1", "creature.vitality", 28, "Vitality exceeds the local reference band.", [new BalanceReference("sentinel", "Ash Sentinel", 18)]);
        var presentation = ContentEvaluationPresenter.Present(new ContentEvaluationResult([], [concern]), Profile);

        Assert.Contains("Passed", presentation.Mechanical);
        Assert.Contains("Advisory Concerns", presentation.Balance);
        Assert.Contains("Observed 28", presentation.Balance);
        Assert.Contains("Ash Sentinel=18", presentation.Balance);
    }

    [Fact]
    public void Refuses_to_invent_balance_when_no_profile_exists()
    {
        var presentation = ContentEvaluationPresenter.Present(new ContentEvaluationResult([], []), null);

        Assert.Contains("Passed", presentation.Mechanical);
        Assert.Contains("Not Configured", presentation.Balance);
        Assert.Contains("Will Not Invent A Universal Score", presentation.Balance);
    }
}
