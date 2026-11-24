using MongoDB.Driver;
using System.Text.Json.Nodes;
using WorldSeed.Content;
using WorldSeed.DesignSessions;
using WorldSeed.GameData;
using WorldSeed.Persistence.Mongo;
using WorldSeed.Publication;
using WorldSeed.ProjectPlanning;
using WorldSeed.SchemaTranslation;
using WorldSeed.WorkingRules;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("WorldSeed")
    ?? throw new InvalidOperationException("ConnectionStrings:WorldSeed must be configured.");
var databaseName = builder.Configuration["WorldSeed:DatabaseName"] ?? "worldseed";

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
builder.Services.AddSingleton<IMongoDatabase>(services => services.GetRequiredService<IMongoClient>().GetDatabase(databaseName));
builder.Services.AddSingleton<MongoDesignProjectStore>();
builder.Services.AddSingleton<IDesignProjectStore>(services => services.GetRequiredService<MongoDesignProjectStore>());
builder.Services.AddSingleton<MongoWorkingRuleSetStore>();
builder.Services.AddSingleton<IWorkingRuleSetStore>(services => services.GetRequiredService<MongoWorkingRuleSetStore>());
builder.Services.AddSingleton<IWorkingRulePreparationStore, MongoWorkingRulePreparationStore>();
builder.Services.AddSingleton<MongoProjectSchemaProposalStore>();
builder.Services.AddSingleton<IProjectSchemaProposalStore>(services => services.GetRequiredService<MongoProjectSchemaProposalStore>());
builder.Services.AddSingleton<MongoProjectSchemaChangeRequestStore>();
builder.Services.AddSingleton<IProjectSchemaChangeRequestStore>(services => services.GetRequiredService<MongoProjectSchemaChangeRequestStore>());
builder.Services.AddSingleton<MongoActiveGameSchemaStore>();
builder.Services.AddSingleton<IActiveGameSchemaStore>(services => services.GetRequiredService<MongoActiveGameSchemaStore>());
builder.Services.AddSingleton<MongoProjectGameDataStore>();
builder.Services.AddSingleton<IProjectGameDataStore>(services => services.GetRequiredService<MongoProjectGameDataStore>());
builder.Services.AddSingleton<MongoSchemaTransitionStore>();
builder.Services.AddSingleton<ISchemaTransitionStore>(services => services.GetRequiredService<MongoSchemaTransitionStore>());
builder.Services.AddSingleton<MongoContentProposalStore>();
builder.Services.AddSingleton<IContentProposalStore>(services => services.GetRequiredService<MongoContentProposalStore>());
builder.Services.AddSingleton<MongoContentApplicationStore>();
builder.Services.AddSingleton<IContentApplicationStore>(services => services.GetRequiredService<MongoContentApplicationStore>());
builder.Services.AddSingleton<MongoContentBalanceProfileStore>();
builder.Services.AddSingleton<IContentBalanceProfileStore>(services => services.GetRequiredService<MongoContentBalanceProfileStore>());
builder.Services.AddSingleton<MongoPublicationSnapshotStore>();
builder.Services.AddSingleton<IPublicationSnapshotStore>(services => services.GetRequiredService<MongoPublicationSnapshotStore>());
builder.Services.AddSingleton<MongoPublicationEditionStore>();
builder.Services.AddSingleton<IPublicationEditionStore>(services => services.GetRequiredService<MongoPublicationEditionStore>());
builder.Services.AddSingleton<MongoPublicationEditorialStore>();
builder.Services.AddSingleton<IPublicationEditorialStore>(services => services.GetRequiredService<MongoPublicationEditorialStore>());
builder.Services.AddSingleton<MongoProjectInventoryStore>();
builder.Services.AddSingleton<IProjectInventoryStore>(services => services.GetRequiredService<MongoProjectInventoryStore>());
builder.Services.AddSingleton<MongoDevelopmentPlanStore>();
builder.Services.AddSingleton<IDevelopmentPlanStore>(services => services.GetRequiredService<MongoDevelopmentPlanStore>());
builder.Services.AddSingleton<MongoExpansionRequestStore>();
builder.Services.AddSingleton<IExpansionRequestStore>(services => services.GetRequiredService<MongoExpansionRequestStore>());
builder.Services.AddSingleton<MongoContentExpansionRunStore>();
builder.Services.AddSingleton<IContentExpansionRunStore>(services => services.GetRequiredService<MongoContentExpansionRunStore>());
builder.Services.AddSingleton<MongoEvaluationAssistanceStore>();
builder.Services.AddSingleton<IEvaluationAssistanceStore>(services => services.GetRequiredService<MongoEvaluationAssistanceStore>());
builder.Services.AddSingleton<WorkingRuleSetValidator>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<MongoWorkingRuleSetStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoProjectSchemaProposalStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoProjectSchemaChangeRequestStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoActiveGameSchemaStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoProjectGameDataStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoSchemaTransitionStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoContentProposalStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoContentApplicationStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoContentBalanceProfileStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoPublicationSnapshotStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoPublicationEditionStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoPublicationEditorialStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoProjectInventoryStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoDevelopmentPlanStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoExpansionRequestStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoContentExpansionRunStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoEvaluationAssistanceStore>().EnsureIndexesAsync();
}

app.MapGet("/health", async (MongoDesignProjectStore store, CancellationToken cancellationToken) =>
{
    await store.PingAsync(cancellationToken);
    return Results.Ok(new { status = "healthy" });
});

var projects = app.MapGroup("/api/projects");
projects.MapGet("/", async (IDesignProjectStore store, CancellationToken cancellationToken) => Results.Ok(await store.ListAsync(cancellationToken)));
projects.MapGet("/{projectId}", async (string projectId, IDesignProjectStore store, CancellationToken cancellationToken) =>
{
    var project = await store.LoadAsync(projectId, cancellationToken);
    return project is null ? Results.NotFound() : Results.Ok(project);
});
projects.MapPut("/{projectId}", async (string projectId, DesignProject project, IDesignProjectStore store, CancellationToken cancellationToken) =>
{
    if (!string.Equals(projectId, project.Id, StringComparison.Ordinal)) return Results.BadRequest("The URL project id must match the body.");
    await store.SaveAsync(project, cancellationToken);
    return Results.NoContent();
});
projects.MapDelete("/{projectId}", async (string projectId, IDesignProjectStore store, IWorkingRuleSetStore ruleSets, IProjectSchemaProposalStore schemaProposals, IProjectSchemaChangeRequestStore schemaChangeRequests, IActiveGameSchemaStore activeSchemas, IProjectGameDataStore gameData, ISchemaTransitionStore schemaTransitions, IContentProposalStore contentProposals, IContentApplicationStore contentApplications, IContentBalanceProfileStore balanceProfiles, IPublicationSnapshotStore publications, IPublicationEditionStore publicationEditions, IPublicationEditorialStore publicationEditorial, IProjectInventoryStore inventories, IDevelopmentPlanStore developmentPlans, IExpansionRequestStore expansionRequests, IContentExpansionRunStore contentExpansionRuns, IEvaluationAssistanceStore evaluationAssistance, IWorkingRulePreparationStore preparations, CancellationToken cancellationToken) =>
{
    await preparations.DeleteAsync(projectId, cancellationToken);
    await evaluationAssistance.DeleteAsync(projectId, cancellationToken);
    await contentExpansionRuns.DeleteAsync(projectId, cancellationToken);
    await expansionRequests.DeleteAsync(projectId, cancellationToken);
    await developmentPlans.DeleteAsync(projectId, cancellationToken);
    await inventories.DeleteAsync(projectId, cancellationToken);
    await publicationEditorial.DeleteAsync(projectId, cancellationToken);
    await publicationEditions.DeleteAsync(projectId, cancellationToken);
    await publications.DeleteAsync(projectId, cancellationToken);
    await contentApplications.DeleteAsync(projectId, cancellationToken);
    await contentProposals.DeleteAsync(projectId, cancellationToken);
    await balanceProfiles.DeleteAsync(projectId, cancellationToken);
    await schemaTransitions.DeleteAsync(projectId, cancellationToken);
    await gameData.DeleteAsync(projectId, cancellationToken);
    await activeSchemas.DeleteAsync(projectId, cancellationToken);
    await schemaProposals.DeleteAsync(projectId, cancellationToken);
    await schemaChangeRequests.DeleteAsync(projectId, cancellationToken);
    await ruleSets.DeleteAsync(projectId, cancellationToken);
    await store.DeleteAsync(projectId, cancellationToken);
    return Results.NoContent();
});

projects.MapGet("/{projectId}/development-plans", async (string projectId, IDesignProjectStore projectsStore, IWorkingRuleSetStore ruleSets, IProjectSchemaProposalStore schemaProposals, IActiveGameSchemaStore activeSchemas, IDevelopmentPlanStore store, IExpansionRequestStore expansionRequests, CancellationToken cancellationToken) =>
{
    var project = await projectsStore.LoadAsync(projectId, cancellationToken);
    if (project is null) return Results.NotFound("The project does not exist.");
    var requests = await expansionRequests.ListAsync(projectId, cancellationToken);
    var workingRules = await ruleSets.LoadAsync(projectId, cancellationToken);
    var schemaProposal = await schemaProposals.LoadAsync(projectId, cancellationToken);
    var activeSchema = await activeSchemas.LoadAsync(projectId, cancellationToken);
    var schemaDecisions = await expansionRequests.ListSchemaDecisionsAsync(projectId, cancellationToken);
    return Results.Ok(new
    {
        plans = await store.ListPlansAsync(projectId, cancellationToken),
        planDecisions = await store.ListPlanDecisionsAsync(projectId, cancellationToken),
        recommendationDecisions = await store.ListRecommendationDecisionsAsync(projectId, cancellationToken),
        expansionRequests = requests,
        expansionSchemaDecisions = schemaDecisions,
        expansionProgress = requests.Select(request => ExpansionRequestService.EvaluateProgress(request, project, workingRules, schemaProposal, activeSchema, schemaDecisions)).ToArray()
    });
});
projects.MapPut("/{projectId}/development-plans/{planId}", async (string projectId, string planId, DevelopmentPlanProposal plan, IDesignProjectStore projectsStore, IProjectInventoryStore inventories, IDevelopmentPlanStore plans, CancellationToken cancellationToken) =>
{
    if (plan.ProjectId != projectId || plan.Id != planId) return Results.BadRequest("The URL identifiers must match the development plan.");
    if (await projectsStore.LoadAsync(projectId, cancellationToken) is null) return Results.NotFound("The project does not exist.");
    var inventory = (await inventories.ListAsync(projectId, cancellationToken)).SingleOrDefault(item => item.Id == plan.InventoryId);
    if (inventory is null) return Results.BadRequest("The development plan inventory does not exist in this project.");
    var replaced = plan.ReplacesPlanId is null ? null : await plans.LoadPlanAsync(projectId, plan.ReplacesPlanId, cancellationToken);
    try { DevelopmentPlanService.Validate(plan, inventory, replaced); }
    catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
    await plans.SavePlanAsync(plan, cancellationToken);
    return Results.NoContent();
});
projects.MapPost("/{projectId}/development-plans/{planId}/decisions", async (string projectId, string planId, DevelopmentPlanDecisionRequest request, IDevelopmentPlanStore store, CancellationToken cancellationToken) =>
{
    var plan = await store.LoadPlanAsync(projectId, planId, cancellationToken);
    if (plan is null) return Results.NotFound("The development plan does not exist.");
    DevelopmentPlanDecision decision;
    try { decision = DevelopmentPlanService.DecidePlan(plan, request.Kind, request.Note, DateTimeOffset.UtcNow); }
    catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
    await store.SavePlanDecisionAsync(decision, cancellationToken);
    return Results.Ok(decision);
});
projects.MapPost("/{projectId}/development-plans/{planId}/recommendations/{recommendationId}/decisions", async (string projectId, string planId, string recommendationId, DevelopmentRecommendationDecisionRequest request, IDevelopmentPlanStore store, CancellationToken cancellationToken) =>
{
    var plan = await store.LoadPlanAsync(projectId, planId, cancellationToken);
    if (plan is null) return Results.NotFound("The development plan does not exist.");
    DevelopmentRecommendationDecision decision;
    try { decision = DevelopmentPlanService.DecideRecommendation(plan, recommendationId, request.Kind, request.Note, DateTimeOffset.UtcNow); }
    catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
    await store.SaveRecommendationDecisionAsync(decision, cancellationToken);
    return Results.Ok(decision);
});
projects.MapPost("/{projectId}/development-plans/{planId}/recommendations/{recommendationId}/expansion-request", async (
    string projectId,
    string planId,
    string recommendationId,
    IDesignProjectStore projectsStore,
    IProjectInventoryStore inventories,
    IDevelopmentPlanStore plans,
    IExpansionRequestStore expansionRequests,
    CancellationToken cancellationToken) =>
{
    var project = await projectsStore.LoadAsync(projectId, cancellationToken);
    var plan = await plans.LoadPlanAsync(projectId, planId, cancellationToken);
    if (project is null || plan is null) return Results.NotFound("The project or development plan does not exist.");
    var recommendation = plan.Recommendations.SingleOrDefault(item => item.Id == recommendationId);
    if (recommendation is null) return Results.NotFound("The recommendation does not exist in this plan.");

    var allPlans = await plans.ListPlansAsync(projectId, cancellationToken);
    if (allPlans.Any(item => item.ReplacesPlanId == plan.Id))
        return Results.Conflict("This plan has been replaced. Accept a recommendation from the current plan instead.");
    var latestInventory = await inventories.LoadLatestAsync(projectId, cancellationToken);
    if (latestInventory is null || latestInventory.Id != plan.InventoryId || latestInventory.Revision != plan.InventoryRevision)
        return Results.Conflict("This plan uses an older inventory. Build and review a current plan before starting expansion.");
    var latestPlanDecision = (await plans.ListPlanDecisionsAsync(projectId, cancellationToken))
        .Where(item => item.PlanId == plan.Id).OrderByDescending(item => item.CreatedAt).FirstOrDefault();
    if (latestPlanDecision?.Kind is DevelopmentPlanDecisionKind.Rejected or DevelopmentPlanDecisionKind.RevisionRequested)
        return Results.Conflict("A rejected plan or a plan awaiting revision cannot start expansion.");
    var acceptance = (await plans.ListRecommendationDecisionsAsync(projectId, cancellationToken))
        .Where(item => item.PlanId == plan.Id && item.RecommendationId == recommendationId)
        .OrderByDescending(item => item.CreatedAt).FirstOrDefault();
    if (acceptance?.Kind != DevelopmentRecommendationDecisionKind.Accepted)
        return Results.Conflict("Accept the current recommendation before starting expansion.");

    var existing = await expansionRequests.LoadByAcceptanceDecisionAsync(projectId, acceptance.Id, cancellationToken);
    if (existing is not null) return Results.Ok(existing);
    ExpansionRequest request;
    DesignSourceNote source;
    try { (request, source) = ExpansionRequestService.Create(project, latestInventory, plan, recommendation, acceptance, DateTimeOffset.UtcNow); }
    catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
    if (!project.SourceNotes.Any(item => item.Id == source.Id))
    {
        project = DesignProjectService.AddSources(project, [source]);
        await projectsStore.SaveAsync(project, cancellationToken);
    }
    await expansionRequests.SaveAsync(request, cancellationToken);
    return Results.Ok(request);
});
projects.MapPost("/{projectId}/expansion-requests/{requestId}/schema-decisions", async (
    string projectId,
    string requestId,
    ExpansionSchemaDecisionRequest input,
    IDesignProjectStore projectsStore,
    IWorkingRuleSetStore ruleSets,
    IExpansionRequestStore expansionRequests,
    CancellationToken cancellationToken) =>
{
    var project = await projectsStore.LoadAsync(projectId, cancellationToken);
    var request = await expansionRequests.LoadAsync(projectId, requestId, cancellationToken);
    if (project is null || request is null) return Results.NotFound("The project or expansion request does not exist.");
    var workingRules = await ruleSets.LoadAsync(projectId, cancellationToken);
    var progress = ExpansionRequestService.EvaluateProgress(request, project, workingRules);
    ExpansionSchemaDecision decision;
    try { decision = ExpansionRequestService.DecideSchema(request, progress, input.Kind, input.Note, DateTimeOffset.UtcNow); }
    catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
    await expansionRequests.SaveSchemaDecisionAsync(decision, cancellationToken);
    return Results.Ok(decision);
});

projects.MapGet("/{projectId}/inventories", async (string projectId, IProjectInventoryStore store, CancellationToken cancellationToken) =>
    Results.Ok(await store.ListAsync(projectId, cancellationToken)));
projects.MapGet("/{projectId}/inventories/latest", async (string projectId, IProjectInventoryStore store, CancellationToken cancellationToken) =>
{
    var inventory = await store.LoadLatestAsync(projectId, cancellationToken);
    return inventory is null ? Results.NotFound() : Results.Ok(inventory);
});
projects.MapPost("/{projectId}/inventories", async (
    string projectId,
    IDesignProjectStore projectsStore,
    IWorkingRuleSetStore ruleSets,
    IProjectSchemaProposalStore schemaProposals,
    IActiveGameSchemaStore activeSchemas,
    IProjectGameDataStore gameData,
    IContentProposalStore contentProposals,
    IContentApplicationStore contentApplications,
    IContentBalanceProfileStore balanceProfiles,
    IPublicationSnapshotStore publications,
    IProjectInventoryStore inventories,
    CancellationToken cancellationToken) =>
{
    var project = await projectsStore.LoadAsync(projectId, cancellationToken);
    if (project is null) return Results.NotFound("The project does not exist.");
    var input = new ProjectInventoryInput(
        project,
        await ruleSets.LoadAsync(projectId, cancellationToken),
        await schemaProposals.LoadAsync(projectId, cancellationToken),
        await activeSchemas.LoadAsync(projectId, cancellationToken),
        await gameData.LoadAsync(projectId, cancellationToken),
        await contentProposals.ListProposalsAsync(projectId, cancellationToken),
        await contentProposals.ListDecisionsAsync(projectId, cancellationToken),
        await contentApplications.ListAsync(projectId, cancellationToken),
        await balanceProfiles.ListAsync(projectId, cancellationToken),
        await publications.ListAsync(projectId, cancellationToken));
    var inventory = ProjectInventoryService.Create(input, await inventories.LoadLatestAsync(projectId, cancellationToken), DateTimeOffset.UtcNow);
    await inventories.SaveAsync(inventory, cancellationToken);
    return Results.Ok(inventory);
});

projects.MapGet("/{projectId}/content-proposals", async (string projectId, IContentProposalStore store, IContentApplicationStore applications, CancellationToken cancellationToken) => Results.Ok(new { proposals = await store.ListProposalsAsync(projectId, cancellationToken), decisions = await store.ListDecisionsAsync(projectId, cancellationToken), applications = await applications.ListAsync(projectId, cancellationToken) }));
projects.MapGet("/{projectId}/evaluation-proposals", async (string projectId, IEvaluationAssistanceStore store, CancellationToken cancellationToken) =>
    Results.Ok(new EvaluationAssistanceWorkspace(await store.ListProposalsAsync(projectId, cancellationToken), await store.ListDecisionsAsync(projectId, cancellationToken))));
projects.MapPut("/{projectId}/evaluation-proposals/{proposalId}", async (string projectId, string proposalId, EvaluationAssistanceProposal proposal, IDesignProjectStore projectsStore, IActiveGameSchemaStore activeSchemas, IWorkingRuleSetStore ruleSets, IEvaluationAssistanceStore store, CancellationToken cancellationToken) =>
{
    if (proposal.ProjectId != projectId || proposal.Id != proposalId) return Results.BadRequest("The URL identifiers must match the evaluation proposal.");
    if (await projectsStore.LoadAsync(projectId, cancellationToken) is null) return Results.NotFound("The project does not exist.");
    var schema = await activeSchemas.LoadAsync(projectId, cancellationToken);
    var rules = await ruleSets.LoadAsync(projectId, cancellationToken);
    if (schema is null || rules is null) return Results.Conflict("A current active schema and Working Rule Set are required.");
    try
    {
        var candidate = JsonNode.Parse(proposal.CandidateJson) as JsonObject ?? throw new ArgumentException("The evaluation candidate is not a JSON object.");
        var validated = EvaluationAssistanceService.Create(projectId, proposal.EntityTypeId, proposal.Kind, proposal.Summary, candidate, proposal.SourceWorkingRuleIds, schema, rules, proposal.CreatedAt,
            proposal.ReplacesProposalId is null ? null : await store.LoadProposalAsync(projectId, proposal.ReplacesProposalId, cancellationToken));
        if (proposal.Version != validated.Version || proposal.ActiveGameSchemaId != validated.ActiveGameSchemaId || proposal.ActiveGameSchemaRevision != validated.ActiveGameSchemaRevision
            || proposal.WorkingRuleSetId != validated.WorkingRuleSetId || proposal.WorkingRuleSetRevision != validated.WorkingRuleSetRevision)
            return Results.BadRequest("The evaluation proposal must use the current validated revision bindings.");
    }
    catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
    await store.SaveProposalAsync(proposal, cancellationToken);
    return Results.NoContent();
});
projects.MapPost("/{projectId}/evaluation-proposals/{proposalId}/decisions", async (string projectId, string proposalId, EvaluationProposalDecisionRequest request, IEvaluationAssistanceStore store, CancellationToken cancellationToken) =>
{
    var proposal = await store.LoadProposalAsync(projectId, proposalId, cancellationToken);
    if (proposal is null) return Results.NotFound("The evaluation proposal does not exist.");
    EvaluationProposalDecision decision;
    try { decision = EvaluationAssistanceService.Decide(proposal, request.Kind, request.Note, DateTimeOffset.UtcNow); }
    catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
    await store.SaveDecisionAsync(decision, cancellationToken);
    return Results.Ok(decision);
});
projects.MapGet("/{projectId}/content-expansion-runs", async (
    string projectId,
    IContentExpansionRunStore runs,
    IContentProposalStore proposals,
    IContentApplicationStore applications,
    CancellationToken cancellationToken) =>
{
    var allProposals = await proposals.ListProposalsAsync(projectId, cancellationToken);
    var allApplications = await applications.ListAsync(projectId, cancellationToken);
    var result = new List<ContentExpansionRunWorkspaceItem>();
    foreach (var run in await runs.ListRunsAsync(projectId, cancellationToken))
    {
        var links = await runs.ListProposalLinksAsync(projectId, run.Id, cancellationToken);
        result.Add(new ContentExpansionRunWorkspaceItem(run, links, ContentExpansionRunService.Evaluate(run, links, allProposals, allApplications)));
    }
    return Results.Ok(result);
});
projects.MapPost("/{projectId}/content-expansion-runs", async (
    string projectId,
    ContentExpansionRunCreationRequest request,
    IDesignProjectStore projectsStore,
    IActiveGameSchemaStore activeSchemas,
    IWorkingRuleSetStore ruleSets,
    IContentExpansionRunStore runs,
    CancellationToken cancellationToken) =>
{
    if (await projectsStore.LoadAsync(projectId, cancellationToken) is null) return Results.NotFound("The project does not exist.");
    var schema = await activeSchemas.LoadAsync(projectId, cancellationToken);
    var workingRules = await ruleSets.LoadAsync(projectId, cancellationToken);
    if (schema is null || workingRules is null) return Results.Conflict("A current active schema and Working Rule Set are required before planning content expansion.");
    try
    {
        var run = ContentExpansionRunService.Create(projectId, request.Name, schema, workingRules.Id, workingRules.Revision, request.Stages, DateTimeOffset.UtcNow);
        await runs.SaveRunAsync(run, cancellationToken);
        return Results.Ok(run);
    }
    catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
});
projects.MapPost("/{projectId}/content-expansion-runs/{runId}/stages/{stageId}/proposals/{proposalId}", async (
    string projectId,
    string runId,
    string stageId,
    string proposalId,
    IContentExpansionRunStore runs,
    IContentProposalStore proposals,
    IContentApplicationStore applications,
    CancellationToken cancellationToken) =>
{
    var run = await runs.LoadRunAsync(projectId, runId, cancellationToken);
    var proposal = await proposals.LoadProposalAsync(projectId, proposalId, cancellationToken);
    if (run is null || proposal is null) return Results.NotFound("The expansion run or content proposal does not exist.");
    var links = await runs.ListProposalLinksAsync(projectId, runId, cancellationToken);
    var existing = links.SingleOrDefault(link => link.ProposalId == proposalId && link.ProposalVersion == proposal.Version);
    if (existing is not null) return Results.Ok(existing);
    try
    {
        var link = ContentExpansionRunService.LinkProposal(run, stageId, proposal, links,
            await proposals.ListProposalsAsync(projectId, cancellationToken), await applications.ListAsync(projectId, cancellationToken), DateTimeOffset.UtcNow);
        await runs.SaveProposalLinkAsync(link, cancellationToken);
        return Results.Ok(link);
    }
    catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
    catch (InvalidOperationException exception) { return Results.Conflict(exception.Message); }
});
projects.MapPut("/{projectId}/content-proposals/{proposalId}", async (string projectId, string proposalId, ContentProposal proposal, IDesignProjectStore projectsStore, IContentProposalStore store, CancellationToken cancellationToken) =>
{
    if (!string.Equals(projectId, proposal.ProjectId, StringComparison.Ordinal) || !string.Equals(proposalId, proposal.Id, StringComparison.Ordinal)) return Results.BadRequest("The URL identifiers must match the proposal.");
    if (await projectsStore.LoadAsync(projectId, cancellationToken) is null) return Results.NotFound("The project does not exist.");
    var existing = await store.LoadProposalAsync(projectId, proposalId, cancellationToken);
    if (existing is not null && (await store.ListDecisionsAsync(projectId, cancellationToken)).Any(decision => decision.ProposalId == proposalId) &&
        !JsonNode.DeepEquals(System.Text.Json.JsonSerializer.SerializeToNode(existing), System.Text.Json.JsonSerializer.SerializeToNode(proposal)))
        return Results.Conflict("A reviewed proposal cannot be overwritten. Save a new proposal version for review.");
    await store.SaveProposalAsync(proposal, cancellationToken); return Results.NoContent();
});
projects.MapPost("/{projectId}/content-proposals/{proposalId}/decisions", async (string projectId, string proposalId, ContentProposalDecision decision, IContentProposalStore store, CancellationToken cancellationToken) =>
{
    if (!string.Equals(projectId, decision.ProjectId, StringComparison.Ordinal) || !string.Equals(proposalId, decision.ProposalId, StringComparison.Ordinal)) return Results.BadRequest("The URL identifiers must match the decision.");
    if (await store.LoadProposalAsync(projectId, proposalId, cancellationToken) is null) return Results.NotFound("The proposal does not exist.");
    await store.SaveDecisionAsync(decision, cancellationToken); return Results.NoContent();
});
projects.MapPost("/{projectId}/content-proposals/{proposalId}/apply", async (string projectId, string proposalId, IContentProposalStore proposals, IContentApplicationStore applications, IActiveGameSchemaStore activeSchemas, IProjectGameDataStore gameData, CancellationToken cancellationToken) =>
{
    var proposal = await proposals.LoadProposalAsync(projectId, proposalId, cancellationToken);
    if (proposal is null) return Results.NotFound("The proposal does not exist.");
    var existingApplication = await applications.LoadAsync(projectId, proposalId, proposal.Version, cancellationToken);
    if (existingApplication is { Status: ContentApplicationStatus.Applied }) return Results.Ok(existingApplication);

    var latestDecision = (await proposals.ListDecisionsAsync(projectId, cancellationToken))
        .Where(decision => decision.ProposalId == proposal.Id && decision.ProposalVersion == proposal.Version)
        .OrderBy(decision => decision.CreatedAt)
        .LastOrDefault();
    if (latestDecision is not { Kind: ContentProposalDecisionKind.Approved })
        return Results.Conflict("The exact proposal version must have a current approval decision before it can be applied.");
    var activeSchema = await activeSchemas.LoadAsync(projectId, cancellationToken);
    var currentGameData = await gameData.LoadAsync(projectId, cancellationToken);
    if (activeSchema is null || currentGameData is null) return Results.Conflict("The project needs a current active schema and canonical Game Data before content can be applied.");

    if (existingApplication is { Status: ContentApplicationStatus.Pending } && ContentApplicationService.CanonicalEntryMatches(existingApplication, currentGameData))
    {
        var recovered = ContentApplicationService.Complete(existingApplication, currentGameData, DateTimeOffset.UtcNow);
        await applications.SaveAsync(recovered, cancellationToken);
        return Results.Ok(recovered);
    }

    try
    {
        var preparation = ContentApplicationService.Prepare(proposal, latestDecision, activeSchema, currentGameData, DateTimeOffset.UtcNow);
        var pending = existingApplication ?? preparation.Application;
        if (existingApplication is not null && (!string.Equals(existingApplication.CandidateFingerprint, preparation.Application.CandidateFingerprint, StringComparison.Ordinal) || !string.Equals(existingApplication.ApprovalDecisionId, preparation.Application.ApprovalDecisionId, StringComparison.Ordinal)))
            return Results.Conflict("The pending application no longer matches the approved proposal and decision. Nothing was changed.");
        await applications.SaveAsync(pending, cancellationToken);
        await gameData.SaveAsync(preparation.UpdatedGameData, cancellationToken);
        var applied = ContentApplicationService.Complete(pending, preparation.UpdatedGameData, DateTimeOffset.UtcNow);
        await applications.SaveAsync(applied, cancellationToken);
        return Results.Ok(applied);
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(exception.Message);
    }
});
projects.MapGet("/{projectId}/publications", async (string projectId, IPublicationSnapshotStore publications, CancellationToken cancellationToken) =>
    Results.Ok(await publications.ListAsync(projectId, cancellationToken)));
projects.MapPost("/{projectId}/publications", async (string projectId, IDesignProjectStore projectsStore, IWorkingRuleSetStore ruleSets, IActiveGameSchemaStore activeSchemas, IProjectGameDataStore gameData, IContentBalanceProfileStore balanceProfiles, IPublicationSnapshotStore publications, CancellationToken cancellationToken) =>
{
    var project = await projectsStore.LoadAsync(projectId, cancellationToken);
    if (project is null) return Results.NotFound("The project does not exist.");
    var rules = await ruleSets.LoadAsync(projectId, cancellationToken);
    var schema = await activeSchemas.LoadAsync(projectId, cancellationToken);
    var data = await gameData.LoadAsync(projectId, cancellationToken);
    if (rules is null || schema is null || data is null) return Results.Conflict("Publication requires a Working Rule Set, active schema, and canonical Game Data.");
    try
    {
        var snapshot = PublicationSnapshotService.Create(project.Name, rules, schema, data, DateTimeOffset.UtcNow, await balanceProfiles.ListAsync(projectId, cancellationToken));
        await publications.SaveAsync(snapshot, cancellationToken);
        return Results.Ok(snapshot);
    }
    catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
    catch (InvalidOperationException exception) { return Results.Conflict(exception.Message); }
});
projects.MapGet("/{projectId}/publication-editions", async (string projectId, IPublicationEditionStore editions, CancellationToken cancellationToken) =>
    Results.Ok(await editions.ListAsync(projectId, cancellationToken)));
projects.MapPost("/{projectId}/publications/{snapshotId}/editions", async (string projectId, string snapshotId, PublicationEditionRevisionRequest request, IPublicationSnapshotStore publications, IPublicationEditionStore editions, IPublicationEditorialStore editorial, CancellationToken cancellationToken) =>
{
    var snapshot = await publications.LoadAsync(projectId, snapshotId, cancellationToken);
    if (snapshot is null) return Results.NotFound("The immutable publication snapshot does not exist.");
    PublicationEdition? prior = null;
    if (request.BasedOnEditionId is not null || request.BasedOnRevision is not null)
    {
        var requestedBase = (await editions.ListAsync(projectId, cancellationToken)).SingleOrDefault(edition => edition.Id == request.BasedOnEditionId && edition.Revision == request.BasedOnRevision);
        if (requestedBase is null) return Results.Conflict("The requested base publication edition does not exist. Reload the edition history.");
        prior = await editions.LoadLatestAsync(projectId, requestedBase.SeriesId, cancellationToken);
    }
    try
    {
        var edition = PublicationEditionService.CreateRevision(snapshot, request, prior, DateTimeOffset.UtcNow, await editorial.ListProposalsAsync(projectId, cancellationToken), await editorial.ListDecisionsAsync(projectId, cancellationToken));
        await editions.SaveAsync(edition, cancellationToken);
        return Results.Ok(edition);
    }
    catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
    catch (InvalidOperationException exception) { return Results.Conflict(exception.Message); }
});
projects.MapGet("/{projectId}/publication-editorial", async (string projectId, IPublicationEditorialStore store, CancellationToken cancellationToken) =>
    Results.Ok(new { proposals = await store.ListProposalsAsync(projectId, cancellationToken), decisions = await store.ListDecisionsAsync(projectId, cancellationToken) }));
projects.MapPut("/{projectId}/publication-editorial/{proposalId}", async (string projectId, string proposalId, PublicationEditorialProposal proposal, IPublicationSnapshotStore publications, IPublicationEditorialStore store, CancellationToken cancellationToken) =>
{
    if (!string.Equals(projectId, proposal.ProjectId, StringComparison.Ordinal) || !string.Equals(proposalId, proposal.Id, StringComparison.Ordinal)) return Results.BadRequest("The URL identifiers must match the editorial proposal.");
    var snapshot = await publications.LoadAsync(projectId, proposal.SnapshotId, cancellationToken);
    if (snapshot is null) return Results.NotFound("The immutable publication snapshot does not exist.");
    try { PublicationEditorialProposalService.Validate(snapshot, proposal); }
    catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
    catch (InvalidOperationException exception) { return Results.Conflict(exception.Message); }
    await store.SaveProposalAsync(proposal, cancellationToken);
    return Results.NoContent();
});
projects.MapPost("/{projectId}/publication-editorial/{proposalId}/decisions", async (string projectId, string proposalId, PublicationEditorialDecision decision, IPublicationEditorialStore store, CancellationToken cancellationToken) =>
{
    if (!string.Equals(projectId, decision.ProjectId, StringComparison.Ordinal) || !string.Equals(proposalId, decision.ProposalId, StringComparison.Ordinal)) return Results.BadRequest("The URL identifiers must match the editorial decision.");
    var proposal = await store.LoadProposalAsync(projectId, proposalId, cancellationToken);
    if (proposal is null || proposal.Revision != decision.ProposalRevision) return Results.NotFound("The exact editorial proposal revision does not exist.");
    var replacement = decision.ReplacementProposalId is null ? null : await store.LoadProposalAsync(projectId, decision.ReplacementProposalId, cancellationToken);
    try { PublicationEditorialProposalService.ValidateDecision(proposal, decision, replacement); }
    catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
    await store.SaveDecisionAsync(decision, cancellationToken);
    return Results.NoContent();
});
projects.MapGet("/{projectId}/content-balance-profiles", async (string projectId, IContentBalanceProfileStore store, CancellationToken cancellationToken) => Results.Ok(await store.ListAsync(projectId, cancellationToken)));
projects.MapPut("/{projectId}/content-balance-profiles/{profileId}", async (string projectId, string profileId, ProjectContentBalanceProfile profile, IDesignProjectStore projectsStore, IActiveGameSchemaStore activeSchemas, IContentBalanceProfileStore store, CancellationToken cancellationToken) =>
{
    if (!string.Equals(projectId, profile.ProjectId, StringComparison.Ordinal) || !string.Equals(profileId, profile.Id, StringComparison.Ordinal)) return Results.BadRequest("The URL identifiers must match the balance profile.");
    var active = await activeSchemas.LoadAsync(projectId, cancellationToken);
    if (await projectsStore.LoadAsync(projectId, cancellationToken) is null) return Results.NotFound("The project does not exist.");
    if (active is null || !string.Equals(active.Id, profile.ActiveGameSchemaId, StringComparison.Ordinal) || active.Revision != profile.ActiveGameSchemaRevision) return Results.BadRequest("A balance profile must be bound to the current active schema revision.");
    try { _ = ContentBalanceProfileService.Create(projectId, profile.ActiveGameSchemaId, profile.ActiveGameSchemaRevision, profile.Profile, profile.CreatedAt, profile with { Id = "", Revision = 0, CreatedAt = default, UpdatedAt = default }); }
    catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }
    await store.SaveAsync(profile, cancellationToken); return Results.NoContent();
});

projects.MapGet("/{projectId}/working-rule-preparation", async (string projectId, IWorkingRulePreparationStore store, CancellationToken cancellationToken) =>
{
    var checkpoint = await store.LoadAsync(projectId, cancellationToken);
    return checkpoint is null ? Results.NotFound() : Results.Ok(checkpoint);
});
projects.MapPut("/{projectId}/working-rule-preparation", async (string projectId, WorkingRulePreparation checkpoint, IDesignProjectStore projectsStore, IWorkingRuleSetStore rules, IWorkingRulePreparationStore store, CancellationToken cancellationToken) =>
{
    if (checkpoint.ProjectId != projectId) return Results.BadRequest("Project IDs Must Match.");
    var project = await projectsStore.LoadAsync(projectId, cancellationToken);
    if (project is null) return Results.NotFound();
    try { WorkingRulePreparationBinding.Validate(checkpoint, project, await rules.LoadAsync(projectId, cancellationToken), checkpoint.AddMissingOnly); }
    catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }
    await store.SaveAsync(checkpoint, cancellationToken);
    return Results.NoContent();
});
projects.MapDelete("/{projectId}/working-rule-preparation", async (string projectId, IWorkingRulePreparationStore store, CancellationToken cancellationToken) =>
{
    await store.DeleteAsync(projectId, cancellationToken);
    return Results.NoContent();
});
projects.MapPost("/{projectId}/working-rule-preparation/apply", async (string projectId, string fingerprint, WorkingRuleSet ruleSet, IDesignProjectStore projectsStore, IWorkingRuleSetStore rules, IWorkingRulePreparationStore store, WorkingRuleSetValidator validator, CancellationToken cancellationToken) =>
{
    var project = await projectsStore.LoadAsync(projectId, cancellationToken);
    var checkpoint = await store.LoadAsync(projectId, cancellationToken);
    if (project is null || checkpoint is null) return Results.NotFound("Project Or Checkpoint Not Found.");
    if (ruleSet.ProjectId != projectId || checkpoint.InputFingerprint != fingerprint) return Results.Conflict("Checkpoint Binding Changed. Reload The Project.");
    var current = await rules.LoadAsync(projectId, cancellationToken);
    try { WorkingRulePreparationBinding.Validate(checkpoint, project, current, checkpoint.AddMissingOnly); }
    catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }
    var required = WorkingRulePreparationBinding.SelectDrafts(project, current, checkpoint.AddMissingOnly).Select(draft => draft.Reference).ToHashSet();
    var prepared = checkpoint.Batches.SelectMany(batch => batch.RuleSet.Rules).SelectMany(rule => rule.SourceDraftRules).ToHashSet();
    var proposed = ruleSet.Rules.SelectMany(rule => rule.SourceDraftRules).ToHashSet();
    if (!required.IsSubsetOf(prepared) || !required.IsSubsetOf(proposed)) return Results.BadRequest("The Checkpoint Is Not Complete. Saved Project Rules Were Not Changed.");
    var expected = WorkingRuleSetFormalizationService.ComposeCheckpoint(project, current, checkpoint, ruleSet.UpdatedAt);
    if (System.Text.Json.JsonSerializer.Serialize(expected) != System.Text.Json.JsonSerializer.Serialize(ruleSet))
        return Results.BadRequest("The Submitted Rules Differ From The Saved Checkpoint. Resume Its Prepared Work Before Applying.");
    var validation = validator.Validate(ruleSet, project);
    if (!validation.IsValid) return Results.BadRequest(validation.Issues);
    await rules.SaveAsync(ruleSet, cancellationToken);
    return Results.NoContent(); // Desktop clears the checkpoint after this save is acknowledged.
});

projects.MapGet("/{projectId}/working-rules", async (string projectId, IWorkingRuleSetStore store, CancellationToken cancellationToken) =>
{
    var ruleSet = await store.LoadAsync(projectId, cancellationToken);
    return ruleSet is null ? Results.NotFound() : Results.Ok(ruleSet);
});
projects.MapPut("/{projectId}/working-rules", async (string projectId, WorkingRuleSet ruleSet, IDesignProjectStore projectsStore, IWorkingRuleSetStore ruleSets, WorkingRuleSetValidator validator, CancellationToken cancellationToken) =>
{
    if (!string.Equals(projectId, ruleSet.ProjectId, StringComparison.Ordinal)) return Results.BadRequest("The URL project id must match the working rule set projectId.");
    var project = await projectsStore.LoadAsync(projectId, cancellationToken);
    if (project is null) return Results.NotFound("The project does not exist.");
    var validation = validator.Validate(ruleSet, project);
    if (!validation.IsValid) return Results.BadRequest(validation.Issues);
    await ruleSets.SaveAsync(ruleSet, cancellationToken);
    // Retain schema work and provenance at its original binding. Review/activation
    // reject stale bindings until a current proposal has been explicitly prepared.
    return Results.NoContent();
});
projects.MapDelete("/{projectId}/working-rules", async (string projectId, IWorkingRuleSetStore store, IProjectSchemaProposalStore schemaProposals, CancellationToken cancellationToken) =>
{
    await schemaProposals.DeleteAsync(projectId, cancellationToken);
    await store.DeleteAsync(projectId, cancellationToken);
    return Results.NoContent();
});

projects.MapGet("/{projectId}/schema-proposal", async (string projectId, IProjectSchemaProposalStore store, CancellationToken cancellationToken) =>
{
    var proposal = await store.LoadAsync(projectId, cancellationToken);
    return proposal is null ? Results.NotFound() : Results.Ok(proposal);
});
projects.MapPut("/{projectId}/schema-proposal", async (string projectId, ProjectSchemaProposal proposal, IDesignProjectStore projectsStore, IWorkingRuleSetStore ruleSets, IProjectSchemaProposalStore schemaProposals, CancellationToken cancellationToken) =>
{
    if (!string.Equals(projectId, proposal.ProjectId, StringComparison.Ordinal)) return Results.BadRequest("The URL project id must match the proposal projectId.");
    if (string.IsNullOrWhiteSpace(proposal.Id)) return Results.BadRequest("A schema proposal id is required.");
    var project = await projectsStore.LoadAsync(projectId, cancellationToken);
    if (project is null) return Results.NotFound("The project does not exist.");
    var ruleSet = await ruleSets.LoadAsync(projectId, cancellationToken);
    if (ruleSet is null || !string.Equals(ruleSet.Id, proposal.WorkingRuleSetId, StringComparison.Ordinal) || ruleSet.Revision != proposal.WorkingRuleSetRevision)
        return Results.BadRequest("The schema proposal must be based on the current saved Working Rule Set revision.");
    var existingProposal = await schemaProposals.LoadAsync(projectId, cancellationToken);
    if (existingProposal is not null && !string.Equals(existingProposal.Id, proposal.Id, StringComparison.Ordinal))
        return Results.Conflict("The current schema proposal id must be preserved when updating it. Reload the project's saved proposal before saving changes.");
    await schemaProposals.SaveAsync(proposal, cancellationToken);
    return Results.NoContent();
});
projects.MapDelete("/{projectId}/schema-proposal", async (string projectId, IProjectSchemaProposalStore store, CancellationToken cancellationToken) =>
{
    await store.DeleteAsync(projectId, cancellationToken);
    return Results.NoContent();
});

projects.MapGet("/{projectId}/schema-change-requests", async (string projectId, IProjectSchemaChangeRequestStore store, CancellationToken cancellationToken) =>
    Results.Ok(await store.ListAsync(projectId, cancellationToken)));
projects.MapPut("/{projectId}/schema-change-requests/{requestId}", async (string projectId, string requestId, SchemaChangeRequest request, IDesignProjectStore projectsStore, IProjectSchemaChangeRequestStore store, CancellationToken cancellationToken) =>
{
    if (!string.Equals(projectId, request.ProjectId, StringComparison.Ordinal) || !string.Equals(requestId, request.Id, StringComparison.Ordinal))
        return Results.BadRequest("The URL identifiers must match the schema-change request.");
    if (await projectsStore.LoadAsync(projectId, cancellationToken) is null) return Results.NotFound("The project does not exist.");
    await store.SaveAsync(request, cancellationToken);
    return Results.NoContent();
});
projects.MapDelete("/{projectId}/schema-change-requests", async (string projectId, IProjectSchemaChangeRequestStore store, CancellationToken cancellationToken) =>
{
    await store.DeleteAsync(projectId, cancellationToken);
    return Results.NoContent();
});

projects.MapGet("/{projectId}/active-schema", async (string projectId, IActiveGameSchemaStore store, CancellationToken cancellationToken) =>
{
    var schema = await store.LoadAsync(projectId, cancellationToken);
    return schema is null ? Results.NotFound() : Results.Ok(schema);
});
projects.MapGet("/{projectId}/schema-transition", async (string projectId, IProjectSchemaProposalStore proposals, IWorkingRuleSetStore ruleSets, IActiveGameSchemaStore activeSchemas, IProjectGameDataStore gameData, CancellationToken cancellationToken) =>
{
    var proposal = await proposals.LoadAsync(projectId, cancellationToken);
    if (proposal is null) return Results.NotFound("A saved schema proposal is required before a transition can be reviewed.");
    var rules = await ruleSets.LoadAsync(projectId, cancellationToken);
    if (rules is null || rules.Id != proposal.WorkingRuleSetId || rules.Revision != proposal.WorkingRuleSetRevision)
        return Results.Conflict("The saved schema proposal uses an older or different Working Rule Set. Prepare an updated proposal before reviewing the transition.");
    var current = await activeSchemas.LoadAsync(projectId, cancellationToken);
    var existingData = await gameData.LoadAsync(projectId, cancellationToken);
    return Results.Ok(GameDataService.ReviewTransition(proposal, current, existingData, DateTimeOffset.UtcNow));
});
projects.MapGet("/{projectId}/schema-transitions", async (string projectId, ISchemaTransitionStore store, CancellationToken cancellationToken) =>
    Results.Ok(await store.ListAsync(projectId, cancellationToken)));
projects.MapPost("/{projectId}/active-schema", async (string projectId, IDesignProjectStore projectsStore, IProjectSchemaProposalStore proposals, IWorkingRuleSetStore ruleSets, IActiveGameSchemaStore activeSchemas, IProjectGameDataStore gameData, ISchemaTransitionStore transitions, CancellationToken cancellationToken) =>
{
    if (await projectsStore.LoadAsync(projectId, cancellationToken) is null) return Results.NotFound("The project does not exist.");
    var proposal = await proposals.LoadAsync(projectId, cancellationToken);
    if (proposal is null) return Results.BadRequest("A saved schema proposal is required before a schema can be activated.");
    var rules = await ruleSets.LoadAsync(projectId, cancellationToken);
    if (rules is null || rules.Id != proposal.WorkingRuleSetId || rules.Revision != proposal.WorkingRuleSetRevision)
        return Results.Conflict("The saved schema proposal uses an older or different Working Rule Set. Prepare an updated proposal before activation.");

    var current = await activeSchemas.LoadAsync(projectId, cancellationToken);
    var existingData = await gameData.LoadAsync(projectId, cancellationToken);
    SchemaTransitionReview review;
    SchemaTransitionResult transition;
    var now = DateTimeOffset.UtcNow;
    try
    {
        review = GameDataService.ReviewTransition(proposal, current, existingData, now);
        transition = GameDataService.ApplyReviewedTransition(proposal, current, existingData, now);
    }
    catch (GameDataFormatException exception)
    {
        return Results.BadRequest(exception.Message);
    }

    var record = GameDataService.CreateTransitionRecord(proposal, current, existingData, review, transition, now);
    try
    {
        await activeSchemas.SaveAsync(transition.ActiveSchema, cancellationToken);
        await gameData.SaveAsync(transition.GameData, cancellationToken);
        await transitions.SaveAsync(record, cancellationToken);
    }
    catch
    {
        // Recovery must still run when the request token itself was cancelled.
        if (current is not null) await activeSchemas.SaveAsync(current, CancellationToken.None);
        else await activeSchemas.DeleteAsync(projectId, CancellationToken.None);
        if (existingData is not null) await gameData.SaveAsync(existingData, CancellationToken.None);
        else await gameData.DeleteAsync(projectId, CancellationToken.None);
        throw;
    }
    return Results.Ok(transition.ActiveSchema);
});

projects.MapGet("/{projectId}/game-data", async (string projectId, IProjectGameDataStore store, CancellationToken cancellationToken) =>
{
    var data = await store.LoadAsync(projectId, cancellationToken);
    return data is null ? Results.NotFound() : Results.Ok(data);
});
projects.MapPut("/{projectId}/game-data", async (string projectId, ProjectGameData submitted, IDesignProjectStore projectsStore, IActiveGameSchemaStore activeSchemas, IProjectGameDataStore gameData, CancellationToken cancellationToken) =>
{
    if (!string.Equals(projectId, submitted.ProjectId, StringComparison.Ordinal)) return Results.BadRequest("The URL project id must match the game data projectId.");
    if (await projectsStore.LoadAsync(projectId, cancellationToken) is null) return Results.NotFound("The project does not exist.");
    var active = await activeSchemas.LoadAsync(projectId, cancellationToken);
    if (active is null) return Results.BadRequest("An active schema is required before game data can be saved.");

    var existing = await gameData.LoadAsync(projectId, cancellationToken);
    var normalized = submitted with
    {
        Id = existing?.Id ?? $"game-data-{projectId}",
        FormatVersion = "0.1",
        ActiveGameSchemaId = active.Id,
        ActiveGameSchemaRevision = active.Revision,
        CreatedAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
    var validation = GameDataService.Validate(active, normalized);
    if (!validation.IsValid) return Results.BadRequest(validation.Issues);
    await gameData.SaveAsync(normalized, cancellationToken);
    return Results.NoContent();
});

app.Run();

public partial class Program;
