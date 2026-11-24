using WorldSeed.WorkingRules;

namespace WorldSeed.App;

public partial class MainWindow
{
    private async Task RefreshWorkingRulePreparationAsync(string projectId)
    {
        if (_project?.Id != projectId) return;
        try
        {
            var checkpoint = await _workingRulePreparations.LoadAsync(projectId);
            if (_project?.Id != projectId) return;
            _workingRulePreparation = checkpoint;
            WorkingRulesCheckpointPanel.IsVisible = checkpoint is not null;
            if (checkpoint is null) return;
            var count = checkpoint.Batches.SelectMany(batch => batch.RuleSet.Rules).SelectMany(rule => rule.SourceDraftRules).Distinct().Count();
            try
            {
                WorkingRulePreparationBinding.Validate(checkpoint, _project, _workingRuleSet, checkpoint.AddMissingOnly);
                var operation = checkpoint.AddMissingOnly ? "Add New Source Rules" : "Rebuild Project Rules";
                WorkingRulesCheckpointText.Text = $"Saved Checkpoint ({operation}): {count} Source Rules Prepared. Resume The Remaining Work Or Discard This Checkpoint. Project Rules Have Not Been Replaced.";
                ResumeWorkingRulesButton.IsEnabled = !_isPreparingWorkingRules;
            }
            catch (InvalidOperationException)
            {
                WorkingRulesCheckpointText.Text = "This Checkpoint Is Outdated Because Sources Or Project Rules Changed. Discard It Before Starting Again.";
                ResumeWorkingRulesButton.IsEnabled = false;
            }
            DiscardWorkingRulesCheckpointButton.IsEnabled = !_isPreparingWorkingRules;
        }
        catch (Exception ex)
        {
            AppLog.Error("Load rules checkpoint", ex);
            WorkingRulesCheckpointPanel.IsVisible = true;
            WorkingRulesCheckpointText.Text = "Could Not Load The Saved Checkpoint. Check The Local Service And Reload The Project.";
            ResumeWorkingRulesButton.IsEnabled = false;
        }
    }

    private async void ResumeWorkingRules_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_isPreparingWorkingRules && _workingRulePreparation?.ProjectId == _project?.Id && _workingRulePreparation is { } checkpoint)
            await BuildWorkingRulesAsync(checkpoint.AddMissingOnly);
    }

    private async void DiscardWorkingRulesCheckpoint_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await DiscardWorkingRuleCheckpointAsync();

    internal async Task DiscardWorkingRuleCheckpointAsync()
    {
        if (_isPreparingWorkingRules || _project is null) return;
        var id = _project.Id;
        _isPreparingWorkingRules = true;
        BuildWorkingRulesButton.IsEnabled = false; AddMissingWorkingRulesButton.IsEnabled = false;
        ResumeWorkingRulesButton.IsEnabled = false; DiscardWorkingRulesCheckpointButton.IsEnabled = false;
        try
        {
            await _workingRulePreparations.DeleteAsync(id);
            WorkingRulesStatusText.Text = "Checkpoint Discarded. Saved Project Rules And Sources Were Not Changed.";
        }
        catch (Exception ex) { AppLog.Error("Discard rules checkpoint", ex); WorkingRulesStatusText.Text = "Could Not Discard The Checkpoint. Try Again."; }
        finally
        {
            _isPreparingWorkingRules = false; RegisterInteraction();
            BuildWorkingRulesButton.IsEnabled = true; AddMissingWorkingRulesButton.IsEnabled = true;
            await RefreshWorkingRulePreparationAsync(id);
        }
    }
}
