using System.Threading.Tasks;

namespace Quiz_Application_College.Services.Coding
{
    public class CodeRunRequest
    {
        public string Language { get; set; } = "python";
        public string SourceCode { get; set; } = "";
        public string Stdin { get; set; } = "";
        public string ExpectedOutput { get; set; } = "";
        public int? LanguageIdOverride { get; set; } = null; // optional
    }

    public class CodeRunResult
    {
        public bool Succeeded { get; set; }
        public string Stdout { get; set; } = "";
        public string Stderr { get; set; } = "";
        public string CompileOutput { get; set; } = "";
        public double? TimeSec { get; set; }
        public int? MemoryKb { get; set; }
        public string Status { get; set; } = ""; // e.g. "Accepted", "Compilation Error", etc.
    }

    public interface ICodeRunner
    {
        Task<CodeRunResult> RunAsync(CodeRunRequest req);
        bool IsEnabled { get; }
    }
}
