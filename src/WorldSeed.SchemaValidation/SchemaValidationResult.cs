namespace WorldSeed.SchemaValidation;

public sealed record SchemaValidationIssue(string Path, string Message);

public sealed class SchemaValidationResult
{
    public List<SchemaValidationIssue> Issues { get; } = [];
    public bool IsValid => Issues.Count == 0;
    public void Add(string path, string message) => Issues.Add(new SchemaValidationIssue(path, message));
}
