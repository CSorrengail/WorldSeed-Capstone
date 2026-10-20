using MongoDB.Driver;
using WorldSeed.DesignSessions;
using WorldSeed.Persistence.Mongo;
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
builder.Services.AddSingleton<WorkingRuleSetValidator>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<MongoWorkingRuleSetStore>().EnsureIndexesAsync();
    await scope.ServiceProvider.GetRequiredService<MongoProjectSchemaProposalStore>().EnsureIndexesAsync();
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
projects.MapDelete("/{projectId}", async (string projectId, IDesignProjectStore store, IWorkingRuleSetStore ruleSets, IProjectSchemaProposalStore schemaProposals, CancellationToken cancellationToken) =>
{
    await schemaProposals.DeleteAsync(projectId, cancellationToken);
    await ruleSets.DeleteAsync(projectId, cancellationToken);
    await store.DeleteAsync(projectId, cancellationToken);
    return Results.NoContent();
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

app.Run();

public partial class Program;
