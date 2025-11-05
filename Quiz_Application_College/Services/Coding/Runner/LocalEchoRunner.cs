using System.Text;
namespace Quiz_Application_College.Services.Coding.Runner
{
    /// <summary>
    /// Local stub: does NOT compile/run code. 
    /// - If source contains "EXPECTED_ALL_PASS" => all tests pass.
    /// - Else if source contains "EXPECTED_ZERO_PASS" => 0 pass.
    /// - Else: passes the tests whose expected text appears in the source (toy heuristic).
    /// Replace later with Judge0/real runner.
    /// </summary>
    public class LocalEchoRunner : ICodeRunner
    {
        public Task<CodeRunResult> RunAsync(CodeRunRequest req, CancellationToken ct = default)
        {
            var total = req.Tests.Count();
            int passed = 0;

            if (req.Source.Contains("EXPECTED_ALL_PASS"))
            {
                passed = total;
            }
            else if (req.Source.Contains("EXPECTED_ZERO_PASS"))
            {
                passed = 0;
            }
            else
            {
                foreach (var t in req.Tests)
                {
                    // toy rule: if student's code contains the expected output at all, consider it "passed"
                    if (!string.IsNullOrWhiteSpace(t.expected) && req.Source.Contains(t.expected))
                        passed++;
                }
            }

            var runLog = new StringBuilder()
                .AppendLine("LocalEchoRunner used (stub).")
                .AppendLine($"Language: {req.Language}")
                .AppendLine($"Tests: {total}, Passed: {passed}")
                .ToString();

            return Task.FromResult(new CodeRunResult(
                passed: passed,
                total: total,
                compileLog: null,
                runLog: runLog
            ));
        }
    }
}
