using MongoDB.Driver;
using WorldSeed.Content;
using WorldSeed.DesignSessions;
using WorldSeed.GameData;
using WorldSeed.Persistence.Mongo;
using WorldSeed.Publication;
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
builder.Services.AddSingleton<MongoProjectSchemaProposalStore>();
builder.Services.AddSingleton<IProjectSchemaProposalStore>(services => services.GetRequiredService<MongoProjectSchemaProposalStore>());
builder.Services.AddSingleton<MongoProjectSchemaChangeRequestStore>();
builder.Services.AddSingleton<IProjectSchemaChangeRequestStore>(services => services.GetRequiredService<MongoProjectSchemaChangeRequestStore>());
builder.Services.AddSingleton<MongoActiveGameSchemaStore>();
builder.Services.AddSingleton<IActiveGameSchemaStore>(services => services.GetRequiredService<MongoActiveGameSchemaStore>());
builder.Services.AddSingleton<MongoProjectGameDataStore>();
builder.Services.AddSingleton<IProjectGameDataStore>(services => services.GetRequiredService<MongoProjectGameDataStore>());
builder.Services.AddSingleton<MongoContentProposalStore>();
builder.Services.AddSingleton<IContentProposalStore>(services => services.GetRequiredService<MongoContentProposalStore>());
builder.Services.AddSingleton<MongoContentApplicationStore>();
builder.Services.AddSingleton<IContentApplicationStore>(services => services.GetRequiredService<MongoContentApplicationStore>());
builder.Services.AddSingleton<MongoContentBalanceProfileStore>();
builder.Services.AddSingleton<IContentBalanceProfileStore>(services => services.GetRequiredService<MongoContentBalanceProfileStore>());
builder.Services.AddSingleton<MongoPublicationSnapshotStore>();
builder.Services.AddSingleton<IPublicationSnapshotStore>(services => services.GetRequiredService<MongoPublicationSnapshotStore>());
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
    await scope.ServiceProvider.GetRequiredService<MongoContentProposalStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoContentApplicationStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoContentBalanceProfileStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoPublicationSnapshotStore>().EnsureIndexesAsync();
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
projects.MapDelete("/{projectId}", async (string projectId, IDesignProjectStore store, IWorkingRuleSetStore ruleSets, IProjectSchemaProposalStore schemaProposals, IProjectSchemaChangeRequestStore schemaChangeRequests, IActiveGameSchemaStore activeSchemas, IProjectGameDataStore gameData, IContentProposalStore contentProposals, IContentApplicationStore contentApplications, IContentBalanceProfileStore balanceProfiles, IPublicationSnapshotStore publications, CancellationToken cancellationToken) =>
{
    await publications.DeleteAsync(projectId, cancellationToken);
    await contentApplications.DeleteAsync(projectId, cancellationToken);
    await contentProposals.DeleteAsync(projectId, cancellationToken);
    await balanceProfiles.DeleteAsync(projectId, cancellationToken);
    await gameData.DeleteAsync(projectId, cancellationToken);
    await activeSchemas.DeleteAsync(projectId, cancellationToken);
    await schemaProposals.DeleteAsync(projectId, cancellationToken);
    await schemaChangeRequests.DeleteAsync(projectId, cancellationToken);
    await ruleSets.DeleteAsync(projectId, cancellationToken);
    await store.DeleteAsync(projectId, cancellationToken);
    return Results.NoContent();
});

projects.MapGet("/{projectId}/content-proposals", async (string projectId, IContentProposalStore store, IContentApplicationStore applications, CancellationToken cancellationToken) => Results.Ok(new { proposals = await store.ListProposalsAsync(projectId, cancellationToken), decisions = await store.ListDecisionsAsync(projectId, cancellationToken), applications = await applications.ListAsync(projectId, cancellationToken) }));
projects.MapPut("/{projectId}/content-proposals/{proposalId}", async (string projectId, string proposalId, ContentProposal proposal, IDesignProjectStore projectsStore, IContentProposalStore store, CancellationToken cancellationToken) =>
{
    if (!string.Equals(projectId, proposal.ProjectId, StringComparison.Ordinal) || !string.Equals(proposalId, proposal.Id, StringComparison.Ordinal)) return Results.BadRequest("The URL identifiers must match the proposal.");
    if (await projectsStore.LoadAsync(projectId, cancellationToken) is null) return Results.NotFound("The project does not exist.");
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
projects.MapPost("/{projectId}/publications", async (string projectId, IDesignProjectStore projectsStore, IWorkingRuleSetStore ruleSets, IActiveGameSchemaStore activeSchemas, IProjectGameDataStore gameData, IPublicationSnapshotStore publications, CancellationToken cancellationToken) =>
{
    var project = await projectsStore.LoadAsync(projectId, cancellationToken);
    if (project is null) return Results.NotFound("The project does not exist.");
    var rules = await ruleSets.LoadAsync(projectId, cancellationToken);
    var schema = await activeSchemas.LoadAsync(projectId, cancellationToken);
    var data = await gameData.LoadAsync(projectId, cancellationToken);
    if (rules is null || schema is null || data is null) return Results.Conflict("Publication requires a Working Rule Set, active schema, and canonical Game Data.");
    try
    {
        var snapshot = PublicationSnapshotService.Create(project.Name, rules, schema, data, DateTimeOffset.UtcNow);
        await publications.SaveAsync(snapshot, cancellationToken);
        return Results.Ok(snapshot);
    }
    catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
    catch (InvalidOperationException exception) { return Results.Conflict(exception.Message); }
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

projects.MapGet("/{projectId}/working-rules", async (string projectId, IWorkingRuleSetStore store, CancellationToken cancellationToken) =>
{
    var ruleSet = await store.LoadAsync(projectId, cancellationToken);
    return ruleSet is null ? Results.NotFound() : Results.Ok(ruleSet);
});
projects.MapPut("/{projectId}/working-rules", async (string projectId, WorkingRuleSet ruleSet, IDesignProjectStore projectsStore, IWorkingRuleSetStore ruleSets, IProjectSchemaProposalStore schemaProposals, WorkingRuleSetValidator validator, CancellationToken cancellationToken) =>
{
    if (!string.Equals(projectId, ruleSet.ProjectId, StringComparison.Ordinal)) return Results.BadRequest("The URL project id must match the working rule set projectId.");
    var project = await projectsStore.LoadAsync(projectId, cancellationToken);
    if (project is null) return Results.NotFound("The project does not exist.");
    var validation = validator.Validate(ruleSet, project);
    if (!validation.IsValid) return Results.BadRequest(validation.Issues);
    await ruleSets.SaveAsync(ruleSet, cancellationToken);
    await schemaProposals.DeleteAsync(projectId, cancellationToken);
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
    var project = await projectsStore.LoadAsync(projectId, cancellationToken);
    if (project is null) return Results.NotFound("The project does not exist.");
    var ruleSet = await ruleSets.LoadAsync(projectId, cancellationToken);
    if (ruleSet is null || !string.Equals(ruleSet.Id, proposal.WorkingRuleSetId, StringComparison.Ordinal) || ruleSet.Revision != proposal.WorkingRuleSetRevision)
        return Results.BadRequest("The schema proposal must be based on the current saved Working Rule Set revision.");
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
projects.MapPost("/{projectId}/active-schema", async (string projectId, IDesignProjectStore projectsStore, IProjectSchemaProposalStore proposals, IActiveGameSchemaStore activeSchemas, IProjectGameDataStore gameData, CancellationToken cancellationToken) =>
{
    if (await projectsStore.LoadAsync(projectId, cancellationToken) is null) return Results.NotFound("The project does not exist.");
    var proposal = await proposals.LoadAsync(projectId, cancellationToken);
    if (proposal is null) return Results.BadRequest("A saved schema proposal is required before a schema can be activated.");

    var current = await activeSchemas.LoadAsync(projectId, cancellationToken);
    var existingData = await gameData.LoadAsync(projectId, cancellationToken);
    ActiveGameSchema active;
    try
    {
        active = GameDataService.Activate(proposal, current, GameDataService.HasAuthoredContent(existingData), DateTimeOffset.UtcNow);
    }
    catch (GameDataFormatException exception)
    {
        return Results.BadRequest(exception.Message);
    }

    await activeSchemas.SaveAsync(active, cancellationToken);
    if (existingData is null || existingData.ActiveGameSchemaRevision != active.Revision || !string.Equals(existingData.ActiveGameSchemaId, active.Id, StringComparison.Ordinal))
        await gameData.SaveAsync(GameDataService.CreateEmpty(active, DateTimeOffset.UtcNow), cancellationToken);
    return Results.Ok(active);
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
