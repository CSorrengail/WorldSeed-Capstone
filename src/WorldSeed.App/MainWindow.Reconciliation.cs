using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using WorldSeed.LlmIntegration;
using WorldSeed.SchemaTranslation;
using WorldSeed.WorkingRules;

namespace WorldSeed.App;

public partial class MainWindow
{
    private bool _reconcilingRules;
    private string? _reconciliationProjectId;
    private RuleReconciliation? SelectedReconciliation => (ReconciliationHistoryList.SelectedItem as ReconciliationItem)?.Proposal;
    private sealed record ReconciliationItem(RuleReconciliation Proposal)
    {
        public override string ToString() => $"{Proposal.Name} — {Proposal.State} (Rules r{Proposal.BaseRevision})";
    }

    private void RefreshReconciliationView()
    {
        if (_reconciliationProjectId != _workingRuleSet?.ProjectId)
        {
            ReconciliationGuidanceText.Text = "";
            _reconciliationProjectId = _workingRuleSet?.ProjectId;
        }
        var selectedId = SelectedReconciliation?.Id;
        var items = (_workingRuleSet?.Reconciliations ?? []).Reverse().Select(item => new ReconciliationItem(item)).ToArray();
        ReconciliationHistoryList.ItemsSource = items;
        ReconciliationHistoryList.SelectedItem = items.FirstOrDefault(item => item.Proposal.Id == selectedId) ?? items.FirstOrDefault();
        RefreshReconciliationDetail();
    }

    private void ReconciliationHistory_SelectionChanged(object? sender, SelectionChangedEventArgs e) => RefreshReconciliationDetail();

    private void RefreshReconciliationDetail()
    {
        var proposal = SelectedReconciliation;
        ProposeReconciliationButton.IsEnabled = !_reconcilingRules && _workingRuleSet is not null;
        ApplyReconciliationButton.IsEnabled = !_reconcilingRules && proposal?.State == RuleReconciliationState.Pending && proposal.BaseRevision == _workingRuleSet?.Revision;
        RejectReconciliationButton.IsEnabled = !_reconcilingRules && proposal?.State is RuleReconciliationState.Pending or RuleReconciliationState.NeedsClarification;
        PrepareReconciliationSchemaButton.IsEnabled = !_reconcilingRules && proposal?.State == RuleReconciliationState.Applied &&
            proposal.AppliedRevision == _workingRuleSet?.Revision && proposal.OriginalSchemaProposalJson is not null;
        ReconciliationDetailText.Text = proposal is null ? "No Rule Updates Are Proposed." :
            $"{proposal.Name}\n{proposal.State}; Original Rules Revision {proposal.BaseRevision}; Applied Revision {proposal.AppliedRevision?.ToString() ?? "None"}\n\nOriginals\n" +
            string.Join("\n", proposal.Originals.Select(rule => $"{rule.Name}: {rule.AuthoritativeText}")) +
            $"\n\nProposed Rule\n{proposal.Text ?? "Clarification Required"}\n\nReason\n{proposal.Rationale}\n\nQuestion\n{proposal.Question ?? "None"}\n\nResolved Conflicts\n{string.Join("\n", proposal.ResolvedConflicts ?? [])}\n\nResolved Questions\n{string.Join("\n", proposal.ResolvedQuestions ?? [])}\n\nDesigner Guidance\n{proposal.Guidance}";
    }

    private async void ProposeReconciliation_Click(object? sender, RoutedEventArgs e) => await ProposeReconciliationAsync();
    internal async Task<bool> ProposeReconciliationAsync()
    {
        if (_reconcilingRules || _workingRuleSet is null || ModelSelector.SelectedItem is not string model) return false;
        var original = _workingRuleSet;
        var ids = WorkingRuleList.SelectedItems?.OfType<WorkingRuleListItem>().Select(item => item.Rule.Id).ToArray() ?? [];
        if (ids.Length is < 1 or > 8) { WorkingRulesStatusText.Text = "Select One To Eight Rules To Revise Or Reconcile."; return false; }
        var guidance = ReconciliationGuidanceText.Text ?? "";
        if (SelectedReconciliation is { State: RuleReconciliationState.NeedsClarification } pending)
        {
            if (string.IsNullOrWhiteSpace(guidance)) { WorkingRulesStatusText.Text = "Answer The Clarification In Designer Guidance First."; return false; }
            ids = pending.Originals.Select(rule => rule.Id).ToArray();
            guidance = $"Previous Guidance: {pending.Guidance}\nQuestion: {pending.Question}\nDesigner Answer: {guidance}";
        }
        _reconcilingRules = true;
        RefreshReconciliationDetail();
        var capture = new CapturingClient(new OllamaChatClient(_http, OllamaDefaults.CreateProfile("local", "Local", model)));
        try
        {
            var saved = await SchemaProposalStore().LoadAsync(original.ProjectId);
            var proposal = await new RuleReconciliationService(capture).ProposeAsync(original, ids, guidance,
                saved is null ? null : JsonSerializer.Serialize(saved), DateTimeOffset.UtcNow);
            if (_workingRuleSet?.Id != original.Id || _workingRuleSet.Revision != original.Revision || _project?.Id != original.ProjectId)
                throw new InvalidOperationException("The project or Working Rules changed. Generate a fresh proposal.");
            var updated = RuleReconciliationService.AddProposal(_workingRuleSet, proposal, DateTimeOffset.UtcNow);
            await WorkingRuleStore().SaveAsync(updated);
            if (_project?.Id != updated.ProjectId) return true;
            _workingRuleSet = updated;
            RefreshWorkingRulesView();
            ReconciliationHistoryList.SelectedItem = ((IEnumerable<ReconciliationItem>)ReconciliationHistoryList.ItemsSource!).First(item => item.Proposal.Id == proposal.Id);
            WorkingRuleList.SelectedItems?.Clear();
            foreach (var item in (IEnumerable<WorkingRuleListItem>)WorkingRuleList.ItemsSource!)
                if (ids.Contains(item.Rule.Id, StringComparer.Ordinal)) WorkingRuleList.SelectedItems?.Add(item);
            WorkingRulesStatusText.Text = proposal.State == RuleReconciliationState.NeedsClarification
                ? "Clarification Required. Answer In Designer Guidance And Propose Again. No Active Rules Changed."
                : "Rule Update Saved For Review. Accept And Apply Or Reject It; No Active Rules Changed Yet.";
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Propose rule reconciliation", ex);
            WorkingRulesStatusText.Text = ex.Message;
            WorkingRuleText.Text = ex.Message + "\n\n" + Preview(capture.Last?.Content);
            return false;
        }
        finally { _reconcilingRules = false; RefreshReconciliationDetail(); }
    }

    private async void ApplyReconciliation_Click(object? sender, RoutedEventArgs e) => await DecideReconciliationAsync(true);
    private async void RejectReconciliation_Click(object? sender, RoutedEventArgs e) => await DecideReconciliationAsync(false);
    internal async Task<bool> DecideReconciliationAsync(bool apply)
    {
        if (_reconcilingRules || _workingRuleSet is null || SelectedReconciliation is not { } proposal) return false;
        _reconcilingRules = true;
        RefreshReconciliationDetail();
        try
        {
            var current = await WorkingRuleStore().LoadAsync(_workingRuleSet.ProjectId) ?? throw new InvalidOperationException("Reload the Working Rules first.");
            var updated = apply ? RuleReconciliationService.Apply(current, proposal.Id, DateTimeOffset.UtcNow)
                : RuleReconciliationService.Reject(current, proposal.Id, DateTimeOffset.UtcNow);
            await WorkingRuleStore().SaveAsync(updated);
            if (_project?.Id != updated.ProjectId) return true;
            _workingRuleSet = updated;
            _schemaTransitionReview = null;
            RefreshWorkingRulesView();
            RefreshSchemaView();
            WorkingRulesStatusText.Text = apply
                ? "Reviewed Rules Applied. Originals Are Preserved In History. Prepare Schema Review Before Activating Or Publishing The New Context."
                : "Rule Update Rejected. Existing Rules Were Preserved.";
            return true;
        }
        catch (Exception ex) { AppLog.Error("Apply rule reconciliation", ex); WorkingRulesStatusText.Text = ex.Message; return false; }
        finally { _reconcilingRules = false; RefreshReconciliationDetail(); }
    }

    private async void PrepareReconciliationSchema_Click(object? sender, RoutedEventArgs e) => await PrepareReconciliationSchemaAsync();
    internal async Task<bool> PrepareReconciliationSchemaAsync()
    {
        if (_reconcilingRules || _workingRuleSet is null || SelectedReconciliation is not { } selected) return false;
        _reconcilingRules = true;
        RefreshReconciliationDetail();
        try
        {
            var current = await WorkingRuleStore().LoadAsync(_workingRuleSet.ProjectId) ?? throw new InvalidOperationException("Reload Working Rules first.");
            var applied = (current.Reconciliations ?? []).Single(item => item.Id == selected.Id);
            var saved = await SchemaProposalStore().LoadAsync(current.ProjectId) ?? throw new InvalidOperationException("Build A Schema Proposal For The Updated Rules First.");
            var candidate = RuleReconciliationSchemaService.Prepare(current, applied, saved, DateTimeOffset.UtcNow);
            await SchemaProposalStore().SaveAsync(candidate);
            if (_project?.Id != current.ProjectId) return true;
            _schemaProposal = candidate;
            _schemaTransitionReview = null;
            RefreshSchemaView();
            WorkspaceTabs.SelectedIndex = 2;
            SchemaStatusText.Text = "Rule Citations Updated. Review The Schema And Any Mechanical Changes, Then Review And Activate The Proposal. Nothing Is Activated Automatically.";
            return true;
        }
        catch (Exception ex) { AppLog.Error("Prepare reconciliation schema", ex); WorkingRulesStatusText.Text = ex.Message; return false; }
        finally { _reconcilingRules = false; RefreshReconciliationDetail(); }
    }
}
