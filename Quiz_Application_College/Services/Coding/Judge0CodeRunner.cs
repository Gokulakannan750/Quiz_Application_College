using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Quiz_Application_College.Services.Coding
{
    public class Judge0CodeRunner : ICodeRunner
    {
        private readonly HttpClient _http;
        private readonly string? _baseUrl;
        private readonly string? _apiKey;
        private readonly bool _wait;
        private readonly IDictionary<string, int> _languageIds;

        public bool IsEnabled => !string.IsNullOrWhiteSpace(_baseUrl);

        public Judge0CodeRunner(HttpClient http, IConfiguration cfg)
        {
            _http = http;
            var sec = cfg.GetSection("Judge0");
            _baseUrl = sec["BaseUrl"];
            _apiKey = sec["ApiKey"];
            _wait = bool.TryParse(sec["Wait"], out var w) ? w : true;
            _languageIds = sec.GetSection("LanguageIds").Get<Dictionary<string, int>>() ?? new();
        }

        public async Task<CodeRunResult> RunAsync(CodeRunRequest req)
        {
            if (!IsEnabled)
            {
                return new CodeRunResult
                {
                    Succeeded = false,
                    Status = "RunnerDisabled",
                    Stderr = "Judge0 BaseUrl not configured."
                };
            }

            var langKey = (req.Language ?? "python").Trim().ToLowerInvariant();
            int languageId = req.LanguageIdOverride ?? (_languageIds.TryGetValue(langKey, out var id) ? id : 71); // default python

            var url = $"{_baseUrl.TrimEnd('/')}/submissions?base64_encoded=false&wait={_wait.ToString().ToLower()}";
            using var message = new HttpRequestMessage(HttpMethod.Post, url);
            if (!string.IsNullOrWhiteSpace(_apiKey))
                message.Headers.Add("X-Auth-Token", _apiKey);

            var body = new
            {
                source_code = req.SourceCode,
                language_id = languageId,
                stdin = req.Stdin,
                expected_output = string.IsNullOrWhiteSpace(req.ExpectedOutput) ? null : req.ExpectedOutput
            };

            message.Content = JsonContent.Create(body);

            using var resp = await _http.SendAsync(message);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string status = root.TryGetProperty("status", out var st) && st.TryGetProperty("description", out var sd) ? sd.GetString() ?? "" : "";
            string stdout = root.TryGetProperty("stdout", out var so) ? so.GetString() ?? "" : "";
            string stderr = root.TryGetProperty("stderr", out var se) ? se.GetString() ?? "" : "";
            string compile = root.TryGetProperty("compile_output", out var co) ? co.GetString() ?? "" : "";
            double? time = root.TryGetProperty("time", out var ti) && double.TryParse(ti.GetString(), out var tf) ? tf : null;
            int? mem = root.TryGetProperty("memory", out var me) && me.TryGetInt32(out var mi) ? mi : null;

            bool ok = status.Equals("Accepted", StringComparison.OrdinalIgnoreCase) ||
                      (string.IsNullOrEmpty(stderr) && string.IsNullOrEmpty(compile) && !string.IsNullOrEmpty(stdout));

            return new CodeRunResult
            {
                Succeeded = ok,
                Status = status,
                Stdout = stdout ?? "",
                Stderr = stderr ?? "",
                CompileOutput = compile ?? "",
                TimeSec = time,
                MemoryKb = mem
            };
        }
    }
}
