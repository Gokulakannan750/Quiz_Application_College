using System.Net.Http;
using System.Text;
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

        public Judge0CodeRunner(HttpClient http, IConfiguration config)
        {
            _http = http;

            var section = config.GetSection("Judge0");
            _baseUrl = section["BaseUrl"];
            _apiKey = section["ApiKey"];

            _wait = bool.TryParse(section["Wait"], out var w) ? w : true;

            _languageIds = section
                .GetSection("LanguageIds")
                .Get<Dictionary<string, int>>() ?? new Dictionary<string, int>();

            if (!string.IsNullOrWhiteSpace(_baseUrl))
            {
                // Ensure we have trailing slash once
                _http.BaseAddress = new Uri(_baseUrl.TrimEnd('/') + "/");
            }

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                // If your Judge0 instance uses a different header, change this
                _http.DefaultRequestHeaders.Add("X-Auth-Token", _apiKey);
            }
        }

        public async Task<CodeRunResult> RunAsync(CodeRunRequest req)
        {
            if (!IsEnabled)
            {
                return new CodeRunResult
                {
                    Succeeded = false,
                    Status = "RunnerDisabled",
                    Stderr = "Judge0 BaseUrl is not configured."
                };
            }

            int languageId = ResolveLanguageId(req);

            // --- Base64 encode fields as Judge0 requests ---
            string srcB64 = ToBase64(req.SourceCode ?? string.Empty);
            string stdinB64 = ToBase64(req.Stdin ?? string.Empty);
            string expectedB64 = ToBase64(req.ExpectedOutput ?? string.Empty);

            var payload = new
            {
                language_id = languageId,
                source_code = srcB64,
                stdin = stdinB64,
                expected_output = expectedB64
            };

            string query = $"submissions?base64_encoded=true&wait={_wait.ToString().ToLowerInvariant()}";

            string json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.PostAsync(query, content);

            // Read body ALWAYS – errors are in here
            string respBody = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                var msg = $"Judge0 returned {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {respBody}";
                throw new HttpRequestException(msg);
            }

            using var doc = JsonDocument.Parse(respBody);
            var root = doc.RootElement;

            string status = "";
            if (root.TryGetProperty("status", out var st) &&
                st.TryGetProperty("description", out var sd))
            {
                status = sd.GetString() ?? "";
            }

            // Judge0 returns these base64-encoded when base64_encoded=true
            string stdout = "";
            if (root.TryGetProperty("stdout", out var so))
            {
                stdout = FromBase64(so.GetString());
            }

            string stderr = "";
            if (root.TryGetProperty("stderr", out var se))
            {
                stderr = FromBase64(se.GetString());
            }

            string compileOutput = "";
            if (root.TryGetProperty("compile_output", out var co))
            {
                compileOutput = FromBase64(co.GetString());
            }

            double? time = null;
            if (root.TryGetProperty("time", out var ti))
            {
                if (ti.ValueKind == JsonValueKind.String &&
                    double.TryParse(ti.GetString(), out var tf))
                {
                    time = tf;
                }
                else if (ti.ValueKind == JsonValueKind.Number &&
                         ti.TryGetDouble(out var tf2))
                {
                    time = tf2;
                }
            }

            int? memory = null;
            if (root.TryGetProperty("memory", out var me) &&
                me.ValueKind == JsonValueKind.Number &&
                me.TryGetInt32(out var mi))
            {
                memory = mi;
            }

            bool ok = status.Equals("Accepted", StringComparison.OrdinalIgnoreCase);

            return new CodeRunResult
            {
                Succeeded = ok,
                Status = status,
                Stdout = stdout,
                Stderr = stderr,
                CompileOutput = compileOutput,
                TimeSec = time,
                MemoryKb = memory
            };
        }

        private int ResolveLanguageId(CodeRunRequest req)
        {
            if (req.LanguageIdOverride.HasValue)
                return req.LanguageIdOverride.Value;

            if (!string.IsNullOrWhiteSpace(req.Language))
            {
                var key = req.Language.ToLowerInvariant().Trim();
                if (_languageIds.TryGetValue(key, out var id))
                    return id;
            }

            // fallback: python if present, else 71 (Python 3 in common Judge0 configs)
            if (_languageIds.TryGetValue("python", out var py))
                return py;

            return 71;
        }

        private static string ToBase64(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            return Convert.ToBase64String(bytes);
        }

        private static string FromBase64(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            try
            {
                var bytes = Convert.FromBase64String(value);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // If it wasn't actually base64 for some reason, just return as-is
                return value;
            }
        }
    }
}
