namespace Quiz_Application_College.Services.Coding.Runner
{
    public record CodeRunRequest(
        string Language,
        string Source,
        IEnumerable<(string input, string expected, int weight)> Tests
    );

    public record CodeRunResult(
        int passed,
        int total,
        string? compileLog,
        string? runLog
    );

    public interface ICodeRunner
    {
        Task<CodeRunResult> RunAsync(CodeRunRequest req, CancellationToken ct = default);
    }
}
