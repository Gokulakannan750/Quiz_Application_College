namespace Quiz_Application_College.Services.Coding
{
    public class NoopCodeRunner : ICodeRunner
    {
        public bool IsEnabled => false;

        public Task<CodeRunResult> RunAsync(CodeRunRequest req)
        {
            return Task.FromResult(new CodeRunResult
            {
                Succeeded = false,
                Status = "RunnerDisabled",
                Stderr = "Code execution is disabled. Configure Judge0 in appsettings."
            });
        }
    }
}
